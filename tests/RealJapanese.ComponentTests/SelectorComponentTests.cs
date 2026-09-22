using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using RealJapanese.Components.Pages.Adjectives;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Verbs;
using RealJapanese.Components.Pages.Words;

namespace RealJapanese.ComponentTests;

/// <summary>Exercises study-list search, exclusive category assignment, and independent kanji selector state.</summary>
public sealed class SelectorComponentTests
{
    [Fact]
    public void WordSearchMatchesEveryDisplayedFieldAndPreservesSelectionOnNoMatch()
    {
        using var test = new ComponentTestContext();
        var target = test.Words.Words.First(word => word.English.Length >= 5);
        var cut = test.Context.Render<WordsSelector>();
        cut.FindAll("input[type=search]")[0].Input($"  {target.English.ToUpperInvariant()}  ");
        Assert.Contains(VisibleKnownItems(cut), item => item.TextContent.Contains(target.English, StringComparison.Ordinal));

        cut.FindAll("input[type=search]")[0].Input(target.Japanese);
        Assert.Contains(VisibleKnownItems(cut), item => item.TextContent.Contains(target.Japanese, StringComparison.Ordinal));

        cut.FindAll("input[type=search]")[0].Input(target.Kana);
        Assert.Contains(VisibleKnownItems(cut), item => item.TextContent.Contains(target.Kana, StringComparison.Ordinal));

        FindItem(VisibleKnownItems(cut), target).Click();
        cut.FindAll("input[type=search]")[0].Input("__not_present_in_any_catalog_field__");

        Assert.Contains("No matches in this list.", cut.Markup);
        Assert.Contains(target.Id, test.Words.VocabWordIds);
    }

    [Fact]
    public void CategoryAssignmentMovesAnItemOnlyThroughTheVisibleListControls()
    {
        using var test = new ComponentTestContext();
        var target = test.Words.Words.First();
        var cut = test.Context.Render<WordsSelector>();

        FindItem(ItemsInColumn(cut, 0), target).Click();
        Assert.Contains(target.Id, test.Words.VocabWordIds);
        Assert.DoesNotContain(target.Id, test.Words.RehearsingWordIds);
        Assert.DoesNotContain(target.Id, test.Words.TrainingWordIds);
        Assert.DoesNotContain(ItemsInColumn(cut, 1), item => IsItem(item, target));

        FindItem(ItemsInColumn(cut, 0), target).Click();
        FindItem(ItemsInColumn(cut, 1), target).Click();
        Assert.DoesNotContain(target.Id, test.Words.VocabWordIds);
        Assert.Contains(target.Id, test.Words.RehearsingWordIds);

        FindItem(ItemsInColumn(cut, 1), target).Click();
        FindItem(ItemsInColumn(cut, 2), target).Click();
        Assert.DoesNotContain(target.Id, test.Words.VocabWordIds);
        Assert.DoesNotContain(target.Id, test.Words.RehearsingWordIds);
        Assert.Contains(target.Id, test.Words.TrainingWordIds);
    }

    [Fact]
    public void KanjiModeSwitchKeepsSingleAndCombinedSelectionsIndependent()
    {
        using var test = new ComponentTestContext();
        var combined = test.Kanji.Combined.Words.First();
        var single = test.Kanji.Single.Words.First();
        var cut = test.Context.Render<KanjiSelector>();

        FindItem(ItemsInColumn(cut, 0), combined).Click();
        cut.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Single").Click();
        FindItem(ItemsInColumn(cut, 0), single).Click();

        Assert.Contains(combined.Id, test.Kanji.Combined.VocabWordIds);
        Assert.Contains(single.Id, test.Kanji.Single.VocabWordIds);

        cut.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Combined").Click();
        Assert.Contains("1 selected", cut.FindAll(".card-header")[0].TextContent);
    }

    [Theory]
    [InlineData("verbs")]
    [InlineData("adjectives")]
    [InlineData("kanji-combined")]
    [InlineData("kanji-single")]
    public void CatalogSelectorSearchAndCategoryControlsPersistToTheMatchingRepository(string catalog)
    {
        using var test = new ComponentTestContext();
        var (cut, target) = RenderCatalogSelector(test, catalog);

        cut.FindAll("input[type=search]")[0].Input(target.Japanese);
        FindItem(ItemsInColumn(cut, 0), target).Click();
        AssertCategory(test, catalog, target.Id, "known");

        FindItem(ItemsInColumn(cut, 0), target).Click();
        cut.FindAll("input[type=search]")[2].Input(target.Kana);
        FindItem(ItemsInColumn(cut, 2), target).Click();
        AssertCategory(test, catalog, target.Id, "training");
    }

    private static IReadOnlyList<IElement> VisibleKnownItems(IRenderedComponent<WordsSelector> cut) =>
        ItemsInColumn(cut, 0);

    private static IReadOnlyList<IElement> ItemsInColumn<TComponent>(IRenderedComponent<TComponent> cut, int index)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(".category-column")[index].QuerySelectorAll("button.list-group-item").ToList();

    private static IReadOnlyList<IElement> ItemsInColumn(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, int index) =>
        cut.FindAll(".category-column")[index].QuerySelectorAll("button.list-group-item").ToList();

    private static (IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> Cut, Word Target) RenderCatalogSelector(ComponentTestContext test, string catalog)
    {
        return catalog switch
        {
            "verbs" => (test.Context.Render<VerbsSelector>(), test.Verbs.Words.First()),
            "adjectives" => (test.Context.Render<AdjectiveSelector>(), test.Adjectives.Words.First()),
            "kanji-combined" => (test.Context.Render<KanjiSelector>(), test.Kanji.Combined.Words.First()),
            "kanji-single" => RenderSingleKanjiSelector(test),
            _ => throw new ArgumentOutOfRangeException(nameof(catalog))
        };
    }

    private static (IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> Cut, Word Target) RenderSingleKanjiSelector(ComponentTestContext test)
    {
        var cut = test.Context.Render<KanjiSelector>();
        cut.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Single").Click();
        return (cut, test.Kanji.Single.Words.First());
    }

    private static void AssertCategory(ComponentTestContext test, string catalog, int id, string expected)
    {
        var (known, training) = catalog switch
        {
            "verbs" => (test.Verbs.VocabWordIds, test.Verbs.TrainingWordIds),
            "adjectives" => (test.Adjectives.VocabWordIds, test.Adjectives.TrainingWordIds),
            "kanji-combined" => (test.Kanji.Combined.VocabWordIds, test.Kanji.Combined.TrainingWordIds),
            "kanji-single" => (test.Kanji.Single.VocabWordIds, test.Kanji.Single.TrainingWordIds),
            _ => throw new ArgumentOutOfRangeException(nameof(catalog))
        };
        Assert.Equal(expected == "known", known.Contains(id));
        Assert.Equal(expected == "training", training.Contains(id));
    }

    private static IElement FindItem(IEnumerable<IElement> items, Word word) =>
        items.Single(item => IsItem(item, word));

    private static bool IsItem(IElement item, Word word) =>
        item.QuerySelectorAll("div").Select(element => element.TextContent.Trim())
            .SequenceEqual(new[] { word.English, word.Japanese, word.Kana });
}
