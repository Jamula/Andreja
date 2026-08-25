using Andreja.Platform.Contracts.Portability;
using Npgsql;
using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andreja.Adapters.PostgreSql;

public sealed record ApplicationExportResult(
    Guid ExportId,
    string TenantReference,
    IReadOnlyDictionary<PortableDataArea, long> Counts,
    long ArchiveBytes);

public sealed record ApplicationImportReport(
    Guid ExportId,
    string TenantReference,
    bool DryRun,
    bool IdempotentReplay,
    IReadOnlyDictionary<PortableDataArea, long> Counts,
    IReadOnlyList<ExportExclusion> Exclusions,
    IReadOnlyList<ReauthorizationRequirement> Reauthorization);

internal enum ApplicationImportCheckpoint
{
    SessionLockAcquiredBeforeTransaction,
}

internal interface IApplicationImportFaultInjector
{
    ValueTask OnCheckpointAsync(
        ApplicationImportCheckpoint checkpoint,
        CancellationToken cancellationToken);
}

public static class PostgreSqlApplicationPortability
{
    public const long MaximumEncryptedArchiveBytes = 64L * 1024 * 1024;
    public const long MaximumExpandedArchiveBytes = 128L * 1024 * 1024;
    public const long MaximumArtifactBytes = 32L * 1024 * 1024;
    public const long MaximumRecords = 100_000;
    internal static readonly TimeSpan DefaultImportLockTimeout = TimeSpan.FromSeconds(30);
    private const int MaximumCompressionRatio = 100;
    internal const long ImportAdvisoryLock = 1465007187;
    private const string SchemaVersion = "1.0.0";
    private static readonly byte[] EnvelopeMagic = "ANDREJA1"u8.ToArray();
    private static readonly DateTimeOffset StableZipTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly Dictionary<PortableDataArea, HashSet<string>> AllowedRecordTypes =
        new Dictionary<PortableDataArea, HashSet<string>>
        {
            [PortableDataArea.Records] = Fields(
                "tenant", "appUser", "externalIdentity", "principal", "membership",
                "contact", "task", "taskReceipt"),
            [PortableDataArea.Attachments] = [],
            [PortableDataArea.Grants] = [],
            [PortableDataArea.Audit] = Fields("taskAudit", "proposalAudit"),
            [PortableDataArea.Settings] = Fields("tenantSettings"),
            [PortableDataArea.Semantic] = [],
            [PortableDataArea.Provenance] = Fields("proposal", "proposalReceipt"),
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> RecordProperties =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["tenant"] = Fields("Id", "NormalizedName", "DisplayName", "DataResidency", "Plan", "Status"),
            ["appUser"] = Fields("Id", "DisplayName", "PrimaryExternalIdentityId"),
            ["externalIdentity"] = Fields("Id", "AppUserId", "Issuer", "Subject"),
            ["principal"] = Fields("Id", "TenantId", "AppUserId", "DisplayName"),
            ["membership"] = Fields("Id", "TenantId", "AppUserId", "PrincipalId", "Role", "Status"),
            ["contact"] = Fields("Id", "TenantId", "NormalizedName", "DisplayName", "LinkedPrincipalId"),
            ["task"] = Fields("Id", "Version", "TenantId", "OwnerPrincipalId", "Title", "Details", "DueAt", "Status", "SourceKind", "SourceReference", "CreatedAt", "CompletedAt"),
            ["taskReceipt"] = Fields("TenantId", "ActorId", "IdempotencyKey", "Intent", "Outcome", "TaskId", "TaskVersion"),
            ["taskAudit"] = Fields("Id", "TenantId", "ActorId", "ResourceId", "Operation", "Outcome", "SourceKind", "SourceReference", "OccurredAt"),
            ["proposal"] = Fields("Id", "Version", "TenantId", "ActorId", "ActorAppUserId", "SourceActorId", "Purpose", "SourceKind", "SourceReference", "Operation", "ResourceReference", "CanonicalPayload", "PayloadDigest", "BeforeCanonical", "AfterCanonical", "CreatedAt", "ExpiresAt", "State", "ActiveTaskId"),
            ["proposalAudit"] = Fields("Id", "TenantId", "ActorId", "ProposalId", "ProposalVersion", "Action", "Outcome", "SourceKind", "SourceReference", "OccurredAt"),
            ["proposalReceipt"] = Fields("TenantId", "ActorId", "IdempotencyKey", "Intent", "ProposalId", "ProposalVersion", "Outcome", "TaskId", "TaskVersion"),
            ["tenantSettings"] = Fields("TenantId", "DataResidency", "Plan", "Status"),
        };

