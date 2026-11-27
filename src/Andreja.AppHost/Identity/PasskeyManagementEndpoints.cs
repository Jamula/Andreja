using System.Security.Cryptography;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Andreja.AppHost.Identity;

internal static class PasskeyManagementEndpoints
{
    private const string RegistrationOptionsPath = "/Account/Passkeys/RegistrationOptions";
    private const string RegistrationCompletePath = "/Account/Passkeys/RegistrationComplete";
    private const string RevokePath = "/Account/Passkeys/Revoke";

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

    private static async Task<IResult> RegistrationOptionsAsync(
        LocalAccountEndpoints.RegistrationOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        SignInManager<AspNetIdentityUser> signInManager,
        IOptions<LocalIdentityOptions> configured,
        RecentPasskeyAuthentication recentAuthentication,
        IAppUserDisplayNameResolver displayNames)
    {
        _ = input;
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
        }

        var user = await users.GetUserAsync(context.User);
        var displayName = user is null
            ? null
            : await displayNames.ResolveAsync(user, context.RequestAborted);
        if (user is null
            || string.IsNullOrWhiteSpace(displayName)
            || !await recentAuthentication.IsValidAsync(
                context,
                user,
                context.RequestAborted)
            || (await users.GetPasskeysAsync(user)).Count
                >= configured.Value.MaximumPasskeysPerUser)
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
        }

        var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = user.Id.ToString("D"),
            Name = user.UserName ?? $"user-{user.Id:N}",
            DisplayName = displayName,
        });
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> RegistrationCompleteAsync(
        LocalAccountEndpoints.RegistrationCompleteRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        SignInManager<AspNetIdentityUser> signInManager,
        ILocalPasskeyManagementOperations operations,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null
            || !await recentAuthentication.TryConsumeAsync(
                context,
                user,
                context.RequestAborted))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
        }

        var attestation =
            await signInManager.PerformPasskeyAttestationAsync(input.CredentialJson);
        if (!attestation.Succeeded
            || !string.Equals(
                attestation.UserEntity.Id,
                user.Id.ToString("D"),
                StringComparison.Ordinal))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
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
                || LocalAccountEndpoints.IsExpectedDatabaseConflict(exception)))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-registration-failed");
        }
    }

    private static async Task<IResult> RevokeAsync(
        LocalAccountEndpoints.RevokePasskeyRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<AspNetIdentityUser> users,
        ILocalPasskeyManagementOperations operations,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-revocation-failed");
        }

        var user = await users.GetUserAsync(context.User);
        byte[] credentialId;
        try
        {
            credentialId = WebEncoders.Base64UrlDecode(input.CredentialId);
        }
        catch (FormatException)
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-revocation-failed");
        }

        if (user is null)
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-revocation-failed");
        }

        try
        {
            if (!await recentAuthentication.TryConsumeAsync(
                    context,
                    user,
                    context.RequestAborted))
            {
                return LocalAccountEndpoints.IdentityFailure("passkey-revocation-failed");
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
                || LocalAccountEndpoints.IsExpectedDatabaseConflict(exception)))
        {
            return LocalAccountEndpoints.IdentityFailure("passkey-revocation-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialId);
        }
    }
}
