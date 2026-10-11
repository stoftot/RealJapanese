using System.Text.Json;
using System.Collections.Specialized;
using System.Web;
using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RealJapanese.Components.Pages.Vocabulary;
using RealJapanese.Components.Pages.Words;
using RealJapanese.TestSupport;

namespace RealJapanese.ComponentTests;

/// <summary>Protects the read-only saved-vocabulary mixer and unified editable vocabulary across colliding backend IDs.</summary>
public sealed class VocabularyCategoryComponentTests
{
    [Fact]
    public void MixerStartsWithAllSourcesAndOnlySavedWordsInTheirOwnCategories()
    {
        using var test = Fixture();
        var known = test.Adverbs.Words.Single(word => word.Id == 0);
        var training = test.Expressions.Words.Single(word => word.Id == 1);
        var rehearsing = test.Words.Words.Single(word => word.Id == 1002);
        test.Adverbs.AddToVocab(known);
        test.Expressions.AddToTraining(training);
        test.Words.AddToRehearsing(rehearsing);
        var persisted = File.ReadAllBytes(ProgressFile(test));
        var cut = test.Context.Render<WordsSelector>();

        Assert.Equal("Words", cut.Find("h1").TextContent.Trim());
        Assert.Equal(new[]
        {
            "Adjectives", "Adverbs", "Conjunctions", "Counters", "Determiners", "Expressions", "Nouns", "Particles",
            "Pronouns", "Suffixes", "Verbs", "Words"
        }, cut.FindAll("button[data-source]").Select(button => button.GetAttribute("data-source")).Order());
        Assert.All(cut.FindAll("button[data-source]"), button => Assert.Equal("true", button.GetAttribute("aria-pressed")));
        Assert.Equal(["Adverbs:0"], Keys(cut, 0));
        Assert.Equal(["Words:1002"], Keys(cut, 1));
        Assert.Equal(["Expressions:1"], Keys(cut, 2));
        Assert.Empty(cut.FindAll("button.list-group-item"));
        Assert.Empty(cut.FindAll("button[role=checkbox]"));

        cut.FindAll("input[type=search]")[0].Input(known.Kana);
        Assert.Equal(["Adverbs:0"], Keys(cut, 0));
        cut.FindAll("input[type=search]")[0].Input("__no_vocabulary_match__");
        Assert.Empty(Keys(cut, 0));
        cut.FindAll("input[type=search]")[0].Input("");
        cut.FindAll(".category-column")[0].QuerySelector(".word-group-expand")!.Click();
        Assert.Empty(Keys(cut, 0));
        cut.FindAll(".category-column")[0].QuerySelector(".word-group-expand")!.Click();
        Assert.Equal(["Adverbs:0"], Keys(cut, 0));
        Assert.Equal(persisted, File.ReadAllBytes(ProgressFile(test)));
    }

    [Fact]
    public void MixerCombinesSavedGrammarListsWithSupportingAndGeneralVocabulary()
    {
        using var test = Fixture();
        var noun = test.Nouns.Words.First();
        var verb = test.Verbs.Words.First();
        var adjective = test.Adjectives.Words.First();
        test.Words.AddToVocab(test.Words.Words.First());
        test.Adverbs.AddToVocab(test.Adverbs.Words.First());
        test.Nouns.AddToTraining(noun);
        test.Verbs.AddToRehearsing(verb);
        test.Adjectives.AddToVocab(adjective);

        var cut = test.Context.Render<WordsSelector>();

        Assert.Equal(new[] { $"Adjectives:{adjective.Id}", "Adverbs:0", "Words:1000" }.Order(), Keys(cut, 0));
        Assert.Equal([$"Verbs:{verb.Id}"], Keys(cut, 1));
        Assert.Equal([$"Nouns:{noun.Id}"], Keys(cut, 2));
    }

