using System.Text.Json;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Repositories.Genki;

namespace Genki.Studio;

public sealed partial class StudioCoordinator
{
    private async Task RunJobAsync(StudioJob job, CancellationToken cancellationToken)
    {
        var config = job.Configuration; var request = job.Request;
        var store = new BatchStore(config.StateRoot);
        using var lease = store.Lock(); // Shared with the command line; never run two writers over checkpoints.
        var fullVocabulary = workspace.Validate(config, request.Kind is "generate" or "tag");
        var vocabulary = request.Words is null ? fullVocabulary : new GenkiVocabulary(request.Words.Select(fullVocabulary.Resolve));
        var schemas = request.SchemaIds.Select(id => workspace.Catalog.Schemas.Single(s => s.Id == id)).ToArray();
        if (request.Kind == "publish")
        {
            StudioWorkspace.ValidatePublication(config);
            Activity(job, "Validating completed questions before publication");
            cancellationToken.ThrowIfCancellationRequested();
            var count = QuestionBankPublisher.Publish(store, workspace.Catalog, fullVocabulary, config.PublishPath!);
            Report(job, new() { Completed = count }); Activity(job, $"Published {count} questions to {config.PublishPath}"); return;
        }
        if (!job.ResetApplied || job.RetryFailedOnResume)
        {
            ResetSelected(job, store, fullVocabulary, cancellationToken);
            lock (gate) { job.ResetApplied = true; job.RetryFailedOnResume = false; Save(job); }
        }
        if (request.Kind == "scan")
        {
            await ScanAsync(job, store, fullVocabulary, vocabulary, schemas, cancellationToken); return;
        }
        await using var session = models.Create(config.Models);
        BatchSummary summary;
        if (request.Kind == "tag")
        {
            var tagger = new SemanticTagger(fullVocabulary, workspace.Catalog.Registry, store, session);
            tagger.ExportMapping(); // A reset must remove old derived annotations even if inference is interrupted.
            summary = await tagger.RunAsync(Options(config, request), request.Words, request.TagIds,
                cancellationToken, text => Activity(job, text), s => Report(job, s));
            // Tagger reports request failures as they happen; recovered attempts must not fail the finished job.
            summary.Failed = store.ReadAll<TaggingState>("tags")
                .Where(s => request.Words is null || request.Words.Contains(s.Word))
                .Sum(s => s.Evaluations.LongCount(p => (request.TagIds is null || request.TagIds.Contains(p.Key)) && p.Value.Status == "failed"));
        }
        else
        {
            var annotations = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json")) ?? [];
            var enumerator = new CandidateEnumerator(workspace.Catalog, vocabulary, annotations.Where(a => vocabulary.Contains(a.Word)));
            summary = await new QuestionPipeline(workspace.Catalog, vocabulary, enumerator, store, session)
                .RunAsync(schemas, Options(config, request), cancellationToken, text => Activity(job, text), s => Report(job, s));
        }
        Report(job, summary);
        Activity(job, summary.Paused ? "Run budget reached. Resume to continue uncovered work." : "Traversal finished. Review results before publication.");
    }
    private void ResetSelected(StudioJob job, BatchStore store, GenkiVocabulary vocabulary, CancellationToken cancellationToken)
    {
        var request = job.Request;
        var mode = job.RetryFailedOnResume ? "retry-failed" : request.Mode;
        if (mode == "resume" || request.Kind is "scan" or "publish") return;
        var selectedWords = request.Words?.ToHashSet();
        long changed = 0;
        Activity(job, mode == "regenerate" ? "Archiving checkpoints in the selected scope" : "Resetting failed attempts; successful stages are retained");
        void Archive(string area, string identity)
        {
            var source = store.FilePath(area, identity);
            var directory = Path.Combine(store.Root, "studio-archive", job.Id, area);
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, Path.GetFileName(source));
            if (!File.Exists(target)) File.Copy(source, target);
            lock (gate) { job.ArchiveDirectory = Path.Combine(store.Root, "studio-archive", job.Id); }
        }
        if (request.Kind == "generate")
        {
            foreach (var state in store.ReadAll<QuestionState>("questions"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!request.SchemaIds.Contains(state.SchemaId)) continue;
                if (selectedWords is not null)
                {
                    if (!state.Stages.TryGetValue("A-candidate", out var element)) continue;
                    var candidate = element.Deserialize<Candidate>(GenkiJson.Options);
                    if (candidate is null || !candidate.RequiredWords.All(selectedWords.Contains)) continue;
                }
                if (mode == "regenerate")
                {
                    Archive("questions", state.CandidateId);
                    File.Delete(store.FilePath("questions", state.CandidateId));
                }
                else
                {
                    if (state.Status != "failed") continue;
                    if (state.ActiveStage is not null) state.Attempts[state.ActiveStage] = 0;
                    state.Status = "pending"; state.Reason = null; store.Save("questions", state.CandidateId, state);
                }
                changed++;
            }
        }
        else if (request.Kind == "tag")
        {
            if (mode == "regenerate")
            {
                // Invalidate derived positives first, including when cancellation interrupts a reset.
                var mappingPath = Path.Combine(store.Root, "word-tags.json");
                var annotations = BatchStore.ReadFile<WordTags[]>(mappingPath) ?? [];
                BatchStore.WriteAtomic(mappingPath, annotations.Where(a => selectedWords is not null && !selectedWords.Contains(a.Word)).ToArray());
            }
            foreach (var state in store.ReadAll<TaggingState>("tags"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selectedWords is not null && !selectedWords.Contains(state.Word)) continue;
                var ids = request.TagIds ?? state.Evaluations.Keys.ToArray();
                if (mode == "regenerate")
                {
                    Archive("tags", state.Word.ToString());
                    if (request.TagIds is null) { File.Delete(store.FilePath("tags", state.Word.ToString())); changed++; continue; }
                    if (state.WordFingerprint != vocabulary.Resolve(state.Word).Fingerprint)
                        throw new InvalidDataException($"{state.Word} changed. Regenerate all tags for that word.");
                    foreach (var id in ids) state.Evaluations.Remove(id);
                }
                else foreach (var id in ids)
                    if (state.Evaluations.TryGetValue(id, out var evaluation) && evaluation.Status == "failed")
                    { evaluation.Status = "pending"; evaluation.Attempts = 0; evaluation.Reason = null; }
                store.Save("tags", state.Word.ToString(), state); changed++;
            }
        }
        Activity(job, $"Prepared {changed} saved records; published bank unchanged.");
    }
    private async Task ScanAsync(StudioJob job, BatchStore store, GenkiVocabulary fullVocabulary, GenkiVocabulary vocabulary,
        IReadOnlyList<GenkiSchema> schemas, CancellationToken cancellationToken)
    {
        var annotations = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json")) ?? [];
        var fingerprint = StudioWorkspace.Fingerprint(workspace.Catalog, fullVocabulary, annotations);
        var enumerator = new CandidateEnumerator(workspace.Catalog, vocabulary, annotations.Where(a => vocabulary.Contains(a.Word)));
        var options = Options(job.Configuration, job.Request); var timer = System.Diagnostics.Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.TimeBudget.HasValue) deadline.CancelAfter(options.TimeBudget.Value);
        var summary = new BatchSummary();
        foreach (var schema in schemas)
        {
            long count = 0, unseen = 0; var complete = false; var reachedLimit = false;
            try
            {
                Activity(job, $"Counting {schema.Id}; no models are used");
                foreach (var candidate in enumerator.Enumerate(schema, deadline.Token))
                {
                    if (options.Limit.HasValue && summary.Examined >= options.Limit.Value) { reachedLimit = true; break; }
                    count++; summary.Examined++;
                    if (store.Read<QuestionState>("questions", candidate.Id) is null) unseen++; else summary.Reused++;
                    if (count % 100 == 0) { Report(job, summary); await Task.Yield(); }
                }
                complete = !reachedLimit;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
            { reachedLimit = true; }
            finally
            {
                store.Save("studio-coverage", schema.Id, new SchemaCoverage(schema.Id, Scope(job.Request), fingerprint,
                    count, unseen, complete, DateTimeOffset.UtcNow));
                Report(job, summary);
            }
            if (reachedLimit)
            { Activity(job, "Partial scan saved. Increase the scan budget to count further; unvisited combinations remain unknown."); return; }
        }
        Activity(job, $"Coverage counted for {schemas.Count} selected patterns in {timer.Elapsed.TotalSeconds:F1}s. No questions generated.");
    }
}
