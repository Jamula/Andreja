using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Andreja.Modules.Execution;

public static partial class ManifestContract
{
    public static string ComputeDigest<TManifest>(TManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var element = JsonSerializer.SerializeToElement(manifest);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static void Validate(SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateCommon(
            manifest.SchemaVersion,
            "andreja.skill-manifest.v1",
            manifest.SkillId,
            manifest.Version,
            manifest.DisplayName,
            manifest.Description,
            manifest.Publisher,
            manifest.Lifecycle,
            manifest.Permissions,
            manifest.Execution,
            manifest.DataHandling,
            manifest.HelpSupport,
            manifest.Compatibility,
            manifest.Integrity);
        ValidateField(manifest.ChannelDependencies, nameof(manifest.ChannelDependencies));
        RequireValues(manifest.ActivityCategories, nameof(manifest.ActivityCategories));
        if (manifest.Lifecycle.FrameworkStage > 11)
        {
            throw new ArgumentOutOfRangeException(nameof(manifest));
        }

        if (manifest.Tools.Count == 0)
        {
            throw new ArgumentException("A skill manifest must declare at least one tool.");
        }

        var names = manifest.Tools.Select(tool => tool.Name).ToArray();
        RequireUnique(names, nameof(manifest.Tools));
        foreach (var tool in manifest.Tools)
        {
            Require(tool.Name, nameof(tool.Name));
            RequireSemVer(tool.Version, nameof(tool.Version));
            Require(tool.Description, nameof(tool.Description));
            Require(tool.Operation, nameof(tool.Operation));
            Require(tool.DataClass, nameof(tool.DataClass));
            RequireKnown(tool.MaximumDisclosure, nameof(tool.MaximumDisclosure));
            ValidateInputSchema(tool.InputSchema);
            RequireValues(tool.RequiredCapabilities, nameof(tool.RequiredCapabilities));
            RequireValues(tool.AllowedPurposes, nameof(tool.AllowedPurposes));
            RequireSubset(tool.RequiredCapabilities, manifest.Permissions.DeclaredCapabilities, "capability");
            RequireSubset(tool.AllowedPurposes, manifest.Permissions.AllowedPurposes, "purpose");
            if (!manifest.Permissions.DataClasses.Contains(tool.DataClass, StringComparer.Ordinal)
                || tool.MaximumDisclosure > manifest.Permissions.MaximumDisclosure)
            {
                throw new ArgumentException("A tool cannot widen manifest data permissions.");
            }
        }
    }

    public static void Validate(ChannelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateCommon(
            manifest.SchemaVersion,
            "andreja.channel-manifest.v1",
            manifest.ChannelId,
            manifest.Version,
            manifest.DisplayName,
            manifest.Description,
            manifest.Publisher,
            manifest.Lifecycle,
            manifest.Permissions,
            manifest.Execution,
            manifest.DataHandling,
            manifest.HelpSupport,
            manifest.Compatibility,
            manifest.Integrity);
        Require(manifest.Category, nameof(manifest.Category));
        ValidateProvider(manifest.Provider);
        if (manifest.Lifecycle.FrameworkStage > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(manifest));
        }

        if (manifest.Operations.Count == 0)
        {
            throw new ArgumentException("A channel manifest must declare at least one operation.");
        }

        RequireUnique(
            manifest.Operations.Select(operation => operation.Name),
            nameof(manifest.Operations));
        foreach (var operation in manifest.Operations)
        {
            Require(operation.Name, nameof(operation.Name));
            RequireSemVer(operation.Version, nameof(operation.Version));
            Require(operation.Description, nameof(operation.Description));
            Require(operation.Capability, nameof(operation.Capability));
            Require(operation.Operation, nameof(operation.Operation));
            Require(operation.DataClass, nameof(operation.DataClass));
            RequireKnown(operation.MaximumDisclosure, nameof(operation.MaximumDisclosure));
            ValidateInputSchema(operation.InputSchema);
            RequireValues(operation.AllowedPurposes, nameof(operation.AllowedPurposes));
            RequireSubset(
                [operation.Capability],
                manifest.Permissions.DeclaredCapabilities,
                "capability");
            RequireSubset(operation.AllowedPurposes, manifest.Permissions.AllowedPurposes, "purpose");
            if (!manifest.Permissions.DataClasses.Contains(
                    operation.DataClass,
                    StringComparer.Ordinal)
                || operation.MaximumDisclosure > manifest.Permissions.MaximumDisclosure)
            {
                throw new ArgumentException("A channel operation cannot widen manifest data permissions.");
            }
        }
    }

    private static void ValidateCommon(
        string schemaVersion,
        string expectedSchemaVersion,
        string id,
        string version,
        string displayName,
        string description,
        PublisherMetadata publisher,
        LifecycleMetadata lifecycle,
        PermissionMetadata permissions,
        ExecutionMetadata execution,
        DataHandlingMetadata dataHandling,
        HelpSupportMetadata helpSupport,
        CompatibilityMetadata compatibility,
        IntegrityMetadata integrity)
    {
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("The manifest schema version is unsupported.");
        }

