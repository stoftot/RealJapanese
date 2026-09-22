using Microsoft.Playwright;
using RealJapanese.TestSupport;
using Repositories;

namespace RealJapanese.WebTests;

/// <summary>Provides a fresh browser, real app and private catalogs for each browser test, with bounded cleanup.</summary>
public abstract class BrowserTest : IAsyncLifetime
{
    protected TestWorkspace Workspace { get; } = new();
    protected WebApp App { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    private IPlaywright? playwright;

    public async ValueTask InitializeAsync()
    {
        App = new WebApp(Workspace);
        try
        {
            await App.StartAsync();
            playwright = await Playwright.CreateAsync();
            Browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            Page = await Browser.NewPageAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
            Page.SetDefaultTimeout(10_000);
        }
        catch { await DisposeAsync(); throw; }
    }

    protected WordData SeedWords(int count = 3)
    {
        var data = new WordData(Workspace.CreatePaths());
        var selected = data.Words.DistinctBy(word => word.English).DistinctBy(word => word.Kana).Take(count).ToArray();
        Assert.Equal(count, selected.Length);
        foreach (var word in selected) data.AddToVocab(word);
        return data;
    }

    protected async Task OpenAsync(string route)
    {
        await NavigateAsync(Page, App.Url + route);
    }

    protected static async Task NavigateAsync(IPage page, string url)
    {
        await page.GotoAsync(url);
        // Initial prerendered HTML is not interactive. Allow the startup scripts and
        // SignalR negotiation to finish before sending input; assertions still wait for UI results.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async ValueTask DisposeAsync()
    {
        var browser = Browser;
        Browser = null!;
        try
        {
            if (browser is not null) await browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            try { playwright?.Dispose(); }
            finally
            {
                playwright = null;
                try { if (App is not null) await App.DisposeAsync(); }
                finally { Workspace.Dispose(); }
            }
        }
    }
}
