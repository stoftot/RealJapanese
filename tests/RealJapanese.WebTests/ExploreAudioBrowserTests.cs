using System.Text.Json;
using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks catalog-wide audio browsing, prefix filters, native playback disposal and virtualized phone layout.</summary>
public sealed class ExploreAudioBrowserTests : BrowserTest
{
    private static readonly string[] Categories = ["All", "Nouns", "Verbs", "Adjectives", "Words & expressions"];
    private ILocator Rows => Page.Locator(".explore-audio-row");
    private ILocator Search => Page.GetByLabel("Search audio", new() { Exact = true });

    [Fact]
    public async Task Catalog_order_prefix_search_and_exclusive_categories_preserve_progress()
    {
        await Page.SetViewportSizeAsync(1280, 1400);
        Seed([
            new("Words", 2, "挨拶", "あいさつ", "Hello"),
            new("Nouns", 9, "手助け", "てだすけ", "help"),
            new("Verbs", 2, "試す", "ためす", "test"),
            new("Adverbs", 1, "早く", "はやく", "quickly"),
            new("Nouns", 2, "返事", "へんじ", "hep"),
            new("Adjectives", 2, "新しい", "あたらしい", "new"),
            new("Expressions", 1, "失礼", "しつれい", "excuse me"),
            new("Nouns", 11, "不足", "ふそく", "missing", Available: false),
            new("Nouns", 12, "古い記録", "ふるいきろく", "stale", Stale: true),
            new("Unknown", 2, "不明", "ふめい", "unknown")
        ]);
        // Only one item is saved: browsing must still expose every valid exported catalog item.
        var nouns = new NounData(Workspace.CreatePaths());
        nouns.AddToVocab(nouns.Words.Single(word => word.Id == 2));
        var progress = Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json");
        var before = File.ReadAllBytes(progress);
        await OpenAsync("/");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Explore audio", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(App.Url + "/explore/audio");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Explore audio", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Table, new() { Name = "Word audio" })
            .GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Word", "Audio"]);
        await SelectedAsync("All");
        await IdentitiesAsync("Nouns/2", "Nouns/9", "Verbs/2", "Adjectives/2", "Adverbs/1", "Expressions/1", "Words/2");
        foreach (var (dataset, id, text, group) in new[]
        {
            ("Nouns", 2, "返事", "Nouns"), ("Verbs", 2, "試す", "Verbs"),
            ("Adjectives", 2, "新しい", "Adjectives"), ("Words", 2, "挨拶", "Words & expressions")
        })
        {
            await Expect(Row(dataset, id)).ToContainTextAsync(text);
            await Expect(Row(dataset, id)).ToContainTextAsync(group);
        }
        await Expect(Row("Words", 2)).ToContainTextAsync("あいさつ");
        await Expect(Row("Words", 2)).ToContainTextAsync("Hello");