        Require(id, nameof(id));
        RequireSemVer(version, nameof(version));
        Require(displayName, nameof(displayName));
        Require(description, nameof(description));
        Require(publisher.PublisherId, nameof(publisher.PublisherId));
        Require(publisher.DisplayName, nameof(publisher.DisplayName));
        if (lifecycle.FrameworkStage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycle));
        }

        Require(lifecycle.Status, nameof(lifecycle.Status));
        ValidateField(lifecycle.DeprecationNotice, nameof(lifecycle.DeprecationNotice));
        ValidateField(lifecycle.ReplacementArtifact, nameof(lifecycle.ReplacementArtifact));
        RequireValues(permissions.DeclaredCapabilities, nameof(permissions.DeclaredCapabilities));
        RequireValues(permissions.AllowedPurposes, nameof(permissions.AllowedPurposes));
        RequireValues(permissions.DataClasses, nameof(permissions.DataClasses));
        RequireKnown(permissions.MaximumDisclosure, nameof(permissions.MaximumDisclosure));
        if (!Enum.IsDefined(execution.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(execution));
        }

        RequireValues(execution.Entrypoints, nameof(execution.Entrypoints));
        ValidateField(execution.NetworkDestinations, nameof(execution.NetworkDestinations));
        ValidateField(execution.RemoteProtocol, nameof(execution.RemoteProtocol));
        ValidateField(dataHandling.SettingsSchema, nameof(dataHandling.SettingsSchema));
        Require(dataHandling.RetentionPolicy, nameof(dataHandling.RetentionPolicy));
        ValidateField(dataHandling.ResourceLimits, nameof(dataHandling.ResourceLimits));
        if (!helpSupport.HelpUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The help URI must be absolute.", nameof(helpSupport));
        }

        Require(helpSupport.SupportRoute, nameof(helpSupport.SupportRoute));
        Require(helpSupport.Owner, nameof(helpSupport.Owner));
        RequireSemVer(compatibility.MinimumPlatformVersion, nameof(compatibility.MinimumPlatformVersion));
        ValidateField(compatibility.MinimumProtocolVersion, nameof(compatibility.MinimumProtocolVersion));
        RequireValues(
            compatibility.SupportedPlatformVersions,
            nameof(compatibility.SupportedPlatformVersions));
        foreach (var supportedVersion in compatibility.SupportedPlatformVersions)
        {
            RequireSemVer(supportedVersion, nameof(compatibility));
        }

        ValidateField(integrity.PackageDigest, nameof(integrity.PackageDigest));
        ValidateField(integrity.Signature, nameof(integrity.Signature));
        ValidateField(integrity.Provenance, nameof(integrity.Provenance));
        ValidateField(integrity.Sbom, nameof(integrity.Sbom));
    }

    private static void ValidateProvider(ChannelProviderMetadata provider)
    {
        ValidateField(provider.Provider, nameof(provider.Provider));
        ValidateField(provider.AccountTypes, nameof(provider.AccountTypes));
        ValidateField(provider.OAuthScopes, nameof(provider.OAuthScopes));
        ValidateField(provider.QueryMode, nameof(provider.QueryMode));
        ValidateField(provider.SyncMode, nameof(provider.SyncMode));
        ValidateField(provider.PublishMode, nameof(provider.PublishMode));
        ValidateField(provider.WebhookSupport, nameof(provider.WebhookSupport));
        ValidateField(provider.ChangeFeedSupport, nameof(provider.ChangeFeedSupport));
        ValidateField(provider.CachePolicy, nameof(provider.CachePolicy));
        ValidateField(provider.CostModel, nameof(provider.CostModel));
        ValidateField(provider.DeliveryTopology, nameof(provider.DeliveryTopology));
    }

    private static void ValidateField<T>(ManifestField<T> field, string name)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Enum.IsDefined(field.Applicability))
        {
            throw new ArgumentOutOfRangeException(name);
        }

        if (field.Applicability == ManifestApplicability.Applicable)
        {
            if (field.Value is null || !string.IsNullOrWhiteSpace(field.Reason))
            {
                throw new ArgumentException(
                    "Applicable metadata requires a value and no non-applicability reason.",
                    name);
            }

            if (field.Value is string text && string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Applicable string metadata cannot be empty.",
                    name);
            }

            if (field.Value is IEnumerable<string> values)
            {
                RequireValues(values, name);
            }
        }
        else if (field.Value is not null || string.IsNullOrWhiteSpace(field.Reason))
        {
            throw new ArgumentException(
                "Non-applicable metadata requires an explicit reason and no value.",
                name);
        }
    }

    private static void ValidateInputSchema(IReadOnlyList<ToolFieldSchema> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        RequireUnique(schema.Select(field => field.Name), nameof(schema));
        foreach (var field in schema)
        {
            Require(field.Name, nameof(schema));
            if (!Enum.IsDefined(field.Kind))
            {
                throw new ArgumentOutOfRangeException(nameof(schema));
            }
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }
    }

    private static void RequireSemVer(string value, string name)
    {
        Require(value, name);
        if (!SemanticVersion().IsMatch(value))
        {
            throw new ArgumentException("A semantic version is required.", name);
        }
    }

    private static void RequireKnown(
        DisclosureLevel level,
        string name)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireValues(IEnumerable<string> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        var materialized = values.ToArray();
        if (materialized.Length == 0 || materialized.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty value is required.", name);
        }

        RequireUnique(materialized, name);
    }

    private static void RequireUnique(IEnumerable<string> values, string name)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException("Duplicate values are not allowed.", name);
        }
    }

    private static void RequireSubset(
        IEnumerable<string> values,
        IReadOnlyList<string> declared,
        string kind)
    {
        if (values.Any(value => !declared.Contains(value, StringComparer.Ordinal)))
        {
            throw new ArgumentException($"An operation uses an undeclared {kind}.");
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
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

    [GeneratedRegex(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersion();
}
