using System.Collections.Concurrent;
using System.Diagnostics;
using RealJapanese.TestSupport;

namespace RealJapanese.WebTests;

/// <summary>Runs an isolated copy of the real web host on an OS-assigned port with disposable progress.</summary>
public sealed class WebApp(TestWorkspace workspace, string progressName = "progress") : IAsyncDisposable
{
    private Process? process;
    private readonly ConcurrentQueue<string> output = new();
    public string Url { get; private set; } = "";

    public async Task StartAsync()
    {
        if (process is not null) throw new InvalidOperationException("Stop the existing test host before restarting it.");
        output.Clear();
        var assembly = Path.Combine(AppContext.BaseDirectory, "web-host", "RealJapanese.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(TestWorkspace.RepositoryRoot, "RealJapanese", "RealJapanese"),
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        start.Environment["StudyData__CatalogRoot"] = workspace.CatalogRoot;
        start.Environment["StudyData__ProgressRoot"] = workspace.CreatePaths(progressName).ProgressRoot;
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
        process.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("Web host exited: " + string.Join('\n', output)));
        try { process.Start(); }
        catch { process.Dispose(); process = null; throw; }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try { Url = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (Exception ex)
        {
            await DisposeAsync();
            throw new InvalidOperationException("Web host did not start. " + string.Join('\n', output), ex);
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
