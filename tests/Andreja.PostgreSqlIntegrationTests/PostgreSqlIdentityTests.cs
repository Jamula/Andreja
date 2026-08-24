using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Microsoft.AspNetCore.Identity;
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

    public async Task InitializeAsync()
    {
        var databaseName = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database;
        if (string.IsNullOrWhiteSpace(databaseName)
            || !databaseName.StartsWith("andreja_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ANDREJA_TEST_POSTGRES must target a disposable database named andreja_test_*.");
        }

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddOptions();
        collection.Configure<IdentityOptions>(
            options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        collection.AddScoped<ScopedTenantPrincipalContext>();
        collection.AddScoped<ITenantPrincipalContextAccessor>(
            provider => provider.GetRequiredService<ScopedTenantPrincipalContext>());
        collection.AddDbContext<AndrejaIdentityDbContext>(
            options => options.UseNpgsql(connectionString));
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
}
