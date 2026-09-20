using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Repositories.Sync;

/// <summary>A comparison must be made on the two physical screens, not using a code received from the peer.</summary>
public sealed class PairingApproval
{
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal PairingApproval(string code) => Code = code;
    public string Code { get; }
    public bool IsConfirmed => decision.Task.IsCompletedSuccessfully && decision.Task.Result;
    public void Confirm(bool matches) => decision.TrySetResult(matches);
    internal Task<bool> WaitAsync(CancellationToken cancellationToken) => decision.Task.WaitAsync(cancellationToken);
}

// RJLAN003: committed ephemeral P-256 exchange, human SAS comparison, then directional
// HMAC-SHA256 records. Payloads deliberately remain readable. No persistent pairing keys.
internal sealed class PairingProtocol : IDisposable
{
    private const int OpeningLength = 123; // 32 random bytes followed by canonical 91-byte P-256 SPKI.
    private readonly byte[] sendKey;
    private readonly byte[] receiveKey;
    private readonly Stream stream;
    private ulong sendSequence;
    private ulong receiveSequence;
    public PairingApproval Approval { get; }

    private PairingProtocol(Stream stream, byte[] sendKey, byte[] receiveKey, string code)
    {
        this.stream = stream;
        this.sendKey = sendKey;
        this.receiveKey = receiveKey;
        Approval = new(code);
    }

    public static async Task<PairingProtocol> EstablishAsync(Stream stream, bool initiator, CancellationToken cancellationToken)
    {
        using var ownKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var opening = new byte[OpeningLength];
        RandomNumberGenerator.Fill(opening.AsSpan(0, 32));
        var publicKey = ownKey.ExportSubjectPublicKeyInfo();
        if (publicKey.Length != 91) throw new CryptographicException("Unsupported pairing key format.");
        publicKey.CopyTo(opening, 32);
        var ownCommitment = Commit(opening, initiator);
        byte[] peerCommitment;
        byte[] peerOpening;
        // Both parties commit before either opens. The random nonce prevents choosing
        // an adaptive key after seeing the other opening to force a matching short code.
        if (initiator)
        {
            await LocalProgressTransfer.WriteFrameAsync(stream, 1, ownCommitment, cancellationToken).ConfigureAwait(false);
            peerCommitment = await ReadExactFrameAsync(stream, 2, 32, cancellationToken).ConfigureAwait(false);
            await LocalProgressTransfer.WriteFrameAsync(stream, 3, opening, cancellationToken).ConfigureAwait(false);
            peerOpening = await ReadExactFrameAsync(stream, 4, OpeningLength, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            peerCommitment = await ReadExactFrameAsync(stream, 1, 32, cancellationToken).ConfigureAwait(false);
            await LocalProgressTransfer.WriteFrameAsync(stream, 2, ownCommitment, cancellationToken).ConfigureAwait(false);
            peerOpening = await ReadExactFrameAsync(stream, 3, OpeningLength, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(peerCommitment, Commit(peerOpening, true)))
                throw new InvalidDataException("Pairing verification failed. Start again and compare both screens.");
            await LocalProgressTransfer.WriteFrameAsync(stream, 4, opening, cancellationToken).ConfigureAwait(false);
        }
        if (!CryptographicOperations.FixedTimeEquals(peerCommitment, Commit(peerOpening, !initiator)))
            throw new InvalidDataException("Pairing verification failed. Start again and compare both screens.");

        using var peerKey = ECDiffieHellman.Create();
        peerKey.ImportSubjectPublicKeyInfo(peerOpening.AsSpan(32), out var consumed);
        var parameters = peerKey.ExportParameters(false);
        if (consumed != 91 || parameters.Curve.Oid.Value != "1.2.840.10045.3.1.7" ||
            !peerKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(peerOpening.AsSpan(32)))
            throw new InvalidDataException("Unsupported pairing key.");

        var transcript = new byte[8 + 2 * OpeningLength];
        "RJLAN003"u8.CopyTo(transcript);
        (initiator ? opening : peerOpening).CopyTo(transcript, 8);
        (initiator ? peerOpening : opening).CopyTo(transcript, 8 + OpeningLength);
        var transcriptHash = SHA256.HashData(transcript);
        var material = ownKey.DeriveKeyFromHash(peerKey.PublicKey, HashAlgorithmName.SHA256);
        try
        {
            var clientKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, transcriptHash, "RJLAN003 client records"u8.ToArray());
            var serverKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, transcriptHash, "RJLAN003 server records"u8.ToArray());
            var sas = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 8, transcriptHash, "RJLAN003 comparison"u8.ToArray());
            var digits = (BinaryPrimitives.ReadUInt64BigEndian(sas) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            CryptographicOperations.ZeroMemory(sas);
            return new(stream, initiator ? clientKey : serverKey, initiator ? serverKey : clientKey,
                digits[..3] + " " + digits[3..]);
        }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    public async Task ConfirmAsync(bool initiator, CancellationToken cancellationToken)
    {
        // Read concurrently so a rejection from either screen immediately closes the
        // other waiting screen, even before its user has made a choice.
        using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var peer = ReadAsync(initiator ? (byte)6 : (byte)5, 1, confirmation.Token);
        var local = Approval.WaitAsync(confirmation.Token);
        try
        {
            if (await Task.WhenAny(peer, local).ConfigureAwait(false) == peer)
                RequireAccepted(await peer.ConfigureAwait(false));
            var accepted = await local.ConfigureAwait(false);
            await WriteAsync(initiator ? (byte)5 : (byte)6, new byte[] { accepted ? (byte)1 : (byte)0 }, confirmation.Token).ConfigureAwait(false);
            if (!accepted) throw new InvalidDataException("Pairing was declined. No progress was transferred.");
            RequireAccepted(await peer.ConfigureAwait(false));
        }
        finally
        {
            confirmation.Cancel();
            try { await peer.ConfigureAwait(false); } catch (Exception) { /* Observe cancellation/failure of the concurrent read. */ }
        }
    }

