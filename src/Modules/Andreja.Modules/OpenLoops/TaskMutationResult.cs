namespace Andreja.Modules.OpenLoops;

public sealed record TaskMutationResult(
    TaskMutationOutcome Outcome,
    OpenLoopTask? Task);
