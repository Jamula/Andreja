using Andreja.Platform.Contracts.Portability;

namespace Andreja.Adapters.PostgreSql;

public sealed record ApplicationImportReport(
    Guid ExportId,
    string TenantReference,
    bool DryRun,
    bool IdempotentReplay,
    IReadOnlyDictionary<PortableDataArea, long> Counts,
    IReadOnlyList<ExportExclusion> Exclusions,
    IReadOnlyList<ReauthorizationRequirement> Reauthorization);
