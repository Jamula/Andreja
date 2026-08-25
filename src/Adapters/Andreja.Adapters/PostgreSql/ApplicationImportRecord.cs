namespace Andreja.Adapters.PostgreSql;

internal sealed class ApplicationImportRecord
{
    private ApplicationImportRecord()
    {
    }

    public ApplicationImportRecord(
        Guid exportId,
        string manifestSha256,
        string tenantReference,
        DateTimeOffset importedAt)
    {
        ExportId = exportId;
        ManifestSha256 = manifestSha256;
        TenantReference = tenantReference;
        ImportedAt = importedAt;
    }

    public Guid ExportId { get; private set; }

    public string ManifestSha256 { get; private set; } = string.Empty;

    public string TenantReference { get; private set; } = string.Empty;

    public DateTimeOffset ImportedAt { get; private set; }
}
