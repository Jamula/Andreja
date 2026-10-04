using Andreja.AppHost.Identity;
using Andreja.AppHost.OpenLoops;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

namespace Andreja.UnitTests;

[TestClass]
public sealed class CircuitDelegationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 24, 5, 15, 0, TimeSpan.Zero);

    [TestMethod]
    public void TokenIsAudienceBoundAndSingleUse()
    {
        var service = CreateService(out _);
        var principal = Principal();
        var token = service.Issue(principal, CircuitDelegation.OpenLoopsAudience);

        var wrongAudience = service.ValidateAndConsume(token, "andreja.other");
        var valid = service.ValidateAndConsume(
            token,
            CircuitDelegation.OpenLoopsAudience);
        var replay = service.ValidateAndConsume(
            token,
            CircuitDelegation.OpenLoopsAudience);

        Assert.IsFalse(wrongAudience.Succeeded);
        Assert.AreEqual("delegation-token-invalid", wrongAudience.FailureCode);
        Assert.IsTrue(valid.Succeeded);
        Assert.AreEqual(
            principal.FindFirst(AndrejaClaimTypes.TenantId)?.Value,
            valid.Principal?.FindFirst(AndrejaClaimTypes.TenantId)?.Value);
        Assert.IsFalse(replay.Succeeded);
        Assert.AreEqual("delegation-token-replayed", replay.FailureCode);
    }

    [TestMethod]
    public void ExpiredAndTamperedTokensFailClosed()
    {
        var service = CreateService(out var time);
        var token = service.Issue(Principal(), CircuitDelegation.OpenLoopsAudience);
        var replacement = token[^1] == 'A' ? 'B' : 'A';
        var tampered = token[..^1] + replacement;

        var tamperedResult = service.ValidateAndConsume(
            tampered,
            CircuitDelegation.OpenLoopsAudience);
        time.Advance(CircuitDelegation.TokenLifetime.Add(TimeSpan.FromSeconds(1)));
        var expired = service.ValidateAndConsume(
            token,
            CircuitDelegation.OpenLoopsAudience);

        Assert.IsFalse(tamperedResult.Succeeded);
        Assert.AreEqual("delegation-token-invalid", tamperedResult.FailureCode);
        Assert.IsFalse(expired.Succeeded);
        Assert.AreEqual("delegation-token-expired", expired.FailureCode);
    }

    [TestMethod]
    public void MissingOrConflictingTenantAndPrincipalClaimsCannotBeDelegated()
    {
        var service = CreateService(out _);
        var valid = Principal();
        var conflictingIdentity = new ClaimsIdentity(
            valid.Claims.Append(new Claim(
                AndrejaClaimTypes.TenantId,
                Guid.CreateVersion7().ToString("D"))),
            "test");
        var missingPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            valid.Claims.Where(
                claim => claim.Type != AndrejaClaimTypes.PrincipalId),
            "test"));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            service.Issue(
                new ClaimsPrincipal(conflictingIdentity),
                CircuitDelegation.OpenLoopsAudience));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            service.Issue(
                missingPrincipal,
                CircuitDelegation.OpenLoopsAudience));
    }

    private static CircuitDelegationTokenService CreateService(
        out MutableTimeProvider time)
    {
        time = new(Now);
        return new(new EphemeralDataProtectionProvider(), time);
    }

    private static ClaimsPrincipal Principal()
    {
        var tenantId = Guid.CreateVersion7();
        var appUserId = Guid.CreateVersion7();
        var principalId = Guid.CreateVersion7();
        return new(new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, appUserId.ToString("D")),
                new(AndrejaClaimTypes.TenantId, tenantId.ToString("D")),
                new(AndrejaClaimTypes.AppUserId, appUserId.ToString("D")),
                new(AndrejaClaimTypes.PrincipalId, principalId.ToString("D")),
            ],
            "test"));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }
}
