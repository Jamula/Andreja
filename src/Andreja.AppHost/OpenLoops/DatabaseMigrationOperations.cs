using Andreja.Adapters.PostgreSql;
using Andreja.AppHost.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andreja.AppHost.OpenLoops;

public interface IDatabaseMigrationExecutor
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        CancellationToken cancellationToken = default);

    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public sealed class EfDatabaseMigrationExecutor(AndrejaIdentityDbContext database)
    : IDatabaseMigrationExecutor
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        CancellationToken cancellationToken = default) =>
        (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        database.Database.MigrateAsync(cancellationToken);
}

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
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database migration state could not be verified.");
        }
    }
}

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

public sealed record DatabaseMigrationApproval
{
    public required string DatabaseName { get; init; }

    public required string BackupPath { get; init; }

    public required string BackupSha256 { get; init; }

    public required string MigrationScriptPath { get; init; }

    public required string MigrationScriptSha256 { get; init; }

    public required IReadOnlyList<string> ApprovedMigrations { get; init; }
}

public static class DatabaseMigrationCommand
{
    public const string CommandName = "--migrate-database";
    public const string ApprovalOption = "--approval-file";

    private const int UsageError = 64;
    private const int ApprovalMismatch = 65;
    private const int Cancelled = 130;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Contains(CommandName, StringComparer.Ordinal);

    public static async Task<int?> TryRunAsync(
        IReadOnlyList<string> args,
        IServiceProvider services,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        if (!IsRequested(args))
        {
            return null;
        }

        try
        {
            var approvalPath = ParseApprovalPath(args);
            var approval = await ReadApprovalAsync(approvalPath, cancellationToken);
            await using var scope = services.CreateAsyncScope();
            var databaseOptions = scope.ServiceProvider
                .GetRequiredService<IOptions<AndrejaOperationsOptions>>().Value.Database;
            if (!databaseOptions.Enabled)
            {
                await output.WriteLineAsync(
                    "Database migration refused: PostgreSQL is not enabled.");
                return ApprovalMismatch;
            }

            var validationError = await ValidateApprovalAsync(
                approval,
                databaseOptions.Name,
                cancellationToken);
            if (validationError is not null)
            {
                await output.WriteLineAsync($"Database migration refused: {validationError}");
                return ApprovalMismatch;
            }

            var executor = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationExecutor>();
            var pending = await executor.GetPendingMigrationsAsync(cancellationToken);
            if (!pending.SequenceEqual(approval.ApprovedMigrations, StringComparer.Ordinal))
            {
                await output.WriteLineAsync(
                    "Database migration refused: pending migrations do not match approval.");
                return ApprovalMismatch;
            }

            await executor.MigrateAsync(cancellationToken);
            var remaining = await executor.GetPendingMigrationsAsync(cancellationToken);
            if (remaining.Count != 0)
            {
                await output.WriteLineAsync(
                    "Database migration failed: pending migrations remain.");
                return 1;
            }

            await output.WriteLineAsync(
                $"Applied {pending.Count} approved database migration(s).");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync("Database migration cancelled.");
            return Cancelled;
        }
        catch (ArgumentException exception)
        {
            await output.WriteLineAsync($"Database migration usage error: {exception.Message}");
            return UsageError;
        }
        catch (Exception)
        {
            await output.WriteLineAsync("Database migration failed without changing readiness.");
            return 1;
        }
    }

    private static string ParseApprovalPath(IReadOnlyList<string> args)
    {
        if (args.Count != 3 ||
            !string.Equals(args[0], CommandName, StringComparison.Ordinal) ||
            !string.Equals(args[1], ApprovalOption, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(args[2]))
        {
            throw new ArgumentException(
                $"Usage: {CommandName} {ApprovalOption} <approval.json>");
        }

        return Path.GetFullPath(args[2]);
    }

    private static async Task<DatabaseMigrationApproval> ReadApprovalAsync(
        string approvalPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(approvalPath);
        return await JsonSerializer.DeserializeAsync<DatabaseMigrationApproval>(
            stream,
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException("Migration approval is empty.");
    }

    private static async Task<string?> ValidateApprovalAsync(
        DatabaseMigrationApproval approval,
        string configuredDatabaseName,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
            approval.DatabaseName,
            configuredDatabaseName,
            StringComparison.Ordinal))
        {
            return "approved database does not match configuration.";
        }
        if (approval.ApprovedMigrations.Count == 0 ||
            approval.ApprovedMigrations.Any(string.IsNullOrWhiteSpace) ||
            approval.ApprovedMigrations.Distinct(StringComparer.Ordinal).Count() !=
                approval.ApprovedMigrations.Count)
        {
            return "approved migration list is empty or invalid.";
        }

        return await VerifyArtifactAsync(
            approval.BackupPath,
            approval.BackupSha256,
            "backup",
            cancellationToken)
            ?? await VerifyArtifactAsync(
                approval.MigrationScriptPath,
                approval.MigrationScriptSha256,
                "reviewed migration script",
                cancellationToken);
    }

    private static async Task<string?> VerifyArtifactAsync(
        string path,
        string expectedSha256,
        string description,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !IsSha256(expectedSha256) ||
            !File.Exists(path))
        {
            return $"{description} artifact is missing or invalid.";
        }

        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        return string.Equals(actual, expectedSha256, StringComparison.Ordinal)
            ? null
            : $"{description} checksum does not match approval.";
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
