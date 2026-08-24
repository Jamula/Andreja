using Andreja.Platform.Contracts.Semantics;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Modules.Semantics;

public static class SemanticRecordDigest
{
    public static string Compute(ProfileAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Add(hash, assertion.ContractVersion);
        Add(hash, assertion.AssertionId.Value);
        Add(hash, assertion.Version);
        Add(hash, assertion.TenantId.Value);
        Add(hash, assertion.AppUserId.Value);
        Add(hash, assertion.PrincipalId.Value);
        Add(hash, assertion.CreatedByPrincipalId.Value);
        Add(hash, assertion.SubjectId.Value);
        Add(hash, assertion.Predicate);
        Add(hash, assertion.Value.Kind);
        Add(hash, assertion.Value.LexicalValue);
        Add(hash, assertion.Value.DataTypeIri);
        Add(hash, assertion.Value.Language);
        Add(hash, assertion.Value.NodeReference?.Value);
        Add(hash, assertion.DataClass);
        Add(hash, assertion.EpistemicStatus);
        Add(hash, assertion.VerificationState);
        Add(hash, assertion.ReviewState);
        Add(hash, assertion.Confidence?.Value);
        Add(hash, assertion.Confidence?.Method);
        Add(hash, assertion.Confidence?.MethodVersion);
        Add(hash, assertion.Confidence?.Explanation);
        foreach (var evidence in assertion.Evidence.OrderBy(item => item.SourceId.Value))
        {
            Add(hash, evidence.SourceId.Value);
            Add(hash, evidence.SourceDigest);
            Add(hash, evidence.Role);
        }
        foreach (var inputId in assertion.DerivedFromAssertionIds.OrderBy(item => item.Value))
        {
            Add(hash, inputId.Value);
        }

        Add(hash, assertion.Handling.Sensitivity);
        Add(hash, assertion.Handling.ModelExposure);
        Add(hash, assertion.Handling.Sharing);
        foreach (var purpose in assertion.Handling.AllowedPurposes.Order(StringComparer.Ordinal))
        {
            Add(hash, purpose);
        }

        Add(hash, assertion.Handling.Retention.Export);
        Add(hash, assertion.Handling.Retention.Delete);
        Add(hash, assertion.Handling.Retention.Audit);
        Add(hash, assertion.Handling.Retention.RetainUntil);
        Add(hash, assertion.RecordedAt);
        Add(hash, assertion.ObservedAt);
        Add(hash, assertion.ValidFrom);
        Add(hash, assertion.ValidTo);
        Add(hash, assertion.Lineage.CorrectsAssertionId?.Value);
        Add(hash, assertion.Lineage.SupersedesAssertionId?.Value);
        Add(hash, assertion.Lineage.RetractsAssertionId?.Value);
        foreach (var extension in assertion.Extensions.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Add(hash, extension.Key);
            Add(hash, extension.Value.GetRawText());
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static ProfileAssertion Seal(ProfileAssertion assertion) =>
        assertion with { RecordDigest = Compute(assertion) };

    private static void Add<T>(IncrementalHash hash, T value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTimeOffset dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
        var bytes = Encoding.UTF8.GetBytes(text);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
