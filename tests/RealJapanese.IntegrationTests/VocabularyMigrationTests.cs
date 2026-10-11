using System.Security.Cryptography;
using System.Text.Json;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Protects every old vocabulary identity, saved category, restart and recovery through the grammatical split.</summary>
public sealed class VocabularyMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_version_two_assignment_moves_once_and_survives_restart(bool unified)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var entries = Manifest().Entries;
        var old = ProgressStore.Version2DatasetNames.ToDictionary(name => name, _ => new VocabSaveFile());
        foreach (var entry in entries) Ids(old[entry.SourceDataset], entry.SourceId % 3).Add(entry.SourceId);
        old["Kanji/Singel"].KnownIds.Add(new KanjiData(paths).Single.Words.First().Id);
        Directory.CreateDirectory(paths.ProgressRoot);
        var progressFile = Path.Combine(paths.ProgressRoot, "Progress.json");
        if (unified)
            File.WriteAllText(progressFile, JsonSerializer.Serialize(new { Version = 2, Revision = Guid.NewGuid(), Data = old }));
        else
            foreach (var (name, saved) in old)
            {
                var folder = Path.Combine(paths.ProgressRoot, name);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "SavedData.json"), JsonSerializer.Serialize(saved));
            }
        var original = Directory.GetFiles(paths.ProgressRoot, "*.json", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);

        var migrated = Snapshot(paths);
        Assert.Equal(ProgressStore.CurrentVersion, migrated.Version);
        Assert.Equal(413, entries.Length);
        Assert.Equal(414, migrated.Data.Values.Sum(saved => saved.KnownIds.Count + saved.TrainingIds.Count + saved.RehearsingIds.Count));
        foreach (var entry in entries)
            Assert.Contains(entry.TargetId, Ids(migrated.Data[entry.TargetDataset], entry.SourceId % 3));
        Assert.Contains(new AdjectiveData(paths).RehearsingWords, word => word.Japanese == "おいしい"); // old Words 44
        Assert.Contains(new AdverbData(paths).TrainingWords, word => word.Japanese == "あまり"); // old Words 58
        Assert.Contains(new PronounData(paths).RehearsingWords, word => word.Japanese == "私"); // old Nouns 8
        Assert.Contains(new ExpressionData(paths).VocabWords, word => word.Japanese == "おはよう"); // old Words 0
        foreach (var (file, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(file));

        var nouns = new NounData(paths);
        nouns.AddToVocab(nouns.VocabWords.First());
        var restarted = Snapshot(new RepositoryPaths(paths.CatalogRoot, paths.ProgressRoot));
        Assert.Equal(JsonSerializer.Serialize(migrated.Data), JsonSerializer.Serialize(restarted.Data));
        using var persisted = JsonDocument.Parse(File.ReadAllBytes(progressFile));
        Assert.Equal(ProgressStore.CurrentVersion, persisted.RootElement.GetProperty("Version").GetInt32());
        if (!unified)
            foreach (var (file, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(file));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Recovery_migrates_only_a_recognized_old_catalog(bool compatible)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var manifest = Manifest();
        var data = ProgressStore.Version2DatasetNames.ToDictionary(name => name, _ => new VocabSaveFile());
        var hashes = ProgressStore.Version2DatasetNames.ToDictionary(name => name, name =>
            manifest.OldCatalogHashes.TryGetValue(name, out var old) ? old[0].ToUpperInvariant() : Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(paths.CatalogRoot, name, name.Split('/')[^1] + ".json")))));
        if (!compatible) hashes["Adjectives"] = "UNKNOWN";
        var recoveryData = JsonSerializer.Deserialize<Dictionary<string, VocabSaveFile>>(JsonSerializer.Serialize(data))!;
        recoveryData["Words"].KnownIds = [44, 58];
        var recovery = new ProgressSnapshot { Version = 2, Data = recoveryData, CatalogHashes = hashes };
        Directory.CreateDirectory(paths.ProgressRoot);
        var file = Path.Combine(paths.ProgressRoot, "Progress.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new { Version = 2, Revision = Guid.NewGuid(), Data = data, Recovery = recovery }));
        var original = File.ReadAllBytes(file);
        var service = Sync(paths);
        if (compatible)
        {
            var preview = service.PreviewRecovery();
            Assert.Equal(original, File.ReadAllBytes(file));
            service.Apply(preview);
            Assert.Equal(["おいしい"], new AdjectiveData(paths).VocabWords.Select(word => word.Japanese));
            Assert.Equal(["あまり"], new AdverbData(paths).VocabWords.Select(word => word.Japanese));
        }
        else
        {
            Assert.True(service.HasRecovery);
            Assert.Throws<InvalidDataException>(() => service.PreviewRecovery());
            Assert.Equal(original, File.ReadAllBytes(file));
        }
    }

    [Fact]
    public void Bulk_assignment_is_exclusive_persistent_and_rejects_invalid_groups_without_partial_save()
    {
        using var workspace = new TestWorkspace();
        var paths = workspace.CreatePaths();
        var repository = new AdverbData(paths);
        var group = repository.Words.Take(4).ToList();
        repository.AddToVocab(group[0]);
        repository.AddToRehearsing(group[1]);
        repository.SetCategory(group, WordPracticeCategory.Training);
        Assert.Empty(repository.VocabWords);
        Assert.Empty(repository.RehearsingWords);
        Assert.Equal(group.Select(word => word.Id).Order(), new AdverbData(workspace.CreatePaths()).TrainingWordIds.Order());

        var file = Path.Combine(paths.ProgressRoot, "Progress.json");
        var saved = File.ReadAllBytes(file);
        Assert.Throws<ArgumentException>(() => repository.SetCategory(
            [group[0], group[0] with { Id = int.MaxValue }], WordPracticeCategory.Known));
        Assert.Equal(saved, File.ReadAllBytes(file));
        repository.RemoveFromCategory(group.Take(2), WordPracticeCategory.Training);
        Assert.Equal(group.Skip(2).Select(word => word.Id).Order(), repository.TrainingWordIds.Order());
    }

    private static ProgressSyncService Sync(RepositoryPaths paths) =>
        new(paths, new WordData(paths), new VerbData(paths), new AdjectiveData(paths), new KanjiData(paths), new NounData(paths));

    [Theory]
    [InlineData("Adverbs")]
    [InlineData("Expressions")]
    [InlineData("Pronouns")]
    [InlineData("Determiners")]
    [InlineData("Counters")]
    [InlineData("Conjunctions")]
    [InlineData("Particles")]
    [InlineData("Suffixes")]
    public void Self_added_words_with_missing_ids_keep_their_progress_through_sync(string dataset)
    {
        using var workspace = new TestWorkspace();
        var file = Path.Combine(workspace.CatalogRoot, dataset, dataset + ".json");
        var words = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(file))!.AsArray();
        words.Add(System.Text.Json.Nodes.JsonNode.Parse("""{"japanese":"追加","kana":"ついか","english":"extra"}"""));
        File.WriteAllText(file, words.ToJsonString());
        var source = workspace.CreatePaths("source");
        var target = workspace.CreatePaths("target");
        var sourceRepository = new TestVocabularyData(source, dataset);
        var added = sourceRepository.Words.Single(word => word.English == "extra");
        Assert.True(added.Id >= 0);
        sourceRepository.AddToTraining(added);
        var exported = Sync(source).ExportSnapshot();
        var sync = Sync(target);
        sync.Apply(sync.PreviewSnapshot(exported, ImportMode.Replace));
        Assert.Equal([added.Id], new TestVocabularyData(target, dataset).TrainingWordIds);
    }

    private sealed class TestVocabularyData(RepositoryPaths paths, string name)
        : Repositories.Bases.WordDataBase<Word>(paths, name, name + ".json");

    private static ProgressSnapshot Snapshot(RepositoryPaths paths) => JsonSerializer.Deserialize<ProgressSnapshot>(Sync(paths).ExportSnapshot())!;

    private static List<int> Ids(VocabSaveFile saved, int category) => category switch
    {
        0 => saved.KnownIds, 1 => saved.TrainingIds, _ => saved.RehearsingIds
    };

    private static SplitManifest Manifest() => JsonSerializer.Deserialize<SplitManifest>(File.ReadAllBytes(Path.Combine(
        TestWorkspace.RepositoryRoot, "RealJapanese", "Repositories", "Sync", "VocabularySplit.json")))!;

    private sealed record SplitManifest(Dictionary<string, string[]> OldCatalogHashes, Entry[] Entries);
    private sealed record Entry(string SourceDataset, int SourceId, string TargetDataset, int TargetId);
}
