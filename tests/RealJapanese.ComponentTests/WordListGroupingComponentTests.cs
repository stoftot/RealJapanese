using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;

namespace RealJapanese.ComponentTests;

/// <summary>Protects book/lesson hierarchy, independent expansion, bulk scope and accessible selection state.</summary>
public sealed class WordListGroupingComponentTests
{
    [Fact]
    public void IntroductorySectionsSortBeforeLessonsAndKeepIndependentBulkSelection()
    {
        using var context = new BunitContext();
        var words = new List<Word>
        {
            Word(1, "Book-A-1"), Word(2, "Book-A-Introduction: Greetings & introductions"),
            Word(3, "Book-A-Introduction: Greetings & introductions") with { AdditionalOrigins = ["Book-A-2"] },
            Word(4, "Other book-Introduction: Greetings & introductions"), Word(5, "self added")
        };
        List<int> selected = [2];
        List<List<int>> batches = [];
        var cut = Render(context, words, selected, batches, []);
        var book = Book(cut, "Book-A");
        Assert.Equal(new[] { "Greetings & introductions", "Lesson 1", "Lesson 2" },
            book.QuerySelectorAll(".word-lesson").Select(section => section.GetAttribute("data-lesson")));

        IElement Introduction() => Lesson(Book(cut, "Book-A"), "Greetings & introductions");
        Expand(Introduction()).Click();
        Assert.Equal([2], selected);
        Select(Introduction()).Click();
        Assert.Equal(new[] { 2, 3 }, selected.Order());
        Assert.Equal([3], Assert.Single(batches));
        Assert.Equal("false", Expand(Introduction()).GetAttribute("aria-expanded"));
        Assert.Equal("true", Select(Lesson(Book(cut, "Book-A"), "Lesson 2")).GetAttribute("aria-checked"));
        Assert.Equal("false", Select(Lesson(Book(cut, "Book-A"), "Lesson 1")).GetAttribute("aria-checked"));
        Select(Book(cut, "Book-A")).Click();
        Assert.Equal(new[] { 1, 2, 3 }, selected.Order());
    }

    [Fact]
    public void BooksKeepSameNumberedLessonsSeparateAndSortLessonsNumerically()
    {
        using var context = new BunitContext();
        var words = new List<Word>
        {
            Word(1, "Book-A-10"), Word(2, "Book-A-2"), Word(3, "Book-A-1"), Word(4, "Book B-1")
        };
        var cut = Render(context, words, []);

        Assert.Equal(new[] { "Book B", "Book-A" }, cut.FindAll(".word-book").Select(book => book.GetAttribute("data-book")));
        Assert.Equal(new[] { "Lesson 1", "Lesson 2", "Lesson 10" }, Book(cut, "Book-A").QuerySelectorAll(".word-lesson")
            .Select(lesson => lesson.GetAttribute("data-lesson")));
        Assert.Contains("word 3", Lesson(Book(cut, "Book-A"), "Lesson 1").TextContent);
        Assert.Contains("word 4", Lesson(Book(cut, "Book B"), "Lesson 1").TextContent);
    }

    [Fact]
    public void MissingBlankAndSelfAddedOriginsUseGeneralAndUnnumberedOriginsStayInTheirBook()
    {
        using var context = new BunitContext();
        var words = new List<Word>
        {
            new() { Id = 1, English = "default", Japanese = "日本語", Kana = "かな" },
            Word(2, " "), Word(3, "SELF ADDED"), Word(4, "Book without a lesson"), Word(5, "Book-appendix")
        };
        var cut = Render(context, words, []);

        Assert.Equal(3, Book(cut, "General").QuerySelectorAll("button.list-group-item").Length);
        Assert.Empty(Book(cut, "General").QuerySelectorAll(".word-lesson"));
        Assert.Single(Book(cut, "Book without a lesson").QuerySelectorAll(".word-lesson[data-lesson='Other words']"));
        Assert.Single(Book(cut, "Book-appendix").QuerySelectorAll(".word-lesson[data-lesson='Other words']"));
    }

