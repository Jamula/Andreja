namespace Andreja.Adapters.PostgreSql;

internal enum ApplicationImportCheckpoint
{
    SessionLockAcquiredBeforeTransaction,
    TargetTablesLocked,
}
