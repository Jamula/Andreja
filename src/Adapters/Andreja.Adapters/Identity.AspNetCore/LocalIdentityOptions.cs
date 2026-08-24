using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public sealed record LocalIdentityOptions
{
    public const string SectionName = "Andreja:Identity";

    public string AuthenticationScheme { get; init; } = IdentityConstants.ApplicationScheme;

    public string RelyingPartyId { get; init; } = string.Empty;

    public string[] AllowedOrigins { get; init; } = [];

    public string[] TrustedProxyAddresses { get; init; } = ["127.0.0.1", "::1"];

    public string BootstrapTokenFile { get; init; } = string.Empty;

    public int BootstrapTokenBytes { get; init; } = 32;

    public TimeSpan BootstrapCeremonyLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public int MaximumPasskeysPerUser { get; init; } = 10;

    public int RecoveryCodeCount { get; init; } = 10;

    public TimeSpan RecoveryCodeLifetime { get; init; } = TimeSpan.FromDays(90);

    public TimeSpan RecentAuthenticationWindow { get; init; } = TimeSpan.FromMinutes(10);

    public int RecoveryRateLimitAttempts { get; init; } = 5;

    public int RecoveryGlobalRateLimitAttempts { get; init; } = 100;

    public TimeSpan RecoveryRateLimitWindow { get; init; } = TimeSpan.FromMinutes(15);
}

public sealed class LocalIdentityOptionsValidator : IValidateOptions<LocalIdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> failures = [];

        if (!string.Equals(
                options.AuthenticationScheme,
                IdentityConstants.ApplicationScheme,
                StringComparison.Ordinal))
        {
            failures.Add("Only the ASP.NET Core Identity application scheme is allowed.");
        }

        if (string.IsNullOrWhiteSpace(options.RelyingPartyId)
            || Uri.CheckHostName(options.RelyingPartyId) == UriHostNameType.Unknown)
        {
            failures.Add("RelyingPartyId must be a valid host name.");
        }

        if (options.AllowedOrigins.Length == 0)
        {
            failures.Add("At least one HTTPS WebAuthn origin is required.");
        }
        else
        {
            foreach (var originValue in options.AllowedOrigins)
            {
                if (!Uri.TryCreate(originValue, UriKind.Absolute, out var origin)
                    || origin.Scheme != Uri.UriSchemeHttps
                    || !string.IsNullOrEmpty(origin.UserInfo)
                    || !string.IsNullOrEmpty(origin.PathAndQuery.Trim('/'))
                    || !string.IsNullOrEmpty(origin.Fragment)
                    || !OriginMatchesRelyingParty(origin.Host, options.RelyingPartyId))
                {
                    failures.Add(
                        $"Allowed origin '{originValue}' must be an HTTPS origin within the relying-party domain.");
                }
            }
        }

        if (options.TrustedProxyAddresses.Length == 0)
        {
            failures.Add("At least one exact trusted proxy IP address is required.");
        }
        else
        {
            foreach (var proxyValue in options.TrustedProxyAddresses)
            {
                if (!System.Net.IPAddress.TryParse(proxyValue, out var proxy)
                    || !IsExactUnicastAddress(proxy))
                {
                    failures.Add(
                        $"Trusted proxy '{proxyValue}' must be one exact unicast IP address.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(options.BootstrapTokenFile)
            || !Path.IsPathFullyQualified(options.BootstrapTokenFile))
        {
            failures.Add("BootstrapTokenFile must be an absolute host-mounted file path.");
        }

        if (options.BootstrapTokenBytes < 32)
        {
            failures.Add("Bootstrap tokens must contain at least 256 bits of entropy.");
        }

        if (options.BootstrapCeremonyLifetime < TimeSpan.FromMinutes(1)
            || options.BootstrapCeremonyLifetime > TimeSpan.FromMinutes(10))
        {
            failures.Add(
                "BootstrapCeremonyLifetime must be between one and ten minutes.");
        }

        if (options.MaximumPasskeysPerUser is < 2 or > 20)
        {
            failures.Add("MaximumPasskeysPerUser must be between 2 and 20.");
        }

        if (options.RecoveryCodeCount is < 8 or > 32)
        {
            failures.Add("RecoveryCodeCount must be between 8 and 32.");
        }

        if (options.RecoveryCodeLifetime < TimeSpan.FromHours(1)
            || options.RecoveryCodeLifetime > TimeSpan.FromDays(365))
        {
            failures.Add("RecoveryCodeLifetime must be between one hour and 365 days.");
        }

        if (options.RecentAuthenticationWindow <= TimeSpan.Zero
            || options.RecentAuthenticationWindow > TimeSpan.FromMinutes(30))
        {
            failures.Add("RecentAuthenticationWindow must be greater than zero and at most 30 minutes.");
        }

        if (options.RecoveryRateLimitAttempts is < 1 or > 20)
        {
            failures.Add("RecoveryRateLimitAttempts must be between 1 and 20.");
        }

        if (options.RecoveryGlobalRateLimitAttempts is < 20 or > 1000
            || options.RecoveryGlobalRateLimitAttempts
                < options.RecoveryRateLimitAttempts)
        {
            failures.Add(
                "RecoveryGlobalRateLimitAttempts must be between 20 and 1000 and not less than the per-client limit.");
        }

        if (options.RecoveryRateLimitWindow < TimeSpan.FromMinutes(1)
            || options.RecoveryRateLimitWindow > TimeSpan.FromHours(24))
        {
            failures.Add("RecoveryRateLimitWindow must be between one minute and 24 hours.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool OriginMatchesRelyingParty(string host, string relyingPartyId)
    {
        return host.Equals(relyingPartyId, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{relyingPartyId}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactUnicastAddress(System.Net.IPAddress address)
    {
        if (address.Equals(System.Net.IPAddress.Any)
            || address.Equals(System.Net.IPAddress.IPv6Any)
            || address.Equals(System.Net.IPAddress.Broadcast)
            || address.Equals(System.Net.IPAddress.None)
            || address.IsIPv6Multicast)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return address.AddressFamily
            != System.Net.Sockets.AddressFamily.InterNetwork
            || bytes[0] < 224;
    }
}
