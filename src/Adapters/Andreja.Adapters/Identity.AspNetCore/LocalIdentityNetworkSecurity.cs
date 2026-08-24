using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Andreja.Adapters.Identity.AspNetCore;

public static class LocalIdentityNetworkSecurity
{
    public const string RecoveryRateLimitPolicy = "identity-recovery";
    public const string RecoveryOptionsPath = "/Account/Passkeys/RecoveryOptions";
    public const string RecoveryCompletePath = "/Account/Passkeys/RecoveryComplete";

    public static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions forwarded,
        LocalIdentityOptions identity)
    {
        ArgumentNullException.ThrowIfNull(forwarded);
        ArgumentNullException.ThrowIfNull(identity);

        forwarded.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedHost
            | ForwardedHeaders.XForwardedProto;
        forwarded.ForwardLimit = 1;
        forwarded.RequireHeaderSymmetry = true;
        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();
        foreach (var address in identity.TrustedProxyAddresses)
        {
            forwarded.KnownProxies.Add(IPAddress.Parse(address));
        }

        forwarded.AllowedHosts = identity.AllowedOrigins
            .Select(origin => new Uri(origin, UriKind.Absolute).IdnHost)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void ConfigureRateLimiting(
        RateLimiterOptions limiter,
        LocalIdentityOptions identity)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(identity);

        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            context => IsRecoveryRequest(context.Request.Path)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "identity-recovery-global",
                    _ => CreateWindow(
                        identity.RecoveryGlobalRateLimitAttempts,
                        identity.RecoveryRateLimitWindow))
                : RateLimitPartition.GetNoLimiter("non-recovery"));
        limiter.AddPolicy(
            RecoveryRateLimitPolicy,
            context => RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartition(context),
                _ => CreateWindow(
                    identity.RecoveryRateLimitAttempts,
                    identity.RecoveryRateLimitWindow)));
    }

    public static string GetClientPartition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = context.Connection.RemoteIpAddress;
        return address is null
            ? "missing-client-address"
            : address.MapToIPv6().ToString();
    }

    private static bool IsRecoveryRequest(PathString path) =>
        path.Equals(RecoveryOptionsPath, StringComparison.OrdinalIgnoreCase)
        || path.Equals(
            RecoveryOptionsPath + "/",
            StringComparison.OrdinalIgnoreCase);

    private static FixedWindowRateLimiterOptions CreateWindow(
        int permitLimit,
        TimeSpan window) =>
        new()
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true,
        };
}
