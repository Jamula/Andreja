namespace Andreja.AppHost.Identity;

internal sealed record RecentPasskeyAuthenticationTicket(
    Guid UserId,
    string SecurityStamp,
    string Audience,
    string Nonce);
