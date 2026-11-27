namespace Andreja.Modules.Identity;

public sealed class ScopedTenantPrincipalContext : ITenantPrincipalContextAccessor
{
    public TenantPrincipalContext? Current { get; private set; }

    public void Set(TenantPrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Current is not null)
        {
            throw new InvalidOperationException("The scoped identity context is immutable once resolved.");
        }

        if (context.TenantId.Value == Guid.Empty
            || context.AppUserId.Value == Guid.Empty
            || context.PrincipalId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(context.Purpose))
        {
            throw new IdentityAccessDeniedException(
                "Tenant, user, principal, and purpose must all be resolved.");
        }

        Current = context;
    }
}
