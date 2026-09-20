using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("StorageChecks")]

namespace Repositories.Sync;

public sealed record DiscoveredSyncDevice(string Id, string Name, string Address, int Port);

// Discovery data is an untrusted hint. The separate pairing/transfer flow owns security.
public static class LocalSyncDiscovery
{
    public const string MulticastAddress = "239.255.77.77";
    public const int MulticastPort = 47777;
    public const int MaximumDatagramBytes = 1024;

    private const int NonceBytes = 16;
    private const int MaximumIdBytes = 64;
    private const int MaximumNameBytes = 64;
    private const byte QueryType = 1;
    private const byte ReplyType = 2;
    private static readonly byte[] Magic = "RJDISC01"u8.ToArray();
    private static readonly IPAddress GroupAddress = IPAddress.Parse(MulticastAddress);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IAsyncDisposable Advertise(string name, int port)
    {
        ValidatePort(port);
        return new Advertiser(ValidateName(name), port);
    }

    public static async Task<IReadOnlyList<DiscoveredSyncDevice>> FindAsync(
        CancellationToken cancellationToken = default)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var query = CreateQuery(nonce);
        var found = new Dictionary<string, DiscoveredSyncDevice>(StringComparer.Ordinal);
        using var socket = CreateSocket(0);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var addresses = GetPrivateLocalAddresses();
        var nextSend = DateTimeOffset.MinValue;
        var sendCount = 0;
        var buffer = new byte[MaximumDatagramBytes + 1];

        try
        {
            while (!timeout.IsCancellationRequested && found.Count < 32)
            {
                var now = DateTimeOffset.UtcNow;
                if (sendCount < 3 && now >= nextSend)
                {
                    SendQuery(socket, query, addresses);
                    sendCount++;
                    nextSend = now.AddSeconds(1);
                }

                var untilNextSend = sendCount < 3 ? nextSend - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(3);
                if (untilNextSend <= TimeSpan.Zero) continue;
                using var receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                receiveCancellation.CancelAfter(untilNextSend);
                try
                {
                    EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote,
                        receiveCancellation.Token).ConfigureAwait(false);
                    if (result.ReceivedBytes > MaximumDatagramBytes || result.RemoteEndPoint is not IPEndPoint peer ||
                        !LocalProgressTransfer.IsPrivateOrLoopback(peer.Address) ||
                        !TryParseReply(buffer.AsSpan(0, result.ReceivedBytes), nonce, peer.Address, out var device))
                        continue;

                    found.TryAdd($"{device.Id}\n{device.Address}\n{device.Port}", device);
                }
                catch (OperationCanceledException) when (!timeout.IsCancellationRequested)
                {
                    // It is time for the next reliability query.
                }
                catch (SocketException) when (!timeout.IsCancellationRequested)
                {
                    // A transient interface error must not extend the bounded search.
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }

        cancellationToken.ThrowIfCancellationRequested();
        return found.Values.GroupBy(device => device.Id, StringComparer.Ordinal)
            .Select(group => group.OrderBy(device => IPAddress.IsLoopback(IPAddress.Parse(device.Address))).First())
            .OrderBy(device => device.Name, StringComparer.Ordinal).ToArray();
    }

    internal static byte[] CreateQuery(ReadOnlySpan<byte> nonce)
    {
        if (nonce.Length != NonceBytes) throw new ArgumentException("The discovery nonce must be 16 bytes.", nameof(nonce));
        var message = new byte[Magic.Length + 1 + NonceBytes];
        Magic.CopyTo(message, 0);
        message[Magic.Length] = QueryType;
        nonce.CopyTo(message.AsSpan(Magic.Length + 1));
        return message;
    }

    internal static bool IsQuery(ReadOnlySpan<byte> message, out byte[] nonce)
    {
        nonce = [];
        if (message.Length != Magic.Length + 1 + NonceBytes ||
            !message[..Magic.Length].SequenceEqual(Magic) || message[Magic.Length] != QueryType)
            return false;
        nonce = message[(Magic.Length + 1)..].ToArray();
        return true;
    }

    internal static byte[] CreateReply(ReadOnlySpan<byte> nonce, string id, string name, int port)
    {
        if (nonce.Length != NonceBytes) throw new ArgumentException("The discovery nonce must be 16 bytes.", nameof(nonce));
        ValidatePort(port);
        var idBytes = Encoding.ASCII.GetBytes(id);
        var nameBytes = StrictUtf8.GetBytes(ValidateName(name));
        if (idBytes.Length is < 1 or > MaximumIdBytes || id.Any(character => character is < '!' or > '~'))
            throw new ArgumentException("The discovery ID is invalid.", nameof(id));

        var reply = new byte[Magic.Length + 1 + NonceBytes + 1 + idBytes.Length + 1 + nameBytes.Length + 2];
        var offset = 0;
        Magic.CopyTo(reply, offset); offset += Magic.Length;
        reply[offset++] = ReplyType;
        nonce.CopyTo(reply.AsSpan(offset)); offset += NonceBytes;
        reply[offset++] = (byte)idBytes.Length;
        idBytes.CopyTo(reply, offset); offset += idBytes.Length;
        reply[offset++] = (byte)nameBytes.Length;
        nameBytes.CopyTo(reply, offset); offset += nameBytes.Length;
        reply[offset++] = (byte)(port >> 8);
        reply[offset] = (byte)port;
        return reply;
    }

