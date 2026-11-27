namespace Andreja.Adapters.PostgreSql;

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
