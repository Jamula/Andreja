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
