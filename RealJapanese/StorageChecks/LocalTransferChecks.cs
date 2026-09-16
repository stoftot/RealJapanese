using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Repositories.Sync;

internal static class LocalTransferChecks
{
    public static void Run()
    {
        VerifyRoundTripAsync().GetAwaiter().GetResult();
        VerifyRejectedResponsesAsync().GetAwaiter().GetResult();
        VerifyExpiryAsync().GetAwaiter().GetResult();
        VerifyPayloadBoundsAsync().GetAwaiter().GetResult();
        VerifyMalformedRequestDoesNotConsumeSessionAsync().GetAwaiter().GetResult();
        VerifyMidFrameExpiryAsync().GetAwaiter().GetResult();
        VerifyListenerFailureEndsSessionAsync().GetAwaiter().GetResult();
        VerifyReceiverInputValidationAsync().GetAwaiter().GetResult();
    }

    private static async Task VerifyRoundTripAsync()
    {
        var expected = "local 日本語 progress"u8.ToArray();
        await using var session = LocalProgressTransfer.Start(expected);
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port);
        Assert(received.SequenceEqual(expected), "Local transfer did not survive a loopback round trip.");
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "A completed local transfer session remained active.");
        await AssertThrowsAnyAsync(() => LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port),
            "A completed session served a second fetch.");
    }

    private static async Task VerifyRejectedResponsesAsync()
    {
        foreach (var (magic, type, length) in new[]
        {
            ("RJLAN001", (byte)2, 0), // A pairing-enabled peer is incompatible.
            ("RJLAN002", (byte)1, 0), // A request cannot masquerade as a response.
            ("RJLAN002", (byte)2, -1),
            ("RJLAN002", (byte)2, LocalProgressTransfer.MaxSnapshotBytes + 1),
            ("RJLAN002", (byte)2, 10) // Truncated body.
        })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var receiver = LocalProgressTransfer.ReceiveAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            using (var client = await listener.AcceptTcpClientAsync(timeout.Token))
            {
                var request = new byte[13];
                await client.GetStream().ReadExactlyAsync(request, timeout.Token);
                Assert(request.AsSpan(0, 8).SequenceEqual("RJLAN002"u8) && request[8] == 1 &&
                    IPAddress.NetworkToHostOrder(BitConverter.ToInt32(request, 9)) == 0,
                    "A fetch must be an empty request, with no credentials or writable data.");
                var response = new byte[13];
                System.Text.Encoding.ASCII.GetBytes(magic).CopyTo(response, 0);
                response[8] = type;
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length)).CopyTo(response, 9);
                await client.GetStream().WriteAsync(response, timeout.Token);
            }
            await AssertThrowsAsync<InvalidDataException>(() => receiver, "An incompatible or malformed response was accepted.");
        }
    }

    private static async Task VerifyExpiryAsync()
    {
        await using var session = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(80));
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "An expired local transfer session remained active.");
        await AssertThrowsAnyAsync(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port),
            "An expired local transfer session accepted a receiver.");
    }

    private static async Task VerifyPayloadBoundsAsync()
    {
        var expected = new byte[LocalProgressTransfer.MaxSnapshotBytes];
        Random.Shared.NextBytes(expected);
        await using var maximum = LocalProgressTransfer.Start(expected);
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", maximum.Port);
        Assert(received.SequenceEqual(expected), "A maximum-size local transfer did not survive a round trip.");
        AssertThrows<ArgumentException>(
            () => LocalProgressTransfer.Start(new byte[LocalProgressTransfer.MaxSnapshotBytes + 1]),
            "An oversized local-transfer snapshot was accepted.");
    }

    private static async Task VerifyMalformedRequestDoesNotConsumeSessionAsync()
    {
        var expected = "after malformed request"u8.ToArray();
        await using var session = LocalProgressTransfer.Start(expected);
        using (var client = new TcpClient(AddressFamily.InterNetwork))
        {
            await client.ConnectAsync(IPAddress.Loopback, session.Port);
            var oversizedHeader = new byte[13];
            "RJLAN002"u8.CopyTo(oversizedHeader);
            oversizedHeader[8] = 1;
            BitConverter.GetBytes(IPAddress.HostToNetworkOrder(1)).CopyTo(oversizedHeader, 9);
            await client.GetStream().WriteAsync(oversizedHeader);
            client.Close();
        }
        await Task.Delay(50);
        Assert(session.IsActive, "A malformed request consumed the local transfer session.");
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port);
        Assert(received.SequenceEqual(expected), "The valid receiver could not use the session after a malformed request.");
    }

    private static async Task VerifyMidFrameExpiryAsync()
    {
        await using var session = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(100));
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, session.Port);
        await client.GetStream().WriteAsync("RJLAN002"u8.ToArray());
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "A partial client frame prevented session expiry.");
    }

    private static async Task VerifyListenerFailureEndsSessionAsync()
    {
        await using var session = LocalProgressTransfer.Start([]);
        var listener = (TcpListener?)typeof(LocalProgressTransferSession)
            .GetField("listener", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(session) ?? throw new InvalidOperationException("Could not access the test session listener.");
        listener.Stop();
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "A failed local-transfer listener remained advertised as active.");
    }

    private static async Task VerifyReceiverInputValidationAsync()
    {
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("localhost", 1234),
            "A hostname was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("169.254.169.254", 1234),
            "A link-local metadata address was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("8.8.8.8", 1234),
            "A public address was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", 0),
            "An invalid local-transfer port was accepted.");
    }

    private static async Task WaitUntilInactiveAsync(LocalProgressTransferSession session)
    {
        for (var attempt = 0; attempt < 100 && session.IsActive; attempt++)
            await Task.Delay(10);
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static async Task AssertThrowsAnyAsync(Func<Task> action, string message)
    {
        try { await action(); }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
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
