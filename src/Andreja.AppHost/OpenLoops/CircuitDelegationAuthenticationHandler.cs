using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;

namespace Andreja.AppHost.OpenLoops;

public sealed class CircuitDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICircuitDelegationTokenService tokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(
                Request.Headers.Authorization,
                out var authorization)
            || !string.Equals(
                authorization.Scheme,
                CircuitDelegation.AuthorizationScheme,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var validation = tokenService.ValidateAndConsume(
            authorization.Parameter,
            CircuitDelegation.OpenLoopsAudience);
        return Task.FromResult(
            validation.Succeeded && validation.Principal is not null
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    validation.Principal,
                    CircuitDelegation.AuthenticationScheme))
                : AuthenticateResult.Fail(
                    validation.FailureCode ?? "delegation-token-invalid"));
    }
}
