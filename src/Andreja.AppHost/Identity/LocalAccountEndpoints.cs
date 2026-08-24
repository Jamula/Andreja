using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Data.Common;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Andreja.Api.Contracts.OpenLoops;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andreja.AppHost.Identity;

public static class LocalAccountEndpoints
{
    public const string LoginPath = "/Account/Login";
    public const string BootstrapPath = "/Account/Bootstrap";
    public const string RecoveryPath = "/Account/Recovery";
    public const string PasskeysPath = "/Account/Passkeys";
    public const string LogoutPath = "/Account/Logout";
    public const string AntiforgeryHeader = OpenLoopsApi.AntiforgeryHeader;

    private const string BootstrapOptionsPath = "/Account/Passkeys/BootstrapOptions";
    private const string BootstrapCompletePath = "/Account/Passkeys/BootstrapComplete";
    private const string SignInOptionsPath = "/Account/Passkeys/SignInOptions";
    private const string SignInCompletePath = "/Account/Passkeys/SignInComplete";
    private const string RegistrationOptionsPath = "/Account/Passkeys/RegistrationOptions";
    private const string RegistrationCompletePath = "/Account/Passkeys/RegistrationComplete";
    private const string RevokePath = "/Account/Passkeys/Revoke";
    private const string BootstrapCookieName = "__Host-Andreja.Bootstrap";
    private const string RecoveryCookieName = "__Host-Andreja.Recovery";
    private const string RecoveryProtectionPurpose = "Andreja.Identity.Recovery.v1";

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

