using System.Text.Json;
using DataLoaders.Models.Genki;

namespace Repositories.Genki;

/// <summary>Validated curriculum and generation definitions shared by both hosts and offline tools.</summary>
public sealed class GenkiCatalog
{
    public static JsonSerializerOptions JsonOptions => GenkiJson.Options;
    public IReadOnlyList<GenkiLesson> Lessons { get; }
    public IReadOnlyList<GenkiSchema> Schemas { get; }
    public SemanticRegistry Registry { get; }
    public IReadOnlyList<GrammarMaterial> Materials { get; }
    private readonly Dictionary<string, (GenkiGrammarPoint Point, int Order)> points;
    public GenkiCatalog() : this(Read<GenkiLesson>(".Content.lesson-"), ReadArrays<GenkiSchema>(".Schemas.lesson-"),
        ReadArrays<SemanticTag>(".Content.tags.json"), ReadArrays<GrammarMaterial>(".Content.grammar-material.json")) { }
    public GenkiCatalog(GenkiLesson[] lessons, GenkiSchema[] schemas, SemanticTag[] tags, GrammarMaterial[] materials)
    {
        Lessons = lessons.OrderBy(x => x.Number).ToArray(); Schemas = schemas.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Registry = new(tags); Materials = materials;
        Unique(Lessons.Select(x => x.Number.ToString()), "lesson");
        Unique(Lessons.SelectMany(x => x.GrammarPoints).Select(x => x.Id), "grammar point");
        Unique(Schemas.Select(x => x.Id), "schema");
        points = Lessons.SelectMany(x => x.GrammarPoints).Select((p,i) => (p,i)).ToDictionary(x => x.p.Id, x => (x.p,x.i));
        foreach (var lesson in Lessons)
        {
            if (lesson.SchemaVersion != 2 || lesson.Number is < 1 or > 12 || lesson.GrammarPoints.Count == 0)
                throw new InvalidDataException($"Unsupported/empty lesson {lesson.Number}.");
            foreach (var point in lesson.GrammarPoints)
            {
                if (string.IsNullOrWhiteSpace(point.Meaning) || string.IsNullOrWhiteSpace(point.Formation) ||
                    !Schemas.Any(x => x.GrammarPointId == point.Id)) throw new InvalidDataException($"Incomplete point {point.Id}.");
                CheckPrior(point.Id, point.Prerequisites);
            }
        }
        foreach (var material in Materials)
            if (!points.ContainsKey(material.GrammarPointId) || string.IsNullOrEmpty(material.Text))
                throw new InvalidDataException("Invalid grammatical material.");
        foreach (var schema in Schemas) ValidateSchema(schema);
    }
    public GenkiLesson? FindLesson(int number) => Lessons.FirstOrDefault(x => x.Number == number);
    public GenkiGrammarPoint Point(string id) => points.TryGetValue(id, out var p) ? p.Point
        : throw new InvalidDataException($"Unknown grammar point {id}.");
    public int LessonNumber(string id) => Lessons.Single(x => x.GrammarPoints.Any(p => p.Id == id)).Number;
    public IReadOnlyList<GenkiSchema> ForPoint(string id) => Schemas.Where(x => x.GrammarPointId == id).ToArray();
    public HashSet<string> AllowedGrammar(GenkiSchema schema)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        void Add(string id) { if (result.Add(id)) foreach (var prior in Point(id).Prerequisites) Add(prior); }
        Add(schema.GrammarPointId); foreach (var id in schema.PrerequisiteGrammarIds) Add(id);
        return result;
    }
    public bool AllowsLiteral(string text, ISet<string> allowed) => Materials.Any(x => x.Text == text && allowed.Contains(x.GrammarPointId));
    public void ValidateVocabularyReferences(GenkiVocabulary vocabulary)
    {
        foreach (var schema in Schemas)
        {
            var allowed = AllowedGrammar(schema);
            void Walk(IEnumerable<GenkiSegment> segments)
            {
                foreach (var segment in segments)
                {
                    if (segment.Kind == "optional") Walk(segment.Children);
                    if (segment.Kind != "fixed") continue;
                    var word = vocabulary.Resolve(segment.Word!).Word;
                    foreach (var form in segment.FormChoices.Count > 0 ? segment.FormChoices : [segment.Form])
                        if (!GenkiForms.Supports(word, form) || GenkiForms.RequiredGrammar(form, word).Any(g => !allowed.Contains(g)))
                            throw new InvalidDataException($"Unsupported or future fixed-word form '{form}' for {segment.Word} in {schema.Id}.");
                }
            }
            Walk(schema.Segments);
        }
    }
    private void CheckPrior(string target, IEnumerable<string> required)
    {
        foreach (var id in required)
            if (!points.TryGetValue(id, out var prior) || prior.Order >= points[target].Order)
                throw new InvalidDataException($"Invalid/future prerequisite {id} in {target}.");
    }
    private void ValidateSchema(GenkiSchema schema)
    {
        Point(schema.GrammarPointId); CheckPrior(schema.GrammarPointId, schema.PrerequisiteGrammarIds);
        if (schema.Register is not ("polite" or "casual") || schema.ExerciseType != "translation" || schema.Segments.Count == 0 ||
            schema.ValidationRules.Count == 0 || string.IsNullOrWhiteSpace(schema.Tense) || string.IsNullOrWhiteSpace(schema.Polarity))
            throw new InvalidDataException($"Incomplete schema {schema.Id}.");
        foreach (var slot in schema.Slots.Values)
        {
            if (slot.WordTypes.Count == 0 || slot.WordTypes.Any(x => !GenkiVocabulary.WordTypes.Contains(x)))
                throw new InvalidDataException($"Invalid slot word types in {schema.Id}.");
            Registry.Validate(slot.Tags);
        }
        var used = new HashSet<string>(); var allowed = AllowedGrammar(schema);
        void Walk(GenkiSegment segment)
        {
            if (!GenkiForms.Names.Contains(segment.Form) || segment.FormChoices.Any(f => !GenkiForms.Names.Contains(f)))
                throw new InvalidDataException($"Unknown form in {schema.Id}.");
            switch (segment.Kind)
            {
                case "literal":
                    if (segment.Text is null || !AllowsLiteral(segment.Text, allowed))
                        throw new InvalidDataException($"Unregistered or undeclared grammar literal '{segment.Text}' in {schema.Id}.");
                    break;
                case "slot":
                    if (segment.Name is null || !schema.Slots.ContainsKey(segment.Name)) throw new InvalidDataException($"Unknown slot in {schema.Id}.");
                    used.Add(segment.Name); break;
                case "fixed":
                    if (segment.Word is null || !GenkiVocabulary.WordTypes.Contains(segment.Word.WordType))
                        throw new InvalidDataException($"Untracked fixed word in {schema.Id}.");
                    break;
                case "optional":
                    if (segment.Children.Count == 0) throw new InvalidDataException($"Empty optional group in {schema.Id}.");
                    foreach (var child in segment.Children) Walk(child); break;
                default: throw new InvalidDataException($"Unknown segment kind {segment.Kind}.");
            }
            if (segment.Kind != "optional" && segment.Children.Count > 0) throw new InvalidDataException("Only optional groups contain children.");
            if (segment.FormChoices.Distinct().Count() != segment.FormChoices.Count) throw new InvalidDataException("Duplicate form choice.");
        }
        foreach (var segment in schema.Segments) Walk(segment);
        if (!used.SetEquals(schema.Slots.Keys)) throw new InvalidDataException($"Unused slot in {schema.Id}.");
        foreach (var relation in schema.Relations)
        {
            if (!schema.Slots.ContainsKey(relation.Left) || !schema.Slots.ContainsKey(relation.Right) ||
                relation.Kind is not ("distinct" or "same" or "compatible-tags")) throw new InvalidDataException($"Invalid relation in {schema.Id}.");
            foreach (var pair in relation.ForbiddenPairs) Registry.Validate(new() { AllOf = [pair.LeftTag, pair.RightTag] });
        }
    }
    private static void Unique(IEnumerable<string> ids, string kind)
    { if (ids.Any(string.IsNullOrWhiteSpace) || ids.GroupBy(x => x).Any(x => x.Count() > 1)) throw new InvalidDataException($"Invalid {kind} identities."); }
    private static IEnumerable<string> Resources(string marker) => typeof(GenkiCatalog).Assembly.GetManifestResourceNames()
        .Where(x => x.Contains(marker, StringComparison.Ordinal) && x.EndsWith(".json", StringComparison.Ordinal));
    private static T[] Read<T>(string marker) => Resources(marker).Select(name =>
    { using var stream = typeof(GenkiCatalog).Assembly.GetManifestResourceStream(name)!; return JsonSerializer.Deserialize<T>(stream, JsonOptions)!; }).ToArray();
    private static T[] ReadArrays<T>(string marker) => Read<T[]>(marker).SelectMany(x => x).ToArray();
}
