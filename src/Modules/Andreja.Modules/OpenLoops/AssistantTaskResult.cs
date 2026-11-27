using Andreja.Platform.Contracts.Proposals;

namespace Andreja.Modules.OpenLoops;

public sealed record AssistantTaskResult(
    Proposal? Proposal,
    string? Message,
    string? ErrorCode);
