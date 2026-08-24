using Andreja.Modules.Channels;
using Andreja.Modules.Execution;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class ChannelHostTests
{
    [Fact]
    public async Task SkillAndChannelHostsUseOneEvaluatorAndAuditContract()
    {
        var sink = new InMemoryExecutionAuditSink();
        var evaluator = new ExecutionAuthorizationEvaluator(sink);
        var skillManifest = ExecutionContractFixture.SkillManifest();
        var channelManifest = ExecutionContractFixture.ChannelManifest();
        var skill = new InMemorySkillHost(evaluator, sink);
        var channel = new InMemoryChannelHost(evaluator, sink);
        skill.Register(
            skillManifest,
            new Dictionary<string, SkillToolHandler>
            {
                [ExecutionContractFixture.ToolName] = (_, _, _) =>
                    ValueTask.FromResult(new SkillResult(
                        SkillResultStatus.Completed,
                        JsonSerializer.SerializeToElement(new { accepted = true }),
                        null,
                        null)),
            });
        channel.Register(
            channelManifest,
            new Dictionary<string, ChannelOperationHandler>
            {
                [ExecutionContractFixture.ChannelOperationName] = (_, context, _) =>
                    ValueTask.FromResult(new ChannelResult(
                        ChannelResultStatus.Completed,
                        JsonSerializer.SerializeToElement(new
                        {
                            disclosure = context.EffectiveDisclosure,
                        }),
                        null)),
            });
        var skillContext = ExecutionContractFixture.SkillContext();
        var channelContext = ExecutionContractFixture.ChannelContext();

        var skillResult = await skill.InvokeAsync(
            ExecutionContractFixture.SkillInvocation(skillManifest, skillContext),
            skillContext,
            CancellationToken.None);
        var channelResult = await channel.InvokeAsync(
            ExecutionContractFixture.ChannelInvocation(channelManifest, channelContext),
            channelContext,
            CancellationToken.None);

        Assert.Equal(SkillResultStatus.Completed, skillResult.Status);
        Assert.Equal(ChannelResultStatus.Completed, channelResult.Status);
        Assert.Equal(["skill", "channel"], sink.Entries.Select(entry => entry.ArtifactKind));
        Assert.All(sink.Entries, entry => Assert.Equal(ExecutionAuditOutcome.Allowed, entry.Outcome));
    }

    [Fact]
    public async Task ChannelFailsClosedForIdentityGrantCapabilityOperationAndDisclosure()
    {
        var (host, manifest, context) = CreateHost();
        var valid = ExecutionContractFixture.ChannelInvocation(manifest, context);
        var cases = new (ChannelInvocation Invocation, ChannelExecutionContext Context, string Code)[]
        {
            (valid with { TenantId = Guid.CreateVersion7() }, context, "wrong-tenant"),
            (valid with { AppUserId = Guid.CreateVersion7() }, context, "wrong-user"),
            (valid with { PrincipalId = Guid.CreateVersion7() }, context, "wrong-principal"),
            (valid with { Purpose = "profile.publish" }, context, "wrong-purpose"),
            (valid with { Operation = "delete" }, context, "operation-denied"),
            (valid with { DataClass = "credentials" }, context, "data-class-denied"),
            (valid with { RequestedDisclosure = DisclosureLevel.Full }, context, "disclosure-denied"),
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
                            ResourceReference = "other/resource",
                        },
                    },
                },
                "grant-scope-mismatch"),
        };

        foreach (var testCase in cases)
        {
            var result = await host.InvokeAsync(
                testCase.Invocation,
                testCase.Context,
                CancellationToken.None);

            Assert.Equal(ChannelResultStatus.Denied, result.Status);
            Assert.Equal(testCase.Code, result.Failure?.Code);
        }
    }

    [Fact]
    public async Task ChannelRejectsManifestTamperingAndUndeclaredOperation()
    {
        var (host, manifest, context) = CreateHost();
        var valid = ExecutionContractFixture.ChannelInvocation(manifest, context);

        var digest = await host.InvokeAsync(
            valid with { ManifestDigest = "BAD" },
            context,
            CancellationToken.None);
        var version = await host.InvokeAsync(
            valid with { ChannelVersion = "2.0.0" },
            context,
            CancellationToken.None);
        var operation = await host.InvokeAsync(
            valid with { OperationName = "local.tasks.delete" },
            context,
            CancellationToken.None);

        Assert.Equal("manifest-tampered", digest.Failure?.Code);
        Assert.Equal("channel-not-declared", version.Failure?.Code);
        Assert.Equal("operation-not-declared", operation.Failure?.Code);
    }

    [Fact]
    public async Task ChannelConcurrentInvocationsAreDeterministicAndContentFreeInAudit()
    {
        var (host, manifest, context) = CreateHost();
        var invocation = ExecutionContractFixture.ChannelInvocation(manifest, context);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ =>
                host.InvokeAsync(invocation, context, CancellationToken.None).AsTask()));

        Assert.All(results, result => Assert.Equal(ChannelResultStatus.Completed, result.Status));
        Assert.Equal(64, host.AuditEntries.Count);
        var auditJson = JsonSerializer.Serialize(host.AuditEntries);
        Assert.DoesNotContain("Book dentist", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Arguments", auditJson, StringComparison.Ordinal);
    }

    private static (
        InMemoryChannelHost Host,
        ChannelManifest Manifest,
        ChannelExecutionContext Context) CreateHost()
    {
        var manifest = ExecutionContractFixture.ChannelManifest();
        var host = new InMemoryChannelHost();
        host.Register(
            manifest,
            new Dictionary<string, ChannelOperationHandler>
            {
                [ExecutionContractFixture.ChannelOperationName] = (_, _, _) =>
                    ValueTask.FromResult(new ChannelResult(
                        ChannelResultStatus.Completed,
                        JsonSerializer.SerializeToElement(new { accepted = true }),
                        null)),
            });
        return (host, manifest, ExecutionContractFixture.ChannelContext());
    }
}
