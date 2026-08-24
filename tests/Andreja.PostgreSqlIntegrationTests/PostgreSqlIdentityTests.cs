using Andreja.Adapters.PostgreSql;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andreja.PostgreSqlIntegrationTests;

public sealed class PostgreSqlIdentityTests : IAsyncLifetime
{
    private readonly string connectionString =
        Environment.GetEnvironmentVariable("ANDREJA_TEST_POSTGRES")
        ?? throw new InvalidOperationException(
            "BLOCKED: set ANDREJA_TEST_POSTGRES to a disposable local PostgreSQL database.");

    private ServiceProvider services = null!;
    private string tokenPath = null!;
    private string bootstrapToken = null!;

    public async Task InitializeAsync()
    {
        var databaseName = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database;
        if (string.IsNullOrWhiteSpace(databaseName)
            || !databaseName.StartsWith("andreja_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ANDREJA_TEST_POSTGRES must target a disposable database named andreja_test_*.");
        }

        var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        bootstrapToken = Convert.ToBase64String(tokenBytes);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(tokenBytes);
        tokenPath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.bootstrap");
        await File.WriteAllTextAsync(tokenPath, bootstrapToken);
        MakeReadOnly(tokenPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{LocalIdentityOptions.SectionName}:AuthenticationScheme"] =
                    IdentityConstants.ApplicationScheme,
                [$"{LocalIdentityOptions.SectionName}:RelyingPartyId"] = "localhost",
                [$"{LocalIdentityOptions.SectionName}:AllowedOrigins:0"] = "https://localhost",
                [$"{LocalIdentityOptions.SectionName}:BootstrapTokenFile"] = tokenPath,
                [$"{LocalIdentityOptions.SectionName}:BootstrapTokenBytes"] = "32",
                [$"{LocalIdentityOptions.SectionName}:MaximumPasskeysPerUser"] = "3",
                [$"{LocalIdentityOptions.SectionName}:RecoveryCodeCount"] = "8",
                [$"{LocalIdentityOptions.SectionName}:RecoveryCodeLifetime"] = "90.00:00:00",
                [$"{LocalIdentityOptions.SectionName}:RecoveryRateLimitAttempts"] = "3",
                [$"{LocalIdentityOptions.SectionName}:RecoveryRateLimitWindow"] = "00:15:00",
            })
            .Build();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddOptions();
        collection.AddDataProtection();
        collection.AddHttpContextAccessor();
        collection.AddSingleton(TimeProvider.System);
        collection.AddAndrejaIdentityPostgreSql(connectionString);
        collection.AddAndrejaLocalIdentity(
            configuration.GetRequiredSection(LocalIdentityOptions.SectionName));
        collection.AddScoped<IOpenLoopsTaskStore, PostgreSqlOpenLoopsTaskStore>();
        services = collection.BuildServiceProvider();

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        await database.Database.EnsureDeletedAsync();
        await database.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is null)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        await database.Database.EnsureDeletedAsync();
        await services.DisposeAsync();
        DeleteReadOnlyFile(tokenPath);
    }

    [Fact]
    public async Task MigrationCreatesEmptyDatabaseAndEnforcesTwoTenantIsolation()
    {
        var tenantA = await SeedTenantAsync("TENANT-A");
        var tenantB = await SeedTenantAsync("TENANT-B");

        await AddContactAsync(tenantA, "ALPHA");
        await AddContactAsync(tenantB, "BETA");

        await using var scope = CreateScope(tenantA.Context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        var visible = await database.Contacts.Select(contact => contact.NormalizedName).ToArrayAsync();

        Assert.Equal(["ALPHA"], visible);
        database.Contacts.Add(
            new Contact(ContactId.New(), tenantB.Context.TenantId, "FORBIDDEN", "Forbidden"));
        await Assert.ThrowsAsync<IdentityAccessDeniedException>(
            () => database.SaveChangesAsync());
    }

    [Fact]
    public async Task CompositeReferenceAndIssuerSubjectUniquenessFailInDatabase()
    {
        var tenantA = await SeedTenantAsync("TENANT-A");
        var tenantB = await SeedTenantAsync("TENANT-B");

        await using (var scope = CreateScope(tenantA.Context))
        {
            var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
            database.Contacts.Add(
                new Contact(
                    ContactId.New(),
                    tenantA.Context.TenantId,
                    "CROSS-TENANT",
                    "Cross tenant",
                    tenantB.Context.PrincipalId));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        }

        await AddExternalIdentityAsync(tenantA, "https://issuer.example", "same-subject");
        await Assert.ThrowsAsync<DbUpdateException>(
            () => AddExternalIdentityAsync(
                tenantB,
                "https://issuer.example",
                "same-subject"));
    }

    [Fact]
    public async Task TaskMigrationPersistsIdempotentLifecycleAndEnforcesTenantIsolation()
    {
        var tenantA = await SeedTenantAsync("TASK-TENANT-A");
        var tenantB = await SeedTenantAsync("TASK-TENANT-B");
        var contextA = tenantA.Context with { Purpose = OpenLoopsPolicy.Purpose };
        var contextB = tenantB.Context with { Purpose = OpenLoopsPolicy.Purpose };
        var createdAt = new DateTimeOffset(2026, 8, 24, 4, 30, 0, TimeSpan.Zero);
        var task = new OpenLoopTask(
            Guid.CreateVersion7(),
            contextA.TenantId,
            contextA.PrincipalId,
            "Persisted task",
            null,
            null,
            "assistant",
            "assistant:integration",
            createdAt);

        await using (var scope = CreateScope(contextA))
        {
            var store = scope.ServiceProvider.GetRequiredService<IOpenLoopsTaskStore>();
            var created = await store.CreateAsync(
                contextA,
                task,
                Guid.CreateVersion7(),
                "create-integration");
            Assert.Equal(TaskMutationOutcome.Applied, created.Outcome);
        }

        await using (var scope = CreateScope(contextB))
        {
            var store = scope.ServiceProvider.GetRequiredService<IOpenLoopsTaskStore>();
            Assert.Empty(await store.ListAsync(contextB));
            var crossTenant = await store.CompleteAsync(
                contextB,
                task.Id,
                task.Version,
                "complete-cross-tenant",
                createdAt.AddMinutes(1));
            Assert.Equal(TaskMutationOutcome.NotFound, crossTenant.Outcome);
        }

        await using (var scope = CreateScope(contextA))
        {
            var store = scope.ServiceProvider.GetRequiredService<IOpenLoopsTaskStore>();
            var persisted = Assert.Single(await store.ListAsync(contextA));
            var openVersion = persisted.Version;
            var completed = await store.CompleteAsync(
                contextA,
                persisted.Id,
                openVersion,
                "complete-integration",
                createdAt.AddMinutes(1));
            var replay = await store.CompleteAsync(
                contextA,
                persisted.Id,
                openVersion,
                "complete-integration",
                createdAt.AddMinutes(2));

            Assert.Equal(TaskMutationOutcome.Applied, completed.Outcome);
            Assert.Equal(TaskMutationOutcome.IdempotentReplay, replay.Outcome);
            Assert.Equal(OpenLoopTaskStatus.Completed, replay.Task?.Status);
        }
    }

    [Fact]
    public async Task BootstrapIsAtomicSingleUseAndPersistsPasskeyAndHashedRecoveryCodes()
    {
        BootstrapIdentityResult created;
        byte[] credentialId = [1, 2, 3, 4, 5, 6, 7, 8];
        var reservedCredentialUserId = Guid.CreateVersion7();
        await using (var scope = services.CreateAsyncScope())
        {
            var operations = scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
            created = await operations.CompleteBootstrapAsync(
                CreateSecureRequest(),
                bootstrapToken,
                "Personal workspace",
                "Local owner",
                reservedCredentialUserId,
                CreatePasskey(credentialId),
                CancellationToken.None);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
            Assert.Single(await database.Tenants.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await database.Memberships.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await database.IdentityBootstrapStates.ToArrayAsync());
            var storedCodes = await database.IdentityRecoveryCodes.ToArrayAsync();
            Assert.Equal(8, storedCodes.Length);
            Assert.All(storedCodes, code =>
            {
                Assert.Equal(32, code.LookupHash.Length);
                Assert.Equal(16, code.Salt.Length);
                Assert.Equal(32, code.VerificationHash.Length);
            });
            Assert.DoesNotContain(
                created.RecoveryCodes,
                plaintext => storedCodes.Any(code =>
                    Convert.ToBase64String(code.LookupHash) == plaintext
                    || Convert.ToBase64String(code.VerificationHash) == plaintext));

            var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetIdentityUser>>();
            var persisted = Assert.Single(
                await users.Users.Where(user => user.Id == created.User.Id).ToArrayAsync());
            Assert.Equal(reservedCredentialUserId, persisted.Id);
            var passkey = Assert.Single(await users.GetPasskeysAsync(persisted));
            Assert.Equal(credentialId, passkey.CredentialId);

            var operations = scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => operations.CompleteBootstrapAsync(
                    CreateSecureRequest(),
                    bootstrapToken,
                    "Replay",
                    "Replay",
                    Guid.CreateVersion7(),
                    CreatePasskey([9, 9, 9]),
                    CancellationToken.None));
        }
    }

    [Fact]
    public async Task ConcurrentBootstrapAllowsExactlyOneCommit()
    {
        async Task<bool> AttemptAsync(byte discriminator)
        {
            await using var scope = services.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>()
                    .CompleteBootstrapAsync(
                        CreateSecureRequest(),
                        bootstrapToken,
                        $"Workspace {discriminator}",
                        $"Owner {discriminator}",
                        Guid.CreateVersion7(),
                        CreatePasskey([discriminator, 1, 2, 3]),
                        CancellationToken.None);
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or DbUpdateException
                    or Npgsql.PostgresException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(AttemptAsync(1), AttemptAsync(2));

        Assert.Single(outcomes, outcome => outcome);
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        Assert.Single(await database.IdentityBootstrapStates.ToArrayAsync());
        Assert.Single(await database.Tenants.IgnoreQueryFilters().ToArrayAsync());
        Assert.Single(await database.Memberships.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public async Task RecoveryRotatesCodesReplacesPasskeysAndInvalidatesSecurityStamp()
    {
        byte[] initialCredential = [10, 11, 12, 13];
        BootstrapIdentityResult bootstrap;
        await using (var scope = services.CreateAsyncScope())
        {
            bootstrap = await scope.ServiceProvider
                .GetRequiredService<LocalIdentityOperations>()
                .CompleteBootstrapAsync(
                    CreateSecureRequest(),
                    bootstrapToken,
                    "Recovery workspace",
                    "Recovery owner",
                    Guid.CreateVersion7(),
                    CreatePasskey(initialCredential),
                    CancellationToken.None);
        }

        await using var recoveryScope = services.CreateAsyncScope();
        var operations =
            recoveryScope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
        var users = recoveryScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var before = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
        Assert.NotNull(before);
        var oldStamp = before.SecurityStamp;
        var start = await operations.BeginRecoveryAsync(
            bootstrap.RecoveryCodes[0],
            CancellationToken.None);
        Assert.NotNull(start);
        byte[] replacementCredential = [20, 21, 22, 23];

        var recovered = await operations.CompleteRecoveryAsync(
            start,
            CreatePasskey(replacementCredential),
            CancellationToken.None);

        Assert.Equal(8, recovered.RecoveryCodes.Count);
        var after = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
        Assert.NotNull(after);
        Assert.NotEqual(oldStamp, after.SecurityStamp);
        var passkey = Assert.Single(await users.GetPasskeysAsync(after));
        Assert.Equal(replacementCredential, passkey.CredentialId);
        Assert.Null(await operations.BeginRecoveryAsync(
            bootstrap.RecoveryCodes[0],
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => operations.CompleteRecoveryAsync(
                start,
                CreatePasskey([30, 31, 32]),
                CancellationToken.None));
    }

    [Fact]
    public async Task PasskeyLimitAndLastAuthenticationPathFailClosed()
    {
        BootstrapIdentityResult bootstrap;
        await using var scope = services.CreateAsyncScope();
        var operations = scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
        bootstrap = await operations.CompleteBootstrapAsync(
            CreateSecureRequest(),
            bootstrapToken,
            "Limits workspace",
            "Limits owner",
            Guid.CreateVersion7(),
            CreatePasskey([40, 41, 42]),
            CancellationToken.None);
        await operations.RegisterPasskeyAsync(
            bootstrap.User,
            CreatePasskey([43, 44, 45]),
            "Backup one",
            CancellationToken.None);
        await operations.RegisterPasskeyAsync(
            bootstrap.User,
            CreatePasskey([46, 47, 48]),
            "Backup two",
            CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => operations.RegisterPasskeyAsync(
                bootstrap.User,
                CreatePasskey([49, 50, 51]),
                "Over limit",
                CancellationToken.None));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetIdentityUser>>();
        var passkeys = await users.GetPasskeysAsync(bootstrap.User);
        await operations.RevokePasskeyAsync(
            bootstrap.User,
            passkeys[0].CredentialId,
            CancellationToken.None);
        await operations.RevokePasskeyAsync(
            bootstrap.User,
            passkeys[1].CredentialId,
            CancellationToken.None);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        var now = DateTimeOffset.UtcNow;
        foreach (var code in await database.IdentityRecoveryCodes
                     .Where(code => code.ConsumedAt == null)
                     .ToArrayAsync())
        {
            code.Consume(now);
        }

        await database.SaveChangesAsync(CancellationToken.None);
        var remaining = Assert.Single(await users.GetPasskeysAsync(bootstrap.User));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => operations.RevokePasskeyAsync(
                bootstrap.User,
                remaining.CredentialId,
                CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentRegistrationCannotExceedPasskeyLimit()
    {
        BootstrapIdentityResult bootstrap;
        await using (var scope = services.CreateAsyncScope())
        {
            bootstrap = await scope.ServiceProvider
                .GetRequiredService<LocalIdentityOperations>()
                .CompleteBootstrapAsync(
                    CreateSecureRequest(),
                    bootstrapToken,
                    "Concurrent registration",
                    "Concurrent owner",
                    Guid.CreateVersion7(),
                    CreatePasskey([60, 61, 62]),
                    CancellationToken.None);
        }

        async Task<bool> RegisterAsync(byte discriminator)
        {
            await using var scope = services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AspNetIdentityUser>>();
            var user = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
            Assert.NotNull(user);
            try
            {
                await scope.ServiceProvider
                    .GetRequiredService<LocalIdentityOperations>()
                    .RegisterPasskeyAsync(
                        user,
                        CreatePasskey([discriminator, 70, 71]),
                        $"Device {discriminator}",
                        CancellationToken.None);
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or DbUpdateException
                    or Npgsql.PostgresException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(
            RegisterAsync(1),
            RegisterAsync(2),
            RegisterAsync(3));

        Assert.Equal(2, outcomes.Count(outcome => outcome));
        await using var verificationScope = services.CreateAsyncScope();
        var verificationUsers = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var persisted = await verificationUsers.FindByIdAsync(
            bootstrap.User.Id.ToString("D"));
        Assert.NotNull(persisted);
        Assert.Equal(3, (await verificationUsers.GetPasskeysAsync(persisted)).Count);
    }

    [Fact]
    public async Task ConcurrentRevocationPreservesOneAuthenticationPath()
    {
        BootstrapIdentityResult bootstrap;
        byte[][] credentialIds;
        await using (var scope = services.CreateAsyncScope())
        {
            var operations =
                scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
            bootstrap = await operations.CompleteBootstrapAsync(
                CreateSecureRequest(),
                bootstrapToken,
                "Concurrent revocation",
                "Concurrent owner",
                Guid.CreateVersion7(),
                CreatePasskey([80, 81, 82]),
                CancellationToken.None);
            await operations.RegisterPasskeyAsync(
                bootstrap.User,
                CreatePasskey([83, 84, 85]),
                "Second device",
                CancellationToken.None);
            var database =
                scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
            foreach (var code in await database.IdentityRecoveryCodes
                         .Where(code => code.ConsumedAt == null)
                         .ToArrayAsync())
            {
                code.Consume(DateTimeOffset.UtcNow);
            }

            await database.SaveChangesAsync();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AspNetIdentityUser>>();
            credentialIds = (await users.GetPasskeysAsync(bootstrap.User))
                .Select(passkey => passkey.CredentialId)
                .ToArray();
        }

        async Task<bool> RevokeAsync(byte[] credentialId)
        {
            await using var scope = services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AspNetIdentityUser>>();
            var user = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
            Assert.NotNull(user);
            try
            {
                await scope.ServiceProvider
                    .GetRequiredService<LocalIdentityOperations>()
                    .RevokePasskeyAsync(
                        user,
                        credentialId,
                        CancellationToken.None);
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or DbUpdateException
                    or Npgsql.PostgresException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(
            RevokeAsync(credentialIds[0]),
            RevokeAsync(credentialIds[1]));

        Assert.Single(outcomes, outcome => outcome);
        await using var verificationScope = services.CreateAsyncScope();
        var verificationUsers = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var persisted = await verificationUsers.FindByIdAsync(
            bootstrap.User.Id.ToString("D"));
        Assert.NotNull(persisted);
        Assert.Single(await verificationUsers.GetPasskeysAsync(persisted));
    }

    [Fact]
    public async Task RecentAuthenticationGrantIsConsumedExactlyOnce()
    {
        BootstrapIdentityResult bootstrap;
        var nonceHash = System.Security.Cryptography.SHA256.HashData(
            [1, 2, 3, 4, 5, 6, 7, 8]);
        var now = DateTimeOffset.UtcNow;
        await using (var scope = services.CreateAsyncScope())
        {
            bootstrap = await scope.ServiceProvider
                .GetRequiredService<LocalIdentityOperations>()
                .CompleteBootstrapAsync(
                    CreateSecureRequest(),
                    bootstrapToken,
                    "Recent auth workspace",
                    "Recent auth owner",
                    Guid.CreateVersion7(),
                    CreatePasskey([90, 91, 92]),
                    CancellationToken.None);
            await scope.ServiceProvider
                .GetRequiredService<IRecentAuthenticationGrantStore>()
                .IssueAsync(
                    bootstrap.User.Id,
                    nonceHash,
                    now.AddMinutes(5));
        }

        async Task<bool> ConsumeAsync()
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider
                .GetRequiredService<IRecentAuthenticationGrantStore>()
                .TryConsumeAsync(
                    bootstrap.User.Id,
                    nonceHash,
                    now);
        }

        var outcomes = await Task.WhenAll(ConsumeAsync(), ConsumeAsync());

        Assert.Single(outcomes, consumed => consumed);
        await using var verificationScope = services.CreateAsyncScope();
        Assert.False(await verificationScope.ServiceProvider
            .GetRequiredService<IRecentAuthenticationGrantStore>()
            .IsValidAsync(
                bootstrap.User.Id,
                nonceHash,
                now));
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(nonceHash);
    }

    private async Task<SeededIdentity> SeedTenantAsync(string normalizedName)
    {
        var context = new TenantPrincipalContext(
            TenantId.New(),
            AppUserId.New(),
            PrincipalId.New(),
            "integration-test");
        await using var scope = CreateScope(context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        database.AddRange(
            new Tenant(context.TenantId, normalizedName, normalizedName, "local"),
            new AppUser(context.AppUserId, normalizedName),
            new Principal(
                context.PrincipalId,
                context.TenantId,
                context.AppUserId,
                normalizedName),
            new Membership(
                MembershipId.New(),
                context.TenantId,
                context.AppUserId,
                context.PrincipalId,
                MembershipRole.Owner));
        await database.SaveChangesAsync();
        return new SeededIdentity(context);
    }

    private async Task AddContactAsync(SeededIdentity identity, string normalizedName)
    {
        await using var scope = CreateScope(identity.Context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        database.Contacts.Add(
            new Contact(
                ContactId.New(),
                identity.Context.TenantId,
                normalizedName,
                normalizedName));
        await database.SaveChangesAsync();
    }

    private async Task AddExternalIdentityAsync(
        SeededIdentity identity,
        string issuer,
        string subject)
    {
        await using var scope = CreateScope(identity.Context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        database.ExternalIdentities.Add(
            new ExternalIdentity(
                ExternalIdentityId.New(),
                identity.Context.AppUserId,
                issuer,
                subject));
        await database.SaveChangesAsync();
    }

    private AsyncServiceScope CreateScope(TenantPrincipalContext context)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedTenantPrincipalContext>().Set(context);
        return scope;
    }

    private sealed record SeededIdentity(TenantPrincipalContext Context);

    private static HttpRequest CreateSecureRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Headers.Origin = "https://localhost";
        return context.Request;
    }

    private static UserPasskeyInfo CreatePasskey(byte[] credentialId) =>
        new(
            credentialId,
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(77),
            DateTimeOffset.UtcNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [1],
            clientDataJson: [2]);

    private static void MakeReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }
    }

    private static void DeleteReadOnlyFile(string path)
    {
        if (OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        File.Delete(path);
    }
}
