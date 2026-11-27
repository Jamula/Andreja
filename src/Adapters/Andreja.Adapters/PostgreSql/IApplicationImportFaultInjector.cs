namespace Andreja.Adapters.PostgreSql;

internal interface IApplicationImportFaultInjector
{
    ValueTask OnCheckpointAsync(
        ApplicationImportCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
