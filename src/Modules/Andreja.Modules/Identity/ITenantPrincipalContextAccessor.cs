namespace Andreja.Modules.Identity;

public interface ITenantPrincipalContextAccessor
{
    TenantPrincipalContext? Current { get; }
}
