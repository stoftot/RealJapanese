using System.Text.Json;
using System.Text.RegularExpressions;
using DataLoaders.Models;
using Microsoft.Playwright;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks the combined vocabulary editor and saved-only mixer, preserving dataset identities and source queries.</summary>
public sealed class VocabularyCategoryBrowserTests : BrowserTest
{
    [Fact]
    public async Task Unified_editor_persists_each_source_and_legacy_routes_open_the_combined_bundle()
    {
        await SeedFixtureAsync(saveProgress: false);
        var errors = ObserveBrowserErrors();
        await OpenAsync("/vocabulary");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Words & expressions", Exact = true })).ToBeVisibleAsync();
        var navigation = Page.GetByRole(AriaRole.Navigation, new() { Name = "Main navigation", Exact = true });
        await Expect(navigation.GetByRole(AriaRole.Link, new() { Name = "Words & expressions", Exact = true })).ToHaveCountAsync(1);
        foreach (var category in VocabularyCatalog.AdditionalDatasets)
            await Expect(navigation.GetByRole(AriaRole.Link, new() { Name = category, Exact = true })).ToHaveCountAsync(0);
        var known = Column(0);
        foreach (var dataset in new[] { "Words", "Adverbs", "Expressions" })
            await EditableItem(known, dataset, 7).ClickAsync();
        await Expect(known.Locator(".card-header")).ToContainTextAsync("3 selected");
        await EditableItem(Column(1), "Adverbs", 8).ClickAsync();
        await Expect(EditableItem(Column(1), "Adverbs", 8)).ToHaveAttributeAsync("aria-pressed", "true");
        await EditableItem(Column(2), "Adverbs", 9).ClickAsync();
        await Expect(EditableItem(Column(2), "Adverbs", 9)).ToHaveAttributeAsync("aria-pressed", "true");

        var paths = Workspace.CreatePaths();
        Assert.Equal(new[] { 7 }, new WordData(paths).VocabWordIds);
        Assert.Equal(new[] { 7 }, new AdverbData(paths).VocabWordIds);
        Assert.Equal(new[] { 7 }, new ExpressionData(paths).VocabWordIds);
        Assert.Equal(new[] { 8 }, new AdverbData(paths).RehearsingWordIds);
        Assert.Equal(new[] { 9 }, new AdverbData(paths).TrainingWordIds);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(known.Locator(".card-header")).ToContainTextAsync("3 selected");

        // The former category-specific entry points share the same editable bundle.
        foreach (var category in VocabularyCatalog.AdditionalDatasets)
        {
            await OpenAsync("/" + category.ToLowerInvariant());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Words & expressions", Exact = true })).ToBeVisibleAsync();
            await Expect(Column(0).Locator(".card-header")).ToContainTextAsync("3 selected");
            foreach (var dataset in new[] { "Words", "Adverbs", "Expressions" })
                await Expect(EditableItem(Column(0), dataset, 7)).ToHaveAttributeAsync("aria-pressed", "true");
        }

        await OpenAsync("/vocabulary");
        await Practice("Learn the words").ClickAsync();
        await Expect(Page).ToHaveURLAsync(App.Url + "/vocabulary/spelling?category=known");
        Assert.Contains(await Page.Locator("p.lead").InnerTextAsync(), new[] { "general known", "adverb known", "expression known" });
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words & expressions", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(App.Url + "/vocabulary?category=known");
        await Practice("Flash cards").ClickAsync();
        await Expect(Page).ToHaveURLAsync(App.Url + "/vocabulary/flashcards?category=known");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
        var answer = (await Page.GetByRole(AriaRole.Alert).InnerTextAsync()).Replace("Answer:", "", StringComparison.Ordinal).Trim();
        Assert.Contains(answer, new[] { "general known", "adverb known", "expression known" });
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Mixer_toggles_partition_saved_words_and_restore_them_without_writing_progress()
    {
        await SeedFixtureAsync(saveProgress: true);
        var progressBefore = await ReadProgressAsync();
        var errors = ObserveBrowserErrors();
        await OpenAsync("/words");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Words", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("button[data-source]")).ToHaveCountAsync(11);
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(11);
        await Expect(Source("Words")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".list-group-item[data-dataset='Words']")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".word-group-select, button.list-group-item")).ToHaveCountAsync(0);
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(3);
        await Expect(SavedItems(Column(1))).ToHaveCountAsync(1);
        await Expect(SavedItems(Column(2))).ToHaveCountAsync(1);

