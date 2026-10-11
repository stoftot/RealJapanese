using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Pages.Adjectives;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Nouns;
using RealJapanese.Components.Pages.Verbs;
using RealJapanese.Components.Pages.Vocabulary;
using RealJapanese.Components.Pages.Words;
using RealJapanese.Components.Shared;
using RealJapanese.TestSupport;

namespace RealJapanese.ComponentTests;

/// <summary>Protects bulk category moves across editable selectors and keeps the saved-word mixer read-only.</summary>
public sealed class BulkCategoryMoveComponentTests
{
    [Theory]
    [InlineData(0, 1, "Move selected to Rehearsing")]
    [InlineData(1, 0, "Move selected to Known")]
    [InlineData(1, 2, "Move selected to Training")]
    [InlineData(2, 1, "Move selected to Rehearsing")]
    public void MoveButtonMovesEverySavedSourceIdAndPreservesOtherMembership(int source, int destination, string label)
    {
        using var test = WordFixture();
        var words = test.Words.Words.OrderBy(word => word.Id).Take(4).ToArray();
        var moved = words[..2];
        var destinationMember = words[2];
        var unrelated = words[3];
        var unrelatedCategory = 3 - source - destination;
        SetCategory(test.Words, moved, source);
        SetCategory(test.Words, [destinationMember], destination);
        SetCategory(test.Words, [unrelated], unrelatedCategory);
        var cut = test.Context.Render<SupportingVocabularyStudy>();

        // One selected source item is visible and one is hidden by search. Both still belong to the saved source set.
        IElement SourceColumn() => cut.FindAll(".category-column")[source];
        SourceColumn().QuerySelector("input[type=search]")!.Input(moved[0].English);
        Assert.Single(SourceColumn().QuerySelectorAll("button.list-group-item"));
        SourceColumn().QuerySelector("input[type=search]")!.Input("");
        var hiddenLesson = SourceColumn().QuerySelector(".word-book[data-book='Bulk fixture'] section.word-lesson[data-lesson='Lesson 2']")!;
        hiddenLesson.QuerySelector("button.word-group-expand")!.Click();
        Assert.DoesNotContain(SourceColumn().QuerySelectorAll("button.list-group-item"), item =>
            item.TextContent.Contains(moved[1].English, StringComparison.Ordinal));
        Assert.Contains(SourceColumn().QuerySelectorAll("button.list-group-item"), item =>
            item.TextContent.Contains(moved[0].English, StringComparison.Ordinal));
        var button = MoveButton(SourceColumn(), label);
        Assert.NotNull(SourceColumn().QuerySelector(".word-move-actions"));
        button.Click();

        Assert.Equal(moved.Select(word => word.Id).Append(destinationMember.Id).Order(), CategoryIds(test.Words, destination).Order());
        Assert.DoesNotContain(moved[0].Id, CategoryIds(test.Words, source));
        Assert.DoesNotContain(moved[1].Id, CategoryIds(test.Words, source));
        Assert.Contains(unrelated.Id, CategoryIds(test.Words, unrelatedCategory));
        AssertPersisted(test, "Words", destination, destinationMember.Id, moved.Select(word => word.Id));
        AssertPersisted(test, "Words", source, null, []);
        var destinationColumn = cut.FindAll(".category-column")[destination];
        var selectedRows = destinationColumn.QuerySelectorAll("button.list-group-item.active");
        Assert.Equal(3, selectedRows.Length);
        foreach (var word in moved.Append(destinationMember))
        {
            var row = selectedRows.Single(item => item.TextContent.Contains(word.English, StringComparison.Ordinal));
            Assert.Equal("true", row.GetAttribute("aria-pressed"));
        }
    }

    [Theory]
    [InlineData(0, 1, "Move selected to Rehearsing")]
    [InlineData(1, 0, "Move selected to Known")]
    [InlineData(1, 2, "Move selected to Training")]
    [InlineData(2, 1, "Move selected to Rehearsing")]
    public void WordComponentBaseSelectorPersistsEachMoveDirection(int source, int destination, string label)
    {
        using var test = new ComponentTestContext();
        var words = test.Verbs.Words.Take(3).ToArray();
        var moved = words[..2];
        var existing = words[2];
        SetCategory(test.Verbs, moved, source);
        SetCategory(test.Verbs, [existing], destination);
        var cut = test.Context.Render<VerbsSelector>();

        MoveButton(cut.FindAll(".category-column")[source], label).Click();

        Assert.Equal(moved.Select(word => word.Id).Append(existing.Id).Order(), CategoryIds(test.Verbs, destination).Order());
        Assert.Empty(CategoryIds(test.Verbs, source));
        AssertPersisted(test, "Verbs", destination, existing.Id, moved.Select(word => word.Id));
        AssertPersisted(test, "Verbs", source, null, []);
    }

    [Theory]
    [InlineData("nouns")]
    [InlineData("verbs")]
    [InlineData("adjectives")]
    [InlineData("kanji-combined")]
    [InlineData("kanji-single")]
    [InlineData("vocabulary")]
    [InlineData("vocabulary-selector")]
    public void EditableSelectorsExposeEmptyDisabledMoveActions(string selector)
    {
        using var test = new ComponentTestContext();
        var cut = RenderSelector(test, selector);

        Assert.Equal(3, cut.FindAll(".category-column").Count);
        foreach (var (index, label) in new[] { (0, "Move selected to Rehearsing"), (1, "Move selected to Known"), (1, "Move selected to Training"), (2, "Move selected to Rehearsing") })
        {
            var column = cut.FindAll(".category-column")[index];
            var button = MoveButton(column, label);
            Assert.True(button.HasAttribute("disabled"));
            var actions = column.QuerySelector(".word-move-actions");
            Assert.NotNull(actions);
            Assert.Contains(button, actions.QuerySelectorAll("button"));
        }
    }

