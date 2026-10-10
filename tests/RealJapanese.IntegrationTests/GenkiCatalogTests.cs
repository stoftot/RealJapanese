using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Checks the independently transcribed lesson list manifest, grammatical types and catalog/save/relation ID integrity.</summary>
public sealed class GenkiCatalogTests
{
    [Fact]
    public void Every_opening_vocabulary_row_has_a_catalog_entry_in_its_lesson_and_no_unlisted_book_entries()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var catalogs = Catalogs(workspace);
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestWorkspace.RepositoryRoot, "tests", "fixtures", "genki-vocabulary.json")));
        var lessons = source.RootElement.GetProperty("lessons").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 12), lessons.Select(lesson => lesson.GetProperty("lesson").GetInt32()));
        int[] expectedCounts = [66, 57, 56, 62, 52, 47, 56, 56, 55, 56, 62, 53];
        var references = new HashSet<(string Dataset, int Id, string Origin)>();
        foreach (var lesson in lessons)
        {
            var number = lesson.GetProperty("lesson").GetInt32();
            var origin = $"GENKI I (3rd edition)-{number}";
            var rows = lesson.GetProperty("rows").EnumerateArray().ToArray();
            Assert.Equal(expectedCounts[number - 1], rows.Length);
            var pages = lesson.GetProperty("pdfPages").EnumerateArray().Select(page => page.GetInt32()).ToArray();
            foreach (var row in rows)
            {
                Assert.Contains(row.GetProperty("pdfPage").GetInt32(), pages);
                var targets = row.GetProperty("targets").EnumerateArray().ToArray();
                Assert.NotEmpty(targets);
                foreach (var target in targets)
                {
                    var dataset = target.GetProperty("dataset").GetString()!;
                    var id = target.GetProperty("id").GetInt32();
                    var word = Assert.Single(catalogs[dataset], word => word.Id == id);
                    Assert.Contains(origin, new[] { word.Origin }.Concat(word.AdditionalOrigins));
                    references.Add((dataset, id, origin));
                }
            }
        }
        foreach (var (dataset, words) in catalogs)
        foreach (var word in words)
        foreach (var origin in new[] { word.Origin }.Concat(word.AdditionalOrigins))
        {
            Assert.False(string.IsNullOrWhiteSpace(origin));
            if (origin != "self added") Assert.Contains((dataset, word.Id, origin), references);
        }
    }

    [Fact]
    public void Catalogs_have_unique_ids_valid_grammar_types_and_matching_saved_and_kanji_references()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var catalogs = Catalogs(workspace);
        foreach (var (dataset, words) in catalogs)
        {
            Assert.Equal(words.Length, words.Select(word => word.Id).Distinct().Count());
            Assert.All(words, word => Assert.True(word.Id >= 0));
            var saved = new JsonLoader<VocabSaveFile>(Path.Combine(TestWorkspace.RepositoryRoot, "RealJapanese", "Data", dataset), "SavedData.json").Load().Single();
            var ids = saved.KnownIds.Concat(saved.TrainingIds).Concat(saved.RehearsingIds).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            Assert.All(ids, id => Assert.Contains(words, word => word.Id == id));
        }
        var paths = workspace.CreatePaths();
        Assert.Empty(new WordData(paths).Words);
        Assert.All(new VerbData(paths).Words, word => Assert.Contains(word.Type, new[] { "u", "ru", "irregular" }));
        Assert.All(new AdjectiveData(paths).Words, word => Assert.Contains(word.Type, new[] { "i", "na", "irregular" }));
        Assert.Contains(new AdjectiveData(paths).Words, word => word.Kana == "だいじょうぶ" && word.Type == "na");
        Assert.Contains(new AdjectiveData(paths).Words, word => word.Japanese == "易しい" && word.English.Contains("easy"));
        Assert.DoesNotContain(new NounData(paths).Words, word => word.Japanese is "私" or "あなた" or "僕" or "一時間");
        Assert.Contains(new PronounData(paths).Words, word => word.Kana == "いつ");
        Assert.Contains(new NounData(paths).Words, word => word.Kana == "いくら");
        Assert.Contains(new AdverbData(paths).Words, word => word.Kana == "なにも");

        using var relations = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestWorkspace.RepositoryRoot,
            "RealJapanese", "Data", "Kanji", "FromOtherData", "kanjiRelationData.json")));
        foreach (var relation in relations.RootElement.EnumerateArray())
        foreach (var (dataset, words) in catalogs)
        {
            var field = dataset == "Suffixes" ? "SuffixIds" : dataset[..^1] + "Ids";
            foreach (var id in relation.GetProperty(field).EnumerateArray())
                Assert.Contains(words, word => word.Id == id.GetInt32());
        }
    }

    private static Dictionary<string, Word[]> Catalogs(TestWorkspace workspace) => ProgressStore.DatasetNames
        .Where(name => !name.StartsWith("Kanji/")).ToDictionary(name => name,
            name => new JsonLoader<Word>(Path.Combine(workspace.CatalogRoot, name), name + ".json").Load().ToArray());
}
