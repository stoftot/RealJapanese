using Microsoft.Playwright;
using System.Net;
using System.Net.Sockets;
using RealJapanese.TestSupport;
using Repositories;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Checks the UI approval boundary, pairing cancellation and disposal when navigating away.</summary>
public sealed class SyncPairingBrowserTests : SyncBrowserTest
{
    [Fact]
    public async Task One_sided_approval_then_rejection_preserves_progress_and_can_retry()
    {
        using var senderData = new TestWorkspace();
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        await Connect(senderPage);
        await senderPage.GetByRole(AriaRole.Button, new() { Name = "Codes match", Exact = true }).ClickAsync();
        await Expect(senderPage.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Waiting for the other device");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Apply this preview" })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Codes do not match", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
        await Expect(senderPage.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
        await Pair(senderPage);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        Assert.False(File.Exists(Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json")));
    }

    [Theory]
    [InlineData("Close dialog")]
    [InlineData("Escape")]
    public async Task Pairing_dismissal_and_leaving_sender_stop_sessions_but_allow_retry(string dismissal)
    {
        using var senderData = new TestWorkspace();
        await using var sender = new WebApp(senderData);
        await sender.StartAsync();
        var senderPage = await Browser.NewPageAsync();
        await OpenPeers(senderPage, sender);
        await Connect(senderPage);
        if (dismissal == "Escape") await Page.Keyboard.PressAsync("Escape");
        else await Page.GetByRole(AriaRole.Button, new() { Name = "Close dialog" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
        await Expect(senderPage.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();

        await StartSharing(senderPage);
        var port = senderPage.Locator("dd").Nth(1);
        await Expect(port).ToBeVisibleAsync();
        var oldPort = await port.InnerTextAsync();
        await senderPage.GetByRole(AriaRole.Link, new() { Name = "← Home", Exact = true }).ClickAsync();
        await Expect(senderPage.GetByRole(AriaRole.Heading, new() { Name = "Sync progress", Exact = true })).ToHaveCountAsync(0);
        // Bind without listening: prove the original listener was released, then keep
        // another parallel test from acquiring its port before the rejection assertion.
        using var reservedPort = await ReserveReleasedPort(int.Parse(oldPort));
        await Page.GetByLabel("Port", new() { Exact = true }).FillAsync(oldPort);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Connection ended");
        await NavigateAsync(senderPage, sender.Url + "/sync");
        await senderPage.GetByText("Manual", new() { Exact = true }).ClickAsync();
        await Expect(senderPage.GetByLabel("Port", new() { Exact = true })).ToBeVisibleAsync();
        await Pair(senderPage);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        Assert.False(File.Exists(Path.Combine(Workspace.CreatePaths().ProgressRoot, "Progress.json")));
    }

    private static async Task<Socket> ReserveReleasedPort(int port)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ExclusiveAddressUse = true
            };
            try { socket.Bind(new IPEndPoint(IPAddress.Any, port)); return socket; }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                socket.Dispose();
                await Task.Delay(25, timeout.Token);
            }
            catch { socket.Dispose(); throw; }
        }
    }

}
