using Microsoft.Playwright;
using Repositories;
using Repositories.Exstensions;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Exercises noun selection and both noun practice routes through a real browser.</summary>
public sealed class NounPracticeBrowserTests : BrowserTest
{
    [Fact]
    public async Task Noun_can_be_assigned_to_training_and_used_for_spelling_and_flashcards()
    {
        var nouns = new NounData(Workspace.CreatePaths());
        var selected = nouns.Words
            .GroupBy(noun => noun.English)
            .Select(group => group.First())
            .Take(2)
            .ToArray();
        Assert.Equal(2, selected.Length);

        await OpenAsync("/nouns");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Training", Exact = true }).ClickAsync();
        var training = Page.Locator(".category-column").Nth(2);
        var search = training.GetByRole(AriaRole.Searchbox);
        foreach (var noun in selected)
        {
            await search.FillAsync(noun.English);
            var item = training.Locator("button.list-group-item").Filter(new() { HasText = noun.English }).First;
            await item.ClickAsync();
            await Expect(item).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("active"));
        }

        await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Learn the nouns" })
            .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();

        var question = Page.Locator("p.lead");
        var firstQuestion = await question.InnerTextAsync();
        var answer = selected.Single(noun => noun.English == firstQuestion.Trim()).Kana.ToRomaji();
        var input = Page.GetByPlaceholder("Type your answer…");
        await input.FillAsync(answer);
        await Expect(question).Not.ToHaveTextAsync(firstQuestion);

        await Page.GetByRole(AriaRole.Link, new() { Name = "Nouns", Exact = true }).ClickAsync();
        // Returning to a selector starts on Known; use the populated Training list.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Training", Exact = true }).ClickAsync();
        await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Flash cards" })
            .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
        var flashcardQuestion = await Page.Locator("p.flash-card-question").InnerTextAsync();
        Assert.Contains(selected, noun => flashcardQuestion.Contains(noun.Kana, StringComparison.Ordinal)
            || flashcardQuestion.Contains(noun.Japanese, StringComparison.Ordinal)
            || flashcardQuestion.Contains(noun.English, StringComparison.Ordinal));
    }
}