    [Fact]
    public void SourceToggleExcludesAndRestoresOnlyItsOwnSavedWordsWithoutMergingCollidingIds()
    {
        using var test = Fixture();
        test.Adverbs.AddToVocab(test.Adverbs.Words.Single(word => word.Id == 0));
        test.Expressions.AddToVocab(test.Expressions.Words.Single(word => word.Id == 0));
        var persisted = File.ReadAllBytes(ProgressFile(test));
        var cut = test.Context.Render<WordsSelector>();
        Assert.Equal(["Adverbs:0", "Expressions:0"], Keys(cut, 0));

        SourceButton(cut, "Adverbs").Click();
        Assert.Equal("false", SourceButton(cut, "Adverbs").GetAttribute("aria-pressed"));
        Assert.Equal(["Expressions:0"], Keys(cut, 0));
        SourceButton(cut, "Adverbs").Click();
        Assert.Equal(["Adverbs:0", "Expressions:0"], Keys(cut, 0));
        Assert.Equal(persisted, File.ReadAllBytes(ProgressFile(test)));
        Assert.Equal([0], test.Adverbs.VocabWordIds);
        Assert.Equal([0], test.Expressions.VocabWordIds);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("unknown-source")]
    public void EmptyOrUnknownSourceQueryShowsNoSavedWordsAndOffersEmptyPractice(string sources)
    {
        using var test = Fixture();
        test.Adverbs.AddToVocab(test.Adverbs.Words.First());
        Navigate(test, $"/words?sources={sources}");
        var cut = test.Context.Render<WordsSelector>();
        Assert.Empty(cut.FindAll("div.list-group-item[data-dataset]"));
        Assert.All(cut.FindAll("button[data-source]"), button => Assert.Equal("false", button.GetAttribute("aria-pressed")));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Start practice").Click();
        Assert.Equal("none", Query(test)["sources"]);
        var practice = test.Context.Render<WordSpelling>();
        Assert.Contains("Nothing to practise yet", practice.Markup);
        Assert.NotEmpty(practice.FindAll("input[type=range]"));
        Assert.Equal([0], test.Adverbs.VocabWordIds);
    }

    [Fact]
    public void SourceQueryIsRestoredAndPropagatedThroughExerciseAndBackLinks()
    {
        using var test = Fixture();
        test.Expressions.AddToTraining(test.Expressions.Words.Single(word => word.Id == 1));
        Navigate(test, "/words?category=training&sources=Expressions,Words,unknown-source");
        var cut = test.Context.Render<WordsSelector>();
        Assert.Equal(new[] { "Expressions", "Words" }, cut.FindAll("button[data-source][aria-pressed=true]")
            .Select(button => button.GetAttribute("data-source")).Order());
        Assert.Equal(["Expressions:1"], Keys(cut, 2));
        cut.FindAll("button").Last(button => button.TextContent.Trim() == "Start practice").Click();
        Assert.Equal("training", Query(test)["category"]);
        Assert.Equal(new[] { "Expressions", "Words" }, (Query(test)["sources"] ?? "").Split(',').Order());
        var flashcards = test.Context.Render<WordFlashCards>();
        var back = flashcards.FindAll("a").Single(link =>
            Uri.TryCreate(new Uri("http://localhost/"), link.GetAttribute("href"), out var uri) && uri.AbsolutePath == "/words");
        var backQuery = HttpUtility.ParseQueryString(new Uri(new Uri("http://localhost/"), back.GetAttribute("href")).Query);
        Assert.Equal("training", backQuery["category"]);
        Assert.Equal(new[] { "Expressions", "Words" }, (backQuery["sources"] ?? "").Split(',').Order());
        Assert.Equal(test.Expressions.Words.Single(word => word.Id == 1).Kana, flashcards.Find("p.flash-card-question").TextContent.Trim());
    }

    [Fact]
    public void UnifiedEditorAssignsCollidingEntriesAndGeneralWordsToOriginalRepositories()
    {
        using var test = Fixture();
        var cut = test.Context.Render<SupportingVocabularyStudy>();
        Assert.Equal("Words & expressions", cut.Find("h1").TextContent.Trim());
        FindRow(cut, 0, "Adverbs", 0).Click();
        FindRow(cut, 0, "Expressions", 0).Click();
        FindRow(cut, 0, "Words", 1000).Click();
        Assert.Equal([0], test.Adverbs.VocabWordIds);
        Assert.Equal([0], test.Expressions.VocabWordIds);
        Assert.Equal([1000], test.Words.VocabWordIds);
        Assert.Empty(test.Nouns.VocabWordIds);
        Assert.Empty(test.Verbs.VocabWordIds);
        Assert.Empty(test.Adjectives.VocabWordIds);

        FindRow(cut, 0, "Adverbs", 0).Click();
        FindRow(cut, 2, "Adverbs", 0).Click();
        Assert.Empty(test.Adverbs.VocabWordIds);
        Assert.Equal([0], test.Adverbs.TrainingWordIds);
        Assert.Equal([0], test.Expressions.VocabWordIds);
        Assert.Equal([1000], test.Words.VocabWordIds);
    }

