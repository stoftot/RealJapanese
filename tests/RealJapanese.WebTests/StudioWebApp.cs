using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using RealJapanese.TestSupport;

namespace RealJapanese.WebTests;

/// <summary>Runs Genki Studio with a private config and generated-state folder for a browser test.</summary>
public sealed class StudioWebApp(TestWorkspace workspace) : IAsyncDisposable
{
    private Process? process;
    private readonly ConcurrentQueue<string> output = new();

    public string Url { get; private set; } = "";
    public string ConfigurationFile { get; } = Path.Combine(workspace.Root, "genki.local.json");
    public string StateRoot { get; } = Path.Combine(workspace.Root, "studio-state");
    public string PublishPath { get; } = Path.Combine(workspace.Root, "published-questions.jsonl");

    public async Task StartAsync()
    {
        if (process is not null) throw new InvalidOperationException("Stop the existing Studio host before restarting it.");
        Directory.CreateDirectory(StateRoot);
        await File.WriteAllTextAsync(ConfigurationFile, JsonSerializer.Serialize(new
        {
            dataRoot = workspace.CatalogRoot,
            stateRoot = StateRoot,
            publishPath = PublishPath,
            models = new { modelsDirectory = "", maxTokens = 2048 },
            batch = new { maxAttempts = 1, tagGroupSize = 8, alternativeLimit = 1, delayMilliseconds = 0 }
        }));

        output.Clear();
        var projectDirectory = Path.Combine(TestWorkspace.RepositoryRoot, "RealJapanese", "Genki.Studio");
        var assembly = Path.Combine(AppContext.BaseDirectory, "studio-host", "Genki.Studio.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["GenkiStudio__ConfigFile"] = ConfigurationFile;
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";

        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        process = new Process { StartInfo = start, EnableRaisingEvents = true };
        void Record(object sender, DataReceivedEventArgs args)
        {
            if (args.Data is not { } line) return;
            output.Enqueue(line);
            while (output.Count > 80) output.TryDequeue(out _);
            const string marker = "Now listening on: ";
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0) ready.TrySetResult(line[(index + marker.Length)..].Trim());
        }
        process.OutputDataReceived += Record;
        process.ErrorDataReceived += Record;
        process.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("Studio host exited: " + string.Join('\n', output)));
        try { process.Start(); }
        catch { process.Dispose(); process = null; throw; }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try { Url = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (Exception ex)
        {
            await DisposeAsync();
            throw new InvalidOperationException("Genki Studio did not start. " + string.Join('\n', output), ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var hostedProcess = process;
        process = null;
        if (hostedProcess is null) return;
        try
        {
            try { if (!hostedProcess.HasExited) hostedProcess.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (hostedProcess.HasExited) { }
            await hostedProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { hostedProcess.Dispose(); }
    }
}
