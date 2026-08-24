using Andreja.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Andreja.Adapters.PostgreSql;

public static class PostgreSqlServiceCollectionExtensions
{
    public static IServiceCollection AddAndrejaIdentityPostgreSql(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<ScopedTenantPrincipalContext>();
        services.AddScoped<ITenantPrincipalContextAccessor>(
            provider => provider.GetRequiredService<ScopedTenantPrincipalContext>());
        services.AddDbContext<AndrejaIdentityDbContext>(
            options => options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "identity")));
        services.AddScoped<IIdentityStore, PostgreSqlIdentityStore>();

        return services;
    }
}
