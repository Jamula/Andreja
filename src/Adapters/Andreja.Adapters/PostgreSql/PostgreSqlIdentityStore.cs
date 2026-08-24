using Andreja.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.PostgreSql;

public sealed class PostgreSqlIdentityStore(
    AndrejaIdentityDbContext database,
    ITenantPrincipalContextAccessor contextAccessor) : IIdentityStore
{
    public async Task<IReadOnlyList<ContactProjection>> ListContactsAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentContext(context);
        return await database.Contacts
            .AsNoTracking()
            .OrderBy(contact => contact.NormalizedName)
            .Select(contact => new ContactProjection(
                contact.Id,
                contact.DisplayName,
                contact.LinkedPrincipalId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContactProjection> AddContactAsync(
        TenantPrincipalContext context,
        Contact contact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        EnsureCurrentContext(context);
        if (contact.TenantId != context.TenantId)
        {
            throw new IdentityAccessDeniedException(
                "The contact belongs to a different tenant.");
        }

        database.Contacts.Add(contact);
        await database.SaveChangesAsync(cancellationToken);
        return new ContactProjection(contact.Id, contact.DisplayName, contact.LinkedPrincipalId);
    }

    private void EnsureCurrentContext(TenantPrincipalContext supplied)
    {
        var current = TenantPrincipalContext.Require(contextAccessor);
        if (supplied != current)
        {
            throw new IdentityAccessDeniedException(
                "The supplied access context does not match the immutable request scope.");
        }
    }
}
