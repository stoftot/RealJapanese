using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Repositories.Sync;

internal static class LocalDiscoveryChecks
{
    public static void Run()
    {
        var nonce = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var query = LocalSyncDiscovery.CreateQuery(nonce);
        Assert(LocalSyncDiscovery.IsQuery(query, out var parsedNonce) && parsedNonce.SequenceEqual(nonce),
            "A valid discovery query was rejected.");
        Assert(!LocalSyncDiscovery.IsQuery(query.Append((byte)0).ToArray(), out _),
            "A discovery query with trailing data was accepted.");

        var reply = LocalSyncDiscovery.CreateReply(nonce, "session-id", "Kitchen tablet 日", 43123);
        Assert(LocalSyncDiscovery.TryParseReply(reply, nonce, IPAddress.Parse("192.168.1.20"), out var device),
            "A valid discovery reply was rejected.");
        Assert(device == new DiscoveredSyncDevice("session-id", "Kitchen tablet 日", "192.168.1.20", 43123),
            "A discovery reply did not preserve its bounded fields or UDP source address.");

        var wrongNonce = nonce.ToArray();
        wrongNonce[0] ^= 0xff;
        Assert(!LocalSyncDiscovery.TryParseReply(reply, wrongNonce, IPAddress.Loopback, out _),
            "A discovery reply with the wrong nonce was accepted.");
        Assert(!LocalSyncDiscovery.TryParseReply(reply, nonce, IPAddress.Parse("8.8.8.8"), out _),
            "A discovery reply from a public address was accepted.");
        Assert(!LocalSyncDiscovery.TryParseReply(new byte[LocalSyncDiscovery.MaximumDatagramBytes + 1], nonce,
                IPAddress.Loopback, out _),
            "An oversized discovery datagram was accepted.");

        var invalidUtf8 = reply.ToArray();
        var nameStart = 8 + 1 + 16 + 1 + "session-id".Length + 1;
        invalidUtf8[nameStart] = 0xff;
        Assert(!LocalSyncDiscovery.TryParseReply(invalidUtf8, nonce, IPAddress.Loopback, out _),
            "A discovery reply with an invalid UTF-8 name was accepted.");

        AssertThrows<ArgumentException>(() => LocalSyncDiscovery.Advertise(new string('x', 65), 12345),
            "An oversized advertised name was accepted.");
        AssertThrows<ArgumentException>(() => LocalSyncDiscovery.Advertise("Trusted\u202Eexe", 12345),
            "A device name containing a bidirectional formatting character was accepted.");
        VerifyLocalAdvertiser().GetAwaiter().GetResult();
    }

    private static async Task VerifyLocalAdvertiser()
    {
        var localAddress = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address) && LocalProgressTransfer.IsPrivateOrLoopback(address));
        if (localAddress is null) return;

        await using var advertiser = LocalSyncDiscovery.Advertise("Discovery check", 43124);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(localAddress, 0));
        var nonce = Enumerable.Range(16, 16).Select(value => (byte)value).ToArray();
        var query = LocalSyncDiscovery.CreateQuery(nonce);
        await socket.SendToAsync(query, SocketFlags.None,
            new IPEndPoint(localAddress, LocalSyncDiscovery.MulticastPort));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var buffer = new byte[LocalSyncDiscovery.MaximumDatagramBytes + 1];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, timeout.Token);
        Assert(result.RemoteEndPoint is IPEndPoint peer &&
            LocalSyncDiscovery.TryParseReply(buffer.AsSpan(0, result.ReceivedBytes), nonce, peer.Address, out var device) &&
            device.Name == "Discovery check" && device.Port == 43124 && device.Address == localAddress.ToString(),
            "The local discovery advertiser did not return a valid unicast response to its request source.");
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
