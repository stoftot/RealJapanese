using System.Text;
using System.Text.Json;

namespace AudioStudio;

public sealed class WordCatalog
{
    public static readonly string[] Datasets = ["Nouns", "Verbs", "Adjectives", "Words", "Adverbs", "Expressions", "Pronouns", "Determiners", "Counters", "Conjunctions", "Particles", "Suffixes"];
    public IReadOnlyList<CatalogWord> Words { get; }
    private readonly Dictionary<string, CatalogWord> byKey;

    public WordCatalog(string dataRoot)
    {
        var words = new List<CatalogWord>();
        foreach (string dataset in Datasets)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, dataset, dataset + ".json")));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.GetProperty("id");
                int number = id.ValueKind == JsonValueKind.String ? int.Parse(id.GetString()!) : id.GetInt32();
                if (number < 0) throw new InvalidDataException($"{dataset} contains an unassigned ID. Assign stable catalog IDs before generating audio.");
                string Value(string key) => item.GetProperty(key).GetString() ?? "";
                var word = new CatalogWord(dataset, number, dataset is "Nouns" or "Verbs" or "Adjectives" ? dataset : "Words & expressions",
                    Value("japanese"), Value("kana"), Value("english"));
                if (string.IsNullOrWhiteSpace(word.Kana) || string.IsNullOrWhiteSpace(word.Japanese))
                    throw new InvalidDataException($"{word.Key} needs Japanese and kana text.");
                words.Add(word);
            }
        }
        Words = words.OrderBy(w => Array.IndexOf(Datasets, w.Dataset)).ThenBy(w => w.Id).ToArray();
        byKey = Words.ToDictionary(w => w.Key, StringComparer.Ordinal);
    }

    public CatalogWord Get(string key) => byKey.TryGetValue(key, out var word) ? word : throw new StudioException("Unknown word: " + key);
    // Catalog placeholders describe where another word belongs; they are not
    // spoken syllables. Keep these consumer-only adaptations out of study data.
    private static string WithoutPlaceholders(string text) => text.Replace("〜", "").Replace("～", "");
    public static string SpokenJapanese(CatalogWord word) => WithoutPlaceholders(word.Japanese);
    public static string SpokenKana(CatalogWord word) => word.Kana == "Tシャツ" ? "ティーシャツ" : WithoutPlaceholders(word.Kana);
    public void VerifyCurrent(CatalogWord snapshot)
    {
        if (Get(snapshot.Key) != snapshot) throw new StudioException($"The catalog entry for {snapshot.Key} has changed. Generate it again before exporting.", 409);
    }

    // Compare conservative surface readings only; this is not a pronunciation/pitch score.
    public static string NormalizeReading(string value)
    {
        var result = new StringBuilder();
        foreach (char c in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(c) || "。、，,.!?！？「」『』\"'".Contains(c)) continue;
            result.Append(c is >= '\u30a1' and <= '\u30f6' ? (char)(c - 0x60) : c);
        }
        return result.ToString();
    }
    public static string ValidateTranscript(CatalogWord word, string? transcript, string? error)
    {
        if (error is not null) return "Check failed";
        if (string.IsNullOrWhiteSpace(transcript)) return "No speech recognized";
        string recognized = NormalizeReading(transcript);
        return recognized == NormalizeReading(SpokenKana(word)) || recognized == NormalizeReading(SpokenJapanese(word))
            ? "Text matches" : "Review reading";
    }
}
