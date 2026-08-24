namespace Andreja.Platform.Contracts.Sharing;

public enum DisclosureLevel
{
    Existence = 0,
    Timing = 1,
    Summary = 2,
    Full = 3,
}

public enum ConsentState
{
    Offered,
    Accepted,
    Active,
    Rejected,
    Expired,
    Revoked,
}

public sealed record ConsentTerms(
    string Purpose,
    DisclosureLevel MaximumDisclosure,
    IReadOnlySet<string> AllowedOperations,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt);

public sealed record ConsentDecision(
    ConsentState State,
    Guid ActorId,
    DateTimeOffset OccurredAt);

public sealed record ConsentRecord(
    Guid ConsentId,
    string Version,
    Guid GrantId,
    Guid OfferingPrincipalId,
    Guid ReceivingPrincipalId,
    ConsentTerms Terms,
    IReadOnlyList<ConsentDecision> Timeline);

public sealed record Grant(
    Guid GrantId,
    string Version,
    Guid OwnerTenantId,
    string ResourceReference,
    string ScopeReference,
    Guid GranteePrincipalId,
    string Purpose,
    DisclosureLevel MaximumDisclosure,
    IReadOnlySet<string> AllowedOperations,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    bool IsRevoked,
    DateTimeOffset? RevokedAt,
    Guid ConsentId);

public enum ShareAuditOutcome
{
    Allowed,
    Denied,
}

public sealed record ShareAuditEntry(
    Guid AuditId,
    string Version,
    Guid TenantId,
    Guid ActorId,
    Guid PeerId,
    Guid GrantId,
    Guid ConsentId,
    string ResourceReference,
    string ScopeReference,
    string Purpose,
    DisclosureLevel DisclosureLevel,
    string Operation,
    ShareAuditOutcome Outcome,
    string? EnvelopeDigest,
    string? PayloadDigest,
    DateTimeOffset OccurredAt);

public interface IShareAuditSink
{
    ValueTask AppendAsync(ShareAuditEntry entry, CancellationToken cancellationToken);
}

public static class DisclosurePolicy
{
    public static DisclosureLevel Reduce(
        DisclosureLevel requested,
        DisclosureLevel granted,
        DisclosureLevel sensitivityMaximum)
    {
        Validate(requested);
        Validate(granted);
        Validate(sensitivityMaximum);

        return (DisclosureLevel)Math.Min(
            (int)requested,
            Math.Min((int)granted, (int)sensitivityMaximum));
    }

    private static void Validate(DisclosureLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown disclosure level.");
        }
    }
}
