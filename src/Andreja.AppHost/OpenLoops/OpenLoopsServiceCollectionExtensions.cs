using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.Assistant.OpenAiCompatible;
using Andreja.Adapters.PostgreSql;
using Andreja.Api.Contracts.OpenLoops;
using Andreja.AppHost.Hosting;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Proposals;
using Andreja.Platform.Contracts.Skills;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.OpenLoops;

public sealed class OpenLoopsOptions
{
    public const string SectionName = "Andreja:OpenLoops";

    public bool Enabled { get; init; }

    [Required]
    [Url]
    public string PublicOrigin { get; init; } = "https://localhost:5001";

    public string AssistantProvider { get; init; } = "deterministic";
}

public static class OpenLoopsServiceCollectionExtensions
{
    public static IServiceCollection AddAndrejaOpenLoops(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(OpenLoopsOptions.SectionName);
        var openLoops = section.Get<OpenLoopsOptions>() ?? new();
        services.AddOptions<OpenLoopsOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                options => Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
                    && origin.Scheme is "http" or "https",
                "The Open Loops public origin must be an absolute HTTP(S) URI.")
            .Validate(
                options => options.AssistantProvider is "deterministic" or "openai-compatible",
                "The assistant provider must be deterministic or openai-compatible.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddCascadingAuthenticationState();
        services.AddAntiforgery(options => options.HeaderName = OpenLoopsApi.AntiforgeryHeader);
        services.AddAuthentication(IdentityConstants.ApplicationScheme);
        services.AddAuthorizationBuilder()
            .AddPolicy(
                "andreja-user",
                policy => policy.RequireAuthenticatedUser());

        if (!openLoops.Enabled)
        {
            return services;
        }

        var operations = configuration
            .GetRequiredSection(AndrejaOperationsOptions.SectionName)
            .Get<AndrejaOperationsOptions>()
            ?? throw new InvalidOperationException("Andreja operations configuration is required.");

        if (operations.Database.Enabled)
        {
            var database = operations.Database;
            var connectionString = PostgreSqlServiceCollectionExtensions.BuildLocalConnectionString(
                database.Host,
                database.Port,
                database.Name,
                database.Username,
                database.PasswordFile);
            services.AddAndrejaIdentityPostgreSql(connectionString);
            services.AddAndrejaOpenLoopsPostgreSql();

            var identitySection = configuration.GetSection(LocalIdentityOptions.SectionName);
            if (!identitySection.Exists())
            {
                throw new InvalidOperationException(
                    "Andreja identity configuration is required when Open Loops is enabled.");
            }

            services.AddAndrejaLocalIdentity(identitySection);
        }
        else if (environment.IsDevelopment())
        {
            services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme);
            services.AddScoped<ScopedTenantPrincipalContext>();
            services.AddScoped<ITenantPrincipalContextAccessor>(
                provider => provider.GetRequiredService<ScopedTenantPrincipalContext>());
            services.AddSingleton<IOpenLoopsTaskStore, InMemoryOpenLoopsTaskStore>();
        }
        else
        {
            throw new InvalidOperationException(
                "Open Loops requires PostgreSQL outside Development.");
        }

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<InMemoryProposalStore>();
        services.AddSingleton<IProposalStore>(
            provider => provider.GetRequiredService<InMemoryProposalStore>());
        services.AddSingleton<IProposalAuditSink>(
            provider => provider.GetRequiredService<InMemoryProposalStore>());
        services.AddScoped<OpenLoopsTaskApplication>();
        services.AddScoped<ISkillHost>(
            provider => OpenLoopsSkill.CreateHost(
                provider.GetRequiredService<OpenLoopsTaskApplication>()));
        services.AddSingleton<IAssistantProvider>(_ =>
            openLoops.AssistantProvider == "openai-compatible"
                ? new OpenAiCompatibleAssistantAdapter()
                : OpenLoopsSkill.CreateDeterministicProvider());
        services.AddScoped<OpenLoopsAssistantService>();

        services.AddTransient<AuthenticationCookieForwardingHandler>();
        services.AddHttpClient<IOpenLoopsApiClient, OpenLoopsApiClient>(
                (provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<OpenLoopsOptions>>().Value;
                    client.BaseAddress = new Uri(options.PublicOrigin, UriKind.Absolute);
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
            })
            .AddHttpMessageHandler<AuthenticationCookieForwardingHandler>();
        return services;
    }

    public static async Task ApplyAndrejaOpenLoopsMigrationsAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        var openLoops = application.Configuration
            .GetSection(OpenLoopsOptions.SectionName)
            .Get<OpenLoopsOptions>();
        var operations = application.Configuration
            .GetSection(AndrejaOperationsOptions.SectionName)
            .Get<AndrejaOperationsOptions>();
        if (openLoops?.Enabled != true || operations?.Database.Enabled != true)
        {
            return;
        }

        await using var scope = application.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AndrejaIdentityDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
