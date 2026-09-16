using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Repositories.Sync;

// Deliberately unauthenticated: only share on a trusted local network.
// The request has no body, paths, commands or remote mutation operations.
public static class LocalProgressTransfer
{
    public const int MaxSnapshotBytes = 4 * 1024 * 1024;
    private const int HeaderBytes = 13;
    private static readonly byte[] Magic = "RJLAN002"u8.ToArray();
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReceiverTimeout = TimeSpan.FromSeconds(15);

    public static LocalProgressTransferSession Start(byte[] snapshot, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Length > MaxSnapshotBytes)
            throw new ArgumentException($"The progress snapshot cannot exceed {MaxSnapshotBytes} bytes.", nameof(snapshot));
        var actualLifetime = lifetime ?? DefaultLifetime;
        if (actualLifetime <= TimeSpan.Zero || actualLifetime > DefaultLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "The session lifetime must be positive and no longer than five minutes.");
        return new LocalProgressTransferSession(snapshot.ToArray(), actualLifetime);
    }

    public static async Task<byte[]> ReceiveAsync(string address, int port, CancellationToken cancellationToken = default)
    {
        var ipAddress = ParsePrivateAddress(address);
        if (port is < 1 or > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReceiverTimeout);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await client.ConnectAsync(ipAddress, port, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            await WriteFrameAsync(stream, 1, ReadOnlyMemory<byte>.Empty, timeout.Token).ConfigureAwait(false);
            return await ReadFrameAsync(stream, 2, MaxSnapshotBytes, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The local progress transfer timed out.", exception);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("The other app rejected the request or returned an incomplete response. Use matching app versions.", exception);
        }
    }

    internal static async Task WriteFrameAsync(Stream stream, byte messageType, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = messageType;
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length)).CopyTo(header, Magic.Length + 1);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadFrameAsync(Stream stream, byte expectedMessageType, int maximumBytes,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic) || header[Magic.Length] != expectedMessageType)
            throw new InvalidDataException("Unsupported local transfer protocol. Update both applications to matching versions.");
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, Magic.Length + 1));
        if (length < 0 || length > maximumBytes)
            throw new InvalidDataException("The local transfer frame has an invalid length.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static IPAddress ParsePrivateAddress(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var parts = address.Split('.');
        if (parts.Length != 4 || parts.Any(part => part.Length == 0 || part.Length > 3 ||
            part.Any(character => character is < '0' or > '9') ||
            !byte.TryParse(part, out _) || (part.Length > 1 && part[0] == '0')) ||
            !IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Address must be a literal private or loopback IPv4 address.", nameof(address));

        if (!IsPrivateOrLoopback(parsed))
            throw new ArgumentException("Address must be a literal private or loopback IPv4 address.", nameof(address));
        return parsed;
    }

    internal static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) ||
            bytes[0] == 127;
    }

    internal static IReadOnlyList<string> GetLocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address)
        .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
        .Select(address => address.ToString())
        .Where(address =>
        {
            try { ParsePrivateAddress(address); return true; }
            catch (ArgumentException) { return false; }
        })
        .Distinct()
        .OrderBy(address => address.StartsWith("127.", StringComparison.Ordinal) ? 1 : 0)
        .ToArray();
}

public sealed class LocalProgressTransferSession : IDisposable, IAsyncDisposable
{
    private readonly byte[] snapshot;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetimeCancellation;
    private readonly Task serverTask;
    private int transferState;
    private int terminal;
    private int disposed;

    internal LocalProgressTransferSession(byte[] snapshot, TimeSpan lifetime)
    {
        this.snapshot = snapshot;
        ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime);
        Addresses = LocalProgressTransfer.GetLocalAddresses();
        listener = new TcpListener(IPAddress.Any, 0);
        listener.Start(4);
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        lifetimeCancellation = new CancellationTokenSource(lifetime);
        serverTask = RunAsync();
    }

    public int Port { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public IReadOnlyList<string> Addresses { get; }
    public bool IsActive => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref terminal) == 0 &&
        Volatile.Read(ref transferState) != 2 && !lifetimeCancellation.IsCancellationRequested;

    private async Task RunAsync()
    {
        var clients = new HashSet<Task>();
        try
        {
            while (!lifetimeCancellation.IsCancellationRequested && Volatile.Read(ref transferState) != 2)
            {
                foreach (var completed in clients.Where(task => task.IsCompleted).ToArray())
                {
                    completed.GetAwaiter().GetResult();
                    clients.Remove(completed);
                }
                var client = await listener.AcceptTcpClientAsync(lifetimeCancellation.Token).ConfigureAwait(false);
                if (clients.Count >= 4)
                {
                    client.Dispose();
                    continue;
                }
                clients.Add(HandleClientAsync(client));
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (SocketException) when (lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            // A listener or client-worker failure terminates this one-use session. The UI must not
            // continue advertising a socket that can no longer serve the snapshot.
        }
        finally
        {
            Interlocked.Exchange(ref terminal, 1);
            lifetimeCancellation.Cancel();
            listener.Stop();
            try
            {
                await Task.WhenAll(clients).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Observe every client task; the session is already terminal and cannot be reused.
            }
            Array.Clear(snapshot);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                if (client.Client.RemoteEndPoint is not IPEndPoint remote ||
                    !LocalProgressTransfer.IsPrivateOrLoopback(remote.Address))
                    return;
                using var stream = client.GetStream();
                await LocalProgressTransfer.ReadFrameAsync(stream, 1, 0, timeout.Token).ConfigureAwait(false);
                if (Interlocked.CompareExchange(ref transferState, 1, 0) != 0) return;
                try
                {
                    await LocalProgressTransfer.WriteFrameAsync(stream, 2, snapshot, timeout.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref transferState, 2);
                    lifetimeCancellation.Cancel();
                }
                catch
                {
                    Interlocked.CompareExchange(ref transferState, 0, 1);
                    throw;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or SocketException)
            {
                // Invalid or stalled clients do not consume the one-use session.
            }
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
        try { await serverTask.ConfigureAwait(false); }
        catch (ObjectDisposedException) { }
        lifetimeCancellation.Dispose();
    }
}