    private static void RequireAccepted(byte[] decision)
    {
        if (decision.Length != 1 || decision[0] != 1)
            throw new InvalidDataException("The other device declined pairing. No progress was transferred.");
    }

    public async Task WriteAsync(byte type, byte[] body, CancellationToken cancellationToken)
    {
        var envelope = new byte[8 + body.Length + 32];
        BinaryPrimitives.WriteUInt64BigEndian(envelope, sendSequence++);
        body.CopyTo(envelope, 8);
        Tag(sendKey, type, envelope.AsSpan(0, envelope.Length - 32)).CopyTo(envelope, envelope.Length - 32);
        await LocalProgressTransfer.WriteFrameAsync(stream, type, envelope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadAsync(byte type, int maximumBytes, CancellationToken cancellationToken)
    {
        var envelope = await LocalProgressTransfer.ReadFrameAsync(stream, type, maximumBytes + 40, cancellationToken).ConfigureAwait(false);
        if (envelope.Length < 40 || BinaryPrimitives.ReadUInt64BigEndian(envelope) != receiveSequence ||
            !CryptographicOperations.FixedTimeEquals(envelope.AsSpan(envelope.Length - 32),
                Tag(receiveKey, type, envelope.AsSpan(0, envelope.Length - 32))))
            throw new InvalidDataException("Transfer integrity check failed. No progress was imported.");
        receiveSequence++;
        return envelope.AsSpan(8, envelope.Length - 40).ToArray();
    }

    private static byte[] Tag(byte[] key, byte type, ReadOnlySpan<byte> content)
    {
        var authenticated = new byte[13 + content.Length];
        "RJLAN003"u8.CopyTo(authenticated);
        authenticated[8] = type;
        BinaryPrimitives.WriteInt32BigEndian(authenticated.AsSpan(9), content.Length + 32);
        content.CopyTo(authenticated.AsSpan(13));
        return HMACSHA256.HashData(key, authenticated);
    }

    private static byte[] Commit(byte[] opening, bool initiator)
    {
        var input = new byte[9 + opening.Length];
        "RJLAN003"u8.CopyTo(input);
        input[8] = initiator ? (byte)1 : (byte)2;
        opening.CopyTo(input, 9);
        return SHA256.HashData(input);
    }

    private static async Task<byte[]> ReadExactFrameAsync(Stream stream, byte type, int length, CancellationToken cancellationToken)
    {
        var bytes = await LocalProgressTransfer.ReadFrameAsync(stream, type, length, cancellationToken).ConfigureAwait(false);
        if (bytes.Length != length) throw new InvalidDataException("Invalid pairing message length.");
        return bytes;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(sendKey);
        CryptographicOperations.ZeroMemory(receiveKey);
    }
}
