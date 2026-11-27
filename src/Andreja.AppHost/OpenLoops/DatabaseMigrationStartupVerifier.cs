namespace Andreja.AppHost.OpenLoops;

public static class DatabaseMigrationStartupVerifier
{
    public static async Task VerifyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var migrations = scope.ServiceProvider.GetService<IDatabaseMigrationExecutor>();
        if (migrations is null)
        {
            return;
        }

        var pending = await migrations.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Count != 0)
        {
            throw new InvalidOperationException(
                $"Database schema requires {pending.Count} reviewed migration(s). " +
                "Run the explicit operator migration command before web startup.");
        }
    }
}
