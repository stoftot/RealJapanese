using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Repositories.Sync;

public static class LocalProgressTransfer
{
    public const int MaxSnapshotBytes = 4 * 1024 * 1024;
    private const int HeaderBytes = 13;
    private static readonly byte[] Magic = "RJLAN003"u8.ToArray();
    internal static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(90);
    internal static readonly TimeSpan TransferTimeout = TimeSpan.FromSeconds(15);

    public static LocalProgressTransferSession Start(byte[] snapshot, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Length > MaxSnapshotBytes) throw new ArgumentException("The progress snapshot is too large.", nameof(snapshot));
        var duration = lifetime ?? TimeSpan.FromMinutes(5);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        return new(snapshot.ToArray(), duration);
    }

    public static async Task<LocalProgressReceiveSession> ConnectAsync(string address, int port, CancellationToken cancellationToken = default)
    {
        var ip = ParsePrivateAddress(address);
        if (port is < 1 or > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HandshakeTimeout);
        var client = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token).ConfigureAwait(false);
            var protocol = await PairingProtocol.EstablishAsync(client.GetStream(), true, timeout.Token).ConfigureAwait(false);
            timeout.CancelAfter(ApprovalTimeout);
            return new(client, protocol, timeout, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            timeout.Dispose();
            throw new TimeoutException("The other device did not finish pairing in time.", exception);
        }
        catch
        {
            client.Dispose();
            timeout.Dispose();
            throw;
        }
    }

    internal static async Task WriteFrameAsync(Stream stream, byte type, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        header[8] = type;
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length)).CopyTo(header, 9);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadFrameAsync(Stream stream, byte type, int maximumBytes, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, 8).SequenceEqual(Magic) || header[8] != type)
            throw new InvalidDataException("Unsupported pairing message. Update both applications to matching versions.");
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 9));
        if (length < 0 || length > maximumBytes) throw new InvalidDataException("The transfer message is too large or invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static IPAddress ParsePrivateAddress(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var parts = address.Split('.');
        if (parts.Length != 4 || parts.Any(part => part.Length == 0 || part.Length > 3 ||
            part.Any(character => character is < '0' or > '9') || !byte.TryParse(part, out _) ||
            (part.Length > 1 && part[0] == '0')) || !IPAddress.TryParse(address, out var parsed) || !IsPrivateOrLoopback(parsed))
            throw new ArgumentException("Address must be a literal private or loopback IPv4 address.", nameof(address));
        return parsed;
    }

    internal static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) || bytes[0] == 127;
    }

    internal static IReadOnlyList<string> GetLocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address).Where(IsPrivateOrLoopback)
        .Select(address => address.ToString()).Distinct()
        .OrderBy(address => address.StartsWith("127.", StringComparison.Ordinal) ? 1 : 0).ToArray();
}

public sealed class LocalProgressReceiveSession : IAsyncDisposable
{
    private readonly TcpClient client;
    private readonly CancellationTokenSource cancellation;
    private readonly CancellationToken callerCancellation;
    private int disposed;
    public PairingApproval Pairing { get; }
    public Task<byte[]> Completion { get; }

    internal LocalProgressReceiveSession(TcpClient client, PairingProtocol protocol, CancellationTokenSource cancellation, CancellationToken callerCancellation)
    {
        this.client = client;
        this.cancellation = cancellation;
        this.callerCancellation = callerCancellation;
        Pairing = protocol.Approval;
        Completion = ReceiveAsync(protocol);
    }

