using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Andreja.Platform.Contracts.Portability;

public static class ApplicationExportContract
{
    public const string ArchiveVersion = "1";
    public const string ChecksumAlgorithm = "SHA-256";

    public static readonly IReadOnlySet<PortableDataArea> RequiredDataAreas =
        new HashSet<PortableDataArea>(Enum.GetValues<PortableDataArea>());

    public static readonly IReadOnlySet<string> RequiredExclusions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "credentials",
            "passkeys",
            "recovery-secrets",
            "provider-tokens",
            "data-protection-keys",
            "caches",
            "sensitive-inferences",
        };

    public static readonly IReadOnlyDictionary<PortableDataArea, string>
        RequiredContractVersions = new Dictionary<PortableDataArea, string>
        {
            [PortableDataArea.Semantic] = "1.0",
            [PortableDataArea.Provenance] = "1.0",
        };
}

public sealed record ApplicationExportManifest
{
    public required string ArchiveVersion { get; init; }

    public required string SchemaVersion { get; init; }

    public required string ApplicationVersion { get; init; }

    public required Guid ExportId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string TenantReference { get; init; }

    public required IReadOnlyList<PortableArtifactDescriptor> Artifacts { get; init; }

    public required IReadOnlyList<ExportExclusion> Exclusions { get; init; }

    public required IReadOnlyList<ReauthorizationRequirement> Reauthorization { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<PortableDataArea>))]
public enum PortableDataArea
{
    Records,
    Attachments,
    Grants,
    Audit,
    Settings,
    Semantic,
    Provenance,
}

public sealed record PortableArtifactDescriptor
{
    public required PortableDataArea DataArea { get; init; }

    public required string ContractVersion { get; init; }

    public required string Path { get; init; }

    public required string Sha256 { get; init; }

    public required long ByteLength { get; init; }

    public required long RecordCount { get; init; }
}

public sealed record ExportExclusion
{
    public required string Code { get; init; }

    public required string Reason { get; init; }
}

public sealed record ReauthorizationRequirement
{
    public required string ProviderReference { get; init; }

    public required string Action { get; init; }
}

public sealed record ApplicationImportValidationResult(
    bool IsValid,
    bool TargetIsClean,
    IReadOnlyList<string> Errors);

public interface ICleanInstanceImportProbe
{
    ValueTask<bool> IsCleanAsync(CancellationToken cancellationToken = default);
}

public static class ApplicationExportVerifier
{
    public static async ValueTask<ApplicationImportValidationResult> ValidateAsync(
        ApplicationExportManifest manifest,
        ICleanInstanceImportProbe cleanInstanceProbe,
        Func<string, CancellationToken, ValueTask<Stream>> openArtifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(cleanInstanceProbe);
        ArgumentNullException.ThrowIfNull(openArtifact);

        var errors = new List<string>();
        var targetIsClean = await cleanInstanceProbe.IsCleanAsync(cancellationToken);
        if (!targetIsClean)
        {
            errors.Add("target-not-clean");
        }

        if (!string.Equals(
            manifest.ArchiveVersion,
            ApplicationExportContract.ArchiveVersion,
            StringComparison.Ordinal))
        {
            errors.Add("unsupported-archive-version");
        }

        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            errors.Add("missing-schema-version");
        }
        if (string.IsNullOrWhiteSpace(manifest.ApplicationVersion))
        {
            errors.Add("missing-application-version");
        }
        if (manifest.ExportId == Guid.Empty)
        {
            errors.Add("missing-export-id");
        }
        if (manifest.CreatedAtUtc == default)
        {
            errors.Add("missing-created-at");
        }
        if (string.IsNullOrWhiteSpace(manifest.TenantReference))
        {
            errors.Add("missing-tenant-reference");
        }

        var dataAreas = manifest.Artifacts.Select(artifact => artifact.DataArea).ToHashSet();
        if (!ApplicationExportContract.RequiredDataAreas.IsSubsetOf(dataAreas))
        {
            errors.Add("missing-data-area");
        }

        var exclusions = manifest.Exclusions.Select(exclusion => exclusion.Code).ToHashSet(
            StringComparer.Ordinal);
        if (!ApplicationExportContract.RequiredExclusions.IsSubsetOf(exclusions))
        {
            errors.Add("missing-required-exclusion");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in manifest.Artifacts)
        {
            if (!IsSafeRelativePath(artifact.Path) || !paths.Add(artifact.Path))
            {
                errors.Add($"invalid-artifact-path:{artifact.Path}");
                continue;
            }

            if (!IsSha256(artifact.Sha256) || artifact.ByteLength < 0 || artifact.RecordCount < 0)
            {
                errors.Add($"invalid-artifact-metadata:{artifact.Path}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(artifact.ContractVersion)
                || (ApplicationExportContract.RequiredContractVersions.TryGetValue(
                    artifact.DataArea,
                    out var requiredVersion)
                    && !string.Equals(
                        artifact.ContractVersion,
                        requiredVersion,
                        StringComparison.Ordinal)))
            {
                errors.Add($"unsupported-artifact-contract-version:{artifact.Path}");
                continue;
            }

            await using var content = await openArtifact(artifact.Path, cancellationToken);
            var (checksum, byteLength) = await ComputeSha256Async(content, cancellationToken);
            if (!string.Equals(checksum, artifact.Sha256, StringComparison.Ordinal))
            {
                errors.Add($"checksum-mismatch:{artifact.Path}");
            }

            if (byteLength != artifact.ByteLength)
            {
                errors.Add($"length-mismatch:{artifact.Path}");
            }
        }

        return new ApplicationImportValidationResult(errors.Count == 0, targetIsClean, errors);
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith('/') ||
            path.Contains('\\') ||
            path.Contains(':'))
        {
            return false;
        }

        return path.Split('/').All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");
    }

    private static bool IsSha256(string? checksum) =>
        checksum?.Length == 64 && checksum.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async ValueTask<(string Checksum, long ByteLength)> ComputeSha256Async(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long byteLength = 0;
        int bytesRead;
        while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hasher.AppendData(buffer, 0, bytesRead);
            byteLength += bytesRead;
        }

        return (
            Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant(),
            byteLength);
    }
}
