using Microsoft.Playwright;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks directional moves, persistence, read-only exclusion and usable list space in Chromium.</summary>
public sealed class BulkCategoryMoveBrowserTests : BrowserTest
{
    [Fact]
    public async Task MovingSelectedNounsPersistsAndLeavesTheWordsMixerReadOnly()
    {
        var data = new NounData(Workspace.CreatePaths());
        var moved = data.Words.Take(2).ToArray();
        data.SetCategory(moved, DataLoaders.Models.WordPracticeCategory.Known);
        var existing = data.Words.Skip(2).First();
        data.AddToRehearsing(existing);
        List<string> errors = [];
        Page.PageError += (_, error) => errors.Add(error);
        await OpenAsync("/nouns");
        var known = Page.Locator(".category-column").Nth(0);
        var rehearsing = Page.Locator(".category-column").Nth(1);
        var training = Page.Locator(".category-column").Nth(2);
        await Expect(Page.Locator(".word-move-actions button")).ToHaveCountAsync(4);
        await Expect(training.GetByRole(AriaRole.Button, new() { Name = "Move selected to Rehearsing", Exact = true })).ToBeDisabledAsync();

        // A filter must not silently narrow a move of the whole saved selection.
        await known.GetByRole(AriaRole.Searchbox).FillAsync("__no_matches__");
        var move = known.GetByRole(AriaRole.Button, new() { Name = "Move selected to Rehearsing", Exact = true });
        await move.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(known.Locator(".card-header")).ToContainTextAsync("0 selected");
        await Expect(rehearsing.Locator(".card-header")).ToContainTextAsync("3 selected");
        await Expect(move).ToBeDisabledAsync();
        var expected = moved.Select(word => word.Id).Append(existing.Id).Order().ToArray();
        Assert.Equal(expected, new NounData(Workspace.CreatePaths()).RehearsingWordIds.Order());

        await App.DisposeAsync();
        await App.StartAsync();
        await OpenAsync("/nouns");
        await Expect(rehearsing.Locator("button.list-group-item.active")).ToHaveCountAsync(3);
        await rehearsing.GetByRole(AriaRole.Button, new() { Name = "Move selected to Training", Exact = true }).ClickAsync();
        await Expect(training.Locator(".card-header")).ToContainTextAsync("3 selected");
        await training.GetByRole(AriaRole.Button, new() { Name = "Move selected to Rehearsing", Exact = true }).ClickAsync();
        await Expect(rehearsing.Locator(".card-header")).ToContainTextAsync("3 selected");
        await rehearsing.GetByRole(AriaRole.Button, new() { Name = "Move selected to Known", Exact = true }).ClickAsync();
        await Expect(known.Locator(".card-header")).ToContainTextAsync("3 selected");
        Assert.Equal(expected, new NounData(Workspace.CreatePaths()).VocabWordIds.Order());

        var progressPath = Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json");
        var progressBefore = await File.ReadAllTextAsync(progressPath, TestContext.Current.CancellationToken);
        await OpenAsync("/words?category=known&sources=Nouns");
        await Expect(Page.Locator(".word-move-actions")).ToHaveCountAsync(0);
        await Expect(known.Locator(".list-group-item[data-dataset='Nouns']")).ToHaveCountAsync(3);
        Assert.Equal(progressBefore, await File.ReadAllTextAsync(progressPath, TestContext.Current.CancellationToken));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(320)]
    [InlineData(1280)]
    [InlineData(1920)]
    public async Task MoveButtonsFitAndListsShowTwoHeadersAndFiveWords(int width)
    {
        await Page.SetViewportSizeAsync(width, 1000);
        foreach (var route in new[] { "/nouns", "/vocabulary" })
        {
            await OpenAsync(route);
            var known = Page.Locator(".category-column").First;
            var scroll = known.Locator(".word-list-scroll");
            Assert.True(await scroll.EvaluateAsync<bool>("""
                list => {
                    const top = list.getBoundingClientRect().top;
                    const headers = list.querySelectorAll('.word-group-header');
                    const fifth = list.querySelectorAll('.list-group-item')[4];
                    return headers.length >= 2 && fifth
                        && fifth.getBoundingClientRect().bottom - top <= list.clientHeight + 1;
                }
                """), $"{route} at {width}px must fit two group headers and five full word entries.");

            if (width < 768)
                await Page.GetByRole(AriaRole.Button, new() { Name = "Rehearsing", Exact = true }).ClickAsync();
            var middle = Page.Locator(".category-column").Nth(1);
            await Expect(middle.GetByRole(AriaRole.Button, new() { Name = "Move selected to Known", Exact = true })).ToBeVisibleAsync();
            await Expect(middle.GetByRole(AriaRole.Button, new() { Name = "Move selected to Training", Exact = true })).ToBeVisibleAsync();
            Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
            Assert.True(await middle.EvaluateAsync<bool>("""
                column => {
                    const left = column.querySelector('.word-move-left').getBoundingClientRect();
                    const right = column.querySelector('.word-move-right').getBoundingClientRect();
                    const card = column.querySelector('.card').getBoundingClientRect();
                    return left.right <= right.left && Math.abs(left.left - card.left) <= 1
                        && Math.abs(right.right - card.right) <= 1
                        && Math.max(left.bottom, right.bottom) <= card.top;
                }
                """), "Move buttons should sit at the left and right corners above the card without overlapping.");
        }
    }
}
