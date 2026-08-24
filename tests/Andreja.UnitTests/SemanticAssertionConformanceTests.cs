using Andreja.Modules.Semantics;
using Andreja.Platform.Contracts.Semantics;
using System.Collections.Immutable;
using System.Text.Json;

namespace Andreja.UnitTests;

public sealed class SemanticAssertionConformanceTests
{
    private static readonly DateTimeOffset RecordedAt =
        new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly SemanticTenantId TenantId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000001"));
    private static readonly SemanticAppUserId AppUserId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000002"));
    private static readonly SemanticPrincipalId PrincipalId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000003"));
    private static readonly SemanticSubjectId SubjectId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000004"));
    private static readonly ProvenanceSourceId SourceId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000005"));
    private static readonly ProfileAssertionId AssertionId =
        new(Guid.Parse("018f0000-0000-7000-8000-000000000006"));
    private static readonly string SourceDigest = new('a', 64);
    private static readonly string TamperedDigest = new('b', 64);
    private const string Purpose = "task-management";

    [Fact]
    public void ContractRoundTripsWithoutCollapsingTypedIdentityOrExtensions()
    {
        var assertion = CreateAssertion();
        var json = JsonSerializer.Serialize(assertion);
        var roundTrip = JsonSerializer.Deserialize<ProfileAssertion>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(assertion.TenantId, roundTrip.TenantId);
        Assert.Equal(assertion.AppUserId, roundTrip.AppUserId);
        Assert.Equal(assertion.PrincipalId, roundTrip.PrincipalId);
        Assert.Equal(assertion.CreatedByPrincipalId, roundTrip.CreatedByPrincipalId);
        Assert.Equal(assertion.RecordDigest, roundTrip.RecordDigest);
        Assert.Equal(
            "bounded-extension-value",
            roundTrip.Extensions["https://skills.example.invalid/ns#bounded"].GetString());
    }

