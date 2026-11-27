using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Andreja.AppHost.OpenLoops;

public sealed class DatabaseMigrationReadinessHealthCheck(IServiceScopeFactory scopeFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var migrations = scope.ServiceProvider
                .GetRequiredService<IDatabaseMigrationExecutor>();
            var pending = await migrations.GetPendingMigrationsAsync(cancellationToken);
            return pending.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"Database schema requires {pending.Count} reviewed migration(s).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is System.Data.Common.DbException or InvalidOperationException
                or TimeoutException)
        {
            return HealthCheckResult.Unhealthy(
                "Database migration state could not be verified.");
        }
    }
}
