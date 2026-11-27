using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Andreja.AppHost.Identity;

internal static class BootstrapAccountEndpoints
{
    private const string BootstrapOptionsPath = "/Account/Passkeys/BootstrapOptions";
    private const string BootstrapCompletePath = "/Account/Passkeys/BootstrapComplete";
    private const string BootstrapCookieName = "__Host-Andreja.Bootstrap";

    internal static IEndpointRouteBuilder MapBootstrapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(BootstrapOptionsPath, BootstrapOptionsAsync).AllowAnonymous();
        endpoints.MapPost(BootstrapCompletePath, BootstrapCompleteAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> BootstrapOptionsAsync(
        LocalAccountEndpoints.BootstrapOptionsRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityBootstrapOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        BootstrapCeremonyTicketProtector tickets)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !await operations.CanBeginBootstrapAsync(
                context.Request,
                input.Token,
                context.RequestAborted))
        {
            return LocalAccountEndpoints.IdentityFailure("bootstrap-unavailable");
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
            var challenge = LocalAccountEndpoints.ReadJsonString(options, "challenge");
            var protectedTicket = tickets.Protect(new(
                reservedCredentialUserId,
                input.Token,
                tenantName,
                displayName,
                challenge));
            context.Response.Cookies.Append(
                BootstrapCookieName,
                protectedTicket,
                LocalAccountEndpoints.StrictTemporaryCookie(tickets.Lifetime));
            return TypedResults.Content(options, "application/json");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return LocalAccountEndpoints.IdentityFailure("bootstrap-unavailable");
        }
    }

    private static async Task<IResult> BootstrapCompleteAsync(
        LocalAccountEndpoints.BootstrapCompleteRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        ILocalIdentityBootstrapOperations operations,
        SignInManager<AspNetIdentityUser> signInManager,
        BootstrapCeremonyTicketProtector tickets)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery))
        {
            return LocalAccountEndpoints.IdentityFailure("invalid-request");
        }

        if (!context.Request.Cookies.TryGetValue(
                BootstrapCookieName,
                out var protectedTicket))
        {
            return LocalAccountEndpoints.IdentityFailure("bootstrap-unavailable");
        }

        context.Response.Cookies.Delete(
            BootstrapCookieName,
            LocalAccountEndpoints.StrictTemporaryCookie(null));
        if (!tickets.TryUnprotect(protectedTicket, out var ticket)
            || ticket is null)
        {
            return LocalAccountEndpoints.IdentityFailure("bootstrap-unavailable");
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
                    LocalAccountEndpoints.ReadClientChallenge(attestation.Passkey.ClientDataJson),
                    ticket.Challenge,
                    StringComparison.Ordinal))
            {
                return LocalAccountEndpoints.IdentityFailure("passkey-verification-failed");
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
            return Results.Ok(new LocalAccountEndpoints.IdentityCompletionDto(
                LocalAccountEndpoints.IsLocalReturnUrl(input.ReturnUrl) ? input.ReturnUrl! : "/",
                result.RecoveryCodes));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or ArgumentException
                or InvalidDataException
                or JsonException
                or DbUpdateException)
        {
            return LocalAccountEndpoints.IdentityFailure("bootstrap-unavailable");
        }
    }
}
