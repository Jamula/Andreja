using System.Security.Cryptography;
using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Andreja.AppHost.Identity;

internal static class RecoveryAccountEndpoints
{
    private const string RecoveryCookieName = "__Host-Andreja.Recovery";
    private const string RecoveryProtectionPurpose = "Andreja.Identity.Recovery.v1";

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

    private static async Task<IResult> RecoveryOptionsAsync(
        LocalAccountEndpoints.RecoveryOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityRecoveryOperations operations,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager,
        IDataProtectionProvider dataProtection)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsPlausibleRecoveryCode(
                input.RecoveryCode)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return LocalAccountEndpoints.IdentityFailure("recovery-failed");
        }

        var recovery = await operations.BeginRecoveryAsync(
            input.RecoveryCode,
            context.RequestAborted);
        if (recovery is null)
        {
            return LocalAccountEndpoints.IdentityFailure("recovery-failed");
        }

        var protectedTicket = dataProtection
            .CreateProtector(RecoveryProtectionPurpose)
            .ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(recovery), TimeSpan.FromMinutes(5));
        context.Response.Cookies.Append(
            RecoveryCookieName,
            protectedTicket,
            LocalAccountEndpoints.StrictTemporaryCookie(TimeSpan.FromMinutes(5)));
        var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = recovery.UserId.ToString("D"),
            Name = recovery.UserName,
            DisplayName = recovery.DisplayName,
        });
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> RecoveryCompleteAsync(
        LocalAccountEndpoints.CredentialRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityRecoveryOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        IDataProtectionProvider dataProtection)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !context.Request.Cookies.TryGetValue(
                RecoveryCookieName,
                out var protectedTicket))
        {
            return LocalAccountEndpoints.IdentityFailure("recovery-failed");
        }

        context.Response.Cookies.Delete(
            RecoveryCookieName,
            LocalAccountEndpoints.StrictTemporaryCookie(null));
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
            return LocalAccountEndpoints.IdentityFailure("recovery-failed");
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
                return LocalAccountEndpoints.IdentityFailure("recovery-failed");
            }

            var result = await operations.CompleteRecoveryAsync(
                recovery,
                attestation.Passkey,
                context.RequestAborted);
            await signInManager.SignOutAsync();
            return Results.Ok(new LocalAccountEndpoints.IdentityCompletionDto(
                LocalAccountEndpoints.LoginPath,
                result.RecoveryCodes));
        }
        catch (Exception exception) when (
            !context.RequestAborted.IsCancellationRequested
            && (exception is InvalidOperationException
                || LocalAccountEndpoints.IsExpectedDatabaseConflict(exception)))
        {
            await operations.AuditRecoveryFailureAsync(
                recovery?.UserId,
                context.RequestAborted);
            return LocalAccountEndpoints.IdentityFailure("recovery-failed");
        }
    }
}