    internal static IEndpointRouteBuilder MapBootstrapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(BootstrapOptionsPath, BootstrapOptionsAsync).AllowAnonymous();
        endpoints.MapPost(BootstrapCompletePath, BootstrapCompleteAsync).AllowAnonymous();
        return endpoints;
    }

    internal static IEndpointRouteBuilder MapPasskeySignInEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(SignInOptionsPath, SignInOptionsAsync).AllowAnonymous();
        endpoints.MapPost(SignInCompletePath, SignInCompleteAsync).AllowAnonymous();
        endpoints.MapPost(LogoutPath, LogoutAsync).RequireAuthorization();
        return endpoints;
    }

    internal static IEndpointRouteBuilder MapRecoveryAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                LocalIdentityNetworkSecurity.RecoveryOptionsPath,
                RecoveryOptionsAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LocalIdentityNetworkSecurity.RecoveryRateLimitPolicy);
        endpoints.MapPost(
                LocalIdentityNetworkSecurity.RecoveryCompletePath,
                RecoveryCompleteAsync)
            .AllowAnonymous();
        return endpoints;
    }

    internal static IEndpointRouteBuilder MapPasskeyManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RegistrationOptionsPath, RegistrationOptionsAsync)
            .RequireAuthorization();
        endpoints.MapPost(RegistrationCompletePath, RegistrationCompleteAsync)
            .RequireAuthorization();
        endpoints.MapPost(RevokePath, RevokeAsync).RequireAuthorization();
        return endpoints;
    }

    public static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
        && !returnUrl.Contains('\\')
        && !returnUrl.Any(char.IsControl);

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

    private static async Task<IResult> BootstrapOptionsAsync(
        BootstrapOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityBootstrapOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        BootstrapCeremonyTicketProtector tickets)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !await operations.CanBeginBootstrapAsync(
                context.Request,
                input.Token,
                context.RequestAborted))
        {
            return IdentityFailure("bootstrap-unavailable");
        }

        try
        {
            var reservedCredentialUserId = Guid.CreateVersion7();
            var tenantName = input.TenantName.Trim();
            var displayName = input.UserDisplayName.Trim();
            var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
            {
                Id = reservedCredentialUserId.ToString("D"),
                Name = "Andreja owner",
                DisplayName = displayName,
            });
            var challenge = ReadJsonString(options, "challenge");
            var protectedTicket = tickets.Protect(new(
                reservedCredentialUserId,
                input.Token,
                tenantName,
                displayName,
                challenge));
            context.Response.Cookies.Append(
                BootstrapCookieName,
                protectedTicket,
                StrictTemporaryCookie(tickets.Lifetime));
            return TypedResults.Content(options, "application/json");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return IdentityFailure("bootstrap-unavailable");
        }
    }

    private static async Task<IResult> BootstrapCompleteAsync(
        BootstrapCompleteRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityBootstrapOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        BootstrapCeremonyTicketProtector tickets)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return IdentityFailure("invalid-request");
        }

        if (!context.Request.Cookies.TryGetValue(
                BootstrapCookieName,
                out var protectedTicket))
        {
            return IdentityFailure("bootstrap-unavailable");
        }

        context.Response.Cookies.Delete(
            BootstrapCookieName,
            StrictTemporaryCookie(null));
        if (!tickets.TryUnprotect(protectedTicket, out var ticket)
            || ticket is null)
        {
            return IdentityFailure("bootstrap-unavailable");
        }

        try
        {
            var attestation =
                await signInManager.PerformPasskeyAttestationAsync(input.CredentialJson);
            if (!attestation.Succeeded
                || !Guid.TryParse(
                    attestation.UserEntity.Id,
                    out var reservedCredentialUserId)
                || reservedCredentialUserId != ticket.CredentialUserId
                || !string.Equals(
                    attestation.UserEntity.DisplayName,
                    ticket.UserDisplayName,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ReadClientChallenge(attestation.Passkey.ClientDataJson),
                    ticket.Challenge,
                    StringComparison.Ordinal))
            {
                return IdentityFailure("passkey-verification-failed");
            }

            var result = await operations.CompleteBootstrapAsync(
                context.Request,
                ticket.Token,
                ticket.TenantName,
                ticket.UserDisplayName,
                reservedCredentialUserId,
                attestation.Passkey,
                context.RequestAborted);
            await signInManager.SignInAsync(
                result.User,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true,
                });
            return Results.Ok(new IdentityCompletionDto(
                IsLocalReturnUrl(input.ReturnUrl) ? input.ReturnUrl! : "/",
                result.RecoveryCodes));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or ArgumentException
                or InvalidDataException
                or JsonException
                or DbUpdateException)
        {
            return IdentityFailure("bootstrap-unavailable");
        }
    }

    private static async Task<IResult> SignInOptionsAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return IdentityFailure("sign-in-unavailable");
        }

        var options = await signInManager.MakePasskeyRequestOptionsAsync(null!);
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> SignInCompleteAsync(
        SignInRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager,
        UserManager<AspNetIdentityUser> users,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return IdentityFailure("sign-in-failed");
        }

        var result = await signInManager.PasskeySignInAsync(input.CredentialJson);
        if (!result.Succeeded)
        {
            return IdentityFailure("sign-in-failed");
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return IdentityFailure("sign-in-failed");
        }

        await recentAuthentication.IssueAsync(
            context,
            user,
            context.RequestAborted);
        return Results.Ok(new IdentityCompletionDto(
            IsLocalReturnUrl(input.ReturnUrl) ? input.ReturnUrl! : "/",
            null));
    }

    private static async Task<IResult> RecoveryOptionsAsync(
        RecoveryOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityRecoveryOperations operations,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager,
        IDataProtectionProvider dataProtection)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return IdentityFailure("recovery-failed");
        }

        var recovery = await operations.BeginRecoveryAsync(
            input.RecoveryCode,
            context.RequestAborted);
        if (recovery is null)
        {
            return IdentityFailure("recovery-failed");
        }

        var protectedTicket = dataProtection
            .CreateProtector(RecoveryProtectionPurpose)
            .ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(recovery), TimeSpan.FromMinutes(5));
        context.Response.Cookies.Append(
            RecoveryCookieName,
            protectedTicket,
            StrictTemporaryCookie(TimeSpan.FromMinutes(5)));
        var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = recovery.UserId.ToString("D"),
            Name = recovery.UserName,
            DisplayName = recovery.DisplayName,
        });
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> RecoveryCompleteAsync(
        CredentialRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityRecoveryOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        IDataProtectionProvider dataProtection)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !context.Request.Cookies.TryGetValue(
                RecoveryCookieName,
                out var protectedTicket))
        {
            return IdentityFailure("recovery-failed");
        }

        context.Response.Cookies.Delete(RecoveryCookieName, StrictTemporaryCookie(null));
        RecoveryStartResult? recovery;
        try
        {
            var ticket = dataProtection
                .CreateProtector(RecoveryProtectionPurpose)
                .ToTimeLimitedDataProtector()
                .Unprotect(protectedTicket);
            recovery = JsonSerializer.Deserialize<RecoveryStartResult>(ticket);
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException)
        {
            return IdentityFailure("recovery-failed");
        }

        try
        {
            var attestation =
                await signInManager.PerformPasskeyAttestationAsync(input.CredentialJson);
            if (recovery is null
                || !attestation.Succeeded
                || !Guid.TryParse(attestation.UserEntity.Id, out var attestedUserId)
                || attestedUserId != recovery.UserId)
            {
                await operations.AuditRecoveryFailureAsync(
                    recovery?.UserId,
                    context.RequestAborted);
                return IdentityFailure("recovery-failed");
            }

            var result = await operations.CompleteRecoveryAsync(
                recovery,
                attestation.Passkey,
                context.RequestAborted);
            await signInManager.SignOutAsync();
            return Results.Ok(new IdentityCompletionDto(LoginPath, result.RecoveryCodes));
        }
        catch (Exception exception) when (
            !context.RequestAborted.IsCancellationRequested
            && (exception is InvalidOperationException
                || IsExpectedDatabaseConflict(exception)))
        {
            await operations.AuditRecoveryFailureAsync(
                recovery?.UserId,
                context.RequestAborted);
            return IdentityFailure("recovery-failed");
        }
    }

    private static async Task<IResult> RegistrationOptionsAsync(
        RegistrationOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        SignInManager<AspNetIdentityUser> signInManager,
        IOptions<LocalIdentityOptions> configured,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return IdentityFailure("passkey-registration-failed");
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null
            || !await recentAuthentication.IsValidAsync(
                context,
                user,
                context.RequestAborted)
            || (await users.GetPasskeysAsync(user)).Count
                >= configured.Value.MaximumPasskeysPerUser)
        {
            return IdentityFailure("passkey-registration-failed");
        }

        var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = user.Id.ToString("D"),
            Name = user.UserName ?? $"user-{user.Id:N}",
            DisplayName = input.DeviceName.Trim(),
        });
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> RegistrationCompleteAsync(
        RegistrationCompleteRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        SignInManager<AspNetIdentityUser> signInManager,
        ILocalPasskeyManagementOperations operations,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return IdentityFailure("passkey-registration-failed");
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null
            || !await recentAuthentication.TryConsumeAsync(
                context,
                user,
                context.RequestAborted))
        {
            return IdentityFailure("passkey-registration-failed");
        }

        var attestation =
            await signInManager.PerformPasskeyAttestationAsync(input.CredentialJson);
        if (!attestation.Succeeded
            || !string.Equals(
                attestation.UserEntity.Id,
                user.Id.ToString("D"),
                StringComparison.Ordinal))
        {
            return IdentityFailure("passkey-registration-failed");
        }

        try
        {
            await operations.RegisterPasskeyAsync(
                user,
                attestation.Passkey,
                input.DeviceName,
                context.RequestAborted);
            return Results.Ok();
        }
        catch (Exception exception) when (
            !context.RequestAborted.IsCancellationRequested
            && (exception is InvalidOperationException or ArgumentException
                || IsExpectedDatabaseConflict(exception)))
        {
            return IdentityFailure("passkey-registration-failed");
        }
    }

    private static async Task<IResult> RevokeAsync(
        RevokePasskeyRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        ILocalPasskeyManagementOperations operations,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return IdentityFailure("passkey-revocation-failed");
        }

        var user = await users.GetUserAsync(context.User);
        byte[] credentialId;
        try
        {
            credentialId = WebEncoders.Base64UrlDecode(input.CredentialId);
        }
        catch (FormatException)
        {
            return IdentityFailure("passkey-revocation-failed");
        }

        if (user is null)
        {
            return IdentityFailure("passkey-revocation-failed");
        }

        try
        {
            if (!await recentAuthentication.TryConsumeAsync(
                    context,
                    user,
                    context.RequestAborted))
            {
                return IdentityFailure("passkey-revocation-failed");
            }

            await operations.RevokePasskeyAsync(
                user,
                credentialId,
                context.RequestAborted);
            return Results.Ok();
        }
        catch (Exception exception) when (
            !context.RequestAborted.IsCancellationRequested
            && (exception is InvalidOperationException
                || IsExpectedDatabaseConflict(exception)))
        {
            return IdentityFailure("passkey-revocation-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialId);
        }
    }

    private static Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<AspNetIdentityUser> signInManager) =>
        LogoutCoreAsync(
            context,
            antiforgery,
            signInManager);

    private static async Task<IResult> LogoutCoreAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<AspNetIdentityUser> signInManager)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return IdentityFailure("invalid-request");
        }

        await signInManager.SignOutAsync();
        RecentPasskeyAuthentication.Clear(context);
        return Results.LocalRedirect(LoginPath);
    }

    private static CookieOptions StrictTemporaryCookie(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAge,
        };

    private static string ReadClientChallenge(byte[] clientDataJson)
    {
        using var document = JsonDocument.Parse(clientDataJson);
        return ReadJsonString(document.RootElement, "challenge");
    }

    private static string ReadJsonString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return ReadJsonString(document.RootElement, propertyName);
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

    private static async Task<bool> ValidateAntiforgeryAsync(
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

    private static IResult IdentityFailure(string code) =>
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

    public sealed record BootstrapOptionsRequest(
        string Token,
        string TenantName,
        string UserDisplayName);

    public sealed record BootstrapCompleteRequest(
        string CredentialJson,
        string? ReturnUrl);

    public sealed record SignInRequest(string CredentialJson, string? ReturnUrl);

    public sealed record RecoveryOptionsRequest(string RecoveryCode);

    public sealed record CredentialRequest(string CredentialJson);

    public sealed record RegistrationOptionsRequest(string DeviceName);

    public sealed record RegistrationCompleteRequest(
        string DeviceName,
        string CredentialJson);

    public sealed record RevokePasskeyRequest(string CredentialId);

    public sealed record IdentityCompletionDto(
        string RedirectUrl,
        IReadOnlyList<string>? RecoveryCodes);

    public sealed record PasskeyDto(
        string CredentialId,
        string Name,
        DateTimeOffset CreatedAt,
        bool IsBackedUp);
}
