using System.Globalization;
using System.Net;
using AudioStudio;
using GenerateAudioUsingTTS;
using Microsoft.AspNetCore.Http.Features;

bool preparePitch = args.Contains("--prepare-pitch");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--prepare-pitch").ToArray());
builder.WebHost.UseUrls(builder.Configuration["AudioStudio:Url"] ?? "http://127.0.0.1:5291");
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 256L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 257L * 1024 * 1024);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = StudioJson.Options.PropertyNamingPolicy;
    o.SerializerOptions.Encoder = StudioJson.Options.Encoder;
});
var options = StudioOptions.Load(builder.Configuration);
Directory.CreateDirectory(options.WorkRoot);
// A second process must not recover or overwrite a running process's manifests.
using var ownership = new FileStream(Path.Combine(options.WorkRoot, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
Synthesis.ConfigureRuntime(options.RuntimeRoot, options.Speech.LibraryRoot);
Environment.SetEnvironmentVariable("HF_HOME", Path.Combine(options.RuntimeRoot, "huggingface"));
var catalog = new WordCatalog(options.DataRoot);
if (preparePitch)
{
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    await PitchProfiles.PrepareAsync(options, catalog, cancel.Token);
    return;
}
var pitch = new PitchProfiles(options.PitchRoot);
var store = new StudioStore(options.WorkRoot, options.ExportRoot, catalog);
var media = options.CreateMedia();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(catalog);
builder.Services.AddSingleton(store);
builder.Services.AddSingleton(media);
builder.Services.AddSingleton(pitch);
builder.Services.AddSingleton<IGenerationBackend, LibraryGenerationBackend>();
builder.Services.AddSingleton<GenerationService>();
builder.Services.AddHostedService<GenerationHost>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        if (context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip))
            throw new StudioException("Audio Studio is available only on this computer.", 403);
        if (context.Request.Host.Host is not ("127.0.0.1" or "localhost" or "[::1]" or "::1"))
            throw new StudioException("Use the local Audio Studio address.", 403);
        if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            if (context.Request.Headers["X-Audio-Studio"] != "1") throw new StudioException("Missing local request header.", 403);
            if (context.Request.Headers.Origin.FirstOrDefault() is { } origin && origin != $"{context.Request.Scheme}://{context.Request.Host}")
                throw new StudioException("Cross-origin changes are not allowed.", 403);
        }
        context.Response.Headers.XContentTypeOptions = "nosniff";
        await next(context);
    }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        int status = error is StudioException studio ? studio.Status : error is ArgumentException or InvalidDataException ? 400 : error is FileNotFoundException ? 404 : 500;
        if (status == 500) app.Logger.LogError(error, "Audio Studio request failed");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = error.Message,
            requiresAcknowledgement = error is StudioException { UnselectedCount: not null },
            unselectedCount = (error as StudioException)?.UnselectedCount });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", (GenerationService jobs) => new { ready = true, words = catalog.Words.Count, activeRunId = jobs.ActiveId });
