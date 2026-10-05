using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.AspNetCore.Http;

namespace Andreja.UnitTests;

[TestClass]
public sealed class LocalIdentityRequestTests
{
    private static readonly LocalIdentityOptions Options = new()
    {
        RelyingPartyId = "andreja.example",
        AllowedOrigins = ["https://andreja.example"],
        BootstrapTokenFile = Path.GetFullPath("unused"),
    };

    [TestMethod]
    [DataRow("https", "andreja.example", "https://andreja.example", true)]
    [DataRow("http", "andreja.example", "http://andreja.example", false)]
    [DataRow("https", "evil.example", "https://evil.example", false)]
    [DataRow("https", "andreja.example", "https://evil.example", false)]
    [DataRow("https", "sub.andreja.example", "https://sub.andreja.example", false)]
    [DataRow("https", "andreja.example:444", "https://andreja.example:444", false)]
    public void RelyingPartyRequestRequiresExactConfiguredHttpsOrigin(
        string scheme,
        string host,
        string origin,
        bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(host);
        context.Request.Headers.Origin = origin;

        Assert.AreEqual(
            expected,
            LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                Options));
    }

    [TestMethod]
    public void MissingOriginFailsClosed()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("andreja.example");

        Assert.IsFalse(LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
            context.Request,
            Options));
    }

    [TestMethod]
    public void ProductionIdentityOptionsRequireBoundedRecoveryRateLimit()
    {
        var validator = new LocalIdentityOptionsValidator();
        var invalid = Options with
        {
            TrustedProxyAddresses = ["0.0.0.0"],
            BootstrapCeremonyLifetime = TimeSpan.FromSeconds(1),
            RecoveryRateLimitAttempts = 0,
            RecoveryGlobalRateLimitAttempts = 1,
            RecoveryRateLimitWindow = TimeSpan.FromSeconds(1),
        };

        var result = validator.Validate(null, invalid);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.IsTrue(result.Failures!.Any(
            failure => failure.Contains(
                "BootstrapCeremonyLifetime",
                StringComparison.Ordinal)));
        Assert.IsTrue(result.Failures.Any(
            failure => failure.Contains("Trusted proxy", StringComparison.Ordinal)));
        Assert.IsTrue(result.Failures.Any(
            failure => failure.Contains("RecoveryRateLimitAttempts", StringComparison.Ordinal)));
        Assert.IsTrue(result.Failures.Any(
            failure => failure.Contains(
                "RecoveryGlobalRateLimitAttempts",
                StringComparison.Ordinal)));
        Assert.IsTrue(result.Failures.Any(
            failure => failure.Contains("RecoveryRateLimitWindow", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(42, false)]
    [DataRow(43, true)]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public void RecoveryCodesAreLengthBoundedBeforeHashing(
        int length,
        bool expected) =>
        Assert.AreEqual(
            expected,
            LocalIdentityOperations.IsPlausibleRecoveryCode(
                new string('A', length)));

    [TestMethod]
    public void RecoveryCodeBoundsRejectWhitespaceShrinkageAndNull()
    {
        Assert.IsFalse(LocalIdentityOperations.IsPlausibleRecoveryCode(null));
        Assert.IsFalse(LocalIdentityOperations.IsPlausibleRecoveryCode(
            " " + new string(
                'A',
                LocalIdentityOperations.RecoveryCodeMinimumLength - 1)));
    }
}
