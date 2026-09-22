using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks all browser preview dismissal methods preserve saved progress and permit another transfer.</summary>
public sealed class SyncPreviewBrowserTests : SyncBrowserTest
{
    [Theory]
    [InlineData("Cancel")]
    [InlineData("Close dialog")]
    [InlineData("Escape")]
    [InlineData("Backdrop")]
    public async Task Dismissed_preview_preserves_progress_and_allows_fresh_transfer(string method)
    {
        using var senderData = new TestWorkspace();
        var senderWords = new WordData(senderData.CreatePaths());
        senderWords.AddToVocab(senderWords.Words.First());
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Pair(senderPage);
            var dialog = Page.GetByRole(AriaRole.Dialog);
            if (method == "Escape") await Page.Keyboard.PressAsync("Escape");
            else if (method == "Backdrop") await Page.Mouse.ClickAsync(2, 2);
            else await dialog.GetByRole(AriaRole.Button, new() { Name = method, Exact = true }).ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            Assert.Empty(new WordData(Workspace.CreatePaths()).VocabWords);
            Assert.False(File.Exists(Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json")));
        }
    }

}
