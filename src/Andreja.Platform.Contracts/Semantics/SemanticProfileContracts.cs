using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andreja.Platform.Contracts.Semantics;

public static class SemanticProfileContract
{
    public const string Version = "1.0";
    public const string JsonLdContextVersion = "1.0";
    public const string JsonLdContextIri = "https://andreja.invalid/ns/v1#";
    public const string ProvenanceContextIri = "http://www.w3.org/ns/prov#";

    public static bool IsAllowedPurpose(string? purpose) =>
        string.Equals(purpose, "task-management", StringComparison.Ordinal);

    public static bool IsValidPredicate(string? predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate)
            || predicate.Any(character =>
                char.IsWhiteSpace(character)
                || char.IsControl(character)
                || character == '\\'))
        {
            return false;
        }

        const string corePrefix = "and:";
        if (predicate.StartsWith(corePrefix, StringComparison.Ordinal))
        {
            var term = predicate.AsSpan(corePrefix.Length);
            return term.Length > 0
                && IsAsciiLetter(term[0])
                && term[1..].ToString().All(IsAllowedTermCharacter);
        }

        if (!Uri.TryCreate(predicate, UriKind.Absolute, out var iri)
            || !string.Equals(predicate, iri.OriginalString, StringComparison.Ordinal)
            || !iri.IsWellFormedOriginalString())
        {
            return false;
        }

        if (string.Equals(iri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(iri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(iri.Host)
                && string.IsNullOrEmpty(iri.UserInfo);
        }

        return string.Equals(iri.Scheme, "urn", StringComparison.Ordinal)
            && predicate.Length > "urn:".Length;
    }

    private static bool IsAsciiLetter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAllowedTermCharacter(char character) =>
        IsAsciiLetter(character)
        || character is >= '0' and <= '9'
        || character is '.' or '_' or '-';
}

public readonly record struct SemanticTenantId(Guid Value)
{
    public static SemanticTenantId New() => new(Guid.CreateVersion7());
}

public readonly record struct SemanticAppUserId(Guid Value)
{
    public static SemanticAppUserId New() => new(Guid.CreateVersion7());
}

public readonly record struct SemanticPrincipalId(Guid Value)
{
    public static SemanticPrincipalId New() => new(Guid.CreateVersion7());
}

public readonly record struct SemanticSubjectId(Guid Value)
{
    public static SemanticSubjectId New() => new(Guid.CreateVersion7());
}

public readonly record struct ProfileAssertionId(Guid Value)
{
    public static ProfileAssertionId New() => new(Guid.CreateVersion7());
}

public readonly record struct ProvenanceSourceId(Guid Value)
{
    public static ProvenanceSourceId New() => new(Guid.CreateVersion7());
}

public readonly record struct SemanticAuditId(Guid Value)
;

[JsonConverter(typeof(JsonStringEnumConverter<SemanticDataClass>))]
public enum SemanticDataClass
{
    Profile,
    Preference,
    Relationship,
    Task,
    Contact,
}

[JsonConverter(typeof(JsonStringEnumConverter<SemanticValueKind>))]
public enum SemanticValueKind
{
    NodeReference,
    Text,
    Boolean,
    WholeNumber,
    FractionalNumber,
    Date,
    DateTime,
    Interval,
    Structured,
}

[JsonConverter(typeof(JsonStringEnumConverter<EpistemicStatus>))]
public enum EpistemicStatus
{
    Observed,
    UserStated,
    Inferred,
}

[JsonConverter(typeof(JsonStringEnumConverter<VerificationState>))]
public enum VerificationState
{
    Unverified,
    UserStated,
    Verified,
    Disputed,
}

[JsonConverter(typeof(JsonStringEnumConverter<AssertionReviewState>))]
public enum AssertionReviewState
{
    PendingUserReview,
    UserApproved,
    UserRejected,
}

[JsonConverter(typeof(JsonStringEnumConverter<SensitivityClass>))]
public enum SensitivityClass
{
    Ordinary,
    Personal,
    Sensitive,
    HighlyRestricted,
}

[JsonConverter(typeof(JsonStringEnumConverter<SemanticExposureLevel>))]
public enum SemanticExposureLevel
{
    Denied = 0,
    Summary = 1,
    Full = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExportDisposition>))]
public enum ExportDisposition
{
    Include,
    ExcludeByDefault,
    Never,
}

[JsonConverter(typeof(JsonStringEnumConverter<DeleteDisposition>))]
public enum DeleteDisposition
{
    HardDelete,
    Tombstone,
}

[JsonConverter(typeof(JsonStringEnumConverter<AuditDisposition>))]
public enum AuditDisposition
{
    ContentMinimized,
}

[JsonConverter(typeof(JsonStringEnumConverter<AssertionLifecycleAction>))]
public enum AssertionLifecycleAction
{
    Created,
    Corrected,
    Superseded,
    Retracted,
    Deleted,
    Exported,
    AccessDenied,
}

[JsonConverter(typeof(JsonStringEnumConverter<AssertionLifecycleState>))]
public enum AssertionLifecycleState
{
    Active,
    Superseded,
    Retracted,
    Deleted,
}

[JsonConverter(typeof(JsonStringEnumConverter<SemanticUseKind>))]
public enum SemanticUseKind
{
    OwnerRead,
    Model,
    Share,
    Export,
    Delete,
}

