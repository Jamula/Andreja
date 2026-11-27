namespace Andreja.Adapters.PostgreSql;

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
