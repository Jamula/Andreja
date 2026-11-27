using Microsoft.AspNetCore.Identity;

namespace Andreja.Adapters.Identity.AspNetCore;

public interface ILocalIdentityRecoveryOperations
{
    Task<RecoveryStartResult?> BeginRecoveryAsync(
        string? recoveryCode,
        CancellationToken cancellationToken = default);

    Task<RecoveryCompletionResult> CompleteRecoveryAsync(
        RecoveryStartResult recovery,
        UserPasskeyInfo newPasskey,
        CancellationToken cancellationToken = default);

    Task AuditRecoveryFailureAsync(
        Guid? userId,
        CancellationToken cancellationToken = default);
}
