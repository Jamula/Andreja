using Andreja.Adapters.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Andreja.Adapters.Identity.AspNetCore;

public interface IAppUserDisplayNameResolver
{
    Task<string?> ResolveAsync(
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlAppUserDisplayNameResolver(
    AndrejaIdentityDbContext database) : IAppUserDisplayNameResolver
{
    public Task<string?> ResolveAsync(
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return database.AppUsers
            .IgnoreQueryFilters()
            .Where(appUser => appUser.Id == user.AppUserId)
            .Select(appUser => appUser.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
