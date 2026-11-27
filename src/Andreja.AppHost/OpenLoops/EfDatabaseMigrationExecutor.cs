using Andreja.Adapters.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Andreja.AppHost.OpenLoops;

public sealed class EfDatabaseMigrationExecutor(AndrejaIdentityDbContext database)
    : IDatabaseMigrationExecutor
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        CancellationToken cancellationToken = default) =>
        (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        database.Database.MigrateAsync(cancellationToken);
}
