using System.Security.Claims;
using System.Text.Json;
using System.Data.Common;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Andreja.Api.Contracts.OpenLoops;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andreja.AppHost.Identity;

public static partial class LocalAccountEndpoints
{
    public const string LoginPath = "/Account/Login";
    public const string BootstrapPath = "/Account/Bootstrap";
    public const string RecoveryPath = "/Account/Recovery";
    public const string PasskeysPath = "/Account/Passkeys";
    public const string LogoutPath = "/Account/Logout";
    public const string AntiforgeryHeader = OpenLoopsApi.AntiforgeryHeader;

#if DEBUG
    public const string DevelopmentSignInPath = "/Account/DevelopmentSignIn";

    private static readonly Guid DevelopmentTenantId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000D001");
    private static readonly Guid DevelopmentAppUserId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000D002");
    private static readonly Guid DevelopmentPrincipalId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000D003");
#endif

    public static IServiceCollection ConfigureAndrejaCookieBehavior(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme,
            ConfigureCookie);
        services.AddAntiforgery(
            configured => configured.HeaderName =
                OpenLoopsApi.AntiforgeryHeader);
        services.AddRateLimiter();
        services.AddScoped<BootstrapCeremonyTicketProtector>();
        services.TryAddScoped<
            IRecentAuthenticationGrantStore,
            RecentPasskeyAuthentication.UnavailableRecentAuthenticationGrantStore>();
        services.TryAddScoped<
            IAppUserDisplayNameResolver,
            RecentPasskeyAuthentication.UnavailableAppUserDisplayNameResolver>();
        services.AddScoped<RecentPasskeyAuthentication>();
        return services;
    }

    public static IEndpointRouteBuilder MapLocalAccountEndpoints(
        this IEndpointRouteBuilder endpoints,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);

#if DEBUG
        if (environment.IsDevelopment())
        {
            endpoints.MapPost(DevelopmentSignInPath, DevelopmentSignInAsync)
                .AllowAnonymous();
        }
#endif

        var serviceInspector =
            endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (!serviceInspector.IsService(typeof(SignInManager<AspNetIdentityUser>)))
        {
            endpoints.MapPost(
                    LogoutPath,
                    async (HttpContext context, IAntiforgery antiforgery) =>
                    {
                        if (!await ValidateAntiforgeryAsync(context, antiforgery))
                        {
                            return IdentityFailure("invalid-request");
                        }

                        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                        return Results.LocalRedirect(LoginPath);
                    })
                .RequireAuthorization();
            return endpoints;
        }

        endpoints.MapBootstrapAccountEndpoints();
        endpoints.MapPasskeySignInEndpoints();
        endpoints.MapRecoveryAccountEndpoints();
        endpoints.MapPasskeyManagementEndpoints();
        endpoints.MapGet(
                PasskeysPath + "/List",
                async (
                    HttpContext context,
                    UserManager<AspNetIdentityUser> users) =>
                {
                    var user = await users.GetUserAsync(context.User);
                    if (user is null)
                    {
                        return Results.Unauthorized();
                    }

                    var passkeys = await users.GetPasskeysAsync(user);
                    return Results.Ok(passkeys.Select(passkey => new PasskeyDto(
                        WebEncoders.Base64UrlEncode(passkey.CredentialId),
                        passkey.Name ?? "Passkey",
                        passkey.CreatedAt,
                        passkey.IsBackedUp)));
                })
            .RequireAuthorization();
        return endpoints;
    }

    public static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
        && !returnUrl.Contains('\\')
        && !returnUrl.Any(char.IsControl);

    internal static CookieOptions StrictTemporaryCookie(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAge,
        };

    internal static string ReadClientChallenge(byte[] clientDataJson)
    {
        using var document = JsonDocument.Parse(clientDataJson);
        return ReadJsonString(document.RootElement, "challenge");
    }

    internal static string ReadJsonString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return ReadJsonString(document.RootElement, propertyName);
    }

    internal static async Task<bool> ValidateAntiforgeryAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    internal static IResult IdentityFailure(string code) =>
        Results.BadRequest(new ApiErrorDto(
            code,
            "The identity request could not be completed."));

    internal static bool IsExpectedDatabaseConflict(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var databaseException = exception switch
        {
            DbException direct => direct,
            DbUpdateException { InnerException: DbException inner } => inner,
            _ => null,
        };
        return databaseException?.SqlState is
            "23505" or "40001" or "40P01";
    }

    private static void ConfigureCookie(
        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions options)
    {
        options.LoginPath = LoginPath;
        options.AccessDeniedPath = LoginPath;
        options.Cookie.Name = "__Host-Andreja.Identity";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.Cookie.IsEssential = true;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = static context =>
            WriteApiOrRedirectAsync(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = static context =>
            WriteApiOrRedirectAsync(context, StatusCodes.Status403Forbidden);
    }

    private static string ReadJsonString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(property.GetString())
                ? property.GetString()!
                : throw new InvalidDataException(
                    "The passkey ceremony state is invalid.");

    private static Task WriteApiOrRedirectAsync(
        RedirectContext<
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context,
        int apiStatus)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = apiStatus;
            return context.Response.WriteAsJsonAsync(new ApiErrorDto(
                apiStatus == StatusCodes.Status401Unauthorized
                    ? "authentication-required"
                    : "access-denied",
                apiStatus == StatusCodes.Status401Unauthorized
                    ? "Authentication is required."
                    : "Access is denied."));
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

#if DEBUG
    private static async Task<IResult> DevelopmentSignInAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgeryAsync(httpContext, antiforgery))
        {
            return IdentityFailure("invalid-antiforgery-token");
        }

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        var returnUrl = form["returnUrl"].ToString();
        if (!IsLocalReturnUrl(returnUrl))
        {
            returnUrl = "/";
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, DevelopmentAppUserId.ToString("D")),
            new Claim(ClaimTypes.Name, "Local development user"),
            new Claim(LocalIdentityClaimTypes.TenantId, DevelopmentTenantId.ToString("D")),
            new Claim(LocalIdentityClaimTypes.AppUserId, DevelopmentAppUserId.ToString("D")),
            new Claim(LocalIdentityClaimTypes.PrincipalId, DevelopmentPrincipalId.ToString("D")),
        };
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
            });
        return Results.LocalRedirect(returnUrl);
    }
#endif
}