        // Identical original IDs are distinct entries when their datasets differ.
        foreach (var dataset in new[] { "Adverbs", "Expressions", "Nouns" })
            await Expect(SavedItem(Column(0), dataset, 7)).ToHaveCountAsync(1);
        await Expect(Page.Locator(".list-group-item[data-source-id='10']")).ToHaveCountAsync(0);
        await Expect(SavedItem(Column(0), "Adverbs", 8)).ToHaveCountAsync(0);
        await Expect(SavedItem(Column(1), "Adverbs", 8)).ToHaveCountAsync(1);
        await Expect(SavedItem(Column(2), "Adverbs", 9)).ToHaveCountAsync(1);

        await Source("Adverbs").ClickAsync();
        await Expect(Source("Adverbs")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(2);
        await Expect(SavedItems(Column(1))).ToHaveCountAsync(0);
        await Expect(SavedItems(Column(2))).ToHaveCountAsync(0);
        Assert.DoesNotContain("Adverbs", SelectedSources());
        Assert.Equal(progressBefore, await ReadProgressAsync());
        await Source("Adverbs").ClickAsync();
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(3);
        await Expect(SavedItems(Column(1))).ToHaveCountAsync(1);
        await Expect(SavedItems(Column(2))).ToHaveCountAsync(1);

        await Column(0).GetByRole(AriaRole.Searchbox).FillAsync("adverb known");
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(1);
        await Expect(SavedItem(Column(0), "Adverbs", 7)).ToBeVisibleAsync();
        await Column(0).GetByRole(AriaRole.Searchbox).FillAsync("");
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(3);
        foreach (var key in SourceKeys)
            await Source(key).ClickAsync();
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(0);
        Assert.Equal(new[] { "none" }, SelectedSources());
        foreach (var column in Enumerable.Range(0, 3))
            await Expect(SavedItems(Column(column))).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(0);
        Assert.Equal(progressBefore, await ReadProgressAsync());
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Mixer_preserves_sources_and_study_category_through_practice_back_reload_and_mobile_layout()
    {
        await SeedFixtureAsync(saveProgress: true);
        var progressBefore = await ReadProgressAsync();
        var errors = ObserveBrowserErrors();
        await OpenAsync("/words?category=training&sources=Nouns%2CAdverbs");
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(2);
        await Expect(Source("Nouns")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Source("Adverbs")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Source("Words")).ToHaveCountAsync(0);
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(2);
        await Expect(SavedItems(Column(1))).ToHaveCountAsync(1);
        await Expect(SavedItems(Column(2))).ToHaveCountAsync(1);
        await Expect(Page.Locator(".category-column-active")).ToContainTextAsync("adverb training");

        await Practice("Learn the words").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/words/spelling\?"));
        Assert.Equal("/words/spelling", new Uri(Page.Url).AbsolutePath);
        Assert.Equal("training", QueryValue("category"));
        Assert.Equal(new[] { "Nouns", "Adverbs" }, SelectedSources());
        await Expect(Page.Locator("p.lead")).ToHaveTextAsync("adverb training");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/words\?"));
        Assert.Equal("/words", new Uri(Page.Url).AbsolutePath);
        Assert.Equal("training", QueryValue("category"));
        Assert.Equal(new[] { "Nouns", "Adverbs" }, SelectedSources());
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".category-column-active")).ToContainTextAsync("adverb training");
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(2);

