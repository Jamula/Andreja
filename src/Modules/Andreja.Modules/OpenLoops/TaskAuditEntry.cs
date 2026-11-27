namespace Andreja.Modules.OpenLoops;

public sealed record TaskAuditEntry(
    Guid AuditId,
    Guid TenantId,
    Guid ActorId,
    Guid ResourceId,
    string Operation,
    string Outcome,
    string SourceKind,
    string SourceReference,
    DateTimeOffset OccurredAt);
