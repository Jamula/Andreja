using Andreja.AppHost.Hosting;
using Andreja.AppHost.OpenLoops;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class DatabaseMigrationOperationsTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void NormalStartupContainsNoMigrationMutation()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(
            Path.Combine(root, "src", "Andreja.AppHost", "Program.cs"));
        var registration = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Andreja.AppHost",
            "OpenLoops",
            "OpenLoopsServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("MigrateAsync", program, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ApplyAndrejaOpenLoopsMigrationsAsync",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MigrateAsync", registration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PendingMigrationsFailReadinessWithoutApplying()
    {
        var executor = new FakeMigrationExecutor(["MigrationA"]);
        await using var provider = CreateProvider(executor);
        var check = new DatabaseMigrationReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(0, executor.MigrateCalls);
    }

    [Fact]
    public async Task PendingMigrationsFailStartupWithoutApplying()
    {
        var executor = new FakeMigrationExecutor(["MigrationA"]);
        await using var provider = CreateProvider(executor);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseMigrationStartupVerifier.VerifyAsync(provider));

        Assert.Contains("explicit operator migration", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, executor.MigrateCalls);
    }

    [Fact]
    public async Task ExplicitCommandAppliesExactlyApprovedMigrations()
    {
        await using var fixture = await MigrationFixture.CreateAsync(["MigrationA"]);
        var executor = new FakeMigrationExecutor(["MigrationA"]);
        await using var provider = CreateProvider(executor);
        using var output = new StringWriter();

        var exitCode = await DatabaseMigrationCommand.TryRunAsync(
            fixture.Arguments,
            provider,
            output);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, executor.MigrateCalls);
        Assert.Empty(executor.Pending);
        Assert.Contains("Applied 1 approved", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitCommandRejectsApprovalMismatchWithoutApplying()
    {
        await using var fixture = await MigrationFixture.CreateAsync(["MigrationA"]);
        var executor = new FakeMigrationExecutor(["MigrationB"]);
        await using var provider = CreateProvider(executor);
        using var output = new StringWriter();

        var exitCode = await DatabaseMigrationCommand.TryRunAsync(
            fixture.Arguments,
            provider,
            output);

        Assert.Equal(65, exitCode);
        Assert.Equal(0, executor.MigrateCalls);
        Assert.Contains(
            "pending migrations do not match approval",
            output.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitCommandReportsFailureWithoutClaimingSuccess()
    {
        await using var fixture = await MigrationFixture.CreateAsync(["MigrationA"]);
        var executor = new FakeMigrationExecutor(["MigrationA"])
        {
            MigrateException = new InvalidOperationException("synthetic"),
        };
        await using var provider = CreateProvider(executor);
        using var output = new StringWriter();

        var exitCode = await DatabaseMigrationCommand.TryRunAsync(
            fixture.Arguments,
            provider,
            output);

        Assert.Equal(1, exitCode);
        Assert.Equal(1, executor.MigrateCalls);
        Assert.DoesNotContain("synthetic", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitCommandHonorsCancellation()
    {
        await using var fixture = await MigrationFixture.CreateAsync(["MigrationA"]);
        using var cancellation = new CancellationTokenSource();
        var executor = new FakeMigrationExecutor(["MigrationA"])
        {
            OnMigrate = cancellation.Cancel,
        };
        await using var provider = CreateProvider(executor);
        using var output = new StringWriter();

        var exitCode = await DatabaseMigrationCommand.TryRunAsync(
            fixture.Arguments,
            provider,
            output,
            cancellation.Token);

        Assert.Equal(130, exitCode);
        Assert.Equal(1, executor.MigrateCalls);
        Assert.Contains("cancelled", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCompositionRegistersMigrationReadinessAndExecutor()
    {
        var directory = CreateScratchDirectory();
        var passwordPath = Path.Combine(directory, "postgres-password");
        File.WriteAllText(passwordPath, "synthetic-password");
        try
        {
            Dictionary<string, string?> values = new()
            {
                [$"{AndrejaOperationsOptions.SectionName}:Database:Enabled"] = "true",
                [$"{AndrejaOperationsOptions.SectionName}:Database:Host"] = "postgres",
                [$"{AndrejaOperationsOptions.SectionName}:Database:Port"] = "5432",
                [$"{AndrejaOperationsOptions.SectionName}:Database:Name"] = "andreja",
                [$"{AndrejaOperationsOptions.SectionName}:Database:Username"] = "andreja",
                [$"{AndrejaOperationsOptions.SectionName}:Database:PasswordFile"] = passwordPath,
                [$"{OpenLoopsOptions.SectionName}:Enabled"] = "true",
                [$"{OpenLoopsOptions.SectionName}:PublicOrigin"] = "https://localhost",
                [$"{OpenLoopsOptions.SectionName}:AssistantProvider"] = "deterministic",
                ["Andreja:Identity:AuthenticationScheme"] = "Identity.Application",
                ["Andreja:Identity:RelyingPartyId"] = "localhost",
                ["Andreja:Identity:AllowedOrigins:0"] = "https://localhost",
                ["Andreja:Identity:BootstrapTokenFile"] = "unused",
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            var environment = new TestHostEnvironment
            {
                EnvironmentName = Environments.Production,
            };
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddAndrejaOpenLoops(configuration, environment);

            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            using var scope = provider.CreateScope();
            Assert.IsType<EfDatabaseMigrationExecutor>(
                scope.ServiceProvider.GetRequiredService<IDatabaseMigrationExecutor>());
            var registrations = provider
                .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
                .Value.Registrations;
            Assert.Contains(
                registrations,
                registration => registration.Name == "database-migrations" &&
                    registration.Tags.Contains("ready"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ServiceProvider CreateProvider(FakeMigrationExecutor executor)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<AndrejaOperationsOptions>>(Options.Create(
            new AndrejaOperationsOptions
            {
                DataProtectionKeysPath = ".andreja/keys",
                Database = new DatabaseReadinessOptions
                {
                    Enabled = true,
                    Name = "andreja",
                    PasswordFile = "unused",
                },
            }));
        services.AddScoped<IDatabaseMigrationExecutor>(_ => executor);
        return services.BuildServiceProvider();
    }

    private static string CreateScratchDirectory()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "migration-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class FakeMigrationExecutor(IEnumerable<string> pending)
        : IDatabaseMigrationExecutor
    {
        public List<string> Pending { get; } = [.. pending];

        public int MigrateCalls { get; private set; }

        public Exception? MigrateException { get; init; }

        public Action? OnMigrate { get; init; }

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>([.. Pending]);
        }

        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            MigrateCalls++;
            OnMigrate?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (MigrateException is not null)
            {
                throw MigrateException;
            }

            Pending.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class MigrationFixture(string directory, string approvalPath)
        : IAsyncDisposable
    {
        public string[] Arguments =>
        [
            DatabaseMigrationCommand.CommandName,
            DatabaseMigrationCommand.ApprovalOption,
            approvalPath,
        ];

        public static async Task<MigrationFixture> CreateAsync(
            IReadOnlyList<string> approvedMigrations)
        {
            var directory = CreateScratchDirectory();
            var backupPath = Path.Combine(directory, "backup.dump");
            var migrationPath = Path.Combine(directory, "migration.sql");
            var approvalPath = Path.Combine(directory, "approval.json");
            await File.WriteAllTextAsync(backupPath, "synthetic-backup");
            await File.WriteAllTextAsync(migrationPath, "synthetic-migration");
            var approval = new DatabaseMigrationApproval
            {
                DatabaseName = "andreja",
                BackupPath = backupPath,
                BackupSha256 = ComputeSha256(backupPath),
                MigrationScriptPath = migrationPath,
                MigrationScriptSha256 = ComputeSha256(migrationPath),
                ApprovedMigrations = approvedMigrations,
            };
            await File.WriteAllTextAsync(
                approvalPath,
                JsonSerializer.Serialize(approval, SerializerOptions));
            return new MigrationFixture(directory, approvalPath);
        }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(directory, recursive: true);
            return ValueTask.CompletedTask;
        }

        private static string ComputeSha256(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Andreja.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