        await Practice("Flash cards").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/words/flashcards\?"));
        Assert.Equal("/words/flashcards", new Uri(Page.Url).AbsolutePath);
        Assert.Equal("training", QueryValue("category"));
        Assert.Equal(new[] { "Nouns", "Adverbs" }, SelectedSources());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show answer", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("adverb training");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words", Exact = true }).ClickAsync();
        await Page.SetViewportSizeAsync(320, 800);
        await Expect(Page.Locator(".category-column:visible")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".category-column-active")).ToContainTextAsync("adverb training");
        await Expect(Source("Words")).ToHaveCountAsync(0);
        await Expect(Source("Nouns")).ToBeVisibleAsync();
        await Expect(Source("Adverbs")).ToBeVisibleAsync();
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));

        await Source("Adverbs").ClickAsync();
        await Expect(SavedItems(Column(2))).ToHaveCountAsync(0);
        await Source("Adverbs").ClickAsync();
        await Expect(SavedItem(Column(2), "Adverbs", 9)).ToBeVisibleAsync();
        Assert.Equal(progressBefore, await ReadProgressAsync());
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Mixer_ignores_fallback_words_in_legacy_queries_without_changing_saved_progress()
    {
        await SeedFixtureAsync(saveProgress: true);
        var progressBefore = await ReadProgressAsync();
        var errors = ObserveBrowserErrors();
        await OpenAsync("/words?category=known&sources=Words%2CNouns%2Cunknown-source");
        await Expect(Page.Locator("button[data-source]")).ToHaveCountAsync(11);
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(1);
        await Expect(Source("Nouns")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Source("Words")).ToHaveCountAsync(0);
        await Expect(SavedItems(Column(0))).ToHaveCountAsync(1);
        await Expect(SavedItem(Column(0), "Nouns", 7)).ToBeVisibleAsync();
        await Expect(Page.Locator(".list-group-item[data-dataset='Words']")).ToHaveCountAsync(0);
        await Practice("Learn the words").ClickAsync();
        await Expect(Page.Locator("p.lead")).ToHaveTextAsync("noun known");
        Assert.Equal(new[] { "Nouns" }, SelectedSources());
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words", Exact = true }).ClickAsync();
        Assert.Equal(new[] { "Nouns" }, SelectedSources());

        await OpenAsync("/words/flashcards?category=known&sources=Words");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Nothing to practise yet", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to words", Exact = true }).ClickAsync();
        Assert.Equal(new[] { "none" }, SelectedSources());
        await Expect(Page.Locator("button[data-source][aria-pressed='true']")).ToHaveCountAsync(0);
        Assert.Equal(progressBefore, await ReadProgressAsync());
        Assert.Empty(errors);
    }

    private static readonly string[] SourceKeys =
        ["Nouns", "Verbs", "Adjectives", "Adverbs", "Expressions", "Pronouns", "Determiners", "Counters", "Conjunctions", "Particles", "Suffixes"];

    private List<string> ObserveBrowserErrors()
    {
        List<string> errors = [];
        Page.PageError += (_, message) => errors.Add(message);
        Page.Console += (_, message) =>
        {
            if (message.Type == "error") errors.Add(message.Text);
        };
        return errors;
    }

    private async Task SeedFixtureAsync(bool saveProgress)
    {
        await App.DisposeAsync();
        await WriteCatalogAsync("Words", [Word(7, "general known", "ねこ"), Word(8, "general unsaved", "いぬ")]);
        await WriteCatalogAsync("Adverbs", [Word(7, "adverb known", "はやく"), Word(8, "adverb rehearsing", "ゆっくり"),
            Word(9, "adverb training", "とても"), Word(10, "adverb unsaved", "あまり")]);
        await WriteCatalogAsync("Expressions", [Word(7, "expression known", "こんにちは")]);
        await WriteCatalogAsync("Nouns", [Word(7, "noun known", "みず"), Word(8, "noun unsaved", "そら")]);
        if (saveProgress)
        {
            var paths = Workspace.CreatePaths();
            var words = new WordData(paths);
            words.AddToVocab(words.Words.Single(word => word.Id == 7));
            var adverbs = new AdverbData(paths);
            adverbs.AddToVocab(adverbs.Words.Single(word => word.Id == 7));
            adverbs.AddToRehearsing(adverbs.Words.Single(word => word.Id == 8));
            adverbs.AddToTraining(adverbs.Words.Single(word => word.Id == 9));
            var expressions = new ExpressionData(paths);
            expressions.AddToVocab(expressions.Words.Single(word => word.Id == 7));
            var nouns = new NounData(paths);
            nouns.AddToVocab(nouns.Words.Single(word => word.Id == 7));
        }
        await App.StartAsync();
    }

    private Task WriteCatalogAsync(string dataset, Word[] words) => File.WriteAllTextAsync(
        Path.Combine(Workspace.CatalogRoot, dataset, dataset + ".json"), JsonSerializer.Serialize(words), TestContext.Current.CancellationToken);

    private Task<string> ReadProgressAsync() => File.ReadAllTextAsync(
        Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json"), TestContext.Current.CancellationToken);

    private static Word Word(int id, string english, string kana) => new()
    {
        Id = id, English = english, Japanese = kana, Kana = kana
    };

    private ILocator Column(int index) => Page.Locator(".category-column").Nth(index);
    private ILocator Source(string key) => Page.Locator($"button[data-source='{key}']");
    private ILocator Practice(string title) => Page.Locator(".practice-selector .card").Filter(new() { HasText = title })
        .GetByRole(AriaRole.Button, new() { Name = "Start practice", Exact = true });
    private static ILocator EditableItem(ILocator column, string dataset, int id) =>
        column.Locator($"button.list-group-item[data-dataset='{dataset}'][data-source-id='{id}']");
    private static ILocator SavedItem(ILocator column, string dataset, int id) =>
        column.Locator($"div.list-group-item[data-dataset='{dataset}'][data-source-id='{id}']");
    private static ILocator SavedItems(ILocator column) => column.Locator("div.list-group-item[data-dataset][data-source-id]");
    private string[] SelectedSources() => QueryValue("sources")?.Split(',') ?? SourceKeys;
    private string? QueryValue(string key) => new Uri(Page.Url).Query.TrimStart('?').Split('&')
        .Select(pair => pair.Split('=', 2)).Where(pair => pair[0] == key)
        .Select(pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : "").FirstOrDefault();
}
