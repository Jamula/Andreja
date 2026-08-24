using Andreja.Platform.Contracts.Semantics;
using System.Text;
using System.Text.Json;

namespace Andreja.Modules.Semantics;

public static class SemanticJsonLdSerializer
{
    public static string Serialize(SemanticExportPackage package, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(package);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Indented = indented }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("@context");
            writer.WriteStartObject();
            writer.WriteString("and", SemanticProfileContract.JsonLdContextIri);
            writer.WriteString("prov", SemanticProfileContract.ProvenanceContextIri);
            writer.WriteString("id", "@id");
            writer.WriteString("type", "@type");
            writer.WriteEndObject();
            writer.WriteString("and:contractVersion", package.ContractVersion);
            writer.WriteString("and:contextVersion", package.JsonLdContextVersion);
            writer.WriteString("and:tenantReference", package.TenantReference);
            writer.WriteString("and:exportedAt", package.ExportedAt);
            writer.WritePropertyName("@graph");
            writer.WriteStartArray();
            foreach (var source in package.Sources.OrderBy(item => item.SourceId.Value))
            {
                WriteSource(writer, source);
            }

            foreach (var assertion in package.Assertions.OrderBy(item => item.AssertionId.Value))
            {
                WriteAssertion(writer, assertion);
            }

