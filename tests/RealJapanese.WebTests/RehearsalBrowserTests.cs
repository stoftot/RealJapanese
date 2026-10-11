using System.Text.Json;
using Microsoft.Playwright;
using Repositories;
using Repositories.Exstensions;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Exercises automatic list movement through real Blazor input, retries, navigation, and restart.</summary>
public sealed class RehearsalBrowserTests : BrowserTest
{
    [Fact]
    public async Task Finishing_retry_rechunks_remaining_words_without_changing_selected_chunk()
    {
        var words = SeedWords(5).VocabWords;
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        Page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await OpenAsync("/words/spelling?category=known&sources=Adverbs");
        var chunkInputs = Page.Locator("input[type=number]");
        await chunkInputs.Nth(0).FillAsync("2");
        await chunkInputs.Nth(0).PressAsync("Tab");
        await Expect(Page.Locator("small.text-muted")).ToHaveTextAsync("1/3");
        await chunkInputs.Nth(1).FillAsync("2");
        await chunkInputs.Nth(1).PressAsync("Tab");
        var progress = Page.Locator("small.text-muted");
        await Expect(progress).ToHaveTextAsync("1/2");
        var question = Page.Locator("p.lead");
        var missedQuestion = await question.InnerTextAsync();
        var missed = words.Single(word => word.English == missedQuestion);
        var next = Page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer (Enter)", Exact = true }).ClickAsync();
        await next.ClickAsync();
        await Expect(progress).ToHaveTextAsync("2/2");
        await next.ClickAsync();
        await Expect(progress).ToHaveTextAsync("1/1");
        await Expect(question).ToHaveTextAsync(missed.English);
        await Page.GetByPlaceholder("Type your answer…").FillAsync(missed.Kana.ToRomaji());

        await Expect(progress).ToHaveTextAsync("1/2");
        await Expect(chunkInputs.Nth(0)).ToHaveValueAsync("2");
        await Expect(chunkInputs.Nth(1)).ToHaveValueAsync("2");
        var seen = new HashSet<string>();
        for (var index = 0; index < 2; index++)
        {
            Assert.True(seen.Add(await question.InnerTextAsync()));
            await next.ClickAsync();
            await Expect(progress).ToHaveTextAsync(index == 0 ? "2/2" : "1/2");
        }
        Assert.Equal(words.Where(word => word.Id != missed.Id).Skip(2).Select(word => word.English).Order(), seen.Order());
        Assert.Contains(missed.Id, new AdverbData(Workspace.CreatePaths()).RehearsingWordIds);
        await Expect(Page.GetByPlaceholder("Type your answer…")).ToBeFocusedAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Missed_known_word_moves_immediately_and_stays_rehearsing_after_retry_and_restart()
    {
        var word = SeedWords(1).VocabWords.Single();
        await OpenAsync("/words/spelling?category=known&sources=Adverbs");
        var input = Page.GetByPlaceholder("Type your answer…");
        await input.FillAsync("incorrect");
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        Assert.DoesNotContain(word.Id, new AdverbData(Workspace.CreatePaths()).VocabWordIds);
        Assert.Contains(word.Id, new AdverbData(Workspace.CreatePaths()).RehearsingWordIds);
        await input.FillAsync(word.Kana.ToRomaji()); // Copying the revealed answer enters the retry.
        await Expect(Page.GetByRole(AriaRole.Alert)).Not.ToBeVisibleAsync();
        await Expect(input).ToHaveValueAsync("");
        Assert.DoesNotContain(word.Id, new AdverbData(Workspace.CreatePaths()).VocabWordIds);
        await input.FillAsync(word.Kana.ToRomaji());
        await Expect(Page.GetByText("Nothing to practise yet", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Contains(word.Id, new AdverbData(Workspace.CreatePaths()).RehearsingWordIds);

        await App.DisposeAsync();
        await App.StartAsync();
        await OpenAsync("/words?category=rehearsing&sources=Adverbs");
        var item = Page.Locator($".list-group-item[data-dataset='Adverbs'][data-source-id='{word.Id}']");
        await Expect(Page.Locator(".category-column").Nth(0).Locator(item)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".category-column").Nth(1).Locator(item)).ToHaveCountAsync(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Immediate_known_move_persists_across_navigation_or_restart_before_retry(bool restart)
    {
        var word = SeedWords(1).VocabWords.Single();
        await OpenAsync("/words/spelling?category=known&sources=Adverbs");
        var input = Page.GetByPlaceholder("Type your answer…");
        await input.FillAsync("incorrect");
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();

        var data = new AdverbData(Workspace.CreatePaths());
        Assert.DoesNotContain(word.Id, data.VocabWordIds);
        Assert.Contains(word.Id, data.RehearsingWordIds);
        Assert.Equal(0, SavedCorrectDays(word.Id));

        if (restart)
        {
            await App.DisposeAsync();
            await App.StartAsync();
            await OpenAsync("/words?category=rehearsing&sources=Adverbs");
        }
        else
        {
            await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words", Exact = true }).ClickAsync();
        }

        var item = $".list-group-item[data-dataset='Adverbs'][data-source-id='{word.Id}']";
        await Expect(Page.Locator(".category-column").Nth(0).Locator(item)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".category-column").Nth(1).Locator(item)).ToHaveCountAsync(1);
        await OpenAsync("/words/spelling?category=known&sources=Adverbs");
        await Expect(Page.GetByText("Nothing to practise yet", new() { Exact = true })).ToBeVisibleAsync();
        await OpenAsync("/words/spelling?category=rehearsing&sources=Adverbs");
        await Expect(Page.Locator("p.lead")).ToHaveTextAsync(word.English);
        Assert.Equal(0, SavedCorrectDays(word.Id));
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
        await Expect(Page.GetByText("Nothing to practise yet", new() { Exact = true })).ToBeVisibleAsync();
        await OpenAsync("/words?category=known&sources=Adverbs");
        var item = $".list-group-item[data-dataset='Adverbs'][data-source-id='{word.Id}']";
        await Expect(Page.Locator(".category-column").Nth(0).Locator(item)).ToHaveCountAsync(1);
        await Expect(Page.Locator(".category-column").Nth(1).Locator(item)).ToHaveCountAsync(0);
    }

    private int SavedCorrectDays(int wordId)
    {
        using var saved = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json")));
        return saved.RootElement.GetProperty("Data").GetProperty("Adverbs").GetProperty("Rehearsal")
            .GetProperty(wordId.ToString()).GetProperty("CorrectDays").GetInt32();
    }

    private sealed class StudyClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
