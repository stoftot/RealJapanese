using Microsoft.Playwright;
using System.Text.Json;
using Repositories.Genki;
using RealJapanese.TestSupport;
using static Microsoft.Playwright.Assertions;

namespace RealJapanese.WebTests;

/// <summary>Exercises the local Studio setup, bounded model-free scans, queue filters and publication review in Chromium.</summary>
public sealed class GenkiStudioBrowserTests : IAsyncLifetime
{
    private readonly TestWorkspace workspace = new();
    private StudioWebApp app = null!;
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IPage page = null!;

    public async ValueTask InitializeAsync()
    {
        app = new StudioWebApp(workspace);
        try
        {
            await app.StartAsync();
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
            page.SetDefaultTimeout(10_000);
        }
        catch { await DisposeAsync(); throw; }
    }

    [Fact]
    public async Task Setup_small_scans_queue_filters_and_cancelled_publication_are_isolated()
    {
        var browserErrors = new List<string>();
        page.PageError += (_, error) => browserErrors.Add(error);
        await page.GotoAsync(app.Url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your generation workspace" })).ToBeVisibleAsync();

        // Save an edited local state path, then reload the real app to prove the configuration is durable.
        var savedStateRoot = Path.Combine(workspace.Root, "saved-studio-state");
        await page.GetByText("Show and edit configuration", new() { Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("Data root", new() { Exact = true })).ToHaveValueAsync(workspace.CatalogRoot);
        await page.GetByLabel("State root", new() { Exact = true }).FillAsync(savedStateRoot);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save configuration", Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("State root", new() { Exact = true })).ToHaveValueAsync(savedStateRoot);
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.GetByText("Show and edit configuration", new() { Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("State root", new() { Exact = true })).ToHaveValueAsync(savedStateRoot);

        var nouns = GenkiVocabulary.Load(workspace.CatalogRoot).Entries
            .Where(entry => entry.Ref.WordType == "noun").Take(2).ToArray();
        Assert.Equal(2, nouns.Length);

        await page.GetByRole(AriaRole.Combobox, new() { Name = "Lesson", Exact = true }).SelectOptionAsync("1");
        await page.GetByPlaceholder("Search pattern, grammar or ID").FillAsync("g01-01-noun-predicate");
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Include g01-01-noun-predicate", Exact = true }).CheckAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Use all vocabulary", Exact = true }).UncheckAsync();
        foreach (var noun in nouns)
        {
            await page.GetByPlaceholder("Search Japanese or English")
                .FillAsync($"{noun.Ref.WordType}:{noun.Ref.Id}");
            var row = page.Locator(".word-option").Filter(new() { HasText = noun.Word.English });
            await Expect(row).ToHaveCountAsync(1);
            await row.Locator("input[type=checkbox]").CheckAsync();
        }
        await page.GetByPlaceholder("Search Japanese or English").FillAsync("");
        await Expect(page.Locator(".selection-count")).ToContainTextAsync("2 selected");
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Action", Exact = true }).SelectOptionAsync("scan");
        await page.GetByLabel("Candidate limit", new() { Exact = true }).FillAsync("2");
        await page.GetByLabel("Time limit · minutes", new() { Exact = true }).FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Queue run" }).ClickAsync();

        var patternRow = page.Locator(".schema-table tbody tr").Filter(new() { HasText = "g01-01-noun-predicate" });
        await Expect(patternRow).ToContainTextAsync("At least 2 visited", new() { Timeout = 30_000 });
        await Expect(page.Locator(".job-card").First).ToContainTextAsync("Partial scan saved", new() { Timeout = 30_000 });

        // A larger bounded scan recounts and finishes the same small scope.
        await page.GetByLabel("Candidate limit", new() { Exact = true }).FillAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Queue run" }).ClickAsync();
        await Expect(patternRow).ToContainTextAsync("Exact · 6 enumerated · 6 unseen", new() { Timeout = 30_000 });
        await Expect(page.Locator(".job-card").First).ToContainTextAsync("Coverage counted", new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Combobox, new() { Name = "Queue status", Exact = true }).SelectOptionAsync("Not queued");
        await Expect(page.GetByRole(AriaRole.Checkbox, new() { Name = "Include g01-01-noun-predicate", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Queue status", Exact = true }).SelectOptionAsync("Queued");
        await Expect(page.GetByText("No generation patterns match these filters.", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Queue status", Exact = true }).SelectOptionAsync("All states");

        Assert.False(File.Exists(Path.Combine(savedStateRoot, "questions", "unused.json")));
        Assert.False(Directory.Exists(Path.Combine(savedStateRoot, "questions")));
        Assert.False(File.Exists(app.PublishPath));

        // Review shows the exact destination. Cancelling must leave a pre-existing isolated bank untouched.
        const string originalBank = "test bank remains unchanged\n";
        await File.WriteAllTextAsync(app.PublishPath, originalBank);
        await page.GetByRole(AriaRole.Button, new() { Name = "Review publication" }).ClickAsync();
        var confirmation = page.Locator("[role=alertdialog]");
        await Expect(confirmation).ToContainTextAsync(app.PublishPath);
        await Expect(confirmation).ToContainTextAsync("current bank stays unchanged until you confirm");
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(confirmation).ToHaveCountAsync(0);
        Assert.Equal(originalBank, await File.ReadAllTextAsync(app.PublishPath));
        await Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        Assert.Empty(browserErrors);
    }

    [Fact]
    public async Task Review_load_more_keeps_every_checkpoint_across_three_pages()
    {
        var directory = Path.Combine(app.StateRoot, "questions");
        Directory.CreateDirectory(directory);
        for (var index = 0; index < 61; index++)
        {
            var id = $"review-fixture-{index:000}";
            var state = new { candidateId = id, inputFingerprint = "fixture", schemaId = "g01-01-noun-predicate",
                provenance = "fixture", status = "needs-review", reason = "Controlled review fixture" };
            await File.WriteAllTextAsync(Path.Combine(directory, GenkiJson.Hash(id) + ".json"),
                JsonSerializer.Serialize(state), TestContext.Current.CancellationToken);
        }
        await page.GotoAsync(app.Url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.GetByRole(AriaRole.Button, new() { Name = "Load questions", Exact = true }).ClickAsync();
        await Expect(page.Locator(".review-row")).ToHaveCountAsync(25);
        var more = page.GetByRole(AriaRole.Button, new() { Name = "Load more", Exact = true });
        await more.ClickAsync();
        await Expect(page.Locator(".review-row")).ToHaveCountAsync(50);
        await more.ClickAsync();
        await Expect(page.Locator(".review-row")).ToHaveCountAsync(61);
        await Expect(more).ToBeDisabledAsync();
        var identities = await page.Locator(".review-top .subtle").AllInnerTextsAsync();
        Assert.Equal(61, identities.Distinct().Count());
        Assert.False(File.Exists(app.PublishPath));
    }

    [Fact]
    public async Task Studio_rejects_untrusted_host_and_origin_headers()
    {
        using var client = new HttpClient();
        using var badHost = new HttpRequestMessage(HttpMethod.Get, app.Url);
        badHost.Headers.Host = "studio.example.test";
        using var hostResponse = await client.SendAsync(badHost);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, hostResponse.StatusCode);

        using var badOrigin = new HttpRequestMessage(HttpMethod.Get, app.Url);
        badOrigin.Headers.Add("Origin", "http://studio.example.test");
        using var originResponse = await client.SendAsync(badOrigin);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, originResponse.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (browser is not null) await browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        finally
        {
            try { playwright?.Dispose(); }
            finally
            {
                try { if (app is not null) await app.DisposeAsync(); }
                finally { workspace.Dispose(); }
            }
        }
    }
}
