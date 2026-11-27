namespace Andreja.Modules.OpenLoops;

public enum TaskMutationOutcome
{
    Applied,
    IdempotentReplay,
    NotFound,
    Conflict,
    Denied,
}
