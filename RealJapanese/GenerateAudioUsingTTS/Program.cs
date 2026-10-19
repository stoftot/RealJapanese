using System.Text;
using System.Text.Json;
using GenerateAudioUsingTTS;

Console.OutputEncoding = Encoding.UTF8;
if (args.Contains("--help"))
{
    Console.WriteLine("""
        Standalone Japanese verb TTS comparison (IDs 0–10 inclusive by default).
        --settings <json>          Defaults to .tooling/tts-settings.json
        --engines bert,fish,irodori Select one or more engines
        --first-id <n> --last-id <n> Inclusive catalog IDs
        --output <directory>       New directory under .tooling/tts-runs
        --dry-run                  Print plan without starting models or writing files
        --allow-unconditioned      Permit explicitly labeled fallback if reference conditioning fails
        See docs/tts-comparison.md for binary setup and reference preparation.
        """);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Report? report = null;
try
{
    string root = Paths.RepositoryRoot();
    Options options = Options.Parse(args, root);
    Settings settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(options.SettingsPath), Json.Format)
        ?? throw new InvalidDataException("Settings JSON was empty.");
    settings.Validate();
    Paths.RequireOutsideLibrary(options.Output, settings.LibraryRoot);
    var verbs = Verb.Load(Path.Combine(root, "RealJapanese", "Data", "Verbs", "Verbs.json"), options.FirstId, options.LastId);
    if (options.DryRun)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { options, settings, verbs, ExpectedClips = verbs.Count * options.Engines.Length }, Json.Format));
        return 0;
    }
    if (Directory.Exists(options.Output) || File.Exists(options.Output)) throw new IOException("Output already exists; choose a new run directory.");
    Directory.CreateDirectory(options.Output);
    // CreateNew also arbitrates two processes that both observed a nonexistent directory.
    using var ownership = new FileStream(Path.Combine(options.Output, ".run.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    report = new(options, settings, verbs);
    report.Save();
    Synthesis.ConfigureRuntime(Path.Combine(options.Output, "runtime"), settings.LibraryRoot);
    string reference = await ReferenceClip.PrepareAsync(settings, options.Output, cancellation.Token);
    Console.WriteLine($"Generating {verbs.Count * options.Engines.Length} clips in {options.Output}");
    foreach (string engine in options.Engines)
    {
        string? currentReference = engine == "bert" ? null : reference;
        string? fallbackReason = null;
        if (engine == "fish" && string.IsNullOrWhiteSpace(settings.ReferenceTranscript))
        {
            if (options.AllowUnconditioned)
            {
                currentReference = null;
                fallbackReason = "No reference transcript supplied; Fish used an unconditioned voice.";
                report.Notes.Add(fallbackReason);
            }
            else
            {
                foreach (var verb in verbs) report.Samples.Add(new(verb, engine, "not generated", null, null, null, "Fish requires the cropped reference transcript. Supply it or explicitly enable --allow-unconditioned."));
                report.Save();
                continue;
            }
        }
        string rawFolder = Path.Combine(options.Output, engine, "raw");
        Directory.CreateDirectory(rawFolder);
        try
        {
            await using var service = Synthesis.Create(engine, settings);
            foreach (Verb verb in verbs)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                string Mode() => engine == "bert" ? $"fixed {settings.BertVoice} / neutral" : currentReference is not null ? "reference-conditioned" : "unconditioned fallback";
                var request = Synthesis.Request(engine, verb, settings, rawFolder, currentReference);
                Console.WriteLine($"{engine} #{verb.Id:D3} {verb.Japanese} ({verb.Kana}) · {Mode()}");
                try
                {
                    try { await service.SynthesizeSpeechAsync(request, cancellation.Token); }
                    catch (Exception error) when (error is not OperationCanceledException && options.AllowUnconditioned && currentReference is not null)
                    {
                        fallbackReason = $"{engine}: reference synthesis failed; explicitly permitted unconditioned fallback. {error.Message}";
                        report.Notes.Add(fallbackReason);
                        currentReference = null;
                        request = Synthesis.Request(engine, verb, settings, rawFolder, null);
                        Console.WriteLine(fallbackReason);
                        await service.SynthesizeSpeechAsync(request, cancellation.Token);
                    }
                    var (audio, stats) = Audio.PrepareWord(Wave.Read(request.OutputFilePath));
                    string relative = $"{engine}/{verb.Id:D3}.wav";
                    audio.Write(Path.Combine(options.Output, engine, $"{verb.Id:D3}.wav"));
                    report.Samples.Add(new(verb, engine, Mode(), request, relative, stats, fallbackReason));
                    Console.WriteLine($"  Saved {stats.Seconds:0.00}s" + (stats.Warnings.Count > 0 ? $" · {string.Join("; ", stats.Warnings)}" : ""));
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    report.Samples.Add(new(verb, engine, Mode(), request, null, null, error.ToString()));
                    Console.Error.WriteLine($"  Failed: {error.Message}");
                    // A failed worker is torn down by the library. Avoid reloading it for every word
                    // when the first request establishes a startup/environment problem.
                    if (!report.Samples.Any(s => s.Engine == engine && s.File is not null))
                    {
                        foreach (var remaining in verbs.Where(v => v.Id > verb.Id))
                            report.Samples.Add(new(remaining, engine, "not generated", null, null, null,
                                "Engine stopped after its first failed sample. See the first sample's error; rerun this engine after resolving it."));
                        report.Save();
                        break;
                    }
                }
                report.Save();
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            foreach (Verb verb in verbs.Where(v => !report.Samples.Any(s => s.Engine == engine && s.Verb.Id == v.Id)))
                report.Samples.Add(new(verb, engine, "not generated", null, null, null, error.ToString()));
            report.Save();
        }
    }
    int completed = report.Samples.Count(s => s.File is not null);
    report.Status = completed == verbs.Count * options.Engines.Length ? "generated — listening review required" : "partial — see errors";
    report.Save();
    Console.WriteLine($"{completed}/{verbs.Count * options.Engines.Length} saved. Comparison: {Path.Combine(options.Output, "index.html")}");
    return completed == verbs.Count * options.Engines.Length ? 0 : 1;
}
catch (OperationCanceledException)
{
    if (report is not null) { report.Status = "cancelled"; report.Save(); }
    Console.Error.WriteLine("Cancelled. Completed samples remain in the run directory.");
    return 130;
}
catch (Exception error)
{
    if (report is not null) { report.Status = "failed"; report.Notes.Add(error.Message); report.Save(); }
    Console.Error.WriteLine(error.Message);
    return 1;
}
