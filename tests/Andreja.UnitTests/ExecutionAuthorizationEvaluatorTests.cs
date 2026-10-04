using Andreja.Modules.Execution;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;

namespace Andreja.UnitTests;

[TestClass]
public sealed class ExecutionAuthorizationEvaluatorTests
{
    [TestMethod]
    public async Task EvaluatorRejectsEveryPolicyGrantConsentAndIdentityMismatch()
    {
        var valid = Request();
        var cases = new (ExecutionAuthorizationRequest Request, string Code)[]
        {
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    TenantId = Guid.CreateVersion7(),
                }),
                "wrong-tenant"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    AppUserId = Guid.CreateVersion7(),
                }),
                "wrong-user"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    PrincipalId = Guid.CreateVersion7(),
                }),
                "wrong-principal"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    AllowedPurposes = ExecutionContractFixture.Set("profile.publish"),
                }),
                "wrong-purpose"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    AllowedCapabilities = ExecutionContractFixture.Set("tasks.read"),
                }),
                "capability-denied"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    AllowedOperations = ExecutionContractFixture.Set("read"),
                }),
                "operation-denied"),
            (
                WithPolicy(valid, valid.Authorization.UserPolicy with
                {
                    AllowedDataClasses = ExecutionContractFixture.Set("credentials"),
                }),
                "data-class-denied"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    OwnerTenantId = Guid.CreateVersion7(),
                }),
                "grant-tenant-mismatch"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    GranteePrincipalId = Guid.CreateVersion7(),
                }),
                "grant-principal-mismatch"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    ResourceReference = "another/resource",
                }),
                "grant-scope-mismatch"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    ScopeReference = "credentials",
                }),
                "grant-scope-mismatch"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    Purpose = "profile.publish",
                }),
                "grant-inactive"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    AllowedOperations = ExecutionContractFixture.Set("read"),
                }),
                "grant-inactive"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    IsRevoked = true,
                    RevokedAt = ExecutionContractFixture.Now.AddMinutes(-1),
                }),
                "grant-inactive"),
            (
                WithGrant(valid, valid.Authorization.Grant with
                {
                    ExpiresAt = ExecutionContractFixture.Now,
                }),
                "grant-inactive"),
            (
                WithConsent(valid, valid.Authorization.Consent with
                {
                    ReceivingPrincipalId = Guid.CreateVersion7(),
                }),
                "consent-inactive"),
            (
                WithConsent(valid, valid.Authorization.Consent with
                {
                    Timeline =
                    [
                        .. valid.Authorization.Consent.Timeline,
                        new(
                            ConsentState.Revoked,
                            ExecutionContractFixture.PrincipalId,
                            ExecutionContractFixture.Now.AddMinutes(-1)),
                    ],
                }),
                "consent-inactive"),
            (
                valid with { RequestedDisclosure = (DisclosureLevel)99 },
                "disclosure-unknown"),
            (
                valid with { RequestedDisclosure = DisclosureLevel.Full },
                "disclosure-denied"),
        };
        var sink = new InMemoryExecutionAuditSink();
        var evaluator = new ExecutionAuthorizationEvaluator(sink);

        foreach (var testCase in cases)
        {
            var decision = await evaluator.EvaluateAsync(
                testCase.Request,
                CancellationToken.None);

            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual(testCase.Code, decision.Code);
            Assert.IsNull(decision.EffectiveDisclosure);
        }

        Assert.AreEqual(cases.Length, sink.Entries.Count);
        foreach (var entry in sink.Entries)
        {
            Assert.AreEqual(ExecutionAuditOutcome.Denied, entry.Outcome);
        }
    }

    [TestMethod]
    public async Task EvaluatorNeverWidensRequestedDisclosure()
    {
        var request = Request() with { RequestedDisclosure = DisclosureLevel.Existence };
        var sink = new InMemoryExecutionAuditSink();
        var evaluator = new ExecutionAuthorizationEvaluator(sink);

        var decision = await evaluator.EvaluateAsync(request, CancellationToken.None);

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual(DisclosureLevel.Existence, decision.EffectiveDisclosure);
        Assert.AreEqual(
            DisclosureLevel.Existence,
            Assert.ContainsSingle(sink.Entries).EffectiveDisclosure);
    }

    [TestMethod]
    public async Task EveryOrderedDisclosureCeilingBlocksEscalation()
    {
        var valid = Request();
        var fullPolicy = valid.Authorization.UserPolicy with
        {
            MaximumDisclosure = DisclosureLevel.Full,
        };
        var fullGrant = valid.Authorization.Grant with
        {
            MaximumDisclosure = DisclosureLevel.Full,
        };
        var fullConsent = valid.Authorization.Consent with
        {
            Terms = valid.Authorization.Consent.Terms with
            {
                MaximumDisclosure = DisclosureLevel.Full,
            },
        };
        var full = valid with
        {
            RequestedDisclosure = DisclosureLevel.Full,
            DeclaredDisclosureCeiling = DisclosureLevel.Full,
            Authorization = valid.Authorization with
            {
                UserPolicy = fullPolicy,
                Grant = fullGrant,
                Consent = fullConsent,
            },
        };
        var requests = new[]
        {
            full with { DeclaredDisclosureCeiling = DisclosureLevel.Summary },
            WithPolicy(full, fullPolicy with { MaximumDisclosure = DisclosureLevel.Summary }),
            WithGrant(full, fullGrant with { MaximumDisclosure = DisclosureLevel.Summary }),
            WithConsent(
                full,
                fullConsent with
                {
                    Terms = fullConsent.Terms with
                    {
                        MaximumDisclosure = DisclosureLevel.Summary,
                    },
                }),
        };
        var evaluator = new ExecutionAuthorizationEvaluator(new InMemoryExecutionAuditSink());

        foreach (var request in requests)
        {
            var decision = await evaluator.EvaluateAsync(request, CancellationToken.None);
            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual("disclosure-denied", decision.Code);
        }
    }

    [TestMethod]
    [DataRow("policy")]
    [DataRow("grant")]
    [DataRow("consent")]
    public async Task UnknownAuthorizationVersionsFailClosed(string component)
    {
        var request = Request();
        request = component switch
        {
            "policy" => WithPolicy(
                request,
                request.Authorization.UserPolicy with { Version = "2" }),
            "grant" => WithGrant(
                request,
                request.Authorization.Grant with { Version = "2" }),
            "consent" => WithConsent(
                request,
                request.Authorization.Consent with { Version = "2" }),
            _ => throw new InvalidOperationException(),
        };
        var evaluator = new ExecutionAuthorizationEvaluator(new InMemoryExecutionAuditSink());

        var decision = await evaluator.EvaluateAsync(request, CancellationToken.None);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual("authorization-version-unsupported", decision.Code);
    }

    private static ExecutionAuthorizationRequest Request() =>
        new(
            "skill",
            "local.open-loops",
            "1.0.0",
            ExecutionContractFixture.TenantId,
            ExecutionContractFixture.AppUserId,
            ExecutionContractFixture.PrincipalId,
            ExecutionContractFixture.Purpose,
            [ExecutionContractFixture.Capability],
            ExecutionContractFixture.Operation,
            ExecutionContractFixture.DataClass,
            DisclosureLevel.Summary,
            DisclosureLevel.Summary,
            ExecutionContractFixture.Resource,
            ExecutionContractFixture.Authorization());

    private static ExecutionAuthorizationRequest WithPolicy(
        ExecutionAuthorizationRequest request,
        UserExecutionPolicy policy) =>
        request with
        {
            Authorization = request.Authorization with { UserPolicy = policy },
        };

    private static ExecutionAuthorizationRequest WithGrant(
        ExecutionAuthorizationRequest request,
        Grant grant) =>
        request with
        {
            Authorization = request.Authorization with { Grant = grant },
        };

    private static ExecutionAuthorizationRequest WithConsent(
        ExecutionAuthorizationRequest request,
        ConsentRecord consent) =>
        request with
        {
            Authorization = request.Authorization with { Consent = consent },
        };
}
