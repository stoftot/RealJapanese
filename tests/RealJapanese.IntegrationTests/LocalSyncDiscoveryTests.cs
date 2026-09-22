using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Prevents multicast discovery tests from competing for the process-wide discovery port.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalSyncDiscoveryCollection
{
    public const string Name = "Local sync discovery";
}

/// <summary>Checks discovery message parsing and public input validation without opening the multicast port.</summary>
public sealed class LocalSyncDiscoveryProtocolTests
{
    [Fact]
    public void Query_and_reply_messages_validate_nonce_source_and_bounds()
    {
        var nonce = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var query = LocalSyncDiscovery.CreateQuery(nonce);
        Assert.True(LocalSyncDiscovery.IsQuery(query, out var parsedNonce));
        Assert.Equal(nonce, parsedNonce);
        Assert.False(LocalSyncDiscovery.IsQuery(query.Append((byte)0).ToArray(), out _));

        var reply = LocalSyncDiscovery.CreateReply(nonce, "session-id", "Kitchen tablet 日", 43123);
        Assert.True(LocalSyncDiscovery.TryParseReply(reply, nonce, IPAddress.Parse("192.168.1.20"), out var device));
        Assert.Equal(new DiscoveredSyncDevice("session-id", "Kitchen tablet 日", "192.168.1.20", 43123), device);

        var wrongNonce = nonce.ToArray();
        wrongNonce[0] ^= 0xff;
        Assert.False(LocalSyncDiscovery.TryParseReply(reply, wrongNonce, IPAddress.Loopback, out _));
        Assert.False(LocalSyncDiscovery.TryParseReply(reply, nonce, IPAddress.Parse("8.8.8.8"), out _));
        Assert.False(LocalSyncDiscovery.TryParseReply(new byte[LocalSyncDiscovery.MaximumDatagramBytes + 1], nonce,
            IPAddress.Loopback, out _));

        var invalidUtf8 = reply.ToArray();
        var nameStart = 8 + 1 + 16 + 1 + "session-id".Length + 1;
        invalidUtf8[nameStart] = 0xff;
        Assert.False(LocalSyncDiscovery.TryParseReply(invalidUtf8, nonce, IPAddress.Loopback, out _));
    }

    [Theory]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("Trusted\u202Eexe")]
    public void Advertiser_rejects_unsafe_device_names(string name) =>
        Assert.Throws<ArgumentException>(() => LocalSyncDiscovery.Advertise(name, 12345));
}

/// <summary>Checks the live advertiser on a private IPv4 interface while holding the nonparallel multicast collection.</summary>
[Collection(LocalSyncDiscoveryCollection.Name)]
public sealed class LocalSyncDiscoveryNetworkTests
{
    /// <summary>The live multicast check is skipped visibly when the host has no usable private IPv4 interface.</summary>
    [Fact]
    public async Task Advertiser_replies_to_request_source_on_private_interface()
    {
        var localAddress = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address) && LocalProgressTransfer.IsPrivateOrLoopback(address));
        Assert.SkipWhen(localAddress is null, "No active private IPv4 interface is available for multicast discovery.");

        await using var advertiser = LocalSyncDiscovery.Advertise("Discovery check", 43124);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(localAddress!, 0));
        var nonce = Enumerable.Range(16, 16).Select(value => (byte)value).ToArray();
        await socket.SendToAsync(LocalSyncDiscovery.CreateQuery(nonce), SocketFlags.None,
            new IPEndPoint(localAddress!, LocalSyncDiscovery.MulticastPort));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var buffer = new byte[LocalSyncDiscovery.MaximumDatagramBytes + 1];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, timeout.Token);
        var peer = Assert.IsType<IPEndPoint>(result.RemoteEndPoint);
        Assert.True(LocalSyncDiscovery.TryParseReply(buffer.AsSpan(0, result.ReceivedBytes), nonce, peer.Address, out var device));
        Assert.Equal("Discovery check", device.Name);
        Assert.Equal(43124, device.Port);
        Assert.Equal(localAddress!.ToString(), device.Address);
    }
}
