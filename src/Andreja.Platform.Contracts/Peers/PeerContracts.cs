namespace Andreja.Platform.Contracts.Peers;

public sealed record SignedPeerEnvelope(
    string ProtocolVersion,
    Guid EnvelopeId,
    Guid SenderId,
    Guid RecipientId,
    Guid GrantId,
    string Purpose,
    string Nonce,
    string IdempotencyKey,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string PayloadType,
    string PayloadDigest,
    string SigningAlgorithm,
    string KeyId,
    string Signature);

public sealed record PeerReceiveContext(
    Guid ExpectedSenderId,
    Guid ExpectedRecipientId,
    Guid ExpectedGrantId,
    string ExpectedPurpose,
    DateTimeOffset ReceivedAt);

public enum PeerReceiveOutcome
{
    Accepted,
    IdempotentReplay,
    Malformed,
    InvalidVersion,
    InvalidSignature,
    WrongAudience,
    WrongPurpose,
    WrongGrant,
    Expired,
    NotYetValid,
    Replay,
    IdempotencyConflict,
    UnknownAlgorithm,
    UnknownKey,
}

public sealed record PeerReceiveResult(
    PeerReceiveOutcome Outcome,
    bool EffectApplied,
    string? ReceiptId);

public interface IPeerChannel
{
    ValueTask<PeerReceiveResult> ReceiveAsync(
        SignedPeerEnvelope envelope,
        PeerReceiveContext context,
        CancellationToken cancellationToken);
}
