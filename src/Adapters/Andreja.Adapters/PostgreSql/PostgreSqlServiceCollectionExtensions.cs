using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
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

    public static IServiceCollection AddAndrejaOpenLoopsPostgreSql(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IOpenLoopsTaskStore, PostgreSqlOpenLoopsTaskStore>();
        return services;
    }

    public static string BuildLocalConnectionString(
        string host,
        int port,
        string database,
        string username,
        string passwordFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordFile);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("The PostgreSQL password file is empty.");
        }

        return new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            Pooling = true,
            IncludeErrorDetail = false,
        }.ConnectionString;
    }
}
