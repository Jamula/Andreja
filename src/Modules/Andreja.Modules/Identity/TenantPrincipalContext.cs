namespace Andreja.Modules.Identity;

public sealed record TenantPrincipalContext(
    TenantId TenantId,
    AppUserId AppUserId,
    PrincipalId PrincipalId,
    string Purpose)
{
    public static TenantPrincipalContext Require(ITenantPrincipalContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return accessor.Current
            ?? throw new IdentityAccessDeniedException(
                "A resolved tenant and principal context is required.");
    }
}
