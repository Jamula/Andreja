using Andreja.Platform.Contracts.Peers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Modules.Channels;

public sealed class LocalPeerChannel : IPeerChannel
{
    public const string ProtocolVersion = "andreja.peer/1";
    public const string FixtureAlgorithm = "HMAC-SHA256-FIXTURE";

    private readonly object gate = new();
    private readonly Dictionary<string, byte[]> verificationKeys;
    private readonly TimeSpan maximumClockSkew;
    private readonly HashSet<(Guid Sender, string Nonce)> nonces = [];
    private readonly Dictionary<(Guid Sender, string Key), IdempotencyReceipt> receipts = [];

    public LocalPeerChannel(
        IReadOnlyDictionary<string, byte[]> verificationKeys,
        TimeSpan? maximumClockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(verificationKeys);
        this.verificationKeys = verificationKeys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
        this.maximumClockSkew = maximumClockSkew ?? TimeSpan.FromMinutes(5);
    }

    public ValueTask<PeerReceiveResult> ReceiveAsync(
        SignedPeerEnvelope envelope,
        PeerReceiveContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = Validate(envelope, context);
        if (outcome is not null)
        {
            return ValueTask.FromResult(new PeerReceiveResult(outcome.Value, false, null));
        }

        var sender = envelope.SenderId;
        lock (gate)
        {
            if (!nonces.Add((sender, envelope.Nonce)))
            {
                return ValueTask.FromResult(
                    new PeerReceiveResult(PeerReceiveOutcome.Replay, false, null));
            }

            var idempotencyKey = (sender, envelope.IdempotencyKey);
            var fingerprint = IdempotencyFingerprint(envelope);
            if (receipts.TryGetValue(idempotencyKey, out var receipt))
            {
                return ValueTask.FromResult(
                    receipt.Fingerprint == fingerprint
                        ? new PeerReceiveResult(
                            PeerReceiveOutcome.IdempotentReplay,
                            false,
                            receipt.ReceiptId)
                        : new PeerReceiveResult(
                            PeerReceiveOutcome.IdempotencyConflict,
                            false,
                            null));
            }

            var receiptId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
            receipts.Add(idempotencyKey, new(fingerprint, receiptId));
            return ValueTask.FromResult(
                new PeerReceiveResult(PeerReceiveOutcome.Accepted, true, receiptId));
        }
    }

    public static SignedPeerEnvelope SignFixture(
        SignedPeerEnvelope unsignedEnvelope,
        ReadOnlySpan<byte> key)
    {
        var canonical = Canonicalize(unsignedEnvelope with { Signature = string.Empty });
        var signature = HMACSHA256.HashData(key, canonical);
        return unsignedEnvelope with { Signature = Convert.ToBase64String(signature) };
    }

    public static byte[] Canonicalize(SignedPeerEnvelope envelope)
    {
        using var stream = new MemoryStream();
        Append(stream, envelope.ProtocolVersion);
        Append(stream, envelope.EnvelopeId.ToString("N"));
        Append(stream, envelope.SenderId.ToString("N"));
        Append(stream, envelope.RecipientId.ToString("N"));
        Append(stream, envelope.GrantId.ToString("N"));
        Append(stream, envelope.Purpose);
        Append(stream, envelope.Nonce);
        Append(stream, envelope.IdempotencyKey);
        Append(stream, envelope.IssuedAt.ToUniversalTime().ToString("O"));
        Append(stream, envelope.ExpiresAt.ToUniversalTime().ToString("O"));
        Append(stream, envelope.PayloadType);
        Append(stream, envelope.PayloadDigest);
        Append(stream, envelope.SigningAlgorithm);
        Append(stream, envelope.KeyId);
        return stream.ToArray();
    }

    private PeerReceiveOutcome? Validate(
        SignedPeerEnvelope envelope,
        PeerReceiveContext context)
    {
        if (!string.Equals(envelope.ProtocolVersion, ProtocolVersion, StringComparison.Ordinal))
        {
            return PeerReceiveOutcome.InvalidVersion;
        }

        if (envelope.EnvelopeId == Guid.Empty
            || envelope.SenderId == Guid.Empty
            || envelope.RecipientId == Guid.Empty
            || envelope.GrantId == Guid.Empty
            || string.IsNullOrWhiteSpace(envelope.Purpose)
            || string.IsNullOrWhiteSpace(envelope.Nonce)
            || string.IsNullOrWhiteSpace(envelope.IdempotencyKey)
            || string.IsNullOrWhiteSpace(envelope.PayloadType)
            || string.IsNullOrWhiteSpace(envelope.PayloadDigest)
            || envelope.ExpiresAt <= envelope.IssuedAt)
        {
            return PeerReceiveOutcome.Malformed;
        }

        if (!string.Equals(envelope.SigningAlgorithm, FixtureAlgorithm, StringComparison.Ordinal))
        {
            return PeerReceiveOutcome.UnknownAlgorithm;
        }

        if (!verificationKeys.TryGetValue(envelope.KeyId, out var key))
        {
            return PeerReceiveOutcome.UnknownKey;
        }

        if (!TryDecodeSignature(envelope.Signature, out var signature)
            || !CryptographicOperations.FixedTimeEquals(
                HMACSHA256.HashData(key, Canonicalize(envelope with { Signature = string.Empty })),
                signature))
        {
            return PeerReceiveOutcome.InvalidSignature;
        }

        if (envelope.SenderId != context.ExpectedSenderId
            || envelope.RecipientId != context.ExpectedRecipientId)
        {
            return PeerReceiveOutcome.WrongAudience;
        }

        if (!string.Equals(envelope.Purpose, context.ExpectedPurpose, StringComparison.Ordinal))
        {
            return PeerReceiveOutcome.WrongPurpose;
        }

        if (envelope.GrantId != context.ExpectedGrantId)
        {
            return PeerReceiveOutcome.WrongGrant;
        }

        if (context.ReceivedAt >= envelope.ExpiresAt)
        {
            return PeerReceiveOutcome.Expired;
        }

        if (envelope.IssuedAt > context.ReceivedAt + maximumClockSkew)
        {
            return PeerReceiveOutcome.NotYetValid;
        }

        return null;
    }

    private static bool TryDecodeSignature(string value, out byte[] signature)
    {
        try
        {
            signature = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            signature = [];
            return false;
        }
    }

    private static string IdempotencyFingerprint(SignedPeerEnvelope envelope) =>
        string.Join(
            '\u001f',
            envelope.SenderId,
            envelope.RecipientId,
            envelope.GrantId,
            envelope.Purpose,
            envelope.IdempotencyKey,
            envelope.PayloadType,
            envelope.PayloadDigest);

    private static void Append(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }

    private sealed record IdempotencyReceipt(string Fingerprint, string ReceiptId);
}
