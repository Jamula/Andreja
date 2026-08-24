using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class SkillHostTests
{
    [Fact]
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

        Assert.Equal(SkillResultStatus.Completed, result.Status);
        Assert.NotNull(observed);
        Assert.Equal(ExecutionContractFixture.TenantId, observed.TenantId);
        Assert.Equal(ExecutionContractFixture.AppUserId, observed.AppUserId);
        Assert.Equal(ExecutionContractFixture.PrincipalId, observed.PrincipalId);
        Assert.Equal(DisclosureLevel.Summary, observed.EffectiveDisclosure);
        Assert.Equal(ExecutionAuditOutcome.Allowed, Assert.Single(host.AuditEntries).Outcome);
    }

    [Fact]
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

            Assert.Equal(SkillResultStatus.Denied, result.Status);
            Assert.Equal(testCase.Code, result.Failure?.Code);
        }

        Assert.Equal(cases.Length, host.AuditEntries.Count);
        Assert.All(host.AuditEntries, entry => Assert.Equal(ExecutionAuditOutcome.Denied, entry.Outcome));
    }

    [Fact]
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

        Assert.Equal(
            "user-policy-inactive",
            (await host.InvokeAsync(invocation, expiredPolicy, CancellationToken.None)).Failure?.Code);
        Assert.Equal(
            "grant-inactive",
            (await host.InvokeAsync(invocation, revokedGrant, CancellationToken.None)).Failure?.Code);
        Assert.Equal(
            "consent-inactive",
            (await host.InvokeAsync(invocation, revokedConsent, CancellationToken.None)).Failure?.Code);
    }

    [Fact]
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

        Assert.Equal("manifest-tampered", digestResult.Failure?.Code);
        Assert.Equal("skill-not-declared", versionResult.Failure?.Code);
        Assert.Equal("tool-not-declared", toolResult.Failure?.Code);
        Assert.Equal("manifest-tampered", mutationResult.Failure?.Code);
    }

    [Fact]
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

        Assert.All(results, result => Assert.Equal(SkillResultStatus.Completed, result.Status));
        Assert.Equal(64, observed.Count);
        Assert.All(
            observed,
            identity => Assert.Equal(
                (ExecutionContractFixture.AppUserId, ExecutionContractFixture.PrincipalId),
                identity));
        Assert.Equal(64, host.AuditEntries.Count);
    }

    [Fact]
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

        var audit = Assert.Single(host.AuditEntries);
        var serialized = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("title", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("wrong-user", audit.ReasonCode);
    }

    [Fact]
    public async Task HostReturnsStructuredCancellation()
    {
        var (host, manifest, context) = CreateHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await host.InvokeAsync(
            ExecutionContractFixture.SkillInvocation(manifest, context),
            context,
            cancellation.Token);

        Assert.Equal(SkillResultStatus.Cancelled, result.Status);
        Assert.Equal("cancelled", result.Failure?.Code);
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
