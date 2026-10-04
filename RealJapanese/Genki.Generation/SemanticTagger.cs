using System.Diagnostics;
using System.Text.Json;
using DataLoaders.Models.Genki;
using Repositories.Genki;

namespace Genki.Generation;

public sealed class TagEvaluation
{
    public string Status { get; set; } = "pending";
    public string DefinitionFingerprint { get; set; } = "";
    public int Attempts { get; set; }
    public string? Reason { get; set; }
    public string? ModelProvenance { get; set; }
}
public sealed class TaggingState
{
    public required WordRef Word { get; init; }
    public required string WordFingerprint { get; init; }
    public Dictionary<string,TagEvaluation> Evaluations { get; init; } = new(StringComparer.Ordinal);
    public bool NeedsReview { get; set; }
    public string? ReviewReason { get; set; }
}
public sealed record TagDecision(string TagId, string Outcome, string Reason);
public sealed record TagResponse(bool Ambiguous, string Reason, IReadOnlyList<TagDecision> Evaluations);

/// <summary>Retains positive/negative word×tag coverage separately from the clean mapping.</summary>
public sealed class SemanticTagger(GenkiVocabulary vocabulary, SemanticRegistry registry, BatchStore store, IGenkiModels models)
{
    private const string Prompt = "You annotate an existing Japanese dictionary entry, not a newly invented sense. " +
        "Classify ONLY its stated meanings against EACH supplied tag definition. Existing collection and conjugation type are authoritative. " +
        "Parents mean classification inheritance, not association. Flag incompatible senses as ambiguous. Unknown is not false. " +
        "Return strict JSON {ambiguous:bool,reason:string,evaluations:[{tagId:string,outcome:'matches'|'does-not-match'|'uncertain',reason:string}]}. " +
        "Include exactly every requested tag once. A child match implies every ancestor matches. No markdown.";
    public async Task<BatchSummary> RunAsync(BatchOptions options, IEnumerable<WordRef>? words = null,
        IEnumerable<string>? tagIds = null, CancellationToken cancellationToken = default, Action<string>? progress = null)
    {
        options.Validate(); var timer = Stopwatch.StartNew(); var summary = new BatchSummary();
        var requestedCancellation = cancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.TimeBudget.HasValue) deadline.CancelAfter(options.TimeBudget.Value);
        cancellationToken = deadline.Token;
        try
        {
        var targets = (words ?? vocabulary.Entries.Select(x => x.Ref)).Distinct().OrderBy(x => x.ToString(), StringComparer.Ordinal);
        var tags = (tagIds ?? registry.Tags.Keys).Distinct().Order(StringComparer.Ordinal).ToArray();
        foreach (var id in tags) if (!registry.Tags.ContainsKey(id)) throw new InvalidDataException($"Unknown tag {id}.");
        foreach (var reference in targets)
        {
            var word = vocabulary.Resolve(reference);
            var state = store.Read<TaggingState>("tags", reference.ToString()) ?? new() { Word = reference, WordFingerprint = word.Fingerprint };
            if (state.WordFingerprint != word.Fingerprint) throw new InvalidDataException($"Stale annotation for {reference}; reset its checkpoint and regenerate affected questions.");
            foreach (var evaluation in state.Evaluations.Values.Where(e => e.Status == "in-progress"))
            { evaluation.Status = "pending"; evaluation.Attempts = Math.Max(0, evaluation.Attempts - 1); }
            summary.Reused += tags.Count(id => state.Evaluations.TryGetValue(id, out var e) && e.Status is "matches" or "does-not-match");
            foreach (var id in tags)
            {
                var fingerprint = DefinitionFingerprint(id);
                if (state.Evaluations.TryGetValue(id, out var evaluation) && evaluation.DefinitionFingerprint != fingerprint)
                    throw new InvalidDataException($"Changed tag definition {id}; reset affected evaluations before continuing.");
            }
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pending = tags.Where(id => !state.Evaluations.TryGetValue(id, out var e) ||
                    (e.Status is "pending" or "in-progress" or "failed") && e.Attempts < options.MaxAttempts)
                    .Take(options.TagGroupSize).ToArray();
                if (pending.Length == 0) break;
                if (options.Limit.HasValue && summary.Examined >= options.Limit || options.TimeBudget.HasValue && timer.Elapsed >= options.TimeBudget)
                { summary.Paused = true; ExportMapping(); return summary; }
                foreach (var id in pending)
                {
                    if (!state.Evaluations.TryGetValue(id, out var evaluation)) state.Evaluations[id] = evaluation = new() { DefinitionFingerprint = DefinitionFingerprint(id) };
                    evaluation.Status = "in-progress"; evaluation.Attempts++; evaluation.ModelProvenance = models.Provenance;
                }
                store.Save("tags", reference.ToString(), state); summary.Examined++;
                try
                {
                    var input = new { word = word.ModelInput, tags = pending.Select(id => registry.Tags[id]),
                        ancestors = registry.Expand(pending).Except(pending).Select(id => registry.Tags[id]) };
                    var raw = await models.QwenAsync("semantic-tags", Prompt, JsonSerializer.Serialize(input, GenkiJson.Compact), cancellationToken);
                    var response = JsonSerializer.Deserialize<TagResponse>(raw, GenkiJson.Options) ?? throw new InvalidDataException("Empty tag response.");
                    if (response.Evaluations is null || response.Evaluations.Count != pending.Length ||
                        response.Evaluations.Any(x => x is null) ||
                        !response.Evaluations.Select(x => x.TagId).ToHashSet(StringComparer.Ordinal).SetEquals(pending) ||
                        response.Evaluations.Any(x => x.Outcome is not ("matches" or "does-not-match" or "uncertain") || string.IsNullOrWhiteSpace(x.Reason)))
                        throw new InvalidDataException("The response did not evaluate exactly the requested tag group.");
                    foreach (var decision in response.Evaluations)
                    {
                        var evaluation = state.Evaluations[decision.TagId];
                        evaluation.Status = response.Ambiguous ? "uncertain" : decision.Outcome;
                        evaluation.Reason = response.Ambiguous ? response.Reason : decision.Reason;
                    }
                    summary.Completed += pending.Length;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    foreach (var id in pending) { state.Evaluations[id].Status = "pending"; state.Evaluations[id].Attempts--; }
                    store.Save("tags", reference.ToString(), state); ExportMapping(); throw;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    foreach (var id in pending) { state.Evaluations[id].Status = "failed"; state.Evaluations[id].Reason = error.Message; }
                    summary.Failed++;
                }
                ReviewContradictions(state); store.Save("tags", reference.ToString(), state);
                progress?.Invoke($"{reference}: {string.Join(", ", pending.Select(id => $"{id}={state.Evaluations[id].Status}"))}");
                if (options.DelayMilliseconds > 0) await Task.Delay(options.DelayMilliseconds, cancellationToken);
            }
            ReviewContradictions(state); store.Save("tags", reference.ToString(), state);
            if (state.NeedsReview) summary.NeedsReview++;
        }
        ExportMapping(); return summary;
        }
        catch (OperationCanceledException) when (!requestedCancellation.IsCancellationRequested && deadline.IsCancellationRequested)
        { summary.Paused = true; ExportMapping(); return summary; }
    }
    private string DefinitionFingerprint(string id) => GenkiJson.Fingerprint(registry.Expand([id]).Order(StringComparer.Ordinal).Select(x => registry.Tags[x]));
    private void ReviewContradictions(TaggingState state)
    {
        var matches = state.Evaluations.Where(x => x.Value.Status == "matches").Select(x => x.Key).ToArray();
        var inherited = registry.Expand(matches);
        var contradictions = state.Evaluations.Where(x => x.Value.Status == "does-not-match" && inherited.Contains(x.Key)).Select(x => x.Key).ToArray();
        state.NeedsReview = contradictions.Length > 0 || state.Evaluations.Values.Any(x => x.Status == "uncertain");
        state.ReviewReason = contradictions.Length > 0 ? "Child matches contradict negative ancestors: " + string.Join(", ", contradictions)
            : state.NeedsReview ? "Ambiguous or uncertain classification." : null;
    }
    public IReadOnlyList<WordTags> ExportMapping()
    {
        var result = new List<WordTags>();
        foreach (var state in store.ReadAll<TaggingState>("tags"))
        {
            if (!vocabulary.Contains(state.Word) || vocabulary.Resolve(state.Word).Fingerprint != state.WordFingerprint)
                throw new InvalidDataException($"Stale/missing vocabulary in annotation {state.Word}.");
            foreach (var pair in state.Evaluations)
                if (!registry.Tags.ContainsKey(pair.Key) || pair.Value.DefinitionFingerprint != DefinitionFingerprint(pair.Key))
                    throw new InvalidDataException($"Stale tag {pair.Key}; reset affected state.");
            ReviewContradictions(state);
            if (state.NeedsReview) continue;
            result.Add(new() { Word = state.Word, WordFingerprint = state.WordFingerprint,
                DirectTags = registry.MostSpecific(state.Evaluations.Where(x => x.Value.Status == "matches").Select(x => x.Key)) });
        }
        BatchStore.WriteAtomic(Path.Combine(store.Root, "word-tags.json"), result.OrderBy(x => x.Word.ToString(), StringComparer.Ordinal).ToArray());
        return result;
    }
}
