namespace Andreja.Modules.OpenLoops;

public sealed record TaskExport(
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OpenLoopTask> Tasks,
    IReadOnlyList<string> Exclusions);