    [Fact]
    public void BookBulkSelectionIncludesCollapsedLessonsAndEmitsSharedWordsOnce()
    {
        using var context = new BunitContext();
        var shared = Word(1, "Book-1") with { AdditionalOrigins = ["Book-2", "Book-1"] };
        var words = new List<Word> { shared, Word(2, "Book-2"), Word(3, "Other book-1") };
        List<int> selected = [];
        List<List<int>> selectedBatches = [];
        List<List<int>> deselectedBatches = [];
        var cut = Render(context, words, selected, selectedBatches, deselectedBatches);

        Assert.Equal(2, Book(cut, "Book").QuerySelectorAll("button.list-group-item").Count(item => item.TextContent.Contains("word 1")));
        Expand(Book(cut, "Book")).Click();
        Select(Book(cut, "Book")).Click();

        Assert.Equal(new[] { 1, 2 }, selected.Order());
        Assert.Equal(new[] { 1, 2 }, Assert.Single(selectedBatches).Order());
        Assert.Equal("true", Select(Book(cut, "Book")).GetAttribute("aria-checked"));
        Assert.Equal("false", Expand(Book(cut, "Book")).GetAttribute("aria-expanded"));

        Select(Book(cut, "Book")).Click();
        Assert.Empty(selected);
        Assert.Equal(new[] { 1, 2 }, Assert.Single(deselectedBatches).Order());
    }

