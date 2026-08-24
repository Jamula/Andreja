using Andreja.Platform.Contracts.Skills;

namespace Andreja.Platform.Contracts.Assistant;

[Flags]
public enum AssistantCapability
{
    None = 0,
    TextCompletion = 1,
    TypedTools = 2,
    UsageReporting = 4,
    Cancellation = 8,
}

public enum AssistantResponseStatus
{
    Completed,
    Failed,
    Cancelled,
}

public sealed record AssistantProviderCapabilities(
    string Provider,
    string Model,
    AssistantCapability Capabilities);

public sealed record AssistantExecutionContext(
    Guid TenantId,
    Guid AppUserId,
    Guid PrincipalId,
    string Purpose);

public sealed record AssistantSessionRequest(
    Guid SessionId,
    AssistantExecutionContext Context,
    IReadOnlyList<ToolDefinition> AllowedTools);

public sealed record AssistantRequest(
    Guid RequestId,
    string Content,
    IReadOnlyList<string> AllowedToolNames);

public sealed record AssistantToolCall(
    string ToolName,
    IReadOnlyDictionary<string, System.Text.Json.JsonElement> Arguments);

public sealed record AssistantUsage(
    string Provider,
    string Model,
    long? InputUnits,
    long? OutputUnits,
    TimeSpan Duration,
    string ResultClass,
    int RetryCount,
    int ToolCount);

public sealed record AssistantFailure(string Code, string Message, bool IsTransient);

public sealed record AssistantResponse(
    Guid RequestId,
    AssistantResponseStatus Status,
    string? Content,
    IReadOnlyList<AssistantToolCall> ToolCalls,
    AssistantUsage Usage,
    AssistantFailure? Failure);

public interface IAssistantProvider
{
    ValueTask<AssistantProviderCapabilities> GetCapabilitiesAsync(
        CancellationToken cancellationToken);

    ValueTask<IAssistantSession> CreateSessionAsync(
        AssistantSessionRequest request,
        CancellationToken cancellationToken);
}

public interface IAssistantSession : IAsyncDisposable
{
    ValueTask<AssistantResponse> CompleteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken);

    ValueTask CancelAsync(CancellationToken cancellationToken);
}

public sealed record AssistantProviderProfile(
    Uri Endpoint,
    string Model,
    string CredentialHandle,
    TimeSpan Timeout,
    string RetentionDisclosure,
    long? MaximumInputUnits,
    long? MaximumOutputUnits);
