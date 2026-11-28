using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts.Proposals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Andreja.Adapters.PostgreSql;

public static class PostgreSqlServiceCollectionExtensions
{
    private static readonly Action<Npgsql.NpgsqlDataSourceBuilder> ContentSafeTracingAction =
        ConfigureContentSafeTracing;

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
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__migrations", "identity");
                    npgsql.ConfigureDataSource(ContentSafeTracingAction);
                }));
        services.AddScoped<IIdentityStore, PostgreSqlIdentityStore>();

        return services;
    }

    public const string OperationalDataSourceName = "andreja-postgresql";

    public static void ConfigureContentSafeTracing(
        Npgsql.NpgsqlDataSourceBuilder dataSourceBuilder)
    {
        ArgumentNullException.ThrowIfNull(dataSourceBuilder);

        dataSourceBuilder.Name = OperationalDataSourceName;
        dataSourceBuilder.ConfigureTracing(tracing => tracing
            .EnableFirstResponseEvent(false)
            .ConfigureCommandSpanNameProvider(static _ => "postgresql.query")
            .ConfigureBatchSpanNameProvider(static _ => "postgresql.batch")
            .ConfigureCopyOperationSpanNameProvider(static _ => "postgresql.copy")
            .ConfigureCommandEnrichmentCallback(static (activity, _) => ClearDatabaseStatementTags(activity))
            .ConfigureBatchEnrichmentCallback(static (activity, _) => ClearDatabaseStatementTags(activity))
            .ConfigureCopyOperationEnrichmentCallback(static (activity, _) => ClearDatabaseStatementTags(activity)));
    }

    private static void ClearDatabaseStatementTags(System.Diagnostics.Activity activity)
    {
        activity.SetTag("db.query.text", value: null);
        activity.SetTag("db.statement", value: null);
    }

    public static IServiceCollection AddAndrejaOpenLoopsPostgreSql(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IOpenLoopsTaskStore, PostgreSqlOpenLoopsTaskStore>();
        services.AddScoped<PostgreSqlProposalStore>();
        services.AddScoped<IProposalStore>(
            provider => provider.GetRequiredService<PostgreSqlProposalStore>());
        services.AddScoped<IProposalAuditSink>(
            provider => provider.GetRequiredService<PostgreSqlProposalStore>());
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
