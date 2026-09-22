using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks merge counts, stale work from a second circuit, and recovery after a host restart.</summary>
public sealed class SyncRecoveryBrowserTests : SyncBrowserTest
{
    [Fact]
    public async Task Mode_counts_stale_preview_refresh_and_recovery_survive_restart()
    {
        using var senderData = new TestWorkspace();
        var receiverWords = new WordData(Workspace.CreatePaths());
        var words = receiverWords.Words.Take(4).ToArray();
        receiverWords.AddToVocab(words[0]);
        receiverWords.AddToTraining(words[1]);
        var senderWords = new WordData(senderData.CreatePaths());
        senderWords.AddToVocab(words[1]);
        senderWords.AddToRehearsing(words[2]);
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        await Pair(senderPage);
        var dialog = Page.GetByRole(AriaRole.Dialog);
        var row = dialog.GetByRole(AriaRole.Row).Filter(new() { Has = Page.GetByRole(AriaRole.Rowheader, new() { Name = "Words", Exact = true }) });
        await Expect(row.GetByRole(AriaRole.Cell)).ToHaveTextAsync(["1", "0", "0", "1"]);
        await Page.GetByLabel("How should this progress be combined?").SelectOptionAsync("MergeUseIncoming");
        await Expect(row.GetByRole(AriaRole.Cell)).ToHaveTextAsync(["1", "1", "0", "1"]);
        await Page.GetByLabel("How should this progress be combined?").SelectOptionAsync("Replace");
        await Expect(row.GetByRole(AriaRole.Cell)).ToHaveTextAsync(["1", "1", "1", "1"]);

        // A second circuit changes progress after the first circuit's preview was created.
        var otherTab = await Browser.NewPageAsync();
        await NavigateAsync(otherTab, App.Url + "/words");
        var training = otherTab.Locator(".category-column").Nth(2);
        await training.GetByRole(AriaRole.Searchbox).FillAsync(words[3].English);
        await training.Locator("button.list-group-item").Filter(new() { Has = otherTab.Locator(".fw-bold").GetByText(words[3].English, new() { Exact = true }) }).ClickAsync();
        await Expect(training.Locator("button.active")).ToHaveCountAsync(1);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Apply this preview" }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("changed");
        Assert.Contains(words[3].Id, new WordData(Workspace.CreatePaths()).TrainingWordIds);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Refresh preview" }).ClickAsync();
        await Expect(row.GetByRole(AriaRole.Cell)).ToHaveTextAsync(["1", "1", "2", "1"]);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Apply this preview" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        var saved = new WordData(Workspace.CreatePaths());
        Assert.Equal([words[1].Id], saved.VocabWordIds);
        Assert.Equal([words[2].Id], saved.RehearsingWordIds);
        Assert.Empty(saved.TrainingWordIds);

        await App.DisposeAsync();
        await App.StartAsync();
        await OpenAsync("/sync");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Preview recovery copy" }).ClickAsync();
            await Expect(dialog).ToBeVisibleAsync();
            await dialog.GetByRole(AriaRole.Button, new() { Name = attempt == 0 ? "Cancel" : "Apply this preview", Exact = true }).ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            if (attempt == 0) Assert.Equal([words[1].Id], new WordData(Workspace.CreatePaths()).VocabWordIds);
        }
        saved = new WordData(Workspace.CreatePaths());
        Assert.Equal([words[0].Id], saved.VocabWordIds);
        Assert.Equal(new[] { words[1].Id, words[3].Id }.Order(), saved.TrainingWordIds.Order());
        Assert.Empty(saved.RehearsingWordIds);
    }

}