[JsonConverter(typeof(JsonStringEnumConverter<SemanticEvaluationOutcome>))]
public enum SemanticEvaluationOutcome
{
    Allowed,
    Denied,
    NotFound,
    Conflict,
    Invalid,
}

public sealed record SemanticValue(
    SemanticValueKind Kind,
    string? LexicalValue,
    string? DataTypeIri,
    string? Language,
    SemanticSubjectId? NodeReference);

public sealed record AssertionConfidence(
    decimal Value,
    string Method,
    string MethodVersion,
    string Explanation);

public sealed record ProvenanceSource(
    string ContractVersion,
    ProvenanceSourceId SourceId,
    long Version,
    SemanticTenantId TenantId,
    SemanticAppUserId AppUserId,
    SemanticPrincipalId PrincipalId,
    string SourceKind,
    string SourceReference,
    string SourceDigest,
    DateTimeOffset CapturedAt,
    SemanticPrincipalId ProducerPrincipalId,
    string Purpose,
    string Method,
    string MethodVersion);

public sealed record AssertionEvidence(
    ProvenanceSourceId SourceId,
    string SourceDigest,
    string Role);

public sealed record AssertionLineage(
    ProfileAssertionId? CorrectsAssertionId,
    ProfileAssertionId? SupersedesAssertionId,
    ProfileAssertionId? RetractsAssertionId);

public sealed record SemanticRetentionPolicy(
    ExportDisposition Export,
    DeleteDisposition Delete,
    AuditDisposition Audit,
    DateTimeOffset? RetainUntil);

public sealed record SemanticHandlingPolicy(
    SensitivityClass Sensitivity,
    SemanticExposureLevel ModelExposure,
    SemanticExposureLevel Sharing,
    ImmutableArray<string> AllowedPurposes,
    SemanticRetentionPolicy Retention)
{
    public static SemanticHandlingPolicy PrivateByDefault(
        SensitivityClass sensitivity,
        IEnumerable<string> allowedPurposes,
        SemanticRetentionPolicy retention) =>
        new(
            sensitivity,
            SemanticExposureLevel.Denied,
            SemanticExposureLevel.Denied,
            [.. allowedPurposes],
            retention);
}

public sealed record ProfileAssertion(
    string ContractVersion,
    ProfileAssertionId AssertionId,
    long Version,
    SemanticTenantId TenantId,
    SemanticAppUserId AppUserId,
    SemanticPrincipalId PrincipalId,
    SemanticPrincipalId CreatedByPrincipalId,
    SemanticSubjectId SubjectId,
    string Predicate,
    SemanticValue Value,
    SemanticDataClass DataClass,
    EpistemicStatus EpistemicStatus,
    VerificationState VerificationState,
    AssertionReviewState ReviewState,
    AssertionConfidence? Confidence,
    ImmutableArray<AssertionEvidence> Evidence,
    ImmutableArray<ProfileAssertionId> DerivedFromAssertionIds,
    SemanticHandlingPolicy Handling,
    DateTimeOffset RecordedAt,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    AssertionLineage Lineage,
    string RecordDigest,
    ImmutableDictionary<string, JsonElement> Extensions);

public sealed record SemanticRequestContext(
    SemanticTenantId TenantId,
    SemanticAppUserId AppUserId,
    SemanticPrincipalId PrincipalId,
    string Purpose);

public sealed record SemanticUseRequest(
    SemanticRequestContext Context,
    ProfileAssertionId AssertionId,
    long ExpectedVersion,
    SemanticUseKind Use,
    SemanticExposureLevel RequestedExposure,
    DateTimeOffset OccurredAt,
    bool IncludeSensitiveInference = false);

public sealed record SemanticEvaluationResult(
    SemanticEvaluationOutcome Outcome,
    SemanticExposureLevel EffectiveExposure,
    ProfileAssertion? Assertion,
    string Reason);

public sealed record AssertionChangeRequest(
    SemanticRequestContext Context,
    ProfileAssertionId AssertionId,
    long ExpectedVersion,
    DateTimeOffset OccurredAt,
    string Reason);

public sealed record SemanticAuditEntry(
    SemanticAuditId AuditId,
    string ContractVersion,
    ProfileAssertionId AssertionId,
    long AssertionVersion,
    SemanticTenantId TenantId,
    SemanticAppUserId AppUserId,
    SemanticPrincipalId PrincipalId,
    AssertionLifecycleAction Action,
    SemanticEvaluationOutcome Outcome,
    string Purpose,
    string ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record SemanticAssertionTombstone(
    string ContractVersion,
    ProfileAssertionId AssertionId,
    SemanticTenantId TenantId,
    SemanticAppUserId AppUserId,
    SemanticPrincipalId PrincipalId,
    long LastVersion,
    string LastRecordDigest,
    DateTimeOffset DeletedAt);

public sealed record SemanticExportOptions(bool IncludeSensitiveInferences = false);

public sealed record SemanticExportPackage(
    string ContractVersion,
    string JsonLdContextVersion,
    string TenantReference,
    DateTimeOffset ExportedAt,
    ImmutableArray<ProfileAssertion> Assertions,
    ImmutableArray<ProvenanceSource> Sources,
    ImmutableArray<SemanticAssertionTombstone> Tombstones,
    ImmutableArray<SemanticAuditEntry> Audit);
