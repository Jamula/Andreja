using Andreja.Modules.Identity;
using Andreja.Platform.Contracts.Proposals;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.Modules.OpenLoops;

public static class OpenLoopsPolicy
{
    public const string Purpose = "task.capture";
    public const string ProposeCapability = "tasks.propose";
    public const string ProposeOperation = "propose";
    public const string TaskDataClass = "tasks";
    public const string ResourceReference = "open-loops/tasks";
    public const string ManageCapability = "tasks.manage";

    public static void Require(TenantPrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId.Value == Guid.Empty
            || context.AppUserId.Value == Guid.Empty
            || context.PrincipalId.Value == Guid.Empty
            || !string.Equals(context.Purpose, Purpose, StringComparison.Ordinal))
        {
            throw new IdentityAccessDeniedException("Task access is not authorized.");
        }
    }
}

public sealed record ProposedTaskInput(
    string Title,
    string? Details,
    DateTimeOffset? DueAt);

public sealed record TaskExport(
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OpenLoopTask> Tasks,
    IReadOnlyList<string> Exclusions);

public sealed record ProposalConfirmationResult(
    ProposalTransitionOutcome Outcome,
    OpenLoopTask? Task,
    Proposal? Proposal);

public interface IOpenLoopsProposalConfirmationStore
{
    Task<ProposalConfirmationResult> ConfirmAsync(
        TenantPrincipalContext context,
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}

public sealed class OpenLoopsTaskApplication(
    IOpenLoopsTaskStore taskStore,
    IProposalStore proposalStore,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Proposal> ProposeAsync(
        TenantPrincipalContext context,
        ProposedTaskInput input,
        string sourceReference,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);

        var now = timeProvider.GetUtcNow();
        var task = new OpenLoopTask(
            Guid.CreateVersion7(),
            context.TenantId,
            context.PrincipalId,
            input.Title,
            input.Details,
            input.DueAt,
            "assistant",
            sourceReference,
            now);
        var payload = new ProposedTaskPayload(
            task.Id,
            task.Title,
            task.Details,
            task.DueAt,
            task.SourceKind,
            task.SourceReference,
            task.CreatedAt);
        var canonical = JsonSerializer.Serialize(payload, CanonicalJson);
        var proposal = new Proposal(
            Guid.CreateVersion7(),
            1,
            context.TenantId.Value,
            context.PrincipalId.Value,
            OpenLoopsPolicy.Purpose,
            new("assistant", sourceReference, context.PrincipalId.Value),
            new(
                "open-loops.create-task",
                $"tasks/{task.Id:D}",
                canonical,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))),
            new("{}", canonical),
            now,
            now.AddMinutes(10),
            ProposalState.Pending);

        if (!await proposalStore.TryCreateAsync(proposal, cancellationToken))
        {
            throw new InvalidOperationException("The proposal could not be created.");
        }

        return proposal;
    }

    public async Task<Proposal?> GetProposalAsync(
        TenantPrincipalContext context,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        var proposal = await proposalStore.GetAsync(
            context.TenantId.Value,
            proposalId,
            cancellationToken);
        return proposal?.ActorId == context.PrincipalId.Value ? proposal : null;
    }

    public async Task<ProposalConfirmationResult> ConfirmAsync(
        TenantPrincipalContext context,
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        ValidateIdempotencyKey(idempotencyKey);
        var occurredAt = timeProvider.GetUtcNow();

        if (proposalStore is IOpenLoopsProposalConfirmationStore atomicStore)
        {
            return await atomicStore.ConfirmAsync(
                context,
                proposalId,
                expectedVersion,
                idempotencyKey,
                occurredAt,
                cancellationToken);
        }

        var transition = await proposalStore.TryTransitionAsync(
            new(
                proposalId,
                expectedVersion,
                context.TenantId.Value,
                context.PrincipalId.Value,
                ProposalAction.Confirm,
                idempotencyKey,
                occurredAt),
            cancellationToken);

        if (transition.Outcome is not (
                ProposalTransitionOutcome.Applied
                or ProposalTransitionOutcome.IdempotentReplay)
            || transition.Proposal is null)
        {
            return new(transition.Outcome, null, transition.Proposal);
        }

        var task = MaterializeTask(context, transition.Proposal);
        var mutation = await taskStore.CreateAsync(
            context,
            task,
            proposalId,
            idempotencyKey,
            cancellationToken);

        return mutation.Outcome switch
        {
            TaskMutationOutcome.Applied or TaskMutationOutcome.IdempotentReplay =>
                new(transition.Outcome, mutation.Task, transition.Proposal),
            TaskMutationOutcome.Denied =>
                new(ProposalTransitionOutcome.Denied, null, transition.Proposal),
            TaskMutationOutcome.Conflict =>
                new(ProposalTransitionOutcome.Conflict, mutation.Task, transition.Proposal),
            _ => new(ProposalTransitionOutcome.NotFound, null, transition.Proposal),
        };
    }

    public Task<IReadOnlyList<OpenLoopTask>> ListAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        return taskStore.ListAsync(context, cancellationToken);
    }

    public Task<TaskMutationResult> CompleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        ValidateIdempotencyKey(idempotencyKey);
        return taskStore.CompleteAsync(
            context,
            taskId,
            expectedVersion,
            idempotencyKey,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<TaskMutationResult> DeleteAsync(
        TenantPrincipalContext context,
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        ValidateIdempotencyKey(idempotencyKey);
        return taskStore.DeleteAsync(
            context,
            taskId,
            expectedVersion,
            idempotencyKey,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<TaskExport> ExportAsync(
        TenantPrincipalContext context,
        CancellationToken cancellationToken = default)
    {
        OpenLoopsPolicy.Require(context);
        var tasks = await taskStore.ListAsync(context, cancellationToken);
        return new(
            "andreja.open-loops.tasks.v1",
            timeProvider.GetUtcNow(),
            tasks,
            [
                "credentials",
                "passkeys",
                "recovery-secrets",
                "provider-tokens",
                "data-protection-keys",
                "caches",
            ]);
    }

    public static OpenLoopTask MaterializeTask(
        TenantPrincipalContext context,
        Proposal proposal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.TenantId != context.TenantId.Value
            || proposal.ActorId != context.PrincipalId.Value
            || proposal.Source.ActorId != context.PrincipalId.Value
            || !string.Equals(proposal.Purpose, OpenLoopsPolicy.Purpose, StringComparison.Ordinal)
            || !string.Equals(
                proposal.Operation.Operation,
                "open-loops.create-task",
                StringComparison.Ordinal)
            || !string.Equals(
                proposal.Operation.CanonicalPayload,
                proposal.Diff.AfterCanonical,
                StringComparison.Ordinal))
        {
            throw new IdentityAccessDeniedException("The confirmed proposal is not authorized.");
        }

        var digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(proposal.Operation.CanonicalPayload)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(digest),
                Encoding.ASCII.GetBytes(proposal.Operation.PayloadDigest)))
        {
            throw new InvalidOperationException("The confirmed proposal digest is invalid.");
        }

        ProposedTaskPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<ProposedTaskPayload>(
                    proposal.Operation.CanonicalPayload,
                    CanonicalJson)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The confirmed proposal payload is invalid.");
        }

        var reserialized = JsonSerializer.Serialize(payload, CanonicalJson);
        if (!string.Equals(
                reserialized,
                proposal.Operation.CanonicalPayload,
                StringComparison.Ordinal)
            || !string.Equals(payload.SourceKind, proposal.Source.Kind, StringComparison.Ordinal)
            || !string.Equals(
                payload.SourceReference,
                proposal.Source.Reference,
                StringComparison.Ordinal)
            || payload.CreatedAt != proposal.CreatedAt)
        {
            throw new InvalidOperationException("The confirmed proposal payload is not exact.");
        }

        if (!string.Equals(
                proposal.Operation.ResourceReference,
                $"tasks/{payload.Id:D}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The confirmed proposal resource is invalid.");
        }

        return new OpenLoopTask(
            payload.Id,
            context.TenantId,
            context.PrincipalId,
            payload.Title,
            payload.Details,
            payload.DueAt,
            payload.SourceKind,
            payload.SourceReference,
            payload.CreatedAt);
    }

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
        {
            throw new ArgumentException("An idempotency key between 8 and 128 characters is required.", nameof(key));
        }
    }

    private sealed record ProposedTaskPayload(
        Guid Id,
        string Title,
        string? Details,
        DateTimeOffset? DueAt,
        string SourceKind,
        string SourceReference,
        DateTimeOffset CreatedAt);
}
