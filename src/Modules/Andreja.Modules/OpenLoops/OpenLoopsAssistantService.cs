using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Proposals;
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
                OpenLoopsPolicy.Purpose,
                call.Arguments,
                Modules.Skills.InMemorySkillHost.ComputeManifestDigest(manifest));
            var skillResult = await skillHost.InvokeAsync(
                invocation,
                new(
                    context.TenantId.Value,
                    context.PrincipalId.Value,
                    OpenLoopsPolicy.Purpose,
                    new HashSet<string>(
                        [OpenLoopsPolicy.ProposeCapability],
                        StringComparer.Ordinal)),
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

    public static readonly SkillManifest Manifest = new(
        "open-loops",
        "1",
        "Open Loops",
        [
            new(
                ProposeTaskTool,
                "1",
                "Propose a task for user review without writing it.",
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
                        new(context.PrincipalId),
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
