using System.Text.Json;
using DataLoaders.Models;
using Microsoft.Playwright;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks keyboard group controls, collapsed bulk scope, search and persisted selection in real Chromium.</summary>
public sealed class WordListGroupingBrowserTests : BrowserTest
{
    [Fact]
    public async Task GenkiGreetingsAppearBeforeLessonOneAndKeepTheirOriginalSavedIdentities()
    {
        var expressions = new ExpressionData(Workspace.CreatePaths());
        expressions.AddToVocab(expressions.Words.Single(word => word.Id == 0));
        await Page.SetViewportSizeAsync(411, 850);
        await OpenAsync("/vocabulary");
        var known = Page.Locator(".category-column").First;
        var book = known.Locator(".word-book[data-book='GENKI I (3rd edition)']");
        var sections = book.Locator(".word-lesson");
        await Expect(sections.First).ToHaveAttributeAsync("data-lesson", "Greetings & introductions");
        await Expect(sections.Nth(1)).ToHaveAttributeAsync("data-lesson", "Lesson 1");
        var introduction = sections.First;
        await Expect(introduction.Locator("button.list-group-item")).ToHaveCountAsync(19);
        await Expect(introduction.Locator("button.list-group-item.active")).ToHaveAttributeAsync("data-source-id", "0");
        await introduction.GetByRole(AriaRole.Button, new() { Name = "Collapse Greetings & introductions", Exact = true }).ClickAsync();
        await introduction.GetByRole(AriaRole.Checkbox).ClickAsync();
        await Expect(introduction.GetByRole(AriaRole.Checkbox)).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(introduction.GetByRole(AriaRole.Button)).ToHaveAttributeAsync("aria-expanded", "false");
        Assert.Equal(Enumerable.Range(0, 19), new ExpressionData(Workspace.CreatePaths()).VocabWordIds.Order());

        await Page.ReloadAsync();
        await Expect(introduction.Locator("button.list-group-item.active")).ToHaveCountAsync(19);
        await Expect(known.Locator(".word-book[data-book='General']")).Not.ToContainTextAsync("Good morning");
        await OpenAsync("/words?category=known&sources=Expressions");
        var savedIntroduction = Page.Locator(".category-column").First.Locator(".word-lesson[data-lesson='Greetings & introductions']");
        await Expect(savedIntroduction.Locator(".list-group-item[data-dataset='Expressions']")).ToHaveCountAsync(19);
        await Expect(savedIntroduction.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GroupSelectionKeepsExpansionStableAndPersistsMatchingWordsOnly()
    {
        await App.DisposeAsync();
        var words = new Word[]
        {
            Word(1, "Book-A-1", "cat") with { AdditionalOrigins = ["Book-A-2"] },
            Word(2, "Book-A-2", "dog"), Word(3, "Book B-1", "bird"), Word(4, "self added", "fish")
        };
        await File.WriteAllTextAsync(Path.Combine(Workspace.CatalogRoot, "Words", "Words.json"), JsonSerializer.Serialize(words), TestContext.Current.CancellationToken);
        await App.StartAsync();
        List<string> errors = [];
        Page.PageError += (_, message) => errors.Add(message);
        await OpenAsync("/vocabulary");
        var known = Page.Locator(".category-column").First;
        var book = known.Locator(".word-book[data-book='Book-A']");
        var lesson1 = book.Locator(".word-lesson[data-lesson='Lesson 1']");
        var lesson2 = book.Locator(".word-lesson[data-lesson='Lesson 2']");

        // A separate disclosure button supports Enter without assigning any words.
        await lesson2.GetByRole(AriaRole.Button, new() { Name = "Collapse Lesson 2", Exact = true }).FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(lesson2.GetByRole(AriaRole.Button, new() { Name = "Expand Lesson 2", Exact = true })).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(known.Locator(".card-header")).ToContainTextAsync("0 selected");

        // Space operates the header checkbox; only Lesson 1 is assigned.
        await lesson1.GetByRole(AriaRole.Checkbox).FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        var bookSelect = book.GetByRole(AriaRole.Checkbox, new() { Name = "Select all 2 eligible words in Book-A", Exact = true });
        await Expect(bookSelect).ToHaveAttributeAsync("aria-checked", "mixed");
        await Expect(known.Locator(".card-header")).ToContainTextAsync("1 selected");
        await Expect(lesson2.GetByRole(AriaRole.Button, new() { Name = "Expand Lesson 2", Exact = true })).ToBeVisibleAsync();

        // Book selection includes the collapsed second lesson and deduplicates the shared cat.
        await bookSelect.ClickAsync();
        await Expect(known.Locator(".card-header")).ToContainTextAsync("2 selected");
        await Expect(book.GetByRole(AriaRole.Checkbox, new() { Name = "Deselect all 2 eligible words in Book-A", Exact = true })).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(lesson2.GetByRole(AriaRole.Button, new() { Name = "Expand Lesson 2", Exact = true })).ToBeVisibleAsync();

        await known.GetByRole(AriaRole.Searchbox).FillAsync("cat");
        await Expect(known.Locator(".word-selection-scope")).ToContainTextAsync("1 matches");
        await Expect(known.Locator(".word-selection-scope")).ToContainTextAsync("matching words");
        await book.GetByRole(AriaRole.Checkbox, new() { Name = "Deselect all 1 matching words in Book-A", Exact = true }).ClickAsync();
        await Expect(known.Locator(".card-header")).ToContainTextAsync("1 selected");
        await known.GetByRole(AriaRole.Searchbox).FillAsync("");
        await Expect(lesson2.GetByRole(AriaRole.Button, new() { Name = "Expand Lesson 2", Exact = true })).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(known.Locator(".card-header")).ToContainTextAsync("1 selected");
        await Expect(known.Locator("button.list-group-item.active")).ToContainTextAsync("dog");

        // Narrow layouts retain reachable controls and do not add horizontal overflow.
        await Page.SetViewportSizeAsync(320, 800);
        await Expect(book.GetByRole(AriaRole.Button, new() { Name = "Collapse Book-A", Exact = true })).ToBeVisibleAsync();
        Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
        Assert.Empty(errors);
    }

    private static Word Word(int id, string origin, string english) => new()
    {
        Id = id, Origin = origin, English = english, Japanese = $"日本語{id}", Kana = $"かな{id}"
    };
}
