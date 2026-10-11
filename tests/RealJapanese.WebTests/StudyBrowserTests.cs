using Microsoft.Playwright;
using Repositories;
using Repositories.Exstensions;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks browser persistence and answer focus through the real Blazor circuit and JavaScript helpers.</summary>
public sealed class StudyBrowserTests : BrowserTest
{
    [Fact]
    public async Task Selection_survives_page_reload_and_app_restart()
    {
        await OpenAsync("/vocabulary");
        var list = Page.Locator(".category-column").First;
        var search = list.GetByRole(AriaRole.Searchbox);
        await search.FillAsync("___not_present___");
        await Expect(list).ToContainTextAsync("No matches in this list");
        await search.FillAsync("");
        var item = list.Locator("button.list-group-item[data-dataset='Words']").First;
        var sourceId = await item.GetAttributeAsync("data-source-id");
        var english = await item.Locator(".fw-bold").InnerTextAsync();
        var savedItem = list.Locator($"button.list-group-item[data-dataset='Words'][data-source-id='{sourceId}']");
        await item.ClickAsync();
        await Expect(savedItem).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.ReloadAsync();
        await Expect(savedItem).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(savedItem.Locator(".fw-bold")).ToHaveTextAsync(english);
        await App.DisposeAsync();
        await App.StartAsync();
        await OpenAsync("/vocabulary");
        await Expect(savedItem).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(savedItem.Locator(".fw-bold")).ToHaveTextAsync(english);
    }

    [Fact]
    public async Task Correct_answer_and_enter_reveal_advance_once_and_restore_focus()
    {
        var words = SeedWords().VocabWords;
        var questions = words.EnglishToRomajiQuestions();
        await OpenAsync("/words/spelling?category=known");
        var input = Page.GetByPlaceholder("Type your answer…");
        var question = Page.Locator("p.lead");
        var initial = await question.InnerTextAsync();
        var answer = questions.Single(q => q.Question == initial).Answer;
        await input.FillAsync("  " + answer.ToUpperInvariant() + "  ");
        await Expect(question).Not.ToHaveTextAsync(initial);
        await Expect(input).ToHaveValueAsync("");
        await Expect(input).ToBeFocusedAsync();
        var second = await question.InnerTextAsync();
        await input.FillAsync("definitelyincorrect");
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(question).ToHaveTextAsync(second);
        await input.PressAsync("Enter");
        await Expect(question).Not.ToHaveTextAsync(second);
        await Expect(input).ToHaveValueAsync("");
        await Expect(input).ToBeFocusedAsync();
    }
}