    private static readonly ArtifactPlan[] Plans =
    [
        new(PortableDataArea.Records, "records.ndjson",
        [
            Query("tenant", """SELECT jsonb_build_object('type','tenant','value',to_jsonb(t))::text FROM identity.tenants t WHERE "Id"=@tenant ORDER BY "Id" """),
            Query("appUser", """SELECT jsonb_build_object('type','appUser','value',to_jsonb(u))::text FROM identity.app_users u WHERE EXISTS (SELECT 1 FROM identity.memberships m WHERE m."TenantId"=@tenant AND m."AppUserId"=u."Id") ORDER BY "Id" """),
            Query("externalIdentity", """SELECT jsonb_build_object('type','externalIdentity','value',to_jsonb(e))::text FROM identity.external_identities e WHERE EXISTS (SELECT 1 FROM identity.memberships m WHERE m."TenantId"=@tenant AND m."AppUserId"=e."AppUserId") ORDER BY "Id" """),
            Query("principal", """SELECT jsonb_build_object('type','principal','value',to_jsonb(p))::text FROM identity.principals p WHERE "TenantId"=@tenant ORDER BY "Id" """),
            Query("membership", """SELECT jsonb_build_object('type','membership','value',to_jsonb(m))::text FROM identity.memberships m WHERE "TenantId"=@tenant ORDER BY "Id" """),
            Query("contact", """SELECT jsonb_build_object('type','contact','value',to_jsonb(c))::text FROM identity.contacts c WHERE "TenantId"=@tenant ORDER BY "Id" """),
            Query("task", """SELECT jsonb_build_object('type','task','value',to_jsonb(t))::text FROM open_loops.tasks t WHERE "TenantId"=@tenant ORDER BY "Id" """),
            Query("taskReceipt", """SELECT jsonb_build_object('type','taskReceipt','value',to_jsonb(r))::text FROM open_loops.task_receipts r WHERE "TenantId"=@tenant ORDER BY "ActorId","IdempotencyKey" """),
        ]),
        new(PortableDataArea.Attachments, "attachments.ndjson", []),
        new(PortableDataArea.Grants, "grants.ndjson", []),
        new(PortableDataArea.Audit, "audit.ndjson",
        [
            Query("taskAudit", """SELECT jsonb_build_object('type','taskAudit','value',to_jsonb(a))::text FROM open_loops.task_audit a WHERE "TenantId"=@tenant ORDER BY "OccurredAt","Id" """),
            Query("proposalAudit", """SELECT jsonb_build_object('type','proposalAudit','value',to_jsonb(a))::text FROM open_loops.proposal_audit a WHERE "TenantId"=@tenant ORDER BY "OccurredAt","Id" """),
        ]),
        new(PortableDataArea.Settings, "settings.ndjson",
        [
            Query("tenantSettings", """SELECT jsonb_build_object('type','tenantSettings','value',jsonb_build_object('TenantId',"Id",'DataResidency',"DataResidency",'Plan',"Plan",'Status',"Status"))::text FROM identity.tenants WHERE "Id"=@tenant """),
        ]),
        new(PortableDataArea.Semantic, "semantic.ndjson", []),
        new(PortableDataArea.Provenance, "provenance.ndjson",
        [
            Query("proposal", """SELECT jsonb_build_object('type','proposal','value',to_jsonb(p))::text FROM open_loops.proposals p WHERE "TenantId"=@tenant ORDER BY "Id" """),
            Query("proposalReceipt", """SELECT jsonb_build_object('type','proposalReceipt','value',to_jsonb(r))::text FROM open_loops.proposal_receipts r WHERE "TenantId"=@tenant ORDER BY "ActorId","IdempotencyKey" """),
        ]),
    ];

