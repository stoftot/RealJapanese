using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks responsive selector visibility and keyboard-controlled practice scaling in a rendered browser.</summary>
public sealed class LayoutBrowserTests : BrowserTest
{
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
