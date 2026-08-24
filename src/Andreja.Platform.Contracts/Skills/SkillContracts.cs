using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Proposals;
using Andreja.Platform.Contracts.Sharing;
using System.Text.Json;

namespace Andreja.Platform.Contracts.Skills;

public enum ToolValueKind
{
    Text,
    Numeric,
    Logical,
    Structured,
    Sequence,
}

public sealed record ToolFieldSchema(
    string Name,
    ToolValueKind Kind,
    bool Required);

public sealed record ToolDefinition(
    string Name,
    string Version,
    string Description,
    string Operation,
    string DataClass,
    DisclosureLevel MaximumDisclosure,
    IReadOnlyList<ToolFieldSchema> InputSchema,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<string> AllowedPurposes);

public sealed record SkillManifest(
    string SchemaVersion,
    string SkillId,
    string Version,
    string DisplayName,
    string Description,
    IReadOnlyList<string> ActivityCategories,
    PublisherMetadata Publisher,
    LifecycleMetadata Lifecycle,
    PermissionMetadata Permissions,
    ExecutionMetadata Execution,
    DataHandlingMetadata DataHandling,
    HelpSupportMetadata HelpSupport,
    CompatibilityMetadata Compatibility,
    IntegrityMetadata Integrity,
    ManifestField<IReadOnlyList<string>> ChannelDependencies,
    IReadOnlyList<ToolDefinition> Tools);

public sealed record SkillExecutionContext(
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    string Purpose,
    ExecutionAuthorizationContext Authorization,
    DisclosureLevel? EffectiveDisclosure = null);

public sealed record SkillInvocation(
    string SkillId,
    string SkillVersion,
    string ToolName,
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    string Purpose,
    string Operation,
    string DataClass,
    DisclosureLevel RequestedDisclosure,
    string ResourceReference,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    string ManifestDigest);

public enum SkillResultStatus
{
    Completed,
    Proposed,
    Denied,
    Invalid,
    Failed,
    Cancelled,
}

public sealed record SkillFailure(string Code, string Message);

public sealed record SkillResult(
    SkillResultStatus Status,
    JsonElement? Output,
    Proposal? Proposal,
    SkillFailure? Failure);

public delegate ValueTask<SkillResult> SkillToolHandler(
    SkillInvocation invocation,
    SkillExecutionContext context,
    CancellationToken cancellationToken);

public interface ISkillHost
{
    ValueTask<SkillManifest?> ResolveManifestAsync(
        string skillId,
        string version,
        CancellationToken cancellationToken);

    ValueTask<SkillResult> InvokeAsync(
        SkillInvocation invocation,
        SkillExecutionContext context,
        CancellationToken cancellationToken);
}
