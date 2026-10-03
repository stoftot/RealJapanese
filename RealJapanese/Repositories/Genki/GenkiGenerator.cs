using System.Text;
using System.Text.RegularExpressions;
using DataLoaders.Models.Genki;
using Repositories.DTOs;

namespace Repositories.Genki;

public sealed record GenkiExercise
{
    public required string GrammarId { get; init; }
    public required GenkiSchema Schema { get; init; }
    public required QuestionAnswerDto QuestionAnswer { get; init; }
    public required string KanaAnswer { get; init; }
    public required string Context { get; init; }
    public required string Instruction { get; init; }
    public required IReadOnlyList<string> ModelAnswers { get; init; }
    public required IReadOnlyDictionary<string, GenkiLexeme> Words { get; init; }

    public bool MatchesModel(string answer) => !string.IsNullOrWhiteSpace(answer) &&
        ModelAnswers.Any(x => Normalize(x) == Normalize(answer));

    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormKC)
        .Where(c => !char.IsWhiteSpace(c) && c is not ('。' or '、' or '.' or ',' or '?' or '？' or '!' or '！')));
}

/// <summary>Instantiates reviewed frames with typed, compatible words and lesson-gated forms.</summary>
public sealed class GenkiGenerator(GenkiCatalog catalog)
{
    internal static readonly Regex Token = new(@"\{([a-zA-Z][a-zA-Z0-9_-]*)(?::([a-zA-Z][a-zA-Z0-9_-]*))?\}", RegexOptions.Compiled);

    public GenkiExercise Generate(int lessonNumber, string pointId, string schemaId, int vocabularyLesson,
        Random? random = null, IEnumerable<GenkiLexeme>? additionalVocabulary = null)
    {
        var lesson = catalog.FindLesson(lessonNumber) ?? throw new ArgumentOutOfRangeException(nameof(lessonNumber));
        var point = lesson.GrammarPoints.FirstOrDefault(x => x.Id == pointId)
            ?? throw new ArgumentException("Grammar point is not part of this lesson.", nameof(pointId));
        var schema = point.Schemas.FirstOrDefault(x => x.Id == schemaId)
            ?? throw new ArgumentException("Schema is not part of this grammar point.", nameof(schemaId));
        if (vocabularyLesson < lessonNumber || vocabularyLesson > 12)
            throw new ArgumentOutOfRangeException(nameof(vocabularyLesson));
        random ??= Random.Shared;
        var vocabulary = catalog.Vocabulary.Concat(additionalVocabulary ?? []).ToArray();
        if (vocabulary.GroupBy(x => x.Key).Any(g => g.Count() > 1))
            throw new ArgumentException("Additional vocabulary keys must be unique.", nameof(additionalVocabulary));
        var pools = schema.Slots.ToDictionary(x => x.Name, x => vocabulary
            .Where(w => IsEligible(w, x, lessonNumber, vocabularyLesson)).OrderBy(_ => random.Next()).ToArray());
        var chosen = new Dictionary<string, GenkiLexeme>();
        // Smallest pool first reduces backtracking without weakening any semantic constraint.
        var slots = schema.Slots.OrderBy(x => pools[x.Name].Length).ToArray();
        var budget = 20_000;
        bool Assign(int index)
        {
            if (index == slots.Length) return true;
            var slot = slots[index];
            foreach (var word in pools[slot.Name])
            {
                if (--budget < 0) break;
                chosen[slot.Name] = word;
                if (schema.Relations.All(r => Satisfies(r, chosen)) && Assign(index + 1)) return true;
                chosen.Remove(slot.Name);
            }
            return false;
        }
        if (!Assign(0)) throw new InvalidOperationException($"No compatible vocabulary for {schema.Id} at this level.");
        var japanese = Render(schema.Japanese, chosen, "japanese");
        var kana = Render(schema.Kana, chosen, "kana");
        return new()
        {
            GrammarId = point.Id, Schema = schema,
            QuestionAnswer = new() { Question = Capitalize(Render(schema.English, chosen, "english")), Answer = japanese },
            KanaAnswer = kana, Context = Render(schema.Context, chosen, "english"),
            Instruction = Render(schema.Instruction, chosen, "english"),
            ModelAnswers = new[] { japanese, kana }.Concat(schema.Alternatives.SelectMany(a => new[]
                { Render(a.Japanese, chosen, "japanese"), Render(a.Kana, chosen, "kana") })).Distinct().ToArray(),
            Words = new Dictionary<string, GenkiLexeme>(chosen)
        };
    }

    public static bool IsEligible(GenkiLexeme word, GenkiSlot slot, int grammarLesson, int vocabularyLesson) =>
        word.IntroducedLesson >= 1 && word.IntroducedLesson <= vocabularyLesson && word.WordType == slot.WordType &&
        slot.Tags.All(word.Tags.Contains) && slot.Forms.All(f => word.GetForm(f) is { } form &&
            // Base words may expand independently of the grammar progression.
            (f == "base" || form.IntroducedLesson >= 1 && form.IntroducedLesson <= grammarLesson));

    private static bool Satisfies(GenkiRelation relation, IReadOnlyDictionary<string, GenkiLexeme> words)
    {
        if (!words.TryGetValue(relation.Left, out var left) || !words.TryGetValue(relation.Right, out var right)) return true;
        return relation.Kind switch
        {
            "distinct" => left.Japanese != right.Japanese,
            "accepts" => right.Tags.Any(left.Accepts.Contains),
            _ => throw new InvalidDataException($"Unknown relation: {relation.Kind}")
        };
    }

    private static string Render(string template, IReadOnlyDictionary<string, GenkiLexeme> words, string language) =>
        Token.Replace(template, match =>
        {
            var form = words[match.Groups[1].Value].GetForm(match.Groups[2].Success ? match.Groups[2].Value : "base")
                ?? throw new InvalidDataException($"Missing form for {match.Value}.");
            return language switch { "japanese" => form.Japanese, "kana" => form.Kana, _ => form.English };
        });

    private static string Capitalize(string text) => string.IsNullOrEmpty(text)
        ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
