namespace Andreja.Modules.Identity;

public sealed class IdentityAccessDeniedException(string message) : InvalidOperationException(message);

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

public interface ITenantPrincipalContextAccessor
{
    TenantPrincipalContext? Current { get; }
}

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

public sealed record ContactProjection(ContactId Id, string DisplayName, PrincipalId? LinkedPrincipalId);

public interface IIdentityStore
{
    Task<IReadOnlyList<ContactProjection>> ListContactsAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default);

    Task<ContactProjection> AddContactAsync(
        TenantPrincipalContext context,
        Contact contact,
        CancellationToken cancellationToken = default);
}
