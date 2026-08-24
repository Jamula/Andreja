using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public static class LocalIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddAndrejaLocalIdentity(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<LocalIdentityOptions>()
            .Bind(configuration)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LocalIdentityOptions>, LocalIdentityOptionsValidator>();
        services.AddSingleton<IConfigureOptions<IdentityPasskeyOptions>, ConfigurePasskeyOptions>();
        services.AddScoped<IBootstrapTokenVerifier, BootstrapTokenVerifier>();
        services.AddScoped<LocalIdentityOperations>();
        services.AddScoped<ILocalIdentityBootstrapOperations>(
            provider => provider.GetRequiredService<LocalIdentityOperations>());
        services.AddScoped<ILocalIdentityRecoveryOperations>(
            provider => provider.GetRequiredService<LocalIdentityOperations>());
        services.AddScoped<ILocalPasskeyManagementOperations>(
            provider => provider.GetRequiredService<LocalIdentityOperations>());
        services.AddScoped<
            IRecentAuthenticationGrantStore,
            PostgreSqlRecentAuthenticationGrantStore>();
        services.AddScoped<
            IAppUserDisplayNameResolver,
            PostgreSqlAppUserDisplayNameResolver>();
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<LocalIdentityOptions>>(
                (forwarded, identity) =>
                    LocalIdentityNetworkSecurity.ConfigureForwardedHeaders(
                        forwarded,
                        identity.Value));
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<LocalIdentityOptions>>(
                (limiter, identity) =>
                    LocalIdentityNetworkSecurity.ConfigureRateLimiting(
                        limiter,
                        identity.Value));

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.AddOptions<CookieAuthenticationOptions>(
                IdentityConstants.TwoFactorUserIdScheme)
            .Configure<IOptions<LocalIdentityOptions>>(
                (cookie, identity) =>
                {
                    cookie.ExpireTimeSpan =
                        identity.Value.BootstrapCeremonyLifetime;
                    cookie.Cookie.Name = "__Host-Andreja.PasskeyState";
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SameSite = SameSiteMode.Strict;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.Path = "/";
                    cookie.Cookie.IsEssential = true;
                });

        services
            .AddIdentityCore<AspNetIdentityUser>(
                options =>
                {
                    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                    options.User.RequireUniqueEmail = false;
                    options.SignIn.RequireConfirmedAccount = true;
                })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<AndrejaIdentityDbContext>();
        services.AddScoped<
            IUserClaimsPrincipalFactory<AspNetIdentityUser>,
            AndrejaUserClaimsPrincipalFactory>();
        services.Configure<SecurityStampValidatorOptions>(
            configured => configured.ValidationInterval = TimeSpan.Zero);
        return services;
    }

    private sealed class ConfigurePasskeyOptions(IOptions<LocalIdentityOptions> localIdentity)
        : IConfigureOptions<IdentityPasskeyOptions>
    {
        public void Configure(IdentityPasskeyOptions options)
        {
            var configured = localIdentity.Value;
            var allowedOrigins = configured.AllowedOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);
            options.ServerDomain = configured.RelyingPartyId;
            options.ValidateOrigin = context =>
                ValueTask.FromResult(
                    !context.CrossOrigin
                    && allowedOrigins.Contains(context.Origin));
        }
    }
}
