using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Andreja.UnitTests;

[TestClass]
public sealed class SkillHostTests
{
    [TestMethod]
    public async Task AuthorizedInvocationPreservesAllIdentitiesAndLeastDisclosure()
    {
        SkillExecutionContext? observed = null;
        var (host, manifest, context) = CreateHost((_, actual, _) =>
        {
            observed = actual;
            return ValueTask.FromResult(Completed());
        });

        var result = await host.InvokeAsync(
            ExecutionContractFixture.SkillInvocation(manifest, context),
            context,
            CancellationToken.None);

        Assert.AreEqual(SkillResultStatus.Completed, result.Status);
        Assert.IsNotNull(observed);
        Assert.AreEqual(ExecutionContractFixture.TenantId, observed.TenantId);
        Assert.AreEqual(ExecutionContractFixture.AppUserId, observed.AppUserId);
        Assert.AreEqual(ExecutionContractFixture.PrincipalId, observed.PrincipalId);
        Assert.AreEqual(DisclosureLevel.Summary, observed.EffectiveDisclosure);
        Assert.AreEqual(ExecutionAuditOutcome.Allowed, Assert.ContainsSingle(host.AuditEntries).Outcome);
    }

    [TestMethod]
    public async Task HostFailsClosedForEveryIdentityAndDeclaredPermissionMismatch()
    {
        var (host, manifest, context) = CreateHost();
        var valid = ExecutionContractFixture.SkillInvocation(manifest, context);
        var cases = new (SkillInvocation Invocation, SkillExecutionContext Context, string Code)[]
        {
            (valid with { TenantId = Guid.CreateVersion7() }, context, "wrong-tenant"),
            (valid with { AppUserId = Guid.CreateVersion7() }, context, "wrong-user"),
            (valid with { PrincipalId = Guid.CreateVersion7() }, context, "wrong-principal"),
            (valid with { Purpose = "profile.publish" }, context, "wrong-purpose"),
            (valid with { Operation = "delete" }, context, "operation-denied"),
            (valid with { DataClass = "credentials" }, context, "data-class-denied"),
            (
                valid,
                context with
                {
                    Authorization = context.Authorization with
                    {
                        UserPolicy = context.Authorization.UserPolicy with
                        {
                            AllowedCapabilities = ExecutionContractFixture.Set("tasks.read"),
                        },
                    },
                },
                "capability-denied"),
            (
                valid,
                context with
                {
                    Authorization = context.Authorization with
                    {
                        Grant = context.Authorization.Grant with
                        {
                            AllowedOperations = ExecutionContractFixture.Set("read"),
                        },
                    },
                },
                "grant-inactive"),
            (valid with { RequestedDisclosure = DisclosureLevel.Full }, context, "disclosure-denied"),
        };

        foreach (var testCase in cases)
        {
            var result = await host.InvokeAsync(
                testCase.Invocation,
                testCase.Context,
                CancellationToken.None);

            Assert.AreEqual(SkillResultStatus.Denied, result.Status);
            Assert.AreEqual(testCase.Code, result.Failure?.Code);
        }

        Assert.AreEqual(cases.Length, host.AuditEntries.Count);
        foreach (var entry in host.AuditEntries)
        {
            Assert.AreEqual(ExecutionAuditOutcome.Denied, entry.Outcome);
        }
    }

    [TestMethod]
    public async Task HostRejectsInactivePolicyGrantAndConsent()
    {
        var (host, manifest, context) = CreateHost();
        var invocation = ExecutionContractFixture.SkillInvocation(manifest, context);
        var expiredPolicy = context with
        {
            Authorization = context.Authorization with
            {
                UserPolicy = context.Authorization.UserPolicy with
                {
                    ExpiresAt = ExecutionContractFixture.Now,
                },
            },
        };
        var revokedGrant = context with
        {
            Authorization = context.Authorization with
            {
                Grant = context.Authorization.Grant with
                {
                    IsRevoked = true,
                    RevokedAt = ExecutionContractFixture.Now.AddMinutes(-1),
                },
            },
        };
        var revokedConsent = context with
        {
            Authorization = context.Authorization with
            {
                Consent = context.Authorization.Consent with
                {
                    Timeline =
                    [
                        .. context.Authorization.Consent.Timeline,
                        new(
                            ConsentState.Revoked,
                            ExecutionContractFixture.PrincipalId,
                            ExecutionContractFixture.Now.AddMinutes(-1)),
                    ],
                },
            },
        };

        Assert.AreEqual(
            "user-policy-inactive",
            (await host.InvokeAsync(invocation, expiredPolicy, CancellationToken.None)).Failure?.Code);
        Assert.AreEqual(
            "grant-inactive",
            (await host.InvokeAsync(invocation, revokedGrant, CancellationToken.None)).Failure?.Code);
        Assert.AreEqual(
            "consent-inactive",
            (await host.InvokeAsync(invocation, revokedConsent, CancellationToken.None)).Failure?.Code);
    }

