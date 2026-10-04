using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Proposals;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.UnitTests;

[TestClass]
public sealed class ProposalLifecycleTests
{
    [TestMethod]
    public async Task ConfirmationRetryIsIdempotentAndAuditedOnceForAppliedEffect()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.IsTrue(await store.TryCreateAsync(proposal, CancellationToken.None));
        var request = Request(proposal, ProposalAction.Confirm, "confirm-1", proposal.CreatedAt.AddMinutes(1));

        var first = await store.TryTransitionAsync(request, CancellationToken.None);
        var retry = await store.TryTransitionAsync(
            request with { OccurredAt = request.OccurredAt.AddSeconds(5) },
            CancellationToken.None);

        Assert.AreEqual(ProposalTransitionOutcome.Applied, first.Outcome);
        Assert.AreEqual(ProposalState.Confirmed, first.Proposal?.State);
        Assert.AreEqual(ProposalTransitionOutcome.IdempotentReplay, retry.Outcome);
        Assert.ContainsSingle(store.AuditEntries);
        Assert.AreEqual(proposal.ActorId, store.AuditEntries[0].ActorId);
        Assert.AreEqual(proposal.Source.Reference, store.AuditEntries[0].SourceReference);
    }

    [TestMethod]
    public async Task ExpiredProposalCannotBeConfirmed()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.IsTrue(await store.TryCreateAsync(proposal, CancellationToken.None));

        var result = await store.TryTransitionAsync(
            Request(proposal, ProposalAction.Confirm, "late", proposal.ExpiresAt),
            CancellationToken.None);

        Assert.AreEqual(ProposalTransitionOutcome.Expired, result.Outcome);
        Assert.AreEqual(ProposalState.Expired, result.Proposal?.State);
    }

    public static IEnumerable<(string scenario, ProposalTransitionOutcome expectedOutcome)> NegativeReplayCases =>
    [
        ("wrong-actor", ProposalTransitionOutcome.Denied),
        ("wrong-tenant", ProposalTransitionOutcome.Denied),
        ("not-found", ProposalTransitionOutcome.NotFound),
        ("expired", ProposalTransitionOutcome.Expired),
        ("conflict", ProposalTransitionOutcome.Conflict),
        ("invalid-state", ProposalTransitionOutcome.InvalidState),
    ];

    [TestMethod]
    [DynamicData(nameof(NegativeReplayCases))]
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
            Assert.IsTrue(await store.TryCreateAsync(proposal, CancellationToken.None));
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

        Assert.AreEqual(expectedOutcome, first.Outcome);
        Assert.AreEqual(expectedOutcome, retry.Outcome);
        Assert.AreNotEqual(ProposalTransitionOutcome.IdempotentReplay, retry.Outcome);
        Assert.AreEqual(first.Proposal, retry.Proposal);
        Assert.AreEqual(stateAfterFirst, stateAfterRetry);
        Assert.AreEqual(auditCountAfterFirst, store.AuditEntries.Count);
    }

    [TestMethod]
    public async Task ConcurrentTransitionsApplyAtMostOnce()
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.IsTrue(await store.TryCreateAsync(proposal, CancellationToken.None));

        var attempts = Enumerable.Range(0, 8)
            .Select(index => store.TryTransitionAsync(
                Request(
                    proposal,
                    ProposalAction.Confirm,
                    $"confirm-{index}",
                    proposal.CreatedAt.AddMinutes(1)),
                CancellationToken.None).AsTask());
        var results = await Task.WhenAll(attempts);

        Assert.AreEqual(
            1,
            results.Count(result => result.Outcome == ProposalTransitionOutcome.Applied));
        foreach (var result in results.Where(
                     result => result.Outcome != ProposalTransitionOutcome.Applied))
        {
            Assert.AreEqual(ProposalTransitionOutcome.Conflict, result.Outcome);
        }
    }

    [TestMethod]
    [DataRow(ProposalAction.Reject, ProposalState.Rejected)]
    [DataRow(ProposalAction.Cancel, ProposalState.Cancelled)]
    public async Task TerminalActionsPreserveExactOperation(
        ProposalAction action,
        ProposalState expectedState)
    {
        var store = new InMemoryProposalStore();
        var proposal = CreateProposal();
        Assert.IsTrue(await store.TryCreateAsync(proposal, CancellationToken.None));

        var result = await store.TryTransitionAsync(
            Request(proposal, action, action.ToString(), proposal.CreatedAt.AddMinutes(1)),
            CancellationToken.None);

        Assert.AreEqual(expectedState, result.Proposal?.State);
        Assert.AreEqual(proposal.Operation, result.Proposal?.Operation);
        Assert.AreEqual(proposal.Diff, result.Proposal?.Diff);
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
        Assert.AreEqual(ProposalTransitionOutcome.Applied, applied.Outcome);
        return request with { ExpectedVersion = applied.Proposal!.Version };
    }
}
