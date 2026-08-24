using Andreja.Platform.Contracts.Semantics;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Modules.Semantics;

public sealed class InMemorySemanticAssertionLedger
{
    private readonly object gate = new();
    private readonly Dictionary<ProvenanceSourceId, ProvenanceSource> sources = [];
    private readonly Dictionary<ProfileAssertionId, ProfileAssertion> assertions = [];
    private readonly Dictionary<ProfileAssertionId, AssertionLifecycleState> states = [];
    private readonly Dictionary<ProfileAssertionId, SemanticAssertionTombstone> tombstones = [];
    private readonly List<SemanticAuditEntry> audit = [];

    public IReadOnlyList<SemanticAuditEntry> Audit
    {
        get
        {
            lock (gate)
            {
                return audit.ToArray();
            }
        }
    }

    public void AppendSource(
        SemanticRequestContext context,
        ProvenanceSource source)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        ValidateSource(context, source);
        lock (gate)
        {
            if (!sources.TryAdd(source.SourceId, source))
            {
                throw new InvalidOperationException("Provenance sources are append-only.");
            }
        }
    }

    public SemanticEvaluationResult Append(
        SemanticRequestContext context,
        ProfileAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assertion);
        lock (gate)
        {
            var validation = ValidateAssertion(context, assertion);
            if (validation is not null)
            {
                AppendAudit(
                    context,
                    assertion.AssertionId,
                    assertion.Version,
                    AssertionLifecycleAction.AccessDenied,
                    SemanticEvaluationOutcome.Invalid,
                    validation,
                    assertion.RecordedAt);
                return Denied(SemanticEvaluationOutcome.Invalid, validation);
            }

            if (!assertions.TryAdd(assertion.AssertionId, assertion))
            {
                return Denied(SemanticEvaluationOutcome.Conflict, "assertion-already-exists");
            }

            states.Add(assertion.AssertionId, AssertionLifecycleState.Active);
            AppendAudit(
                context,
                assertion.AssertionId,
                assertion.Version,
                AssertionLifecycleAction.Created,
                SemanticEvaluationOutcome.Allowed,
                "created",
                assertion.RecordedAt);
            return Allowed(assertion, SemanticExposureLevel.Full, "created");
        }
    }

    public SemanticEvaluationResult Correct(
        AssertionChangeRequest request,
        ProfileAssertion correction)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(correction);
        lock (gate)
        {
            var current = AuthorizeChange(request);
            if (current.Outcome != SemanticEvaluationOutcome.Allowed)
            {
                return current;
            }

            if (correction.Lineage.CorrectsAssertionId != request.AssertionId
                || correction.Lineage.SupersedesAssertionId != request.AssertionId
                || correction.Version != 1
                || correction.SubjectId != current.Assertion!.SubjectId
                || !string.Equals(
                    correction.Predicate,
                    current.Assertion.Predicate,
                    StringComparison.Ordinal)
                || correction.DataClass != current.Assertion.DataClass)
            {
                return Denied(SemanticEvaluationOutcome.Invalid, "invalid-correction-lineage");
            }

            var append = Append(request.Context, correction);
            if (append.Outcome != SemanticEvaluationOutcome.Allowed)
            {
                return append;
            }

            states[request.AssertionId] = AssertionLifecycleState.Superseded;
            AppendAudit(
                request.Context,
                request.AssertionId,
                request.ExpectedVersion,
                AssertionLifecycleAction.Corrected,
                SemanticEvaluationOutcome.Allowed,
                "corrected",
                request.OccurredAt);
            AppendAudit(
                request.Context,
                request.AssertionId,
                request.ExpectedVersion,
                AssertionLifecycleAction.Superseded,
                SemanticEvaluationOutcome.Allowed,
                "superseded",
                request.OccurredAt);
            InvalidateDependants(
                request.Context,
                request.AssertionId,
                request.OccurredAt);
            return append;
        }
    }

    public SemanticEvaluationResult Retract(AssertionChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var current = AuthorizeChange(request);
            if (current.Outcome != SemanticEvaluationOutcome.Allowed)
            {
                return current;
            }

            states[request.AssertionId] = AssertionLifecycleState.Retracted;
            InvalidateDependants(
                request.Context,
                request.AssertionId,
                request.OccurredAt);
            AppendAudit(
                request.Context,
                request.AssertionId,
                request.ExpectedVersion,
                AssertionLifecycleAction.Retracted,
                SemanticEvaluationOutcome.Allowed,
                "retracted",
                request.OccurredAt);
            return current with { Reason = "retracted" };
        }
    }

    public SemanticEvaluationResult Delete(AssertionChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var current = AuthorizeChange(request);
            if (current.Outcome != SemanticEvaluationOutcome.Allowed
                || current.Assertion is null)
            {
                return current;
            }

            var assertion = current.Assertion;
            if (assertion.Handling.Retention.Delete == DeleteDisposition.Tombstone)
            {
                var tombstone = new SemanticAssertionTombstone(
                    SemanticProfileContract.Version,
                    assertion.AssertionId,
                    assertion.TenantId,
                    assertion.AppUserId,
                    assertion.PrincipalId,
                    assertion.Version,
                    assertion.RecordDigest,
                    request.OccurredAt);
                tombstones[assertion.AssertionId] = tombstone;
            }
            assertions.Remove(assertion.AssertionId);
            states[assertion.AssertionId] = AssertionLifecycleState.Deleted;
            InvalidateDependants(
                request.Context,
                request.AssertionId,
                request.OccurredAt);
            AppendAudit(
                request.Context,
                request.AssertionId,
                request.ExpectedVersion,
                AssertionLifecycleAction.Deleted,
                SemanticEvaluationOutcome.Allowed,
                "deleted-content-removed",
                request.OccurredAt);
            return new(
                SemanticEvaluationOutcome.Allowed,
                SemanticExposureLevel.Denied,
                null,
                "deleted");
        }
    }

    public SemanticEvaluationResult Evaluate(SemanticUseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            if (!assertions.TryGetValue(request.AssertionId, out var assertion)
                || states[request.AssertionId] != AssertionLifecycleState.Active)
            {
                return Denied(SemanticEvaluationOutcome.NotFound, "assertion-not-active");
            }

            if (!Enum.IsDefined(request.Use)
                || !Enum.IsDefined(request.RequestedExposure))
            {
                return Denied(SemanticEvaluationOutcome.Invalid, "invalid-use-request");
            }

            var scopeFailure = ValidateScope(request.Context, assertion);
            if (scopeFailure is not null)
            {
                AppendAudit(
                    request.Context,
                    request.AssertionId,
                    assertion.Version,
                    AssertionLifecycleAction.AccessDenied,
                    SemanticEvaluationOutcome.Denied,
                    scopeFailure,
                    request.OccurredAt);
                return Denied(SemanticEvaluationOutcome.Denied, scopeFailure);
            }

            if (request.ExpectedVersion != assertion.Version)
            {
                return Denied(SemanticEvaluationOutcome.Conflict, "version-conflict");
            }

            if (!assertion.Handling.AllowedPurposes.Contains(
                request.Context.Purpose,
                StringComparer.Ordinal))
            {
                return Denied(SemanticEvaluationOutcome.Denied, "purpose-denied");
            }

            var maximum = request.Use switch
            {
                SemanticUseKind.OwnerRead or SemanticUseKind.Delete => SemanticExposureLevel.Full,
                SemanticUseKind.Model => assertion.Handling.ModelExposure,
                SemanticUseKind.Share => assertion.Handling.Sharing,
                SemanticUseKind.Export => ExportMaximum(assertion, request.IncludeSensitiveInference),
                _ => SemanticExposureLevel.Denied,
            };
            var effective = (SemanticExposureLevel)Math.Min(
                (int)request.RequestedExposure,
                (int)maximum);
            if (effective == SemanticExposureLevel.Denied)
            {
                return Denied(SemanticEvaluationOutcome.Denied, "exposure-denied");
            }

            return Allowed(assertion, effective, "least-exposure-applied");
        }
    }

    public SemanticExportPackage Export(
        SemanticRequestContext context,
        string tenantReference,
        DateTimeOffset exportedAt,
        SemanticExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantReference);
        options ??= new();

        lock (gate)
        {
            var exportedAssertions = assertions.Values
                .Where(assertion => states[assertion.AssertionId] == AssertionLifecycleState.Active)
                .Where(assertion => ValidateScope(context, assertion) is null)
                .Where(assertion => assertion.Handling.AllowedPurposes.Contains(
                    context.Purpose,
                    StringComparer.Ordinal))
                .Where(assertion =>
                    ExportMaximum(assertion, options.IncludeSensitiveInferences)
                    != SemanticExposureLevel.Denied)
                .OrderBy(assertion => assertion.AssertionId.Value)
                .ToArray();
            var sourceIds = exportedAssertions
                .SelectMany(assertion => assertion.Evidence)
                .Select(item => item.SourceId)
                .ToHashSet();
            var exportedSources = sources.Values
                .Where(source => source.TenantId == context.TenantId)
                .Where(source => sourceIds.Contains(source.SourceId))
                .OrderBy(source => source.SourceId.Value)
                .ToArray();
            var exportedTombstones = tombstones.Values
                .Where(item =>
                    item.TenantId == context.TenantId
                    && item.AppUserId == context.AppUserId
                    && item.PrincipalId == context.PrincipalId)
                .OrderBy(item => item.AssertionId.Value)
                .ToArray();
            var exportedAudit = audit
                .Where(item =>
                    item.TenantId == context.TenantId
                    && item.AppUserId == context.AppUserId
                    && item.PrincipalId == context.PrincipalId)
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.AuditId.Value)
                .ToArray();

            foreach (var assertion in exportedAssertions)
            {
                AppendAudit(
                    context,
                    assertion.AssertionId,
                    assertion.Version,
                    AssertionLifecycleAction.Exported,
                    SemanticEvaluationOutcome.Allowed,
                    "exported",
                    exportedAt);
            }

            return new(
                SemanticProfileContract.Version,
                SemanticProfileContract.JsonLdContextVersion,
                tenantReference,
                exportedAt,
                [.. exportedAssertions],
                [.. exportedSources],
                [.. exportedTombstones],
                [.. exportedAudit]);
        }
    }

    public IReadOnlyList<ProfileAssertion> FindActive(
        SemanticRequestContext context,
        SemanticSubjectId subjectId,
        string predicate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(predicate);
        lock (gate)
        {
            return assertions.Values
                .Where(assertion => assertion.SubjectId == subjectId)
                .Where(assertion => string.Equals(
                    assertion.Predicate,
                    predicate,
                    StringComparison.Ordinal))
                .Where(assertion => states[assertion.AssertionId] == AssertionLifecycleState.Active)
                .Where(assertion => ValidateScope(context, assertion) is null)
                .Where(assertion => assertion.Handling.AllowedPurposes.Contains(
                    context.Purpose,
                    StringComparer.Ordinal))
                .OrderBy(assertion => assertion.AssertionId.Value)
                .ToArray();
        }
    }

    private SemanticEvaluationResult AuthorizeChange(AssertionChangeRequest request)
    {
        if (!assertions.TryGetValue(request.AssertionId, out var assertion)
            || states[request.AssertionId] != AssertionLifecycleState.Active)
        {
            return Denied(SemanticEvaluationOutcome.NotFound, "assertion-not-active");
        }

        var scopeFailure = ValidateScope(request.Context, assertion);
        if (scopeFailure is not null)
        {
            return Denied(SemanticEvaluationOutcome.Denied, scopeFailure);
        }

        if (request.ExpectedVersion != assertion.Version)
        {
            return Denied(SemanticEvaluationOutcome.Conflict, "version-conflict");
        }

        if (!assertion.Handling.AllowedPurposes.Contains(
            request.Context.Purpose,
            StringComparer.Ordinal))
        {
            return Denied(SemanticEvaluationOutcome.Denied, "purpose-denied");
        }

        return Allowed(assertion, SemanticExposureLevel.Full, "authorized");
    }

    private string? ValidateAssertion(
        SemanticRequestContext context,
        ProfileAssertion assertion)
    {
        if (!string.Equals(
            assertion.ContractVersion,
            SemanticProfileContract.Version,
            StringComparison.Ordinal))
        {
            return "unsupported-contract-version";
        }

        if (assertion.AssertionId.Value == Guid.Empty
            || assertion.SubjectId.Value == Guid.Empty
            || assertion.Version != 1
            || assertion.Value is null
            || assertion.Lineage is null
            || assertion.Handling is null
            || assertion.Extensions is null
            || string.IsNullOrWhiteSpace(assertion.Predicate)
            || !Enum.IsDefined(assertion.DataClass)
            || !Enum.IsDefined(assertion.EpistemicStatus)
            || !Enum.IsDefined(assertion.VerificationState)
            || !Enum.IsDefined(assertion.ReviewState)
            || !Enum.IsDefined(assertion.Handling.Sensitivity)
            || !Enum.IsDefined(assertion.Handling.ModelExposure)
            || !Enum.IsDefined(assertion.Handling.Sharing)
            || !Enum.IsDefined(assertion.Handling.Retention.Export)
            || !Enum.IsDefined(assertion.Handling.Retention.Delete)
            || !Enum.IsDefined(assertion.Handling.Retention.Audit)
            || assertion.Handling.Retention.RetainUntil < assertion.RecordedAt
            || assertion.ValidTo <= assertion.ValidFrom)
        {
            return "invalid-assertion";
        }

        var scopeFailure = ValidateScope(context, assertion);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        if (assertion.EpistemicStatus == EpistemicStatus.Inferred
            && (assertion.Confidence is null
                || assertion.ReviewState == AssertionReviewState.UserApproved
                || assertion.VerificationState != VerificationState.Unverified))
        {
            return "inference-must-remain-hypothesis";
        }

        if (assertion.EpistemicStatus == EpistemicStatus.UserStated
            && (assertion.VerificationState != VerificationState.UserStated
                || assertion.ReviewState != AssertionReviewState.UserApproved))
        {
            return "user-statement-not-approved";
        }

        if (assertion.Confidence is { } confidence
            && (confidence.Value is < 0 or > 1
                || string.IsNullOrWhiteSpace(confidence.Method)
                || string.IsNullOrWhiteSpace(confidence.MethodVersion)
                || string.IsNullOrWhiteSpace(confidence.Explanation)))
        {
            return "invalid-confidence";
        }

        if (assertion.Handling.AllowedPurposes.IsDefaultOrEmpty
            || assertion.Handling.AllowedPurposes.Any(string.IsNullOrWhiteSpace)
            || assertion.Handling.AllowedPurposes.Any(purpose => purpose.Length > 128)
            || !assertion.Handling.AllowedPurposes.Contains(
                context.Purpose,
                StringComparer.Ordinal))
        {
            return "purpose-denied";
        }

        if (assertion.EpistemicStatus == EpistemicStatus.Inferred
            && assertion.Handling.Sensitivity >= SensitivityClass.Sensitive
            && (assertion.Handling.ModelExposure != SemanticExposureLevel.Denied
                || assertion.Handling.Sharing != SemanticExposureLevel.Denied))
        {
            return "sensitive-inference-exposure-denied";
        }

        if (!IsValueValid(assertion.Value))
        {
            return "invalid-value";
        }

        if (assertion.Evidence.IsDefaultOrEmpty)
        {
            return "missing-evidence";
        }

        foreach (var evidence in assertion.Evidence)
        {
            if (!sources.TryGetValue(evidence.SourceId, out var source)
                || source.TenantId != assertion.TenantId
                || source.AppUserId != assertion.AppUserId
                || source.PrincipalId != assertion.PrincipalId
                || !string.Equals(
                    source.Purpose,
                    context.Purpose,
                    StringComparison.Ordinal)
                || !string.Equals(
                    source.SourceDigest,
                    evidence.SourceDigest,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(evidence.Role))
            {
                return "invalid-provenance";
            }
        }

        var lineageIds = new[]
        {
            assertion.Lineage.CorrectsAssertionId,
            assertion.Lineage.SupersedesAssertionId,
            assertion.Lineage.RetractsAssertionId,
        };
        foreach (var lineageId in lineageIds.Where(id => id is not null))
        {
            if (!assertions.TryGetValue(lineageId!.Value, out var predecessor)
                || predecessor.TenantId != assertion.TenantId
                || predecessor.AppUserId != assertion.AppUserId
                || predecessor.PrincipalId != assertion.PrincipalId)
            {
                return "invalid-lineage";
            }

            if (assertion.DerivedFromAssertionIds.IsDefault)
            {
                return "invalid-derivation";
            }
            foreach (var inputId in assertion.DerivedFromAssertionIds)
            {
                if (!assertions.TryGetValue(inputId, out var input)
                    || states[inputId] != AssertionLifecycleState.Active
                    || input.TenantId != assertion.TenantId
                    || input.AppUserId != assertion.AppUserId
                    || input.PrincipalId != assertion.PrincipalId)
                {
                    return "invalid-derivation";
                }
            }
        }

        foreach (var extension in assertion.Extensions)
        {
            if (!Uri.TryCreate(extension.Key, UriKind.Absolute, out var extensionIri)
                || !string.Equals(
                    extensionIri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.Ordinal)
                || extension.Key.StartsWith(
                    SemanticProfileContract.JsonLdContextIri,
                    StringComparison.Ordinal)
                || extension.Key.StartsWith(
                    SemanticProfileContract.ProvenanceContextIri,
                    StringComparison.Ordinal)
                || extension.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return "invalid-extension";
            }
        }

        var expectedDigest = SemanticRecordDigest.Compute(assertion);
        if (!string.Equals(expectedDigest, assertion.RecordDigest, StringComparison.Ordinal))
        {
            return "record-digest-mismatch";
        }

        return null;
    }

    private static string? ValidateScope(
        SemanticRequestContext context,
        ProfileAssertion assertion)
    {
        if (context.TenantId != assertion.TenantId)
        {
            return "tenant-denied";
        }

        if (context.AppUserId != assertion.AppUserId)
        {
            return "app-user-denied";
        }

        if (context.PrincipalId != assertion.PrincipalId)
        {
            return "principal-denied";
        }

        return null;
    }

    private static SemanticExposureLevel ExportMaximum(
        ProfileAssertion assertion,
        bool includeSensitiveInference)
    {
        if (assertion.Handling.Retention.Export == ExportDisposition.Never
            || assertion.Handling.Retention.Export == ExportDisposition.ExcludeByDefault
            || (assertion.EpistemicStatus == EpistemicStatus.Inferred
                && assertion.Handling.Sensitivity >= SensitivityClass.Sensitive
                && !includeSensitiveInference))
        {
            return SemanticExposureLevel.Denied;
        }

        return SemanticExposureLevel.Full;
    }

    private static bool IsValueValid(SemanticValue value)
    {
        if (!Enum.IsDefined(value.Kind))
        {
            return false;
        }

        return value.Kind == SemanticValueKind.NodeReference
            ? value.NodeReference is { Value: var id } && id != Guid.Empty
            : value.NodeReference is null && !string.IsNullOrWhiteSpace(value.LexicalValue);
    }

    private static void ValidateSource(
        SemanticRequestContext context,
        ProvenanceSource source)
    {
        if (!string.Equals(
            source.ContractVersion,
            SemanticProfileContract.Version,
            StringComparison.Ordinal)
            || source.SourceId.Value == Guid.Empty
            || source.Version != 1
            || source.TenantId.Value == Guid.Empty
            || source.AppUserId.Value == Guid.Empty
            || source.PrincipalId.Value == Guid.Empty
            || source.ProducerPrincipalId.Value == Guid.Empty
            || source.TenantId != context.TenantId
            || source.AppUserId != context.AppUserId
            || source.PrincipalId != context.PrincipalId
            || string.IsNullOrWhiteSpace(source.SourceKind)
            || string.IsNullOrWhiteSpace(source.SourceReference)
            || source.SourceReference.Length > 256
            || source.SourceReference.Contains('\r')
            || source.SourceReference.Contains('\n')
            || string.IsNullOrWhiteSpace(source.Purpose)
            || !string.Equals(source.Purpose, context.Purpose, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.Method)
            || string.IsNullOrWhiteSpace(source.MethodVersion)
            || !IsSha256(source.SourceDigest))
        {
            throw new ArgumentException("The provenance source is invalid.", nameof(source));
        }
    }

    private static bool IsSha256(string digest) =>
        digest.Length == 64
        && digest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void AppendAudit(
        SemanticRequestContext context,
        ProfileAssertionId assertionId,
        long assertionVersion,
        AssertionLifecycleAction action,
        SemanticEvaluationOutcome outcome,
        string reason,
        DateTimeOffset occurredAt)
    {
        var auditId = new SemanticAuditId(CreateAuditId(
            assertionId,
            assertionVersion,
            action,
            outcome,
            occurredAt,
            audit.Count));
        audit.Add(new(
            auditId,
            SemanticProfileContract.Version,
            assertionId,
            assertionVersion,
            context.TenantId,
            context.AppUserId,
            context.PrincipalId,
            action,
            outcome,
            context.Purpose,
            reason,
            occurredAt));
    }

    private void InvalidateDependants(
        SemanticRequestContext context,
        ProfileAssertionId inputAssertionId,
        DateTimeOffset occurredAt)
    {
        var queue = new Queue<ProfileAssertionId>();
        queue.Enqueue(inputAssertionId);
        while (queue.TryDequeue(out var inputId))
        {
            var dependants = assertions.Values
                .Where(assertion =>
                    states[assertion.AssertionId] == AssertionLifecycleState.Active
                    && assertion.DerivedFromAssertionIds.Contains(inputId))
                .OrderBy(assertion => assertion.AssertionId.Value)
                .ToArray();
            foreach (var dependant in dependants)
            {
                states[dependant.AssertionId] = AssertionLifecycleState.Retracted;
                AppendAudit(
                    context,
                    dependant.AssertionId,
                    dependant.Version,
                    AssertionLifecycleAction.Retracted,
                    SemanticEvaluationOutcome.Allowed,
                    "input-invalidated",
                    occurredAt);
                queue.Enqueue(dependant.AssertionId);
            }
        }
    }

    private static SemanticEvaluationResult Allowed(
        ProfileAssertion assertion,
        SemanticExposureLevel exposure,
        string reason) =>
        new(SemanticEvaluationOutcome.Allowed, exposure, assertion, reason);

    private static SemanticEvaluationResult Denied(
        SemanticEvaluationOutcome outcome,
        string reason) =>
        new(outcome, SemanticExposureLevel.Denied, null, reason);

    private static Guid CreateAuditId(
        ProfileAssertionId assertionId,
        long assertionVersion,
        AssertionLifecycleAction action,
        SemanticEvaluationOutcome outcome,
        DateTimeOffset occurredAt,
        int sequence)
    {
        var material = Encoding.UTF8.GetBytes(
            $"{assertionId.Value:D}:{assertionVersion}:{action}:{outcome}:{occurredAt:O}:{sequence}");
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(material, digest);
        return new Guid(digest[..16]);
    }
}
