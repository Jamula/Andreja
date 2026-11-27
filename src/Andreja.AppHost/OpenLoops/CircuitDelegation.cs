namespace Andreja.AppHost.OpenLoops;

public static class CircuitDelegation
{
    public const string AuthenticationScheme = "Andreja.CircuitDelegation";
    public const string AuthorizationScheme = "Andreja-Circuit";
    public const string OpenLoopsAudience = "andreja.internal.open-loops.v1";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);
}
