using System.Security.Cryptography;
using System.Text.Json;
using DataLoaders.Models;

namespace Repositories.Sync;

/// <summary>Moves version 2 vocabulary progress with its lexical identity, exactly once.</summary>
internal static class VocabularyMigration
{
    private static readonly SplitManifest Manifest = LoadManifest();
    private static readonly Dictionary<(string Dataset, int Id), Entry> Entries = Manifest.Entries
        .ToDictionary(entry => (entry.SourceDataset, entry.SourceId));

    internal static string PreviousCatalogHash(string dataset) => Manifest.OldCatalogHashes[dataset][0];

    internal static void Apply(Dictionary<string, VocabSaveFile> data)
    {
        var result = ProgressStore.DatasetNames.ToDictionary(name => name, _ => new VocabSaveFile());
        // Known wins over training, which wins over rehearsing if old duplicate
        // lexical records converge. Every original identity is recorded in the manifest.
        foreach (var category in new[] { WordPracticeCategory.Known, WordPracticeCategory.Training, WordPracticeCategory.Rehearsing })
        foreach (var (dataset, saved) in data)
        foreach (var id in Ids(saved, category))
        {
            var target = Entries.GetValueOrDefault((dataset, id));
            var destination = result[target?.TargetDataset ?? dataset];
            var targetId = target?.TargetId ?? id;
            if (!destination.KnownIds.Contains(targetId) && !destination.TrainingIds.Contains(targetId) &&
                !destination.RehearsingIds.Contains(targetId))
                Ids(destination, category).Add(targetId);
        }
        data.Clear();
        foreach (var pair in result) data.Add(pair.Key, pair.Value);
    }

    internal static void ApplyRecovery(ProgressSnapshot? snapshot, string catalogRoot)
    {
        // Never relabel an unknown catalog's IDs as compatible with this catalog.
        if (snapshot is null || snapshot.Version != 2 || snapshot.CatalogHashes is null ||
            Manifest.OldCatalogHashes.Any(pair => !pair.Value.Contains(snapshot.CatalogHashes.GetValueOrDefault(pair.Key), StringComparer.OrdinalIgnoreCase)))
            return;
        ProgressStore.ValidateShape(snapshot.Data, version: 2);
        Apply(snapshot.Data);
        snapshot.Version = ProgressStore.CurrentVersion;
        foreach (var name in Manifest.OldCatalogHashes.Keys.Concat(VocabularyCatalog.AdditionalDatasets))
            snapshot.CatalogHashes[name] = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(catalogRoot, name, name + ".json"))));
    }

    private static List<int> Ids(VocabSaveFile saved, WordPracticeCategory category) => category switch
    {
        WordPracticeCategory.Known => saved.KnownIds,
        WordPracticeCategory.Training => saved.TrainingIds,
        _ => saved.RehearsingIds
    };

    private static SplitManifest LoadManifest()
    {
        using var stream = typeof(VocabularyMigration).Assembly.GetManifestResourceStream("Repositories.Sync.VocabularySplit.json")!;
        return JsonSerializer.Deserialize<SplitManifest>(stream)!;
    }

    private sealed class SplitManifest
    {
        public Dictionary<string, string[]> OldCatalogHashes { get; set; } = [];
        public Entry[] Entries { get; set; } = [];
    }

    private sealed class Entry
    {
        public string SourceDataset { get; set; } = "";
        public int SourceId { get; set; }
        public string TargetDataset { get; set; } = "";
        public int TargetId { get; set; }
    }
}