    [TestMethod]
    public async Task HostRejectsManifestDigestVersionMutationAndUnknownTool()
    {
        var tools = new List<ToolDefinition>(
            ExecutionContractFixture.SkillManifest().Tools);
        var manifest = ExecutionContractFixture.SkillManifest(tools);
        var (host, _, context) = CreateHost(manifest: manifest);
        var valid = ExecutionContractFixture.SkillInvocation(manifest, context);

        var digestResult = await host.InvokeAsync(
            valid with { ManifestDigest = new string('0', 64) },
            context,
            CancellationToken.None);
        var versionResult = await host.InvokeAsync(
            valid with { SkillVersion = "2.0.0" },
            context,
            CancellationToken.None);
        var toolResult = await host.InvokeAsync(
            valid with { ToolName = "open-loops.delete-all" },
            context,
            CancellationToken.None);
        tools[0] = tools[0] with { Description = "Tampered after registration." };
        var mutationResult = await host.InvokeAsync(valid, context, CancellationToken.None);

        Assert.AreEqual("manifest-tampered", digestResult.Failure?.Code);
        Assert.AreEqual("skill-not-declared", versionResult.Failure?.Code);
        Assert.AreEqual("tool-not-declared", toolResult.Failure?.Code);
        Assert.AreEqual("manifest-tampered", mutationResult.Failure?.Code);
    }

    [TestMethod]
    public async Task ConcurrentInvocationsRemainIsolatedAndAudited()
    {
        var observed = new ConcurrentBag<(Guid User, Guid Principal)>();
        var (host, manifest, context) = CreateHost((_, actual, _) =>
        {
            observed.Add((actual.AppUserId, actual.PrincipalId));
            return ValueTask.FromResult(Completed());
        });
        var invocation = ExecutionContractFixture.SkillInvocation(manifest, context);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ =>
                host.InvokeAsync(invocation, context, CancellationToken.None).AsTask()));

        foreach (var result in results)
        {
            Assert.AreEqual(SkillResultStatus.Completed, result.Status);
        }
        Assert.AreEqual(64, observed.Count);
        foreach (var identity in observed)
        {
            Assert.AreEqual(
                (ExecutionContractFixture.AppUserId, ExecutionContractFixture.PrincipalId),
                identity);
        }
        Assert.AreEqual(64, host.AuditEntries.Count);
    }

    [TestMethod]
    public async Task DeniedAuditIsContentMinimized()
    {
        var (host, manifest, context) = CreateHost();
        const string secret = "canary-secret-must-not-enter-audit";
        var invocation = ExecutionContractFixture.SkillInvocation(manifest, context) with
        {
            Arguments = new Dictionary<string, JsonElement>
            {
                ["title"] = JsonSerializer.SerializeToElement(secret),
            },
            AppUserId = Guid.CreateVersion7(),
        };

        await host.InvokeAsync(invocation, context, CancellationToken.None);

        var audit = Assert.ContainsSingle(host.AuditEntries);
        var serialized = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("title", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual("wrong-user", audit.ReasonCode);
    }

    [TestMethod]
    public async Task HostReturnsStructuredCancellation()
    {
        var (host, manifest, context) = CreateHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await host.InvokeAsync(
            ExecutionContractFixture.SkillInvocation(manifest, context),
            context,
            cancellation.Token);

        Assert.AreEqual(SkillResultStatus.Cancelled, result.Status);
        Assert.AreEqual("cancelled", result.Failure?.Code);
    }

    private static (
        InMemorySkillHost Host,
        SkillManifest Manifest,
        SkillExecutionContext Context) CreateHost(
        SkillToolHandler? handler = null,
        SkillManifest? manifest = null)
    {
        manifest ??= ExecutionContractFixture.SkillManifest();
        var host = new InMemorySkillHost();
        host.Register(
            manifest,
            new Dictionary<string, SkillToolHandler>(StringComparer.Ordinal)
            {
                [ExecutionContractFixture.ToolName] = handler ??
                    ((_, _, _) => ValueTask.FromResult(Completed())),
            });
        return (host, manifest, ExecutionContractFixture.SkillContext());
    }

    private static SkillResult Completed() =>
        new(
            SkillResultStatus.Completed,
            JsonSerializer.SerializeToElement(new { accepted = true }),
            null,
            null);
}
