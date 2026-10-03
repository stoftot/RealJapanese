using System.Text.Json;
using System.Text.RegularExpressions;
using DataLoaders.Models.Genki;

namespace Repositories.Genki;

/// <summary>Same embedded, offline curriculum in both hosts; never writes study progress.</summary>
public sealed class GenkiCatalog
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    public IReadOnlyList<GenkiLesson> Lessons { get; }
    public IReadOnlyList<GenkiLexeme> Vocabulary { get; }
    public GenkiCatalog() : this(ReadLessons().ToArray()) { }

    public GenkiCatalog(GenkiLesson[] lessons)
    {
        Lessons = lessons.OrderBy(x => x.Number).ToArray();
        Vocabulary = Lessons.SelectMany(x => x.Lexicon).ToArray();
        Validate();
    }

    private static IEnumerable<GenkiLesson> ReadLessons()
    {
        var assembly = typeof(GenkiCatalog).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(x => x.Contains(".Genki.Content.lesson-") && x.EndsWith(".json")))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            yield return JsonSerializer.Deserialize<GenkiLesson>(stream, JsonOptions)
                ?? throw new InvalidDataException($"Empty Genki lesson: {name}");
        }
    }

    public GenkiLesson? FindLesson(int number) => Lessons.FirstOrDefault(x => x.Number == number);

    private void Validate()
    {
        if (Lessons.Count == 0) throw new InvalidDataException("No Genki lessons were packaged.");
        Unique(Lessons.Select(x => x.Number.ToString()), "lesson");
        Unique(Vocabulary.Select(x => x.Key), "vocabulary key");
        var points = Lessons.SelectMany(l => l.GrammarPoints.Select((p, i) => (l.Number, Point: p, Index: i)))
            .ToArray();
        Unique(points.Select(x => x.Point.Id), "grammar point");
        Unique(points.SelectMany(x => x.Point.Schemas).Select(x => x.Id), "schema");
        var lookup = points.ToDictionary(x => x.Point.Id);
        foreach (var lesson in Lessons)
        {
            if (lesson.SchemaVersion != 1 || lesson.Number is < 1 or > 12 || lesson.GrammarPoints.Count == 0)
                throw new InvalidDataException($"Unsupported Genki lesson {lesson.Number}.");
            foreach (var word in lesson.Lexicon)
            {
                if (word.IntroducedLesson is < 1 or > 12 || word.Tags.Count == 0 ||
                    string.IsNullOrWhiteSpace(word.Japanese) || string.IsNullOrWhiteSpace(word.Kana) ||
                    string.IsNullOrWhiteSpace(word.English))
                    throw new InvalidDataException($"Incomplete vocabulary: {word.Key}");
                foreach (var form in word.Forms.Values)
                    if (form.IntroducedLesson is < 1 or > 12 || string.IsNullOrWhiteSpace(form.Japanese) ||
                        string.IsNullOrWhiteSpace(form.Kana) || string.IsNullOrWhiteSpace(form.English))
                        throw new InvalidDataException($"Incomplete vocabulary form: {word.Key}");
            }
            foreach (var point in lesson.GrammarPoints)
            {
                if (point.Examples.Count == 0 || point.Schemas.All(s => s.Kind != "translation"))
                    throw new InvalidDataException($"Missing examples or production practice: {point.Id}");
                foreach (var prerequisite in point.Prerequisites.Concat(point.Schemas.SelectMany(s => s.Prerequisites)))
                    if (!lookup.TryGetValue(prerequisite, out var prior) || prior.Number > lesson.Number ||
                        (prior.Number == lesson.Number && prior.Index >= lookup[point.Id].Index))
                        throw new InvalidDataException($"Invalid/future prerequisite {prerequisite} in {point.Id}.");
                foreach (var schema in point.Schemas)
                {
                    if (schema.Slots.Count == 0 || schema.Restrictions.Count == 0 ||
                        schema.Kind is not ("translation" or "transformation" or "response") ||
                        schema.Register is not ("polite" or "casual"))
                        throw new InvalidDataException($"Incomplete schema {schema.Id}.");
                    Unique(schema.Slots.Select(x => x.Name), $"slot in {schema.Id}");
                    foreach (var relation in schema.Relations)
                        if (relation.Kind is not ("distinct" or "accepts") ||
                            !schema.Slots.Any(x => x.Name == relation.Left) ||
                            !schema.Slots.Any(x => x.Name == relation.Right))
                            throw new InvalidDataException($"Invalid relation in {schema.Id}.");
                    foreach (var template in Templates(schema))
                    {
                        foreach (Match token in GenkiGenerator.Token.Matches(template))
                        {
                            var slot = schema.Slots.FirstOrDefault(x => x.Name == token.Groups[1].Value);
                            var form = token.Groups[2].Success ? token.Groups[2].Value : "base";
                            if (slot is null || !slot.Forms.Contains(form))
                                throw new InvalidDataException($"Undeclared slot/form {token.Value} in {schema.Id}.");
                        }
                        if (GenkiGenerator.Token.Replace(template, "").IndexOfAny(['{', '}']) >= 0)
                            throw new InvalidDataException($"Malformed template in {schema.Id}.");
                    }
                }
            }
        }
    }

    internal static IEnumerable<string> Templates(GenkiSchema schema) =>
        new[] { schema.English, schema.Japanese, schema.Kana, schema.Context, schema.Instruction }
            .Concat(schema.Alternatives.SelectMany(x => new[] { x.Japanese, x.Kana }));

    private static void Unique(IEnumerable<string> values, string kind)
    {
        if (values.Any(string.IsNullOrWhiteSpace) || values.GroupBy(x => x).Any(g => g.Count() != 1))
            throw new InvalidDataException($"Empty or duplicate {kind}.");
    }
}
