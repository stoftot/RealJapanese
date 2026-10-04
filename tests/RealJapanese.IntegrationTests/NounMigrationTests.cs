using System.Security.Cryptography;
using System.Text.Json;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Preserves word identity and study state through the noun catalog split and subsequent saves.</summary>
public sealed class NounMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Old_progress_splits_all_categories_and_is_not_migrated_again_after_restart(bool unified)
    {
        using var workspace = new TestWorkspace();
        var paths = workspace.CreatePaths();
        var oldWords = new VocabSaveFile
        {
            KnownIds = [101, 227, 47], // umbrella, food, um...
            TrainingIds = [19, 95], // university, delicious
            RehearsingIds = [76, 316, 298] // mother, wrist, if you like
        };
        var progressFile = Path.Combine(paths.ProgressRoot, "Progress.json");
        string originalPath;
        if (unified)
        {
            Directory.CreateDirectory(paths.ProgressRoot);
            var data = OldData(oldWords);
            data["Verbs"].KnownIds.Add(0);
            File.WriteAllText(progressFile, JsonSerializer.Serialize(new { Version = 1, Revision = Guid.NewGuid(), Data = data }));
            originalPath = progressFile;
        }
        else
        {
            originalPath = Path.Combine(paths.ProgressRoot, "Words", "SavedData.json");
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            File.WriteAllText(originalPath, JsonSerializer.Serialize(oldWords));
        }
        var originalBytes = File.ReadAllBytes(originalPath);
        AssertMigrated(paths);
        Assert.Equal(originalBytes, File.ReadAllBytes(originalPath)); // Loading is read-only.

        var nouns = new NounData(paths);
        nouns.AddToVocab(nouns.Words.Single(word => word.Japanese == "傘"));
        using var persisted = JsonDocument.Parse(File.ReadAllBytes(progressFile));
        Assert.Equal(2, persisted.RootElement.GetProperty("Version").GetInt32());
        AssertMigrated(new RepositoryPaths(paths.CatalogRoot, paths.ProgressRoot));
        if (unified) Assert.Equal([0], new VerbData(paths).VocabWordIds);
        else Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
    }

    [Fact]
    public void Bundled_saves_keep_learned_nouns_and_compact_catalogs_without_remapping_twice()
    {
        using var workspace = new TestWorkspace();
        var paths = workspace.CreatePaths();
        foreach (var name in new[] { "Words", "Nouns" })
        {
            var folder = Path.Combine(paths.ProgressRoot, name);
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(TestWorkspace.RepositoryRoot, "RealJapanese", "Data", name, "SavedData.json"),
                Path.Combine(folder, "SavedData.json"));
        }
        var words = new WordData(paths);
        var nouns = new NounData(paths);
        Assert.Equal(Enumerable.Range(0, 97), words.Words.Select(word => word.Id));
        Assert.Equal(Enumerable.Range(0, 220), nouns.Words.Select(word => word.Id));
        Assert.Equal(99, nouns.VocabWords.Count);
        Assert.Contains(nouns.VocabWords, word => word.Japanese == "傘");
        Assert.Contains(nouns.VocabWords, word => word.Japanese == "手首");
        Assert.Contains(nouns.TrainingWords, word => word.Japanese == "大学");
        Assert.Contains(nouns.RehearsingWords, word => word.Japanese == "魚");
        Assert.Contains(words.TrainingWords, word => word.Japanese == "おはよう");
        Assert.Contains(words.VocabWords, word => word.Japanese == "よかったら");
        Assert.DoesNotContain(words.Words, word => word.Category.Equals("noun", StringComparison.OrdinalIgnoreCase));
        nouns.AddToVocab(nouns.VocabWords.First());
        Assert.Equal(nouns.VocabWords, new NounData(new RepositoryPaths(paths.CatalogRoot, paths.ProgressRoot)).VocabWords);
    }

    [Theory]
    [InlineData("DEE9C498B9A434AA683E45040C067C213F3AE392A1698EB0D844FDCE9BB45689")]
    [InlineData("3358F3BB953BB5F18AE2701B46B52E894E9D8A96C32E319A1EB2DB348EEB3813")]
    public void Pre_split_import_recovery_restores_nouns_and_remaining_words(string oldWordsHash)
    {
        using var workspace = new TestWorkspace();
        var paths = workspace.CreatePaths();
        var hashes = ProgressStore.DatasetNames.Where(name => name != "Nouns").ToDictionary(name => name,
            name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                Path.Combine(paths.CatalogRoot, name, name.Split('/')[^1] + ".json")))));
        hashes["Words"] = oldWordsHash;
        var recovery = new ProgressSnapshot
        {
            Version = 1, CatalogHashes = hashes,
            Data = OldData(new VocabSaveFile { KnownIds = [101, 47], TrainingIds = [19], RehearsingIds = [76] })
        };
        Directory.CreateDirectory(paths.ProgressRoot);
        File.WriteAllText(Path.Combine(paths.ProgressRoot, "Progress.json"), JsonSerializer.Serialize(new
        {
            Version = 1, Revision = Guid.NewGuid(), Data = OldData(new()), Recovery = recovery
        }));
        var words = new WordData(paths);
        var nouns = new NounData(paths);
        var sync = new ProgressSyncService(paths, words, new VerbData(paths), new AdjectiveData(paths), new KanjiData(paths), nouns);
        sync.Apply(sync.PreviewRecovery());
        Assert.Equal(["傘"], nouns.VocabWords.Select(word => word.Japanese));
        Assert.Equal(["大学"], nouns.TrainingWords.Select(word => word.Japanese));
        Assert.Equal(["お母さん"], nouns.RehearsingWords.Select(word => word.Japanese));
        Assert.Equal(["あのう"], words.VocabWords.Select(word => word.Japanese));
    }

    private static Dictionary<string, VocabSaveFile> OldData(VocabSaveFile words) => new()
    {
        ["Words"] = words, ["Verbs"] = new(), ["Adjectives"] = new(), ["Kanji/Singel"] = new(), ["Kanji/Combined"] = new()
    };

    private static void AssertMigrated(RepositoryPaths paths)
    {
        var words = new WordData(paths);
        var nouns = new NounData(paths);
        Assert.Equal(new[] { "傘", "食べ物" }.Order(), nouns.VocabWords.Select(word => word.Japanese).Order());
        Assert.Equal(["大学"], nouns.TrainingWords.Select(word => word.Japanese));
        Assert.Equal(["お母さん", "手首"], nouns.RehearsingWords.Select(word => word.Japanese));
        Assert.Equal(["あのう"], words.VocabWords.Select(word => word.Japanese));
        Assert.Equal(["おいしい"], words.TrainingWords.Select(word => word.Japanese));
        Assert.Equal(["よかったら"], words.RehearsingWords.Select(word => word.Japanese));
    }
}