            foreach (var tombstone in package.Tombstones.OrderBy(item => item.AssertionId.Value))
            {
                WriteTombstone(writer, tombstone);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSource(Utf8JsonWriter writer, ProvenanceSource source)
    {
        writer.WriteStartObject();
        writer.WriteString("id", Urn(source.SourceId.Value));
        writer.WriteString("type", "and:Source");
        writer.WriteString("and:sourceKind", source.SourceKind);
        writer.WriteString("and:sourceReference", source.SourceReference);
        writer.WriteString("and:sourceDigest", source.SourceDigest);
        writer.WriteNumber("and:version", source.Version);
        writer.WriteString("and:capturedAt", source.CapturedAt);
        WriteIdReference(
            writer,
            "prov:wasAttributedTo",
            Urn(source.ProducerPrincipalId.Value));
        writer.WriteString("and:purpose", source.Purpose);
        writer.WriteString("and:method", source.Method);
        writer.WriteString("and:methodVersion", source.MethodVersion);
        writer.WriteEndObject();
    }

    private static void WriteAssertion(Utf8JsonWriter writer, ProfileAssertion assertion)
    {
        if (!SemanticProfileContract.IsValidPredicate(assertion.Predicate))
        {
            throw new ArgumentException(
                "The assertion predicate is not a safe IRI.",
                nameof(assertion));
        }

        writer.WriteStartObject();
        writer.WriteString("id", Urn(assertion.AssertionId.Value));
        writer.WriteString("type", "and:ProfileAssertion");
        writer.WriteNumber("and:version", assertion.Version);
        WriteIdReference(writer, "and:subject", Urn(assertion.SubjectId.Value));
        WriteIdReference(writer, "and:predicate", assertion.Predicate);
        writer.WritePropertyName("and:value");
        writer.WriteStartObject();
        if (assertion.Value.Kind == SemanticValueKind.NodeReference)
        {
            writer.WriteString("id", Urn(assertion.Value.NodeReference!.Value.Value));
        }
        else
        {
            writer.WriteString("@value", assertion.Value.LexicalValue);
            if (assertion.Value.DataTypeIri is not null)
            {
                writer.WriteString("@type", assertion.Value.DataTypeIri);
            }
            if (assertion.Value.Language is not null)
            {
                writer.WriteString("@language", assertion.Value.Language);
            }
        }
        writer.WriteEndObject();
        writer.WriteString("and:dataClass", assertion.DataClass.ToString());
        writer.WriteString("and:epistemicStatus", assertion.EpistemicStatus.ToString());
        writer.WriteString("and:verificationState", assertion.VerificationState.ToString());
        writer.WriteString("and:reviewState", assertion.ReviewState.ToString());
        writer.WriteString("and:sensitivity", assertion.Handling.Sensitivity.ToString());
        writer.WriteString("and:modelExposure", assertion.Handling.ModelExposure.ToString());
        writer.WriteString("and:sharing", assertion.Handling.Sharing.ToString());
        writer.WriteString("and:recordDigest", assertion.RecordDigest);
        WriteIdReference(
            writer,
            "prov:wasAttributedTo",
            Urn(assertion.CreatedByPrincipalId.Value));
        writer.WritePropertyName("and:purposes");
        writer.WriteStartArray();
        foreach (var purpose in assertion.Handling.AllowedPurposes.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(purpose);
        }
        writer.WriteEndArray();
        writer.WriteString("prov:generatedAtTime", assertion.RecordedAt);
        if (assertion.ObservedAt is { } observedAt)
        {
            writer.WriteString("and:observedAt", observedAt);
        }
        if (assertion.ValidFrom is { } validFrom)
        {
            writer.WriteString("and:validFrom", validFrom);
        }
        if (assertion.ValidTo is { } validTo)
        {
            writer.WriteString("and:validTo", validTo);
        }
        writer.WritePropertyName("and:retention");
        writer.WriteStartObject();
        writer.WriteString("and:export", assertion.Handling.Retention.Export.ToString());
        writer.WriteString("and:delete", assertion.Handling.Retention.Delete.ToString());
        writer.WriteString("and:audit", assertion.Handling.Retention.Audit.ToString());
        if (assertion.Handling.Retention.RetainUntil is { } retainUntil)
        {
            writer.WriteString("and:retainUntil", retainUntil);
        }
        writer.WriteEndObject();
        writer.WritePropertyName("prov:wasDerivedFrom");
        writer.WriteStartArray();
        foreach (var evidence in assertion.Evidence.OrderBy(item => item.SourceId.Value))
        {
            WriteIdReference(writer, Urn(evidence.SourceId.Value));
        }
        foreach (var inputId in assertion.DerivedFromAssertionIds.OrderBy(item => item.Value))
        {
            WriteIdReference(writer, Urn(inputId.Value));
        }
        writer.WriteEndArray();
        WriteLineage(
            writer,
            "and:corrects",
            assertion.Lineage.CorrectsAssertionId);
        WriteLineage(
            writer,
            "and:supersedes",
            assertion.Lineage.SupersedesAssertionId);
        WriteLineage(
            writer,
            "and:retracts",
            assertion.Lineage.RetractsAssertionId);
        if (assertion.Confidence is { } confidence)
        {
            writer.WritePropertyName("and:confidence");
            writer.WriteStartObject();
            writer.WriteNumber("and:value", confidence.Value);
            writer.WriteString("and:method", confidence.Method);
            writer.WriteString("and:methodVersion", confidence.MethodVersion);
            writer.WriteString("and:explanation", confidence.Explanation);
            writer.WriteEndObject();
        }
        foreach (var extension in assertion.Extensions.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(extension.Key);
            extension.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static void WriteTombstone(
        Utf8JsonWriter writer,
        SemanticAssertionTombstone tombstone)
    {
        writer.WriteStartObject();
        writer.WriteString("id", Urn(tombstone.AssertionId.Value));
        writer.WriteString("type", "and:DeletedAssertion");
        writer.WriteNumber("and:lastVersion", tombstone.LastVersion);
        writer.WriteString("and:lastRecordDigest", tombstone.LastRecordDigest);
        writer.WriteString("and:deletedAt", tombstone.DeletedAt);
        writer.WriteEndObject();
    }

    private static string Urn(Guid value) => $"urn:uuid:{value:D}";

    private static void WriteLineage(
        Utf8JsonWriter writer,
        string propertyName,
        ProfileAssertionId? assertionId)
    {
        if (assertionId is { } value)
        {
            WriteIdReference(writer, propertyName, Urn(value.Value));
        }
    }

    private static void WriteIdReference(
        Utf8JsonWriter writer,
        string propertyName,
        string id)
    {
        writer.WritePropertyName(propertyName);
        WriteIdReference(writer, id);
    }

    private static void WriteIdReference(Utf8JsonWriter writer, string id)
    {
        writer.WriteStartObject();
        writer.WriteString("id", id);
        writer.WriteEndObject();
    }
}
