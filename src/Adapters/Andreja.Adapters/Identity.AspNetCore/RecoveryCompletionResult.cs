namespace Andreja.Adapters.Identity.AspNetCore;

public sealed record RecoveryCompletionResult(
    IReadOnlyList<string> RecoveryCodes);
