using System.Text;
using DataLoaders.Models.Genki;
using DataLoaders.Models;
using Repositories.Genki;

namespace Genki.Generation;

public sealed record Candidate
{
    public required string Id { get; init; }
    public required GenkiSchema Schema { get; init; }
    public required IReadOnlyDictionary<string, WordRef> Bindings { get; init; }
    public required IReadOnlyDictionary<string, string> Choices { get; init; }
    public required IReadOnlyList<GenkiSegment> Segments { get; init; }
    public required string Japanese { get; init; }
    public required string ExpectedKana { get; init; }
    public required IReadOnlyList<WordRef> RequiredWords { get; init; }
    public required string InputFingerprint { get; init; }
}

/// <summary>Lazy Cartesian traversal with named bindings. Completion is keyed by identity, never list position.</summary>
public sealed class CandidateEnumerator(GenkiCatalog catalog, GenkiVocabulary vocabulary, IEnumerable<WordTags> annotations)
{
    private readonly Dictionary<WordRef, WordTags> mapping = ValidateMapping(vocabulary, catalog.Registry, annotations);
    private sealed record Shape(IReadOnlyList<GenkiSegment> Segments, SortedDictionary<string,string> Choices);
    public IReadOnlyDictionary<string,int> SemanticPoolSizes(GenkiSchema schema) => schema.Slots.ToDictionary(
        pair => pair.Key, pair => vocabulary.Entries.Count(entry => Eligible(entry, pair.Value)));
    public IEnumerable<Candidate> Enumerate(GenkiSchema schema, CancellationToken cancellationToken = default)
    {
        var allowed = catalog.AllowedGrammar(schema);
        foreach (var shape in Shapes(schema.Segments, ""))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = shape.Segments.Where(s => s.Kind == "slot").Select(s => s.Name!).Distinct().Order(StringComparer.Ordinal).ToArray();
            var pools = active.ToDictionary(name => name, name => vocabulary.Entries.Where(entry => Eligible(entry, schema.Slots[name]) &&
                shape.Segments.Where(s => s.Kind == "slot" && s.Name == name).All(s => FormAllowed(entry.Word, s.Form, allowed))).ToArray());
            if (pools.Values.Any(p => p.Length == 0)) continue;
            if (shape.Segments.Where(s => s.Kind == "fixed").Any(s => !vocabulary.Contains(s.Word!) ||
                !FormAllowed(vocabulary.Resolve(s.Word!).Word, s.Form, allowed))) continue;
            var chosen = new SortedDictionary<string, WordRef>(StringComparer.Ordinal);
            IEnumerable<Candidate> Bind(int index)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < active.Length)
                {
                    var name = active[index];
                    foreach (var entry in pools[name])
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        chosen[name] = entry.Ref;
                        if (schema.Relations.All(r => Compatible(r, chosen)))
                            foreach (var candidate in Bind(index + 1)) yield return candidate;
                    }
                    chosen.Remove(name); yield break;
                }
                var rendered = Resolve(shape.Segments, chosen);
                var id = GenkiJson.Fingerprint(new { schema = schema.Id, bindings = chosen, choices = shape.Choices });
                yield return new()
                {
                    Id = id, Schema = schema, Bindings = new SortedDictionary<string,WordRef>(chosen, StringComparer.Ordinal),
                    Choices = shape.Choices, Segments = rendered.Segments, Japanese = rendered.Japanese,
                    ExpectedKana = rendered.Kana, RequiredWords = rendered.Words,
                    InputFingerprint = GenkiJson.Fingerprint(new { schema, words = rendered.Words.Select(w => vocabulary.Resolve(w).ModelInput) })
                };
            }
            foreach (var candidate in Bind(0)) yield return candidate;
        }
    }
    private bool Eligible(VocabularyEntry entry, GenkiSlot slot) => slot.WordTypes.Contains(entry.Ref.WordType) &&
        (slot.ConjugationTypes.Count == 0 || entry.Word is Conjugatabel c && slot.ConjugationTypes.Contains(c.Type)) &&
        catalog.Registry.Matches(slot.Tags, mapping.GetValueOrDefault(entry.Ref)?.DirectTags ?? []);
    private static bool FormAllowed(Word word, string form, HashSet<string> allowed) => GenkiForms.Supports(word, form) &&
        GenkiForms.RequiredGrammar(form, word).All(allowed.Contains);
    private bool Compatible(GenkiRelation relation, IReadOnlyDictionary<string,WordRef> chosen)
    {
        if (!chosen.TryGetValue(relation.Left, out var left) || !chosen.TryGetValue(relation.Right, out var right)) return true;
        if (relation.Kind == "distinct") return left != right;
        if (relation.Kind == "same") return left == right;
        var leftTags = catalog.Registry.Expand(mapping.GetValueOrDefault(left)?.DirectTags ?? []);
        var rightTags = catalog.Registry.Expand(mapping.GetValueOrDefault(right)?.DirectTags ?? []);
        return !relation.ForbiddenPairs.Any(p => leftTags.Contains(p.LeftTag) && rightTags.Contains(p.RightTag));
    }
    public (string Japanese, string Kana, IReadOnlyList<WordRef> Words, IReadOnlyList<GenkiSegment> Segments) Resolve(
        IReadOnlyList<GenkiSegment> segments, IReadOnlyDictionary<string,WordRef>? bindings = null)
    {
        var japanese = new StringBuilder(); var kana = new StringBuilder(); var words = new HashSet<WordRef>();
        var resolved = new List<GenkiSegment>();
        foreach (var segment in segments)
        {
            if (segment.Kind == "literal") { japanese.Append(segment.Text); kana.Append(segment.Text); resolved.Add(segment); continue; }
            var reference = segment.Kind == "fixed" ? segment.Word! : bindings![segment.Name!];
            var entry = vocabulary.Resolve(reference); words.Add(reference);
            japanese.Append(GenkiForms.Render(entry.Word, segment.Form)); kana.Append(GenkiForms.Render(entry.Word, segment.Form, kana: true));
            resolved.Add(new() { Kind = "fixed", Word = reference, Form = segment.Form });
        }
        return (japanese.ToString(), kana.ToString(), words.OrderBy(w => w.ToString(), StringComparer.Ordinal).ToArray(), resolved);
    }
    private static Dictionary<WordRef,WordTags> ValidateMapping(GenkiVocabulary vocabulary, SemanticRegistry registry, IEnumerable<WordTags> annotations)
    {
        var result = new Dictionary<WordRef,WordTags>();
        foreach (var item in annotations)
        {
            if (vocabulary.Resolve(item.Word).Fingerprint != item.WordFingerprint) throw new InvalidDataException($"Stale tags for {item.Word}; reprocess that word.");
            registry.Expand(item.DirectTags);
            if (!result.TryAdd(item.Word, item)) throw new InvalidDataException($"Duplicate mapping for {item.Word}.");
        }
        return result;
    }
    private static IEnumerable<Shape> Shapes(IReadOnlyList<GenkiSegment> segments, string prefix)
    {
        IEnumerable<Shape> Step(int index, List<GenkiSegment> output, SortedDictionary<string,string> choices)
        {
            if (index == segments.Count) { yield return new(output.ToArray(), new(choices, StringComparer.Ordinal)); yield break; }
            var s = segments[index]; var path = prefix + index;
            if (s.Kind == "optional")
            {
                choices[path] = "omit";
                foreach (var shape in Step(index + 1, output, choices)) yield return shape;
                choices[path] = "include";
                foreach (var inner in Shapes(s.Children, path + "."))
                {
                    var nextChoices = new SortedDictionary<string,string>(choices, StringComparer.Ordinal);
                    foreach (var choice in inner.Choices) nextChoices[choice.Key] = choice.Value;
                    foreach (var shape in Step(index + 1, [..output, ..inner.Segments], nextChoices)) yield return shape;
                }
                choices.Remove(path);
            }
            else
            {
                var forms = s.FormChoices.Count > 0 ? s.FormChoices : [s.Form];
                foreach (var form in forms)
                {
                    if (s.FormChoices.Count > 0) choices[path] = form;
                    foreach (var shape in Step(index + 1, [..output, s with { Form = form, FormChoices = [] }], choices)) yield return shape;
                }
                choices.Remove(path);
            }
        }
        return Step(0, [], new(StringComparer.Ordinal));
    }
}
