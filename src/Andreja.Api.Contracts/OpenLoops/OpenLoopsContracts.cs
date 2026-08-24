using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Andreja.Api.Contracts.OpenLoops;

public static class OpenLoopsApi
{
    public const string RoutePrefix = "/api/v1/open-loops";
    public const string AntiforgeryRoute = "/api/v1/security/antiforgery";
    public const string AntiforgeryHeader = "X-CSRF-TOKEN";
}

public sealed record AssistantTaskRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Message { get; init; }
}

public sealed record ConfirmProposalRequest
{
    [Range(1, long.MaxValue)]
    public required long ExpectedVersion { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public required string IdempotencyKey { get; init; }
}

public sealed record CompleteTaskRequest
{
    [Range(1, long.MaxValue)]
    public required long ExpectedVersion { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public required string IdempotencyKey { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<TaskStatusDto>))]
public enum TaskStatusDto
{
    Open,
    Completed,
}

public sealed record TaskDto(
    Guid Id,
    long Version,
    string Title,
    string? Details,
    DateTimeOffset? DueAt,
    TaskStatusDto Status,
    string SourceKind,
    string SourceReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ProposalPolicyDto(
    string Purpose,
    string RequiredCapability,
    bool ConfirmationRequired,
    string Explanation);

public sealed record TaskProposalDto(
    Guid Id,
    long Version,
    string State,
    string Operation,
    string ResourceReference,
    string BeforeCanonical,
    string AfterCanonical,
    string PayloadDigest,
    string SourceKind,
    string SourceReference,
    DateTimeOffset ExpiresAt,
    ProposalPolicyDto Policy);

public sealed record AssistantTaskResponse(
    TaskProposalDto? Proposal,
    string? Message,
    string? ErrorCode);

public sealed record AssistantProviderDto(
    string Provider,
    string Model,
    string Selection,
    bool Ready,
    string Disclosure);

public sealed record ProposalOutcomeDto(
    string Outcome,
    TaskDto? Task,
    TaskProposalDto? Proposal);

public sealed record TaskMutationOutcomeDto(
    string Outcome,
    TaskDto? Task);

public sealed record TaskExportDto(
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TaskDto> Tasks,
    IReadOnlyList<string> Exclusions);

public sealed record ApiErrorDto(string Code, string Message);

public sealed record AntiforgeryTokenDto(string Token);
