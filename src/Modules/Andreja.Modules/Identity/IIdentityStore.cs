namespace Andreja.Modules.Identity;

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
