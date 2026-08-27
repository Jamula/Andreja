using Andreja.Platform.Contracts.Sharing;

namespace Andreja.Modules.Sharing;

public static class ConsentPolicy
{
    public static ConsentRecord Transition(
        ConsentRecord record,
        ConsentState target,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Timeline.Count == 0 || record.Timeline[0].State != ConsentState.Offered)
        {
            throw new InvalidOperationException("Consent must begin with an offer.");
        }

        var current = record.Timeline[^1];
        if (occurredAt <= current.OccurredAt)
        {
            throw new InvalidOperationException("Consent decisions must be strictly ordered.");
        }

        var actorIsParty = actorId == record.OfferingPrincipalId
            || actorId == record.ReceivingPrincipalId;
        if (!actorIsParty)
        {
            throw new UnauthorizedAccessException("Only a consent party may change consent.");
        }

        if (target == ConsentState.Expired)
        {
            if (occurredAt < record.Terms.ExpiresAt)
            {
                throw new InvalidOperationException("Consent has not expired.");
            }
        }
        else if (occurredAt >= record.Terms.ExpiresAt)
        {
            throw new InvalidOperationException("Expired consent cannot transition.");
        }

        var allowed = (current.State, target) switch
        {
            (ConsentState.Offered, ConsentState.Accepted) =>
                actorId == record.ReceivingPrincipalId,
            (ConsentState.Offered, ConsentState.Rejected) =>
                actorId == record.ReceivingPrincipalId,
            (ConsentState.Offered, ConsentState.Expired) => true,
            (ConsentState.Accepted, ConsentState.Active) =>
                actorId == record.OfferingPrincipalId,
            (ConsentState.Accepted, ConsentState.Rejected) =>
                actorId == record.ReceivingPrincipalId,
            (ConsentState.Accepted, ConsentState.Expired) => true,
            (ConsentState.Active, ConsentState.Revoked) => true,
            (ConsentState.Active, ConsentState.Expired) => true,
            _ => false,
        };

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Consent cannot transition from {current.State} to {target}.");
        }

        return record with
        {
            Timeline = [.. record.Timeline, new ConsentDecision(target, actorId, occurredAt)],
        };
    }

    public static bool IsGrantActive(
        Grant grant,
        ConsentRecord consent,
        Guid grantee,
        string purpose,
        string operation,
        DateTimeOffset now)
    {
        return grant.ConsentId == consent.ConsentId
            && consent.GrantId == grant.GrantId
            && consent.Timeline.Count > 0
            && consent.Timeline[^1].State == ConsentState.Active
            && grant.GranteePrincipalId == grantee
            && string.Equals(grant.Purpose, purpose, StringComparison.Ordinal)
            && string.Equals(consent.Terms.Purpose, purpose, StringComparison.Ordinal)
            && grant.AllowedOperations.Contains(operation)
            && consent.Terms.AllowedOperations.Contains(operation)
            && !grant.IsRevoked
            && now >= grant.ValidFrom
            && now < grant.ExpiresAt
            && now >= consent.Terms.ValidFrom
            && now < consent.Terms.ExpiresAt;
    }
}
