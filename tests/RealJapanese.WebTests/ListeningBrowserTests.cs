using System.Text.Json;
using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Exercises real exported MP3/Opus playback, replay, retry lifecycle and browser autoplay fallback.</summary>
public sealed class ListeningBrowserTests : BrowserTest
{
    [Fact]
    public async Task Exported_clips_play_once_per_question_replay_and_repeat_failures_without_changing_progress()
    {
        var answers = SeedExportedAdjectives();
        var progressPath = Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json");
        var before = File.ReadAllBytes(progressPath);
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await TrackPlaybackAsync();
        await OpenAsync("/adjectives");
        await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Listening" })
            .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(App.Url + "/adjectives/listening?category=known");
        await ReadyAsync(1);
        var firstSource = await Page.Locator("audio").GetAttributeAsync("src");
        var correct = answers[firstSource!];
        var choices = Page.GetByRole(AriaRole.Group, new() { Name = "English meanings" }).GetByRole(AriaRole.Button);
        await Expect(choices).ToHaveCountAsync(3);
        Assert.Equal(3, (await choices.AllTextContentsAsync()).Distinct().Count());
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Next question", Exact = true })).ToBeDisabledAsync();

        await Page.Locator("input[type=range]").FillAsync("1.2");
        await Expect(Page.Locator("audio")).ToHaveAttributeAsync("data-play-calls", "1");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Play clip", Exact = true }).ClickAsync();
        await ReadyAsync(2);
        var wrong = (await choices.AllTextContentsAsync()).First(text => text.Trim() != correct).Trim();
        await choices.Filter(new() { HasText = wrong }).First.ClickAsync();
        await Expect(Page.Locator(".alert")).ToContainTextAsync("This clip will repeat");
        await Expect(Page.Locator("audio")).ToHaveAttributeAsync("data-play-calls", "2");
        await Page.EvaluateAsync("window.previousAudio = document.querySelector('audio')");
        await NextAsync();
        await ReadyAsync(1);
        Assert.NotEqual(firstSource, await Page.Locator("audio").GetAttributeAsync("src"));
        Assert.True(await Page.EvaluateAsync<bool>("window.previousAudio.paused && !window.previousAudio.hasAttribute('src')"));
        await AnswerCorrectlyAsync(answers);
        await NextAsync();
        await ReadyAsync(1);
        await Expect(Page.Locator("audio")).ToHaveAttributeAsync("src", firstSource!);
        await Expect(Page.Locator("small.text-muted")).ToHaveTextAsync("1/1");
        await AnswerCorrectlyAsync(answers);
        await NextAsync();
        await ReadyAsync(1);
        await Expect(Page.Locator("small.text-muted")).ToHaveTextAsync("1/2");
        Assert.Equal(before, File.ReadAllBytes(progressPath));

        await Page.EvaluateAsync("window.previousAudio = document.querySelector('audio')");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to adjectives", Exact = true }).ClickAsync();
        await Expect(Page.Locator("audio")).ToHaveCountAsync(0);
        Assert.True(await Page.EvaluateAsync<bool>("window.previousAudio.paused && !window.previousAudio.hasAttribute('src')"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Blocked_autoplay_has_a_working_manual_play_button()
    {
        SeedExportedAdjectives();
        await TrackPlaybackAsync(blockFirst: true);
        await OpenAsync("/adjectives/listening?category=known");
        await Expect(Page.Locator(".listening-player [role=status]")).ToHaveTextAsync("Tap Play clip to enable audio in this browser.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Play clip", Exact = true }).ClickAsync();
        await ReadyAsync(2);
        await Expect(Page.Locator(".listening-player [role=status]")).ToBeEmptyAsync();
    }

    [Theory]
    [InlineData("words")]
    [InlineData("nouns")]
    [InlineData("verbs")]
    [InlineData("adjectives")]
    [InlineData("vocabulary")]
    public async Task Each_area_offers_listening_for_the_selected_list(string area)
    {
        await OpenAsync("/" + area);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Training", Exact = true }).ClickAsync();
        await Page.Locator(".practice-selector .card").Filter(new() { HasText = "Listening" })
            .GetByRole(AriaRole.Button, new() { Name = "Start practice" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/{area}/listening\\?category=training"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Nothing to listen to yet" })).ToBeVisibleAsync();
        await Expect(Page.Locator("audio")).ToHaveCountAsync(0);
    }

    private Task TrackPlaybackAsync(bool blockFirst = false) => Page.AddInitScriptAsync("""
        (() => {
            const nativePlay = HTMLMediaElement.prototype.play;
            let blocked = false;
            HTMLMediaElement.prototype.play = function () {
                this.dataset.playCalls = String(Number(this.dataset.playCalls || 0) + 1);
                delete this.dataset.played;
                if (BLOCK_FIRST && !blocked) {
                    blocked = true;
                    return Promise.reject(new DOMException('Test autoplay policy', 'NotAllowedError'));
                }
                this.addEventListener('playing', () => this.dataset.played = 'true', { once: true });
                return nativePlay.call(this);
            };
        })();
        """.Replace("BLOCK_FIRST", blockFirst ? "true" : "false"));

    private async Task ReadyAsync(int calls)
    {
        await Expect(Page.Locator("audio")).ToHaveAttributeAsync("data-play-calls", calls.ToString());
        await Expect(Page.Locator("audio")).ToHaveAttributeAsync("data-played", "true");
        Assert.True(await Page.Locator("audio").EvaluateAsync<bool>("audio => audio.readyState >= 2 && !audio.error"));
    }

    private async Task AnswerCorrectlyAsync(Dictionary<string, string> answers)
    {
        var source = await Page.Locator("audio").GetAttributeAsync("src");
        await Page.GetByRole(AriaRole.Group, new() { Name = "English meanings" })
            .GetByRole(AriaRole.Button, new() { Name = answers[source!], Exact = true }).ClickAsync();
        await Expect(Page.Locator(".alert")).ToHaveTextAsync("Correct!");
    }

    private Task NextAsync() => Page.GetByRole(AriaRole.Button, new() { Name = "Next question", Exact = true }).ClickAsync();

    private Dictionary<string, string> SeedExportedAdjectives()
    {
        var sourceRoot = Path.Combine(TestWorkspace.RepositoryRoot, "tests", "fixtures", "listening-audio");
        File.Copy(Path.Combine(sourceRoot, "Adjectives.json"), Path.Combine(Workspace.CatalogRoot, "Adjectives", "Adjectives.json"), overwrite: true);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(sourceRoot, "manifest.json")));
        var entries = manifest.RootElement.EnumerateArray()
            .Where(entry => entry.GetProperty("word").GetProperty("dataset").GetString() == "Adjectives")
            .GroupBy(entry => Path.GetExtension(entry.GetProperty("file").GetString()))
            .Where(group => group.Key is ".mp3" or ".opus").Select(group => group.First()).ToArray();
        Assert.Equal(2, entries.Length); // Fixed, real Audio Studio exports cover both codecs.
        var targetRoot = Path.Combine(Workspace.CatalogRoot, "Audio");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "manifest.json"), JsonSerializer.Serialize(entries));
        var data = new AdjectiveData(Workspace.CreatePaths());
        var result = new Dictionary<string, string>();
        foreach (var entry in entries)
        {
            var file = entry.GetProperty("file").GetString()!;
            var target = Path.Combine(targetRoot, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(sourceRoot, file), target);
            var word = data.Words.Single(word => word.Id == entry.GetProperty("word").GetProperty("id").GetInt32());
            data.AddToVocab(word);
            var mime = Path.GetExtension(file) == ".mp3" ? "audio/mpeg" : "audio/ogg";
            result.Add($"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(target))}", word.English.Trim());
        }
        return result;
    }
}
