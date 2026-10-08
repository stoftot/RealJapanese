using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks that verb, adjective and noun grammar practice load and remain interactive in the real web host.</summary>
public sealed class GrammarPracticeBrowserTests : BrowserTest
{
    [Theory]
    [InlineData("/verbs/ConjugationsAndForms")]
    [InlineData("/adjectives/conjugateBase")]
    [InlineData("/nouns/conjugate")]
    public async Task Grammar_practice_loads_reveals_and_advances_without_errors(string route)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        Page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        var response = await Page.GotoAsync(App.Url + route);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("p.lead")).ToBeVisibleAsync();
        var progress = Page.Locator("small.text-muted");
        var initialProgress = await progress.InnerTextAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer (Enter)", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Answers:");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Expect(progress).Not.ToHaveTextAsync(initialProgress);
        await Expect(Page.GetByPlaceholder("Type your answer…")).ToBeFocusedAsync();
        Assert.Empty(errors);
    }
}
