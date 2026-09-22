using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Exercises the paired loopback transport, its limits, and authenticated framing.</summary>
public sealed class LocalProgressTransferTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RoundTrip_requires_both_approvals_and_closes_one_use_sender()
    {
        var expected = "paired 日本語 progress"u8.ToArray();
        await using var sender = LocalProgressTransfer.Start(expected);
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, TestToken);
        var pairing = await SenderPairingAsync(sender);
        Assert.Equal(pairing.Code, receiver.Pairing.Code);
        Assert.Null(sender.Progress);
        Assert.Null(receiver.Progress);
        receiver.Pairing.Confirm(true);
        await AssertPendingAsync(receiver.Completion);
        Assert.Null(sender.Progress);
        Assert.Null(receiver.Progress);
        pairing.Confirm(true);
        Assert.Equal(expected, await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        await WaitInactiveAsync(sender);
        Assert.True(sender.Succeeded);
        Assert.Null(sender.LastError);
        Assert.Equal(new SyncTransferProgress(expected.Length, expected.Length, true), sender.Progress);
        Assert.Equal(new SyncTransferProgress(expected.Length, expected.Length, true), receiver.Progress);
        await AssertTransportFailureAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, TestToken);
        });
    }

    [Fact]
    public async Task Throttled_transfer_reports_monotonic_incremental_progress()
    {
        var expected = new byte[1024 * 1024];
        for (var index = 0; index < expected.Length; index++) expected[index] = (byte)(index % 251);
        await using var sender = LocalProgressTransfer.Start(expected);
        await using var proxy = new FrameProxy(sender.Port, static (_, frame) => frame, 16 * 1024, TimeSpan.FromMilliseconds(2));
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port, TestToken);
        await ApproveAsync(sender, receiver);

        var observed = new List<int>();
        while (!receiver.Completion.IsCompleted)
        {
            if (receiver.Progress is { } current)
            {
                Assert.InRange(current.BytesTransferred, 0, current.TotalBytes);
                Assert.Contains(current.TotalBytes, new[] { 0, expected.Length });
                if (current.IsComplete) Assert.Equal(expected.Length, current.BytesTransferred);
                if (current.TotalBytes == expected.Length && (observed.Count == 0 || observed[^1] != current.BytesTransferred))
                    observed.Add(current.BytesTransferred);
            }
            await Task.Delay(1, TestToken);
        }

        Assert.Equal(expected, await receiver.Completion);
        await WaitInactiveAsync(sender);
        Assert.True(observed.Count >= 2);
        Assert.True(observed.Zip(observed.Skip(1)).All(pair => pair.First <= pair.Second));
        Assert.Contains(observed, value => value is > 0 and < 1024 * 1024);
        Assert.Equal(new SyncTransferProgress(expected.Length, expected.Length, true), sender.Progress);
        Assert.Equal(new SyncTransferProgress(expected.Length, expected.Length, true), receiver.Progress);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Either_peer_can_deny_pairing(bool receiverDenies)
    {
        await using var sender = LocalProgressTransfer.Start("secret"u8.ToArray());
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, TestToken);
        var pairing = await SenderPairingAsync(sender);
        receiver.Pairing.Confirm(!receiverDenies);
        pairing.Confirm(receiverDenies);
        await AssertTransportFailureAsync(async () => _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        await WaitForErrorAsync(sender);
        Assert.False(sender.Succeeded);
        Assert.NotNull(sender.LastError);
    }

    [Fact]
    public async Task Cancellation_while_awaiting_approval_ends_receiver()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        await using var sender = LocalProgressTransfer.Start([]);
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, cancellation.Token);
        _ = await SenderPairingAsync(sender);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.False(sender.Succeeded);
    }

    [Fact]
    public async Task Expired_sender_becomes_inactive_and_rejects_receivers()
    {
        await using var sender = LocalProgressTransfer.Start([], TimeSpan.FromMilliseconds(100));
        await WaitInactiveAsync(sender);
        Assert.False(sender.IsActive);
        Assert.False(sender.Succeeded);
        await AssertTransportFailureAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", sender.Port, TestToken);
        });
    }

    [Fact]
    public async Task Snapshot_address_and_port_bounds_are_enforced()
    {
        using var maximum = LocalProgressTransfer.Start(new byte[LocalProgressTransfer.MaxSnapshotBytes]);
        Assert.Throws<ArgumentException>(() => LocalProgressTransfer.Start(new byte[LocalProgressTransfer.MaxSnapshotBytes + 1]));
        foreach (var address in new[] { "localhost", "169.254.169.254", "8.8.8.8", "127.0.0.01", "::1" })
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using var ignored = await LocalProgressTransfer.ConnectAsync(address, 1234, TestToken);
            });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", 0, TestToken);
        });
    }

    public static TheoryData<byte[]> InvalidHandshakeFrames => new()
    {
        Frame("RJLAN002", 2, new byte[32]),
        Frame("RJLAN003", 1, new byte[32]),
        Header("RJLAN003", 2, -1),
        Header("RJLAN003", 2, 33),
        Header("RJLAN003", 2, 32).Concat(new byte[7]).ToArray()
    };

    [Theory]
    [MemberData(nameof(InvalidHandshakeFrames))]
    public async Task Invalid_or_truncated_server_handshake_is_rejected(byte[] response)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var connect = LocalProgressTransfer.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
        using (var client = await listener.AcceptTcpClientAsync(timeout.Token))
        {
            _ = await ReadFrameAsync(client.GetStream(), timeout.Token);
            await client.GetStream().WriteAsync(response, timeout.Token);
        }
        await AssertTransportFailureAsync(async () =>
        {
            await using var ignored = await connect;
        });
    }

    [Theory]
    [InlineData(21)]
    [InlineData(-1)]
    [InlineData(20)]
    [InlineData(8)]
    public async Task Modified_authenticated_snapshot_is_rejected(int mutationPosition)
    {
        await using var sender = LocalProgressTransfer.Start("authenticated"u8.ToArray());
        await using var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (!fromClient && frame[8] == 7)
            {
                if (mutationPosition == 8) frame[8] = 8;
                else frame[mutationPosition < 0 ? frame.Length - 1 : mutationPosition] ^= 1;
            }
            return frame;
        });
        await using var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port, TestToken);
        await ApproveAsync(sender, receiver);
        await AssertTransportFailureAsync(async () => _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.False(receiver.Progress?.IsComplete ?? false);
        Assert.False(sender.Progress?.IsComplete ?? false);
        Assert.False(sender.Succeeded);
    }

    /// <summary>A captured approval belongs to its ephemeral session and must fail in a new session.</summary>
    [Fact]
    public async Task Approval_replay_from_an_earlier_session_is_rejected()
    {
        byte[]? captured = null;
        await using (var sender = LocalProgressTransfer.Start("first"u8.ToArray()))
        await using (var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (fromClient && frame[8] == 5) captured = frame.ToArray();
            return frame;
        }))
        await using (var receiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port, TestToken))
        {
            await ApproveAsync(sender, receiver);
            _ = await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        }
        Assert.NotNull(captured);

        await using var secondSender = LocalProgressTransfer.Start("second"u8.ToArray());
        await using var secondProxy = new FrameProxy(secondSender.Port,
            (fromClient, frame) => fromClient && frame[8] == 5 ? captured!.ToArray() : frame);
        await using var secondReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", secondProxy.Port, TestToken);
        await ApproveAsync(secondSender, secondReceiver);
        await AssertTransportFailureAsync(async () => _ = await secondReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.False(secondSender.Succeeded);
    }

    [Fact]
    public async Task Independent_pairings_have_display_codes_and_both_complete()
    {
        await using var first = LocalProgressTransfer.Start([]);
        await using var second = LocalProgressTransfer.Start([]);
        await using var firstReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", first.Port, TestToken);
        await using var secondReceiver = await LocalProgressTransfer.ConnectAsync("127.0.0.1", second.Port, TestToken);
        var firstPairing = await SenderPairingAsync(first);
        var secondPairing = await SenderPairingAsync(second);
        Assert.True(IsDisplayCode(firstPairing.Code));
        Assert.True(IsDisplayCode(secondPairing.Code));
        await ApproveAsync(first, firstReceiver);
        await ApproveAsync(second, secondReceiver);
        Assert.Empty(await firstReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.Empty(await secondReceiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
    }

    /// <summary>The commitment must bind the opening before either peer reveals its key material.</summary>
    [Fact]
    public async Task Modified_opening_does_not_reach_pairing()
    {
        await using var sender = LocalProgressTransfer.Start([]);
        var changed = false;
        await using var proxy = new FrameProxy(sender.Port, (fromClient, frame) =>
        {
            if (fromClient && frame[8] == 1 && !changed) { frame[^1] ^= 1; changed = true; }
            return frame;
        });
        await AssertTransportFailureAsync(async () =>
        {
            await using var ignored = await LocalProgressTransfer.ConnectAsync("127.0.0.1", proxy.Port, TestToken);
        });
        Assert.True(changed);
        Assert.Null(sender.Pairing);
        Assert.False(sender.Succeeded);
    }

    [Fact]
    public async Task Five_malformed_openings_end_the_bounded_sender_session()
    {
        await using var sender = LocalProgressTransfer.Start([]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, sender.Port, timeout.Token);
            var invalidCommitment = new byte[13];
            "RJLAN003"u8.CopyTo(invalidCommitment);
            invalidCommitment[8] = 1;
            await client.GetStream().WriteAsync(invalidCommitment, timeout.Token);
            Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1], timeout.Token));
        }
        await WaitInactiveAsync(sender);
        Assert.False(sender.IsActive);
        Assert.False(sender.Succeeded);
        Assert.Contains("Too many", sender.LastError);
    }

    private static bool IsDisplayCode(string code) => code.Length == 7 && code[3] == ' ' &&
        code.Where((_, index) => index != 3).All(char.IsAsciiDigit);

    private static async Task ApproveAsync(LocalProgressTransferSession sender, LocalProgressReceiveSession receiver)
    {
        var pairing = await SenderPairingAsync(sender);
        Assert.Equal(pairing.Code, receiver.Pairing.Code);
        receiver.Pairing.Confirm(true);
        pairing.Confirm(true);
    }

    private static async Task<PairingApproval> SenderPairingAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (sender.Pairing is { } pairing) return pairing;
            if (!sender.IsActive) break;
            await Task.Delay(10, TestToken);
        }
        throw new Xunit.Sdk.XunitException($"Sender did not expose pairing: {sender.LastError}");
    }

    private static async Task WaitInactiveAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500 && sender.IsActive; attempt++) await Task.Delay(10, TestToken);
        Assert.False(sender.IsActive);
    }

    private static async Task WaitForErrorAsync(LocalProgressTransferSession sender)
    {
        for (var attempt = 0; attempt < 500 && sender.LastError is null; attempt++) await Task.Delay(10, TestToken);
    }

    private static async Task AssertPendingAsync(Task task) =>
        Assert.NotSame(task, await Task.WhenAny(task, Task.Delay(150, TestToken)));

    private static async Task AssertTransportFailureAsync(Func<Task> action)
    {
        var error = await Record.ExceptionAsync(action);
        Assert.NotNull(error);
        Assert.True(error is IOException or InvalidDataException or OperationCanceledException or SocketException or
            TimeoutException or InvalidOperationException, $"Unexpected transport exception: {error}");
    }

    private static byte[] Frame(string magic, byte type, byte[] body) => Header(magic, type, body.Length).Concat(body).ToArray();

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

    /// <summary>Relays complete protocol frames so tests can throttle or mutate selected messages.</summary>
    private sealed class FrameProxy : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Func<bool, byte[], byte[]> transform;
        private readonly Task runTask;
        private readonly int targetPort;
        private readonly int relayChunkBytes;
        private readonly TimeSpan relayDelay;

        public FrameProxy(int targetPort, Func<bool, byte[], byte[]> transform, int relayChunkBytes = int.MaxValue,
            TimeSpan relayDelay = default)
        {
            this.targetPort = targetPort;
            this.transform = transform;
            this.relayChunkBytes = relayChunkBytes;
            this.relayDelay = relayDelay;
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
                var clientToServer = RelayAsync(downstream.GetStream(), upstream.GetStream(), true);
                var serverToClient = RelayAsync(upstream.GetStream(), downstream.GetStream(), false);
                await Task.WhenAny(clientToServer, serverToClient);
                cancellation.Cancel();
                try { await Task.WhenAll(clientToServer, serverToClient); }
                catch (Exception error) when (error is IOException or OperationCanceledException or SocketException) { }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or SocketException) { }
        }

        private async Task RelayAsync(Stream source, Stream destination, bool fromClient)
        {
            while (!cancellation.IsCancellationRequested)
            {
                var frame = transform(fromClient, await ReadFrameAsync(source, cancellation.Token));
                for (var offset = 0; offset < frame.Length; offset += relayChunkBytes)
                {
                    var count = Math.Min(relayChunkBytes, frame.Length - offset);
                    await destination.WriteAsync(frame.AsMemory(offset, count), cancellation.Token);
                    if (relayDelay > TimeSpan.Zero) await Task.Delay(relayDelay, cancellation.Token);
                }
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
