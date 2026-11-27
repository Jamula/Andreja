namespace Andreja.AppHost.OpenLoops;

public interface IDatabaseMigrationExecutor
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        CancellationToken cancellationToken = default);

    Task MigrateAsync(CancellationToken cancellationToken = default);
}
