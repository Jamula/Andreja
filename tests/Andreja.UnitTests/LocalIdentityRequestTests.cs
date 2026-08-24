using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.AspNetCore.Http;

namespace Andreja.UnitTests;

public sealed class LocalIdentityRequestTests
{
    private static readonly LocalIdentityOptions Options = new()
    {
        RelyingPartyId = "andreja.example",
        AllowedOrigins = ["https://andreja.example"],
        BootstrapTokenFile = Path.GetFullPath("unused"),
    };

    [Theory]
    [InlineData("https", "andreja.example", "https://andreja.example", true)]
    [InlineData("http", "andreja.example", "http://andreja.example", false)]
    [InlineData("https", "evil.example", "https://evil.example", false)]
    [InlineData("https", "andreja.example", "https://evil.example", false)]
    [InlineData("https", "sub.andreja.example", "https://sub.andreja.example", false)]
    [InlineData("https", "andreja.example:444", "https://andreja.example:444", false)]
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

        Assert.Equal(
            expected,
            LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                context.Request,
                Options));
    }

    [Fact]
    public void MissingOriginFailsClosed()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("andreja.example");

        Assert.False(LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
            context.Request,
            Options));
    }

    [Fact]
    public void ProductionIdentityOptionsRequireBoundedRecoveryRateLimit()
    {
        var validator = new LocalIdentityOptionsValidator();
        var invalid = Options with
        {
            RecoveryRateLimitAttempts = 0,
            RecoveryRateLimitWindow = TimeSpan.FromSeconds(1),
        };

        var result = validator.Validate(null, invalid);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(
            result.Failures!,
            failure => failure.Contains("RecoveryRateLimitAttempts", StringComparison.Ordinal));
        Assert.Contains(
            result.Failures!,
            failure => failure.Contains("RecoveryRateLimitWindow", StringComparison.Ordinal));
    }
}
