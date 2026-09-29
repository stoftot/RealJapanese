using System.Text.Json;
using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories.Kana;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks kana choice persistence, study behavior, offline fonts and storage failure handling in Chromium.</summary>
public sealed class KanaBrowserTests : BrowserTest
{
    private const string StorageKey = "realjapanese.kana.v1";

    [Fact]
    public async Task Choices_and_settings_persist_while_browser_contexts_remain_isolated()
    {
        await OpenAsync("/kana");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear all" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "あ a", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status).First).ToContainTextAsync("1 characters selected");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Katakana", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear all" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "ア a", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Double", Exact = false }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "キャ kya", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Noto Serif JP", Exact = true }).ClickAsync();
        await Page.GetByLabel("Random order").UncheckAsync();
        await Page.GetByLabel("Auto-submit").UncheckAsync();
        await Page.WaitForFunctionAsync("key => { const value = JSON.parse(localStorage.getItem(key) ?? 'null'); return value?.AutoSubmit === false && value?.RandomOrder === false; }", StorageKey);
        var beforeNavigation = await Page.EvaluateAsync<string>("key => localStorage.getItem(key)", StorageKey);
        using (var savedPreferences = JsonDocument.Parse(beforeNavigation))
        {
            Assert.False(savedPreferences.RootElement.GetProperty("AutoSubmit").GetBoolean());
            Assert.False(savedPreferences.RootElement.GetProperty("RandomOrder").GetBoolean());
        }