        await Search.FillAsync(" he ");
        await IdentitiesAsync("Nouns/2", "Nouns/9", "Words/2");
        await Search.FillAsync("HEL");
        await IdentitiesAsync("Nouns/9", "Words/2");
        await Category("Words & expressions").ClickAsync();
        await SelectedAsync("Words & expressions");
        await Expect(Search).ToHaveValueAsync("HEL");
        await IdentitiesAsync("Words/2");
        await Category("Verbs").ClickAsync();
        await SelectedAsync("Verbs");
        await Expect(Page.GetByText("No matching audio.", new() { Exact = true })).ToBeVisibleAsync();
        await Search.FillAsync("test");
        await IdentitiesAsync("Verbs/2");
        await Category("All").ClickAsync();
        await Search.FillAsync("あい");
        await IdentitiesAsync("Words/2");
        await Search.FillAsync("挨");
        await IdentitiesAsync("Words/2");
        await Search.FillAsync("llo");
        await Expect(Page.GetByText("No matching audio.", new() { Exact = true })).ToBeVisibleAsync();
        await Search.FillAsync(" ");
        await Expect(Rows).ToHaveCountAsync(7);
        await Search.FillAsync("");
        await Expect(Rows).ToHaveCountAsync(7);
        Assert.Equal(before, File.ReadAllBytes(progress));
    }

    [Fact]
    public async Task Native_players_play_both_codecs_stop_when_removed_and_virtualize_without_phone_overflow()
    {
        Seed(Enumerable.Range(0, 40).Reverse()
            .Select(id => new Clip("Nouns", id, $"音{id}", $"おと{id}", $"sound {id}")).ToArray());
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Page.SetViewportSizeAsync(320, 900);
        await OpenAsync("/explore/audio");
        await Expect(Row("Nouns", 0)).ToBeVisibleAsync();
        await AssertBottomGapAsync();
        var initialListHeight = (await Page.Locator(".explore-audio-scroll").BoundingBoxAsync())!.Height;
        await Page.SetViewportSizeAsync(320, 1100);
        await AssertBottomGapAsync();
        Assert.InRange((await Page.Locator(".explore-audio-scroll").BoundingBoxAsync())!.Height - initialListHeight, 199, 201);
        await Page.SetViewportSizeAsync(1280, 1100);
        await AssertBottomGapAsync();
        // Exercise the exact Android host stylesheet without claiming native WebView evidence.
        var mobileStyles = await Page.AddStyleTagAsync(new() { Content = File.ReadAllText(Path.Combine(
            TestWorkspace.RepositoryRoot, "RealJapanese", "RealJapanese.Mobile", "wwwroot", "mobile.css")) });
        await AssertBottomGapAsync(0);
        await Page.SetViewportSizeAsync(320, 900);
        await AssertBottomGapAsync(0);
        await Page.EvaluateAsync("document.documentElement.classList.add('android-keyboard-open')");
        await AssertBottomGapAsync(0);
        await Page.EvaluateAsync("document.documentElement.classList.remove('android-keyboard-open')");
        await AssertBottomGapAsync(0);
        await mobileStyles.EvaluateAsync("element => element.remove()");
        await AssertBottomGapAsync();
        Assert.InRange(await Rows.CountAsync(), 1, 39);
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"));
        Assert.True(await Rows.EvaluateAllAsync<bool>("""
            rows => rows.every(row => {
                const cells = row.querySelectorAll('[role=cell],td');
                return cells.length === 2 && cells[1].children.length === 1
                    && cells[1].firstElementChild.tagName === 'AUDIO';
            })
            """));
        await PlayAndRememberAsync(Row("Nouns", 0), "audio/mpeg");
        await Search.FillAsync("sound 1");
        await Expect(Row("Nouns", 0)).ToHaveCountAsync(0);
        await ReleasedAsync();
        await PlayAndRememberAsync(Row("Nouns", 1), "audio/ogg");
        await Category("Verbs").ClickAsync();
        await Expect(Rows).ToHaveCountAsync(0);
        await ReleasedAsync();
        await Search.FillAsync("");
        await Category("All").ClickAsync();
        await Expect(Row("Nouns", 0)).ToBeVisibleAsync();
        await PlayAndRememberAsync(Row("Nouns", 0), "audio/mpeg");
        await Page.Locator(".explore-audio-scroll").EvaluateAsync("element => element.scrollTop = element.scrollHeight");
        await Expect(Row("Nouns", 39)).ToBeVisibleAsync();
        await ReleasedAsync();
        await PlayAndRememberAsync(Row("Nouns", 39), "audio/ogg");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await Expect(Rows).ToHaveCountAsync(0);
        await ReleasedAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Empty_catalog_and_empty_category_are_distinct_from_an_unmatched_search()
    {
        Seed([new("Nouns", 1, "音", "おと", "sound")]);
        await OpenAsync("/explore/audio");
        await Search.FillAsync("absent");
        await Expect(Page.GetByText("No matching audio.", new() { Exact = true })).ToBeVisibleAsync();
        await Category("Adjectives").ClickAsync();
        await Expect(Page.GetByText("No audio for this category.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("No matching audio.", new() { Exact = true })).ToHaveCountAsync(0);

        // Restart the private app so its immutable catalog snapshot observes an empty manifest.
        await App.DisposeAsync();
        Seed([]);
        await App.StartAsync();
        await OpenAsync("/explore/audio");
        await Expect(Page.GetByText("No audio.", new() { Exact = true })).ToBeVisibleAsync();
        await Search.FillAsync("absent");
        await Expect(Page.GetByText("No audio.", new() { Exact = true })).ToBeVisibleAsync();
    }

    private ILocator Category(string name) => Page.GetByRole(AriaRole.Group, new() { Name = "Audio categories" })
        .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    private ILocator Row(string dataset, int id) => Page.Locator($".explore-audio-row[data-dataset='{dataset}'][data-word-id='{id}']");

    private async Task SelectedAsync(string selected)
    {
        foreach (var category in Categories)
            await Expect(Category(category)).ToHaveAttributeAsync("aria-pressed", category == selected ? "true" : "false");
    }

    private async Task IdentitiesAsync(params string[] expected)
    {
        await Expect(Rows).ToHaveCountAsync(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var identity = expected[index].Split('/');
            await Expect(Rows.Nth(index)).ToHaveAttributeAsync("data-dataset", identity[0]);
            await Expect(Rows.Nth(index)).ToHaveAttributeAsync("data-word-id", identity[1]);
        }
        Assert.Equal(expected, await Rows.EvaluateAllAsync<string[]>(
            "rows => rows.map(row => row.dataset.dataset + '/' + row.dataset.wordId)"));
    }

    private async Task PlayAndRememberAsync(ILocator row, string mime)
    {
        var audio = row.Locator("audio");
        await Expect(audio).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("^data:" + mime + ";base64,"));
        await Expect(audio).ToHaveAttributeAsync("controls", "");
        await Expect(audio).ToHaveAccessibleNameAsync("Audio for 音" + await row.GetAttributeAsync("data-word-id"));
        await audio.EvaluateAsync("""
            async audio => {
                window.previousAudio = audio;
                await new Promise((resolve, reject) => {
                    audio.addEventListener('playing', resolve, { once: true });
                    audio.play().catch(reject);
                });
            }
            """);
        Assert.True(await audio.EvaluateAsync<bool>("audio => audio.readyState >= 2 && !audio.error"));
    }

    private async Task ReleasedAsync()
    {
        await Page.WaitForFunctionAsync("window.previousAudio.paused && !window.previousAudio.hasAttribute('src')");
    }

    private async Task AssertBottomGapAsync(double expectedGap = 50)
    {
        var table = Page.GetByRole(AriaRole.Table, new() { Name = "Word audio" });
        await Expect(table).ToBeVisibleAsync();
        var bounds = (await table.BoundingBoxAsync())!;
        var navigationSpace = await Page.EvaluateAsync<double>("""
            () => innerWidth <= 640.98 ? document.querySelector('.sidebar').getBoundingClientRect().height : 0
            """);
        Assert.InRange(Page.ViewportSize!.Height - bounds.Y - bounds.Height - navigationSpace, expectedGap - 1, expectedGap + 1);
    }

    private void Seed(Clip[] clips)
    {
        foreach (var group in clips.Where(clip => clip.Dataset != "Unknown").GroupBy(clip => clip.Dataset))
        {
            var words = group.Select(clip => new
            {
                id = clip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                japanese = clip.Japanese, kana = clip.Kana, english = clip.English,
                type = group.Key == "Verbs" ? "u" : "i"
            });
            File.WriteAllText(Path.Combine(Workspace.CatalogRoot, group.Key, group.Key + ".json"), JsonSerializer.Serialize(words));
        }
        var audioRoot = Path.Combine(Workspace.CatalogRoot, "Audio");
        Directory.CreateDirectory(audioRoot);
        var fixtureRoot = Path.Combine(TestWorkspace.RepositoryRoot, "tests", "fixtures", "listening-audio");
        var mp3 = Directory.GetFiles(fixtureRoot, "*.mp3", SearchOption.AllDirectories).Single();
        var opus = Directory.GetFiles(fixtureRoot, "*.opus", SearchOption.AllDirectories).Single();
        var manifest = clips.Select(clip =>
        {
            var source = clip.Id % 2 == 0 ? mp3 : opus;
            var file = $"{clip.Dataset}/{clip.Id}/clip{Path.GetExtension(source)}";
            var target = Path.Combine(audioRoot, file);
            if (clip.Available)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
            return new { word = new { dataset = clip.Dataset, id = clip.Id,
                japanese = clip.Stale ? "changed snapshot" : clip.Japanese, kana = clip.Kana }, file };
        }).ToArray();
        File.WriteAllText(Path.Combine(audioRoot, "manifest.json"), JsonSerializer.Serialize(manifest));
    }

    private sealed record Clip(string Dataset, int Id, string Japanese, string Kana, string English,
        bool Available = true, bool Stale = false);
}