    private async Task<byte[]> ReceiveAsync(PairingProtocol protocol)
    {
        using (protocol)
        {
            try
            {
                await protocol.ConfirmAsync(true, cancellation.Token).ConfigureAwait(false);
                cancellation.CancelAfter(LocalProgressTransfer.TransferTimeout);
                var result = await protocol.ReadAsync(7, LocalProgressTransfer.MaxSnapshotBytes, cancellation.Token).ConfigureAwait(false);
                await protocol.WriteAsync(8, new byte[] { 1 }, cancellation.Token).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException exception) when (!callerCancellation.IsCancellationRequested && Volatile.Read(ref disposed) == 0)
            { throw new TimeoutException("Pairing was cancelled or timed out. No progress was imported.", exception); }
            finally { client.Dispose(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        cancellation.Cancel();
        client.Dispose();
        try { await Completion.ConfigureAwait(false); } catch (Exception) { /* Caller observes Completion; disposal also releases failed sessions. */ }
        cancellation.Dispose();
    }
}

public sealed class LocalProgressTransferSession : IDisposable, IAsyncDisposable
{
    private readonly byte[] snapshot;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetimeCancellation;
    private readonly Task serverTask;
    private PairingApproval? pairing;
    private int terminal;
    private int disposed;
    private int succeeded;
    private int cleanedUp;
    private string? lastError;
    public int Port { get; }
    public IReadOnlyList<string> Addresses { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public PairingApproval? Pairing => Volatile.Read(ref pairing);
    public string? LastError => Volatile.Read(ref lastError);
    public bool Succeeded => Volatile.Read(ref succeeded) != 0;
    public bool IsActive => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref terminal) == 0 && !lifetimeCancellation.IsCancellationRequested;

    internal LocalProgressTransferSession(byte[] snapshot, TimeSpan lifetime)
    {
        this.snapshot = snapshot;
        ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime);
        Addresses = LocalProgressTransfer.GetLocalAddresses();
        listener = new(IPAddress.Any, 0);
        listener.Start(4);
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        lifetimeCancellation = new(lifetime);
        serverTask = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            // Bounded attempts prevent repeated guesses at the six-digit comparison.
            // One connection at a time keeps the displayed code attached to one request.
            for (var attempt = 0; attempt < 5 && !lifetimeCancellation.IsCancellationRequested; attempt++)
            {
                using var client = await listener.AcceptTcpClientAsync(lifetimeCancellation.Token).ConfigureAwait(false);
                if (client.Client.RemoteEndPoint is not IPEndPoint remote || !LocalProgressTransfer.IsPrivateOrLoopback(remote.Address)) continue;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
                timeout.CancelAfter(LocalProgressTransfer.HandshakeTimeout);
                Volatile.Write(ref lastError, null);
                var deliveryStarted = false;
                try
                {
                    using var protocol = await PairingProtocol.EstablishAsync(client.GetStream(), false, timeout.Token).ConfigureAwait(false);
                    Volatile.Write(ref lastError, null);
                    Volatile.Write(ref pairing, protocol.Approval);
                    timeout.CancelAfter(LocalProgressTransfer.ApprovalTimeout);
                    await protocol.ConfirmAsync(false, timeout.Token).ConfigureAwait(false);
                    timeout.CancelAfter(LocalProgressTransfer.TransferTimeout);
                    deliveryStarted = true;
                    await protocol.WriteAsync(7, snapshot, timeout.Token).ConfigureAwait(false);
                    var receipt = await protocol.ReadAsync(8, 1, timeout.Token).ConfigureAwait(false);
                    if (receipt.Length != 1 || receipt[0] != 1) throw new InvalidDataException("Invalid transfer receipt.");
                    Interlocked.Exchange(ref succeeded, 1);
                    return;
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or SocketException or OperationCanceledException or CryptographicException or ArgumentException)
                {
                    Volatile.Write(ref lastError, deliveryStarted
                        ? "The transfer ended without a receipt. Check the other device; it may already have received the preview."
                        : "Pairing did not complete. Compare both screens and try again.");
                    if (deliveryStarted) return;
                }
                finally { Volatile.Write(ref pairing, null); }
            }
            if (!lifetimeCancellation.IsCancellationRequested)
                Volatile.Write(ref lastError, "Too many unsuccessful pairing attempts. Start sharing again.");
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (SocketException) { Volatile.Write(ref lastError, "Sharing stopped because the network connection closed."); }
        finally
        {
            Interlocked.Exchange(ref terminal, 1);
            lifetimeCancellation.Cancel();
            listener.Stop();
            Volatile.Write(ref pairing, null);
            Array.Clear(snapshot);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetimeCancellation.Cancel();
        listener.Stop();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await serverTask.ConfigureAwait(false);
        if (Interlocked.Exchange(ref cleanedUp, 1) == 0) lifetimeCancellation.Dispose();
    }
}
