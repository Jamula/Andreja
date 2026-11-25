using Andreja.Platform.Contracts.Portability;

namespace Andreja.Adapters.PostgreSql;

public sealed record ApplicationExportResult(
    Guid ExportId,
    string TenantReference,
    IReadOnlyDictionary<PortableDataArea, long> Counts,
    long ArchiveBytes);