        await Page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(App.Url + "/");
        await OpenAsync("/kana");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Double", Exact = false })).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Katakana", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Single", Exact = false }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "ア a", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Double", Exact = false }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "キャ kya", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Hiragana", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Single", Exact = false }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "あ a", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Noto Sans JP", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Noto Serif JP", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.GetByLabel("Random order")).Not.ToBeCheckedAsync();
        await Expect(Page.GetByLabel("Auto-submit")).Not.ToBeCheckedAsync();

        await using var isolated = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var otherPage = await isolated.NewPageAsync();
        await NavigateAsync(otherPage, App.Url + "/kana");
        await Expect(otherPage.GetByRole(AriaRole.Button, new() { Name = "あ a", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(otherPage.GetByRole(AriaRole.Button, new() { Name = "い i", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await otherPage.GetByRole(AriaRole.Button, new() { Name = "Katakana", Exact = true }).ClickAsync();
        await otherPage.GetByRole(AriaRole.Button, new() { Name = "Double", Exact = false }).ClickAsync();
        await Expect(otherPage.GetByRole(AriaRole.Button, new() { Name = "キャ kya", Exact = true })).ToHaveAttributeAsync("aria-pressed", "false");
        await otherPage.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Expect(otherPage.GetByLabel("Auto-submit")).ToBeCheckedAsync();
        await Expect(otherPage.GetByLabel("Random order")).ToBeCheckedAsync();
        await isolated.CloseAsync();
    }

    [Fact]
    public async Task Correct_wrong_reveal_and_review_flow_tracks_first_try_score()
    {
        await SetPreferencesAsync(new KanaPreferences { Selected = ["h-あ", "h-い"], RandomOrder = false });
        await OpenAsync("/kana");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        var input = Page.GetByLabel("Rōmaji answer");
        var status = Page.Locator(".study-status");
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("あ");
        await input.FillAsync("a");
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("い");
        await Expect(input).ToHaveValueAsync("");
        await Expect(input).ToBeFocusedAsync();
        await input.FillAsync("wrong");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("い");
        await Expect(Page.GetByText("Try again, or show the answer.")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("i", new() { Exact = true })).ToBeVisibleAsync();
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".round-score")).ToContainTextAsync("1 / 2 correct");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Review (1)" })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Review (1)" }).ClickAsync();
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("い");
        await input.FillAsync("i");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Back to study" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Review (0)" })).ToBeDisabledAsync();
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Review (0)" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Manual_submit_can_disable_scoring_high_scores_and_review()
    {
        await SetPreferencesAsync(new KanaPreferences
        {
            Selected = ["h-あ", "h-い"], RandomOrder = false, AutoSubmit = false,
            HighScores = false, Review = false, Scoring = false
        });
        await OpenAsync("/kana");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        var input = Page.GetByLabel("Rōmaji answer");
        await input.FillAsync("a");
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("あ");
        await Expect(Page.Locator(".study-status")).ToContainTextAsync("Card 1 / 2");
        await input.PressAsync("Enter");
        await Expect(Page.Locator(".study-character")).ToHaveTextAsync("い");
        await input.FillAsync("wrong");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Try again, or show the answer.")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Review (1)" })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".round-score")).ToHaveCountAsync(0);
        await Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("Best score:"))).ToHaveCountAsync(0);
        var saved = await Page.EvaluateAsync<string?>("key => localStorage.getItem(key)", StorageKey);
        Assert.NotNull(saved);
        using var document = JsonDocument.Parse(saved!);
        Assert.Empty(document.RootElement.GetProperty("Best").EnumerateObject());
        Assert.Empty(document.RootElement.GetProperty("ReviewCards").EnumerateArray());
    }

    [Fact]
    public async Task High_score_keeps_best_result_and_separates_sets_while_empty_selection_is_explained()
    {
        await SetPreferencesAsync(new KanaPreferences { Selected = ["h-あ"], RandomOrder = false });
        await OpenAsync("/kana");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        var input = Page.GetByLabel("Rōmaji answer");
        await input.FillAsync("a");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".round-score")).ToContainTextAsync("1 / 1 correct");
        Assert.Equal(100, await ReadOnlyBestPercentAsync());
        Assert.Equal(1, await ReadBestCountAsync());

        await Page.ReloadAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        await input.FillAsync("wrong");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
        await input.PressAsync("Enter");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".round-score")).ToContainTextAsync("0 / 1 correct");
        await Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("Best score: 100%"))).ToBeVisibleAsync();
        Assert.Equal(100, await ReadOnlyBestPercentAsync());
        Assert.Equal(1, await ReadBestCountAsync());

        await Page.GetByRole(AriaRole.Button, new() { Name = "Hiragana", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear all" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "い i", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        await input.FillAsync("i");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Round complete" })).ToBeVisibleAsync();
        Assert.Equal(2, await ReadBestCountAsync());

        await Page.GetByRole(AriaRole.Button, new() { Name = "Hiragana", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear all" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Choose some kana first" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task Kana_chart_fonts_and_practice_fit_and_all_offline_fonts_load(int width)
    {
        var pageErrors = new List<string>();
        var fontFailures = new List<string>();
        Page.PageError += (_, error) => pageErrors.Add(error);
        Page.RequestFailed += (_, request) => fontFailures.Add(request.Url);
        await Page.SetViewportSizeAsync(width, 900);
        await OpenAsync("/kana");
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), "Kana chart horizontally overflows page");
        const string loadKanaFonts = "async () => { const names = ['Noto Sans JP','Noto Serif JP','Zen Kurenaido','Kaisei Tokumin','Kiwi Maru','Stick','Shippori Antique B1','Kaisei Opti','Klee One']; const faces = await Promise.all(names.map(name => document.fonts.load(`16px \"Kana ${name}\"`, 'あア'))); return faces.every(group => group.length > 0 && group.every(face => face.status === 'loaded')) && names.every(name => document.fonts.check(`16px \"Kana ${name}\"`, 'あア')); }";
        Assert.True(await Page.EvaluateAsync<bool>(loadKanaFonts), "One or more local kana font faces did not load on the character chart");
        var screenshots = Path.Combine(TestWorkspace.RepositoryRoot, ".tooling", "scratch", "kana-browser");
        Directory.CreateDirectory(screenshots);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, $"selection-{width}.png"), FullPage = true });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true }).ClickAsync();
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), "Settings horizontally overflow page");
        Assert.True(await Page.EvaluateAsync<bool>(loadKanaFonts), "One or more local kana font faces did not load on settings");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, $"settings-{width}.png"), FullPage = true });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Study", Exact = true }).ClickAsync();
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), "Study page horizontally overflows page");
        await Expect(Page.Locator(".study-character")).ToBeInViewportAsync();
        await Page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, $"practice-{width}.png"), FullPage = true });
        Assert.Empty(fontFailures);
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Malformed_or_unavailable_storage_shows_message_and_keeps_default_choices_usable()
    {
        await Page.AddInitScriptAsync($"localStorage.setItem('{StorageKey}', 'not valid json');");
        await OpenAsync("/kana");
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Your saved kana choices could not be read" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "あ a", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "い i", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Your saved kana choices could not be read" })).ToHaveCountAsync(0);
        var repaired = await Page.EvaluateAsync<string?>("key => localStorage.getItem(key)", StorageKey);
        Assert.NotNull(repaired);
        using (var repairedDocument = JsonDocument.Parse(repaired!))
            Assert.True(repairedDocument.RootElement.GetProperty("Selected").GetArrayLength() > 0);

        await using var deniedContext = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        await deniedContext.AddInitScriptAsync("Object.defineProperty(window, 'localStorage', { configurable: false, get() { throw new DOMException('denied', 'SecurityError'); } });");
        var deniedPage = await deniedContext.NewPageAsync();
        await NavigateAsync(deniedPage, App.Url + "/kana");
        await Expect(deniedPage.GetByRole(AriaRole.Status).Filter(new() { HasText = "Storage is unavailable" })).ToBeVisibleAsync();
        await Expect(deniedPage.GetByRole(AriaRole.Button, new() { Name = "あ a", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await deniedPage.GetByRole(AriaRole.Button, new() { Name = "い i", Exact = true }).ClickAsync();
        await Expect(deniedPage.GetByRole(AriaRole.Status).Filter(new() { HasText = "Your choices could not be saved" })).ToBeVisibleAsync();
        await Expect(deniedPage.GetByRole(AriaRole.Button, new() { Name = "い i", Exact = true })).ToHaveAttributeAsync("aria-pressed", "false");
        await deniedContext.CloseAsync();
    }

    private async Task SetPreferencesAsync(KanaPreferences preferences)
    {
        var json = JsonSerializer.Serialize(preferences);
        await Page.AddInitScriptAsync($"if (localStorage.getItem('{StorageKey}') === null) localStorage.setItem('{StorageKey}', {JsonSerializer.Serialize(json)});");
    }

    private async Task<int> ReadOnlyBestPercentAsync()
    {
        var saved = await Page.EvaluateAsync<string?>("key => localStorage.getItem(key)", StorageKey);
        Assert.NotNull(saved);
        using var document = JsonDocument.Parse(saved!);
        return Assert.Single(document.RootElement.GetProperty("Best").EnumerateObject()).Value.GetProperty("Percent").GetInt32();
    }

    private async Task<int> ReadBestCountAsync()
    {
        var saved = await Page.EvaluateAsync<string?>("key => localStorage.getItem(key)", StorageKey);
        Assert.NotNull(saved);
        using var document = JsonDocument.Parse(saved!);
        return document.RootElement.GetProperty("Best").EnumerateObject().Count();
    }
}
