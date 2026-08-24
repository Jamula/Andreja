using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Proposals;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.UnitTests;

public sealed class ProposalLifecycleTests
{
    [Fact]
    public async Task ConfirmationRetryIsIdempotentAndAuditedOnceForAppliedEffect()
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

    public static TheoryData<string, ProposalTransitionOutcome> NegativeReplayCases =>
        new()
        {
            { "wrong-actor", ProposalTransitionOutcome.Denied },
            { "wrong-tenant", ProposalTransitionOutcome.Denied },
            { "not-found", ProposalTransitionOutcome.NotFound },
            { "expired", ProposalTransitionOutcome.Expired },
            { "conflict", ProposalTransitionOutcome.Conflict },
            { "invalid-state", ProposalTransitionOutcome.InvalidState },
        };

    [Theory]
    [MemberData(nameof(NegativeReplayCases))]
    public async Task NegativeTransitionRetryPreservesOriginalOutcomeWithoutDuplicateEffects(
        string scenario,
        ProposalTransitionOutcome expectedOutcome)
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        ProposalTransitionRequest request;

        if (scenario == "not-found")
        {
            request = Request(proposal, ProposalAction.Confirm, scenario, proposal.CreatedAt.AddMinutes(1))
                with
            { ProposalId = Guid.CreateVersion7() };
        }
        else
        {
            Assert.True(await store.TryCreateAsync(proposal, CancellationToken.None));
            request = Request(proposal, ProposalAction.Confirm, scenario, proposal.CreatedAt.AddMinutes(1));

            request = scenario switch
            {
                "wrong-actor" => request with { ActorId = Guid.CreateVersion7() },
                "wrong-tenant" => request with { TenantId = Guid.CreateVersion7() },
                "expired" => request with { OccurredAt = proposal.ExpiresAt },
                "conflict" => request with { ExpectedVersion = proposal.Version + 1 },
                "invalid-state" => await CreateInvalidStateRequestAsync(store, proposal, request),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
            };
        }

        var first = await store.TryTransitionAsync(request, CancellationToken.None);
        var stateAfterFirst = await store.GetAsync(
            proposal.TenantId,
            proposal.ProposalId,
            CancellationToken.None);
        var auditCountAfterFirst = store.AuditEntries.Count;

        var retry = await store.TryTransitionAsync(
            request with { OccurredAt = request.OccurredAt.AddSeconds(5) },
            CancellationToken.None);
        var stateAfterRetry = await store.GetAsync(
            proposal.TenantId,
            proposal.ProposalId,
            CancellationToken.None);

        Assert.Equal(expectedOutcome, first.Outcome);
        Assert.Equal(expectedOutcome, retry.Outcome);
        Assert.NotEqual(ProposalTransitionOutcome.IdempotentReplay, retry.Outcome);
        Assert.Equal(first.Proposal, retry.Proposal);
        Assert.Equal(stateAfterFirst, stateAfterRetry);
        Assert.Equal(auditCountAfterFirst, store.AuditEntries.Count);
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

    private static async Task<ProposalTransitionRequest> CreateInvalidStateRequestAsync(
        InMemoryProposalStore store,
        Proposal proposal,
        ProposalTransitionRequest request)
    {
        var applied = await store.TryTransitionAsync(
            Request(
                proposal,
                ProposalAction.Confirm,
                "invalid-state-prerequisite",
                proposal.CreatedAt.AddSeconds(30)),
            CancellationToken.None);
        Assert.Equal(ProposalTransitionOutcome.Applied, applied.Outcome);
        return request with { ExpectedVersion = applied.Proposal!.Version };
    }
}
