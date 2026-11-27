using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Andreja.AppHost.Identity;

internal static class PasskeySignInEndpoints
{
    private const string SignInOptionsPath = "/Account/Passkeys/SignInOptions";
    private const string SignInCompletePath = "/Account/Passkeys/SignInComplete";

    internal static IEndpointRouteBuilder MapPasskeySignInEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(SignInOptionsPath, SignInOptionsAsync).AllowAnonymous();
        endpoints.MapPost(SignInCompletePath, SignInCompleteAsync).AllowAnonymous();
        endpoints.MapPost(LocalAccountEndpoints.LogoutPath, LogoutAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SignInOptionsAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return LocalAccountEndpoints.IdentityFailure("sign-in-unavailable");
        }

        var options = await signInManager.MakePasskeyRequestOptionsAsync(null!);
        return TypedResults.Content(options, "application/json");
    }

    private static async Task<IResult> SignInCompleteAsync(
        LocalAccountEndpoints.SignInRequest input,
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<LocalIdentityOptions> configured,
        SignInManager<AspNetIdentityUser> signInManager,
        UserManager<AspNetIdentityUser> users,
        RecentPasskeyAuthentication recentAuthentication)
    {
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery)
            || !LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                configured.Value))
        {
            return LocalAccountEndpoints.IdentityFailure("sign-in-failed");
        }

        var result = await signInManager.PasskeySignInAsync(input.CredentialJson);
        if (!result.Succeeded)
        {
            return LocalAccountEndpoints.IdentityFailure("sign-in-failed");
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return LocalAccountEndpoints.IdentityFailure("sign-in-failed");
        }

        await recentAuthentication.IssueAsync(
            context,
            user,
            context.RequestAborted);
        return Results.Ok(new LocalAccountEndpoints.IdentityCompletionDto(
            LocalAccountEndpoints.IsLocalReturnUrl(input.ReturnUrl) ? input.ReturnUrl! : "/",
            null));
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
        if (!await LocalAccountEndpoints.ValidateAntiforgeryAsync(context, antiforgery))
        {
            return LocalAccountEndpoints.IdentityFailure("invalid-request");
        }

        await signInManager.SignOutAsync();
        RecentPasskeyAuthentication.Clear(context);
        return Results.LocalRedirect(LocalAccountEndpoints.LoginPath);
    }
}
