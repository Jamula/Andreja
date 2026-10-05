using Andreja.Modules.Identity;

namespace Andreja.UnitTests;

[TestClass]
public sealed class IdentityDomainTests
{
    [TestMethod]
    public void IdentityIdsUseGuidVersionSeven()
    {
        Assert.AreEqual(7, TenantId.New().Value.Version);
        Assert.AreEqual(7, AppUserId.New().Value.Version);
        Assert.AreEqual(7, ExternalIdentityId.New().Value.Version);
        Assert.AreEqual(7, MembershipId.New().Value.Version);
        Assert.AreEqual(7, PrincipalId.New().Value.Version);
        Assert.AreEqual(7, ContactId.New().Value.Version);
    }

    [TestMethod]
    public void ExternalIdentityRequiresHttpsIssuer()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new ExternalIdentity(
                ExternalIdentityId.New(),
                AppUserId.New(),
                "http://issuer.example",
                "subject"));
    }

    [TestMethod]
    public void PrimaryIdentityMustBelongToUser()
    {
        var user = new AppUser(AppUserId.New(), "Owner");
        var otherIdentity = new ExternalIdentity(
            ExternalIdentityId.New(),
            AppUserId.New(),
            "https://issuer.example",
            "subject");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => user.SelectPrimaryIdentity(otherIdentity));
    }

    [TestMethod]
    public void ScopedContextIsRequiredAndImmutable()
    {
        var accessor = new ScopedTenantPrincipalContext();
        Assert.ThrowsExactly<IdentityAccessDeniedException>(
            () => TenantPrincipalContext.Require(accessor));

        var context = CreateContext();
        accessor.Set(context);

        Assert.AreSame(context, TenantPrincipalContext.Require(accessor));
        Assert.ThrowsExactly<InvalidOperationException>(() => accessor.Set(CreateContext()));
    }

    [TestMethod]
    public void LastAuthenticationPathCannotBeRevoked()
    {
        var paths = new AuthenticationPathState(
            PasskeyCount: 1,
            ExternalIdentityCount: 0,
            UnusedRecoveryCodeCount: 0);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRevokePasskey(paths));
    }

    [TestMethod]
    [DataRow(false, true, true, true)]
    [DataRow(false, false, true, true)]
    [DataRow(false, true, false, true)]
    [DataRow(false, true, true, false)]
    public void BootstrapFailsUnlessEveryRequirementIsSatisfied(
        bool initialized,
        bool https,
        bool originAccepted,
        bool tokenVerified)
    {
        if (!initialized && https && originAccepted && tokenVerified)
        {
            IdentityCredentialPolicy.EnsureCanBootstrap(
                initialized,
                https,
                originAccepted,
                tokenVerified);
            return;
        }

        Assert.ThrowsExactly<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanBootstrap(
                initialized,
                https,
                originAccepted,
                tokenVerified));
    }

    [TestMethod]
    public void RecoveryRequiresRateLimitCodeAndNewPasskey()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: false,
                recoveryCodeVerified: true,
                newPasskeyAttestation: "attestation"));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: true,
                recoveryCodeVerified: false,
                newPasskeyAttestation: "attestation"));
        Assert.ThrowsExactly<ArgumentException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: true,
                recoveryCodeVerified: true,
                newPasskeyAttestation: string.Empty));
    }

    [TestMethod]
    public void RecoveryPathAllowsPasskeyRevocation()
    {
        var paths = new AuthenticationPathState(
            PasskeyCount: 1,
            ExternalIdentityCount: 0,
            UnusedRecoveryCodeCount: 1);

        IdentityCredentialPolicy.EnsureCanRevokePasskey(paths);
    }

    [TestMethod]
    public void IdentityLinkRequiresRecentAuthenticationAndProviderProof()
    {
        var now = DateTimeOffset.UtcNow;
        var staleRequest = new ExternalIdentityLinkRequest(
            new Uri("https://issuer.example"),
            "subject",
            "proof",
            now.AddHours(-1));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanLinkExternalIdentity(
                staleRequest,
                now,
                TimeSpan.FromMinutes(10)));

        var missingProof = staleRequest with
        {
            ProviderProof = string.Empty,
            AuthenticatedAt = now,
        };
        Assert.ThrowsExactly<ArgumentException>(
            () => IdentityCredentialPolicy.EnsureCanLinkExternalIdentity(
                missingProof,
                now,
                TimeSpan.FromMinutes(10)));
    }

    private static TenantPrincipalContext CreateContext() =>
        new(TenantId.New(), AppUserId.New(), PrincipalId.New(), "unit-test");
}
