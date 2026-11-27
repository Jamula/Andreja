namespace Andreja.Adapters.PostgreSql;

public sealed class IdentityRecoveryCode
{
    private IdentityRecoveryCode()
    {
    }

    public IdentityRecoveryCode(
        Guid id,
        Guid userId,
        byte[] lookupHash,
        byte[] salt,
        byte[] verificationHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        LookupHash = lookupHash;
        Salt = salt;
        VerificationHash = verificationHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] LookupHash { get; private set; } = [];

    public byte[] Salt { get; private set; } = [];

    public byte[] VerificationHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public void Consume(DateTimeOffset consumedAt)
    {
        if (ConsumedAt.HasValue)
        {
            throw new InvalidOperationException("The recovery code has already been consumed.");
        }

        ConsumedAt = consumedAt;
    }
}
