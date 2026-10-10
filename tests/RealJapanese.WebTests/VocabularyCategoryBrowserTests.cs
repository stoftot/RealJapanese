using Microsoft.Playwright;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks each new category's real routes, saved selection and both basic exercises.</summary>
public sealed class VocabularyCategoryBrowserTests : BrowserTest
{
    [Fact]
    public async Task Every_category_is_reachable_from_the_hub_and_uses_its_selected_word()
    {
        List<string> errors = [];
        Page.PageError += (_, message) => errors.Add(message);
        foreach (var category in VocabularyCatalog.AdditionalDatasets)
        {
            var route = category.ToLowerInvariant();
            await OpenAsync("/words");
            await Page.GetByRole(AriaRole.Navigation, new() { Name = "Vocabulary categories", Exact = true })
                .GetByRole(AriaRole.Link, new() { Name = category, Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(App.Url + "/" + route);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = category, Exact = true })).ToBeVisibleAsync();
            var known = Page.Locator(".category-column").First;
            var first = known.Locator("button.list-group-item").First;
            var english = await first.Locator(".fw-bold").InnerTextAsync();
            await first.ClickAsync();
            await Expect(known.Locator(".card-header")).ToContainTextAsync("1 selected");
            await Page.ReloadAsync();
            await Expect(known.Locator(".card-header")).ToContainTextAsync("1 selected");
            await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Learn the words" })
                .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(App.Url + "/" + route + "/spelling?category=known");
            await Expect(Page.Locator("p.lead")).ToHaveTextAsync(english);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer (Enter)", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Back to " + route, Exact = true }).ClickAsync();
            await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Flash cards" })
                .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(App.Url + "/" + route + "/flashcards?category=known");
            await Expect(Page.Locator("p.flash-card-question")).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync(english);
        }
        Assert.Empty(errors);
    }
}
