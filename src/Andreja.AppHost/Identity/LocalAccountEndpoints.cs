using Andreja.Api.Contracts.OpenLoops;
using Andreja.AppHost.OpenLoops;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Andreja.AppHost.Identity;

public static class LocalAccountEndpoints
{
    public const string LoginPath = "/Account/Login";
    public const string DevelopmentSignInPath = "/Account/DevelopmentSignIn";
    public const string LogoutPath = "/Account/Logout";

#if DEBUG
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
        return services;
    }

    public static IEndpointRouteBuilder MapLocalAccountEndpoints(
        this IEndpointRouteBuilder endpoints,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);

#if DEBUG
        if (IsDevelopmentSignInAvailable(environment))
        {
            endpoints.MapPost(DevelopmentSignInPath, DevelopmentSignInAsync)
                .AllowAnonymous();
        }
#endif

        endpoints.MapPost(
                LogoutPath,
                async (HttpContext context, IAntiforgery antiforgery) =>
                    await LogoutAsync(context, antiforgery))
            .RequireAuthorization();
        return endpoints;
    }

    public static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
        && !returnUrl.Contains('\\');

    public static bool IsDevelopmentSignInAvailable(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
#if DEBUG
        return environment.IsDevelopment();
#else
        return false;
#endif
    }

    private static void ConfigureCookie(
        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions options)
    {
        options.LoginPath = LoginPath;
        options.AccessDeniedPath = LoginPath;
        options.Events.OnRedirectToLogin = static context =>
            WriteApiOrRedirectAsync(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = static context =>
            WriteApiOrRedirectAsync(context, StatusCodes.Status403Forbidden);
    }

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
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new ApiErrorDto(
                "invalid-antiforgery-token",
                "The request verification token is missing or expired."));
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
            new Claim(AndrejaClaimTypes.TenantId, DevelopmentTenantId.ToString("D")),
            new Claim(AndrejaClaimTypes.AppUserId, DevelopmentAppUserId.ToString("D")),
            new Claim(AndrejaClaimTypes.PrincipalId, DevelopmentPrincipalId.ToString("D")),
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

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new ApiErrorDto(
                "invalid-antiforgery-token",
                "The request verification token is missing or expired."));
        }

        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.LocalRedirect(LoginPath);
    }
}
