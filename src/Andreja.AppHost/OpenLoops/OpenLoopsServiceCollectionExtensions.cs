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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Andreja.AppHost.OpenLoops;

public sealed class OpenLoopsOptions
{
    public const string SectionName = "Andreja:OpenLoops";

    public bool Enabled { get; init; }

    [Required]
    [Url]
    public string PublicOrigin { get; init; } = "https://localhost:5001";

    public string AssistantProvider { get; init; } = "deterministic";

    public OpenAiCompatibleProviderOptions OpenAiCompatible { get; init; } = new();
}

public sealed class OpenAiCompatibleProviderOptions
{
    public string Endpoint { get; init; } = string.Empty;

    public string[] AllowedEndpoints { get; init; } = [];

    public string Model { get; init; } = string.Empty;

    public string CredentialHandle { get; init; } = string.Empty;

    public string CredentialFile { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    [Required]
    public string ProviderDisclosure { get; init; } =
        "Operator-selected OpenAI-compatible provider receives the submitted task request.";

    [Required]
    public string RetentionDisclosure { get; init; } =
        "Review the selected provider's retention policy before enabling this profile.";

    [Range(1, long.MaxValue)]
    public long MaximumInputUnits { get; init; } = 10_000;

    [Range(1, long.MaxValue)]
    public long MaximumOutputUnits { get; init; } = 2_000;

    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumResponseBodyBytes { get; init; } = 1024 * 1024;

    [Range(0, 5)]
    public int MaximumRetries { get; init; } = 2;

    [Range(0, 5000)]
    public int RetryBaseDelayMilliseconds { get; init; } = 100;

    [Range(0, long.MaxValue)]
    public long ApprovedExternalTotalUnits { get; init; }
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
            .Validate(
                options => options.AssistantProvider != "openai-compatible"
                    || IsValidOpenAiConfiguration(options.OpenAiCompatible),
                "The OpenAI-compatible provider requires an allowlisted endpoint, model, credential handle, absolute credential file mapping, disclosures, and bounded transport policy.")
            .ValidateOnStart();

        services.AddCascadingAuthenticationState();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<
            ICircuitDelegationTokenService,
            CircuitDelegationTokenService>();
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<
                AuthenticationSchemeOptions,
                CircuitDelegationAuthenticationHandler>(
                CircuitDelegation.AuthenticationScheme,
                _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(
                "andreja-user",
                policy => policy
                    .AddAuthenticationSchemes(
                        CircuitDelegation.AuthenticationScheme,
                        IdentityConstants.ApplicationScheme)
                    .RequireAuthenticatedUser());

        if (!openLoops.Enabled)
        {
            services.AddAuthentication()
                .AddCookie(IdentityConstants.ApplicationScheme);
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
            services.AddScoped<IDatabaseMigrationExecutor, EfDatabaseMigrationExecutor>();
            services.AddHealthChecks()
                .AddCheck<DatabaseMigrationReadinessHealthCheck>(
                    "database-migrations",
                    tags: ["ready"]);

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
            services.AddSingleton<InMemoryProposalStore>();
            services.AddSingleton<IProposalStore>(
                provider => provider.GetRequiredService<InMemoryProposalStore>());
            services.AddSingleton<IProposalAuditSink>(
                provider => provider.GetRequiredService<InMemoryProposalStore>());
        }
        else
        {
            throw new InvalidOperationException(
                "Open Loops requires PostgreSQL outside Development.");
        }

        services.AddScoped<OpenLoopsTaskApplication>();
        services.AddScoped<ISkillHost>(
            provider => OpenLoopsSkill.CreateHost(
                provider.GetRequiredService<OpenLoopsTaskApplication>()));
        if (openLoops.AssistantProvider == "openai-compatible")
        {
            var profile = CreateProfile(openLoops.OpenAiCompatible);
            var transportPolicy = CreateTransportPolicy(openLoops.OpenAiCompatible);
            services.AddSingleton(transportPolicy);
            services.AddSingleton<IAssistantCredentialStore>(
                new FileAssistantCredentialStore(
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [profile.CredentialHandle] = openLoops.OpenAiCompatible.CredentialFile,
                    }));
            services.AddHttpClient<OpenAiCompatibleTransport>(client =>
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .RedactLoggedHeaders(_ => true)
                .ConfigurePrimaryHttpMessageHandler(() => CreateOpenAiHandler(profile));
            services.AddSingleton<IAssistantProvider>(provider =>
                new OpenAiCompatibleAssistantAdapter(
                    profile,
                    provider.GetRequiredService<OpenAiCompatibleTransport>()));
        }
        else
        {
            services.AddSingleton<IAssistantProvider>(
                OpenLoopsSkill.CreateDeterministicProvider());
        }

        services.AddScoped<OpenLoopsAssistantService>();

        services.AddHttpClient<IOpenLoopsApiClient, OpenLoopsApiClient>(
                (provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<OpenLoopsOptions>>().Value;
                    client.BaseAddress = new Uri(options.PublicOrigin, UriKind.Absolute);
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () => CreateSameOriginHandler(environment));
        return services;
    }

    public static HttpClientHandler CreateSameOriginHandler(
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var handler = new HttpClientHandler
        {
            UseCookies = false,
        };
        if (environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback =
                static (request, _, _, _) => request.RequestUri?.IsLoopback == true;
        }

        return handler;
    }

    internal static AssistantProviderProfile CreateProfile(
        OpenAiCompatibleProviderOptions options) =>
        OpenAiCompatibleAssistantAdapter.Validate(
            new(
                new Uri(options.Endpoint, UriKind.Absolute),
                options.Model,
                options.CredentialHandle,
                TimeSpan.FromSeconds(options.TimeoutSeconds),
                options.ProviderDisclosure,
                options.RetentionDisclosure,
                options.MaximumInputUnits,
                options.MaximumOutputUnits));

    internal static OpenAiCompatibleTransportPolicy CreateTransportPolicy(
        OpenAiCompatibleProviderOptions options) =>
        new(
            options.AllowedEndpoints
                .Select(endpoint => new Uri(endpoint, UriKind.Absolute))
                .ToArray(),
            options.MaximumResponseBodyBytes,
            options.MaximumRetries,
            TimeSpan.FromMilliseconds(options.RetryBaseDelayMilliseconds),
            options.ApprovedExternalTotalUnits);

    internal static SocketsHttpHandler CreateOpenAiHandler(
        AssistantProviderProfile profile) =>
        new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = profile.Timeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

    private static bool IsValidOpenAiConfiguration(
        OpenAiCompatibleProviderOptions options)
    {
        try
        {
            if (options is null
                || string.IsNullOrWhiteSpace(options.CredentialFile)
                || !Path.IsPathFullyQualified(options.CredentialFile))
            {
                return false;
            }

            var profile = CreateProfile(options);
            var policy = CreateTransportPolicy(options);
            var canonicalEndpoints = policy.AllowedEndpoints
                .Select(CanonicalEndpoint)
                .ToArray();
            return policy.AllowedEndpoints.Count > 0
                && canonicalEndpoints.Distinct(StringComparer.Ordinal).Count()
                    == canonicalEndpoints.Length
                && options.TimeoutSeconds is >= 1 and <= 300
                && options.MaximumInputUnits > 0
                && options.MaximumOutputUnits > 0
                && policy.MaximumResponseBodyBytes is >= 1024 and <= 16 * 1024 * 1024
                && policy.MaximumRetries is >= 0 and <= 5
                && policy.RetryBaseDelay >= TimeSpan.Zero
                && policy.RetryBaseDelay <= TimeSpan.FromSeconds(5)
                && policy.ApprovedExternalTotalUnits >= 0
                && canonicalEndpoints.Any(endpoint =>
                    string.Equals(
                        endpoint,
                        CanonicalEndpoint(profile.Endpoint),
                        StringComparison.Ordinal));
        }
        catch (Exception exception) when (
            exception is ArgumentException or UriFormatException or OverflowException)
        {
            return false;
        }
    }

    private static string CanonicalEndpoint(Uri endpoint)
    {
        OpenAiCompatibleAssistantAdapter.Validate(
            new(
                endpoint,
                "validation",
                "credential://validation/handle",
                TimeSpan.FromSeconds(1),
                "validation",
                "validation",
                1,
                1));
        return endpoint.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped).TrimEnd('/');
    }
}
