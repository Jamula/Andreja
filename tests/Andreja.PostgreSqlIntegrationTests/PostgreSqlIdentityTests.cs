using Andreja.Adapters.PostgreSql;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts.Proposals;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Andreja.PostgreSqlIntegrationTests;

[TestClass]
public sealed class PostgreSqlIdentityTests
{
    private static readonly string[] ExpectedVisibleTenantNames = ["ALPHA"];
    private string connectionString = string.Empty;

    private ServiceProvider services = null!;
    private string tokenPath = null!;
    private string bootstrapToken = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        connectionString = Environment.GetEnvironmentVariable("ANDREJA_TEST_POSTGRES")
            ?? throw new InvalidOperationException(
                "BLOCKED: set ANDREJA_TEST_POSTGRES to a disposable local PostgreSQL database.");

        try
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
            collection.AddAndrejaOpenLoopsPostgreSql();
            collection.AddScoped<OpenLoopsTaskApplication>();
            services = collection.BuildServiceProvider();

            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
            await database.Database.EnsureDeletedAsync();
            await database.Database.MigrateAsync();
        }
        catch (Exception initializationException)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "PostgreSQL identity test initialization and cleanup both failed.",
                    initializationException,
                    cleanupException);
            }

            throw;
        }
    }

    [TestCleanup]
    public async Task DisposeAsync()
    {
        List<Exception> cleanupFailures = [];
        var currentServices = services;
        if (currentServices is not null)
        {
            try
            {
                await using var scope = currentServices.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
                await database.Database.EnsureDeletedAsync();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
            finally
            {
                try
                {
                    await currentServices.DisposeAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
                finally
                {
                    services = null!;
                }
            }
        }

        try
        {
            if (!string.IsNullOrEmpty(tokenPath))
            {
                DeleteReadOnlyFile(tokenPath);
            }
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }
        finally
        {
            tokenPath = string.Empty;
            bootstrapToken = string.Empty;
        }

        if (cleanupFailures.Count > 0)
        {
            throw new AggregateException(
                "PostgreSQL identity test cleanup failed.",
                cleanupFailures);
        }
    }

    [TestMethod]
    public async Task MigrationCreatesEmptyDatabaseAndEnforcesTwoTenantIsolation()
    {
        var tenantA = await SeedTenantAsync("TENANT-A");
        var tenantB = await SeedTenantAsync("TENANT-B");

        await AddContactAsync(tenantA, "ALPHA");
        await AddContactAsync(tenantB, "BETA");

        await using var scope = CreateScope(tenantA.Context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        var visible = await database.Contacts.Select(contact => contact.NormalizedName).ToArrayAsync();

        CollectionAssert.AreEqual(ExpectedVisibleTenantNames, visible);
        database.Contacts.Add(
            new Contact(ContactId.New(), tenantB.Context.TenantId, "FORBIDDEN", "Forbidden"));
        await Assert.ThrowsExactlyAsync<IdentityAccessDeniedException>(
            () => database.SaveChangesAsync());
    }

    [TestMethod]
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
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => database.SaveChangesAsync());
        }

        await AddExternalIdentityAsync(tenantA, "https://issuer.example", "same-subject");
        await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => AddExternalIdentityAsync(
                tenantB,
                "https://issuer.example",
                "same-subject"));
    }

    [TestMethod]
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
            Assert.AreEqual(TaskMutationOutcome.Applied, created.Outcome);
        }

        await using (var scope = CreateScope(contextB))
        {
            var store = scope.ServiceProvider.GetRequiredService<IOpenLoopsTaskStore>();
            Assert.IsEmpty(await store.ListAsync(contextB));
            var crossTenant = await store.CompleteAsync(
                contextB,
                task.Id,
                task.Version,
                "complete-cross-tenant",
                createdAt.AddMinutes(1));
            Assert.AreEqual(TaskMutationOutcome.NotFound, crossTenant.Outcome);
        }

        await using (var scope = CreateScope(contextA))
        {
            var store = scope.ServiceProvider.GetRequiredService<IOpenLoopsTaskStore>();
            var persisted = Assert.ContainsSingle(await store.ListAsync(contextA));
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

            Assert.AreEqual(TaskMutationOutcome.Applied, completed.Outcome);
            Assert.AreEqual(TaskMutationOutcome.IdempotentReplay, replay.Outcome);
            Assert.AreEqual(OpenLoopTaskStatus.Completed, replay.Task?.Status);
        }
    }

    [TestMethod]
    public async Task ProposalConfirmationIsAtomicAndSurvivesProcessRestart()
    {
        var identity = await SeedTenantAsync("PROPOSAL-RESTART");
        var context = identity.Context with { Purpose = OpenLoopsPolicy.Purpose };
        Proposal proposal;
        ProposalConfirmationResult applied;
        await using (var scope = CreateScope(context))
        {
            var application = scope.ServiceProvider.GetRequiredService<OpenLoopsTaskApplication>();
            proposal = await application.ProposeAsync(
                context,
                new("Durable confirmation", "Restart proof", null),
                "assistant:restart-proof");
            applied = await application.ConfirmAsync(
                context,
                proposal.ProposalId,
                proposal.Version,
                "restart-confirmation");
        }

        await using var restartedServices = CreateProposalServiceProvider();
        await using var restartedScope = restartedServices.CreateAsyncScope();
        restartedScope.ServiceProvider
            .GetRequiredService<ScopedTenantPrincipalContext>()
            .Set(context);
        var restarted = restartedScope.ServiceProvider
            .GetRequiredService<OpenLoopsTaskApplication>();
        var replay = await restarted.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version,
            "restart-confirmation");
        var conflictingReuse = await restarted.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version + 1,
            "restart-confirmation");
        var invalidState = await restarted.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version + 1,
            "restart-invalid-state");
        var database = restartedScope.ServiceProvider
            .GetRequiredService<AndrejaIdentityDbContext>();

        Assert.AreEqual(ProposalTransitionOutcome.Applied, applied.Outcome);
        Assert.AreEqual(ProposalTransitionOutcome.IdempotentReplay, replay.Outcome);
        Assert.AreEqual(applied.Task?.Id, replay.Task?.Id);
        Assert.AreEqual(ProposalTransitionOutcome.Conflict, conflictingReuse.Outcome);
        Assert.AreEqual(ProposalTransitionOutcome.InvalidState, invalidState.Outcome);
        Assert.AreEqual(ProposalState.Confirmed, replay.Proposal?.State);
        Assert.ContainsSingle(await database.OpenLoopTasks.AsNoTracking().ToArrayAsync());
        var audits = await database.ProposalAudits.AsNoTracking().ToArrayAsync();
        Assert.AreEqual(2, audits.Length);
        Assert.AreEqual(
            1,
            audits.Count(audit => audit.Outcome == ProposalTransitionOutcome.Applied));
        Assert.AreEqual(
            2,
            await database.ProposalReceipts.AsNoTracking().CountAsync());
    }

    [TestMethod]
    public async Task ProposalCanonicalPayloadSurvivesTimestampPrecisionAndRestart()
    {
        var identity = await SeedTenantAsync("PROPOSAL-PRECISION");
        var context = identity.Context with { Purpose = OpenLoopsPolicy.Purpose };
        var preciseNow = new DateTimeOffset(
                2026,
                8,
                24,
                21,
                14,
                33,
                TimeSpan.Zero)
            .AddTicks(1_234_567);
        var suppliedDueAt = new DateTimeOffset(
                2026,
                8,
                25,
                9,
                30,
                0,
                TimeSpan.FromHours(5.5))
            .AddTicks(765_437);
        Proposal proposal;
        await using (var proposingServices = CreateProposalServiceProvider(
                         timeProvider: new FixedTimeProvider(preciseNow)))
        await using (var proposingScope = proposingServices.CreateAsyncScope())
        {
            proposingScope.ServiceProvider
                .GetRequiredService<ScopedTenantPrincipalContext>()
                .Set(context);
            proposal = await proposingScope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ProposeAsync(
                    context,
                    new("Canonical precision", "PostgreSQL round-trip", suppliedDueAt),
                    "assistant:canonical-precision");
        }

        Assert.AreEqual(TimeSpan.Zero, proposal.CreatedAt.Offset);
        Assert.AreEqual(0, proposal.CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond);
        using (var payload = JsonDocument.Parse(proposal.Operation.CanonicalPayload))
        {
            var root = payload.RootElement;
            var payloadCreatedAt = root.GetProperty("createdAt").GetDateTimeOffset();
            var payloadDueAt = root.GetProperty("dueAt").GetDateTimeOffset();
            Assert.AreEqual(proposal.CreatedAt, payloadCreatedAt);
            Assert.AreEqual(TimeSpan.Zero, payloadDueAt.Offset);
            Assert.AreEqual(0, payloadDueAt.Ticks % TimeSpan.TicksPerMicrosecond);
        }

        ProposalConfirmationResult applied;
        await using (var confirmingServices = CreateProposalServiceProvider(
                         timeProvider: new FixedTimeProvider(preciseNow.AddMinutes(1))))
        await using (var persistedScope = confirmingServices.CreateAsyncScope())
        {
            persistedScope.ServiceProvider
                .GetRequiredService<ScopedTenantPrincipalContext>()
                .Set(context);
            var application = persistedScope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>();
            var persisted = await application.GetProposalAsync(context, proposal.ProposalId);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(proposal.CreatedAt, persisted.CreatedAt);
            Assert.AreEqual(
                proposal.Operation.CanonicalPayload,
                persisted.Operation.CanonicalPayload);
            Assert.AreEqual(proposal.Operation.PayloadDigest, persisted.Operation.PayloadDigest);
            applied = await application.ConfirmAsync(
                context,
                proposal.ProposalId,
                proposal.Version,
                "precision-confirmation");
        }

        await using var restartedServices = CreateProposalServiceProvider(
            timeProvider: new FixedTimeProvider(preciseNow.AddMinutes(2)));
        await using var restartedScope = restartedServices.CreateAsyncScope();
        restartedScope.ServiceProvider
            .GetRequiredService<ScopedTenantPrincipalContext>()
            .Set(context);
        var restarted = restartedScope.ServiceProvider
            .GetRequiredService<OpenLoopsTaskApplication>();
        var replay = await restarted.ConfirmAsync(
            context,
            proposal.ProposalId,
            proposal.Version,
            "precision-confirmation");
        var persistedTask = Assert.ContainsSingle(await restarted.ListAsync(context));

        Assert.AreEqual(ProposalTransitionOutcome.Applied, applied.Outcome);
        Assert.AreEqual(ProposalTransitionOutcome.IdempotentReplay, replay.Outcome);
        Assert.AreEqual(applied.Task?.Id, replay.Task?.Id);
        Assert.AreEqual(TimeSpan.Zero, persistedTask.CreatedAt.Offset);
        Assert.AreEqual(0, persistedTask.CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.AreEqual(TimeSpan.Zero, persistedTask.DueAt?.Offset);
        Assert.AreEqual(0, persistedTask.DueAt?.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [TestMethod]
    public async Task ConcurrentProposalConfirmationsCommitExactlyOneEffect()
    {
        var identity = await SeedTenantAsync("PROPOSAL-CONCURRENCY");
        var context = identity.Context with { Purpose = OpenLoopsPolicy.Purpose };
        Proposal proposal;
        await using (var scope = CreateScope(context))
        {
            proposal = await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ProposeAsync(
                    context,
                    new("Concurrent confirmation", null, null),
                    "assistant:concurrency");
        }

        async Task<ProposalConfirmationResult> ConfirmAsync(string key)
        {
            await using var scope = CreateScope(context);
            return await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ConfirmAsync(context, proposal.ProposalId, proposal.Version, key);
        }

        var results = await Task.WhenAll(
            ConfirmAsync("concurrent-confirm-a"),
            ConfirmAsync("concurrent-confirm-b"));

        Assert.AreEqual(
            1,
            results.Count(result => result.Outcome == ProposalTransitionOutcome.Applied));
        Assert.AreEqual(
            1,
            results.Count(result => result.Outcome == ProposalTransitionOutcome.Conflict));
        await using var verification = CreateScope(context);
        var database = verification.ServiceProvider
            .GetRequiredService<AndrejaIdentityDbContext>();
        Assert.ContainsSingle(await database.OpenLoopTasks.AsNoTracking().ToArrayAsync());
        Assert.ContainsSingle(
            await database.ProposalAudits
                .AsNoTracking()
                .Where(audit => audit.Outcome == ProposalTransitionOutcome.Applied)
                .ToArrayAsync());
    }

    [TestMethod]
    public async Task ConfirmationRecoversFromCrashBeforeAndAfterCommit()
    {
        var identity = await SeedTenantAsync("PROPOSAL-CRASH");
        var context = identity.Context with { Purpose = OpenLoopsPolicy.Purpose };
        Proposal beforeCommitProposal;
        Proposal afterCommitProposal;
        await using (var scope = CreateScope(context))
        {
            var application = scope.ServiceProvider.GetRequiredService<OpenLoopsTaskApplication>();
            beforeCommitProposal = await application.ProposeAsync(
                context,
                new("Crash before commit", null, null),
                "assistant:before-commit");
            afterCommitProposal = await application.ProposeAsync(
                context,
                new("Crash after commit", null, null),
                "assistant:after-commit");
        }

        await using (var crashingServices = CreateProposalServiceProvider(
                         new ThrowAtCheckpoint(ProposalConfirmationCheckpoint.BeforeCommit)))
        await using (var crashingScope = crashingServices.CreateAsyncScope())
        {
            crashingScope.ServiceProvider
                .GetRequiredService<ScopedTenantPrincipalContext>()
                .Set(context);
            await Assert.ThrowsExactlyAsync<SimulatedProcessCrashException>(
                () => crashingScope.ServiceProvider
                    .GetRequiredService<OpenLoopsTaskApplication>()
                    .ConfirmAsync(
                        context,
                        beforeCommitProposal.ProposalId,
                        beforeCommitProposal.Version,
                        "crash-before-commit"));
        }

        await using (var verification = CreateScope(context))
        {
            var application = verification.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>();
            var pending = await application.GetProposalAsync(
                context,
                beforeCommitProposal.ProposalId);
            Assert.AreEqual(ProposalState.Pending, pending?.State);
            Assert.IsEmpty(await application.ListAsync(context));
            var retry = await application.ConfirmAsync(
                context,
                beforeCommitProposal.ProposalId,
                beforeCommitProposal.Version,
                "crash-before-commit");
            Assert.AreEqual(ProposalTransitionOutcome.Applied, retry.Outcome);
        }

        await using (var crashingServices = CreateProposalServiceProvider(
                         new ThrowAtCheckpoint(ProposalConfirmationCheckpoint.AfterCommit)))
        await using (var crashingScope = crashingServices.CreateAsyncScope())
        {
            crashingScope.ServiceProvider
                .GetRequiredService<ScopedTenantPrincipalContext>()
                .Set(context);
            await Assert.ThrowsExactlyAsync<SimulatedProcessCrashException>(
                () => crashingScope.ServiceProvider
                    .GetRequiredService<OpenLoopsTaskApplication>()
                    .ConfirmAsync(
                        context,
                        afterCommitProposal.ProposalId,
                        afterCommitProposal.Version,
                        "crash-after-commit"));
        }

        await using (var restarted = CreateScope(context))
        {
            var replay = await restarted.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ConfirmAsync(
                    context,
                    afterCommitProposal.ProposalId,
                    afterCommitProposal.Version,
                    "crash-after-commit");
            Assert.AreEqual(ProposalTransitionOutcome.IdempotentReplay, replay.Outcome);
            Assert.AreEqual(2, (await restarted.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ListAsync(context)).Count);
        }
    }

    [TestMethod]
    public async Task ProposalConfirmationFailsClosedForWrongIdentityPurposeAndOrphanReferences()
    {
        var owner = await SeedTenantAsync("PROPOSAL-OWNER");
        var otherTenant = await SeedTenantAsync("PROPOSAL-OTHER");
        var context = owner.Context with { Purpose = OpenLoopsPolicy.Purpose };
        var otherPrincipal = await SeedAdditionalPrincipalAsync(owner, "PROPOSAL-PEER");
        Proposal proposal;
        await using (var scope = CreateScope(context))
        {
            proposal = await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ProposeAsync(
                    context,
                    new("Tenant constrained", null, null),
                    "assistant:tenant-constraint");
        }

        var otherTenantContext = otherTenant.Context with { Purpose = OpenLoopsPolicy.Purpose };
        await using (var scope = CreateScope(otherTenantContext))
        {
            var result = await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ConfirmAsync(
                    otherTenantContext,
                    proposal.ProposalId,
                    proposal.Version,
                    "wrong-tenant-confirm");
            Assert.AreEqual(ProposalTransitionOutcome.NotFound, result.Outcome);
        }

        var wrongUser = context with { AppUserId = AppUserId.New() };
        await using (var scope = CreateScope(wrongUser))
        {
            var result = await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ConfirmAsync(
                    wrongUser,
                    proposal.ProposalId,
                    proposal.Version,
                    "wrong-user-confirm");
            Assert.AreEqual(ProposalTransitionOutcome.Denied, result.Outcome);
        }

        await using (var scope = CreateScope(otherPrincipal))
        {
            var result = await scope.ServiceProvider
                .GetRequiredService<OpenLoopsTaskApplication>()
                .ConfirmAsync(
                    otherPrincipal,
                    proposal.ProposalId,
                    proposal.Version,
                    "wrong-principal-confirm");
            Assert.AreEqual(ProposalTransitionOutcome.Denied, result.Outcome);
        }

        var wrongPurpose = context with { Purpose = "task.export" };
        await using (var scope = CreateScope(wrongPurpose))
        {
            await Assert.ThrowsExactlyAsync<IdentityAccessDeniedException>(
                () => scope.ServiceProvider
                    .GetRequiredService<OpenLoopsTaskApplication>()
                    .ConfirmAsync(
                        wrongPurpose,
                        proposal.ProposalId,
                        proposal.Version,
                        "wrong-purpose-confirm"));
        }

        await using (var scope = CreateScope(context))
        {
            var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
            var orphan = ProposalRecord.FromDomain(
                proposal with
                {
                    ProposalId = Guid.CreateVersion7(),
                    ActorId = otherTenant.Context.PrincipalId.Value,
                    Source = proposal.Source with
                    {
                        ActorId = otherTenant.Context.PrincipalId.Value,
                    },
                },
                context.AppUserId);
            database.Proposals.Add(orphan);
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => database.SaveChangesAsync());
        }
    }

    [TestMethod]
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
            Assert.ContainsSingle(await database.Tenants.IgnoreQueryFilters().ToArrayAsync());
            Assert.ContainsSingle(await database.Memberships.IgnoreQueryFilters().ToArrayAsync());
            Assert.ContainsSingle(await database.IdentityBootstrapStates.ToArrayAsync());
            var storedCodes = await database.IdentityRecoveryCodes.ToArrayAsync();
            Assert.AreEqual(8, storedCodes.Length);
            foreach (var code in storedCodes)
            {
                Assert.AreEqual(32, code.LookupHash.Length);
                Assert.AreEqual(16, code.Salt.Length);
                Assert.AreEqual(32, code.VerificationHash.Length);
            }
            Assert.IsFalse(created.RecoveryCodes.Any(plaintext => storedCodes.Any(code =>
                    Convert.ToBase64String(code.LookupHash) == plaintext
                    || Convert.ToBase64String(code.VerificationHash) == plaintext)));

            var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetIdentityUser>>();
            var persisted = Assert.ContainsSingle(
                await users.Users.Where(user => user.Id == created.User.Id).ToArrayAsync());
            Assert.AreEqual(reservedCredentialUserId, persisted.Id);
            var passkey = Assert.ContainsSingle(await users.GetPasskeysAsync(persisted));
            CollectionAssert.AreEqual(credentialId, passkey.CredentialId);
            Assert.AreEqual(
                "Local owner",
                await scope.ServiceProvider
                    .GetRequiredService<IAppUserDisplayNameResolver>()
                    .ResolveAsync(persisted));

            var operations = scope.ServiceProvider.GetRequiredService<LocalIdentityOperations>();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
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

    [TestMethod]
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

        Assert.AreEqual(1, outcomes.Count(outcome => outcome));
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        Assert.ContainsSingle(await database.IdentityBootstrapStates.ToArrayAsync());
        Assert.ContainsSingle(await database.Tenants.IgnoreQueryFilters().ToArrayAsync());
        Assert.ContainsSingle(await database.Memberships.IgnoreQueryFilters().ToArrayAsync());
    }

    [TestMethod]
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
        Assert.IsNotNull(before);
        var oldStamp = before.SecurityStamp;
        var start = await operations.BeginRecoveryAsync(
            bootstrap.RecoveryCodes[0],
            CancellationToken.None);
        Assert.IsNotNull(start);
        byte[] replacementCredential = [20, 21, 22, 23];

        var recovered = await operations.CompleteRecoveryAsync(
            start,
            CreatePasskey(replacementCredential),
            CancellationToken.None);

        Assert.AreEqual(8, recovered.RecoveryCodes.Count);
        var after = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
        Assert.IsNotNull(after);
        Assert.AreNotEqual(oldStamp, after.SecurityStamp);
        var passkey = Assert.ContainsSingle(await users.GetPasskeysAsync(after));
        CollectionAssert.AreEqual(replacementCredential, passkey.CredentialId);
        Assert.IsNull(await operations.BeginRecoveryAsync(
            bootstrap.RecoveryCodes[0],
            CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => operations.CompleteRecoveryAsync(
                start,
                CreatePasskey([30, 31, 32]),
                CancellationToken.None));
    }

    [TestMethod]
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
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
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
        var remaining = Assert.ContainsSingle(await users.GetPasskeysAsync(bootstrap.User));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => operations.RevokePasskeyAsync(
                bootstrap.User,
                remaining.CredentialId,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task ConcurrentDistinctRegistrationsSerializeToPasskeyLimit()
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

        var ready = 0;
        var allUsersLoaded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> RegisterAsync(byte discriminator)
        {
            await using var scope = services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AspNetIdentityUser>>();
            var user = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
            Assert.IsNotNull(user);
            if (Interlocked.Increment(ref ready) == 3)
            {
                allUsersLoaded.SetResult();
            }

            await allUsersLoaded.Task;
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

        Assert.AreEqual(2, outcomes.Count(outcome => outcome));
        await using var verificationScope = services.CreateAsyncScope();
        var verificationUsers = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var persisted = await verificationUsers.FindByIdAsync(
            bootstrap.User.Id.ToString("D"));
        Assert.IsNotNull(persisted);
        var passkeys = await verificationUsers.GetPasskeysAsync(persisted);
        Assert.AreEqual(3, passkeys.Count);
        Assert.IsTrue(passkeys.Any(
            passkey => passkey.CredentialId.SequenceEqual(new byte[] { 60, 61, 62 })));
        Assert.AreEqual(
            2,
            passkeys.Count(passkey =>
                passkey.CredentialId.Length == 3
                && passkey.CredentialId[1] == 70
                && passkey.CredentialId[2] == 71));
    }

    [TestMethod]
    public async Task ConcurrentDuplicatePasskeyRegistrationFailsClosed()
    {
        BootstrapIdentityResult bootstrap;
        await using (var scope = services.CreateAsyncScope())
        {
            bootstrap = await scope.ServiceProvider
                .GetRequiredService<LocalIdentityOperations>()
                .CompleteBootstrapAsync(
                    CreateSecureRequest(),
                    bootstrapToken,
                    "Concurrent duplicate",
                    "Concurrent owner",
                    Guid.CreateVersion7(),
                    CreatePasskey([72, 73, 74]),
                    CancellationToken.None);
        }

        var ready = 0;
        var bothUsersLoaded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] duplicateCredentialId = [75, 76, 77];

        async Task<bool> RegisterAsync(string deviceName)
        {
            await using var scope = services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AspNetIdentityUser>>();
            var user = await users.FindByIdAsync(bootstrap.User.Id.ToString("D"));
            Assert.IsNotNull(user);
            if (Interlocked.Increment(ref ready) == 2)
            {
                bothUsersLoaded.SetResult();
            }

            await bothUsersLoaded.Task;
            try
            {
                await scope.ServiceProvider
                    .GetRequiredService<LocalIdentityOperations>()
                    .RegisterPasskeyAsync(
                        user,
                        CreatePasskey(duplicateCredentialId),
                        deviceName,
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
            RegisterAsync("Duplicate one"),
            RegisterAsync("Duplicate two"));

        Assert.AreEqual(1, outcomes.Count(outcome => outcome));
        await using var verificationScope = services.CreateAsyncScope();
        var verificationUsers = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var persisted = await verificationUsers.FindByIdAsync(
            bootstrap.User.Id.ToString("D"));
        Assert.IsNotNull(persisted);
        var passkeys = await verificationUsers.GetPasskeysAsync(persisted);
        Assert.AreEqual(2, passkeys.Count);
        Assert.AreEqual(
            1,
            passkeys.Count(passkey => passkey.CredentialId.SequenceEqual(duplicateCredentialId)));
    }

    [TestMethod]
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
            Assert.IsNotNull(user);
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

        Assert.AreEqual(1, outcomes.Count(outcome => outcome));
        await using var verificationScope = services.CreateAsyncScope();
        var verificationUsers = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<AspNetIdentityUser>>();
        var persisted = await verificationUsers.FindByIdAsync(
            bootstrap.User.Id.ToString("D"));
        Assert.IsNotNull(persisted);
        Assert.ContainsSingle(await verificationUsers.GetPasskeysAsync(persisted));
    }

    [TestMethod]
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

        Assert.AreEqual(1, outcomes.Count(consumed => consumed));
        await using var verificationScope = services.CreateAsyncScope();
        Assert.IsFalse(await verificationScope.ServiceProvider
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

    private async Task<TenantPrincipalContext> SeedAdditionalPrincipalAsync(
        SeededIdentity tenant,
        string displayName)
    {
        var context = new TenantPrincipalContext(
            tenant.Context.TenantId,
            AppUserId.New(),
            PrincipalId.New(),
            OpenLoopsPolicy.Purpose);
        await using var scope = CreateScope(tenant.Context);
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        database.AddRange(
            new AppUser(context.AppUserId, displayName),
            new Principal(
                context.PrincipalId,
                context.TenantId,
                context.AppUserId,
                displayName),
            new Membership(
                MembershipId.New(),
                context.TenantId,
                context.AppUserId,
                context.PrincipalId,
                MembershipRole.Member));
        await database.SaveChangesAsync();
        return context;
    }

    private AsyncServiceScope CreateScope(TenantPrincipalContext context)
    {
        var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedTenantPrincipalContext>().Set(context);
        return scope;
    }

    private ServiceProvider CreateProposalServiceProvider(
        IProposalConfirmationFaultInjector? faultInjector = null,
        TimeProvider? timeProvider = null)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddOptions();
        collection.AddSingleton(timeProvider ?? TimeProvider.System);
        collection.AddAndrejaIdentityPostgreSql(connectionString);
        collection.AddAndrejaOpenLoopsPostgreSql();
        collection.AddScoped<OpenLoopsTaskApplication>();
        if (faultInjector is not null)
        {
            collection.AddScoped<PostgreSqlProposalStore>(provider => new(
                provider.GetRequiredService<AndrejaIdentityDbContext>(),
                provider.GetRequiredService<ITenantPrincipalContextAccessor>(),
                faultInjector));
        }

        return collection.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
    }

    private sealed record SeededIdentity(TenantPrincipalContext Context);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ThrowAtCheckpoint(ProposalConfirmationCheckpoint target)
        : IProposalConfirmationFaultInjector
    {
        public ValueTask OnCheckpointAsync(
            ProposalConfirmationCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (checkpoint == target)
            {
                throw new SimulatedProcessCrashException();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class SimulatedProcessCrashException : Exception;

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
