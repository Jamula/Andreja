namespace Andreja.AppHost.OpenLoops;

public sealed record DatabaseMigrationApproval
{
    public required string DatabaseName { get; init; }

    public required string BackupPath { get; init; }

    public required string BackupSha256 { get; init; }

    public required string MigrationScriptPath { get; init; }

    public required string MigrationScriptSha256 { get; init; }

    public required IReadOnlyList<string> ApprovedMigrations { get; init; }
}
