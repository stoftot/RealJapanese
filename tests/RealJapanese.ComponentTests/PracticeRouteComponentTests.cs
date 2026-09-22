using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Pages.Adjectives;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Verbs;
using RealJapanese.Components.Pages.Words;
using Repositories.Bases;

namespace RealJapanese.ComponentTests;

/// <summary>Checks every offered selector route and verifies category-bound destinations use only the requested list.</summary>
public sealed class PracticeRouteComponentTests
{
    public static IEnumerable<object[]> OfferedRoutes()
    {
        var categories = new[] { "known", "rehearsing", "training" };
        var actions = new[]
        {
            ("words", 0, "/words/spelling?category={0}"),
            ("words", 1, "/words/flashcards?category={0}"),
            ("verbs", 0, "/verbs/spelling?category={0}"),
            ("verbs", 1, "/verbs/categories?category={0}"),
            ("verbs", 2, "/verbs/ConjugationsAndForms"),
            ("adjectives", 0, "/adjectives/spelling?category={0}"),
            ("adjectives", 1, "/adjectives/categories?category={0}"),
            ("adjectives", 2, "/adjectives/conjugateBase"),
            ("kanji-combined", 0, "/kanji/combined/meaning?category={0}"),
            ("kanji-single", 0, "/kanji/single/meaning?category={0}")
        };

        foreach (var (selector, actionIndex, route) in actions)
        foreach (var category in categories)
            yield return new object[] { selector, actionIndex, category, string.Format(route, category) };
    }

    public static IEnumerable<object[]> CategoryDestinations()
    {
        var destinations = new[]
        {
            "word-spelling", "word-flashcards", "verb-spelling", "verb-categories",
            "adjective-spelling", "adjective-categories", "kanji-single", "kanji-combined"
        };
        foreach (var destination in destinations)
        foreach (var category in new[] { "known", "rehearsing", "training" })
            yield return new object[] { destination, category };
    }

    public static IEnumerable<object[]> EmptyDestinations() =>
        CategoryDestinations().Where(row => (string)row[1] == "known");

    [Theory]
    [MemberData(nameof(OfferedRoutes))]
    public void OfferedActionCarriesTheSelectedCategoryToItsExactRoute(
        string selector, int actionIndex, string category, string expectedRoute)
    {
        using var test = new ComponentTestContext();
        var cut = RenderSelector(test, selector);
        cut.FindAll("button.toggle-slider-option")
            .Single(button => button.TextContent.Trim().Equals(ToLabel(category), StringComparison.Ordinal))
            .Click();

        cut.FindAll("button").Where(button => button.TextContent.Trim() == "Start practice").ElementAt(actionIndex).Click();

        var navigation = test.Context.Services.GetService<NavigationManager>()!;
        Assert.EndsWith(expectedRoute, navigation.Uri, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CategoryDestinations))]
    public void CategoryDestinationDisplaysOnlyTheQuestionAssignedToItsQuery(string destination, string category)
    {
        using var test = new ComponentTestContext();
        var expectedQuestion = SeedDisjointCategories(test, destination, category);
        test.UseCategory(category);

        var cut = RenderDestination(test, destination);

        var questionSelector = destination == "word-flashcards" ? "p.flash-card-question" : "p.lead";
        Assert.Equal(expectedQuestion, cut.Find(questionSelector).TextContent.Trim());
        Assert.NotEmpty(cut.FindAll("input[type=range]"));
    }

    [Theory]
    [MemberData(nameof(EmptyDestinations))]
    public void EmptyCategoryDestinationStillRendersInteractivePracticeControls(string destination, string category)
    {
        using var test = new ComponentTestContext();
        test.UseCategory(category);

        var cut = RenderDestination(test, destination);

        Assert.Contains("Nothing to practise yet", cut.Markup);
        Assert.NotEmpty(cut.FindAll("input[type=range]"));
    }

    private static IRenderedComponent<IComponent> RenderSelector(ComponentTestContext test, string selector)
    {
        if (selector == "kanji-single")
        {
            var kanji = test.Context.Render<KanjiSelector>();
            kanji.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Single").Click();
            return kanji;
        }

        return selector switch
        {
            "words" => test.Context.Render<WordsSelector>(),
            "verbs" => test.Context.Render<VerbsSelector>(),
            "adjectives" => test.Context.Render<AdjectiveSelector>(),
            "kanji-combined" => test.Context.Render<KanjiSelector>(),
            _ => throw new ArgumentOutOfRangeException(nameof(selector))
        };
    }

    private static IRenderedComponent<IComponent> RenderDestination(ComponentTestContext test, string destination) => destination switch
    {
        "word-spelling" => test.Context.Render<WordSpelling>(),
        "word-flashcards" => test.Context.Render<WordFlashCards>(),
        "verb-spelling" => test.Context.Render<VerbSpelling>(),
        "verb-categories" => test.Context.Render<CategoriesVerbs>(),
        "adjective-spelling" => test.Context.Render<AdjectiveSpelling>(),
        "adjective-categories" => test.Context.Render<CategoriesAdjectives>(),
        "kanji-single" => test.Context.Render<KanjiSingleMeaning>(),
        "kanji-combined" => test.Context.Render<KanjiCombinedMeaning>(),
        _ => throw new ArgumentOutOfRangeException(nameof(destination))
    };

    private static string SeedDisjointCategories(ComponentTestContext test, string destination, string category)
    {
        if (destination.StartsWith("word-", StringComparison.Ordinal))
        {
            var selected = Seed(test.Words, category, word => destination == "word-flashcards" ? word.Kana : word.English);
            return destination == "word-flashcards" ? selected.Kana : selected.English;
        }
        if (destination.StartsWith("verb-", StringComparison.Ordinal))
        {
            var selected = Seed(test.Verbs, category, verb => destination == "verb-categories" ? verb.Kana : verb.English);
            return destination == "verb-categories" ? selected.Kana : selected.English;
        }
        if (destination.StartsWith("adjective-", StringComparison.Ordinal))
        {
            var selected = Seed(test.Adjectives, category, adjective => destination == "adjective-categories" ? adjective.Kana : adjective.English);
            return destination == "adjective-categories" ? selected.Kana : selected.English;
        }

        WordDataBase<Word> data = destination == "kanji-single" ? test.Kanji.Single : test.Kanji.Combined;
        return Seed(data, category, word => word.Japanese).Japanese;
    }

    private static T Seed<T>(WordDataBase<T> data, string requestedCategory, Func<T, string> question)
        where T : Word
    {
        var selected = data.Words.GroupBy(question).Select(group => group.First()).Take(3).ToArray();
        Assert.Equal(3, selected.Length);
        data.AddToVocab(selected[0]);
        data.AddToRehearsing(selected[1]);
        data.AddToTraining(selected[2]);
        return requestedCategory switch
        {
            "rehearsing" => selected[1],
            "training" => selected[2],
            _ => selected[0]
        };
    }

    private static string ToLabel(string category) => char.ToUpperInvariant(category[0]) + category[1..];
}