    [Fact]
    public void UnifiedGroupSelectionPreservesBackendIdsAcrossSources()
    {
        using var test = Fixture();
        var cut = test.Context.Render<SupportingVocabularyStudy>();
        var column = cut.FindAll(".category-column")[2];
        column.QuerySelector("section.word-book[data-book='Mixer fixture'] .word-group-select")!.Click();
        Assert.Equal([0, 1, 2], test.Adverbs.TrainingWordIds.Order());
        Assert.Equal([0, 1, 2], test.Expressions.TrainingWordIds.Order());
        Assert.Empty(test.Words.TrainingWordIds);
        cut.FindAll(".category-column")[2]
            .QuerySelector("section.word-book[data-book='Mixer fixture'] .word-group-select")!.Click();
        Assert.Empty(test.Adverbs.TrainingWordIds);
        Assert.Empty(test.Expressions.TrainingWordIds);
    }

    [Theory]
    [InlineData("spelling", "known")]
    [InlineData("spelling", "rehearsing")]
    [InlineData("spelling", "training")]
    [InlineData("flashcards", "known")]
    [InlineData("flashcards", "rehearsing")]
    [InlineData("flashcards", "training")]
    public void MixedPracticeUsesOnlyRequestedSourcesAndSavedCategory(string exercise, string category)
    {
        using var test = Fixture();
        SeedCategories(test);
        Navigate(test, $"/words/{exercise}?category={category}&sources=Expressions");
        var cut = RenderPractice(test, exercise);
        var id = category switch { "known" => 0, "rehearsing" => 1, _ => 2 };
        var selected = test.Expressions.Words.Single(word => word.Id == id);
        Assert.Equal(exercise == "spelling" ? selected.English : selected.Kana,
            cut.Find(exercise == "spelling" ? "p.lead" : "p.flash-card-question").TextContent.Trim());
        Assert.Contains("1/1", cut.Markup);
        Assert.Equal([0], test.Adverbs.VocabWordIds);
        Assert.Equal([1], test.Adverbs.RehearsingWordIds);
        Assert.Equal([2], test.Adverbs.TrainingWordIds);
    }

    [Theory]
    [InlineData("spelling")]
    [InlineData("flashcards")]
    public void ChangingSourcesReloadsPracticeAndClearsRevealedAnswers(string exercise)
    {
        using var test = Fixture();
        SeedCategories(test);
        Navigate(test, $"/words/{exercise}?category=known&sources=Adverbs");
        var cut = RenderPractice(test, exercise);
        cut.FindAll("button").Single(button => button.TextContent.Trim() ==
            (exercise == "spelling" ? "Show answer (Enter)" : "Forgot")).Click();
        Navigate(test, $"/words/{exercise}?category=known&sources=Expressions");
        cut.WaitForAssertion(() =>
        {
            var target = test.Expressions.Words.Single(word => word.Id == 0);
            Assert.Equal(exercise == "spelling" ? target.English : target.Kana,
                cut.Find(exercise == "spelling" ? "p.lead" : "p.flash-card-question").TextContent.Trim());
            Assert.DoesNotContain("Answers:", cut.Markup);
            Assert.Single(cut.FindAll("button").Where(button => button.TextContent.Trim() ==
                (exercise == "spelling" ? "Show answer (Enter)" : "Show answer")));
            Assert.DoesNotContain(test.Adverbs.Words.Single(word => word.Id == 0).English, cut.Markup);
        });
        if (exercise == "flashcards")
            cut.FindAll("button").Single(button => button.TextContent.Trim() == "Show answer").Click();
        cut.FindAll("button").Single(button => button.TextContent.Trim() ==
            (exercise == "spelling" ? "Next" : "Next question")).Click();
        var nextRoundTarget = test.Expressions.Words.Single(word => word.Id == 0);
        Assert.Equal(exercise == "spelling" ? nextRoundTarget.English : nextRoundTarget.Kana,
            cut.Find(exercise == "spelling" ? "p.lead" : "p.flash-card-question").TextContent.Trim());
        Navigate(test, $"/words/{exercise}?category=known&sources=none");
        cut.WaitForAssertion(() => Assert.Contains("Nothing to practise yet", cut.Markup));
    }

