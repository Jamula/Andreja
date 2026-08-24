using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Proposals;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.Modules.OpenLoops;

public sealed record AssistantTaskResult(
    Proposal? Proposal,
    string? Message,
    string? ErrorCode);

public sealed class OpenLoopsAssistantService(
    IAssistantProvider assistantProvider,
    ISkillHost skillHost)
{
    public async Task<AssistantTaskResult> ProposeTaskAsync(
        TenantPrincipalContext context,
        string message,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.Trim().Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Maximum length is 500.");
        }

        try
        {
            var manifest = await skillHost.ResolveManifestAsync(
                OpenLoopsSkill.Manifest.SkillId,
                OpenLoopsSkill.Manifest.Version,
                cancellationToken);
            if (manifest is null)
            {
                return new(null, null, "skill-unavailable");
            }

            var sessionId = Guid.CreateVersion7();
            await using var session = await assistantProvider.CreateSessionAsync(
                new(
                    sessionId,
                    new(
                        context.TenantId.Value,
                        context.AppUserId.Value,
                        context.PrincipalId.Value,
                        OpenLoopsPolicy.Purpose),
                    manifest.Tools),
                cancellationToken);
            var response = await session.CompleteAsync(
                new(
                    Guid.CreateVersion7(),
                    message.Trim(),
                    [OpenLoopsSkill.ProposeTaskTool]),
                cancellationToken);

            if (response.Status == AssistantResponseStatus.Cancelled)
            {
                return new(null, null, "cancelled");
            }

            if (response.Status == AssistantResponseStatus.Failed)
            {
                return new(
                    null,
                    null,
                    response.Failure?.IsTransient == true
                        ? "provider-temporarily-unavailable"
                        : "provider-failed");
            }

            var call = response.ToolCalls.SingleOrDefault(tool =>
                string.Equals(tool.ToolName, OpenLoopsSkill.ProposeTaskTool, StringComparison.Ordinal));
            if (call is null || response.ToolCalls.Count != 1)
            {
                return new(null, response.Content, "provider-did-not-propose-task");
            }

            var invocation = new SkillInvocation(
                manifest.SkillId,
                manifest.Version,
                call.ToolName,
                context.TenantId.Value,
                context.AppUserId.Value,
                context.PrincipalId.Value,
                OpenLoopsPolicy.Purpose,
                OpenLoopsPolicy.ProposeOperation,
                OpenLoopsPolicy.TaskDataClass,
                DisclosureLevel.Summary,
                OpenLoopsPolicy.ResourceReference,
                call.Arguments,
                Modules.Skills.InMemorySkillHost.ComputeManifestDigest(manifest));
            var authorization = OpenLoopsSkill.CreateAuthorization(context);
            var skillResult = await skillHost.InvokeAsync(
                invocation,
                new(
                    context.TenantId.Value,
                    context.AppUserId.Value,
                    context.PrincipalId.Value,
                    OpenLoopsPolicy.Purpose,
                    authorization),
                cancellationToken);

            return skillResult.Status switch
            {
                SkillResultStatus.Proposed when skillResult.Proposal is not null =>
                    new(skillResult.Proposal, response.Content, null),
                SkillResultStatus.Cancelled => new(null, null, "cancelled"),
                SkillResultStatus.Denied => new(null, null, "not-authorized"),
                SkillResultStatus.Invalid => new(null, null, "invalid-task"),
                _ => new(null, null, "skill-failed"),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(null, null, "cancelled");
        }
    }
}

public static class OpenLoopsSkill
{
    public const string ProposeTaskTool = "open-loops.propose-task";
    private static readonly Guid PlatformPrincipalId =
        Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976101");

