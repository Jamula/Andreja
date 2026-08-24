using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Proposals;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.UnitTests;

public sealed class ProposalLifecycleTests
{
    [Fact]
    public async Task ConfirmationRetryIsIdempotentAndAuditedOncePerAttempt()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.True(await store.TryCreateAsync(proposal, CancellationToken.None));
        var request = Request(proposal, ProposalAction.Confirm, "confirm-1", proposal.CreatedAt.AddMinutes(1));

        var first = await store.TryTransitionAsync(request, CancellationToken.None);
        var retry = await store.TryTransitionAsync(
            request with { OccurredAt = request.OccurredAt.AddSeconds(5) },
            CancellationToken.None);

        Assert.Equal(ProposalTransitionOutcome.Applied, first.Outcome);
        Assert.Equal(ProposalState.Confirmed, first.Proposal?.State);
        Assert.Equal(ProposalTransitionOutcome.IdempotentReplay, retry.Outcome);
        Assert.Single(store.AuditEntries);
        Assert.Equal(proposal.ActorId, store.AuditEntries[0].ActorId);
        Assert.Equal(proposal.Source.Reference, store.AuditEntries[0].SourceReference);
    }

    [Fact]
    public async Task ExpiredProposalCannotBeConfirmed()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.True(await store.TryCreateAsync(proposal, CancellationToken.None));

        var result = await store.TryTransitionAsync(
            Request(proposal, ProposalAction.Confirm, "late", proposal.ExpiresAt),
            CancellationToken.None);

        Assert.Equal(ProposalTransitionOutcome.Expired, result.Outcome);
        Assert.Equal(ProposalState.Expired, result.Proposal?.State);
    }

    [Fact]
    public async Task ConcurrentTransitionsApplyAtMostOnce()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.True(await store.TryCreateAsync(proposal, CancellationToken.None));

        var attempts = Enumerable.Range(0, 8)
            .Select(index => store.TryTransitionAsync(
                Request(
                    proposal,
                    ProposalAction.Confirm,
                    $"confirm-{index}",
                    proposal.CreatedAt.AddMinutes(1)),
                CancellationToken.None).AsTask());
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.Outcome == ProposalTransitionOutcome.Applied);
        Assert.All(
            results.Where(result => result.Outcome != ProposalTransitionOutcome.Applied),
            result => Assert.Equal(ProposalTransitionOutcome.Conflict, result.Outcome));
    }

    [Theory]
    [InlineData(ProposalAction.Reject, ProposalState.Rejected)]
    [InlineData(ProposalAction.Cancel, ProposalState.Cancelled)]
    public async Task TerminalActionsPreserveExactOperation(
        ProposalAction action,
        ProposalState expectedState)
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.True(await store.TryCreateAsync(proposal, CancellationToken.None));

        var result = await store.TryTransitionAsync(
            Request(proposal, action, action.ToString(), proposal.CreatedAt.AddMinutes(1)),
            CancellationToken.None);

        Assert.Equal(expectedState, result.Proposal?.State);
        Assert.Equal(proposal.Operation, result.Proposal?.Operation);
        Assert.Equal(proposal.Diff, result.Proposal?.Diff);
    }

    private static Proposal CreateProposal()
    {
        var created = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var actor = Guid.CreateVersion7();
        const string canonical = """{"title":"Book dentist"}""";
        return new(
            Guid.CreateVersion7(),
            1,
            Guid.CreateVersion7(),
            actor,
            "task.capture",
            new("assistant", "session-7", actor),
            new(
                "open-loops.create-task",
                "tasks/new",
                canonical,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))),
            new(
                "{}",
                canonical),
            created,
            created.AddMinutes(5),
            ProposalState.Pending);
    }

    private static ProposalTransitionRequest Request(
        Proposal proposal,
        ProposalAction action,
        string idempotencyKey,
        DateTimeOffset occurredAt) =>
        new(
            proposal.ProposalId,
            proposal.Version,
            proposal.TenantId,
            proposal.ActorId,
            action,
            idempotencyKey,
            occurredAt);
}
