namespace Andreja.AppHost.OpenLoops;

public static class AndrejaClaimTypes
{
    public const string TenantId =
        Andreja.Adapters.Identity.AspNetCore.LocalIdentityClaimTypes.TenantId;
    public const string AppUserId =
        Andreja.Adapters.Identity.AspNetCore.LocalIdentityClaimTypes.AppUserId;
    public const string PrincipalId =
        Andreja.Adapters.Identity.AspNetCore.LocalIdentityClaimTypes.PrincipalId;
}