    public static readonly SkillManifest Manifest = new(
        "andreja.skill-manifest.v1",
        "open-loops",
        "1.0.0",
        "Open Loops",
        "Capture a reviewable task proposal without writing until user confirmation.",
        ["Productivity", "Calendar"],
        new("andreja.first-party", "Andreja"),
        new(
            5,
            "Implementation",
            ManifestField.NotApplicable<string>("This active first-party skill is not deprecated."),
            ManifestField.NotApplicable<string>("No replacement is required for an active skill.")),
        new(
            [OpenLoopsPolicy.ProposeCapability],
            [OpenLoopsPolicy.Purpose],
            [OpenLoopsPolicy.TaskDataClass],
            DisclosureLevel.Summary),
        new(
            ManifestExecutionMode.FirstPartyInProcess,
            [ProposeTaskTool],
            ManifestField.NotApplicable<IReadOnlyList<string>>(
                "The in-process first-party skill has no network destinations."),
            ManifestField.NotApplicable<string>(
                "The in-process first-party skill uses no remote execution protocol.")),
        new(
            ManifestField.NotApplicable<string>(
                "The Phase 1A Open Loops skill has no skill-specific settings schema."),
            "The skill retains no content; proposal and task stores own their explicit retention.",
            ManifestField.NotApplicable<string>(
                "First-party in-process resource limits are enforced by the application host.")),
        new(
            new("https://github.com/Jamula/Andreja/blob/main/docs/development.md"),
            "GitHub issue #75",
            "Seven of Nine"),
        new(
            "1.0.0",
            ManifestField.NotApplicable<string>(
                "The Phase 1A local skill does not use a federation protocol."),
            ["1.0.0"]),
        new(
            ManifestField.NotApplicable<string>(
                "The built-in skill ships in the application artifact, not a separate package."),
            ManifestField.NotApplicable<string>(
                "The built-in skill is covered by the signed application release."),
            ManifestField.NotApplicable<string>(
                "Repository and application release provenance cover the built-in source."),
            ManifestField.NotApplicable<string>(
                "The built-in skill has no separate package SBOM.")),
        ManifestField.NotApplicable<IReadOnlyList<string>>(
            "The Phase 1A Open Loops proposal path has no channel dependency."),
        [
            new(
                ProposeTaskTool,
                "1.0.0",
                "Propose a task for user review without writing it.",
                OpenLoopsPolicy.ProposeOperation,
                OpenLoopsPolicy.TaskDataClass,
                DisclosureLevel.Summary,
                [
                    new("title", ToolValueKind.Text, true),
                    new("details", ToolValueKind.Text, false),
                    new("dueAt", ToolValueKind.Text, false),
                ],
                [OpenLoopsPolicy.ProposeCapability],
                [OpenLoopsPolicy.Purpose]),
        ]);

