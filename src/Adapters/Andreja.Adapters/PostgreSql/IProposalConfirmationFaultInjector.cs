namespace Andreja.Adapters.PostgreSql;

internal interface IProposalConfirmationFaultInjector
{
    ValueTask OnCheckpointAsync(
        ProposalConfirmationCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
