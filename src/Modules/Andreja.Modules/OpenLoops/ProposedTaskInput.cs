namespace Andreja.Modules.OpenLoops;

public sealed record ProposedTaskInput(
    string Title,
    string? Details,
    DateTimeOffset? DueAt);
