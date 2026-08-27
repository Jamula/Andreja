using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using System.Collections.Concurrent;

namespace Andreja.Modules.Execution;

public sealed class InMemoryExecutionAuditSink : IExecutionAuditSink
{
    private readonly ConcurrentQueue<ExecutionAuditEntry> entries = new();

    public IReadOnlyList<ExecutionAuditEntry> Entries => entries.ToArray();

    public ValueTask AppendAsync(
        ExecutionAuditEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries.Enqueue(entry);
        return ValueTask.CompletedTask;
    }
}

public sealed class ExecutionAuthorizationEvaluator(IExecutionAuditSink auditSink)
    : IExecutionAuthorizationEvaluator
{
    public async ValueTask<ExecutionAuthorizationDecision> EvaluateAsync(
        ExecutionAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = request.Authorization.UserPolicy;
        var grant = request.Authorization.Grant;
        var consent = request.Authorization.Consent;
        var now = request.Authorization.EvaluatedAt;
        string? denial = null;

        if (request.TenantId == Guid.Empty
            || request.AppUserId == Guid.Empty
            || request.PrincipalId == Guid.Empty
            || policy.TenantId == Guid.Empty
            || policy.AppUserId == Guid.Empty
            || policy.PrincipalId == Guid.Empty)
        {
            denial = "identity-required";
        }
        else if (policy.Version != "1" || grant.Version != "1" || consent.Version != "1")
        {
            denial = "authorization-version-unsupported";
        }
        else if (request.TenantId != policy.TenantId)
        {
            denial = "wrong-tenant";
        }
        else if (request.AppUserId != policy.AppUserId)
        {
            denial = "wrong-user";
        }
        else if (request.PrincipalId != policy.PrincipalId)
        {
            denial = "wrong-principal";
        }
        else if (policy.IsRevoked
            || policy.RevokedAt is not null
            || now < policy.ValidFrom
            || now >= policy.ExpiresAt)
        {
            denial = "user-policy-inactive";
        }
        else if (!policy.AllowedPurposes.Contains(request.Purpose))
        {
            denial = "wrong-purpose";
        }
        else if (request.RequiredCapabilities.Count == 0
            || request.RequiredCapabilities.Any(capability =>
                !policy.AllowedCapabilities.Contains(capability)))
        {
            denial = "capability-denied";
        }
        else if (!policy.AllowedOperations.Contains(request.Operation))
        {
            denial = "operation-denied";
        }
        else if (!policy.AllowedDataClasses.Contains(request.DataClass))
        {
            denial = "data-class-denied";
        }
        else if (grant.OwnerTenantId != request.TenantId)
        {
            denial = "grant-tenant-mismatch";
        }
        else if (grant.GranteePrincipalId != request.PrincipalId)
        {
            denial = "grant-principal-mismatch";
        }
        else if (!string.Equals(
                     grant.ResourceReference,
                     request.ResourceReference,
                     StringComparison.Ordinal)
            || !string.Equals(grant.ScopeReference, request.DataClass, StringComparison.Ordinal))
        {
            denial = "grant-scope-mismatch";
        }
        else if (!IsActiveBilateralConsent(request, now))
        {
            denial = "consent-inactive";
        }
        else if (!IsActiveGrant(request, now))
        {
            denial = "grant-inactive";
        }
        else if (!IsKnownDisclosure(request.RequestedDisclosure)
            || !IsKnownDisclosure(request.DeclaredDisclosureCeiling)
            || !IsKnownDisclosure(policy.MaximumDisclosure)
            || !IsKnownDisclosure(grant.MaximumDisclosure)
            || !IsKnownDisclosure(consent.Terms.MaximumDisclosure))
        {
            denial = "disclosure-unknown";
        }
        else
        {
            var ceiling = new[]
            {
                request.DeclaredDisclosureCeiling,
                policy.MaximumDisclosure,
                grant.MaximumDisclosure,
                consent.Terms.MaximumDisclosure,
            }.Min();
            if (request.RequestedDisclosure > ceiling)
            {
                denial = "disclosure-denied";
            }
        }

        var allowed = denial is null;
        var decision = new ExecutionAuthorizationDecision(
            allowed,
            denial ?? "authorized",
            allowed ? request.RequestedDisclosure : null);
        await auditSink.AppendAsync(
            CreateAudit(request, decision),
            CancellationToken.None).ConfigureAwait(false);
        return decision;
    }

    private static bool IsActiveGrant(
        ExecutionAuthorizationRequest request,
        DateTimeOffset now)
    {
        var grant = request.Authorization.Grant;
        var consent = request.Authorization.Consent;
        return grant.ConsentId == consent.ConsentId
            && consent.GrantId == grant.GrantId
            && string.Equals(grant.Purpose, request.Purpose, StringComparison.Ordinal)
            && string.Equals(consent.Terms.Purpose, request.Purpose, StringComparison.Ordinal)
            && grant.AllowedOperations.Contains(request.Operation)
            && consent.Terms.AllowedOperations.Contains(request.Operation)
            && !grant.IsRevoked
            && grant.RevokedAt is null
            && now >= grant.ValidFrom
            && now < grant.ExpiresAt
            && now >= consent.Terms.ValidFrom
            && now < consent.Terms.ExpiresAt;
    }

    private static bool IsActiveBilateralConsent(
        ExecutionAuthorizationRequest request,
        DateTimeOffset now)
    {
        var consent = request.Authorization.Consent;
        if (consent.OfferingPrincipalId == consent.ReceivingPrincipalId
            || consent.ReceivingPrincipalId != request.PrincipalId
            || consent.Timeline.Count != 3)
        {
            return false;
        }

        var offered = consent.Timeline[0];
        var accepted = consent.Timeline[1];
        var active = consent.Timeline[2];
        return offered.State == ConsentState.Offered
            && offered.ActorId == consent.OfferingPrincipalId
            && accepted.State == ConsentState.Accepted
            && accepted.ActorId == consent.ReceivingPrincipalId
            && active.State == ConsentState.Active
            && active.ActorId == consent.OfferingPrincipalId
            && offered.OccurredAt < accepted.OccurredAt
            && accepted.OccurredAt < active.OccurredAt
            && active.OccurredAt <= now;
    }

    private static bool IsKnownDisclosure(DisclosureLevel level) =>
        Enum.IsDefined(level);

    private static ExecutionAuditEntry CreateAudit(
        ExecutionAuthorizationRequest request,
        ExecutionAuthorizationDecision decision) =>
        new(
            Guid.CreateVersion7(),
            "1",
            request.ArtifactKind,
            request.ArtifactId,
            request.ArtifactVersion,
            request.TenantId,
            request.AppUserId,
            request.PrincipalId,
            request.Authorization.UserPolicy.PolicyId,
            request.Authorization.Grant.GrantId,
            request.Authorization.Consent.ConsentId,
            request.Purpose,
            request.Operation,
            request.DataClass,
            request.RequestedDisclosure,
            decision.EffectiveDisclosure,
            decision.Allowed ? ExecutionAuditOutcome.Allowed : ExecutionAuditOutcome.Denied,
            decision.Code,
            request.Authorization.EvaluatedAt);
}

internal static class ExecutionAudit
{
    public static ValueTask DeniedAsync(
        IExecutionAuditSink sink,
        string artifactKind,
        string artifactId,
        string artifactVersion,
        Guid tenantId,
        Guid appUserId,
        Guid principalId,
        string purpose,
        string operation,
        string dataClass,
        DisclosureLevel? requestedDisclosure,
        ExecutionAuthorizationContext? authorization,
        string reasonCode) =>
        sink.AppendAsync(
            new(
                Guid.CreateVersion7(),
                "1",
                artifactKind,
                artifactId,
                artifactVersion,
                tenantId,
                appUserId,
                principalId,
                authorization?.UserPolicy.PolicyId,
                authorization?.Grant.GrantId,
                authorization?.Consent.ConsentId,
                purpose,
                operation,
                dataClass,
                requestedDisclosure,
                null,
                ExecutionAuditOutcome.Denied,
                reasonCode,
                authorization?.EvaluatedAt ?? DateTimeOffset.UtcNow),
            CancellationToken.None);
}
