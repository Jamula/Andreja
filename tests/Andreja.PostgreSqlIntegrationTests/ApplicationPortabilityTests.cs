using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    [Fact]
    public async Task ApplicationTableLocksPreventDirtyTargetAtImportCommit()
    {
        var tenant = await SeedPortableTenantAsync("QUIESCENCE", "Quiescence");
        var key = RandomNumberGenerator.GetBytes(32);
        var archive = ArchivePath();
        var target = await CreateMigratedTargetAsync("quiescence");
        try
        {
            var exported = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                tenant,
                archive,
                key,
                "quiescence");
            var blocker = new TableLockBlockingFaultInjector();
            var importing = PostgreSqlApplicationPortability.ImportAsync(
                target,
                archive,
                key,
                commit: true,
                exported.ExportId,
                TimeSpan.FromSeconds(10),
                blocker);
            await blocker.TablesLocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var writer = ExecuteAsync(
                target,
                """
                    INSERT INTO identity.tenants
                    ("Id","NormalizedName","DisplayName","DataResidency","Plan","Status")
                    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
                            'CONCURRENT','Concurrent','local','SelfHosted',1)
                    """);
            await Task.Delay(100);
            Assert.False(writer.IsCompleted);

            blocker.Release.TrySetResult();
            _ = await importing;
            await writer;
            Assert.Equal(1, await CountAsync(target, "portability.application_imports"));
            Assert.Equal(2, await CountAsync(target, "identity.tenants"));
            await AssertImportLockAvailableAsync(target);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteFile(archive);
            await DropAdditionalDatabaseAsync(target);
        }
    }

    [Fact]
    public async Task DryRunLocksEveryCheckedTableAgainstConcurrentApplicationWrites()
    {
        var tenant = await SeedPortableTenantAsync("TABLE-LOCK", "Table lock");
        var key = RandomNumberGenerator.GetBytes(32);
        var archive = ArchivePath();
        try
        {
            _ = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                tenant,
                archive,
                key,
                "table-lock");
            var blocker = new TableLockBlockingFaultInjector();
            var dryRunTask = PostgreSqlApplicationPortability.ImportAsync(
                targetConnectionString,
                archive,
                key,
                commit: false,
                approvedExportId: null,
                TimeSpan.FromSeconds(10),
                blocker);
            await blocker.TablesLocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(
                await CheckedTableCountAsync(targetConnectionString),
                await CheckedTableShareLockCountAsync(targetConnectionString));
            await using var writer = new NpgsqlConnection(targetConnectionString);
            await writer.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                    INSERT INTO identity.tenants
                    ("Id","NormalizedName","DisplayName","DataResidency","Plan","Status")
                    VALUES (@id,'CONCURRENT','Concurrent','local','SelfHosted',1)
                    """,
                writer);
            command.Parameters.AddWithValue("id", Guid.CreateVersion7());
            var writeTask = command.ExecuteNonQueryAsync();
            await Task.Delay(100);
            Assert.False(writeTask.IsCompleted);

            blocker.Release.TrySetResult();
            var report = await dryRunTask;
            Assert.True(report.DryRun);
            Assert.Equal(1, await writeTask);
            Assert.Equal(1, await CountAsync(targetConnectionString, "identity.tenants"));
            Assert.Equal(
                0,
                await CountAsync(targetConnectionString, "portability.application_imports"));
            await AssertImportLockAvailableAsync(targetConnectionString);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteFile(archive);
            await ExecuteAsync(targetConnectionString, "DELETE FROM identity.tenants");
        }
    }

    [Fact]
    public async Task ImportRejectsSettingsMembershipAndReauthorizationDivergence()
    {
        var tenant = await SeedPortableTenantAsync("LINEAGE", "Lineage");
        await AddSecondPortableUserAndExternalIdentityAsync(tenant);
        var key = RandomNumberGenerator.GetBytes(32);
        var archive = ArchivePath();
        var settingsArchive = ArchivePath();
        var membershipArchive = ArchivePath();
        var reauthorizationArchive = ArchivePath();
        try
        {
            _ = await PostgreSqlApplicationPortability.ExportAsync(
                sourceConnectionString,
                tenant,
                archive,
                key,
                "lineage");

            File.Copy(archive, settingsArchive);
            await RewriteAuthenticatedArchiveAsync(settingsArchive, key, files =>
                MutateRecord(
                    files,
                    "settings.ndjson",
                    "tenantSettings",
                    value => value["DataResidency"] = "different"));
            var settingsFailure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    settingsArchive,
                    key,
                    commit: false,
                    approvedExportId: null));
            Assert.Contains("settings do not match", settingsFailure.Message, StringComparison.Ordinal);

            File.Copy(archive, membershipArchive);
            await RewriteAuthenticatedArchiveAsync(
                membershipArchive,
                key,
                MutateMembershipAppUser);
            var membershipFailure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    membershipArchive,
                    key,
                    commit: false,
                    approvedExportId: null));
            Assert.Contains(
                "principal does not belong",
                membershipFailure.Message,
                StringComparison.Ordinal);

            File.Copy(archive, reauthorizationArchive);
            await RewriteAuthenticatedArchiveAsync(
                reauthorizationArchive,
                key,
                files =>
                {
                    var manifest = ParseObject(files["manifest.json"]);
                    manifest["reauthorization"]![0]!["providerReference"] =
                        "https://different.example";
                    files["manifest.json"] = SerializeNode(manifest);
                });
            var reauthorizationFailure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                PostgreSqlApplicationPortability.ImportAsync(
                    targetConnectionString,
                    reauthorizationArchive,
                    key,
                    commit: false,
                    approvedExportId: null));
            Assert.Contains(
                "do not match imported external identities",
                reauthorizationFailure.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteFile(archive);
            DeleteFile(settingsArchive);
            DeleteFile(membershipArchive);
            DeleteFile(reauthorizationArchive);
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

    private static async Task<long> CheckedTableCountAsync(string connectionString) =>
        await ScalarAsync<long>(
            connectionString,
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_type='BASE TABLE'
              AND table_schema IN ('identity','open_loops','portability')
              AND table_name <> '__migrations'
              AND NOT (table_schema='portability' AND table_name='application_imports')
            """);

    private static async Task<long> CheckedTableShareLockCountAsync(string connectionString) =>
        await ScalarAsync<long>(
            connectionString,
            """
            SELECT count(DISTINCT (n.nspname,c.relname))
            FROM pg_locks l
            JOIN pg_class c ON c.oid=l.relation
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE l.granted
              AND l.mode='ShareLock'
              AND c.relkind='r'
              AND n.nspname IN ('identity','open_loops','portability')
              AND c.relname <> '__migrations'
              AND NOT (n.nspname='portability' AND c.relname='application_imports')
            """);

    private async Task AddSecondPortableUserAndExternalIdentityAsync(Guid tenantId)
    {
        await using var source = new NpgsqlConnection(sourceConnectionString);
        await source.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity.app_users ("Id","DisplayName","PrimaryExternalIdentityId")
            VALUES (@user,'Second portable user',NULL);
            INSERT INTO identity.principals ("Id","TenantId","AppUserId","DisplayName")
            VALUES (@principal,@tenant,@user,'Second portable principal');
            INSERT INTO identity.memberships
                ("Id","TenantId","AppUserId","PrincipalId","Role","Status")
            VALUES (@membership,@tenant,@user,@principal,1,1);
            INSERT INTO identity.external_identities ("Id","AppUserId","Issuer","Subject")
            SELECT @identity,"AppUserId",'https://issuer.example/lineage','lineage'
            FROM identity.memberships
            WHERE "TenantId"=@tenant
            ORDER BY "Id"
            LIMIT 1
            """,
            source);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("user", Guid.CreateVersion7());
        command.Parameters.AddWithValue("principal", Guid.CreateVersion7());
        command.Parameters.AddWithValue("membership", Guid.CreateVersion7());
        command.Parameters.AddWithValue("identity", Guid.CreateVersion7());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RewriteAuthenticatedArchiveAsync(
        string path,
        byte[] key,
        Action<Dictionary<string, byte[]>> mutation)
    {
        var envelope = await File.ReadAllBytesAsync(path);
        var magic = "ANDREJA1"u8.ToArray();
        var plaintext = new byte[envelope.Length - magic.Length - 28];
        byte[]? rewrittenZip = null;
        byte[]? rewrittenEnvelope = null;
        try
        {
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(
                    envelope.AsSpan(magic.Length, 12),
                    envelope.AsSpan(magic.Length + 28),
                    envelope.AsSpan(magic.Length + 12, 16),
                    plaintext,
                    magic);
            }

            var files = ReadZipFiles(plaintext);
            mutation(files);
            var manifest = ParseObject(files["manifest.json"]);
            foreach (var descriptorNode in manifest["artifacts"]!.AsArray())
            {
                var descriptor = descriptorNode!.AsObject();
                var artifactPath = descriptor["path"]!.GetValue<string>();
                var content = files[artifactPath];
                descriptor["sha256"] =
                    Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
                descriptor["byteLength"] = content.LongLength;
                descriptor["recordCount"] = content.LongCount(value => value == (byte)'\n');
            }
            files["manifest.json"] = SerializeNode(manifest);

            rewrittenZip = PostgreSqlApplicationPortability.CreateZip(files);
            rewrittenEnvelope = new byte[magic.Length + 28 + rewrittenZip.Length];
            magic.CopyTo(rewrittenEnvelope, 0);
            var nonce = rewrittenEnvelope.AsSpan(magic.Length, 12);
            RandomNumberGenerator.Fill(nonce);
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(
                    nonce,
                    rewrittenZip,
                    rewrittenEnvelope.AsSpan(magic.Length + 28),
                    rewrittenEnvelope.AsSpan(magic.Length + 12, 16),
                    magic);
            }
            await File.WriteAllBytesAsync(path, rewrittenEnvelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(magic);
            if (rewrittenZip is not null)
            {
                CryptographicOperations.ZeroMemory(rewrittenZip);
            }
            if (rewrittenEnvelope is not null)
            {
                CryptographicOperations.ZeroMemory(rewrittenEnvelope);
            }
        }
    }

    private static Dictionary<string, byte[]> ReadZipFiles(byte[] content)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var input = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var output = new MemoryStream();
            using (var stream = entry.Open())
            {
                stream.CopyTo(output);
            }
            files.Add(entry.FullName, output.ToArray());
        }
        return files;
    }

    private static void MutateRecord(
        Dictionary<string, byte[]> files,
        string path,
        string type,
        Action<JsonObject> mutation)
    {
        var records = ParseRecords(files[path]);
        var record = records.Single(
            candidate => candidate["type"]!.GetValue<string>() == type);
        mutation(record["value"]!.AsObject());
        files[path] = SerializeRecords(records);
    }

    private static void MutateMembershipAppUser(Dictionary<string, byte[]> files)
    {
        var records = ParseRecords(files["records.ndjson"]);
        var appUsers = records
            .Where(record => record["type"]!.GetValue<string>() == "appUser")
            .Select(record => record["value"]!["Id"]!.GetValue<string>())
            .ToArray();
        var principalUsers = records
            .Where(record => record["type"]!.GetValue<string>() == "principal")
            .ToDictionary(
                record => record["value"]!["Id"]!.GetValue<string>(),
                record => record["value"]!["AppUserId"]!.GetValue<string>(),
                StringComparer.Ordinal);
        var membership = records.First(
            record => record["type"]!.GetValue<string>() == "membership");
        var value = membership["value"]!.AsObject();
        var principalId = value["PrincipalId"]!.GetValue<string>();
        value["AppUserId"] = appUsers.Single(
            appUserId => appUserId != principalUsers[principalId]);
        files["records.ndjson"] = SerializeRecords(records);
    }

    private static List<JsonObject> ParseRecords(byte[] content) =>
        Encoding.UTF8.GetString(content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .ToList();

    private static JsonObject ParseObject(byte[] content) =>
        JsonNode.Parse(content)!.AsObject();

    private static byte[] SerializeRecords(IEnumerable<JsonObject> records) =>
        Encoding.UTF8.GetBytes(
            string.Join('\n', records.Select(record => record.ToJsonString())) + "\n");

    private static byte[] SerializeNode(JsonNode node) =>
        Encoding.UTF8.GetBytes(node.ToJsonString());

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
            if (checkpoint == ApplicationImportCheckpoint.SessionLockAcquiredBeforeTransaction)
            {
                Acquired.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            else
            {
                Assert.Equal(ApplicationImportCheckpoint.TargetTablesLocked, checkpoint);
            }
        }
    }

    private sealed class TableLockBlockingFaultInjector : IApplicationImportFaultInjector
    {
        public TaskCompletionSource TablesLocked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask OnCheckpointAsync(
            ApplicationImportCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            if (checkpoint == ApplicationImportCheckpoint.TargetTablesLocked)
            {
                TablesLocked.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
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
