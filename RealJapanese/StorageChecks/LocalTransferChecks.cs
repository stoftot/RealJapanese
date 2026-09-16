using System.Net;
using System.Net.Sockets;
using Repositories.Sync;

internal static class LocalTransferChecks
{
    public static void Run()
    {
        VerifyRoundTripAsync().GetAwaiter().GetResult();
        VerifyWrongSecretDoesNotConsumeSessionAsync().GetAwaiter().GetResult();
        VerifyExpiryAsync().GetAwaiter().GetResult();
        VerifyPayloadBoundsAsync().GetAwaiter().GetResult();
        VerifyMalformedRequestDoesNotConsumeSessionAsync().GetAwaiter().GetResult();
        VerifyMidFrameExpiryAsync().GetAwaiter().GetResult();
        VerifyReceiverInputValidationAsync().GetAwaiter().GetResult();
    }

    private static async Task VerifyRoundTripAsync()
    {
        var expected = "local 日本語 progress"u8.ToArray();
        await using var session = LocalProgressTransfer.Start(expected);
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port, session.PairingCode);
        Assert(received.SequenceEqual(expected), "Local transfer did not survive a loopback round trip.");
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "A completed local transfer session remained active.");
    }

    private static async Task VerifyWrongSecretDoesNotConsumeSessionAsync()
    {
        var expected = "still available"u8.ToArray();
        await using var session = LocalProgressTransfer.Start(expected);
        var wrongCode = (session.PairingCode[0] == 'A' ? 'B' : 'A') + session.PairingCode[1..];
        await AssertThrowsAsync<InvalidDataException>(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port, wrongCode),
            "A wrong local-transfer secret was accepted.");
        Assert(session.IsActive, "A wrong secret consumed the local transfer session.");
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port, session.PairingCode);
        Assert(received.SequenceEqual(expected), "The valid receiver could not use the session after a wrong secret.");
    }

    private static async Task VerifyExpiryAsync()
    {
        await using var session = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(80));
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "An expired local transfer session remained active.");
        await AssertThrowsAnyAsync(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port, session.PairingCode),
            "An expired local transfer session accepted a receiver.");
    }

    private static async Task VerifyPayloadBoundsAsync()
    {
        var expected = new byte[LocalProgressTransfer.MaxSnapshotBytes];
        Random.Shared.NextBytes(expected);
        await using var maximum = LocalProgressTransfer.Start(expected);
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", maximum.Port, maximum.PairingCode);
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
            "RJLAN001"u8.CopyTo(oversizedHeader);
            oversizedHeader[8] = 1;
            BitConverter.GetBytes(IPAddress.HostToNetworkOrder(33)).CopyTo(oversizedHeader, 9);
            await client.GetStream().WriteAsync(oversizedHeader);
            client.Close();
        }
        await Task.Delay(50);
        Assert(session.IsActive, "A malformed request consumed the local transfer session.");
        var received = await LocalProgressTransfer.ReceiveAsync("127.0.0.1", session.Port, session.PairingCode);
        Assert(received.SequenceEqual(expected), "The valid receiver could not use the session after a malformed request.");
    }

    private static async Task VerifyMidFrameExpiryAsync()
    {
        await using var session = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(100));
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, session.Port);
        await client.GetStream().WriteAsync("RJLAN001"u8.ToArray());
        await WaitUntilInactiveAsync(session);
        Assert(!session.IsActive, "A partial client frame prevented session expiry.");
    }

    private static async Task VerifyReceiverInputValidationAsync()
    {
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("localhost", 1234, new string('A', 43)),
            "A hostname was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("169.254.169.254", 1234, new string('A', 43)),
            "A link-local metadata address was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("8.8.8.8", 1234, new string('A', 43)),
            "A public address was accepted by the local-transfer receiver.");
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", 0, new string('A', 43)),
            "An invalid local-transfer port was accepted.");
        await AssertThrowsAsync<ArgumentException>(
            () => LocalProgressTransfer.ReceiveAsync("127.0.0.1", 1234, "short"),
            "An invalid local-transfer pairing-code length was accepted.");
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
