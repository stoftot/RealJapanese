using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Verifies real keyboard events and shortcut disposal across client-side navigation.</summary>
public sealed class FlashcardBrowserTests : BrowserTest
{
    [Fact]
    public async Task Space_and_backspace_perform_single_actions_after_repeated_navigation()
    {
        SeedWords();
        await OpenAsync("/words/flashcards?category=known");
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var question = Page.Locator(".flash-card-question");
            var primary = Page.Locator(".practice-actions .btn-primary");
            await Expect(primary).ToBeFocusedAsync();
            var initial = await question.InnerTextAsync();
            await primary.PressAsync("Space");
            await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
            await Expect(question).ToHaveTextAsync(initial);
            await primary.PressAsync("Space");
            await Expect(question).Not.ToHaveTextAsync(initial);
            await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            await primary.PressAsync("Backspace");
            await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
            await primary.PressAsync("Space");
            await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            // Use SPA navigation so disposal, not a document reload, removes the listener.
            await Page.GetByRole(AriaRole.Link, new() { Name = "Words", Exact = true }).ClickAsync();
            var search = Page.GetByRole(AriaRole.Searchbox).First;
            await search.FillAsync("abc");
            await search.PressAsync("Space");
            await Expect(search).ToHaveValueAsync("abc ");
            await search.PressAsync("Backspace");
            await Expect(search).ToHaveValueAsync("abc");
            await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Flash cards" })
                .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
        }
    }
}
