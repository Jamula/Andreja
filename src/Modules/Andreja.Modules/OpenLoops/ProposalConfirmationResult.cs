using Andreja.Platform.Contracts.Proposals;

namespace Andreja.Modules.OpenLoops;

public sealed record ProposalConfirmationResult(
    ProposalTransitionOutcome Outcome,
    OpenLoopTask? Task,
    Proposal? Proposal);
