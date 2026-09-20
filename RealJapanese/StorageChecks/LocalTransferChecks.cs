using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Repositories.Sync;

internal static class LocalTransferChecks
{
    public static void Run()
    {
        VerifyRoundTripAndApprovalsAsync().GetAwaiter().GetResult();
        VerifyDenialsAsync().GetAwaiter().GetResult();
        VerifyCancellationAndExpiryAsync().GetAwaiter().GetResult();
        VerifyBoundsAndAddressesAsync().GetAwaiter().GetResult();
        VerifyBadServerFramesAsync().GetAwaiter().GetResult();
        VerifyTamperingAndReplayAsync().GetAwaiter().GetResult();
        VerifyIndependentPairingsAsync().GetAwaiter().GetResult();
        VerifyModifiedCommitmentAsync().GetAwaiter().GetResult();
        VerifyAttemptLimitAsync().GetAwaiter().GetResult();
    }

    private static async Task VerifyAttemptLimitAsync()
    {
        await using var sender = LocalProgressTransfer.Start([]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, sender.Port, timeout.Token);
            var invalidCommitment = new byte[13];
            "RJLAN003"u8.CopyTo(invalidCommitment);
            invalidCommitment[8] = 1; // Empty commitment is malformed; length must be 32.
            await client.GetStream().WriteAsync(invalidCommitment, timeout.Token);
            Assert(await client.GetStream().ReadAsync(new byte[1], timeout.Token) == 0,
                "A malformed pairing attempt was not closed.");
        }
        await WaitInactiveAsync(sender);
        Assert(!sender.IsActive && !sender.Succeeded && sender.LastError?.Contains("Too many") == true,
            "Repeated failed handshakes did not end the bounded sharing session.");
    }

    private static async Task VerifyRoundTripAndApprovalsAsync()
    {
        var expected = "paired 日本語 progress"u8.ToArray();
        await using var sender = LocalProgressTransfer.Start(expected);
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port);
        var pairing = await SenderPairingAsync(sender);
        Assert(pairing.Code == receiver.Pairing.Code, "The peers displayed different pairing codes.");
        receiver.Pairing.Confirm(true);
        await AssertPendingAsync(receiver.Completion, "One approval released the snapshot.");
        pairing.Confirm(true);
        Assert((await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5))).SequenceEqual(expected),
            "The paired snapshot did not survive a loopback round trip.");
        await WaitInactiveAsync(sender);
        Assert(sender.Succeeded && sender.LastError is null, "A completed sender did not report success.");
        await ThrowsAnyAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port);
        },
            "A successful one-use sender accepted another receiver.");
    }

    private static async Task VerifyDenialsAsync()
    {
        foreach (var receiverDenies in new[] { true, false })
        {
            await using var sender = LocalProgressTransfer.Start("secret"u8.ToArray());
            await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port);
            var pairing = await SenderPairingAsync(sender);
            receiver.Pairing.Confirm(!receiverDenies);
            pairing.Confirm(receiverDenies);
            await ThrowsAnyAsync(async () => _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5)),
                "A denied pairing released the snapshot.");
            await WaitForErrorAsync(sender);
            Assert(!sender.Succeeded && sender.LastError is not null,
                "A denied pairing did not report rejection.");
        }
    }

    private static async Task VerifyCancellationAndExpiryAsync()
    {
        using (var cancellation = new CancellationTokenSource())
        await using (var sender = LocalProgressTransfer.Start([]))
        await using (var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, cancellation.Token))
        {
            _ = await SenderPairingAsync(sender);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(async () =>
                    _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5)),
                "Cancellation while awaiting approval did not end the receiver.");
            Assert(!sender.Succeeded, "A canceled transfer reported success.");
        }

        await using var expired = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(100));
        await WaitInactiveAsync(expired);
        Assert(!expired.IsActive && !expired.Succeeded, "An expired sender remained active or reported success.");
        await ThrowsAnyAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", expired.Port);
        },
            "An expired sender accepted a receiver.");
    }

    private static async Task VerifyBoundsAndAddressesAsync()
    {
        using var maximum = LocalProgressTransfer.Start(new byte[LocalProgressTransfer.MaxSnapshotBytes]);
        Throws<ArgumentException>(() => LocalProgressTransfer.Start(
            new byte[LocalProgressTransfer.MaxSnapshotBytes + 1]), "An oversized snapshot was accepted.");
        foreach (var address in new[] { "localhost", "169.254.169.254", "8.8.8.8", "127.0.0.01", "::1" })
            await ThrowsAsync<ArgumentException>(async () =>
            {
                await using var ignored = await LocalProgressTransfer.ConnectAsync(address, 1234);
            }, $"Address '{address}' was accepted.");
        await ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", 0);
        }, "Port zero was accepted.");
    }

    private static async Task VerifyBadServerFramesAsync()
    {
        foreach (var response in new[]
        {
            Frame("RJLAN002", 2, new byte[32]), Frame("RJLAN003", 1, new byte[32]),
            Header("RJLAN003", 2, -1), Header("RJLAN003", 2, 33),
            Header("RJLAN003", 2, 32).Concat(new byte[7]).ToArray()
        })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var connect = LocalProgressTransfer.ConnectAsync("127.0.0.1",
                ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            using (var client = await listener.AcceptTcpClientAsync(timeout.Token))
            {
                _ = await ReadFrameAsync(client.GetStream(), timeout.Token);
                await client.GetStream().WriteAsync(response, timeout.Token);
            }
            await ThrowsAnyAsync(async () =>
            {
                await using var ignored = await connect;
            },
                "An incompatible, invalid-length, or truncated handshake frame was accepted.");
        }
    }

    private static async Task VerifyTamperingAndReplayAsync()
    {
        foreach (var mutation in new Action<byte[]>[]
        {
            frame => frame[21] ^= 1, // Body.
            frame => frame[^1] ^= 1, // Authentication tag.
            frame => frame[20] ^= 1, // Sequence number.
            frame => frame[8] = 8 // Direction/message type.
        })
            await VerifySnapshotMutationAsync(mutation);

        byte[]? captured = null;
        await using (var sender = LocalProgressTransfer.Start("first"u8.ToArray()))
        await using (var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (fromClient && frame[8] == 5) captured = frame.ToArray();
            return frame;
        }))
        await using (var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port))
        {
            await ApproveAsync(sender, receiver);
            _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert(captured is not null, "The first approval frame was not observed.");

        await using var secondSender = LocalProgressTransfer.Start("second"u8.ToArray());
        await using var secondProxy = new FrameProxy(secondSender.Port, (fromClient, frame) =>
            fromClient && frame[8] == 5 ? captured!.ToArray() : frame);
        await using var secondReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", secondProxy.Port);
        await ApproveAsync(secondSender, secondReceiver);
        await ThrowsAnyAsync(async () => _ = await secondReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5)),
            "An approval from an earlier session was accepted.");
        Assert(!secondSender.Succeeded, "A replayed session reported success.");
    }

    private static async Task VerifySnapshotMutationAsync(Action<byte[]> mutation)
    {
        await using var sender = LocalProgressTransfer.Start("authenticated"u8.ToArray());
        await using var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (!fromClient && frame[8] == 7) mutation(frame);
            return frame;
        });
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port);
        await ApproveAsync(sender, receiver);
        await ThrowsAnyAsync(async () => _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5)),
            "A snapshot with a modified authenticated envelope was accepted.");
        Assert(!sender.Succeeded, "The sender succeeded without an authenticated receipt.");
    }

    private static async Task VerifyIndependentPairingsAsync()
    {
        await using var first = LocalProgressTransfer.Start([]);
        await using var second = LocalProgressTransfer.Start([]);
        await using var firstReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", first.Port);
        await using var secondReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", second.Port);
        var firstPairing = await SenderPairingAsync(first);
        var secondPairing = await SenderPairingAsync(second);
        Assert(IsDisplayCode(firstPairing.Code) && IsDisplayCode(secondPairing.Code),
            "A pairing code was not formatted as six digits in two groups.");
        await ApproveAsync(first, firstReceiver);
        await ApproveAsync(second, secondReceiver);
        Assert((await firstReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5))).Length == 0 &&
            (await secondReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5))).Length == 0,
            "Independent pairing handshakes did not both complete.");
    }

    private static bool IsDisplayCode(string code) => code.Length == 7 && code[3] == ' ' &&
        code.Where((_, index) => index != 3).All(char.IsAsciiDigit);

    private static async Task VerifyModifiedCommitmentAsync()
    {
        await using var sender = LocalProgressTransfer.Start([]);
        var changed = false;
        await using var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (fromClient && frame[8] == 1 && !changed) { frame[^1] ^= 1; changed = true; }
            return frame;
        });
        await ThrowsAnyAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port);
        },
            "A client opening that did not match its commitment was accepted.");
        Assert(changed && sender.Pairing is null && !sender.Succeeded,
            "A modified commitment reached pairing or reported success.");
    }

    private static async Task ApproveAsync(LocalProgressTransferSession sender, LocalProgressReceiveSession receiver)
    {
        var pairing = await SenderPairingAsync(sender);
        Assert(pairing.Code == receiver.Pairing.Code, "Pairing codes did not match.");
        receiver.Pairing.Confirm(true);
        pairing.Confirm(true);
    }

    private static async Task<PairingApproval> SenderPairingAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (sender.Pairing is { } pairing) return pairing;
            if (!sender.IsActive) break;
            await Task.Delay(10);
        }
        throw new InvalidOperationException($"Sender did not expose pairing: {sender.LastError}");
    }

    private static async Task WaitInactiveAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500 && sender.IsActive; attempt++) await Task.Delay(10);
    }

    private static async Task WaitForErrorAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500 && sender.LastError is null; attempt++) await Task.Delay(10);
    }

    private static async Task AssertPendingAsync(Task task, string message)
    {
        if (await Task.WhenAny(task, Task.Delay(150)) == task) throw new InvalidOperationException(message);
    }

    private static byte[] Frame(string magic, byte type, byte[] body) =>
        Header(magic, type, body.Length).Concat(body).ToArray();

    private static byte[] Header(string magic, byte type, int length)
    {
        var header = new byte[13];
        System.Text.Encoding.ASCII.GetBytes(magic).CopyTo(header, 0);
        header[8] = type;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(9), length);
        return header;
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[13];
        await stream.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(9));
        if (length is < 0 or > LocalProgressTransfer.MaxSnapshotBytes + 64)
            throw new InvalidDataException("Proxy received an invalid frame length.");
        var frame = new byte[13 + length];
        header.CopyTo(frame, 0);
        await stream.ReadExactlyAsync(frame.AsMemory(13), token);
        return frame;
    }

    private static async Task ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    { try { await action(); } catch (T) { return; } throw new InvalidOperationException(message); }

    private static async Task ThrowsAnyAsync(Func<Task> action, string message)
    {
        try { await action(); }
        catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException or
            SocketException or TimeoutException or InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException(message); }

    private static void Assert(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class FrameProxy : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Func<bool, byte[], byte[]> transform;
        private readonly Task runTask;
        private readonly int targetPort;

        public FrameProxy(int targetPort, Func<bool, byte[], byte[]> transform)
        {
            this.targetPort = targetPort;
            this.transform = transform;
            listener.Start(1);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            runTask = RunAsync();
        }

        public int Port { get; }

        private async Task RunAsync()
        {
            try
            {
                using var downstream = await listener.AcceptTcpClientAsync(cancellation.Token);
                using var upstream = new TcpClient(AddressFamily.InterNetwork);
                await upstream.ConnectAsync(IPAddress.Loopback, targetPort, cancellation.Token);
                var a = RelayAsync(downstream.GetStream(), upstream.GetStream(), true);
                var b = RelayAsync(upstream.GetStream(), downstream.GetStream(), false);
                await Task.WhenAny(a, b);
                cancellation.Cancel();
                try { await Task.WhenAll(a, b); }
                catch (Exception error) when (error is IOException or OperationCanceledException or SocketException) { }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or SocketException) { }
        }

        private async Task RelayAsync(Stream source, Stream destination, bool fromClient)
        {
            while (!cancellation.IsCancellationRequested)
            {
                var frame = transform(fromClient, await ReadFrameAsync(source, cancellation.Token));
                await destination.WriteAsync(frame, cancellation.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();
            try { await runTask; } catch (ObjectDisposedException) { }
            cancellation.Dispose();
        }
    }
}
