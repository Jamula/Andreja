using Andreja.Adapters.OpenTelemetry;
using Andreja.AppHost.Hosting;
using Andreja.Platform.Contracts.Portability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class OperationsContractTests
{
    [Fact]
    public void TelemetryProcessorRetainsOnlyAllowedAttributes()
    {
        using var activity = new Activity("request").Start();
        activity.SetTag("http.request.method", "GET");
        activity.SetTag("task", "CANARY-TASK-CONTENT");
        activity.SetTag("user.id", "CANARY-USER");
        activity.SetTag("custom.attribute", "CANARY-UNKNOWN");

        new ContentSuppressingActivityProcessor().OnEnd(activity);

        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.Null(activity.GetTagItem("task"));
        Assert.Null(activity.GetTagItem("user.id"));
        Assert.Null(activity.GetTagItem("custom.attribute"));
    }

    [Fact]
    public async Task ExportVerifierAcceptsCompleteCleanArchive()
    {
        var files = CreateArtifacts();
        var manifest = CreateManifest(files);

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(new MemoryStream(files[path])));

        Assert.True(result.IsValid);
        Assert.True(result.TargetIsClean);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ExportVerifierRejectsDirtyTargetAndMissingExclusions()
    {
        var files = CreateArtifacts();
        var complete = CreateManifest(files);
        var manifest = complete with { Exclusions = [] };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: false),
            (path, _) => ValueTask.FromResult<Stream>(new MemoryStream(files[path])));

        Assert.False(result.IsValid);
        Assert.Contains("target-not-clean", result.Errors);
        Assert.Contains("missing-required-exclusion", result.Errors);
    }

    [Fact]
    public async Task ExportVerifierRejectsTamperingAndNonPortablePaths()
    {
        var files = CreateArtifacts();
        files["records.ndjson"][0] ^= 0xff;
        files["..\\audit.ndjson"] = files["audit.ndjson"];
        var complete = CreateManifest(CreateArtifacts());
        var artifacts = complete.Artifacts
            .Where(artifact => artifact.DataArea != PortableDataArea.Audit)
            .Append(complete.Artifacts.Single(
                artifact => artifact.DataArea == PortableDataArea.Audit) with
            { Path = "..\\audit.ndjson" })
            .ToArray();
        var manifest = complete with { Artifacts = artifacts };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(new MemoryStream(files[path])));

        Assert.False(result.IsValid);
        Assert.Contains("checksum-mismatch:records.ndjson", result.Errors);
        Assert.Contains("invalid-artifact-path:..\\audit.ndjson", result.Errors);
    }

    [Fact]
    public async Task ExportVerifierRejectsUnsupportedSemanticContractVersion()
    {
        var files = CreateArtifacts();
        var complete = CreateManifest(files);
        var semantic = complete.Artifacts.Single(
            artifact => artifact.DataArea == PortableDataArea.Semantic);
        var manifest = complete with
        {
            Artifacts = complete.Artifacts
                .Where(artifact => artifact.DataArea != PortableDataArea.Semantic)
                .Append(semantic with { ContractVersion = "99" })
                .ToArray(),
        };

        var result = await ApplicationExportVerifier.ValidateAsync(
            manifest,
            new CleanInstanceProbe(isClean: true),
            (path, _) => ValueTask.FromResult<Stream>(new MemoryStream(files[path])));

        Assert.False(result.IsValid);
        Assert.Contains(
            $"unsupported-artifact-contract-version:{semantic.Path}",
            result.Errors);
    }

    [Fact]
    public async Task ReadinessRequiresWritableKeyState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"andreja-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new AndrejaOperationsOptions
            {
                DataProtectionKeysPath = path,
                Database = new DatabaseReadinessOptions { Enabled = false },
            });
            var telemetryOptions = Options.Create(new AndrejaTelemetryOptions { Enabled = false });
            var check = new OperationalReadinessHealthCheck(options, telemetryOptions);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public void DeploymentContractsArePinnedAndContentSafe()
    {
        var root = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(root, "compose.yaml"));
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var collector = File.ReadAllText(Path.Combine(root, "deploy", "otel-collector.yaml"));
        var maintenance = File.ReadAllText(
            Path.Combine(root, "deploy", "compose.maintenance.yaml"));
        var migration = File.ReadAllText(
            Path.Combine(root, "deploy", "compose.migration.yaml"));
        var schema = File.ReadAllText(
            Path.Combine(root, "docs", "operations", "application-export-v1.schema.json"));

        Assert.Contains("image: ${ANDREJA_IMAGE:?", compose, StringComparison.Ordinal);
        Assert.Contains(
            "${ANDREJA_PUBLIC_ORIGIN:-https://${ANDREJA_HOSTNAME:-localhost}}",
            compose,
            StringComparison.Ordinal);
        Assert.DoesNotContain("POSTGRES_PASSWORD:", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD_FILE:", compose, StringComparison.Ordinal);
        Assert.Contains("USER $APP_UID", dockerfile, StringComparison.Ordinal);
        Assert.Equal(2, dockerfile.Split("@sha256:", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "org.opencontainers.image.licenses=\"Apache-2.0\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "org.opencontainers.image.licenses=\"MIT\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\n  debug:", collector, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  logging:", collector, StringComparison.Ordinal);
        Assert.Contains("attributes/suppress", collector, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:", maintenance, StringComparison.Ordinal);
        Assert.Contains("migration-approval.json:ro", migration, StringComparison.Ordinal);
        Assert.Contains("backup.dump:ro", migration, StringComparison.Ordinal);
        Assert.Contains("migration.sql:ro", migration, StringComparison.Ordinal);

        var legalGate = File.ReadAllText(
            Path.Combine(root, "docs", "legal", "license-evaluation.md"));
        Assert.Contains(
            "Do not publish releases, packages, containers",
            legalGate,
            StringComparison.Ordinal);

        using var document = JsonDocument.Parse(schema);
        Assert.Equal(
            "1",
            document.RootElement.GetProperty("properties")
                .GetProperty("archiveVersion")
                .GetProperty("const")
                .GetString());
        foreach (var exclusion in ApplicationExportContract.RequiredExclusions)
        {
            Assert.Contains(exclusion, schema, StringComparison.Ordinal);
        }
        Assert.Equal(
            "1.0",
            ApplicationExportContract.RequiredContractVersions[PortableDataArea.Semantic]);
        Assert.Equal(
            "1.0",
            ApplicationExportContract.RequiredContractVersions[PortableDataArea.Provenance]);
    }

    private static Dictionary<string, byte[]> CreateArtifacts() =>
        Enum.GetValues<PortableDataArea>().ToDictionary(
            area => $"{area.ToString().ToLowerInvariant()}.ndjson",
            area => Encoding.UTF8.GetBytes($"{{\"area\":\"{area}\"}}\n"),
            StringComparer.Ordinal);

    private static ApplicationExportManifest CreateManifest(
        Dictionary<string, byte[]> files) =>
        new()
        {
            ArchiveVersion = ApplicationExportContract.ArchiveVersion,
            SchemaVersion = "1.0.0",
            ApplicationVersion = "test",
            ExportId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TenantReference = "tenant-fixture",
            Artifacts = Enum.GetValues<PortableDataArea>().Select(area =>
            {
                var path = $"{area.ToString().ToLowerInvariant()}.ndjson";
                var content = files[path];
                return new PortableArtifactDescriptor
                {
                    DataArea = area,
                    ContractVersion =
                        ApplicationExportContract.RequiredContractVersions.GetValueOrDefault(
                            area,
                            "1"),
                    Path = path,
                    Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                    ByteLength = content.LongLength,
                    RecordCount = 1,
                };
            }).ToArray(),
            Exclusions = ApplicationExportContract.RequiredExclusions.Select(code =>
                new ExportExclusion { Code = code, Reason = "Excluded by v1 contract." }).ToArray(),
            Reauthorization =
            [
                new ReauthorizationRequirement
                {
                    ProviderReference = "fixture-provider",
                    Action = "Reauthorize after import.",
                },
            ],
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class CleanInstanceProbe(bool isClean) : ICleanInstanceImportProbe
    {
        public ValueTask<bool> IsCleanAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(isClean);
    }
}
