namespace AudioStudio;

/// <summary>Persists each completed request before starting another; paused runs hold no model.</summary>
public sealed class GenerationService(StudioStore store, WordCatalog catalog, IGenerationBackend backend)
{
    public static readonly string[] Engines = ["bert", "fish", "irodori"];
    public static readonly string[] Qualities = ClipFormats.Qualities;
    private readonly SemaphoreSlim work = new(1);
    private readonly object gate = new();
    private readonly CancellationTokenSource shutdown = new();
    private Execution? active;

    private sealed class Execution(string id, CancellationToken shutdownToken) : IDisposable
    {
        public string Id { get; } = id;
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        public bool PauseRequested { get; set; }
        public Task Task { get; set; } = Task.CompletedTask;
        public void Dispose() => Cancellation.Dispose();
    }
    private sealed class PauseCheckpoint : Exception;

    public string? ActiveId { get { lock (gate) return active?.Id; } }
    public async Task StopAsync(CancellationToken token)
    {
        shutdown.Cancel();
        Task? task;
        lock (gate) task = active?.Task;
        if (task is not null) await task.WaitAsync(token);
    }
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
        await AcquireAsync();
        bool launched = false;
        try
        {
            var run = new StudioRun
            {
                Words = words, Engines = Engines.Where(input.Engines.Contains).ToArray(), OutputQuality = input.OutputQuality, AllQualities = true,
                ReferenceId = reference?.Id, ReferenceName = reference?.Name, ReferenceSnapshot = reference,
                Notes = ["Automatic transcripts screen recognition, not pitch accent or voice similarity. Listen before exporting.",
                    "BERT uses a fixed voice. Fish and Irodori can use the reference."]
            };
            foreach (string engine in run.Engines)
                foreach (var word in words) run.Clips.Add(new() { Key = word.Key, Engine = engine });
            backend.Prepare(run);
            store.AddRun(run);
            Launch(run);
            launched = true;
            return store.GetRun(run.Id);
        }
        catch { if (!launched) work.Release(); throw; }
    }
    public async Task<StudioRun> ResumeAsync(string id)
    {
        await AcquireAsync();
        bool launched = false;
        try
        {
            lock (gate)
            {
                var run = store.GetRun(id);
                if (!run.CanResume) throw new StudioException("Only paused or interrupted runs can continue.", 409);
                foreach (var word in run.Words) catalog.VerifyCurrent(word);
                foreach (var clip in run.Clips.Where(c => c.File is not null))
                {
                    foreach (var variant in ClipFormats.Available(run, clip).Values)
                    {
                        string file = StudioStore.SafePath(store.RunFolder(id), variant.File);
                        if (!File.Exists(file) || StudioStore.Hash(file) != variant.Sha256)
                            throw new StudioException("A completed clip is missing or changed. Restore it or start a new run.", 409);
                    }
                }
                if (!run.FormatUpgrade) backend.Prepare(run);
                store.UpdateRun(id, r => { r.SpeechSettings = run.SpeechSettings; r.Status = "Queued"; r.Phase = "Continuing from saved checkpoints"; });
                Launch(store.GetRun(id));
                launched = true;
                return store.GetRun(id);
            }
        }
        catch { if (!launched) work.Release(); throw; }
    }
    private async Task AcquireAsync()
    {
        if (shutdown.IsCancellationRequested) throw new StudioException("The studio is shutting down.", 409);
        if (!await work.WaitAsync(0)) throw new StudioException("Another generation or reference operation is running.", 409);
    }
    public async Task<StudioRun> PrepareFormatsAsync(string id)
    {
        await AcquireAsync();
        bool launched = false;
        try
        {
            lock (gate)
            {
                var run = store.GetRun(id);
                if (run.IsActive || run.CanResume) throw new StudioException("Finish or resume the run before preparing qualities.", 409);
                if (!run.Clips.Any(c => c.File is not null)) throw new StudioException("This run has no recordings to convert.", 409);
                store.UpdateRun(id, r =>
                {
                    r.AllQualities = true; r.FormatUpgrade = true; r.Status = "Queued";
                    r.Phase = "Preparing all qualities from saved raw recordings";
                });
                Launch(store.GetRun(id));
                launched = true;
                return store.GetRun(id);
            }
        }
        catch { if (!launched) work.Release(); throw; }
    }
    private void Launch(StudioRun run)
    {
        lock (gate)
        {
            var execution = new Execution(run.Id, shutdown.Token);
            active = execution;
            execution.Task = Task.Run(() => ExecuteAsync(run, execution));
        }
    }
    public void Pause(string id)
    {
        lock (gate)
        {
            if (active?.Id != id || !store.GetRun(id).IsActive) throw new StudioException("This run is not active.", 409);
            active.PauseRequested = true;
            store.UpdateRun(id, r => { r.Status = "Pausing"; r.Phase = "Pausing after the current request is saved"; });
        }
    }
    public void Cancel(string id)
    {
        lock (gate)
        {
            if (active?.Id == id && store.GetRun(id).IsActive) { active.Cancellation.Cancel(); return; }
            if (!store.GetRun(id).CanResume) throw new StudioException("This run cannot be cancelled.", 409);
            store.UpdateRun(id, r => { r.Status = "Cancelled"; r.Phase = "Cancelled; completed clips are preserved"; });
        }
    }
    public async Task<T> ReferenceOperation<T>(Func<CancellationToken, Task<T>> operation, CancellationToken requestCancellation)
    {
        if (!await work.WaitAsync(0, requestCancellation)) throw new StudioException("Wait for the current generation or reference operation to finish.", 409);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, requestCancellation);
        try { return await operation(cancel.Token); }
        finally { work.Release(); }
    }
    private void Checkpoint(Execution execution)
    {
        lock (gate)
        {
            execution.Cancellation.Token.ThrowIfCancellationRequested();
            if (execution.PauseRequested) throw new PauseCheckpoint();
        }
    }
    private void Phase(Execution execution, string status, string phase)
    {
        lock (gate)
            if (!execution.PauseRequested) store.UpdateRun(execution.Id, r => { r.Status = status; r.Phase = phase; });
    }
    private async Task ExecuteAsync(StudioRun run, Execution execution)
    {
        string endStatus = "Failed", endPhase = "Stopped";
        var token = execution.Cancellation.Token;
        try
        {
            foreach (string engine in run.Engines)
            {
                if (run.FormatUpgrade) break;
                var pending = run.Clips.Where(c => c.Engine == engine && c.File is null && c.Status != "Failed").ToArray();
                if (pending.Length == 0) continue;
                Checkpoint(execution);
                Phase(execution, "Generating", $"Generating with {engine}");
                await using var voice = backend.OpenVoice(run, engine);
                int failures = 0;
                foreach (var clip in pending)
                {
                    Checkpoint(execution);
                    void Update(Action<StudioClip> update) => store.UpdateRun(run.Id, r => update(r.Clips.Single(c => c.Key == clip.Key && c.Engine == engine)));
                    if (failures >= 3)
                    {
                        Update(c => { c.Status = "Failed"; c.Error = "Model stopped after three consecutive failures. See earlier errors and retry in a new run."; });
                        continue;
                    }
                    Update(c => c.Status = "Generating");
                    try
                    {
                        var audio = await voice.GenerateAsync(run.Words.Single(w => w.Key == clip.Key), token);
                        Update(c =>
                        {
                            c.Status = "Generated"; c.File = audio.File; c.RawFile = audio.RawFile; c.Seconds = audio.Seconds;
                            c.Sha256 = audio.Sha256; c.PitchSource = audio.PitchSource; c.Error = null;
                            c.AudioUrl = $"/api/runs/{run.Id}/audio?key={Uri.EscapeDataString(clip.Key)}&engine={engine}";
                            c.RawUrl = c.AudioUrl + "&raw=true";
                        });
                        failures = 0;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    { failures++; Update(c => { c.Status = "Failed"; c.Error = e.Message; }); }
                    Checkpoint(execution);
                }
            }
            foreach (var clip in store.GetRun(run.Id).Clips.Where(c => c.File is not null))
            {
                Checkpoint(execution);
                if (!run.AllQualities || ClipFormats.Qualities.All(clip.Variants.ContainsKey)) continue;
                Phase(execution, "Generating", "Preparing all four audio qualities");
                try
                {
                    var variants = await backend.ConvertFormatsAsync(run, clip, token);
                    store.UpdateRun(run.Id, r => r.Clips.Single(c => c.Key == clip.Key && c.Engine == clip.Engine).Variants = variants);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { store.UpdateRun(run.Id, r => r.Notes.Add($"Qualities for {clip.Engine} {clip.Key}: {e.Message}")); }
                Checkpoint(execution);
            }
            var checks = store.GetRun(run.Id).Clips.Where(c => c.File is not null)
                .SelectMany(c => ClipFormats.Available(run, c).Where(v => v.Value.Validation == "Pending")
                    .Select(v => (Clip: c, Quality: v.Key, Audio: v.Value))).ToArray();
            if (checks.Length > 0)
            {
                Checkpoint(execution);
                Phase(execution, "Checking", "Transcribing generated clips with AI library Whisper");
                await using var checker = backend.OpenChecks(run);
                foreach (var check in checks)
                {
                    Checkpoint(execution);
                    var clip = StudioJson.Clone(check.Clip);
                    clip.File = check.Audio.File;
                    CheckedAudio result;
                    try { result = await checker.CheckAsync(clip, token); }
                    catch (Exception e) when (e is not OperationCanceledException) { result = new(null, e.Message, "AI library Whisper small"); }
                    store.UpdateRun(run.Id, r =>
                    {
                        var saved = r.Clips.Single(c => c.Key == clip.Key && c.Engine == clip.Engine);
                        string validation = WordCatalog.ValidateTranscript(r.Words.Single(w => w.Key == clip.Key), result.Text, result.Error);
                        if (saved.Variants.TryGetValue(check.Quality, out var variant))
                        {
                            variant.Transcript = result.Text; variant.Error = result.Error; variant.Validation = validation;
                            variant.TranscriptSource = result.Source; variant.TranscriptionDiagnostics = result.Diagnostics;
                        }
                        if (check.Quality == r.OutputQuality)
                        {
                            saved.Transcript = result.Text; saved.Error = result.Error; saved.Validation = validation;
                            saved.TranscriptSource = result.Source; saved.TranscriptionDiagnostics = result.Diagnostics;
                        }
                    });
                    Checkpoint(execution);
                }
            }
            var completed = store.GetRun(run.Id);
            endStatus = completed.Clips.Any(c => c.File is null
                || (completed.AllQualities && c.Variants.Count != Qualities.Length)
                || ClipFormats.Available(completed, c).Values.Any(v => v.Validation is "Pending" or "Check failed")) ? "Partial" : "Completed";
            endPhase = "Ready for listening and selection";
        }
        catch (PauseCheckpoint) { endStatus = "Paused"; endPhase = "Paused; completed clips and checks are saved"; }
        catch (OperationCanceledException)
        {
            endStatus = shutdown.IsCancellationRequested ? "Interrupted" : "Cancelled";
            endPhase = shutdown.IsCancellationRequested ? "Interrupted by application shutdown" : "Cancelled; completed clips are preserved";
        }
        catch (Exception e) { store.UpdateRun(run.Id, r => r.Notes.Add(e.Message)); }
        finally
        {
            // Models have been disposed before advertising a resumable, idle state.
            lock (gate)
            {
                try { store.UpdateRun(run.Id, r => { r.Status = endStatus; r.Phase = endPhase; if (endStatus is "Completed" or "Partial") r.FormatUpgrade = false; }); }
                finally
                {
                    active = null;
                    execution.Dispose();
                    work.Release();
                }
            }
        }
    }
}
