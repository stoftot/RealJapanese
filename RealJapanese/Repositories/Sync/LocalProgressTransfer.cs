using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Repositories.Sync;

public static class LocalProgressTransfer
{
    public const int MaxSnapshotBytes = 4 * 1024 * 1024;

    private const int SecretBytes = 16;
    private const int ChallengeBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int HeaderBytes = 13;
    private static readonly byte[] Magic = "RJLAN001"u8.ToArray();
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ServerClientTimeout = TimeSpan.FromSeconds(5);
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

    public static async Task<byte[]> ReceiveAsync(
        string address,
        int port,
        string pairingCode,
        CancellationToken cancellationToken = default)
    {
        var ipAddress = ParsePrivateAddress(address);
        if (port is < 1 or > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port));
        var key = DecodePairingCode(pairingCode);
        var challenge = RandomNumberGenerator.GetBytes(ChallengeBytes);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReceiverTimeout);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await client.ConnectAsync(ipAddress, port, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            await WriteEncryptedFrameAsync(stream, 1, challenge, key, timeout.Token).ConfigureAwait(false);

            var response = await ReadEncryptedFrameAsync(
                stream,
                2,
                key,
                ChallengeBytes + MaxSnapshotBytes,
                timeout.Token).ConfigureAwait(false);
            if (response.Length < ChallengeBytes ||
                !CryptographicOperations.FixedTimeEquals(response.AsSpan(0, ChallengeBytes), challenge))
            {
                CryptographicOperations.ZeroMemory(response);
                throw new InvalidDataException("The transfer response did not match this request.");
            }

            var snapshot = response.AsSpan(ChallengeBytes).ToArray();
            CryptographicOperations.ZeroMemory(response);
            return snapshot;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The local progress transfer timed out.", exception);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("The local transfer peer rejected the request or returned an incomplete response.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(challenge);
        }
    }

    internal static async Task WriteEncryptedFrameAsync(
        Stream stream,
        byte messageType,
        ReadOnlyMemory<byte> plaintext,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var header = CreateHeader(messageType, plaintext.Length);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using (var aes = new AesGcm(key, TagBytes))
            aes.Encrypt(nonce, plaintext.Span, ciphertext, tag, header);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(nonce, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadEncryptedFrameAsync(
        Stream stream,
        byte expectedMessageType,
        byte[] key,
        int maximumPlaintextBytes,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic) || header[Magic.Length] != expectedMessageType)
            throw new InvalidDataException("The local transfer protocol header is invalid.");

        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, Magic.Length + 1));
        if (length < 0 || length > maximumPlaintextBytes)
            throw new InvalidDataException("The local transfer frame has an invalid length.");

        var nonce = new byte[NonceBytes];
        var ciphertext = new byte[length];
        var tag = new byte[TagBytes];
        await stream.ReadExactlyAsync(nonce, cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(ciphertext, cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(tag, cancellationToken).ConfigureAwait(false);

        var plaintext = new byte[length];
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
            return plaintext;
        }
        catch (CryptographicException exception)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new InvalidDataException("The local transfer could not be authenticated.", exception);
        }
    }

    private static byte[] CreateHeader(byte messageType, int plaintextLength)
    {
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = messageType;
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder(plaintextLength)).CopyTo(header, Magic.Length + 1);
        return header;
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

    private static byte[] DecodePairingCode(string pairingCode)
    {
        ArgumentNullException.ThrowIfNull(pairingCode);
        if (pairingCode.Length != SecretBytes * 2 || pairingCode.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Pairing code must be exactly 32 hexadecimal characters.", nameof(pairingCode));

        try
        {
            return SHA256.HashData(Convert.FromHexString(pairingCode));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Pairing code is not valid hexadecimal text.", nameof(pairingCode), exception);
        }
    }

    internal static string EncodePairingCode(byte[] secret) => Convert.ToHexString(secret);

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
    private readonly byte[] key;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetimeCancellation;
    private readonly Task serverTask;
    private int transferState;
    private int disposed;

    internal LocalProgressTransferSession(byte[] snapshot, TimeSpan lifetime)
    {
        this.snapshot = snapshot;
        var secret = RandomNumberGenerator.GetBytes(16);
        PairingCode = LocalProgressTransfer.EncodePairingCode(secret);
        key = SHA256.HashData(secret);
        CryptographicOperations.ZeroMemory(secret);
        ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime);
        Addresses = LocalProgressTransfer.GetLocalAddresses();
        listener = new TcpListener(IPAddress.Any, 0);
        listener.Start(4);
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        lifetimeCancellation = new CancellationTokenSource(lifetime);
        serverTask = RunAsync();
    }

    public int Port { get; }
    public string PairingCode { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public IReadOnlyList<string> Addresses { get; }
    public bool IsActive => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref transferState) != 2 && !lifetimeCancellation.IsCancellationRequested;

    private async Task RunAsync()
    {
        var clients = new HashSet<Task>();
        try
        {
            while (!lifetimeCancellation.IsCancellationRequested && Volatile.Read(ref transferState) != 2)
            {
                var client = await listener.AcceptTcpClientAsync(lifetimeCancellation.Token).ConfigureAwait(false);
                clients.RemoveWhere(task => task.IsCompleted);
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
        finally
        {
            listener.Stop();
            await Task.WhenAll(clients).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(snapshot);
            CryptographicOperations.ZeroMemory(key);
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
                var challenge = await LocalProgressTransfer.ReadEncryptedFrameAsync(stream, 1, key, 32, timeout.Token).ConfigureAwait(false);
                if (challenge.Length != 32 || Interlocked.CompareExchange(ref transferState, 1, 0) != 0)
                {
                    CryptographicOperations.ZeroMemory(challenge);
                    return;
                }

                try
                {
                    var response = new byte[challenge.Length + snapshot.Length];
                    challenge.CopyTo(response, 0);
                    snapshot.CopyTo(response, challenge.Length);
                    await LocalProgressTransfer.WriteEncryptedFrameAsync(stream, 2, response, key, timeout.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref transferState, 2);
                    lifetimeCancellation.Cancel();
                    CryptographicOperations.ZeroMemory(response);
                }
                catch
                {
                    Interlocked.CompareExchange(ref transferState, 0, 1);
                    throw;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(challenge);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or SocketException or CryptographicException)
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
