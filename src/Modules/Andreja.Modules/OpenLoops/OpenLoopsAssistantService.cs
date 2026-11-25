using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;

namespace Andreja.Modules.OpenLoops;

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
                cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return new(null, null, "skill-unavailable");
            }

            var sessionId = Guid.CreateVersion7();
            var session = await assistantProvider.CreateSessionAsync(
                new(
                    sessionId,
                    new(
                        context.TenantId.Value,
                        context.AppUserId.Value,
                        context.PrincipalId.Value,
                        OpenLoopsPolicy.Purpose),
                    manifest.Tools),
                cancellationToken).ConfigureAwait(false);
            AssistantResponse response;
            await using (session.ConfigureAwait(false))
            {
                response = await session.CompleteAsync(
                    new(
                        Guid.CreateVersion7(),
                        message.Trim(),
                        [OpenLoopsSkill.ProposeTaskTool]),
                    cancellationToken).ConfigureAwait(false);
            }

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
                cancellationToken).ConfigureAwait(false);

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
