using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks invalid endpoints and mismatched catalogs produce errors and allow corrected transfers.</summary>
public sealed class SyncErrorBrowserTests : SyncBrowserTest
{
    [Fact]
    public async Task Invalid_endpoints_leave_controls_usable_for_corrected_transfer()
    {
        using var senderData = new TestWorkspace();
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        foreach (var (address, port) in new[] { ("not-an-ip", "12345"), ("8.8.8.8", "12345"), ("127.0.0.1", "0") })
        {
            await Page.GetByLabel("Other device's address").FillAsync(address);
            await Page.GetByLabel("Port", new() { Exact = true }).FillAsync(port);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("private IPv4");
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true })).ToBeEnabledAsync();
        }
        await Pair(senderPage);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Catalog_mismatch_rejects_transfer_and_corrected_sender_can_retry()
    {
        using var senderData = new TestWorkspace();
        var catalogFile = Path.Combine(senderData.CatalogRoot, "Words", "Words.json");
        var original = await File.ReadAllBytesAsync(catalogFile, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(catalogFile, "\n ", TestContext.Current.CancellationToken);
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        await Connect(senderPage);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Codes match", Exact = true }).ClickAsync();
        await senderPage.GetByRole(AriaRole.Button, new() { Name = "Codes match", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("catalog");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Apply this preview" })).ToHaveCountAsync(0);
        Assert.False(File.Exists(Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json")));
        await sender.DisposeAsync();
        await File.WriteAllBytesAsync(catalogFile, original, TestContext.Current.CancellationToken);
        await sender.StartAsync();
        await NavigateAsync(senderPage, sender.Url + "/sync");
        await senderPage.GetByText("Manual", new() { Exact = true }).ClickAsync();
        await Expect(senderPage.GetByLabel("Port", new() { Exact = true })).ToBeVisibleAsync();
        await Pair(senderPage);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
    }

}
