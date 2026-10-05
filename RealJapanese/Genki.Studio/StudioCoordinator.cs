using System.Text.Json;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Genki.Inference;
using Repositories.Genki;

namespace Genki.Studio;

/// <summary>One durable batch queue per workspace. Jobs resume only after an explicit action after restart.</summary>
public sealed partial class StudioCoordinator : BackgroundService
{
    private readonly StudioWorkspace workspace;
    private readonly IStudioModelFactory models;
    private readonly object gate = new();
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly SemaphoreSlim wake = new(0);
    private readonly SemaphoreSlim refresh = new(1, 1);
    private readonly List<StudioJob> jobs = [];
    private GenkiConfiguration configuration;
    private IDisposable? studioLease;
    private CancellationTokenSource? activeCancellation;
    private string? activeId;
    private string stopDisposition = "paused";
    private string? loadError;
    private string[] errors = [];
    private StudioSchemaRow[] rows = [];
    private StudioWord[] words = [];
    private StudioTagCoverage[] tagCoverage = [];
    private DateTimeOffset? refreshedAt;

    public StudioCoordinator(StudioWorkspace workspace, IStudioModelFactory models)
    {
        this.workspace = workspace; this.models = models;
        try { configuration = workspace.Load(); }
        catch (Exception error) { configuration = workspace.Defaults.Resolve(Path.GetDirectoryName(workspace.ConfigurationFile)!); loadError = error.Message; }
        try { OpenWorkspace(); }
        catch (Exception error) { loadError = error.Message; }
    }
    private BatchStore Store => new(configuration.StateRoot);
    private bool Busy => jobs.Any(j => j.Status is "queued" or "running" or "stopping");
    private void OpenWorkspace()
    {
        studioLease = new FileStream(Path.Combine(Store.Root, "studio.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        jobs.Clear();
        foreach (var job in Store.ReadAll<StudioJob>("studio-jobs").OrderBy(j => j.CreatedAt))
        {
            if (job.Status is "queued" or "running" or "stopping")
            { job.Status = "paused"; job.Activity = "Application restarted. Resume when ready."; Save(job); }
            jobs.Add(job);
        }
    }
    public StudioSnapshot Snapshot()
    {
        lock (gate)
        {
            var views = jobs.OrderByDescending(j => j.CreatedAt).Select(j => new StudioJobView(j.Id, j.Request.Kind,
                j.Request.Mode, j.Status, j.CreatedAt, j.UpdatedAt, j.Request.SchemaIds.ToArray(), Scope(j.Request),
                j.Request.Limit, j.Request.Minutes, j.Request.Exhaustive, j.Activity, Copy(j.Summary), j.Error, j.ArchiveDirectory)).ToArray();
            var currentRows = rows.Select(r => r with { QueueStatus = QueueStatus(r.Id) }).ToArray();
            return new(StudioWorkspace.ToSettings(configuration), workspace.ConfigurationFile, models.IsAvailable,
                Busy, errors.ToArray(), currentRows, words, workspace.Catalog.Registry.Tags.Values.ToArray(), views, refreshedAt) { TagCoverage = tagCoverage };
        }
    }
    private string QueueStatus(string schema)
    {
        var relevant = jobs.Where(j => j.Request.Kind == "generate" && j.Request.SchemaIds.Contains(schema)).ToArray();
        if (relevant.Any(j => j.Status is "running" or "stopping")) return "Running";
        if (relevant.Any(j => j.Status == "queued")) return "Queued";
        if (relevant.Any(j => j.Status == "paused")) return "Paused";
        return relevant.Length == 0 ? "Not queued" : "Previously queued";
    }
    public async Task SaveSettingsAsync(StudioSettings settings)
    {
        await commands.WaitAsync();
        try
        {
            lock (gate) if (Busy) throw new InvalidOperationException("Pause or cancel queued work before changing configuration.");
            var next = StudioWorkspace.FromSettings(settings).Resolve(Path.GetDirectoryName(workspace.ConfigurationFile)!);
            workspace.Validate(next, false); StudioWorkspace.ValidatePublication(next);
            // Validate numeric model settings even if files have not been installed yet.
            AiLibraryModels.ValidateOptions(next.Models with { ModelsDirectory = string.IsNullOrWhiteSpace(next.Models.ModelsDirectory) ? next.StateRoot : next.Models.ModelsDirectory });
            lock (gate)
            {
                // Resume can be requested while validation runs outside this lock.
                if (Busy) throw new InvalidOperationException("Pause or cancel queued work before changing configuration.");
                if (studioLease is null || !string.Equals(configuration.StateRoot, next.StateRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var nextStore = new BatchStore(next.StateRoot);
                    var lease = new FileStream(Path.Combine(nextStore.Root, "studio.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    try
                    {
                        var recovered = nextStore.ReadAll<StudioJob>("studio-jobs").OrderBy(j => j.CreatedAt).ToArray();
                        foreach (var job in recovered.Where(j => j.Status is "queued" or "running" or "stopping"))
                        { job.Status = "paused"; job.Activity = "Workspace opened. Resume when ready."; Save(job); }
                        workspace.Save(next);
                        studioLease?.Dispose(); studioLease = lease;
                        configuration = next; jobs.Clear(); jobs.AddRange(recovered);
                    }
                    catch { lease.Dispose(); throw; }
                }
                else { workspace.Save(next); configuration = next; }
                loadError = null;
            }
            await RefreshAsync();
        }
        finally { commands.Release(); }
    }
    public async Task<string> EnqueueAsync(StudioRequest request)
    {
        await commands.WaitAsync();
        try
        {
            GenkiConfiguration config;
            lock (gate)
            {
                if (loadError is not null || studioLease is null) throw new InvalidOperationException(loadError ?? "Workspace is not open.");
                config = configuration;
            }
            if (request.Kind is not ("generate" or "tag" or "scan" or "publish") || request.Mode is not ("resume" or "retry-failed" or "regenerate"))
                throw new InvalidDataException("Choose a supported operation and resume mode.");
            if (request.Kind == "scan" && request.Mode != "resume") throw new InvalidDataException("Coverage scans do not reset generated work.");
            if (request.Mode == "regenerate" && !request.ConfirmRegeneration) throw new InvalidOperationException("Confirm regeneration of the selected scope first.");
            if (request.Kind == "publish" && !request.ConfirmPublication) throw new InvalidOperationException("Review the bank destination and confirm publication first.");
            if (request.Kind == "publish" && request.ExpectedPublishPath is not null &&
                !string.Equals(request.ExpectedPublishPath, config.PublishPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The publication destination changed. Review publication again before confirming.");
            if (!request.Exhaustive && request.Kind != "publish" && request.Limit is null && request.Minutes is null)
                throw new InvalidDataException("Set a run limit/time budget or explicitly choose exhaustive processing.");
            if (request.Minutes.HasValue && (!double.IsFinite(request.Minutes.Value) || request.Minutes <= 0)) throw new InvalidDataException("Minutes must be positive.");
            Options(config, request).Validate();
            var inference = request.Kind is "tag" or "generate";
            if (inference && !models.IsAvailable) throw new InvalidOperationException("Model support is not built. Restart the launcher with the AiLibrary folder configured.");
            var vocabulary = workspace.Validate(config, inference);
            if (request.Kind is "generate" or "scan" && request.SchemaIds.Length == 0) throw new InvalidDataException("Select at least one grammar pattern.");
            foreach (var id in request.SchemaIds) if (!workspace.Catalog.Schemas.Any(s => s.Id == id)) throw new InvalidDataException($"Unknown schema {id}.");
            if (request.Words is { Length: 0 }) throw new InvalidDataException("Select vocabulary or choose all vocabulary.");
            foreach (var word in request.Words ?? []) vocabulary.Resolve(word);
            if (request.TagIds is { Length: 0 }) throw new InvalidDataException("Select tags or choose all tags.");
            foreach (var tag in request.TagIds ?? []) if (!workspace.Catalog.Registry.Tags.ContainsKey(tag)) throw new InvalidDataException($"Unknown tag {tag}.");
            if (request.Kind == "publish") StudioWorkspace.ValidatePublication(config);
            var normalized = request with
            {
                SchemaIds = request.SchemaIds.Distinct().Order(StringComparer.Ordinal).ToArray(),
                Words = request.Words?.Distinct().OrderBy(w => w.ToString(), StringComparer.Ordinal).ToArray(),
                TagIds = request.TagIds?.Distinct().Order(StringComparer.Ordinal).ToArray(),
                Limit = request.Exhaustive ? null : request.Limit, Minutes = request.Exhaustive ? null : request.Minutes
            };
            lock (gate)
            {
                var job = new StudioJob { Id = Guid.NewGuid().ToString("N"), Request = normalized, Configuration = config };
                Save(job); jobs.Add(job); wake.Release(); return job.Id;
            }
        }
        finally { commands.Release(); }
    }
    public Task PauseAsync(string id) => StopJob(id, "paused");
    public Task CancelAsync(string id) => StopJob(id, "cancelled");
    private Task StopJob(string id, string disposition)
    {
        lock (gate)
        {
            var job = FindJob(id);
            if (job.Status is "completed" or "cancelled" or "failed") throw new InvalidOperationException("This job is no longer running or queued.");
            if (activeId == id && job.Request.Kind == "publish") throw new InvalidOperationException("Publication finishes atomically; wait for its result.");
            if (activeId == id) { stopDisposition = disposition; job.Status = "stopping"; activeCancellation!.Cancel(); }
            else job.Status = disposition;
            job.Activity = activeId == id ? "Stopping at the current checkpoint…" : "Work retained; no bank changed.";
            Save(job);
        }
        return Task.CompletedTask;
    }
    public Task ResumeAsync(string id)
    {
        lock (gate)
        {
            var job = FindJob(id);
            if (job.Status is not ("paused" or "failed")) throw new InvalidOperationException("Only paused or failed jobs can resume.");
            if (job.Status == "failed") job.RetryFailedOnResume = true;
            job.Status = "queued"; job.Error = null; job.Activity = "Waiting to resume saved stages"; Save(job); wake.Release();
        }
        return Task.CompletedTask;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await wake.WaitAsync(stoppingToken);
                StudioJob? job;
                lock (gate)
                {
                    job = jobs.FirstOrDefault(j => j.Status == "queued");
                    if (job is null) continue;
                    activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    activeId = job.Id; stopDisposition = "paused";
                    job.Status = "running"; job.Error = null; job.Summary = new(); job.Activity = "Opening saved workspace"; Save(job);
                }
                try
                {
                    await RunJobAsync(job, activeCancellation.Token);
                    lock (gate) { job.Status = activeCancellation.IsCancellationRequested ? stopDisposition : job.Summary.Paused ? "paused" : job.Summary.Failed > 0 ? "failed" : "completed"; }
                }
                catch (OperationCanceledException) when (activeCancellation.IsCancellationRequested)
                { lock (gate) { job.Status = stopDisposition; job.Activity = "Stopped. Successful stages are retained."; } }
                catch (Exception error)
                { lock (gate) { job.Status = "failed"; job.Error = error.Message; job.Activity = "Needs attention"; } }
                finally
                {
                    lock (gate) { Save(job); activeId = null; activeCancellation.Dispose(); activeCancellation = null; }
                    await RefreshAsync();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private StudioJob FindJob(string id) => jobs.SingleOrDefault(j => j.Id == id) ?? throw new InvalidDataException("Unknown job.");
    private void Save(StudioJob job) { job.UpdatedAt = DateTimeOffset.UtcNow; new BatchStore(job.Configuration.StateRoot).Save("studio-jobs", job.Id, job); }
    private void Activity(StudioJob job, string text) { lock (gate) { job.Activity = text; Save(job); } }
    private void Report(StudioJob job, BatchSummary summary) { lock (gate) { job.Summary = Copy(summary); Save(job); } }
    private static string Scope(StudioRequest request) => request.Words is null ? "All source vocabulary" : $"{request.Words.Length} selected words";
    private static BatchOptions Options(GenkiConfiguration c, StudioRequest r) => c.Batch with
    { Limit = r.Exhaustive ? null : r.Limit, TimeBudget = r.Exhaustive || r.Minutes is null ? null : TimeSpan.FromMinutes(r.Minutes.Value) };
    private static BatchSummary Copy(BatchSummary s) => new() { Examined = s.Examined, Reused = s.Reused, Completed = s.Completed,
        Rejected = s.Rejected, NeedsReview = s.NeedsReview, Failed = s.Failed, Paused = s.Paused };
    public override void Dispose() { base.Dispose(); studioLease?.Dispose(); }
}