    public static ISkillHost CreateHost(OpenLoopsTaskApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var host = new Modules.Skills.InMemorySkillHost();
        host.Register(
            Manifest,
            new Dictionary<string, SkillToolHandler>(StringComparer.Ordinal)
            {
                [ProposeTaskTool] = async (invocation, context, cancellationToken) =>
                {
                    if (context.PrincipalId == Guid.Empty)
                    {
                        return new(
                            SkillResultStatus.Denied,
                            null,
                            null,
                            new("principal-required", "A principal is required."));
                    }

                    var title = invocation.Arguments["title"].GetString();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return new(
                            SkillResultStatus.Invalid,
                            null,
                            null,
                            new("invalid-title", "A task title is required."));
                    }

                    DateTimeOffset? dueAt = null;
                    if (invocation.Arguments.TryGetValue("dueAt", out var dueValue)
                        && !string.IsNullOrWhiteSpace(dueValue.GetString()))
                    {
                        if (!DateTimeOffset.TryParse(
                                dueValue.GetString(),
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.RoundtripKind,
                                out var parsedDueAt))
                        {
                            return new(
                                SkillResultStatus.Invalid,
                                null,
                                null,
                                new("invalid-due-date", "The due date is invalid."));
                        }

                        dueAt = parsedDueAt;
                    }

                    var details = invocation.Arguments.TryGetValue("details", out var detailsValue)
                        ? detailsValue.GetString()
                        : null;
                    var identityContext = new TenantPrincipalContext(
                        new(context.TenantId),
                        new(context.AppUserId),
                        new(context.PrincipalId),
                        context.Purpose);
                    Proposal proposal;
                    try
                    {
                        proposal = await application.ProposeAsync(
                            identityContext,
                            new(title, details, dueAt),
                            $"assistant:{Guid.CreateVersion7():D}",
                            cancellationToken);
                    }
                    catch (ArgumentException)
                    {
                        return new(
                            SkillResultStatus.Invalid,
                            null,
                            null,
                            new("invalid-task", "The proposed task is invalid."));
                    }

                    return new(SkillResultStatus.Proposed, null, proposal, null);
                },
            });
        return host;
    }

    internal static ExecutionAuthorizationContext CreateAuthorization(
        TenantPrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var grantId = Guid.CreateVersion7();
        var consentId = Guid.CreateVersion7();
        var policy = new UserExecutionPolicy(
            Guid.CreateVersion7(),
            "1",
            context.TenantId.Value,
            context.AppUserId.Value,
            context.PrincipalId.Value,
            new HashSet<string>([OpenLoopsPolicy.Purpose], StringComparer.Ordinal),
            new HashSet<string>([OpenLoopsPolicy.ProposeCapability], StringComparer.Ordinal),
            new HashSet<string>([OpenLoopsPolicy.ProposeOperation], StringComparer.Ordinal),
            new HashSet<string>([OpenLoopsPolicy.TaskDataClass], StringComparer.Ordinal),
            DisclosureLevel.Summary,
            now.AddMinutes(-1),
            now.AddMinutes(10),
            false,
            null);
        var consent = new ConsentRecord(
            consentId,
            "1",
            grantId,
            PlatformPrincipalId,
            context.PrincipalId.Value,
            new(
                OpenLoopsPolicy.Purpose,
                DisclosureLevel.Summary,
                new HashSet<string>([OpenLoopsPolicy.ProposeOperation], StringComparer.Ordinal),
                now.AddMinutes(-1),
                now.AddMinutes(10)),
            [
                new(ConsentState.Offered, PlatformPrincipalId, now.AddMinutes(-3)),
                new(ConsentState.Accepted, context.PrincipalId.Value, now.AddMinutes(-2)),
                new(ConsentState.Active, PlatformPrincipalId, now.AddMinutes(-1)),
            ]);
        var grant = new Grant(
            grantId,
            "1",
            context.TenantId.Value,
            OpenLoopsPolicy.ResourceReference,
            OpenLoopsPolicy.TaskDataClass,
            context.PrincipalId.Value,
            OpenLoopsPolicy.Purpose,
            DisclosureLevel.Summary,
            new HashSet<string>([OpenLoopsPolicy.ProposeOperation], StringComparer.Ordinal),
            now.AddMinutes(-1),
            now.AddMinutes(10),
            false,
            null,
            consentId);
        return new(policy, grant, consent, now);
    }

    public static IAssistantProvider CreateDeterministicProvider() =>
        new Modules.Assistant.DeterministicAssistantProvider(
            new(
                "deterministic",
                "open-loops-v1",
                AssistantCapability.TextCompletion
                    | AssistantCapability.TypedTools
                    | AssistantCapability.UsageReporting
                    | AssistantCapability.Cancellation),
            static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["title"] = JsonSerializer.SerializeToElement(request.Content.Trim()),
                };
                return ValueTask.FromResult(new AssistantResponse(
                    request.RequestId,
                    AssistantResponseStatus.Completed,
                    "I prepared a task proposal. Review the exact change before confirming.",
                    [new(ProposeTaskTool, arguments)],
                    new(
                        "deterministic",
                        "open-loops-v1",
                        null,
                        null,
                        TimeSpan.Zero,
                        "proposed",
                        0,
                        1),
                    null));
            });
}
