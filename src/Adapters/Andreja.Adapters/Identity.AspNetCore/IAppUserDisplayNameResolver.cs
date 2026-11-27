using Andreja.Adapters.PostgreSql;

namespace Andreja.Adapters.Identity.AspNetCore;

public interface IAppUserDisplayNameResolver
{
    Task<string?> ResolveAsync(
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default);
}
