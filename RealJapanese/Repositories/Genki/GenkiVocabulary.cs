using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataLoaders;
using DataLoaders.Models;
using DataLoaders.Models.Genki;

namespace Repositories.Genki;

public static class GenkiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Fingerprint<T>(T value) => Hash(JsonSerializer.Serialize(value, Compact));
}

public sealed record VocabularyEntry(WordRef Ref, Word Word)
{
    public object ModelInput => new { wordType = Ref.WordType, id = Ref.Id, Word.Japanese, Word.Kana, Word.English,
        type = (Word as Conjugatabel)?.Type, Word.Category };
    public string Fingerprint => GenkiJson.Fingerprint(ModelInput);
}

/// <summary>Read-only view of the four existing collections. Never assigns IDs or writes catalogs.</summary>
public sealed class GenkiVocabulary
{
    public IReadOnlyList<VocabularyEntry> Entries { get; }
    private readonly IReadOnlyDictionary<WordRef, VocabularyEntry> byId;
    public GenkiVocabulary(IEnumerable<VocabularyEntry> entries)
    {
        Entries = entries.OrderBy(x => x.Ref.WordType, StringComparer.Ordinal).ThenBy(x => x.Ref.Id, StringComparer.Ordinal).ToArray();
        if (Entries.Any(x => !WordTypes.Contains(x.Ref.WordType) || x.Word.Id < 0 ||
                x.Ref.Id != x.Word.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
            Entries.GroupBy(x => x.Ref).Any(x => x.Count() > 1))
            throw new InvalidDataException("Vocabulary needs stable, unique collection-qualified source IDs.");
        byId = Entries.ToDictionary(x => x.Ref);
    }
    public static readonly string[] WordTypes = ["word", "noun", "verb", "adjective"];
    public VocabularyEntry Resolve(WordRef reference) => byId.TryGetValue(reference, out var entry) ? entry
        : throw new InvalidDataException($"Unknown vocabulary reference {reference}.");
    public bool Contains(WordRef reference) => byId.ContainsKey(reference);
    public static GenkiVocabulary Load(string root) => new(
        Read<Word>(root, "Words", "word").Concat(Read<Word>(root, "Nouns", "noun"))
            .Concat(Read<Verb>(root, "Verbs", "verb")).Concat(Read<Adjective>(root, "Adjectives", "adjective")));
    private static IEnumerable<VocabularyEntry> Read<T>(string root, string folder, string collection) where T : Word =>
        new JsonLoader<T>(Path.Combine(root, folder), folder + ".json").Load()
            .Select(w => new VocabularyEntry(new(collection, w.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), w));
}

/// <summary>Multi-parent classification inheritance; unknown is never a negative assignment.</summary>
public sealed class SemanticRegistry
{
    public IReadOnlyDictionary<string, SemanticTag> Tags { get; }
    private readonly Dictionary<string, HashSet<string>> ancestors = new(StringComparer.Ordinal);
    public SemanticRegistry(IEnumerable<SemanticTag> definitions)
    {
        var items = definitions.ToArray();
        if (items.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name) ||
            string.IsNullOrWhiteSpace(x.Description)) || items.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            throw new InvalidDataException("Tag IDs, names and descriptions must be nonempty and IDs unique.");
        Tags = items.ToDictionary(x => x.Id, StringComparer.Ordinal);
        HashSet<string> Visit(string id, HashSet<string> path)
        {
            if (ancestors.TryGetValue(id, out var result)) return result;
            if (!Tags.TryGetValue(id, out var tag)) throw new InvalidDataException($"Missing parent tag {id}.");
            if (!path.Add(id)) throw new InvalidDataException($"Cyclic tag hierarchy at {id}.");
            result = new(StringComparer.Ordinal) { id };
            foreach (var parent in tag.Parents) result.UnionWith(Visit(parent, path));
            path.Remove(id); ancestors[id] = result; return result;
        }
        foreach (var id in Tags.Keys) Visit(id, new(StringComparer.Ordinal));
    }
    public HashSet<string> Expand(IEnumerable<string> direct) => direct.SelectMany(id => ancestors.TryGetValue(id, out var set)
        ? set : throw new InvalidDataException($"Unregistered tag {id}.")).ToHashSet(StringComparer.Ordinal);
    public IReadOnlyList<string> MostSpecific(IEnumerable<string> tags)
    {
        var all = tags.Distinct(StringComparer.Ordinal).ToArray();
        return all.Where(id => !all.Any(other => other != id && ancestors[other].Contains(id))).Order(StringComparer.Ordinal).ToArray();
    }
    public void Validate(TagFilter filter)
    {
        foreach (var id in filter.AllOf.Concat(filter.AnyOf).Concat(filter.NoneOf))
            if (!Tags.ContainsKey(id)) throw new InvalidDataException($"Unregistered schema tag {id}.");
    }
    public bool Matches(TagFilter filter, IEnumerable<string> direct)
    {
        Validate(filter); var membership = Expand(direct);
        return filter.AllOf.All(membership.Contains) && (filter.AnyOf.Count == 0 || filter.AnyOf.Any(membership.Contains)) &&
            !filter.NoneOf.Any(membership.Contains);
    }
}
