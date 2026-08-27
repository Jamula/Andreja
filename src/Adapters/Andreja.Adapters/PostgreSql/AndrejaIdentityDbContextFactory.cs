using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Andreja.Adapters.PostgreSql;

public sealed class AndrejaIdentityDbContextFactory
    : IDesignTimeDbContextFactory<AndrejaIdentityDbContext>
{
    public AndrejaIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ANDREJA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Database=andreja_migrations;Username=postgres";
        var applicationServices = new ServiceCollection()
            .AddOptions()
            .Configure<IdentityOptions>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AndrejaIdentityDbContext>()
            .UseApplicationServiceProvider(applicationServices)
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "identity"))
            .Options;
        var context = new ScopedTenantPrincipalContext();
        context.Set(
            new TenantPrincipalContext(
                TenantId.New(),
                AppUserId.New(),
                PrincipalId.New(),
                "migration-design"));
        return new AndrejaIdentityDbContext(options, context);
    }
}
