using System.Text.Json;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Repositories.Genki;

namespace Genki.Studio;

public sealed partial class StudioCoordinator
{
    public async Task RefreshAsync()
    {
        await refresh.WaitAsync();
        try
        {
            Genki.Inference.GenkiConfiguration config;
            lock (gate) config = configuration;
            var result = await Task.Run(() => ReadDashboard(config));
            lock (gate)
            {
                if (config != configuration) return;
                rows = result.Rows; words = result.Words; errors = result.Errors; tagCoverage = result.Tags; refreshedAt = DateTimeOffset.UtcNow;
            }
        }
        finally { refresh.Release(); }
    }
    private (StudioSchemaRow[] Rows, StudioWord[] Words, string[] Errors, StudioTagCoverage[] Tags) ReadDashboard(Genki.Inference.GenkiConfiguration config)
    {
        var problems = new List<string>();
        if (loadError is not null) problems.Add(loadError);
        GenkiVocabulary? vocabulary = null; CandidateEnumerator? enumerator = null; var fingerprint = "";
        var counts = new Dictionary<string, Dictionary<string,long>>(StringComparer.Ordinal);
        var coverage = new Dictionary<string,SchemaCoverage>();
        var tagStates = new Dictionary<WordRef,TaggingState>();
        try
        {
            vocabulary = workspace.Validate(config, false);
            var store = new BatchStore(config.StateRoot);
            foreach (var state in store.ReadAll<QuestionState>("questions"))
            {
                if (!counts.TryGetValue(state.SchemaId, out var bucket)) counts[state.SchemaId] = bucket = new();
                bucket[state.Status] = bucket.GetValueOrDefault(state.Status) + 1;
            }
            foreach (var item in store.ReadAll<SchemaCoverage>("studio-coverage")) coverage[item.SchemaId] = item;
            foreach (var item in store.ReadAll<TaggingState>("tags")) tagStates[item.Word] = item;
            var annotations = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json")) ?? [];
            enumerator = new(workspace.Catalog, vocabulary, annotations);
            fingerprint = StudioWorkspace.Fingerprint(workspace.Catalog, vocabulary, annotations);
        }
        catch (Exception error) { problems.Add(error.Message); }
        try { workspace.Validate(config, true); }
        catch (Exception error) { if (!problems.Contains(error.Message)) problems.Add(error.Message); }
        DateTimeOffset latestGeneration;
        lock (gate) latestGeneration = jobs.Where(j => j.Request.Kind is "generate" or "tag").Select(j => j.UpdatedAt).DefaultIfEmpty().Max();
        var result = workspace.Catalog.Schemas.Select(schema =>
        {
            var lesson = workspace.Catalog.Lessons.Single(l => l.GrammarPoints.Any(p => p.Id == schema.GrammarPointId));
            var bucket = counts.GetValueOrDefault(schema.Id) ?? [];
            var scan = coverage.GetValueOrDefault(schema.Id);
            var upper = enumerator?.UpperBound(schema).ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown";
            return new StudioSchemaRow(schema.Id, lesson.Number, lesson.Title, schema.GrammarPointId, workspace.Catalog.Point(schema.GrammarPointId).Title,
                schema.Register, Pattern(schema.Segments), upper, bucket.Values.Sum(), bucket.GetValueOrDefault("completed"), bucket.GetValueOrDefault("failed"),
                bucket.GetValueOrDefault("needs-review"), bucket.GetValueOrDefault("rejected"), bucket.GetValueOrDefault("pending") + bucket.GetValueOrDefault("in-progress"),
                "Not queued", scan, scan is not null && (scan.InputsFingerprint != fingerprint || scan.UpdatedAt < latestGeneration));
        }).ToArray();
        var tagRows = vocabulary?.Entries.Select(word =>
        {
            var state = tagStates.GetValueOrDefault(word.Ref);
            var values = state?.Evaluations.Where(p => workspace.Catalog.Registry.Tags.ContainsKey(p.Key)).Select(p => p.Value).ToArray() ?? [];
            var stale = state is not null && state.WordFingerprint != word.Fingerprint;
            return new StudioTagCoverage(word.Ref, values.LongCount(v => v.Status == "matches"), values.LongCount(v => v.Status == "does-not-match"),
                values.LongCount(v => v.Status == "uncertain"), values.LongCount(v => v.Status == "failed"), values.LongCount(v => v.Status is "pending" or "in-progress"),
                workspace.Catalog.Registry.Tags.Count - values.LongLength, stale || state?.NeedsReview == true,
                stale ? "Source word changed; regenerate its annotations." : state?.ReviewReason);
        }).ToArray() ?? [];
        return (result, vocabulary?.Entries.Select(w => new StudioWord(w.Ref, w.Word.Japanese, w.Word.Kana, w.Word.English)).ToArray() ?? [], problems.ToArray(), tagRows);
    }
    private static string Pattern(IEnumerable<GenkiSegment> segments) => string.Concat(segments.Select(s => s.Kind switch
    {
        "literal" => s.Text, "fixed" => $"[{s.Word}:{s.Form}]", "slot" => $"{{{s.Name}:{(s.FormChoices.Count > 0 ? string.Join("/", s.FormChoices) : s.Form)}}}",
        "optional" => "(" + Pattern(s.Children) + ")", _ => "?"
    }));
    public Task<StudioReviewPage> ReviewAsync(string? schemaId, string? status, int skip = 0)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        string root; lock (gate) root = configuration.StateRoot;
        return Task.Run(() =>
        {
            var states = new BatchStore(root).ReadAll<QuestionState>("questions")
                .Where(s => (string.IsNullOrEmpty(schemaId) || s.SchemaId == schemaId) && (string.IsNullOrEmpty(status) || s.Status == status))
                .Skip(skip).Take(26).ToArray();
            var items = states.Take(25).Select(s =>
            {
                Candidate? candidate = s.Stages.TryGetValue("A-candidate", out var element) ? element.Deserialize<Candidate>(GenkiJson.Options) : null;
                return new StudioQuestion(s.CandidateId, s.SchemaId, s.Status, s.ActiveStage, s.Reason, s.Question?.English,
                    s.Question?.Answers.FirstOrDefault()?.Japanese ?? candidate?.Japanese, s.Question?.Answers.FirstOrDefault()?.Kana ?? candidate?.ExpectedKana,
                    s.Question?.Answers.Select(a => a.Japanese + " · " + a.Kana).ToArray() ?? []);
            }).ToArray();
            return new StudioReviewPage(items, states.Length > 25);
        });
    }
    public Task<string> DetailsAsync(string candidateId)
    {
        string root; lock (gate) root = configuration.StateRoot;
        return Task.Run(() => JsonSerializer.Serialize(new BatchStore(root).Read<QuestionState>("questions", candidateId)
            ?? throw new InvalidDataException("Question checkpoint not found."), GenkiJson.Options));
    }
}
