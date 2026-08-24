using Andreja.Modules.Identity;

namespace Andreja.UnitTests;

public sealed class IdentityDomainTests
{
    [Fact]
    public void IdentityIdsUseGuidVersionSeven()
    {
        Assert.Equal(7, TenantId.New().Value.Version);
        Assert.Equal(7, AppUserId.New().Value.Version);
        Assert.Equal(7, ExternalIdentityId.New().Value.Version);
        Assert.Equal(7, MembershipId.New().Value.Version);
        Assert.Equal(7, PrincipalId.New().Value.Version);
        Assert.Equal(7, ContactId.New().Value.Version);
    }

    [Fact]
    public void ExternalIdentityRequiresHttpsIssuer()
    {
        Assert.Throws<ArgumentException>(
            () => new ExternalIdentity(
                ExternalIdentityId.New(),
                AppUserId.New(),
                "http://issuer.example",
                "subject"));
    }

    [Fact]
    public void PrimaryIdentityMustBelongToUser()
    {
        var user = new AppUser(AppUserId.New(), "Owner");
        var otherIdentity = new ExternalIdentity(
            ExternalIdentityId.New(),
            AppUserId.New(),
            "https://issuer.example",
            "subject");

        Assert.Throws<InvalidOperationException>(
            () => user.SelectPrimaryIdentity(otherIdentity));
    }

    [Fact]
    public void ScopedContextIsRequiredAndImmutable()
    {
        var accessor = new ScopedTenantPrincipalContext();
        Assert.Throws<IdentityAccessDeniedException>(
            () => TenantPrincipalContext.Require(accessor));

        var context = CreateContext();
        accessor.Set(context);

        Assert.Same(context, TenantPrincipalContext.Require(accessor));
        Assert.Throws<InvalidOperationException>(() => accessor.Set(CreateContext()));
    }

    [Fact]
    public void LastAuthenticationPathCannotBeRevoked()
    {
        var paths = new AuthenticationPathState(
            PasskeyCount: 1,
            ExternalIdentityCount: 0,
            UnusedRecoveryCodeCount: 0);

        Assert.Throws<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRevokePasskey(paths));
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
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

        Assert.Throws<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanBootstrap(
                initialized,
                https,
                originAccepted,
                tokenVerified));
    }

    [Fact]
    public void RecoveryRequiresRateLimitCodeAndNewPasskey()
    {
        Assert.Throws<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: false,
                recoveryCodeVerified: true,
                newPasskeyAttestation: "attestation"));
        Assert.Throws<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: true,
                recoveryCodeVerified: false,
                newPasskeyAttestation: "attestation"));
        Assert.Throws<ArgumentException>(
            () => IdentityCredentialPolicy.EnsureCanRecover(
                rateLimitAcquired: true,
                recoveryCodeVerified: true,
                newPasskeyAttestation: string.Empty));
    }

    [Fact]
    public void RecoveryPathAllowsPasskeyRevocation()
    {
        var paths = new AuthenticationPathState(
            PasskeyCount: 1,
            ExternalIdentityCount: 0,
            UnusedRecoveryCodeCount: 1);

        IdentityCredentialPolicy.EnsureCanRevokePasskey(paths);
    }

    [Fact]
    public void IdentityLinkRequiresRecentAuthenticationAndProviderProof()
    {
        var now = DateTimeOffset.UtcNow;
        var staleRequest = new ExternalIdentityLinkRequest(
            new Uri("https://issuer.example"),
            "subject",
            "proof",
            now.AddHours(-1));

        Assert.Throws<InvalidOperationException>(
            () => IdentityCredentialPolicy.EnsureCanLinkExternalIdentity(
                staleRequest,
                now,
                TimeSpan.FromMinutes(10)));

        var missingProof = staleRequest with
        {
            ProviderProof = string.Empty,
            AuthenticatedAt = now,
        };
        Assert.Throws<ArgumentException>(
            () => IdentityCredentialPolicy.EnsureCanLinkExternalIdentity(
                missingProof,
                now,
                TimeSpan.FromMinutes(10)));
    }

    private static TenantPrincipalContext CreateContext() =>
        new(TenantId.New(), AppUserId.New(), PrincipalId.New(), "unit-test");
}
