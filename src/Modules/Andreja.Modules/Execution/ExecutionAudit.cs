using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;

namespace Andreja.Modules.Execution;

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
