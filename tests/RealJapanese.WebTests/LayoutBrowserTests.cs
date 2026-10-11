using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks responsive selector visibility and keyboard-controlled practice scaling in a rendered browser.</summary>
public sealed class LayoutBrowserTests : BrowserTest
{
    [Theory]
    [InlineData(320)]
    [InlineData(411)]
    public async Task Phone_navigation_keeps_complete_labels_and_scrolls_each_keyboard_destination_into_view(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await OpenAsync("/");
        var navigation = Page.GetByRole(AriaRole.Navigation, new() { Name = "Main navigation", Exact = true });
        var links = navigation.GetByRole(AriaRole.Link);
        var labels = new[] { "Home", "Kana", "Numbers", "Words", "Nouns", "Verbs", "Adjectives", "Words & expressions", "Kanji" };
        await Expect(links).ToHaveCountAsync(labels.Length);
        Assert.True(await navigation.EvaluateAsync<bool>("""
            nav => nav.parentElement.scrollWidth > nav.parentElement.clientWidth
                && document.documentElement.scrollWidth <= innerWidth
            """), "Navigation should scroll within its bar without making the page wider.");

        await links.First.FocusAsync();
        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0) await Page.Keyboard.PressAsync("Tab");
            var link = links.Nth(index);
            await Expect(link).ToHaveAccessibleNameAsync(labels[index]);
            await Expect(link).ToBeFocusedAsync();
            // Native focus may leave some link padding clipped while its complete label is visible.
            await Expect(link.Locator(".nav-label")).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.True(await link.Locator(".nav-label").EvaluateAsync<bool>("""
                label => {
                    const range = document.createRange();
                    range.selectNodeContents(label);
                    return range.getClientRects().length === 1
                        && parseFloat(getComputedStyle(label).fontSize) >= 12;
                }
                """), $"{labels[index]} should remain a readable, complete single-line label.");
        }
        Assert.True(await navigation.EvaluateAsync<bool>("nav => nav.parentElement.scrollLeft > 0"),
            "Keyboard focus should reach destinations beyond the initially visible links.");
    }

    [Theory]
    [InlineData(320, 1)]
    [InlineData(1280, 3)]
    public async Task Selector_columns_and_scaled_practice_fit_viewport(int width, int visibleColumns)
    {
        SeedWords();
        await Page.SetViewportSizeAsync(width, 900);
        foreach (var route in new[] { "/words", "/kanji" })
        {
            await OpenAsync(route);
            await Expect(Page.Locator(".category-column:visible")).ToHaveCountAsync(visibleColumns);
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), route + " overflows horizontally");
        }
        await OpenAsync("/words/spelling?category=known");
        var scale = Page.GetByRole(AriaRole.Slider, new() { Name = "Text size" });
        await scale.FocusAsync();
        await scale.PressAsync("Home");
        await Expect(scale).ToHaveValueAsync("0.8");
        await Expect(Page.Locator(".scaled")).ToHaveAttributeAsync("style", "--ui-scale:0.8");
        await scale.PressAsync("End");
        await Expect(scale).ToHaveValueAsync("1.6");
        await Expect(Page.Locator(".scaled")).ToHaveAttributeAsync("style", "--ui-scale:1.6");
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), "Scaled practice overflows horizontally");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Show answer (Enter)", Exact = true })).ToBeInViewportAsync();
    }
}
