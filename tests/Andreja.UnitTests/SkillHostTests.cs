using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class SkillHostTests
{
    [Fact]
    public async Task HostDeniesUndeclaredTool()
    {
        var (host, manifest, context) = CreateHost();
        var invocation = Invocation(manifest, context) with { ToolName = "open-loops.delete-all" };

        var result = await host.InvokeAsync(invocation, context, CancellationToken.None);

        Assert.Equal(SkillResultStatus.Denied, result.Status);
        Assert.Equal("tool-not-declared", result.Failure?.Code);
    }

    [Fact]
    public async Task HostDeniesWrongPurposeAndTenant()
    {
        var (host, manifest, context) = CreateHost();
        var wrongPurpose = Invocation(manifest, context) with { Purpose = "profile.publish" };
        var wrongTenant = Invocation(manifest, context) with { TenantId = Guid.CreateVersion7() };

        var purposeResult = await host.InvokeAsync(wrongPurpose, context, CancellationToken.None);
        var tenantResult = await host.InvokeAsync(wrongTenant, context, CancellationToken.None);

        Assert.Equal("wrong-purpose", purposeResult.Failure?.Code);
        Assert.Equal("wrong-tenant", tenantResult.Failure?.Code);
    }

    [Fact]
    public async Task HostDeniesManifestTamperingAndMissingCapability()
    {
        var (host, manifest, context) = CreateHost();
        var invocation = Invocation(manifest, context);
        var tampered = invocation with { ManifestDigest = new string('0', 64) };
        var noGrant = context with { GrantedCapabilities = new HashSet<string>() };

        var tamperResult = await host.InvokeAsync(tampered, context, CancellationToken.None);
        var grantResult = await host.InvokeAsync(invocation, noGrant, CancellationToken.None);

        Assert.Equal("manifest-tampered", tamperResult.Failure?.Code);
        Assert.Equal("capability-denied", grantResult.Failure?.Code);
    }

    [Fact]
    public async Task HostReturnsStructuredCancellation()
    {
        var (host, manifest, context) = CreateHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await host.InvokeAsync(Invocation(manifest, context), context, cancellation.Token);

        Assert.Equal(SkillResultStatus.Cancelled, result.Status);
        Assert.Equal("cancelled", result.Failure?.Code);
    }

    [Fact]
    public async Task HostDetectsRegisteredManifestMutation()
    {
        var toolList = new List<ToolDefinition>
        {
            new(
                "open-loops.propose-task",
                "1",
                "Propose a task without writing it.",
                [new("title", ToolValueKind.Text, true)],
                ["tasks.propose"],
                ["task.capture"]),
        };
        var manifest = new SkillManifest("open-loops", "1", "Open Loops", toolList);
        var host = new InMemorySkillHost();
        host.Register(
            manifest,
            new Dictionary<string, SkillToolHandler>
            {
                ["open-loops.propose-task"] = (_, _, _) =>
                    ValueTask.FromResult(new SkillResult(SkillResultStatus.Completed, null, null, null)),
            });
        var context = new SkillExecutionContext(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "task.capture",
            new HashSet<string>(["tasks.propose"]));
        var invocation = Invocation(manifest, context);
        toolList[0] = toolList[0] with { AllowedPurposes = ["profile.publish"] };

        var result = await host.InvokeAsync(invocation, context, CancellationToken.None);

        Assert.Equal("manifest-tampered", result.Failure?.Code);
    }

    private static (InMemorySkillHost Host, SkillManifest Manifest, SkillExecutionContext Context) CreateHost()
    {
        var manifest = new SkillManifest(
            "open-loops",
            "1",
            "Open Loops",
            [
                new(
                    "open-loops.propose-task",
                    "1",
                    "Propose a task without writing it.",
                    [new("title", ToolValueKind.Text, true)],
                    ["tasks.propose"],
                    ["task.capture"]),
            ]);
        var host = new InMemorySkillHost();
        host.Register(
            manifest,
            new Dictionary<string, SkillToolHandler>(StringComparer.Ordinal)
            {
                ["open-loops.propose-task"] = (_, _, _) =>
                    ValueTask.FromResult(new SkillResult(
                        SkillResultStatus.Completed,
                        JsonSerializer.SerializeToElement(new { accepted = true }),
                        null,
                        null)),
            });
        var context = new SkillExecutionContext(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "task.capture",
            new HashSet<string>(["tasks.propose"], StringComparer.Ordinal));
        return (host, manifest, context);
    }

    private static SkillInvocation Invocation(
        SkillManifest manifest,
        SkillExecutionContext context) =>
        new(
            manifest.SkillId,
            manifest.Version,
            "open-loops.propose-task",
            context.TenantId,
            context.Purpose,
            new Dictionary<string, JsonElement>
            {
                ["title"] = JsonSerializer.SerializeToElement("Book dentist"),
            },
            InMemorySkillHost.ComputeManifestDigest(manifest));
}
