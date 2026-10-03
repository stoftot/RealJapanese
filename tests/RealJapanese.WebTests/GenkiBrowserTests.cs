using Microsoft.Playwright;
using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks real Genki navigation, sentence entry, comparison and retry flow on desktop and mobile without browser errors or overflow.</summary>
public sealed class GenkiBrowserTests : BrowserTest
{
    [Theory]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task Lesson_navigation_and_sentence_retry_round_work_at_mobile_and_desktop_widths(int width)
    {
        var pageErrors = new List<string>();
        Page.PageError += (_, error) => pageErrors.Add(error);
        await Page.SetViewportSizeAsync(width, 900);
        await OpenAsync("/");
        await Page.GetByRole(AriaRole.Navigation, new() { Name = "Main navigation" })
            .GetByRole(AriaRole.Link, new() { Name = "Genki", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".lesson-grid a")).ToHaveCountAsync(12);
        await AssertFitsViewportAsync("Lesson index");
        await Page.Locator(".lesson-grid a[href='genki/1']").ClickAsync();
        await Expect(Page.Locator(".genki-module > h2")).ToContainTextAsync("Lesson 1 ·");
        await AssertFitsViewportAsync("Grammar recap");
        var advertisedPatterns = await Page.Locator("#g01-03 .small.text-muted").InnerTextAsync();
        var patternCount = int.Parse(Regex.Match(advertisedPatterns, @"(\d+) patterns").Groups[1].Value);
        Assert.True(patternCount > 0, "The recap must advertise a nonempty practice round.");
        await Page.Locator("#g01-03").GetByRole(AriaRole.Button).ClickAsync();
        await Expect(Page.Locator(".sentence-practice h3")).ToBeFocusedAsync();
        // Bootstrap animates the focus scroll; wait for the observable final position.
        await Expect(Page.Locator(".sentence-practice h3")).ToBeInViewportAsync(new() { Ratio = 1 });

        var input = Page.GetByLabel("Your Japanese sentence");
        var next = Page.GetByRole(AriaRole.Button, new() { Name = "I understand · next", Exact = true });
        var compare = Page.GetByRole(AriaRole.Button, new() { Name = "Compare with model", Exact = true });
        var progress = Page.Locator(".sentence-practice .card-body small");
        var question = Page.Locator(".sentence-practice p.lead");
        var feedback = Page.Locator(".review-note");
        var answers = Page.Locator(".sentence-practice [role=alert]");
        await Expect(input).ToBeVisibleAsync();
        await Expect(next).ToBeDisabledAsync();
        await Expect(progress).ToHaveTextAsync($"0 reviewed · {patternCount} remaining");
        var originalQuestion = await question.InnerTextAsync();
        await input.FillAsync("別の言い方です。");
        await compare.ClickAsync();
        await Expect(feedback).ToContainTextAsync("different sentence may also be valid");
        await Expect(next).ToBeEnabledAsync();
        var originalModels = await answers.InnerTextAsync();
        await AssertFitsViewportAsync("Revealed sentence practice");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Practise this again", Exact = true }).ClickAsync();
        await Expect(progress).ToHaveTextAsync($"1 reviewed · {patternCount} remaining");
        await Expect(input).ToHaveValueAsync("");
        await Expect(next).ToBeDisabledAsync();
        for (var index = 1; index < patternCount; index++)
        {
            await compare.ClickAsync();
            await next.ClickAsync();
            await Expect(progress).ToHaveTextAsync($"{index + 1} reviewed · {patternCount - index} remaining");
        }
        await Expect(progress).ToHaveTextAsync($"{patternCount} reviewed · 1 remaining");
        await Expect(question).ToHaveTextAsync(originalQuestion);
        await compare.ClickAsync();
        await Expect(answers).ToHaveTextAsync(originalModels);
        await next.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync($"You reviewed {patternCount + 1} sentence prompts");
        await AssertFitsViewportAsync("Completed practice round");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Next lesson", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".genki-module > h2")).ToContainTextAsync("Lesson 2 ·");
        await Expect(Page.Locator(".sentence-practice, .review-note")).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Link, new() { Name = "All lessons", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".lesson-grid a")).ToHaveCountAsync(12);
        await Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        Assert.Empty(pageErrors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task Unknown_lesson_route_has_a_working_return_to_the_lesson_index(int lesson)
    {
        var pageErrors = new List<string>();
        Page.PageError += (_, error) => pageErrors.Add(error);
        await OpenAsync($"/genki/{lesson}");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("This lesson does not exist");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Choose a lesson", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".lesson-grid a")).ToHaveCountAsync(12);
        await Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        Assert.Empty(pageErrors);
    }

    private async Task AssertFitsViewportAsync(string stage) =>
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"),
            stage + " horizontally overflows the viewport.");
}
