using Microsoft.AspNetCore.Identity;

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
