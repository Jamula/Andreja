using System.Security.Claims;

namespace Andreja.AppHost.OpenLoops;

public sealed record CircuitDelegationValidation(
    bool Succeeded,
    ClaimsPrincipal? Principal,
    string? FailureCode);
