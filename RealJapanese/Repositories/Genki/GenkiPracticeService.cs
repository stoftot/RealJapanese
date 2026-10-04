using System.Text;
using System.Text.Json;
using DataLoaders.Models.Genki;
using Repositories.DTOs;

namespace Repositories.Genki;

public sealed record GenkiExercise
{
    public required GenkiQuestion Question { get; init; }
    public required IReadOnlyList<GenkiAnswer> DisplayAnswers { get; init; }
    public QuestionAnswerDto QuestionAnswer => new() { Question = Question.English, Answer = DisplayAnswers[0].Japanese };
    public string GrammarId => Question.GrammarPointId;
    public string Context => Question.Setting ?? "Translate the sentence using the target grammar.";
    public string Register => Question.Register;
    public IReadOnlyList<string> ModelAnswers => DisplayAnswers.SelectMany(a => new[] { a.Japanese, a.Kana }).Distinct().ToArray();
    // Knowledge filters service eligibility, not whether an approved stored answer is correct.
    public bool MatchesModel(string answer) => !string.IsNullOrWhiteSpace(answer) && Question.Answers
        .Any(a => Normalize(a.Japanese) == Normalize(answer) || Normalize(a.Kana) == Normalize(answer));
    public static string Normalize(string value)
    {
        // Width/compatibility normalization and whitespace are harmless. Only terminal sentence punctuation is ignored.
        var text = string.Concat(value.Normalize(NormalizationForm.FormKC).Where(c => !char.IsWhiteSpace(c)));
        return text.TrimEnd('。', '.', '!', '?', '！', '？');
    }
}

/// <summary>Stored-data-only practice. Neither this assembly nor either host calls a model.</summary>
public sealed class GenkiPracticeService(RepositoryPaths paths, GenkiCatalog catalog)
{
    public string BankPath => Path.Combine(paths.CatalogRoot, "Genki", "questions.jsonl");
    public HashSet<WordRef> KnownWords()
    {
        var progress = paths.Progress.Read().Data;
        return new[] { ("Words", "word"), ("Nouns", "noun"), ("Verbs", "verb"), ("Adjectives", "adjective") }
            .SelectMany(pair => progress[pair.Item1].KnownIds.Select(id => new WordRef(pair.Item2,
                id.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToHashSet();
    }
    public bool HasPublishedQuestions => File.Exists(BankPath) && new FileInfo(BankPath).Length > 0;
    public static bool Eligible(GenkiQuestion question, ISet<WordRef> known, ISet<string> grammar) =>
        grammar.Contains(question.GrammarPointId) && question.RequiredGrammar.All(grammar.Contains) &&
        question.Answers.Any(a => a.RequiredWords.All(known.Contains));
    public IReadOnlyList<GenkiExercise> CreateRound(int lessonNumber, string? pointId, int maximum = 20, Random? random = null)
    {
        if (maximum < 1 || maximum > 100) throw new ArgumentOutOfRangeException(nameof(maximum));
        var lesson = catalog.FindLesson(lessonNumber) ?? throw new ArgumentOutOfRangeException(nameof(lessonNumber));
        var targets = lesson.GrammarPoints.Where(p => pointId is null || p.Id == pointId).Select(p => p.Id).ToHashSet();
        var allowed = catalog.Lessons.Where(l => l.Number <= lessonNumber).SelectMany(l => l.GrammarPoints).Select(p => p.Id).ToHashSet();
        var known = KnownWords(); random ??= Random.Shared;
        if (!File.Exists(BankPath)) return [];
        var vocabulary = GenkiVocabulary.Load(paths.CatalogRoot);
        // One reservoir per point keeps memory bounded and provides lesson-level mixtures.
        var buckets = targets.ToDictionary(id => id, _ => new List<GenkiExercise>());
        var seen = targets.ToDictionary(id => id, _ => 0L);
        using var stream = new FileStream(BankPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var question = JsonSerializer.Deserialize<GenkiQuestion>(line, GenkiJson.Options)
                ?? throw new InvalidDataException("Invalid stored Genki question.");
            ValidateQuestion(question, catalog, vocabulary);
            if (!targets.Contains(question.GrammarPointId) || !Eligible(question, known, allowed)) continue;
            var familiar = question.Answers.Where(a => a.RequiredWords.All(known.Contains)).ToArray();
            var exercise = new GenkiExercise { Question = question, DisplayAnswers = familiar };
            var bucket = buckets[question.GrammarPointId]; var count = ++seen[question.GrammarPointId];
            if (bucket.Count < maximum) bucket.Add(exercise);
            else { var replace = random.NextInt64(count); if (replace < maximum) bucket[(int)replace] = exercise; }
        }
        var result = new List<GenkiExercise>(); var shuffled = buckets.Values.Select(b => b.OrderBy(_ => random.Next()).ToArray()).ToArray();
        for (var i = 0; i < maximum && result.Count < maximum; i++)
            foreach (var bucket in shuffled.Where(b => b.Length > i))
                if (result.Count < maximum) result.Add(bucket[i]);
        return result;
    }
    public static void ValidateQuestion(GenkiQuestion question, GenkiCatalog catalog, GenkiVocabulary vocabulary)
    {
        var schema = catalog.Schemas.SingleOrDefault(s => s.Id == question.SchemaId);
        if (question.Version != 1 || schema is null || schema.GrammarPointId != question.GrammarPointId ||
            schema.Register != question.Register || string.IsNullOrWhiteSpace(question.Id) || string.IsNullOrWhiteSpace(question.English) ||
            question.Answers.Count == 0 || !catalog.AllowedGrammar(schema).SetEquals(question.RequiredGrammar) ||
            question.Answers.Any(a => string.IsNullOrWhiteSpace(a.Japanese) || string.IsNullOrWhiteSpace(a.Kana) || a.RequiredWords.Any(w => !vocabulary.Contains(w))))
            throw new InvalidDataException($"Invalid or stale published question {question.Id}.");
    }
}