    [Theory]
    [InlineData("adverbs")]
    [InlineData("expressions")]
    [InlineData("pronouns")]
    [InlineData("determiners")]
    [InlineData("counters")]
    [InlineData("conjunctions")]
    [InlineData("particles")]
    [InlineData("suffixes")]
    public void LegacyCategoryPagesUseTheUnifiedEditor(string category)
    {
        using var test = Fixture();
        var cut = category switch
        {
            "adverbs" => (IRenderedComponent<IComponent>)test.Context.Render<AdverbsStudy>(),
            "expressions" => test.Context.Render<ExpressionsStudy>(),
            "pronouns" => test.Context.Render<PronounsStudy>(),
            "determiners" => test.Context.Render<DeterminersStudy>(),
            "counters" => test.Context.Render<CountersStudy>(),
            "conjunctions" => test.Context.Render<ConjunctionsStudy>(),
            "particles" => test.Context.Render<ParticlesStudy>(),
            "suffixes" => test.Context.Render<SuffixesStudy>(),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
        Assert.Equal("Words & expressions", cut.Find("h1").TextContent.Trim());
        Assert.NotNull(FindRow(cut, 0, "Adverbs", 0));
        Assert.NotNull(FindRow(cut, 0, "Expressions", 0));
        Assert.NotNull(FindRow(cut, 0, "Words", 1000));
    }

    private static ComponentTestContext Fixture() => new(ConfigureCatalogs);

    private static void ConfigureCatalogs(TestWorkspace workspace)
    {
        foreach (var (dataset, label, japanese) in new[] { ("Adverbs", "adverb", "副詞"), ("Expressions", "expression", "表現") })
            File.WriteAllText(Path.Combine(workspace.CatalogRoot, dataset, dataset + ".json"),
                JsonSerializer.Serialize(Enumerable.Range(0, 3).Select(id => new Word
                {
                    Id = id, Japanese = japanese + id, Kana = $"{label}{id}", English = $"{label} {id}", Origin = "Mixer fixture-1"
                })));
    }

    private static void SeedCategories(ComponentTestContext test)
    {
        foreach (var word in test.Adverbs.Words)
            test.Adverbs.SetCategory([word], (WordPracticeCategory)(word.Id == 1 ? 1 : word.Id == 2 ? 2 : 0));
        foreach (var word in test.Expressions.Words)
            test.Expressions.SetCategory([word], (WordPracticeCategory)(word.Id == 1 ? 1 : word.Id == 2 ? 2 : 0));
    }

    private static IRenderedComponent<IComponent> RenderPractice(ComponentTestContext test, string exercise) =>
        exercise == "spelling" ? test.Context.Render<WordSpelling>() : test.Context.Render<WordFlashCards>();

    private static string[] Keys<TComponent>(IRenderedComponent<TComponent> cut, int column) where TComponent : IComponent =>
        cut.FindAll(".category-column")[column].QuerySelectorAll(".list-group-item[data-dataset]")
            .Select(row => row.GetAttribute("data-dataset") + ":" + row.GetAttribute("data-source-id")).Distinct().Order().ToArray();

    private static IElement FindRow<TComponent>(IRenderedComponent<TComponent> cut, int column, string dataset, int id)
        where TComponent : IComponent => cut.FindAll(".category-column")[column]
            .QuerySelector($"button.list-group-item[data-dataset='{dataset}'][data-source-id='{id}']")!;

    private static IElement SourceButton<TComponent>(IRenderedComponent<TComponent> cut, string dataset)
        where TComponent : IComponent => cut.Find($"button[data-source='{dataset}']");

    private static void Navigate(ComponentTestContext test, string uri) =>
        test.Context.Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    private static NameValueCollection Query(ComponentTestContext test) =>
        HttpUtility.ParseQueryString(new Uri(test.Context.Services.GetRequiredService<NavigationManager>().Uri).Query);

    private static string ProgressFile(ComponentTestContext test) => Path.Combine(test.Paths.ProgressRoot, "Progress.json");
}
