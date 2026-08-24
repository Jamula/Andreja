namespace Andreja.Adapters.PostgreSql;

public sealed class IdentityBootstrapState
{
    public static readonly Guid SingletonId =
        Guid.Parse("0198D19E-5D34-7000-8000-000000000001");

    private IdentityBootstrapState()
    {
    }

    public IdentityBootstrapState(Guid userId, DateTimeOffset consumedAt)
    {
        Id = SingletonId;
        UserId = userId;
        ConsumedAt = consumedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset ConsumedAt { get; private set; }
}

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

public sealed class IdentitySecurityAuditRecord
{
    private IdentitySecurityAuditRecord()
    {
    }

    public IdentitySecurityAuditRecord(
        Guid id,
        Guid? userId,
        string operation,
        bool succeeded,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        Id = id;
        UserId = userId;
        Operation = operation;
        Succeeded = succeeded;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public bool Succeeded { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}

public sealed class IdentityRecentAuthenticationGrant
{
    private IdentityRecentAuthenticationGrant()
    {
    }

    public IdentityRecentAuthenticationGrant(
        Guid id,
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        NonceHash = nonceHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] NonceHash { get; private set; } = [];

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }
}
