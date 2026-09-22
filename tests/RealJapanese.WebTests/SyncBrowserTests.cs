using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Connects two real web peers using the public pairing and approval controls.</summary>
public abstract class SyncBrowserTest : BrowserTest
{
    protected async Task OpenPeers(IPage senderPage, WebApp sender)
    {
        await OpenAsync("/sync");
        await NavigateAsync(senderPage, sender.Url + "/sync");
        await Page.GetByText("Manual", new() { Exact = true }).ClickAsync();
        await senderPage.GetByText("Manual", new() { Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Port", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(senderPage.GetByLabel("Port", new() { Exact = true })).ToBeVisibleAsync();
    }

    protected async Task Connect(IPage senderPage)
    {
        await StartSharing(senderPage);
        var port = senderPage.Locator("dd").Nth(1);
        await Expect(port).ToBeVisibleAsync();
        await Page.GetByLabel("Other device's address").FillAsync("127.0.0.1");
        await Page.GetByLabel("Port", new() { Exact = true }).FillAsync(await port.InnerTextAsync());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();
        var code = Page.GetByLabel("Pairing code");
        await Expect(code).ToBeVisibleAsync();
        await Expect(senderPage.GetByLabel("Pairing code")).ToHaveTextAsync(await code.InnerTextAsync());
        await Expect(Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
    }

    protected static async Task StartSharing(IPage page)
    {
        // A denied peer does not consume the share session; explicitly renew it for the next attempt.
        var stop = page.GetByRole(AriaRole.Button, new() { Name = "Stop sharing", Exact = true });
        if (await stop.IsVisibleAsync()) await stop.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Share progress", Exact = true }).ClickAsync();
    }

    protected async Task Pair(IPage senderPage)
    {
        await Connect(senderPage);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Codes match", Exact = true }).ClickAsync();
        await senderPage.GetByRole(AriaRole.Button, new() { Name = "Codes match", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Apply this preview" })).ToBeVisibleAsync();
        await Expect(senderPage.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
    }
}