    internal static bool TryParseReply(ReadOnlySpan<byte> message, ReadOnlySpan<byte> expectedNonce,
        IPAddress sourceAddress, out DiscoveredSyncDevice device)
    {
        device = null!;
        if (expectedNonce.Length != NonceBytes || message.Length > MaximumDatagramBytes ||
            sourceAddress.AddressFamily != AddressFamily.InterNetwork ||
            !LocalProgressTransfer.IsPrivateOrLoopback(sourceAddress))
            return false;
        var minimum = Magic.Length + 1 + NonceBytes + 1 + 1 + 2;
        if (message.Length < minimum || !message[..Magic.Length].SequenceEqual(Magic) ||
            message[Magic.Length] != ReplyType ||
            !CryptographicOperations.FixedTimeEquals(message.Slice(Magic.Length + 1, NonceBytes), expectedNonce))
            return false;

        var offset = Magic.Length + 1 + NonceBytes;
        var idLength = message[offset++];
        if (idLength is < 1 or > MaximumIdBytes || offset + idLength + 1 + 2 > message.Length) return false;
        var idBytes = message.Slice(offset, idLength); offset += idLength;
        if (idBytes.ContainsAnyExceptInRange((byte)'!', (byte)'~')) return false;
        var nameLength = message[offset++];
        if (nameLength is < 1 or > MaximumNameBytes || offset + nameLength + 2 != message.Length) return false;
        string name;
        try { name = StrictUtf8.GetString(message.Slice(offset, nameLength)); }
        catch (DecoderFallbackException) { return false; }
        if (!IsValidName(name)) return false;
        offset += nameLength;
        var port = (message[offset] << 8) | message[offset + 1];
        if (port == 0) return false;

        device = new DiscoveredSyncDevice(Encoding.ASCII.GetString(idBytes), name, sourceAddress.ToString(), port);
        return true;
    }

    private static Socket CreateSocket(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ExclusiveAddressUse = false
        };
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<IPAddress> GetPrivateLocalAddresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Select(item => item.Address)
        .Where(LocalProgressTransfer.IsPrivateOrLoopback)
        .Distinct()
        .ToArray();

    private static void SendQuery(Socket socket, byte[] query, IReadOnlyList<IPAddress> addresses)
    {
        var endpoint = new IPEndPoint(GroupAddress, MulticastPort);
        foreach (var address in addresses)
        {
            try
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
                socket.SendTo(query, endpoint);
            }
            catch (SocketException)
            {
                // Interfaces can disappear while discovery is running.
            }
        }
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (!IsValidName(name) || StrictUtf8.GetByteCount(name) > MaximumNameBytes)
            throw new ArgumentException($"The device name must be 1-{MaximumNameBytes} UTF-8 bytes and contain no control characters.", nameof(name));
        return name;
    }

    private static bool IsValidName(string name) => name.Length > 0 && !name.Any(character =>
        char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.Surrogate);

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
    }

    private sealed class Advertiser : IAsyncDisposable
    {
        private readonly Socket socket;
        private readonly CancellationTokenSource cancellation = new();
        private readonly byte[] id = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        private readonly string name;
        private readonly int port;
        private readonly Task worker;
        private int disposed;

        internal Advertiser(string name, int port)
        {
            this.name = name;
            this.port = port;
            socket = CreateSocket(MulticastPort);
            try
            {
                var joined = false;
                foreach (var address in GetPrivateLocalAddresses())
                {
                    try
                    {
                        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                            new MulticastOption(GroupAddress, address));
                        joined = true;
                    }
                    catch (SocketException)
                    {
                        // Continue with other active interfaces (common during Android/Windows transitions).
                    }
                }
                if (!joined)
                {
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(GroupAddress));
                }
            }
            catch
            {
                socket.Dispose();
                cancellation.Dispose();
                throw;
            }
            worker = RunAsync();
        }

        private async Task RunAsync()
        {
            var buffer = new byte[MaximumDatagramBytes + 1];
            var windowStart = Environment.TickCount64;
            var repliesInWindow = 0;
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    SocketReceiveFromResult result;
                    try
                    {
                        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote,
                            cancellation.Token).ConfigureAwait(false);
                    }
                    catch (SocketException) when (!cancellation.IsCancellationRequested)
                    {
                        // Oversized or transiently failed datagrams must not terminate advertising.
                        await Task.Delay(25, cancellation.Token).ConfigureAwait(false);
                        continue;
                    }
                    if (result.ReceivedBytes > MaximumDatagramBytes || result.RemoteEndPoint is not IPEndPoint peer ||
                        !LocalProgressTransfer.IsPrivateOrLoopback(peer.Address) ||
                        !IsQuery(buffer.AsSpan(0, result.ReceivedBytes), out var nonce))
                        continue;

                    var now = Environment.TickCount64;
                    if (now - windowStart >= 1000)
                    {
                        windowStart = now;
                        repliesInWindow = 0;
                    }
                    if (repliesInWindow >= 32) continue;
                    repliesInWindow++;
                    var reply = CreateReply(nonce, Encoding.ASCII.GetString(id), name, port);
                    try
                    {
                        await socket.SendToAsync(reply, SocketFlags.None, peer, cancellation.Token).ConfigureAwait(false);
                    }
                    catch (SocketException) when (!cancellation.IsCancellationRequested)
                    {
                        // A peer/interface can disappear between receive and reply.
                        await Task.Delay(25, cancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
            catch (SocketException) when (cancellation.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            cancellation.Cancel();
            socket.Dispose();
            try { await worker.ConfigureAwait(false); }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            finally { cancellation.Dispose(); }
        }
    }
}
