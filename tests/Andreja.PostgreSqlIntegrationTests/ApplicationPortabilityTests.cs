using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography;

namespace Andreja.PostgreSqlIntegrationTests;

public sealed class ApplicationPortabilityTests : IAsyncLifetime
{
    private readonly string adminConnectionString =
        Environment.GetEnvironmentVariable("ANDREJA_TEST_POSTGRES")
        ?? throw new InvalidOperationException(
            "BLOCKED: set ANDREJA_TEST_POSTGRES to a disposable local PostgreSQL database.");
    private string sourceConnectionString = string.Empty;
    private string targetConnectionString = string.Empty;
    private string sourceDatabase = string.Empty;
    private string targetDatabase = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (string.IsNullOrWhiteSpace(configured.Database)
            || !configured.Database.StartsWith("andreja_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ANDREJA_TEST_POSTGRES must target a disposable database named andreja_test_*.");
        }
        var suffix = Guid.NewGuid().ToString("N")[..8];
        sourceDatabase = $"andreja_test_port_src_{suffix}";
        targetDatabase = $"andreja_test_port_dst_{suffix}";
        await CreateDatabaseAsync(sourceDatabase);
        await CreateDatabaseAsync(targetDatabase);
        configured.Database = sourceDatabase;
        sourceConnectionString = configured.ConnectionString;
        configured.Database = targetDatabase;
        targetConnectionString = configured.ConnectionString;
        await MigrateAsync(sourceConnectionString);
        await MigrateAsync(targetConnectionString);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (!string.IsNullOrEmpty(sourceDatabase))
        {
            await DropDatabaseAsync(sourceDatabase);
        }
        if (!string.IsNullOrEmpty(targetDatabase))
        {
            await DropDatabaseAsync(targetDatabase);
        }
    }

    [Fact]
    public async Task ExportDryRunAndAtomicImportRoundTripPortableDataOnly()
    {
        var tenantId = new TenantId(Guid.CreateVersion7());
        var appUserId = new AppUserId(Guid.CreateVersion7());
        var principalId = new PrincipalId(Guid.CreateVersion7());
        var context = new ScopedTenantPrincipalContext();
        context.Set(new(
            tenantId,
            appUserId,
            principalId,
            "open-loops"));
        await using (var database = CreateContext(sourceConnectionString, context))
        {
            database.Tenants.Add(new(tenantId, "PORTABLE", "Portable", "local"));
            database.AppUsers.Add(new(appUserId, "Portable user"));
            database.ExternalIdentities.AddRange(
                new(
                    ExternalIdentityId.New(),
                    appUserId,
                    "https://issuer.example/realm-a/",
                    "portable-a"),
                new(
                    ExternalIdentityId.New(),
                    appUserId,
                    "https://issuer.example/realm-b",
                    "portable-b"));
            database.Principals.Add(new(principalId, tenantId, appUserId, "Portable principal"));
            database.Memberships.Add(new(
                MembershipId.New(),
                tenantId,
                appUserId,
                principalId,
                MembershipRole.Owner));
            database.Contacts.Add(new(
                ContactId.New(),
                tenantId,
                "PORTABLE",
                "Portable contact",
                principalId));
            await database.SaveChangesAsync();
        }
        await SeedExcludedSecurityDataAsync(appUserId);
        Assert.Equal(1, await CountAsync(sourceConnectionString, "identity.credential_users"));
        Assert.Equal(1, await CountAsync(sourceConnectionString, "identity.user_passkeys"));
        Assert.Equal(1, await CountAsync(sourceConnectionString, "identity.recovery_codes"));
        Assert.Equal(1, await CountAsync(sourceConnectionString, "identity.user_tokens"));

        var taskId = Guid.CreateVersion7();
        var proposalId = Guid.CreateVersion7();
        const string payload = """{"title":"Portable visible task"}""";
        var payloadDigest = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload)));
        await using (var source = new NpgsqlConnection(sourceConnectionString))
        {
            await source.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO open_loops.tasks
                ("Id","Version","TenantId","OwnerPrincipalId","Title","Details","DueAt",
                 "Status","SourceKind","SourceReference","CreatedAt","CompletedAt")
                VALUES (@id,1,@tenant,@principal,'Portable visible task',NULL,NULL,1,
                        'user','integration',@at,NULL);
                INSERT INTO open_loops.task_receipts
                ("TenantId","ActorId","IdempotencyKey","Intent","Outcome","TaskId","TaskVersion")
                VALUES (@tenant,@principal,'portable-task-key','create:integration',0,@id,1);
                INSERT INTO open_loops.task_audit
                ("Id","TenantId","ActorId","ResourceId","Operation","Outcome","SourceKind",
                 "SourceReference","OccurredAt")
                VALUES (@taskAudit,@tenant,@principal,@id,'create','applied','user','integration',@at);
                INSERT INTO open_loops.proposals
                ("Id","Version","TenantId","ActorId","ActorAppUserId","SourceActorId",
                 "Purpose","SourceKind","SourceReference","Operation","ResourceReference",
                 "CanonicalPayload","PayloadDigest","BeforeCanonical","AfterCanonical",
                 "CreatedAt","ExpiresAt","State","ActiveTaskId")
                VALUES (@proposal,2,@tenant,@principal,@user,@principal,'open-loops','user',
                        'integration','create','task:portable',@payload,@digest,'{}',@payload,
                        @at,@expires,1,@id);
                INSERT INTO open_loops.proposal_receipts
                ("TenantId","ActorId","IdempotencyKey","Intent","ProposalId","ProposalVersion",
                 "Outcome","TaskId","TaskVersion")
                VALUES (@tenant,@principal,'portable-proposal-key','confirm:integration',
                        @proposal,2,0,@id,1);
                INSERT INTO open_loops.proposal_audit
                ("Id","TenantId","ActorId","ProposalId","ProposalVersion","Action","Outcome",
                 "SourceKind","SourceReference","OccurredAt")
                VALUES (@proposalAudit,@tenant,@principal,@proposal,2,0,0,'user','integration',@at)
                """,
                source);
            command.Parameters.AddWithValue("id", taskId);
            command.Parameters.AddWithValue("proposal", proposalId);
            command.Parameters.AddWithValue("taskAudit", Guid.CreateVersion7());
            command.Parameters.AddWithValue("proposalAudit", Guid.CreateVersion7());
            command.Parameters.AddWithValue("tenant", tenantId.Value);
            command.Parameters.AddWithValue("user", appUserId.Value);
            command.Parameters.AddWithValue("principal", principalId.Value);
            command.Parameters.AddWithValue("payload", payload);
            command.Parameters.AddWithValue("digest", payloadDigest);
            command.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
            command.Parameters.AddWithValue("expires", DateTimeOffset.UtcNow.AddHours(1));
            await command.ExecuteNonQueryAsync();
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var archive = Path.Combine(
            AppContext.BaseDirectory,
            $"portability-{Guid.NewGuid():N}.andreja");
        try
        {
            var exported = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                tenantId.Value,
                archive,
                key,
                "integration");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    archive,
                    key,
                    commit: true,
                    approvedExportId: Guid.NewGuid()));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.tenants"));

            await ExecuteAsync(
                targetConnectionString,
                """
                ALTER TABLE identity.tenants
                ADD CONSTRAINT "CK_portability_dry_run"
                CHECK ("NormalizedName" <> 'PORTABLE')
                """);
            await Assert.ThrowsAsync<PostgresException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    archive,
                    key,
                    commit: false,
                    approvedExportId: null));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.tenants"));
            Assert.Equal(0, await CountAsync(
                targetConnectionString,
                "portability.application_imports"));
            await ExecuteAsync(
                targetConnectionString,
                """
                ALTER TABLE identity.tenants
                DROP CONSTRAINT "CK_portability_dry_run"
                """);

            var dryRun = await PostgreSqlApplicationPortability.ImportAsync(
                targetConnectionString,
                archive,
                key,
                commit: false,
                approvedExportId: null);
            Assert.True(dryRun.DryRun);
            Assert.False(dryRun.IdempotentReplay);
            Assert.Equal(exported.ExportId, dryRun.ExportId);
            Assert.Equal(
                [
                    "https://issuer.example/realm-a",
                    "https://issuer.example/realm-b",
                ],
                dryRun.Reauthorization.Select(item => item.ProviderReference));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.tenants"));
            Assert.Equal(0, await CountAsync(
                targetConnectionString,
                "portability.application_imports"));

            await ExecuteAsync(
                targetConnectionString,
                """
                INSERT INTO identity.tenants
                ("Id","NormalizedName","DisplayName","DataResidency","Plan","Status")
                VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','DIRTY','Dirty','local','SelfHosted',1)
                """);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    archive,
                    key,
                    commit: false,
                    approvedExportId: null));
            await ExecuteAsync(targetConnectionString, "DELETE FROM identity.tenants");

            var imported = await PostgreSqlApplicationPortability.ImportAsync(
                targetConnectionString,
                archive,
                key,
                commit: true,
                approvedExportId: exported.ExportId);
            Assert.False(imported.DryRun);
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.tasks"));
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.proposals"));
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.task_receipts"));
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.proposal_receipts"));
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.task_audit"));
            Assert.Equal(1, await CountAsync(targetConnectionString, "open_loops.proposal_audit"));
            Assert.Equal(
                "Portable visible task",
                await ScalarAsync<string>(
                    targetConnectionString,
                    """SELECT "Title" FROM open_loops.tasks"""));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.credential_users"));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.user_passkeys"));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.recovery_codes"));
            Assert.Equal(0, await CountAsync(targetConnectionString, "identity.user_tokens"));

            var replay = await PostgreSqlApplicationPortability.ImportAsync(
                targetConnectionString,
                archive,
                key,
                commit: true,
                approvedExportId: exported.ExportId);
            Assert.True(replay.IdempotentReplay);

            var tampered = await File.ReadAllBytesAsync(archive);
            tampered[^1] ^= 0xff;
            await File.WriteAllBytesAsync(archive, tampered);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    archive,
                    key,
                    commit: false,
                    approvedExportId: null));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (File.Exists(archive))
            {
                File.Delete(archive);
            }
        }
    }

    [Fact]
    public async Task ConcurrentDistinctImportsSerializeBeforeTransactionSnapshot()
    {
        var first = await SeedPortableTenantAsync("RACE-A", "Race winner A");
        var second = await SeedPortableTenantAsync("RACE-B", "Race loser B");
        var key = RandomNumberGenerator.GetBytes(32);
        var firstArchive = ArchivePath();
        var secondArchive = ArchivePath();
        try
        {
            var firstExport = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                first,
                firstArchive,
                key,
                "concurrency");
            var secondExport = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                second,
                secondArchive,
                key,
                "concurrency");

            for (var attempt = 0; attempt < 4; attempt++)
            {
                var target = await CreateMigratedTargetAsync($"race_{attempt}");
                try
                {
                    var blocker = new BlockingImportFaultInjector();
                    var winner = PostgreSqlApplicationPortability.ImportAsync(
                        target,
                        firstArchive,
                        key,
                        commit: true,
                        firstExport.ExportId,
                        TimeSpan.FromSeconds(10),
                        blocker);
                    await blocker.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    var loser = PostgreSqlApplicationPortability.ImportAsync(
                        target,
                        secondArchive,
                        key,
                        commit: true,
                        secondExport.ExportId,
                        TimeSpan.FromSeconds(10),
                        faultInjector: null);
                    await Task.Delay(100);
                    Assert.False(loser.IsCompleted);

                    blocker.Release.TrySetResult();
                    var committed = await winner;
                    Assert.False(committed.IdempotentReplay);
                    var conflict = await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await loser);
                    Assert.Contains(
                        "conflicting application import",
                        conflict.Message,
                        StringComparison.Ordinal);
                    Assert.Equal(1, await CountAsync(target, "portability.application_imports"));
                    Assert.Equal("Race winner A", await ScalarAsync<string>(
                        target,
                        """SELECT "Title" FROM open_loops.tasks"""));

                    var replay = await PostgreSqlApplicationPortability.ImportAsync(
                        target,
                        firstArchive,
                        key,
                        commit: true,
                        firstExport.ExportId);
                    Assert.True(replay.IdempotentReplay);
                    await AssertImportLockAvailableAsync(target);
                }
                finally
                {
                    await DropAdditionalDatabaseAsync(target);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteFile(firstArchive);
            DeleteFile(secondArchive);
        }
    }

    [Fact]
    public async Task CancellationTimeoutAndInjectedFailureReleaseDedicatedSessionLock()
    {
        var tenant = await SeedPortableTenantAsync("LOCK-RELEASE", "Lock release");
        var key = RandomNumberGenerator.GetBytes(32);
        var archive = ArchivePath();
        var tampered = ArchivePath();
        try
        {
            var exported = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                tenant,
                archive,
                key,
                "lock-release");

            var cancellationTarget = await CreateMigratedTargetAsync("cancel");
            try
            {
                var blocker = new BlockingImportFaultInjector();
                using var cancellation = new CancellationTokenSource();
                var cancelled = PostgreSqlApplicationPortability.ImportAsync(
                    cancellationTarget,
                    archive,
                    key,
                    commit: true,
                    exported.ExportId,
                    TimeSpan.FromSeconds(10),
                    blocker,
                    cancellation.Token);
                await blocker.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () => await cancelled);
                await AssertImportLockAvailableAsync(cancellationTarget);

                var imported = await PostgreSqlApplicationPortability.ImportAsync(
                    cancellationTarget,
                    archive,
                    key,
                    commit: true,
                    exported.ExportId);
                Assert.False(imported.IdempotentReplay);
            }
            finally
            {
                await DropAdditionalDatabaseAsync(cancellationTarget);
            }

            var failureTarget = await CreateMigratedTargetAsync("fault");
            try
            {
                await Assert.ThrowsAsync<InjectedImportFailureException>(() =>
                    PostgreSqlApplicationPortability.ImportAsync(
                        failureTarget,
                        archive,
                        key,
                        commit: true,
                        exported.ExportId,
                        TimeSpan.FromSeconds(10),
                        new ThrowingImportFaultInjector()));
                await AssertImportLockAvailableAsync(failureTarget);
                _ = await PostgreSqlApplicationPortability.ImportAsync(
                    failureTarget,
                    archive,
                    key,
                    commit: true,
                    exported.ExportId);
            }
            finally
            {
                await DropAdditionalDatabaseAsync(failureTarget);
            }

            var timeoutTarget = await CreateMigratedTargetAsync("timeout");
            try
            {
                await using var holder = new NpgsqlConnection(
                    NonPooledConnectionString(timeoutTarget));
                await holder.OpenAsync();
                await ExecuteLockAsync(holder, "pg_advisory_lock");
                await Assert.ThrowsAsync<TimeoutException>(() =>
                    PostgreSqlApplicationPortability.ImportAsync(
                        timeoutTarget,
                        archive,
                        key,
                        commit: true,
                        exported.ExportId,
                        TimeSpan.FromMilliseconds(150),
                        faultInjector: null));
                await ExecuteLockAsync(holder, "pg_advisory_unlock");
                await holder.CloseAsync();
                await AssertImportLockAvailableAsync(timeoutTarget);

                var bytes = await File.ReadAllBytesAsync(archive);
                bytes[^1] ^= 0xff;
                await File.WriteAllBytesAsync(tampered, bytes);
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    PostgreSqlApplicationPortability.ImportAsync(
                        timeoutTarget,
                        tampered,
                        key,
                        commit: false,
                        approvedExportId: null));
                var imported = await PostgreSqlApplicationPortability.ImportAsync(
                    timeoutTarget,
                    archive,
                    key,
                    commit: true,
                    exported.ExportId);
                Assert.False(imported.IdempotentReplay);
                await AssertImportLockAvailableAsync(timeoutTarget);
            }
            finally
            {
                await DropAdditionalDatabaseAsync(timeoutTarget);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteFile(archive);
            DeleteFile(tampered);
        }
    }

    private static AndrejaIdentityDbContext CreateContext(
        string connectionString,
        ITenantPrincipalContextAccessor accessor)
    {
        var services = new ServiceCollection()
            .AddOptions()
            .Configure<IdentityOptions>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();
        return new(
            new DbContextOptionsBuilder<AndrejaIdentityDbContext>()
                .UseApplicationServiceProvider(services)
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__migrations", "identity"))
                .Options,
            accessor);
    }

    private static async Task MigrateAsync(string connectionString)
    {
        await using var database = CreateContext(
            connectionString,
            new ScopedTenantPrincipalContext());
        await database.Database.MigrateAsync();
    }

    private async Task CreateDatabaseAsync(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = "postgres",
        };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE {Quote(database)}",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = "postgres",
        };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {Quote(database)} WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connectionString, string table) =>
        await ScalarAsync<long>(connectionString, $"SELECT count(*) FROM {table}");

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Expected a scalar result."));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Quote(string identifier) =>
        '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private async Task<Guid> SeedPortableTenantAsync(string normalizedName, string taskTitle)
    {
        var tenantId = TenantId.New();
        var appUserId = AppUserId.New();
        var principalId = PrincipalId.New();
        var context = new ScopedTenantPrincipalContext();
        context.Set(new(tenantId, appUserId, principalId, "open-loops"));
        await using (var database = CreateContext(sourceConnectionString, context))
        {
            database.Tenants.Add(new(tenantId, normalizedName, normalizedName, "local"));
            database.AppUsers.Add(new(appUserId, $"{normalizedName} user"));
            database.Principals.Add(new(
                principalId,
                tenantId,
                appUserId,
                $"{normalizedName} principal"));
            database.Memberships.Add(new(
                MembershipId.New(),
                tenantId,
                appUserId,
                principalId,
                MembershipRole.Owner));
            await database.SaveChangesAsync();
        }

        await using var source = new NpgsqlConnection(sourceConnectionString);
        await source.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO open_loops.tasks
            ("Id","Version","TenantId","OwnerPrincipalId","Title","Details","DueAt",
             "Status","SourceKind","SourceReference","CreatedAt","CompletedAt")
            VALUES (@id,1,@tenant,@principal,@title,NULL,NULL,1,'user','concurrency',@at,NULL)
            """,
            source);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("tenant", tenantId.Value);
        command.Parameters.AddWithValue("principal", principalId.Value);
        command.Parameters.AddWithValue("title", taskTitle);
        command.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync();
        return tenantId.Value;
    }

    private async Task SeedExcludedSecurityDataAsync(AppUserId appUserId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAndrejaIdentityPostgreSql(sourceConnectionString);
        services
            .AddIdentityCore<AspNetIdentityUser>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AndrejaIdentityDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetIdentityUser>>();
        var credentialUser = new AspNetIdentityUser
        {
            Id = Guid.CreateVersion7(),
            AppUserId = appUserId,
            UserName = "portable-owner",
            EmailConfirmed = true,
        };
        Assert.True((await users.CreateAsync(credentialUser)).Succeeded);
        var passkey = new UserPasskeyInfo(
            [1, 2, 3, 4],
            RandomNumberGenerator.GetBytes(77),
            DateTimeOffset.UtcNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: [])
        {
            Name = "Synthetic passkey",
        };
        Assert.True((await users.AddOrUpdatePasskeyAsync(
            credentialUser,
            passkey)).Succeeded);
        Assert.True((await users.SetAuthenticationTokenAsync(
            credentialUser,
            "synthetic-provider",
            "access-token",
            "synthetic-token")).Succeeded);

        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        database.IdentityRecoveryCodes.Add(new(
            Guid.CreateVersion7(),
            credentialUser.Id,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16),
            RandomNumberGenerator.GetBytes(32),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1)));
        await database.SaveChangesAsync();
    }

    private async Task<string> CreateMigratedTargetAsync(string purpose)
    {
        var database = $"andreja_test_port_{purpose}_{Guid.NewGuid():N}"[..52];
        await CreateDatabaseAsync(database);
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = database,
        };
        await MigrateAsync(builder.ConnectionString);
        return builder.ConnectionString;
    }

    private async Task DropAdditionalDatabaseAsync(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await DropDatabaseAsync(database!);
    }

    private static async Task AssertImportLockAvailableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(
            NonPooledConnectionString(connectionString));
        await connection.OpenAsync();
        await using var acquire = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(@lockKey)",
            connection);
        acquire.Parameters.AddWithValue(
            "lockKey",
            PostgreSqlApplicationPortability.ImportAdvisoryLock);
        Assert.True(await acquire.ExecuteScalarAsync() is true);
        await ExecuteLockAsync(connection, "pg_advisory_unlock");
    }

    private static async Task ExecuteLockAsync(NpgsqlConnection connection, string function)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT {function}(@lockKey)",
            connection);
        command.Parameters.AddWithValue(
            "lockKey",
            PostgreSqlApplicationPortability.ImportAdvisoryLock);
        await command.ExecuteNonQueryAsync();
    }

    private static string NonPooledConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            Multiplexing = false,
        };
        return builder.ConnectionString;
    }

    private static string ArchivePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            $"portability-{Guid.NewGuid():N}.andreja");

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class BlockingImportFaultInjector : IApplicationImportFaultInjector
    {
        public TaskCompletionSource Acquired { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask OnCheckpointAsync(
            ApplicationImportCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            Assert.Equal(
                ApplicationImportCheckpoint.SessionLockAcquiredBeforeTransaction,
                checkpoint);
            Acquired.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ThrowingImportFaultInjector : IApplicationImportFaultInjector
    {
        public ValueTask OnCheckpointAsync(
            ApplicationImportCheckpoint checkpoint,
            CancellationToken cancellationToken) =>
            throw new InjectedImportFailureException();
    }

    private sealed class InjectedImportFailureException : Exception;
}