    [Fact]
    public void JsonLdProjectionMatchesPinnedFixture()
    {
        var ledger = CreateLedger();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), CreateAssertion()).Outcome);
        var package = ledger.Export(
            Context(),
            "tenant-local-1",
            RecordedAt.AddHours(1));

        var actual = SemanticJsonLdSerializer.Serialize(package, indented: true);
        var fixturePath = Path.Join(
            AppContext.BaseDirectory,
            "Fixtures",
            "semantic-profile-v1.jsonld");
        using var expectedDocument = JsonDocument.Parse(File.ReadAllText(fixturePath));
        using var actualDocument = JsonDocument.Parse(actual);

        Assert.True(JsonElement.DeepEquals(
            expectedDocument.RootElement,
            actualDocument.RootElement));
        Assert.Contains("\"@context\"", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", actual, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConflictingAssertionsCoexistUntilExplicitCorrectionOrRetraction()
    {
        var ledger = CreateLedger();
        var morning = CreateAssertion();
        var afternoon = CreateAssertion(
            ProfileAssertionId.New(),
            lexicalValue: "afternoon");
        Assert.Equal(SemanticEvaluationOutcome.Allowed, ledger.Append(Context(), morning).Outcome);
        Assert.Equal(SemanticEvaluationOutcome.Allowed, ledger.Append(Context(), afternoon).Outcome);
        Assert.Equal(2, ledger.FindActive(Context(), SubjectId, "and:preferredTime").Count);

        var correction = CreateAssertion(
            ProfileAssertionId.New(),
            lexicalValue: "early-morning",
            lineage: new(morning.AssertionId, morning.AssertionId, null));
        var corrected = ledger.Correct(
            Change(morning),
            correction);
        var retracted = ledger.Retract(Change(afternoon));

        Assert.Equal(SemanticEvaluationOutcome.Allowed, corrected.Outcome);
        Assert.Equal(SemanticEvaluationOutcome.Allowed, retracted.Outcome);
        var active = Assert.Single(
            ledger.FindActive(Context(), SubjectId, "and:preferredTime"));
        Assert.Equal("early-morning", active.Value.LexicalValue);
        Assert.Contains(
            ledger.Audit,
            item => item.Action == AssertionLifecycleAction.Corrected);
        Assert.Contains(
            ledger.Audit,
            item => item.Action == AssertionLifecycleAction.Superseded);
        Assert.Contains(
            ledger.Audit,
            item => item.Action == AssertionLifecycleAction.Retracted);
    }

    [Theory]
    [InlineData("corrects")]
    [InlineData("supersedes")]
    [InlineData("retracts")]
    public void RawAppendRejectsForgedLineage(string lineageKind)
    {
        var ledger = CreateLedger();
        var original = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), original).Outcome);
        var lineage = lineageKind switch
        {
            "corrects" => new AssertionLineage(original.AssertionId, null, null),
            "supersedes" => new AssertionLineage(null, original.AssertionId, null),
            "retracts" => new AssertionLineage(null, null, original.AssertionId),
            _ => throw new ArgumentOutOfRangeException(nameof(lineageKind)),
        };
        var forged = CreateAssertion(ProfileAssertionId.New(), lineage: lineage);

        var result = ledger.Append(Context(), forged);

        Assert.Equal(SemanticEvaluationOutcome.Invalid, result.Outcome);
        Assert.Equal("raw-lineage-not-allowed", result.Reason);
        Assert.Equal(
            original.AssertionId,
            Assert.Single(
                ledger.FindActive(Context(), SubjectId, original.Predicate)).AssertionId);
    }

    [Fact]
    public void DuplicateCorrectionRetiresPredecessorExactlyOnce()
    {
        var ledger = CreateLedger();
        var original = CreateAssertion();
        var correction = CreateCorrection(original, "early-morning");
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), original).Outcome);

        var first = ledger.Correct(Change(original), correction);
        var duplicate = ledger.Correct(Change(original), correction);

        Assert.Equal(SemanticEvaluationOutcome.Allowed, first.Outcome);
        Assert.Equal(SemanticEvaluationOutcome.NotFound, duplicate.Outcome);
        Assert.Equal(
            correction.AssertionId,
            Assert.Single(
                ledger.FindActive(Context(), SubjectId, original.Predicate)).AssertionId);
        Assert.Single(
            ledger.Audit,
            entry =>
                entry.AssertionId == original.AssertionId
                && entry.Action == AssertionLifecycleAction.Superseded);
    }

    [Fact]
    public async Task ConcurrentCorrectionsCreateExactlyOneActiveSuccessor()
    {
        var ledger = CreateLedger();
        var original = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), original).Outcome);
        var corrections = Enumerable.Range(0, 16)
            .Select(index => CreateCorrection(original, $"corrected-{index}"))
            .ToArray();
        using var start = new ManualResetEventSlim();
        var operations = corrections.Select(correction => Task.Run(() =>
        {
            start.Wait();
            return ledger.Correct(Change(original), correction);
        })).ToArray();

        start.Set();
        var results = await Task.WhenAll(operations);

        var winner = Assert.Single(
            results,
            result => result.Outcome == SemanticEvaluationOutcome.Allowed);
        Assert.Equal(
            15,
            results.Count(result => result.Outcome == SemanticEvaluationOutcome.NotFound));
        Assert.Equal(
            winner.Assertion!.AssertionId,
            Assert.Single(
                ledger.FindActive(Context(), SubjectId, original.Predicate)).AssertionId);
    }

    [Fact]
    public void CorrectionRejectsInactiveCrossScopeAndCyclicPredecessors()
    {
        var inactiveLedger = CreateLedger();
        var inactive = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            inactiveLedger.Append(Context(), inactive).Outcome);
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            inactiveLedger.Retract(Change(inactive)).Outcome);
        Assert.Equal(
            SemanticEvaluationOutcome.NotFound,
            inactiveLedger.Correct(
                Change(inactive),
                CreateCorrection(inactive, "inactive-correction")).Outcome);

        var crossScopeLedger = CreateLedger();
        var original = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            crossScopeLedger.Append(Context(), original).Outcome);
        var wrongContext = Context() with { PrincipalId = SemanticPrincipalId.New() };
        Assert.Equal(
            SemanticEvaluationOutcome.Denied,
            crossScopeLedger.Correct(
                Change(original) with { Context = wrongContext },
                CreateCorrection(original, "cross-scope")).Outcome);
        var wrongScopeSuccessor = SemanticRecordDigest.Seal(
            CreateCorrection(original, "wrong-successor-scope") with
            {
                TenantId = SemanticTenantId.New(),
            });
        Assert.Equal(
            SemanticEvaluationOutcome.Invalid,
            crossScopeLedger.Correct(
                Change(original),
                wrongScopeSuccessor).Outcome);

        var cycle = CreateCorrection(original, "cycle") with
        {
            AssertionId = original.AssertionId,
        };
        cycle = SemanticRecordDigest.Seal(cycle);
        var cycleResult = crossScopeLedger.Correct(Change(original), cycle);
        Assert.Equal(SemanticEvaluationOutcome.Invalid, cycleResult.Outcome);
        Assert.Equal("invalid-correction-cycle", cycleResult.Reason);
        Assert.Equal(
            original.AssertionId,
            Assert.Single(
                crossScopeLedger.FindActive(
                    Context(),
                    SubjectId,
                    original.Predicate)).AssertionId);
    }

    [Theory]
    [InlineData("and:preferredTime")]
    [InlineData("https://schema.org/startDate")]
    [InlineData("http://www.w3.org/ns/prov#value")]
    [InlineData("urn:example:preferred-time")]
    public void PredicateValidationAcceptsPinnedAndApprovedAbsoluteIris(string predicate)
    {
        var ledger = CreateLedger();

        var result = ledger.Append(Context(), CreateAssertion(predicate: predicate));

        Assert.Equal(SemanticEvaluationOutcome.Allowed, result.Outcome);
    }

    [Theory]
    [InlineData("and:")]
    [InlineData("and:bad term")]
    [InlineData("and:term:extra")]
    [InlineData("relative/path")]
    [InlineData("unknown:predicate")]
    [InlineData("javascript:alert(1)")]
    [InlineData(" https://schema.org/startDate")]
    [InlineData("https://example.invalid/bad path")]
    [InlineData("and:term\ninjected")]
    public void PredicateValidationRejectsUnsafeOrUnknownIris(string predicate)
    {
        var ledger = CreateLedger();
        var assertion = CreateAssertion(predicate: predicate);

        var result = ledger.Append(Context(), assertion);

        Assert.Equal(SemanticEvaluationOutcome.Invalid, result.Outcome);
        Assert.Equal("invalid-predicate", result.Reason);
        var package = new SemanticExportPackage(
            SemanticProfileContract.Version,
            SemanticProfileContract.JsonLdContextVersion,
            "tenant-local-1",
            RecordedAt,
            [assertion],
            [CreateSource()],
            [],
            []);
        Assert.Throws<ArgumentException>(() =>
            SemanticJsonLdSerializer.Serialize(package));
    }

    [Theory]
    [InlineData("tenant", "tenant-denied")]
    [InlineData("user", "app-user-denied")]
    [InlineData("principal", "principal-denied")]
    [InlineData("purpose", "purpose-denied")]
    public void OwnershipAndPurposeMismatchesFailClosed(string mismatch, string reason)
    {
        var ledger = CreateLedger();
        var assertion = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), assertion).Outcome);
        var context = mismatch switch
        {
            "tenant" => Context() with { TenantId = SemanticTenantId.New() },
            "user" => Context() with { AppUserId = SemanticAppUserId.New() },
            "principal" => Context() with { PrincipalId = SemanticPrincipalId.New() },
            "purpose" => Context() with { Purpose = "unapproved-purpose" },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };

        var result = ledger.Evaluate(new(
            context,
            assertion.AssertionId,
            assertion.Version,
            SemanticUseKind.OwnerRead,
            SemanticExposureLevel.Full,
            RecordedAt));

        Assert.Equal(SemanticEvaluationOutcome.Denied, result.Outcome);
        Assert.Equal(reason, result.Reason);
        Assert.Null(result.Assertion);
    }

    [Fact]
    public void ExposureUsesTheLeastAllowedLevelAndDefaultsPrivate()
    {
        var ledger = CreateLedger();
        var privateAssertion = CreateAssertion();
        Assert.Equal(
            SemanticExposureLevel.Denied,
            privateAssertion.Handling.ModelExposure);
        Assert.Equal(
            SemanticExposureLevel.Denied,
            privateAssertion.Handling.Sharing);
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), privateAssertion).Outcome);

        var denied = ledger.Evaluate(new(
            Context(),
            privateAssertion.AssertionId,
            1,
            SemanticUseKind.Model,
            SemanticExposureLevel.Full,
            RecordedAt));
        Assert.Equal(SemanticEvaluationOutcome.Denied, denied.Outcome);

        var summaryAssertion = CreateAssertion(
            ProfileAssertionId.New(),
            handling: CreateHandling() with
            {
                ModelExposure = SemanticExposureLevel.Summary,
            });
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), summaryAssertion).Outcome);
        var reduced = ledger.Evaluate(new(
            Context(),
            summaryAssertion.AssertionId,
            1,
            SemanticUseKind.Model,
            SemanticExposureLevel.Full,
            RecordedAt));
        Assert.Equal(SemanticExposureLevel.Summary, reduced.EffectiveExposure);
    }

    [Fact]
    public void SensitiveInferenceIsAReviewableHypothesisAndNotExportedByDefault()
    {
        var ledger = CreateLedger();
        var hypothesis = CreateAssertion(
            ProfileAssertionId.New(),
            epistemicStatus: EpistemicStatus.Inferred,
            verificationState: VerificationState.Unverified,
            reviewState: AssertionReviewState.PendingUserReview,
            confidence: new(0.64m, "bounded-rule", "1", "Two cited inputs matched."),
            handling: CreateHandling(
                SensitivityClass.Sensitive,
                ExportDisposition.Include));
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), hypothesis).Outcome);

        var defaultExport = ledger.Export(
            Context(),
            "tenant-local-1",
            RecordedAt.AddMinutes(1));
        var explicitExport = ledger.Export(
            Context(),
            "tenant-local-1",
            RecordedAt.AddMinutes(2),
            new(IncludeSensitiveInferences: true));

        Assert.Empty(defaultExport.Assertions);
        Assert.Single(explicitExport.Assertions);
        Assert.Equal(EpistemicStatus.Inferred, explicitExport.Assertions[0].EpistemicStatus);
        Assert.Equal(
            AssertionReviewState.PendingUserReview,
            explicitExport.Assertions[0].ReviewState);
    }

    [Fact]
    public void RetractionInvalidatesDependentHypothesesTransitively()
    {
        var ledger = CreateLedger();
        var stated = CreateAssertion();
        var inferred = CreateAssertion(
            ProfileAssertionId.New(),
            lexicalValue: "suggested-time",
            epistemicStatus: EpistemicStatus.Inferred,
            verificationState: VerificationState.Unverified,
            reviewState: AssertionReviewState.PendingUserReview,
            confidence: new(0.8m, "bounded-rule", "1", "The stated preference matched."),
            derivedFrom: [stated.AssertionId]);
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), stated).Outcome);
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), inferred).Outcome);

        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Retract(Change(stated)).Outcome);
        var evaluation = ledger.Evaluate(new(
            Context(),
            inferred.AssertionId,
            inferred.Version,
            SemanticUseKind.OwnerRead,
            SemanticExposureLevel.Full,
            RecordedAt.AddMinutes(2)));

        Assert.Equal(SemanticEvaluationOutcome.NotFound, evaluation.Outcome);
        Assert.Contains(
            ledger.Audit,
            item =>
                item.AssertionId == inferred.AssertionId
                && item.ReasonCode == "input-invalidated");
    }

    [Fact]
    public void DeleteRemovesContentFromSerializedAuditExportAndTombstones()
    {
        const string lexicalMarker = "PRIVATE-LEXICAL-VALUE";
        const string predicateMarker = "private-predicate-marker";
        const string sourceMarker = "receipt:PRIVATE-SOURCE-REFERENCE";
        const string evidenceMarker = "PRIVATE-EVIDENCE-TEXT";
        const string explanationMarker = "PRIVATE-CONFIDENCE-EXPLANATION";
        const string extensionMarker = "PRIVATE-EXTENSION-VALUE";
        const string reasonMarker = "PRIVATE-REASON-CONTENT";
        var ledger = new InMemorySemanticAssertionLedger();
        var source = CreateSource(sourceReference: sourceMarker);
        ledger.AppendSource(Context(), source);
        var assertion = CreateAssertion(
            lexicalValue: lexicalMarker,
            predicate: $"https://example.invalid/vocab/{predicateMarker}",
            confidence: new(1m, "user-entry", "1", explanationMarker),
            evidenceRole: evidenceMarker,
            extensionValue: extensionMarker,
            handling: CreateHandling(
                SensitivityClass.Sensitive,
                deleteDisposition: DeleteDisposition.Tombstone));
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), assertion).Outcome);

        var deleted = ledger.Delete(Change(assertion, reasonMarker));
        var package = ledger.Export(
            Context(),
            "tenant-local-1",
            RecordedAt.AddHours(1));

        Assert.Equal(SemanticEvaluationOutcome.Allowed, deleted.Outcome);
        Assert.Null(deleted.Assertion);
        Assert.Empty(package.Assertions);
        var tombstone = Assert.Single(package.Tombstones);
        Assert.Equal(assertion.RecordDigest, tombstone.LastRecordDigest);
        var serialized = JsonSerializer.Serialize(new
        {
            LedgerAudit = ledger.Audit,
            Export = package,
            JsonLd = SemanticJsonLdSerializer.Serialize(package),
        });
        foreach (var forbidden in new[]
        {
            lexicalMarker,
            predicateMarker,
            sourceMarker,
            SourceDigest,
            evidenceMarker,
            explanationMarker,
            extensionMarker,
            reasonMarker,
        })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdversarialPurposeIsRejectedAndNormalizedInSerializedAudit()
    {
        const string purposeMarker = "PRIVATE-PURPOSE-CONTENT";
        var adversarialPurpose = $"task-management\n{purposeMarker}";
        var ledger = CreateLedger();
        var assertion = CreateAssertion(
            handling: CreateHandling(purposes: [adversarialPurpose]));

        var result = ledger.Append(
            Context() with { Purpose = adversarialPurpose },
            assertion);
        var serialized = JsonSerializer.Serialize(ledger.Audit);

        Assert.Equal(SemanticEvaluationOutcome.Invalid, result.Outcome);
        Assert.Equal("purpose-denied", result.Reason);
        Assert.DoesNotContain(purposeMarker, serialized, StringComparison.Ordinal);
        Assert.Contains("\"Purpose\":\"invalid-purpose\"", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void HardDeleteDoesNotRetainTombstone()
    {
        var ledger = CreateLedger();
        var assertion = CreateAssertion(
            handling: CreateHandling(
                deleteDisposition: DeleteDisposition.HardDelete));
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), assertion).Outcome);

        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Delete(Change(assertion)).Outcome);

        Assert.Empty(ledger.Export(
            Context(),
            "tenant-local-1",
            RecordedAt.AddHours(1)).Tombstones);
    }

    [Theory]
    [InlineData("digest", "record-digest-mismatch")]
    [InlineData("version", "unsupported-contract-version")]
    [InlineData("class", "invalid-assertion")]
    [InlineData("source-digest", "invalid-provenance")]
    [InlineData("extension", "invalid-extension")]
    public void TamperVersionUnknownClassAndProvenanceFailuresAreDeterministic(
        string mutation,
        string reason)
    {
        var ledger = CreateLedger();
        var assertion = CreateAssertion();
        assertion = mutation switch
        {
            "digest" => assertion with
            {
                RecordDigest = TamperedDigest,
            },
            "version" => SemanticRecordDigest.Seal(assertion with
            {
                ContractVersion = "99",
            }),
            "class" => SemanticRecordDigest.Seal(assertion with
            {
                DataClass = (SemanticDataClass)999,
            }),
            "source-digest" => SemanticRecordDigest.Seal(assertion with
            {
                Evidence =
                [
                    new(SourceId, TamperedDigest, "decisive"),
                ],
            }),
            "extension" => SemanticRecordDigest.Seal(assertion with
            {
                Extensions = new Dictionary<string, JsonElement>
                {
                    [SemanticProfileContract.JsonLdContextIri + "predicate"] =
                            assertion.Extensions.Values.Single(),
                }
                    .ToImmutableDictionary(StringComparer.Ordinal),
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        var result = ledger.Append(Context(), assertion);

        Assert.Equal(SemanticEvaluationOutcome.Invalid, result.Outcome);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public async Task ConcurrentLifecycleUpdatesAllowExactlyOneWinner()
    {
        var ledger = CreateLedger();
        var assertion = CreateAssertion();
        Assert.Equal(
            SemanticEvaluationOutcome.Allowed,
            ledger.Append(Context(), assertion).Outcome);
        var request = Change(assertion);
        using var start = new ManualResetEventSlim();
        var operations = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return ledger.Retract(request);
        })).ToArray();

        start.Set();
        var results = await Task.WhenAll(operations);

        Assert.Single(
            results,
            result => result.Outcome == SemanticEvaluationOutcome.Allowed);
        Assert.Equal(
            15,
            results.Count(result => result.Outcome == SemanticEvaluationOutcome.NotFound));
    }

    [Fact]
    public void ProvenanceIsAppendOnlyAndEvidenceDigestMustMatch()
    {
        var ledger = new InMemorySemanticAssertionLedger();
        var source = CreateSource();
        ledger.AppendSource(Context(), source);

        Assert.Throws<InvalidOperationException>(() => ledger.AppendSource(Context(), source));
    }

    private static InMemorySemanticAssertionLedger CreateLedger()
    {
        var ledger = new InMemorySemanticAssertionLedger();
        ledger.AppendSource(Context(), CreateSource());
        return ledger;
    }

    private static ProvenanceSource CreateSource(
        string sourceReference = "receipt:local-minimized-reference") =>
        new(
            SemanticProfileContract.Version,
            SourceId,
            1,
            TenantId,
            AppUserId,
            PrincipalId,
            "interaction-receipt",
            sourceReference,
            SourceDigest,
            RecordedAt.AddMinutes(-1),
            PrincipalId,
            Purpose,
            "user-entry",
            "1");

    private static ProfileAssertion CreateAssertion(
        ProfileAssertionId? assertionId = null,
        string lexicalValue = "morning",
        EpistemicStatus epistemicStatus = EpistemicStatus.UserStated,
        VerificationState verificationState = VerificationState.UserStated,
        AssertionReviewState reviewState = AssertionReviewState.UserApproved,
        AssertionConfidence? confidence = null,
        SemanticHandlingPolicy? handling = null,
        AssertionLineage? lineage = null,
        IEnumerable<ProfileAssertionId>? derivedFrom = null,
        string predicate = "and:preferredTime",
        string evidenceRole = "decisive",
        string extensionValue = "bounded-extension-value")
    {
        using var extension = JsonDocument.Parse(JsonSerializer.Serialize(extensionValue));
        var assertion = new ProfileAssertion(
            SemanticProfileContract.Version,
            assertionId ?? AssertionId,
            1,
            TenantId,
            AppUserId,
            PrincipalId,
            PrincipalId,
            SubjectId,
            predicate,
            new(
                SemanticValueKind.Text,
                lexicalValue,
                "http://www.w3.org/2001/XMLSchema#string",
                "en",
                null),
            SemanticDataClass.Preference,
            epistemicStatus,
            verificationState,
            reviewState,
            confidence,
            [new(SourceId, SourceDigest, evidenceRole)],
            [.. derivedFrom ?? []],
            handling ?? CreateHandling(),
            RecordedAt,
            null,
            RecordedAt,
            null,
            lineage ?? new(null, null, null),
            string.Empty,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["https://skills.example.invalid/ns#bounded"] =
                        extension.RootElement.Clone(),
            }
                .ToImmutableDictionary(StringComparer.Ordinal));
        return SemanticRecordDigest.Seal(assertion);
    }

    private static SemanticHandlingPolicy CreateHandling(
        SensitivityClass sensitivity = SensitivityClass.Personal,
        ExportDisposition exportDisposition = ExportDisposition.Include,
        DeleteDisposition deleteDisposition = DeleteDisposition.Tombstone,
        IEnumerable<string>? purposes = null) =>
        SemanticHandlingPolicy.PrivateByDefault(
            sensitivity,
            purposes ?? [Purpose],
            new(
                exportDisposition,
                deleteDisposition,
                AuditDisposition.ContentMinimized,
                RecordedAt.AddYears(1)));

    private static SemanticRequestContext Context() =>
        new(TenantId, AppUserId, PrincipalId, Purpose);

    private static ProfileAssertion CreateCorrection(
        ProfileAssertion predecessor,
        string lexicalValue) =>
        CreateAssertion(
            ProfileAssertionId.New(),
            lexicalValue,
            lineage: new(
                predecessor.AssertionId,
                predecessor.AssertionId,
                null));

    private static AssertionChangeRequest Change(
        ProfileAssertion assertion,
        string reason = "user-request") =>
        new(
            Context(),
            assertion.AssertionId,
            assertion.Version,
            RecordedAt.AddMinutes(1),
            reason);
}
