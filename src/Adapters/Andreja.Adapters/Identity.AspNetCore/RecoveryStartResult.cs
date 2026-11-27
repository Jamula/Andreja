namespace Andreja.Adapters.Identity.AspNetCore;

public sealed record RecoveryStartResult(
    Guid RecoveryCodeId,
    Guid UserId,
    string SecurityStamp,
    string UserName,
    string DisplayName);