app.MapGet("/api/catalog", () =>
{
    var exported = store.Exports().ToDictionary(e => e.Word.Key);
    return catalog.Words.Select(w => new { w.Key, w.Dataset, w.Id, w.Group, w.Japanese, w.Kana, w.English, w.SpokenKana, w.SpeechNote,
        hasAudio = exported.TryGetValue(w.Key, out var e) && e.Word == w && File.Exists(store.ExportAudio(w.Key)),
        pitchStatus = pitch.For(w)?.Profile is not null ? "Stored BERT estimate" : "Model-inferred" });
});
app.MapGet("/api/runs", () => store.Runs().Select(r => new { r.Id, r.CreatedAt, r.Status, r.WordCount, r.Engines, r.OutputQuality, r.ReferenceName, r.Progress, r.IsActive, r.CanResume }));
app.MapGet("/api/runs/{id}", (string id) => store.GetRun(id));
app.MapPost("/api/runs", (GenerationInput input, GenerationService jobs) => jobs.StartAsync(input));
app.MapPost("/api/runs/{id}/pause", (string id, GenerationService jobs) => { jobs.Pause(id); return Results.Ok(); });
app.MapPost("/api/runs/{id}/resume", (string id, GenerationService jobs) => jobs.ResumeAsync(id));
app.MapPost("/api/runs/{id}/cancel", (string id, GenerationService jobs) => { jobs.Cancel(id); return Results.Ok(); });
app.MapPut("/api/runs/{id}/selections", (string id, SelectionInput input) => store.Select(id, input.Choices));
app.MapPost("/api/runs/{id}/export", (string id, ExportInput input) => store.Export(id, input.AcknowledgeUnselected));
app.MapGet("/api/runs/{id}/audio", (string id, string key, string engine, bool? raw) =>
{
    var clip = store.GetRun(id).Clips.SingleOrDefault(c => c.Key == key && c.Engine == engine) ?? throw new StudioException("Clip not found.", 404);
    string? relative = raw == true ? clip.RawFile : clip.File;
    return AudioFile(relative is null ? throw new StudioException("Clip is not ready.", 404) : StudioStore.SafePath(store.RunFolder(id), relative));
});
app.MapGet("/api/exports", () => store.Exports());
app.MapGet("/api/exports/audio", (string key) => AudioFile(store.ExportAudio(key)));
app.MapGet("/api/references", () => store.References());
app.MapGet("/api/references/{id}/audio", (string id) => { _ = store.Reference(id); return AudioFile(store.ReferenceAudio(id)); });
app.MapPut("/api/references/{id}", (string id, ReferenceEdit edit) =>
{
    if (string.IsNullOrWhiteSpace(edit.Name) || edit.Name.Length > 120 || string.IsNullOrWhiteSpace(edit.Transcript) || edit.Transcript.Length > 5000)
        throw new StudioException("Provide a name (up to 120 characters) and transcript (up to 5,000 characters).");
    var reference = store.Reference(id) with { Name = edit.Name.Trim(), Transcript = edit.Transcript.Trim(), TranscriptSource = "User edited" };
    store.SaveReference(reference);
    return reference;
});
app.MapPost("/api/references", async (HttpRequest request, GenerationService jobs, CancellationToken token) =>
{
    return await jobs.ReferenceOperation(async ct =>
    {
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? throw new StudioException("Select an audio or video recording.");
        string name = form["name"].ToString().Trim();
        if (name.Length is < 1 or > 120) throw new StudioException("Provide a name up to 120 characters.");
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".wav", ".webm", ".mp3", ".m4a", ".aac", ".mp4", ".ogg", ".opus", ".flac" }.Contains(extension))
            throw new StudioException("Use WAV, WebM, MP3, M4A/AAC, MP4, Ogg/Opus, or FLAC.");
        if (file.Length is <= 0 or > 256L * 1024 * 1024) throw new StudioException("The recording must be between 1 byte and 256 MB.");
        if (!double.TryParse(form["startSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out double start)
            || !double.TryParse(form["durationSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out double duration))
            throw new StudioException("Specify the excerpt start and duration in seconds.");
        string id = Guid.NewGuid().ToString("N");
        string folder = store.ReferenceFolder(id);
        Directory.CreateDirectory(folder);
        string upload = Path.Combine(folder, "source" + extension);
        bool saved = false;
        try
        {
            await using (var stream = new FileStream(upload, FileMode.CreateNew)) await file.CopyToAsync(stream, ct);
            double seconds = await media.CreateReferenceAsync(upload, store.ReferenceAudio(id), start, duration, ct);
            await using var transcriber = options.CreateTranscriber();
            var result = await transcriber.TranscribeOneAsync(id, store.ReferenceAudio(id), true, ct);
            if (result.Error is not null) throw new StudioException("Reference transcription failed: " + result.Error);
            var reference = new ReferenceItem(id, name, result.Text ?? "", "AI library Whisper small; not human-verified", seconds,
                Path.GetFileName(file.FileName) + $" ({start:0.##}–{start + seconds:0.##}s)", DateTimeOffset.UtcNow);
            store.SaveReference(reference);
            saved = true;
            return reference;
        }
        finally
        {
            if (File.Exists(upload)) File.Delete(upload);
            if (!saved && Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }, token);
});
app.MapFallbackToFile("index.html");

// The original sample has complete phrases, one speaker and no cut-off word at
// its boundaries. Persist it once; later launches preserve edits and new clips.
if (!store.References().Any() && builder.Configuration["AudioStudio:SkipDefaultReference"] != "true")
{
    string id = Guid.NewGuid().ToString("N");
    Directory.CreateDirectory(store.ReferenceFolder(id));
    try
    {
        double seconds = await media.CreateReferenceAsync(options.Speech.ReferenceAudio, store.ReferenceAudio(id),
            options.Speech.ReferenceStartSeconds, options.Speech.ReferenceDurationSeconds, CancellationToken.None);
        store.SaveReference(new(id, "Podcast 41 · calm conversation", options.Speech.ReferenceTranscript ?? "",
            options.Speech.ReferenceTranscriptSource, seconds,
            Path.GetFileName(options.Speech.ReferenceAudio) + $" ({options.Speech.ReferenceStartSeconds:0.##}s)", DateTimeOffset.UtcNow));
    }
    catch (Exception e)
    {
        if (Directory.Exists(store.ReferenceFolder(id))) Directory.Delete(store.ReferenceFolder(id), true);
        app.Logger.LogWarning(e, "Default reference could not be prepared; upload a recording in References");
    }
}
await app.RunAsync();

static IResult AudioFile(string path)
{
    if (!File.Exists(path)) throw new StudioException("Audio file is missing.", 404);
    string contentType = Path.GetExtension(path).ToLowerInvariant() switch
    { ".wav" => "audio/wav", ".mp3" => "audio/mpeg", ".opus" => "audio/ogg", _ => "application/octet-stream" };
    return Results.File(path, contentType, enableRangeProcessing: true);
}
