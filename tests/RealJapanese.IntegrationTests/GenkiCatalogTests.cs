using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Checks the transcribed lesson and introductory lists, grammatical types and catalog/save/relation ID integrity.</summary>
public sealed class GenkiCatalogTests
{
    [Fact]
    public void Repeated_word_senses_share_the_earliest_entry_but_homophones_remain_distinct()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var seen = new HashSet<(string?, string?, string?)>();
        foreach (var dataset in ProgressStore.DatasetNames.Where(name => !name.StartsWith("Kanji/")))
        {
            using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(workspace.CatalogRoot, dataset, dataset + ".json")));
            var entries = catalog.RootElement.EnumerateArray().ToArray();
            var identities = entries.Select(word => (word.GetProperty("japanese").GetString(),
                word.GetProperty("kana").GetString(),
                Type: word.TryGetProperty("type", out var type) ? type.GetString() : null)).ToArray();
            Assert.Equal(identities.Length, identities.Distinct().Count());
            Assert.All(identities, identity => Assert.True(seen.Add(identity), $"Repeated word: {identity}"));
        }
        var verbs = new VerbData(workspace.CreatePaths()).Words.ToArray();
        var kiku = Assert.Single(verbs, word => word.Japanese == "聞く");
        Assert.Equal(2, kiku.Id);
        Assert.Equal("to listen; to hear; to ask", kiku.English);
        Assert.Equal("GENKI I (3rd edition)-3", kiku.Origin);
        Assert.Contains("GENKI I (3rd edition)-5", kiku.AdditionalOrigins);
        Assert.DoesNotContain(verbs, word => word.Id == 61);
        Assert.Contains(verbs, word => word.Id == 20 && word.Type == "ru");
        Assert.Contains(verbs, word => word.Id == 64 && word.Type == "u");
        var pronouns = new PronounData(workspace.CreatePaths()).Words.ToArray();
        Assert.Contains(pronouns, word => word.Id == 21 && word.English == "she; girlfriend");
        Assert.Contains(pronouns, word => word.Id == 22 && word.English == "he; boyfriend");
        Assert.DoesNotContain(new NounData(workspace.CreatePaths()).Words, word => word.Id is 348 or 349);
    }

    [Fact]
    public void Every_vocabulary_row_has_a_catalog_entry_in_its_book_section_and_no_unlisted_book_entries()
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
        var introduction = Assert.Single(source.RootElement.GetProperty("introductorySections").EnumerateArray());
        const string introductionOrigin = "GENKI I (3rd edition)-Introduction: Greetings & introductions";
        Assert.Equal(introductionOrigin, introduction.GetProperty("origin").GetString());
        Assert.Equal("Greetings & introductions", introduction.GetProperty("name").GetString());
        Assert.Equal(41, Assert.Single(introduction.GetProperty("pdfPages").EnumerateArray()).GetInt32());
        var introductoryRows = introduction.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(19, introductoryRows.Length);
        foreach (var (row, id) in introductoryRows.Select((row, id) => (row, id)))
        {
            Assert.Equal(41, row.GetProperty("pdfPage").GetInt32());
            Assert.Equal(32, row.GetProperty("printedPage").GetInt32());
            var target = Assert.Single(row.GetProperty("targets").EnumerateArray());
            Assert.Equal("Expressions", target.GetProperty("dataset").GetString());
            Assert.Equal(id, target.GetProperty("id").GetInt32());
            var word = Assert.Single(catalogs["Expressions"], word => word.Id == id);
            Assert.Equal(introductionOrigin, word.Origin);
            references.Add(("Expressions", id, introductionOrigin));
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
