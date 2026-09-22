using RealJapanese.TestSupport;
using Repositories;

namespace RealJapanese.IntegrationTests;

/// <summary>Checks catalog loading and isolated progress persistence against disposable catalog copies.</summary>
public sealed class RepositoryPersistenceTests
{
    [Fact]
    public void Repository_construction_reads_valid_catalogs_without_creating_or_rewriting_progress()
    {
        using var workspace = new TestWorkspace();
        var originalFiles = Directory.EnumerateFiles(workspace.CatalogRoot, "*.json", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        var paths = workspace.CreatePaths();

        var words = new WordData(paths);
        var verbs = new VerbData(paths);
        var adjectives = new AdjectiveData(paths);
        var kanji = new KanjiData(paths);

        AssertUniqueIds(words.Words.Select(word => word.Id));
        AssertUniqueIds(verbs.Words.Select(word => word.Id));
        AssertUniqueIds(adjectives.Words.Select(word => word.Id));
        AssertUniqueIds(kanji.Single.Words.Select(word => word.Id));
        AssertUniqueIds(kanji.Combined.Words.Select(word => word.Id));
        Assert.Empty(words.VocabWordIds);
        Assert.Empty(verbs.VocabWordIds);
        Assert.Empty(adjectives.VocabWordIds);
        Assert.Empty(kanji.Single.VocabWordIds);
        Assert.Empty(kanji.Combined.VocabWordIds);
        Assert.False(Directory.Exists(paths.ProgressRoot));
        foreach (var (path, bytes) in originalFiles) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void Progress_roots_are_independent_survive_restart_and_filter_stale_legacy_ids()
    {
        using var workspace = new TestWorkspace();
        var firstPaths = workspace.CreatePaths("first-progress");
        var secondPaths = workspace.CreatePaths("second-progress");
        var first = new WordData(firstPaths);
        var second = new WordData(secondPaths);
        var firstWord = first.Words.First();
        var secondWord = second.Words.Skip(1).First();

        first.AddToTraining(firstWord);
        second.AddToVocab(secondWord);

        var restartedFirst = new WordData(firstPaths);
        var restartedSecond = new WordData(secondPaths);
        Assert.Equal([firstWord.Id], restartedFirst.TrainingWordIds);
        Assert.Empty(restartedFirst.VocabWordIds);
        Assert.Equal([secondWord.Id], restartedSecond.VocabWordIds);
        Assert.Empty(restartedSecond.TrainingWordIds);

        var stalePaths = workspace.CreatePaths("stale-legacy-progress");
        var staleProgressPath = Path.Combine(stalePaths.ProgressRoot, "Words", "SavedData.json");
        Directory.CreateDirectory(Path.GetDirectoryName(staleProgressPath)!);
        File.WriteAllText(staleProgressPath,
            $$"""{ "KnownIds": [{{firstWord.Id}}, 2147483647], "TrainingIds": [], "RehearsingIds": [] }""");
        var withStaleId = new WordData(stalePaths);
        Assert.Equal([firstWord.Id], withStaleId.VocabWords.Select(word => word.Id));
        Assert.Equal([firstWord.Id], withStaleId.VocabWordIds);
    }

    /// <summary>Missing IDs are assigned in memory after the highest explicit ID and never written into the catalog.</summary>
    [Fact]
    public void Missing_catalog_ids_are_stable_across_repository_instances_without_rewriting_source()
    {
        using var workspace = new TestWorkspace();
        var catalogRoot = Path.Combine(workspace.Root, "missing-id-catalog");
        var wordsFolder = Path.Combine(catalogRoot, "Words");
        Directory.CreateDirectory(wordsFolder);
        var catalogPath = Path.Combine(wordsFolder, "Words.json");
        File.WriteAllText(catalogPath,
            """
            [
              { "japanese": "一", "kana": "いち", "english": "one" },
              { "id": "5", "japanese": "二", "kana": "に", "english": "two" },
              { "japanese": "三", "kana": "さん", "english": "three" }
            ]
            """);
        var original = File.ReadAllBytes(catalogPath);
        var paths = new RepositoryPaths(catalogRoot, Path.Combine(workspace.Root, "missing-id-progress"));

        var firstIds = new WordData(paths).Words.Select(word => word.Id).ToArray();
        var secondIds = new WordData(paths).Words.Select(word => word.Id).ToArray();

        Assert.Equal([6, 5, 7], firstIds);
        Assert.Equal(firstIds, secondIds);
        Assert.Equal(original, File.ReadAllBytes(catalogPath));
    }

    private static void AssertUniqueIds(IEnumerable<int> ids)
    {
        var values = ids.ToArray();
        Assert.NotEmpty(values);
        Assert.All(values, id => Assert.True(id >= 0));
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}