    public static async Task<ApplicationExportResult> ExportAsync(
        string connectionString,
        Guid tenantId,
        string destinationPath,
        ReadOnlyMemory<byte> encryptionKey,
        string applicationVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(connectionString, tenantId, destinationPath, encryptionKey, applicationVersion);
        if (File.Exists(destinationPath))
        {
            throw new IOException("The export destination already exists.");
        }

        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var descriptors = new List<PortableArtifactDescriptor>();
        var providerReferences = new SortedSet<string>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        if (!await TenantExistsAsync(connection, transaction, tenantId, cancellationToken))
        {
            throw new InvalidOperationException("The requested tenant does not exist.");
        }

        foreach (var plan in Plans)
        {
            var content = await ExportArtifactAsync(
                connection,
                transaction,
                tenantId,
                plan,
                providerReferences,
                cancellationToken);
            artifacts.Add(plan.Path, content.Bytes);
            descriptors.Add(new PortableArtifactDescriptor
            {
                DataArea = plan.Area,
                ContractVersion =
                    ApplicationExportContract.RequiredContractVersions.GetValueOrDefault(plan.Area, "1"),
                Path = plan.Path,
                Sha256 = Sha256(content.Bytes),
                ByteLength = content.Bytes.LongLength,
                RecordCount = content.Count,
            });
        }

        await transaction.CommitAsync(cancellationToken);
        var manifest = new ApplicationExportManifest
        {
            ArchiveVersion = ApplicationExportContract.ArchiveVersion,
            SchemaVersion = SchemaVersion,
            ApplicationVersion = applicationVersion,
            ExportId = Guid.CreateVersion7(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TenantReference = TenantReference(tenantId),
            Artifacts = descriptors,
            Exclusions = CreateExclusions(),
            Reauthorization = providerReferences.Select(reference =>
                new ReauthorizationRequirement
                {
                    ProviderReference = reference,
                    Action = "Create a new local credential mapping and reauthorize it after import.",
                }).ToArray(),
        };
        var manifestBytes = Canonicalize(JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
        artifacts.Add("manifest.json", manifestBytes);
        var zipBytes = CreateZip(artifacts);
        if (zipBytes.LongLength > MaximumEncryptedArchiveBytes)
        {
            throw new InvalidDataException("The compressed archive exceeds the configured limit.");
        }

        var envelope = Encrypt(zipBytes, encryptionKey.Span);
        var partialPath = destinationPath + $".partial-{Guid.NewGuid():N}";
        try
        {
            await using (var output = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(envelope, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(partialPath, destinationPath, overwrite: false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(zipBytes);
            CryptographicOperations.ZeroMemory(envelope);
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }

        return new(
            manifest.ExportId,
            manifest.TenantReference,
            descriptors.ToDictionary(item => item.DataArea, item => item.RecordCount),
            new FileInfo(destinationPath).Length);
    }

    public static async Task<ApplicationImportReport> ImportAsync(
        string connectionString,
        string archivePath,
        ReadOnlyMemory<byte> encryptionKey,
        bool commit,
        Guid? approvedExportId,
        CancellationToken cancellationToken = default) =>
        await ImportAsync(
            connectionString,
            archivePath,
            encryptionKey,
            commit,
            approvedExportId,
            DefaultImportLockTimeout,
            faultInjector: null,
            cancellationToken);

    internal static async Task<ApplicationImportReport> ImportAsync(
        string connectionString,
        string archivePath,
        ReadOnlyMemory<byte> encryptionKey,
        bool commit,
        Guid? approvedExportId,
        TimeSpan lockTimeout,
        IApplicationImportFaultInjector? faultInjector,
        CancellationToken cancellationToken = default)
    {
        ValidateArchiveInput(connectionString, archivePath, encryptionKey);
        if (lockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockTimeout),
                "The import lock timeout must be positive.");
        }
        var encrypted = await ReadBoundedFileAsync(
            archivePath,
            MaximumEncryptedArchiveBytes + 64,
            cancellationToken);
        byte[] zipBytes;
        try
        {
            zipBytes = Decrypt(encrypted, encryptionKey.Span);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }

        ArchiveContent archive;
        try
        {
            archive = ReadArchive(zipBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(zipBytes);
        }

        var manifest = ParseManifest(archive.Manifest);
        if (commit && approvedExportId != manifest.ExportId)
        {
            throw new InvalidOperationException(
                "Commit requires --approve-export with the exact dry-run export ID.");
        }

        ValidateArchiveAgainstManifest(archive, manifest);
        var records = ParseRecords(archive, manifest);
        ValidateLineage(records, manifest);
        var manifestDigest = Sha256(archive.Manifest);

        await using var lockConnection = CreateImportLockConnection(connectionString);
        await lockConnection.OpenAsync(cancellationToken);
        var lockAcquired = false;
        Exception? operationException = null;
        try
        {
            await AcquireImportLockAsync(lockConnection, lockTimeout, cancellationToken);
            lockAcquired = true;
            if (faultInjector is not null)
            {
                await faultInjector.OnCheckpointAsync(
                    ApplicationImportCheckpoint.SessionLockAcquiredBeforeTransaction,
                    cancellationToken);
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await EnsureImportMigrationAsync(connection, transaction, cancellationToken);
            var replay = await ReadImportStateAsync(
                connection,
                transaction,
                manifest,
                manifestDigest,
                cancellationToken);
            if (replay)
            {
                await EnsureReplayMatchesAsync(
                    connection,
                    transaction,
                    manifest,
                    records,
                    cancellationToken);
                await transaction.RollbackAsync(cancellationToken);
                return CreateReport(manifest, dryRun: !commit, idempotent: true);
            }

            await EnsureCleanAsync(connection, transaction, cancellationToken);
            if (!commit)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CreateReport(manifest, dryRun: true, idempotent: false);
            }

            await InsertAllAsync(connection, transaction, records, cancellationToken);
            await InsertImportRecordAsync(
                connection,
                transaction,
                manifest,
                manifestDigest,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CreateReport(manifest, dryRun: false, idempotent: false);
        }
        catch (Exception exception)
        {
            operationException = exception;
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                await ReleaseImportLockAsync(
                    lockConnection,
                    throwOnFailure: operationException is null);
            }
        }
    }

    private static async Task<(byte[] Bytes, long Count)> ExportArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        ArtifactPlan plan,
        SortedSet<string> providerReferences,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        long count = 0;
        foreach (var item in plan.Queries)
        {
            await using var command = new NpgsqlCommand(item.Sql, connection, transaction);
            command.Parameters.AddWithValue("tenant", tenantId);
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (++count > MaximumRecords)
                {
                    throw new InvalidDataException("An artifact exceeds the record-count limit.");
                }
                var canonical = Canonicalize(Encoding.UTF8.GetBytes(reader.GetString(0)));
                if (output.Length + canonical.Length + 1 > MaximumArtifactBytes)
                {
                    throw new InvalidDataException("An artifact exceeds the expanded-size limit.");
                }
                await output.WriteAsync(canonical, cancellationToken);
                output.WriteByte((byte)'\n');
                if (item.Type == "externalIdentity")
                {
                    using var document = JsonDocument.Parse(canonical);
                    var issuer = document.RootElement.GetProperty("value").GetProperty("Issuer").GetString();
                    if (Uri.TryCreate(issuer, UriKind.Absolute, out var uri))
                    {
                        providerReferences.Add(uri.GetLeftPart(UriPartial.Authority));
                    }
                }
            }
        }
        return (output.ToArray(), count);
    }

    private static byte[] CreateZip(IReadOnlyDictionary<string, byte[]> files)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(file.Key, CompressionLevel.Optimal);
                entry.LastWriteTime = StableZipTimestamp;
                using var stream = entry.Open();
                stream.Write(file.Value);
            }
        }
        return output.ToArray();
    }

    private static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        var result = new byte[EnvelopeMagic.Length + 12 + 16 + plaintext.Length];
        EnvelopeMagic.CopyTo(result, 0);
        var nonce = result.AsSpan(EnvelopeMagic.Length, 12);
        RandomNumberGenerator.Fill(nonce);
        var tag = result.AsSpan(EnvelopeMagic.Length + 12, 16);
        var ciphertext = result.AsSpan(EnvelopeMagic.Length + 28);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, EnvelopeMagic);
        return result;
    }

    private static byte[] Decrypt(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> key)
    {
        if (envelope.Length < EnvelopeMagic.Length + 28
            || !CryptographicOperations.FixedTimeEquals(
                envelope[..EnvelopeMagic.Length],
                EnvelopeMagic))
        {
            throw new InvalidDataException("The archive envelope is unsupported.");
        }
        var plaintext = new byte[envelope.Length - EnvelopeMagic.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(
                envelope.Slice(EnvelopeMagic.Length, 12),
                envelope[(EnvelopeMagic.Length + 28)..],
                envelope.Slice(EnvelopeMagic.Length + 12, 16),
                plaintext,
                EnvelopeMagic);
            return plaintext;
        }
        catch (CryptographicException exception)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new InvalidDataException("Archive authentication failed.", exception);
        }
    }

    private static ArchiveContent ReadArchive(byte[] zipBytes)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        using var input = new MemoryStream(zipBytes, writable: false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count > Plans.Length + 1)
        {
            throw new InvalidDataException("The archive contains extra entries.");
        }
        foreach (var entry in zip.Entries)
        {
            if (!ApplicationExportVerifier.IsSafeRelativePath(entry.FullName)
                || !files.TryAdd(entry.FullName, []))
            {
                throw new InvalidDataException("The archive contains an invalid or duplicate path.");
            }
            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixMode == 0xA000)
            {
                throw new InvalidDataException("Archive symbolic links are forbidden.");
            }
            if (entry.Length > MaximumArtifactBytes
                || entry.CompressedLength > 0
                    && entry.Length > entry.CompressedLength * MaximumCompressionRatio)
            {
                throw new InvalidDataException("An archive entry exceeds expansion limits.");
            }
            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumExpandedArchiveBytes)
            {
                throw new InvalidDataException("The expanded archive exceeds its limit.");
            }
            using var stream = entry.Open();
            files[entry.FullName] = ReadBounded(stream, MaximumArtifactBytes);
        }
        if (!files.Remove("manifest.json", out var manifest))
        {
            throw new InvalidDataException("The archive has no manifest.");
        }
        return new(manifest, files);
    }

    private static ApplicationExportManifest ParseManifest(byte[] content)
    {
        RejectDuplicateJsonProperties(content);
        if (!Canonicalize(content).AsSpan().SequenceEqual(content))
        {
            throw new InvalidDataException("The manifest is not canonical JSON.");
        }
        var manifest = JsonSerializer.Deserialize<ApplicationExportManifest>(content, JsonOptions)
            ?? throw new InvalidDataException("The manifest is empty.");
        return manifest;
    }

    private static void ValidateArchiveAgainstManifest(
        ArchiveContent archive,
        ApplicationExportManifest manifest)
    {
        var expected = manifest.Artifacts.Select(item => item.Path).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        if (!expected.SetEquals(archive.Artifacts.Keys))
        {
            throw new InvalidDataException("Archive entries do not exactly match the manifest.");
        }
        if (manifest.ExportId == Guid.Empty
            || manifest.CreatedAtUtc == default
            || manifest.CreatedAtUtc.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(manifest.ApplicationVersion)
            || manifest.TenantReference.Length != 64
            || manifest.TenantReference.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            || manifest.Artifacts.Count != Plans.Length
            || manifest.Artifacts.Select(item => item.DataArea).Distinct().Count() != Plans.Length)
        {
            throw new InvalidDataException("The manifest must contain each data area exactly once.");
        }
        if (!string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(
                manifest.ArchiveVersion,
                ApplicationExportContract.ArchiveVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The archive or schema version is unsupported.");
        }
        var exclusions = manifest.Exclusions.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        if (!ApplicationExportContract.RequiredExclusions.SetEquals(exclusions)
            || manifest.Exclusions.Count != exclusions.Count
            || manifest.Exclusions.Any(item =>
                string.IsNullOrWhiteSpace(item.Reason)))
        {
            throw new InvalidDataException("The required exclusion set is incomplete or unsupported.");
        }
        if (manifest.Reauthorization.Any(item =>
                string.IsNullOrWhiteSpace(item.ProviderReference)
                || string.IsNullOrWhiteSpace(item.Action))
            || manifest.Reauthorization.Select(item => item.ProviderReference).Distinct(
                StringComparer.Ordinal).Count() != manifest.Reauthorization.Count)
        {
            throw new InvalidDataException("Reauthorization requirements are invalid.");
        }
        foreach (var descriptor in manifest.Artifacts)
        {
            var plan = Plans.Single(item => item.Area == descriptor.DataArea);
            if (!string.Equals(descriptor.Path, plan.Path, StringComparison.Ordinal)
                || !ApplicationExportVerifier.IsSafeRelativePath(descriptor.Path)
                || !archive.Artifacts.TryGetValue(descriptor.Path, out var content)
                || descriptor.ByteLength != content.LongLength
                || !string.Equals(descriptor.Sha256, Sha256(content), StringComparison.Ordinal)
                || descriptor.RecordCount < 0
                || descriptor.RecordCount > MaximumRecords)
            {
                throw new InvalidDataException($"Artifact validation failed for {descriptor.DataArea}.");
            }
            var requiredVersion =
                ApplicationExportContract.RequiredContractVersions.GetValueOrDefault(
                    descriptor.DataArea,
                    "1");
            if (!string.Equals(descriptor.ContractVersion, requiredVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("An artifact contract version is unsupported.");
            }
        }
    }

    private static Dictionary<string, List<JsonElement>> ParseRecords(
        ArchiveContent archive,
        ApplicationExportManifest manifest)
    {
        var result = RecordProperties.Keys.ToDictionary(
            key => key,
            _ => new List<JsonElement>(),
            StringComparer.Ordinal);
        foreach (var descriptor in manifest.Artifacts)
        {
            var content = archive.Artifacts[descriptor.Path];
            string text;
            try
            {
                text = StrictUtf8.GetString(content);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("An artifact is not valid UTF-8.", exception);
            }
            var lines = text.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries);
            if (lines.LongLength != descriptor.RecordCount)
            {
                throw new InvalidDataException("An artifact record count does not match.");
            }
            foreach (var line in lines)
            {
                var bytes = Encoding.UTF8.GetBytes(line);
                RejectDuplicateJsonProperties(bytes);
                if (!Canonicalize(bytes).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("An artifact record is not canonical JSON.");
                }
                using var document = JsonDocument.Parse(bytes);
                var root = document.RootElement;
                RequireProperties(root, Fields("type", "value"));
                var type = root.GetProperty("type").GetString()
                    ?? throw new InvalidDataException("A record type is missing.");
                if (!RecordProperties.TryGetValue(type, out var fields))
                {
                    throw new InvalidDataException("An archive record type is unsupported.");
                }
                if (!AllowedRecordTypes[descriptor.DataArea].Contains(type))
                {
                    throw new InvalidDataException("An archive record is in the wrong data area.");
                }
                var value = root.GetProperty("value");
                RequireProperties(value, fields);
                result[type].Add(value.Clone());
            }
        }
        return result;
    }

    private static void ValidateLineage(
        Dictionary<string, List<JsonElement>> records,
        ApplicationExportManifest manifest)
    {
        if (records["tenant"].Count != 1)
        {
            throw new InvalidDataException("An export must contain exactly one tenant.");
        }
        var tenantId = records["tenant"][0].GetProperty("Id").GetGuid();
        if (tenantId == Guid.Empty
            || !string.Equals(
                manifest.TenantReference,
                TenantReference(tenantId),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The tenant lineage does not match the manifest.");
        }
        foreach (var values in records.Values)
        {
            foreach (var value in values)
            {
                if (value.TryGetProperty("TenantId", out var candidate)
                    && candidate.GetGuid() != tenantId)
                {
                    throw new InvalidDataException("The archive contains cross-tenant records.");
                }
            }
        }

        var appUsers = records["appUser"].Select(item => item.GetProperty("Id").GetGuid()).ToHashSet();
        var externalIdentities = records["externalIdentity"]
            .Select(item => item.GetProperty("Id").GetGuid())
            .ToHashSet();
        var principals = records["principal"].Select(item => item.GetProperty("Id").GetGuid()).ToHashSet();
        var tasks = records["task"].Select(item => item.GetProperty("Id").GetGuid()).ToHashSet();
        var proposals = records["proposal"].Select(item => item.GetProperty("Id").GetGuid()).ToHashSet();
        RequireUnique(records["appUser"], "Id");
        RequireUnique(records["externalIdentity"], "Id");
        RequireUnique(records["principal"], "Id");
        RequireUnique(records["membership"], "Id");
        RequireUnique(records["contact"], "Id");
        RequireUnique(records["task"], "Id");
        RequireUnique(records["taskAudit"], "Id");
        RequireUnique(records["proposal"], "Id");
        RequireUnique(records["proposalAudit"], "Id");
        foreach (var user in records["appUser"])
        {
            if (OptionalGuid(user, "PrimaryExternalIdentityId") is Guid identityId
                && !externalIdentities.Contains(identityId))
            {
                throw new InvalidDataException("An AppUser has foreign identity lineage.");
            }
        }
        foreach (var identity in records["externalIdentity"])
        {
            if (!appUsers.Contains(identity.GetProperty("AppUserId").GetGuid()))
            {
                throw new InvalidDataException("An external identity has foreign user lineage.");
            }
        }
        foreach (var principal in records["principal"])
        {
            if (!appUsers.Contains(principal.GetProperty("AppUserId").GetGuid()))
            {
                throw new InvalidDataException("A principal has foreign user lineage.");
            }
        }
        foreach (var membership in records["membership"])
        {
            if (!appUsers.Contains(membership.GetProperty("AppUserId").GetGuid())
                || !principals.Contains(membership.GetProperty("PrincipalId").GetGuid()))
            {
                throw new InvalidDataException("A membership has foreign lineage.");
            }
        }
        foreach (var contact in records["contact"])
        {
            if (OptionalGuid(contact, "LinkedPrincipalId") is Guid principalId
                && !principals.Contains(principalId))
            {
                throw new InvalidDataException("A contact has foreign principal lineage.");
            }
        }
        foreach (var task in records["task"])
        {
            if (!principals.Contains(task.GetProperty("OwnerPrincipalId").GetGuid()))
            {
                throw new InvalidDataException("A task has foreign principal lineage.");
            }
        }
        foreach (var receipt in records["taskReceipt"])
        {
            if (!principals.Contains(receipt.GetProperty("ActorId").GetGuid())
                || OptionalGuid(receipt, "TaskId") is Guid taskId && !tasks.Contains(taskId))
            {
                throw new InvalidDataException("A task receipt has foreign lineage.");
            }
        }
        foreach (var audit in records["taskAudit"])
        {
            if (!principals.Contains(audit.GetProperty("ActorId").GetGuid()))
            {
                throw new InvalidDataException("A task audit has foreign principal lineage.");
            }
        }
        foreach (var proposal in records["proposal"])
        {
            var digest = Convert.ToHexString(SHA256.HashData(
                StrictUtf8.GetBytes(proposal.GetProperty("CanonicalPayload").GetString()!)));
            if (!principals.Contains(proposal.GetProperty("ActorId").GetGuid())
                || !principals.Contains(proposal.GetProperty("SourceActorId").GetGuid())
                || !appUsers.Contains(proposal.GetProperty("ActorAppUserId").GetGuid())
                || OptionalGuid(proposal, "ActiveTaskId") is Guid taskId && !tasks.Contains(taskId)
                || !string.Equals(
                    digest,
                    proposal.GetProperty("PayloadDigest").GetString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A proposal has foreign lineage or an invalid digest.");
            }
        }
        foreach (var receipt in records["proposalReceipt"])
        {
            if (!principals.Contains(receipt.GetProperty("ActorId").GetGuid())
                || !proposals.Contains(receipt.GetProperty("ProposalId").GetGuid())
                || OptionalGuid(receipt, "TaskId") is Guid taskId && !tasks.Contains(taskId))
            {
                throw new InvalidDataException("A proposal receipt has foreign lineage.");
            }
        }
        foreach (var audit in records["proposalAudit"])
        {
            if (!principals.Contains(audit.GetProperty("ActorId").GetGuid())
                || !proposals.Contains(audit.GetProperty("ProposalId").GetGuid()))
            {
                throw new InvalidDataException("A proposal audit has foreign lineage.");
            }
        }
        if (records["tenantSettings"].Count != 1
            || records["tenantSettings"][0].GetProperty("TenantId").GetGuid() != tenantId)
        {
            throw new InvalidDataException("Tenant settings lineage is invalid.");
        }
    }

    private static async Task InsertAllAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Dictionary<string, List<JsonElement>> records,
        CancellationToken cancellationToken)
    {
        await InsertAsync(connection, transaction, "identity.tenants", records["tenant"], cancellationToken);
        await ExecuteJsonAsync(
            connection,
            transaction,
            """INSERT INTO identity.app_users ("Id","DisplayName","PrimaryExternalIdentityId") SELECT "Id","DisplayName",NULL FROM jsonb_populate_recordset(NULL::identity.app_users,@json::jsonb)""",
            records["appUser"],
            cancellationToken);
        await InsertAsync(connection, transaction, "identity.external_identities", records["externalIdentity"], cancellationToken);
        await ExecuteJsonAsync(
            connection,
            transaction,
            """UPDATE identity.app_users u SET "PrimaryExternalIdentityId"=v."PrimaryExternalIdentityId" FROM jsonb_populate_recordset(NULL::identity.app_users,@json::jsonb) v WHERE u."Id"=v."Id" """,
            records["appUser"],
            cancellationToken);
        await InsertAsync(connection, transaction, "identity.principals", records["principal"], cancellationToken);
        await InsertAsync(connection, transaction, "identity.memberships", records["membership"], cancellationToken);
        await InsertAsync(connection, transaction, "identity.contacts", records["contact"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.tasks", records["task"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.task_receipts", records["taskReceipt"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.proposals", records["proposal"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.proposal_receipts", records["proposalReceipt"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.task_audit", records["taskAudit"], cancellationToken);
        await InsertAsync(connection, transaction, "open_loops.proposal_audit", records["proposalAudit"], cancellationToken);
    }

    private static async Task EnsureReplayMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ApplicationExportManifest manifest,
        Dictionary<string, List<JsonElement>> records,
        CancellationToken cancellationToken)
    {
        var tenantId = records["tenant"][0].GetProperty("Id").GetGuid();
        var providers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var plan in Plans)
        {
            var actual = await ExportArtifactAsync(
                connection,
                transaction,
                tenantId,
                plan,
                providers,
                cancellationToken);
            var expected = manifest.Artifacts.Single(item => item.DataArea == plan.Area);
            if (actual.Count != expected.RecordCount
                || actual.Bytes.LongLength != expected.ByteLength
                || !string.Equals(Sha256(actual.Bytes), expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The previously imported portable state conflicts with this archive.");
            }
        }
    }

    private static Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        IReadOnlyCollection<JsonElement> values,
        CancellationToken cancellationToken) =>
        ExecuteJsonAsync(
            connection,
            transaction,
            $"INSERT INTO {table} SELECT * FROM jsonb_populate_recordset(NULL::{table},@json::jsonb)",
            values,
            cancellationToken);

    private static async Task ExecuteJsonAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        IReadOnlyCollection<JsonElement> values,
        CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return;
        }
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("json", JsonSerializer.Serialize(values));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCleanAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string tablesSql =
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type='BASE TABLE'
              AND table_schema IN ('identity','open_loops','portability')
              AND table_name <> '__migrations'
              AND NOT (table_schema='portability' AND table_name='application_imports')
            ORDER BY table_schema, table_name
            """;
        var tables = new List<(string Schema, string Table)>();
        await using (var command = new NpgsqlCommand(tablesSql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        foreach (var table in tables)
        {
            var quoted = $"{QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Table)}";
            await using var command = new NpgsqlCommand(
                $"SELECT EXISTS (SELECT 1 FROM {quoted} LIMIT 1)",
                connection,
                transaction);
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                throw new InvalidOperationException("The target database is not provably clean.");
            }
        }
    }

    private static async Task<bool> ReadImportStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ApplicationExportManifest manifest,
        string manifestDigest,
        CancellationToken cancellationToken)
    {
        const string sql =
            """SELECT "ExportId","ManifestSha256","TenantReference" FROM portability.application_imports LIMIT 2""";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }
        var matches = reader.GetGuid(0) == manifest.ExportId
            && string.Equals(reader.GetString(1), manifestDigest, StringComparison.Ordinal)
            && string.Equals(reader.GetString(2), manifest.TenantReference, StringComparison.Ordinal);
        if (!matches || await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("A conflicting application import already exists.");
        }
        return true;
    }

    private static async Task InsertImportRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ApplicationExportManifest manifest,
        string manifestDigest,
        CancellationToken cancellationToken)
    {
        const string sql =
            """INSERT INTO portability.application_imports ("ExportId","ManifestSha256","TenantReference","ImportedAt") VALUES (@id,@digest,@tenant,@at)""";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", manifest.ExportId);
        command.Parameters.AddWithValue("digest", manifestDigest);
        command.Parameters.AddWithValue("tenant", manifest.TenantReference);
        command.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureImportMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('portability.application_imports') IS NOT NULL",
            connection,
            transaction);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new InvalidOperationException(
                "The explicit ApplicationPortability database migration has not been applied.");
        }
    }

    private static NpgsqlConnection CreateImportLockConnection(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            Multiplexing = false,
            Enlist = false,
            ApplicationName = "Andreja application import lock",
        };
        return new NpgsqlConnection(builder.ConnectionString);
    }

    private static async Task AcquireImportLockAsync(
        NpgsqlConnection connection,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(lockTimeout);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_lock(@lockKey)",
            connection)
        {
            CommandTimeout = 0,
        };
        command.Parameters.AddWithValue("lockKey", ImportAdvisoryLock);
        try
        {
            await command.ExecuteNonQueryAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Application import lock acquisition exceeded {lockTimeout}.",
                exception);
        }
        catch (NpgsqlException exception)
            when (timeoutCancellation.IsCancellationRequested
                  && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Application import lock acquisition exceeded {lockTimeout}.",
                exception);
        }
    }

    private static async Task ReleaseImportLockAsync(
        NpgsqlConnection connection,
        bool throwOnFailure)
    {
        Exception? failure = null;
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(@lockKey)",
                connection)
            {
                CommandTimeout = 5,
            };
            command.Parameters.AddWithValue("lockKey", ImportAdvisoryLock);
            if (await command.ExecuteScalarAsync(CancellationToken.None) is not true)
            {
                failure = new InvalidOperationException(
                    "The application import session lock was not held at release.");
            }
        }
        catch (Exception exception) when (
            exception is NpgsqlException or InvalidOperationException)
        {
            failure = exception;
        }
        finally
        {
            await connection.CloseAsync();
        }

        if (failure is not null && throwOnFailure)
        {
            throw new InvalidOperationException(
                "The application import session lock could not be explicitly released.",
                failure);
        }
    }

    private static async Task<bool> TenantExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """SELECT EXISTS (SELECT 1 FROM identity.tenants WHERE "Id"=@tenant)""",
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static ApplicationImportReport CreateReport(
        ApplicationExportManifest manifest,
        bool dryRun,
        bool idempotent) =>
        new(
            manifest.ExportId,
            manifest.TenantReference,
            dryRun,
            idempotent,
            manifest.Artifacts.ToDictionary(item => item.DataArea, item => item.RecordCount),
            manifest.Exclusions,
            manifest.Reauthorization);

    private static ExportExclusion[] CreateExclusions() =>
        ApplicationExportContract.RequiredExclusions
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => new ExportExclusion
            {
                Code = code,
                Reason = "Excluded by the application export v1 security contract.",
            })
            .ToArray();

    private static string TenantReference(Guid tenantId) =>
        Sha256(Encoding.UTF8.GetBytes($"andreja-tenant-v1:{tenantId:D}"));

    private static byte[] Canonicalize(ReadOnlySpan<byte> json)
    {
        using var document = JsonDocument.Parse(json.ToArray());
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return output.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                    item => item.Name,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void RejectDuplicateJsonProperties(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        var propertySets = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                propertySets.Push(new(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.PropertyName
                     && !propertySets.Peek().Add(reader.GetString()!))
            {
                throw new InvalidDataException("Duplicate JSON properties are forbidden.");
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                propertySets.Pop();
            }
        }
    }

    private static void RequireProperties(JsonElement element, HashSet<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(item => item.Name).ToHashSet(
                StringComparer.Ordinal).SetEquals(expected))
        {
            throw new InvalidDataException("An archive record has missing or extra properties.");
        }
    }

    private static HashSet<string> Fields(params string[] names) =>
        new(names, StringComparer.Ordinal);

    private static Guid? OptionalGuid(JsonElement value, string propertyName)
    {
        var property = value.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetGuid();
    }

    private static void RequireUnique(IEnumerable<JsonElement> values, string propertyName)
    {
        var identifiers = values.Select(item => item.GetProperty(propertyName).GetGuid()).ToArray();
        if (identifiers.Any(id => id == Guid.Empty)
            || identifiers.Distinct().Count() != identifiers.Length)
        {
            throw new InvalidDataException("An archive contains missing or duplicate identifiers.");
        }
    }

    private static ArtifactQuery Query(string type, string sql) => new(type, sql);

    private static string Sha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string QuoteIdentifier(string value) =>
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private static byte[] ReadBounded(Stream stream, long maximum)
    {
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (output.Length + read > maximum)
            {
                throw new InvalidDataException("Archive content exceeds its limit.");
            }
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        long maximum,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maximum)
        {
            throw new InvalidDataException("The archive is missing or exceeds its limit.");
        }
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    private static void ValidateInputs(
        string connectionString,
        Guid tenantId,
        string destinationPath,
        ReadOnlyMemory<byte> encryptionKey,
        string applicationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant ID is required.", nameof(tenantId));
        }
        ValidateKey(encryptionKey);
    }

    private static void ValidateArchiveInput(
        string connectionString,
        string archivePath,
        ReadOnlyMemory<byte> encryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ValidateKey(encryptionKey);
    }

    private static void ValidateKey(ReadOnlyMemory<byte> encryptionKey)
    {
        if (encryptionKey.Length != 32)
        {
            throw new ArgumentException("A 256-bit archive encryption key is required.");
        }
    }

    private sealed record ArtifactPlan(
        PortableDataArea Area,
        string Path,
        ArtifactQuery[] Queries);
    private sealed record ArtifactQuery(string Type, string Sql);
    private sealed record ArchiveContent(
        byte[] Manifest,
        Dictionary<string, byte[]> Artifacts);
}