    [Fact]
    public void LessonSelectionUsesOnlyItsWordsAndMixedBookSelectionAddsOnlyMissingWords()
    {
        using var context = new BunitContext();
        var words = new List<Word> { Word(1, "Book-1"), Word(2, "Book-1"), Word(3, "Book-2"), Word(4, "Other-1") };
        List<int> selected = [];
        List<List<int>> batches = [];
        var cut = Render(context, words, selected, batches, []);

        Select(Lesson(Book(cut, "Book"), "Lesson 1")).Click();
        Assert.Equal(new[] { 1, 2 }, selected.Order());
        Assert.Equal("true", Select(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-checked"));
        Assert.Equal("false", Select(Lesson(Book(cut, "Book"), "Lesson 2")).GetAttribute("aria-checked"));
        Assert.Equal("mixed", Select(Book(cut, "Book")).GetAttribute("aria-checked"));

        Select(Book(cut, "Book")).Click();
        Assert.Equal(new[] { 3 }, batches[1]);
        Assert.Equal(new[] { 1, 2, 3 }, selected.Order());
        Assert.DoesNotContain(4, selected);
    }

    [Fact]
    public void SharedWordSelectionUpdatesEveryLessonAndWholeBookWithoutDuplicateIds()
    {
        using var context = new BunitContext();
        var words = new List<Word>
        {
            Word(1, "Book-1") with { AdditionalOrigins = ["Book-2"] }, Word(2, "Book-2")
        };
        List<int> selected = [];
        var cut = Render(context, words, selected);

        Lesson(Book(cut, "Book"), "Lesson 1").QuerySelector("button.list-group-item")!.Click();
        Assert.Equal(new[] { 1 }, selected);
        Assert.Equal("true", Select(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-checked"));
        Assert.Equal("mixed", Select(Lesson(Book(cut, "Book"), "Lesson 2")).GetAttribute("aria-checked"));
        Assert.Equal("mixed", Select(Book(cut, "Book")).GetAttribute("aria-checked"));

        Select(Book(cut, "Book")).Click();
        Assert.Equal(new[] { 1, 2 }, selected.Order());
    }

    [Fact]
    public void ExpansionDoesNotSelectAndPersistsAcrossSelectionAndParentRenders()
    {
        using var context = new BunitContext();
        var words = new List<Word> { Word(1, "Book-1"), Word(2, "Book-2") };
        List<int> selected = [];
        var cut = Render(context, words, selected);
        var panelId = Expand(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-controls");

        Expand(Lesson(Book(cut, "Book"), "Lesson 1")).Click();
        Assert.Empty(selected);
        Select(Lesson(Book(cut, "Book"), "Lesson 2")).Click();
        cut.Render(parameters => parameters.Add(component => component.AllWords, words.ToList()));

        Assert.Equal(new[] { 2 }, selected);
        Assert.Equal("false", Expand(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-expanded"));
        Assert.Equal(panelId, Expand(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-controls"));
        Assert.Empty(Lesson(Book(cut, "Book"), "Lesson 1").QuerySelectorAll("button.list-group-item"));

        Expand(Book(cut, "Book")).Click();
        Expand(Book(cut, "Book")).Click();
        Assert.Equal("false", Expand(Lesson(Book(cut, "Book"), "Lesson 1")).GetAttribute("aria-expanded"));
        Assert.Equal(new[] { 2 }, selected);
    }

    [Fact]
    public void SearchPreservesHierarchyAndBulkActionsAffectOnlyMatches()
    {
        using var context = new BunitContext();
        var words = new List<Word>
        {
            Word(1, "Book-1") with { English = "cat" }, Word(2, "Book-1") with { English = "dog" },
            Word(3, "Book-2") with { English = "catfish" }, Word(4, "Other-1") with { English = "bird" }
        };
        List<int> selected = [2];
        List<List<int>> batches = [];
        var cut = Render(context, words, selected, batches, []);

        cut.Find("input[type=search]").Input("  CAT  ");
        Assert.Single(cut.FindAll(".word-book"));
        Assert.Equal(2, Book(cut, "Book").QuerySelectorAll(".word-lesson").Length);
        Assert.Contains("2 matches", cut.Find(".word-selection-scope").TextContent);
        Assert.Contains("matching words", cut.Find(".word-selection-scope").TextContent);
        Assert.Contains("all 2 matching words", Select(Book(cut, "Book")).GetAttribute("aria-label"));
        Select(Book(cut, "Book")).Click();
        Assert.Equal(new[] { 1, 3 }, Assert.Single(batches).Order());
        Assert.Equal(new[] { 1, 2, 3 }, selected.Order());

        Select(Book(cut, "Book")).Click();
        Assert.Equal(new[] { 2 }, selected);
        cut.Find("input[type=search]").Input("__missing__");
        Assert.Contains("No matches in this list.", cut.Markup);
        Assert.Equal(new[] { 2 }, selected);
    }

    [Fact]
    public void SearchOpensMatchingGroupsAndClearingItRestoresPreviousExpansion()
    {
        using var context = new BunitContext();
        var words = new List<Word> { Word(1, "Book-1") };
        var cut = Render(context, words, []);
        Expand(Book(cut, "Book")).Click();

        cut.Find("input[type=search]").Input("word 1");
        Assert.Equal("true", Expand(Book(cut, "Book")).GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("button.list-group-item"));
        cut.Find("input[type=search]").Input("");
        Assert.Equal("false", Expand(Book(cut, "Book")).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void LegacyItemCallbacksSupportGroupSelectionAndDeselection()
    {
        using var context = new BunitContext();
        var words = new List<Word> { Word(1, "self added"), Word(2, "") };
        List<int> selected = [];
        var cut = Render(context, words, selected);

        Select(Book(cut, "General")).Click();
        Assert.Equal(new[] { 1, 2 }, selected.Order());
        Assert.All(cut.FindAll("button.list-group-item"), item => Assert.Equal("true", item.GetAttribute("aria-pressed")));
        Select(Book(cut, "General")).Click();
        Assert.Empty(selected);
    }

    private static IRenderedComponent<WordList<Word>> Render(BunitContext context, List<Word> words, List<int> selected,
        List<List<int>>? selectedBatches = null, List<List<int>>? deselectedBatches = null) =>
        context.Render<WordList<Word>>(parameters =>
        {
            parameters.Add(component => component.Title, "Known")
                .Add(component => component.AllWords, words)
                .Add(component => component.SelectedIds, selected)
                .Add(component => component.WordSelected, EventCallback.Factory.Create<Word>(context, word => selected.Add(word.Id)))
                .Add(component => component.WordDeselected, EventCallback.Factory.Create<Word>(context, word => { selected.Remove(word.Id); }));
            if (selectedBatches is not null)
                parameters.Add(component => component.WordsSelected, EventCallback.Factory.Create<List<Word>>(context, batch =>
                {
                    selectedBatches.Add(batch.Select(word => word.Id).ToList());
                    selected.AddRange(batch.Select(word => word.Id));
                }));
            if (deselectedBatches is not null)
                parameters.Add(component => component.WordsDeselected, EventCallback.Factory.Create<List<Word>>(context, batch =>
                {
                    deselectedBatches.Add(batch.Select(word => word.Id).ToList());
                    selected.RemoveAll(id => batch.Any(word => word.Id == id));
                }));
        });

    private static Word Word(int id, string origin) => new()
    {
        Id = id, Origin = origin, English = $"word {id}", Japanese = $"日本語{id}", Kana = $"かな{id}"
    };

    private static IElement Book(IRenderedComponent<WordList<Word>> cut, string title) =>
        cut.FindAll(".word-book").Single(book => book.GetAttribute("data-book") == title);

    private static IElement Lesson(IElement book, string title) =>
        book.QuerySelectorAll(".word-lesson").Single(lesson => lesson.GetAttribute("data-lesson") == title);

    private static IElement Select(IElement group) => group.QuerySelector("button.word-group-select")!;
    private static IElement Expand(IElement group) => group.QuerySelector("button.word-group-expand")!;
}
