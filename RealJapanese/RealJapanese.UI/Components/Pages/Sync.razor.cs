using System.Net.Sockets;
using Microsoft.AspNetCore.Components;
using Repositories.Sync;

namespace RealJapanese.Components.Pages;

public partial class Sync
{
    private LocalProgressTransferSession? sharing;
    private readonly CancellationTokenSource lifetime = new();
    private Task? refreshTask;
    private string address = "";
    private int port;
    private string pairingCode = "";
    private bool busy;
    private string? message;
    private string? error;
    private byte[]? received;
    private ImportMode mode = ImportMode.MergeKeepLocal;
    private ImportPreview? preview;

    protected override void OnInitialized() => refreshTask = RefreshStatus();

    private async Task RefreshStatus()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                if (sharing is not null) await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) { }
    }

    private Task StartSharing() => Run(async () =>
    {
        await StopSharing();
        sharing = LocalProgressTransfer.Start(ProgressSync.ExportSnapshot());
        if (!sharing.Addresses.Any(value => !value.StartsWith("127.")))
        {
            await StopSharing();
            throw new InvalidOperationException("No private network address was found. Connect this device to Wi-Fi and try again.");
        }
    });

    private async Task StopSharing()
    {
        if (sharing is not null) await sharing.DisposeAsync();
        sharing = null;
    }

    private Task Receive() => Run(async () =>
    {
        CancelPreview();
        received = await LocalProgressTransfer.ReceiveAsync(address.Trim(), port,
            pairingCode.Replace(" ", "").Replace("-", "").Trim(), lifetime.Token);
        pairingCode = "";
        preview = ProgressSync.PreviewSnapshot(received, mode);
        message = "Progress received. Review the preview below; your progress has not changed.";
    });

    private Task RefreshPreview() => Run(() =>
    {
        preview = null;
        if (received is not null) preview = ProgressSync.PreviewSnapshot(received, mode);
        return Task.CompletedTask;
    });

    private Task PreviewRecovery() => Run(() =>
    {
        CancelPreview();
        preview = ProgressSync.PreviewRecovery();
        return Task.CompletedTask;
    });

    private Task RefreshCurrentPreview() => received is not null ? RefreshPreview() : PreviewRecovery();

    private Task Apply() => Run(() =>
    {
        if (preview is null) return Task.CompletedTask;
        ProgressSync.Apply(preview);
        CancelPreview();
        message = "Progress saved. The previous progress is available under Recovery. Share this combined copy back to finish syncing both devices.";
        return Task.CompletedTask;
    });

    private void CancelPreview()
    {
        received = null;
        preview = null;
    }

    private async Task Run(Func<Task> action)
    {
        busy = true;
        error = null;
        message = null;
        try { await action(); }
        catch (Exception exception) when (exception is SocketException or TimeoutException)
        { error = "Could not connect. Check the address, port and pairing code, keep both apps open, and use the same private Wi-Fi. Try sharing from the phone if the PC firewall blocks sharing."; }
        catch (ArgumentException)
        { error = "Enter a private IPv4 address, a port between 1 and 65535, and the 32-character pairing code shown on the other device."; }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        { error = exception.Message; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { error = "Could not read or save progress. Check available storage and access, then try again. No import was applied."; }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { busy = false; }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await StopSharing();
        if (refreshTask is not null) await refreshTask;
        lifetime.Dispose();
    }
}
