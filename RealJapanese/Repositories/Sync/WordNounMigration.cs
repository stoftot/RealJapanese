using System.Security.Cryptography;
using System.Text.Json;
using DataLoaders.Models;

namespace Repositories.Sync;

/// <summary>Maps the pre-noun catalog IDs to the two compact catalogs exactly once.</summary>
internal static class WordNounMigration
{
    private static readonly SplitManifest Manifest = LoadManifest();
    private static readonly Dictionary<int, int> Words = Index(Manifest.WordIds);
    private static readonly Dictionary<int, int> Nouns = Index(Manifest.NounIds);

    internal static void Apply(Dictionary<string, VocabSaveFile> data)
    {
        var original = data["Words"];
        data["Words"] = Remap(original, Words);
        data.Add("Nouns", Remap(original, Nouns));
    }

    internal static void ApplyRecovery(ProgressSnapshot? snapshot, string catalogRoot)
    {
        if (snapshot is null || snapshot.Version != 1 || snapshot.CatalogHashes is null ||
            !Manifest.OldWordsHashes.Contains(snapshot.CatalogHashes.GetValueOrDefault("Words")))
            return; // Preserve incompatible recovery snapshots; normal sync validation rejects them.

        ProgressStore.ValidateShape(snapshot.Data, includeNouns: false);
        Apply(snapshot.Data);
        snapshot.Version = 2;
        foreach (var name in new[] { "Words", "Nouns" })
            snapshot.CatalogHashes[name] = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(catalogRoot, name, name + ".json"))));
    }

    private static VocabSaveFile Remap(VocabSaveFile saved, Dictionary<int, int> map) => new()
    {
        KnownIds = Map(saved.KnownIds, map),
        TrainingIds = Map(saved.TrainingIds, map),
        RehearsingIds = Map(saved.RehearsingIds, map)
    };

    // Stale IDs outside the old catalog have always been ignored by repositories.
    private static List<int> Map(List<int> ids, Dictionary<int, int> map) =>
        ids.Where(map.ContainsKey).Select(id => map[id]).ToList();

    private static Dictionary<int, int> Index(int[] ids) =>
        ids.Select((oldId, newId) => (oldId, newId)).ToDictionary(pair => pair.oldId, pair => pair.newId);

    private static SplitManifest LoadManifest()
    {
        using var stream = typeof(WordNounMigration).Assembly.GetManifestResourceStream("Repositories.Sync.WordNounSplit.json")!;
        return JsonSerializer.Deserialize<SplitManifest>(stream)!;
    }

    private sealed class SplitManifest
    {
        // Git checkouts can contain either CRLF or LF versions of the original file.
        public string[] OldWordsHashes { get; set; } = [];
        public int[] WordIds { get; set; } = [];
        public int[] NounIds { get; set; } = [];
    }
}
