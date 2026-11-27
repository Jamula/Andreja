using System.Security.Claims;

namespace Andreja.AppHost.OpenLoops;

public interface ICircuitDelegationTokenService
{
    string Issue(ClaimsPrincipal principal, string audience);

    CircuitDelegationValidation ValidateAndConsume(string token, string audience);
}
