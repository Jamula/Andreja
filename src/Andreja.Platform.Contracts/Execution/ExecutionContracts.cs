using Andreja.Platform.Contracts.Sharing;

namespace Andreja.Platform.Contracts.Execution;

public enum ManifestApplicability
{
    Applicable,
    NotApplicable,
}

public sealed record ManifestField<T>(
    ManifestApplicability Applicability,
    T? Value,
    string? Reason);

public static class ManifestField
{
    public static ManifestField<T> Applicable<T>(T value) =>
        new(ManifestApplicability.Applicable, value, null);

    public static ManifestField<T> NotApplicable<T>(string reason) =>
        new(ManifestApplicability.NotApplicable, default, reason);
}

public enum ManifestExecutionMode
{
    FirstPartyInProcess,
    RemoteHttp,
    RemoteMcp,
    SandboxedLocal,
    Declarative,
}

public sealed record PublisherMetadata(
    string PublisherId,
    string DisplayName);

public sealed record LifecycleMetadata(
    int FrameworkStage,
    string Status,
    ManifestField<string> DeprecationNotice,
    ManifestField<string> ReplacementArtifact);

public sealed record PermissionMetadata(
    IReadOnlyList<string> DeclaredCapabilities,
    IReadOnlyList<string> AllowedPurposes,
    IReadOnlyList<string> DataClasses,
    DisclosureLevel MaximumDisclosure);

public sealed record ExecutionMetadata(
    ManifestExecutionMode Mode,
    IReadOnlyList<string> Entrypoints,
    ManifestField<IReadOnlyList<string>> NetworkDestinations,
    ManifestField<string> RemoteProtocol);

public sealed record DataHandlingMetadata(
    ManifestField<string> SettingsSchema,
    string RetentionPolicy,
    ManifestField<string> ResourceLimits);

public sealed record HelpSupportMetadata(
    Uri HelpUri,
    string SupportRoute,
    string Owner);

public sealed record CompatibilityMetadata(
    string MinimumPlatformVersion,
    ManifestField<string> MinimumProtocolVersion,
    IReadOnlyList<string> SupportedPlatformVersions);

public sealed record IntegrityMetadata(
    ManifestField<string> PackageDigest,
    ManifestField<string> Signature,
    ManifestField<string> Provenance,
    ManifestField<string> Sbom);

public sealed record UserExecutionPolicy(
    Guid PolicyId,
    string Version,
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    IReadOnlySet<string> AllowedPurposes,
    IReadOnlySet<string> AllowedCapabilities,
    IReadOnlySet<string> AllowedOperations,
    IReadOnlySet<string> AllowedDataClasses,
    DisclosureLevel MaximumDisclosure,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    bool IsRevoked,
    DateTimeOffset? RevokedAt);

public sealed record ExecutionAuthorizationContext(
    UserExecutionPolicy UserPolicy,
    Grant Grant,
    ConsentRecord Consent,
    DateTimeOffset EvaluatedAt);

public sealed record ExecutionAuthorizationRequest(
    string ArtifactKind,
    string ArtifactId,
    string ArtifactVersion,
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    string Purpose,
    IReadOnlyList<string> RequiredCapabilities,
    string Operation,
    string DataClass,
    DisclosureLevel RequestedDisclosure,
    DisclosureLevel DeclaredDisclosureCeiling,
    string ResourceReference,
    ExecutionAuthorizationContext Authorization);

public sealed record ExecutionAuthorizationDecision(
    bool Allowed,
    string Code,
    DisclosureLevel? EffectiveDisclosure);

public interface IExecutionAuthorizationEvaluator
{
    ValueTask<ExecutionAuthorizationDecision> EvaluateAsync(
        ExecutionAuthorizationRequest request,
        CancellationToken cancellationToken);
}

public enum ExecutionAuditOutcome
{
    Allowed,
    Denied,
}

public sealed record ExecutionAuditEntry(
    Guid AuditId,
    string Version,
    string ArtifactKind,
    string ArtifactId,
    string ArtifactVersion,
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    Guid? PolicyId,
    Guid? GrantId,
    Guid? ConsentId,
    string Purpose,
    string Operation,
    string DataClass,
    DisclosureLevel? RequestedDisclosure,
    DisclosureLevel? EffectiveDisclosure,
    ExecutionAuditOutcome Outcome,
    string ReasonCode,
    DateTimeOffset OccurredAt);

public interface IExecutionAuditSink
{
    ValueTask AppendAsync(ExecutionAuditEntry entry, CancellationToken cancellationToken);
}
