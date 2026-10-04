using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataLoaders.Models.Genki;
using Repositories.Genki;

namespace Genki.Generation;

public sealed class QuestionState
{
    public required string CandidateId { get; init; }
    public required string InputFingerprint { get; init; }
    public required string SchemaId { get; init; }
    public required string Provenance { get; init; }
    public string Status { get; set; } = "pending";
    public string? ActiveStage { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string,JsonElement> Stages { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string,int> Attempts { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string,string> AlternativeExclusions { get; init; } = new(StringComparer.Ordinal);
    public GenkiQuestion? Question { get; set; }
}
public sealed record Judgment
{
    public required string Outcome { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<string> GrammarIds { get; init; }
}
public sealed record ContextResponse
{
    public string? Setting { get; init; }
    public required Judgment Judgment { get; init; }
}
public sealed record AlternativeProposal
{
    public required string Japanese { get; init; }
    public required IReadOnlyList<GenkiSegment> Segments { get; init; }
    public required IReadOnlyList<string> GrammarIds { get; init; }
}
public sealed record AlternativesResponse
{
    public required IReadOnlyList<AlternativeProposal> Alternatives { get; init; }
}
public sealed record KanaResponse
{
    public required string Outcome { get; init; }
    public required string Kana { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Offline A–H pipeline; successful stages survive failures and no incomplete state is published.</summary>
public sealed class QuestionPipeline(GenkiCatalog catalog, GenkiVocabulary vocabulary, CandidateEnumerator enumerator,
    BatchStore store, IGenkiModels models)
{
    public const string Version = "genki-offline-pipeline-v2";
    private const string Boundary = "Use only the target grammar and explicitly allowed prerequisites. Keep meaning, referents, tense, polarity and register identical. " +
        "No fantastical rescue contexts, no added facts, no Japanese answer leaked in English context. Vocabulary is existing collection-qualified entries, never invent IDs. " +
        "Return strict JSON without markdown. A model judgment is fallible: report uncertainty, never guess approval.";
    private const string JudgmentShape = " Return {outcome:'valid'|'invalid'|'uncertain',reason:string,grammarIds:[string]}. " +
        "List grammar used, including the target. valid means grammatical, coherent, ordinary and natural, with every requirement satisfied.";
    public async Task<BatchSummary> RunAsync(IEnumerable<GenkiSchema> schemas, BatchOptions options, CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        options.Validate(); var summary = new BatchSummary(); var timer = Stopwatch.StartNew();
        var requestedCancellation = cancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.TimeBudget.HasValue) deadline.CancelAfter(options.TimeBudget.Value);
        cancellationToken = deadline.Token;
        try
        {
        foreach (var schema in schemas.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            long candidates = 0;
            foreach (var candidate in enumerator.Enumerate(schema, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested(); candidates++;
                if (options.TimeBudget.HasValue && timer.Elapsed >= options.TimeBudget)
                { summary.Paused = true; return summary; }
                var provenance = GenkiJson.Fingerprint(new { Version, models = models.Provenance, options.AlternativeLimit });
                var state = store.Read<QuestionState>("questions", candidate.Id);
                if (state is not null && (state.InputFingerprint != candidate.InputFingerprint || state.Provenance != provenance))
                    throw new InvalidDataException($"Stale candidate {candidate.Id}; inspect/reset its checkpoint and republish after reprocessing.");
                if (state?.Status == "in-progress")
                {
                    if (state.ActiveStage is not null) state.Attempts[state.ActiveStage] = Math.Max(0, state.Attempts.GetValueOrDefault(state.ActiveStage) - 1);
                    state.Status = "pending"; store.Save("questions", candidate.Id, state);
                }
                if (state?.Status is "completed" or "rejected" or "needs-review") { summary.Reused++; continue; }
                if (state?.Status == "failed" && state.ActiveStage is not null && state.Attempts.GetValueOrDefault(state.ActiveStage) >= options.MaxAttempts)
                { summary.Failed++; continue; }
                if (options.Limit.HasValue && summary.Examined >= options.Limit) { summary.Paused = true; return summary; }
                summary.Examined++;
                state ??= new() { CandidateId = candidate.Id, SchemaId = schema.Id, InputFingerprint = candidate.InputFingerprint, Provenance = provenance };
                await ProcessAsync(candidate, state, options, cancellationToken);
                switch (state.Status)
                {
                    case "completed": summary.Completed++; break;
                    case "rejected": summary.Rejected++; break;
                    case "needs-review": summary.NeedsReview++; break;
                    case "failed": summary.Failed++; break;
                }
                progress?.Invoke($"{schema.Id} {candidate.Id[..12]}: {state.Status} ({state.ActiveStage ?? "ready for publication"})");
                if (options.DelayMilliseconds > 0) await Task.Delay(options.DelayMilliseconds, cancellationToken);
            }
            progress?.Invoke($"{schema.Id}: visited {candidates} eligible combinations{(candidates == 0 ? " (no compatible vocabulary/forms; no coverage claimed)" : "")}.");
        }
        return summary;
        }
        catch (OperationCanceledException) when (!requestedCancellation.IsCancellationRequested && deadline.IsCancellationRequested)
        { summary.Paused = true; return summary; }
    }
    private async Task ProcessAsync(Candidate candidate, QuestionState state, BatchOptions options, CancellationToken cancellationToken)
    {
        var schema = candidate.Schema; var allowed = catalog.AllowedGrammar(schema);
        var grammar = allowed.Order(StringComparer.Ordinal).Select(id => catalog.Point(id)).ToArray();
        var words = candidate.RequiredWords.Select(r => vocabulary.Resolve(r).ModelInput).ToArray();
        var context = new { schema, target = catalog.Point(schema.GrammarPointId), allowedGrammar = grammar,
            vocabulary = words, candidate.Japanese, candidate.Bindings, candidate.Choices };
        void Save() => store.Save("questions", state.CandidateId, state);
        async Task<T> Stage<T>(string name, Func<Task<T>> work)
        {
            if (state.Stages.TryGetValue(name, out var cached)) return cached.Deserialize<T>(GenkiJson.Options)!;
            state.ActiveStage = name;
            while (state.Attempts.GetValueOrDefault(name) < options.MaxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested(); state.Status = "in-progress";
                state.Attempts[name] = state.Attempts.GetValueOrDefault(name) + 1; Save();
                try
                {
                    var result = await work();
                    state.Stages[name] = JsonSerializer.SerializeToElement(result, GenkiJson.Options);
                    state.Reason = null; Save(); return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                { state.Attempts[name]--; state.Status = "pending"; state.Reason = "Cancelled; completed stages retained."; Save(); throw; }
                catch (Exception error) when (error is not OutOfMemoryException)
                { state.Status = "failed"; state.Reason = error.Message; Save(); }
            }
            throw new StageFailureException();
        }
        async Task<T> Ask<T>(string stage, string instructions, object input)
        {
            var raw = await models.QwenAsync(stage, Boundary + " " + instructions, JsonSerializer.Serialize(input, GenkiJson.Compact), cancellationToken);
            return JsonSerializer.Deserialize<T>(raw, GenkiJson.Options) ?? throw new InvalidDataException("Empty structured model response.");
        }
        async Task<Judgment> Judge(string stage, string instructions, object input)
        {
            var result = await Ask<Judgment>(stage, instructions + JudgmentShape, input);
            ValidateJudgment(result); return result;
        }
        bool Accept(Judgment judgment, bool judgingCanonicalCandidate = false)
        {
            var grammarValid = judgment.GrammarIds.Contains(schema.GrammarPointId) && judgment.GrammarIds.All(allowed.Contains);
            if (judgment.Outcome == "valid" && grammarValid) return true;
            state.Status = judgment.Outcome == "invalid" && judgingCanonicalCandidate ? "rejected" : "needs-review";
            state.Reason = grammarValid ? judgment.Reason : "Judgment uses undeclared grammar or omits the target grammar.";
            Save(); return false;
        }
        try
        {
            // A: preserve both bindings and rendered provenance before any inference.
            await Stage("A-candidate", () => Task.FromResult(candidate));
            var initial = await Stage("B-candidate-validation", () => Judge("candidate-validation",
                "Evaluate this canonical candidate without rewriting it. A context-dependent but plausible sentence may be valid; the next stage establishes its context.", context));
            if (!Accept(initial, judgingCanonicalCandidate: true)) return;
            var setting = await Stage("C-context", async () =>
            {
                var result = await Ask<ContextResponse>("context",
                    "Generate a brief English setting only if needed to establish referents or roles; otherwise setting=null. Recheck the unchanged sentence in that setting. " +
                    "Return {setting:string|null,judgment:{outcome:'valid'|'invalid'|'uncertain',reason:string,grammarIds:[string]}}.", context);
                ValidateJudgment(result.Judgment);
                if (result.Setting?.Length > 600 || result.Setting is not null && Regex.IsMatch(result.Setting, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]"))
                    throw new InvalidDataException("Setting is too long or exposes Japanese answer text.");
                return result;
            });
            if (!Accept(setting.Judgment)) return;
            var english = await Stage("D-translation", async () =>
            {
                var translated = (await models.TranslateAsync(candidate.Japanese, setting.Setting, schema.Register, cancellationToken)).Trim();
                if (string.IsNullOrWhiteSpace(translated) || translated.Length > 2000) throw new InvalidDataException("Empty or oversized LFM translation.");
                return translated;
            });
            var translationCheck = await Stage("D-translation-validation", () => Judge("translation-validation",
                "Check that LFM's English is a single faithful prompt for the canonical Japanese in this setting. It must preserve its intended meaning without translating the setting instructions or adding facts.",
                new { context, setting.Setting, english }));
            if (!Accept(translationCheck)) return;
            var proposals = await Stage("E-alternatives", async () =>
            {
                if (options.AlternativeLimit == 0) return new AlternativesResponse { Alternatives = [] };
                var result = await Ask<AlternativesResponse>("alternatives",
                    $"Propose at most {options.AlternativeLimit} different answers to this SAME English exercise. Zero is valid. Keep canonical vocabulary where possible. " +
                    "A recoverable omission must not remove the target construction. No particle replaced by punctuation. Return {alternatives:[{japanese:string,grammarIds:[string],segments:[...]}]}. " +
                    "Each segment must be {kind:'fixed',word:{wordType:string,id:string},form:string} for an existing lexical word, or {kind:'literal',text:string} using ONLY the supplied grammaticalMaterial. " +
                    "No slots, optional groups, invented vocabulary, hidden lexical literals or unlisted forms. Concatenating segments must exactly reproduce japanese.",
                    new { context, setting.Setting, english, vocabulary = AlternativeVocabulary(candidate),
                        grammaticalMaterial = catalog.Materials.Where(m => allowed.Contains(m.GrammarPointId)),
                        forms = GenkiForms.Names });
                if (result.Alternatives is null || result.Alternatives.Count > options.AlternativeLimit ||
                    result.Alternatives.Any(a => a is null || string.IsNullOrWhiteSpace(a.Japanese) || a.Segments is null ||
                        a.Segments.Count == 0 || a.Segments.Any(s => s is null || s.Children is null || s.FormChoices is null || string.IsNullOrWhiteSpace(s.Form)) ||
                        a.GrammarIds is null || a.GrammarIds.Any(string.IsNullOrWhiteSpace)))
                    throw new InvalidDataException("Malformed alternatives or alternative limit violated.");
                return result;
            });
            var approved = new List<(string Japanese, string Kana, IReadOnlyList<WordRef> Words)>
                { (candidate.Japanese, candidate.ExpectedKana, candidate.RequiredWords) };
            for (var index = 0; index < proposals.Alternatives.Count; index++)
            {
                var proposal = proposals.Alternatives[index]; var prefix = $"F-{index}";
                (string Japanese, string Kana, IReadOnlyList<WordRef> Words, IReadOnlyList<GenkiSegment> Segments) rendered;
                try
                {
                    if (!proposal.GrammarIds.Contains(schema.GrammarPointId) || proposal.GrammarIds.Any(id => !allowed.Contains(id)))
                        throw new InvalidDataException("Alternative does not retain the target/allowed grammar.");
                    ValidateAnswerSegments(proposal.Segments, allowed);
                    rendered = enumerator.Resolve(proposal.Segments);
                    if (proposal.Japanese != rendered.Japanese) throw new InvalidDataException("Alternative text does not match its lexical provenance.");
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException)
                { state.AlternativeExclusions[prefix] = error.Message; Save(); continue; }
                if (approved.Any(a => GenkiExercise.Normalize(a.Japanese) == GenkiExercise.Normalize(rendered.Japanese) &&
                    GenkiExercise.Normalize(a.Kana) == GenkiExercise.Normalize(rendered.Kana) && a.Words.ToHashSet().SetEquals(rendered.Words))) continue;
                var check = await Stage(prefix + "-Japanese", () => Judge("alternative-validation",
                    "Check the proposed answer's Japanese, exact meaning, referents, tense, polarity, register and target grammar against the canonical sentence and the single established English prompt. Context must work for both.",
                    new { context, english, setting.Setting, alternative = proposal, vocabulary = rendered.Words.Select(w => vocabulary.Resolve(w).ModelInput) }));
                if (check.Outcome != "valid" || !check.GrammarIds.Contains(schema.GrammarPointId) || check.GrammarIds.Any(id => !allowed.Contains(id)))
                { state.AlternativeExclusions[prefix] = check.Reason; Save(); continue; }
                var backTranslation = await Stage(prefix + "-back-translation", async () =>
                {
                    var text = (await models.TranslateAsync(proposal.Japanese, setting.Setting, schema.Register, cancellationToken)).Trim();
                    if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Empty alternative back-translation."); return text;
                });
                var equivalence = await Stage(prefix + "-equivalence", () => Judge("equivalence",
                    "Assess full semantic equivalence, using back-translation as additional fallible evidence. Different English wording may be equivalent; identical wording is not proof. The Japanese target construction must survive.",
                    new { context, english, setting.Setting, alternative = proposal.Japanese, backTranslation }));
                if (equivalence.Outcome != "valid" || !equivalence.GrammarIds.Contains(schema.GrammarPointId) || equivalence.GrammarIds.Any(id => !allowed.Contains(id)))
                { state.AlternativeExclusions[prefix] = equivalence.Reason; Save(); continue; }
                approved.Add((rendered.Japanese, rendered.Kana, rendered.Words));
            }
            var answers = new List<GenkiAnswer>();
            for (var index = 0; index < approved.Count; index++)
            {
                var answer = approved[index];
                var reading = await Stage($"G-{index}-kana", async () =>
                {
                    var result = await Ask<KanaResponse>("kana",
                        "Return {outcome:'valid'|'uncertain'|'invalid',kana:string,reason:string} giving the complete reading of this unchanged Japanese sentence. Keep written particles は/へ/を and appropriate katakana. " +
                        "No romaji, paraphrases or pronunciation-only particle rewrites. expectedReading is the reading of the WHOLE sentence, including particles and grammatical endings; " +
                        "it is not a dictionary reading of any one word. Base vocabulary readings are separate anchors. Confirm the complete sentence reading, flag any genuine uncertainty in reason.",
                        new { answer.Japanese, setting.Setting, schema.Register, expectedReading = answer.Kana,
                            vocabulary = answer.Words.Select(w => vocabulary.Resolve(w).ModelInput) });
                    if (result.Outcome is not ("valid" or "uncertain" or "invalid") || string.IsNullOrWhiteSpace(result.Kana) || string.IsNullOrWhiteSpace(result.Reason)) throw new InvalidDataException("Incomplete kana response.");
                    return result;
                });
                if (reading.Outcome != "valid" || GenkiExercise.Normalize(reading.Kana) != GenkiExercise.Normalize(answer.Kana))
                {
                    if (index > 0) { state.AlternativeExclusions[$"G-{index}"] = "Kana conflicts with tracked readings."; Save(); continue; }
                    state.Status = "needs-review"; state.Reason = "Canonical kana conflicts with known readings/grammar orthography: " + reading.Reason; Save(); return;
                }
                answers.Add(new() { Japanese = answer.Japanese, Kana = reading.Kana, RequiredWords = answer.Words });
            }
            var question = new GenkiQuestion
            {
                Id = candidate.Id, SchemaId = schema.Id, GrammarPointId = schema.GrammarPointId, English = english,
                Setting = setting.Setting, Register = schema.Register, RequiredGrammar = allowed.Order(StringComparer.Ordinal).ToArray(),
                Answers = answers, Provenance = state.Provenance
            };
            GenkiPracticeService.ValidateQuestion(question, catalog, vocabulary);
            state.Question = question; state.Status = "completed"; state.ActiveStage = null; state.Reason = null; Save();
        }
        catch (StageFailureException) { /* The failed stage and reason are already durable and retryable. */ }
    }
    private void ValidateAnswerSegments(IReadOnlyList<GenkiSegment> segments, HashSet<string> allowed)
    {
        if (segments.Count == 0) throw new InvalidDataException("Alternative has no provenance.");
        foreach (var segment in segments)
        {
            if (segment.Children.Count > 0 || segment.FormChoices.Count > 0 || segment.Name is not null) throw new InvalidDataException("Unresolved alternative structure.");
            if (segment.Kind == "literal")
            {
                if (segment.Text is null || !catalog.AllowsLiteral(segment.Text, allowed)) throw new InvalidDataException("Untracked lexical or future grammar literal.");
            }
            else if (segment.Kind == "fixed" && segment.Word is not null)
            {
                var word = vocabulary.Resolve(segment.Word).Word;
                if (!GenkiForms.Supports(word, segment.Form) || GenkiForms.RequiredGrammar(segment.Form, word).Any(g => !allowed.Contains(g)))
                    throw new InvalidDataException("Alternative requires unsupported/future forms.");
            }
            else throw new InvalidDataException("Alternative must resolve all lexical words.");
        }
    }
    private object[] AlternativeVocabulary(Candidate candidate)
    {
        var canonical = candidate.RequiredWords.Select(vocabulary.Resolve).ToArray();
        var meanings = canonical.SelectMany(w => w.Word.English.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return canonical.Concat(vocabulary.Entries.Where(w => w.Word.English.Split([';', ','], StringSplitOptions.TrimEntries).Any(meanings.Contains)).Take(24))
            .DistinctBy(w => w.Ref).Select(w => w.ModelInput).ToArray();
    }
    private static void ValidateJudgment(Judgment judgment)
    {
        if (judgment is null || judgment.Outcome is not ("valid" or "invalid" or "uncertain") || string.IsNullOrWhiteSpace(judgment.Reason) || judgment.GrammarIds is null)
            throw new InvalidDataException("Malformed linguistic judgment.");
    }
    private sealed class StageFailureException : Exception;
}
