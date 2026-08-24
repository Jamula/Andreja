using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.Platform.Contracts.Channels;

public sealed record ChannelProviderMetadata(
    ManifestField<string> Provider,
    ManifestField<IReadOnlyList<string>> AccountTypes,
    ManifestField<IReadOnlyList<string>> OAuthScopes,
    ManifestField<string> QueryMode,
    ManifestField<string> SyncMode,
    ManifestField<string> PublishMode,
    ManifestField<string> WebhookSupport,
    ManifestField<string> ChangeFeedSupport,
    ManifestField<string> CachePolicy,
    ManifestField<string> CostModel,
    ManifestField<string> DeliveryTopology);

public sealed record ChannelOperationDefinition(
    string Name,
    string Version,
    string Description,
    string Capability,
    string Operation,
    string DataClass,
    DisclosureLevel MaximumDisclosure,
    IReadOnlyList<string> AllowedPurposes,
    IReadOnlyList<ToolFieldSchema> InputSchema);

public sealed record ChannelManifest(
    string SchemaVersion,
    string ChannelId,
    string Version,
    string DisplayName,
    string Description,
    string Category,
    PublisherMetadata Publisher,
    LifecycleMetadata Lifecycle,
    PermissionMetadata Permissions,
    ExecutionMetadata Execution,
    DataHandlingMetadata DataHandling,
    HelpSupportMetadata HelpSupport,
    CompatibilityMetadata Compatibility,
    IntegrityMetadata Integrity,
    ChannelProviderMetadata Provider,
    IReadOnlyList<ChannelOperationDefinition> Operations);

public sealed record ChannelExecutionContext(
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    string Purpose,
    ExecutionAuthorizationContext Authorization,
    DisclosureLevel? EffectiveDisclosure = null);

public sealed record ChannelInvocation(
    string ChannelId,
    string ChannelVersion,
    string OperationName,
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

public enum ChannelResultStatus
{
    Completed,
    Denied,
    Invalid,
    Failed,
    Cancelled,
}

public sealed record ChannelFailure(string Code, string Message);

public sealed record ChannelResult(
    ChannelResultStatus Status,
    JsonElement? Output,
    ChannelFailure? Failure);

public delegate ValueTask<ChannelResult> ChannelOperationHandler(
    ChannelInvocation invocation,
    ChannelExecutionContext context,
    CancellationToken cancellationToken);

public interface IChannelHost
{
    ValueTask<ChannelManifest?> ResolveManifestAsync(
        string channelId,
        string version,
        CancellationToken cancellationToken);

    ValueTask<ChannelResult> InvokeAsync(
        ChannelInvocation invocation,
        ChannelExecutionContext context,
        CancellationToken cancellationToken);
}
