using AiLibrary;
using GenerateAudioUsingTTS;

namespace AudioStudio;

public sealed class GenerationService(StudioOptions options, StudioStore store, WordCatalog catalog, MediaTools media, PitchProfiles pitch) : IHostedService
{
    public static readonly string[] Engines = ["bert", "fish", "irodori"];
    public static readonly string[] Qualities = ["OriginalWav", "Wav24000Hz16BitMono", "Mp3Mono64Kbps", "OpusMono32Kbps"];
    private readonly SemaphoreSlim work = new(1);
    private readonly object gate = new();
    private CancellationTokenSource? currentCancellation;
    private string? currentId;
    private Task? currentTask;
    private readonly CancellationTokenSource shutdown = new();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        shutdown.Cancel();
        Task? task;
        lock (gate) { currentCancellation?.Cancel(); task = currentTask; }
        if (task is not null) await task.WaitAsync(cancellationToken);
    }
    public string? ActiveId { get { lock (gate) return currentId; } }

    public async Task<StudioRun> StartAsync(GenerationInput input)
    {
        if (input.WordKeys is null || input.WordKeys.Length == 0 || input.WordKeys.Length > catalog.Words.Count)
            throw new StudioException("Select at least one catalog word.");
        if (input.WordKeys.Distinct().Count() != input.WordKeys.Length) throw new StudioException("Duplicate word selections.");
        if (input.Engines is null || input.Engines.Length is < 1 or > 3 || input.Engines.Distinct().Count() != input.Engines.Length
            || input.Engines.Any(e => !Engines.Contains(e))) throw new StudioException("Select one to three available models.");
        if (!Qualities.Contains(input.OutputQuality)) throw new StudioException("Unknown output quality.");
        var words = input.WordKeys.Select(catalog.Get).ToList();
        var reference = input.ReferenceId is null ? null : store.Reference(input.ReferenceId);
        if (input.Engines.Contains("fish") && reference is not null && string.IsNullOrWhiteSpace(reference.Transcript))
            throw new StudioException("Fish needs a transcript for the reference. Edit it in References first.");
        if (!await work.WaitAsync(0)) throw new StudioException("Another generation or reference operation is running.", 409);
        try
        {
            var run = new StudioRun
            {
                Words = words, Engines = Engines.Where(input.Engines.Contains).ToArray(), OutputQuality = input.OutputQuality,
                ReferenceId = reference?.Id, ReferenceName = reference?.Name, ReferenceSnapshot = reference,
                Notes = ["Automatic transcripts screen recognition, not pitch accent or voice similarity. Listen before exporting.",
                    "BERT uses a fixed voice. Fish and Irodori can use the reference."]
            };
            foreach (string engine in run.Engines)
                foreach (var word in words) run.Clips.Add(new() { Key = word.Key, Engine = engine });
            store.AddRun(run);
            var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            lock (gate)
            {
                currentId = run.Id;
                currentCancellation = cancel;
                currentTask = Task.Run(async () =>
                {
                    try { await GenerateAsync(run, cancel.Token); }
                    finally
                    {
                        lock (gate) { currentId = null; currentCancellation = null; }
                        cancel.Dispose();
                        work.Release();
                    }
                });
            }
            return store.GetRun(run.Id);
        }
        catch { work.Release(); throw; }
    }

    public void Cancel(string id)
    {
        lock (gate)
        {
            if (currentId != id) throw new StudioException("This run is not active.", 409);
            currentCancellation!.Cancel();
        }
    }
    public async Task<T> ReferenceOperation<T>(Func<CancellationToken, Task<T>> operation, CancellationToken requestCancellation)
    {
        if (!await work.WaitAsync(0, requestCancellation)) throw new StudioException("Wait for the current generation or reference operation to finish.", 409);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, requestCancellation);
        try { return await operation(cancel.Token); }
        finally { work.Release(); }
    }

    private async Task GenerateAsync(StudioRun run, CancellationToken token)
    {
        try
        {
            string runFolder = store.RunFolder(run.Id);
            string? reference = null;
            if (run.ReferenceId is not null)
            {
                reference = Path.Combine(runFolder, "reference.wav");
                File.Copy(store.ReferenceAudio(run.ReferenceId), reference, false);
            }
            var settings = options.Speech with { ReferenceTranscript = run.ReferenceSnapshot?.Transcript };
            store.UpdateRun(run.Id, r => r.Status = "Generating");
            foreach (string engine in run.Engines)
            {
                token.ThrowIfCancellationRequested();
                store.UpdateRun(run.Id, r => r.Phase = $"Generating with {engine}");
                // The documented library batch pattern keeps one engine/reference cache warm.
                await using var service = Synthesis.Create(engine, settings);
                int consecutiveFailures = 0;
                foreach (var word in run.Words)
                {
                    token.ThrowIfCancellationRequested();
                    void Update(Action<StudioClip> update) => store.UpdateRun(run.Id, r => update(r.Clips.Single(c => c.Key == word.Key && c.Engine == engine)));
                    if (consecutiveFailures >= 3)
                    {
                        Update(c => { c.Status = "Failed"; c.Error = "Model stopped after three consecutive failures. See earlier errors and retry in a new run."; });
                        continue;
                    }
                    Update(c => c.Status = "Generating");
                    try
                    {
                        string relativeRoot = $"{engine}/{word.Dataset}/{word.Id}";
                        string folder = StudioStore.SafePath(runFolder, relativeRoot);
                        string rawFolder = Path.Combine(folder, "raw");
                        Directory.CreateDirectory(rawFolder);
                        var request = Synthesis.Request(engine, new Verb(word.Id, word.SpokenJapanese, word.SpokenKana, word.English), settings, rawFolder,
                            engine == "bert" ? null : reference);
                        var pronunciation = engine == "bert" ? pitch.For(word)?.Profile : null;
                        if (pronunciation is not null)
                        {
                            request = request with { PronunciationProfile = pronunciation.Validate(), SplitLines = false };
                            StudioStore.AtomicWrite(Path.Combine(folder, "pitch-profile.json"), pronunciation);
                        }
                        // Expressions can be longer than dictionary words; never impose the old
                        // fixed 2.5-second limit on the complete catalog.
                        if (engine == "irodori") request = request with
                        {
                            IrodoriOptions = new IrodoriSpeechOptions { MinSeconds = 0.5, MaxSeconds = Math.Clamp(word.Kana.Length * 0.22 + 1.5, 2.5, 20), DurationScale = 1 },
                            VoiceDescription = "自然で落ち着いた声。指定された日本語だけを明瞭に、普通の速さで発音する。説明や他の言葉を付け加えない。"
                        };
                        if (engine == "fish") request = request with
                        {
                            FishOptions = new FishSpeechOptions { MaxNewTokens = Math.Clamp(word.Kana.Length * 20, 128, 1024), Temperature = 0.65, TopP = 0.7, RepetitionPenalty = 1.2 }
                        };
                        StudioStore.AtomicWrite(Path.Combine(folder, "request.json"), request);
                        await service.SynthesizeSpeechAsync(request, token);
                        string extension = run.OutputQuality switch { "Mp3Mono64Kbps" => ".mp3", "OpusMono32Kbps" => ".opus", _ => ".wav" };
                        string file = relativeRoot + "/clip" + extension;
                        string output = StudioStore.SafePath(runFolder, file);
                        double seconds = await media.ProcessWordAsync(request.OutputFilePath, output, run.OutputQuality, token);
                        Update(c =>
                        {
                            c.Status = "Generated"; c.File = file; c.RawFile = Path.GetRelativePath(runFolder, request.OutputFilePath);
                            c.Seconds = seconds; c.Sha256 = StudioStore.Hash(output);
                            c.PitchSource = pronunciation is not null ? "Stored BERT profile · machine estimate" : "Model-inferred accent";
                            string url = $"/api/runs/{run.Id}/audio?key={Uri.EscapeDataString(word.Key)}&engine={engine}";
                            c.AudioUrl = url; c.RawUrl = url + "&raw=true";
                        });
                        consecutiveFailures = 0;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        consecutiveFailures++;
                        Update(c => { c.Status = "Failed"; c.Error = e.Message; });
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            store.UpdateRun(run.Id, r => { r.Status = "Checking"; r.Phase = "Transcribing generated clips"; });
            var finished = store.GetRun(run.Id);
            var checks = finished.Clips.Where(c => c.File is not null)
                .Select(c => new TranscriptionInput(c.Engine + ":" + c.Key, StudioStore.SafePath(runFolder, c.File!))).ToArray();
            try
            {
                await media.TranscribeAsync(checks, result =>
                {
                    store.UpdateRun(run.Id, r =>
                    {
                        var clip = r.Clips.Single(c => c.Engine + ":" + c.Key == result.Id);
                        clip.Transcript = result.Text;
                        clip.Validation = WordCatalog.ValidateTranscript(r.Words.Single(w => w.Key == clip.Key), result.Text, result.Error);
                        if (result.Error is not null) clip.Error = result.Error;
                    });
                    return Task.CompletedTask;
                }, token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                store.UpdateRun(run.Id, r =>
                {
                    r.Notes.Add("Transcription failed: " + e.Message);
                    foreach (var c in r.Clips.Where(c => c.File is not null && c.Validation == "Pending"))
                    { c.Validation = "Check failed"; c.Error = e.Message; }
                });
            }
            store.UpdateRun(run.Id, r =>
            {
                r.Status = r.Clips.Any(c => c.File is null || c.Validation == "Check failed") ? "Partial" : "Completed";
                r.Phase = "Ready for listening and selection";
            });
        }
        catch (OperationCanceledException)
        {
            store.UpdateRun(run.Id, r => { r.Status = "Cancelled"; r.Phase = "Cancelled; completed clips are preserved"; });
        }
        catch (Exception e)
        {
            store.UpdateRun(run.Id, r => { r.Status = "Failed"; r.Phase = "Stopped"; r.Notes.Add(e.Message); });
        }
    }
}