    [Fact]
    public void CombinedVocabularyMoveKeepsCollidingIdsInTheirOriginalDatasets()
    {
        using var test = new ComponentTestContext(ConfigureCombinedCatalogs);
        var adverb = test.Adverbs.Words.Single(word => word.Id == 0);
        var expression = test.Expressions.Words.Single(word => word.Id == 0);
        var existing = test.Adverbs.Words.Single(word => word.Id == 1);
        test.Adverbs.AddToVocab(adverb);
        test.Expressions.AddToVocab(expression);
        test.Adverbs.AddToRehearsing(existing);
        var cut = test.Context.Render<SupportingVocabularyStudy>();

        var known = cut.FindAll(".category-column")[0];
        var move = MoveButton(known, "Move selected to Rehearsing");
        Assert.False(move.HasAttribute("disabled"));
        move.Click();

        Assert.Equal([0, 1], test.Adverbs.RehearsingWordIds.Order());
        Assert.Equal([0], test.Expressions.RehearsingWordIds);
        Assert.Empty(test.Adverbs.VocabWordIds);
        Assert.Empty(test.Expressions.VocabWordIds);
        AssertPersisted(test, "Adverbs", 1, 1, [0]);
        AssertPersisted(test, "Expressions", 1, null, [0]);
        Assert.Empty(cut.FindAll(".category-column")[0].QuerySelectorAll("button.list-group-item.active"));
    }

    [Fact]
    public void ReadOnlyWordsMixerNeverRendersMoveActions()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<WordsSelector>();

        Assert.Empty(cut.FindAll(".word-move-actions"));
        Assert.DoesNotContain(cut.FindAll("button"), button => AccessibleLabel(button).StartsWith("Move selected to ", StringComparison.Ordinal));
    }

    private static ComponentTestContext WordFixture() => new(workspace =>
    {
        File.WriteAllText(Path.Combine(workspace.CatalogRoot, "Words", "Words.json"), JsonSerializer.Serialize(
            Enumerable.Range(0, 4).Select(id => new Word
            {
                Id = id, Japanese = $"語{id}", Kana = $"ご{id}", English = $"term {id}", Origin = $"Bulk fixture-{id + 1}"
            })));
    });

    private static void ConfigureCombinedCatalogs(TestWorkspace workspace)
    {
        foreach (var (dataset, label, japanese) in new[] { ("Adverbs", "adverb", "副詞"), ("Expressions", "expression", "表現") })
            File.WriteAllText(Path.Combine(workspace.CatalogRoot, dataset, dataset + ".json"), JsonSerializer.Serialize(
                Enumerable.Range(0, 2).Select(id => new Word
                {
                    Id = id, Japanese = japanese + id, Kana = $"{label}{id}", English = $"{label} {id}", Origin = $"Bulk fixture-{id + 1}"
                })));
    }

    private static IRenderedComponent<IComponent> RenderSelector(ComponentTestContext test, string selector) => selector switch
    {
        "nouns" => test.Context.Render<NounsSelector>(),
        "verbs" => test.Context.Render<VerbsSelector>(),
        "adjectives" => test.Context.Render<AdjectiveSelector>(),
        "kanji-combined" => test.Context.Render<KanjiSelector>(),
        "kanji-single" => RenderSingleKanji(test),
        "vocabulary" => test.Context.Render<SupportingVocabularyStudy>(),
        "vocabulary-selector" => test.Context.Render<VocabularySelector<Verb>>(parameters => parameters
            .Add(component => component.Repository, test.Verbs)
            .Add(component => component.Title, "Verbs")
            .Add(component => component.RoutePrefix, "verbs")),
        _ => throw new ArgumentOutOfRangeException(nameof(selector))
    };

    private static IRenderedComponent<IComponent> RenderSingleKanji(ComponentTestContext test)
    {
        var cut = test.Context.Render<KanjiSelector>();
        cut.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Single").Click();
        return cut;
    }

    private static IElement MoveButton(IElement column, string label) =>
        column.QuerySelectorAll(".word-move-actions button").Single(button => AccessibleLabel(button) == label);

    private static string AccessibleLabel(IElement button) =>
        string.Join(" ", button.QuerySelectorAll("span:not([aria-hidden='true'])").Select(span => span.TextContent.Trim())).Trim();

    private static void SetCategory<T>(Repositories.Bases.WordDataBase<T> data, IEnumerable<T> words, int category) where T : Word =>
        data.SetCategory(words, (WordPracticeCategory)category);

    private static IEnumerable<int> CategoryIds<T>(Repositories.Bases.WordDataBase<T> data, int category) where T : Word =>
        data.GetWords((WordPracticeCategory)category).Select(word => word.Id);

    private static void AssertPersisted(ComponentTestContext test, string dataset, int category, int? preserveId, IEnumerable<int> moved)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(test.Paths.ProgressRoot, "Progress.json")));
        var categories = document.RootElement.GetProperty("Data").GetProperty(dataset);
        var name = category switch { 0 => "KnownIds", 1 => "RehearsingIds", _ => "TrainingIds" };
        var actual = categories.GetProperty(name).EnumerateArray().Select(value => value.GetInt32()).Order().ToArray();
        var expected = (preserveId.HasValue ? moved.Append(preserveId.Value) : moved).Order().ToArray();
        Assert.Equal(expected, actual);
    }
}
