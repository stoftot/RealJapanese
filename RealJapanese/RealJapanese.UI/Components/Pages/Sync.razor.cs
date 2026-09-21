using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using Repositories.Sync;

namespace RealJapanese.Components.Pages;

public partial class Sync
{
    private LocalProgressTransferSession? sharing;
    private LocalProgressReceiveSession? receiving;
    private IAsyncDisposable? advertisement;
    private IDisposable? advertisingLease;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private Task? refreshTask;
    private Task? operationTask;
    private bool automatic = true;
    private bool searched;
    private IReadOnlyList<DiscoveredSyncDevice> devices = [];
    private string address = "";
    private int port;
    private bool busy;
    private string? message;
    private string? error;
    private string? discoveryError;
    private byte[]? received;
    private ImportMode mode = ImportMode.MergeKeepLocal;
    private ImportPreview? preview;
    private PairingApproval? CurrentPairing => receiving?.Pairing ?? sharing?.Pairing;
    private SyncTransferProgress? CurrentTransfer => receiving?.Progress ??
        (sharing is { IsActive: true } ? sharing.Progress : null);
    private bool DialogOpen => CurrentPairing is not null || CurrentTransfer is not null || received is not null || preview is not null;
    private string DialogTitle => CurrentTransfer is not null ? "Transferring progress" :
        CurrentPairing is not null ? "Compare the codes" : received is not null ? "Review before applying" : "Restore previous progress";

    private string TransferStatus(SyncTransferProgress transfer) => transfer.BytesTransferred >= transfer.TotalBytes
        ? receiving is not null ? "Checking transfer integrity…" : "Waiting for the receiving device to verify…"
        : $"{(long)transfer.BytesTransferred * 100 / transfer.TotalBytes}% · {transfer.BytesTransferred:N0} of {transfer.TotalBytes:N0} bytes";

    private async Task DismissDialog()
    {
        var dismissingConnection = CurrentPairing is not null || CurrentTransfer is not null;
        CancelPreview();
        if (!dismissingConnection) return;
        CancelConnection();
        if (receiving is not null && operationTask is not null) await operationTask;
        if (sharing is not null) await StopSharing();
    }

    protected override void OnInitialized() => refreshTask = RefreshStatus();

    private async Task RefreshStatus()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                await InvokeAsync(async () =>
                {
                    if (sharing is { IsActive: false }) await StopAdvertising();
                    StateHasChanged();
                });
        }
        catch (OperationCanceledException) { }
    }

    private Task SetAutomatic(bool value) => Run(async () =>
    {
        automatic = value;
        devices = [];
        searched = false;
        await StopAdvertising();
        if (automatic && sharing is { IsActive: true }) TryAdvertise();
    });

    private Task StartSharing() => Run(async () =>
    {
        await StopSharing();
        CancelPreview();
        sharing = LocalProgressTransfer.Start(ProgressSync.ExportSnapshot());
        if (!sharing.Addresses.Any(value => !value.StartsWith("127.")))
        {
            await StopSharing();
            throw new InvalidOperationException("No private network address was found. Connect this device to Wi-Fi and try again.");
        }
        if (automatic) TryAdvertise();
    });

    private void TryAdvertise()
    {
        discoveryError = null;
        try
        {
            advertisingLease = NetworkEnvironment.EnableDiscovery();
            advertisement = LocalSyncDiscovery.Advertise(NetworkEnvironment.DeviceName, sharing!.Port);
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException or UnauthorizedAccessException)
        {
            advertisingLease?.Dispose();
            advertisingLease = null;
            discoveryError = "This device could not announce itself. Switch to Manual to connect using its address and port.";
        }
    }

    private async Task StopAdvertising()
    {
        var previous = advertisement;
        advertisement = null;
        try { if (previous is not null) await previous.DisposeAsync(); }
        finally { advertisingLease?.Dispose(); advertisingLease = null; }
    }

    private async Task StopSharing()
    {
        var previous = sharing;
        sharing = null;
        try { await StopAdvertising(); }
        finally
        {
            // Discovery failures must never leave the TCP share alive on navigation/stop.
            if (previous is not null) await previous.DisposeAsync();
            discoveryError = null;
        }
    }

    private Task FindDevices() => StartOperation(async token =>
    {
        devices = [];
        searched = false;
        using var lease = NetworkEnvironment.EnableDiscovery();
        devices = await LocalSyncDiscovery.FindAsync(token);
        // Do not offer this page's own active advertisement as a receiving target.
        if (sharing is not null)
            devices = devices.Where(device => device.Port != sharing.Port || !sharing.Addresses.Contains(device.Address)).ToArray();
        searched = true;
    });

    private Task SelectDevice(DiscoveredSyncDevice device)
    {
        address = device.Address;
        port = device.Port;
        return Receive();
    }

    private Task Receive() => StartOperation(async token =>
    {
        await StopSharing();
        CancelPreview();
        receiving = await LocalProgressTransfer.ConnectAsync(address.Trim(), port, token);
        devices = [];
        searched = false;
        StateHasChanged();
        try
        {
            received = await receiving.Completion;
            token.ThrowIfCancellationRequested();
            preview = ProgressSync.PreviewSnapshot(received, mode);
            message = "Pairing and integrity checks passed. Review the preview; your progress has not changed.";
        }
        catch { CancelPreview(); throw; }
        finally
        {
            await receiving.DisposeAsync();
            receiving = null;
        }
    });

    private Task StartOperation(Func<CancellationToken, Task> action)
    {
        if (busy) return Task.CompletedTask;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var current = operation;
        operationTask = Run(async () =>
        {
            try { await action(current.Token); }
            finally { operation = null; current.Dispose(); }
        });
        return operationTask;
    }

    private void CancelConnection() => operation?.Cancel();

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

    private void CancelPreview() { received = null; preview = null; }

    private async Task Run(Func<Task> action)
    {
        busy = true;
        error = null;
        message = null;
        try { await action(); }
        catch (Exception exception) when (exception is SocketException or TimeoutException)
        { error = "Connection ended or timed out. Keep both apps open on the same Wi-Fi and confirm matching codes on both screens. If discovery fails, try Manual."; }
        catch (ArgumentException)
        { error = "Enter the private IPv4 address and port (1 to 65535) shown on the other device."; }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        { error = exception.Message; }
        catch (CryptographicException)
        { error = "Pairing verification failed. Nothing was imported. Start again and compare both screens."; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { error = "The transfer or save could not complete. Check the connection and storage, then try again."; }
        catch (OperationCanceledException)
        { if (!lifetime.IsCancellationRequested) message = "Connection cancelled. No progress was imported."; }
        finally { busy = false; }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        operation?.Cancel();
        if (operationTask is not null) await operationTask;
        await StopSharing();
        if (refreshTask is not null) await refreshTask;
        lifetime.Dispose();
    }
}
