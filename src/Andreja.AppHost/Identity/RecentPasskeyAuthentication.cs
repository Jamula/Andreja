using System.Security.Cryptography;
using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Andreja.AppHost.Identity;

internal sealed record RecentPasskeyAuthenticationTicket(
    Guid UserId,
    string SecurityStamp,
    string Audience);

internal sealed class RecentPasskeyAuthentication
{
    internal const string Audience = "andreja.passkey-management.v1";
    internal const string CookieName = "__Host-Andreja.RecentPasskey";
    private const string ProtectionPurpose =
        "Andreja.Identity.RecentPasskeyAuthentication.v1";
    private readonly ITimeLimitedDataProtector protector;
    private readonly IOptions<LocalIdentityOptions> options;

    public RecentPasskeyAuthentication(
        IDataProtectionProvider dataProtection,
        IOptions<LocalIdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        this.options = options;
        protector = dataProtection
            .CreateProtector(ProtectionPurpose)
            .ToTimeLimitedDataProtector();
    }

    public void Issue(HttpContext context, AspNetIdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        var stamp = user.SecurityStamp;
        if (string.IsNullOrEmpty(stamp))
        {
            throw new InvalidOperationException(
                "A security stamp is required for recent authentication.");
        }

        context.Response.Cookies.Append(
            CookieName,
            ProtectUntil(
                new(user.Id, stamp, Audience),
                DateTimeOffset.UtcNow + options.Value.RecentAuthenticationWindow),
            Cookie(options.Value.RecentAuthenticationWindow));
    }

    public bool IsValid(HttpContext context, AspNetIdentityUser user) =>
        TryRead(context, user, consume: false);

    public bool TryConsume(HttpContext context, AspNetIdentityUser user) =>
        TryRead(context, user, consume: true);

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, Cookie(null));

    internal string ProtectUntil(
        RecentPasskeyAuthenticationTicket ticket,
        DateTimeOffset expiration) =>
        protector.Protect(JsonSerializer.Serialize(ticket), expiration);

    private bool TryRead(
        HttpContext context,
        AspNetIdentityUser user,
        bool consume)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        if (!context.Request.Cookies.TryGetValue(CookieName, out var protectedTicket))
        {
            return false;
        }

        if (consume)
        {
            Clear(context);
        }

        try
        {
            var ticket = JsonSerializer.Deserialize<RecentPasskeyAuthenticationTicket>(
                protector.Unprotect(protectedTicket));
            return ticket is not null
                && ticket.UserId == user.Id
                && string.Equals(ticket.Audience, Audience, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(user.SecurityStamp)
                && string.Equals(
                    ticket.SecurityStamp,
                    user.SecurityStamp,
                    StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException)
        {
            return false;
        }
    }

    private static CookieOptions Cookie(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAge,
        };
}
