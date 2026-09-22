using Bunit;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;

namespace RealJapanese.ComponentTests;

/// <summary>Verifies SyncDialog dismissal gating and its browser-module attach, open, and detach lifecycle.</summary>
public sealed class SyncDialogComponentTests
{
    [Fact]
    public void CloseInvokesDismissOnlyWhileDialogIsOpen()
    {
        using var test = new ComponentTestContext();
        test.Context.JSInterop.SetupModule("./_content/RealJapanese.UI/js/sync-dialog.js");
        var dismissals = 0;
        var cut = test.Context.Render<SyncDialog>(parameters => parameters
            .Add(component => component.Open, false)
            .Add(component => component.Title, "Review progress")
            .Add(component => component.OnDismiss, EventCallback.Factory.Create(this, () => dismissals++)));

        cut.Find("button[aria-label='Close dialog']").Click();
        Assert.Equal(0, dismissals);

        cut.Render(parameters => parameters
            .Add(component => component.Open, true)
            .Add(component => component.Title, "Review progress")
            .Add(component => component.OnDismiss, EventCallback.Factory.Create(this, () => dismissals++)));
        cut.Find("button[aria-label='Close dialog']").Click();

        Assert.Equal(1, dismissals);
    }

    [Fact]
    public async Task BrowserDismissalRejectsAStaleOpenGeneration()
    {
        using var test = new ComponentTestContext();
        var module = test.Context.JSInterop.SetupModule("./_content/RealJapanese.UI/js/sync-dialog.js");
        var dismissals = 0;
        var cut = test.Context.Render<SyncDialog>(parameters => parameters
            .Add(component => component.Open, true)
            .Add(component => component.Title, "Pairing")
            .Add(component => component.OnDismiss, EventCallback.Factory.Create(this, () => dismissals++)));
        cut.WaitForAssertion(() => Assert.Contains(module.Invocations, invocation => invocation.Identifier == "setOpen"));
        var firstGeneration = (int)module.Invocations.Last(invocation => invocation.Identifier == "setOpen").Arguments[2]!;

        cut.Render(parameters => parameters.Add(component => component.Open, false));
        cut.Render(parameters => parameters.Add(component => component.Open, true));
        cut.WaitForAssertion(() => Assert.True(module.Invocations.Count(invocation => invocation.Identifier == "setOpen") >= 3));
        var currentGeneration = (int)module.Invocations.Last(invocation => invocation.Identifier == "setOpen").Arguments[2]!;

        // Native close/cancel events can arrive late; only the current open generation may dismiss UI state.
        await cut.Instance.DismissFromBrowser(firstGeneration);
        await cut.Instance.DismissFromBrowser(currentGeneration);

        Assert.Equal(1, dismissals);
        await cut.Instance.DisposeAsync();
        Assert.Contains(module.Invocations, invocation => invocation.Identifier == "detach");
    }
}
