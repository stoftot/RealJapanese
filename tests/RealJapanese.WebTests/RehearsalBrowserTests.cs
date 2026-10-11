using Microsoft.Playwright;
using Repositories;
using Repositories.Exstensions;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Exercises automatic list movement through real Blazor input, retries, navigation, and restart.</summary>
public sealed class RehearsalBrowserTests : BrowserTest
{
    [Fact]
    public async Task Missed_known_word_moves_after_retry_and_stays_rehearsing_after_restart()
    {
        var word = SeedWords(1).VocabWords.Single();
        await OpenAsync("/words/spelling?category=known&sources=Adverbs");
        var input = Page.GetByPlaceholder("Type your answer…");
        await input.FillAsync("incorrect");
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        Assert.Contains(word.Id, new AdverbData(Workspace.CreatePaths()).VocabWordIds);
        await input.FillAsync(word.Kana.ToRomaji()); // Copying the revealed answer enters the retry.
        await Expect(Page.GetByRole(AriaRole.Alert)).Not.ToBeVisibleAsync();
        await Expect(input).ToHaveValueAsync("");
        Assert.Contains(word.Id, new AdverbData(Workspace.CreatePaths()).VocabWordIds);
        await input.FillAsync(word.Kana.ToRomaji());
        await Expect(input).ToHaveValueAsync("");
        Assert.Contains(word.Id, new AdverbData(Workspace.CreatePaths()).RehearsingWordIds);

        await App.DisposeAsync();
        await App.StartAsync();
        await OpenAsync("/words?category=rehearsing&sources=Adverbs");
        var item = Page.Locator($".list-group-item[data-dataset='Adverbs'][data-source-id='{word.Id}']");
        await Expect(Page.Locator(".category-column").Nth(0).Locator(item)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".category-column").Nth(1).Locator(item)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Seventh_correct_day_promotes_from_rehearsing_to_known()
    {
        var clock = new StudyClock(DateTimeOffset.Now.AddDays(-12));
        var paths = new RepositoryPaths(Workspace.CatalogRoot, Workspace.CreatePaths().ProgressRoot, clock);
        var data = new AdverbData(paths);
        var word = data.Words.First();
        data.AddToRehearsing(word);
        for (var day = 0; day < 6; day++)
        {
            data.RecordPracticeAnswer(word.Id, true);
            clock.Now = clock.Now.AddDays(2);
        }
        await OpenAsync("/words/spelling?category=rehearsing&sources=Adverbs");
        var input = Page.GetByPlaceholder("Type your answer…");
        await input.FillAsync(word.Kana.ToRomaji());
        await Expect(input).ToHaveValueAsync("");
        await OpenAsync("/words?category=known&sources=Adverbs");
        var item = $".list-group-item[data-dataset='Adverbs'][data-source-id='{word.Id}']";
        await Expect(Page.Locator(".category-column").Nth(0).Locator(item)).ToHaveCountAsync(1);
        await Expect(Page.Locator(".category-column").Nth(1).Locator(item)).ToHaveCountAsync(0);
    }

    private sealed class StudyClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
