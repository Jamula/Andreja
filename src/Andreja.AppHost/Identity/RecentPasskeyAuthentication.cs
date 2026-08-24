using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace Andreja.AppHost.Identity;

internal sealed record RecentPasskeyAuthenticationTicket(
    Guid UserId,
    string SecurityStamp,
    string Audience,
    string Nonce);

internal sealed class RecentPasskeyAuthentication
{
    internal const string Audience = "andreja.passkey-management.v1";
    internal const string CookieName = "__Host-Andreja.RecentPasskey";
    private const string ProtectionPurpose =
        "Andreja.Identity.RecentPasskeyAuthentication.v1";
    private readonly ITimeLimitedDataProtector protector;
    private readonly IOptions<LocalIdentityOptions> options;
    private readonly IRecentAuthenticationGrantStore grants;
    private readonly TimeProvider timeProvider;

    public RecentPasskeyAuthentication(
        IDataProtectionProvider dataProtection,
        IOptions<LocalIdentityOptions> options,
        IRecentAuthenticationGrantStore grants,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        this.options = options;
        this.grants = grants;
        this.timeProvider = timeProvider;
        protector = dataProtection
            .CreateProtector(ProtectionPurpose)
            .ToTimeLimitedDataProtector();
    }

    internal sealed class UnavailableAppUserDisplayNameResolver
        : IAppUserDisplayNameResolver
    {
        public Task<string?> ResolveAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    internal sealed class UnavailableRecentAuthenticationGrantStore
        : IRecentAuthenticationGrantStore
    {
        public Task IssueAsync(
            Guid userId,
            byte[] nonceHash,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Durable local identity storage is unavailable.");

        public Task<bool> IsValidAsync(
            Guid userId,
            byte[] nonceHash,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryConsumeAsync(
            Guid userId,
            byte[] nonceHash,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    public async Task IssueAsync(
        HttpContext context,
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        var stamp = user.SecurityStamp;
        if (string.IsNullOrEmpty(stamp))
        {
            throw new InvalidOperationException(
                "A security stamp is required for recent authentication.");
        }

        var nonceBytes = RandomNumberGenerator.GetBytes(32);
        var nonce = WebEncoders.Base64UrlEncode(nonceBytes);
        var nonceHash = SHA256.HashData(nonceBytes);
        var expiresAt =
            timeProvider.GetUtcNow() + options.Value.RecentAuthenticationWindow;
        try
        {
            await grants.IssueAsync(
                user.Id,
                nonceHash,
                expiresAt,
                cancellationToken);
            context.Response.Cookies.Append(
                CookieName,
                ProtectUntil(
                    new(user.Id, stamp, Audience, nonce),
                    expiresAt),
                Cookie(options.Value.RecentAuthenticationWindow));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonceBytes);
            CryptographicOperations.ZeroMemory(nonceHash);
        }
    }

    public Task<bool> IsValidAsync(
        HttpContext context,
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default) =>
        TryReadAsync(context, user, consume: false, cancellationToken);

    public Task<bool> TryConsumeAsync(
        HttpContext context,
        AspNetIdentityUser user,
        CancellationToken cancellationToken = default) =>
        TryReadAsync(context, user, consume: true, cancellationToken);

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, Cookie(null));

    internal string ProtectUntil(
        RecentPasskeyAuthenticationTicket ticket,
        DateTimeOffset expiration) =>
        protector.Protect(JsonSerializer.Serialize(ticket), expiration);

    private async Task<bool> TryReadAsync(
        HttpContext context,
        AspNetIdentityUser user,
        bool consume,
        CancellationToken cancellationToken)
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
            if (ticket is null
                || ticket.UserId != user.Id
                || !string.Equals(ticket.Audience, Audience, StringComparison.Ordinal)
                || string.IsNullOrEmpty(ticket.Nonce)
                || string.IsNullOrEmpty(user.SecurityStamp)
                || !string.Equals(
                    ticket.SecurityStamp,
                    user.SecurityStamp,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var nonceBytes = WebEncoders.Base64UrlDecode(ticket.Nonce);
            var nonceHash = SHA256.HashData(nonceBytes);
            try
            {
                return consume
                    ? await grants.TryConsumeAsync(
                        user.Id,
                        nonceHash,
                        timeProvider.GetUtcNow(),
                        cancellationToken)
                    : await grants.IsValidAsync(
                        user.Id,
                        nonceHash,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(nonceBytes);
                CryptographicOperations.ZeroMemory(nonceHash);
            }
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException or FormatException)
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
