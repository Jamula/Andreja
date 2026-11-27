using System.Globalization;
using Andreja.Docs;

namespace Andreja.DocsTests;

[TestClass]
public sealed class DocsConsistencyTests
{
    private const string ProposedAdr0006 = "- **Status:** Proposed\n";
    private const string AcceptedAdr0006 = "- **Status:** Accepted\n";
    private const string ProposedCharter =
        "- **Status:** Proposed for explicit ratification; not yet authoritative\n";
    private const string AcceptedCharter = "- **Status:** Ratified and authoritative\n";
    private const string ProposedReadme =
        "The charter remains pending explicit ratification.\n";
    private const string AcceptedReadme =
        "The charter is ratified and authoritative.\n";

    private const string RequiredCharterField = """
        body:
          - type: textarea
            id: evidence
            validations:
              required: false
          - type: textarea
            id: charter
            attributes:
              label: Charter impact
            validations:
              required: true
        """;

    private const string ProposedPrTemplate = """
        ## Charter impact

        <!-- charter-impact-state: proposed -->
        <!--
        This section is optional while ADR 0006 is Proposed and required after it is Accepted.
        -->

        ## Gates

        - [ ] Company charter impact recorded or not applicable (required after ADR 0006
              acceptance)
        """;

    private const string RequiredPrTemplate = """
        ## Charter impact

        <!-- charter-impact-state: required -->
        <!-- This section is required. -->

        ## Gates

        - [ ] Company charter impact recorded or not applicable (required)
        """;

    private const string HalfAcceptedPrTemplate = """
        ## Charter impact

        <!-- charter-impact-state: required -->
        <!-- This section is required. -->

        ## Gates

        - [ ] Company charter impact recorded or not applicable (required after ADR 0006
              acceptance)
        """;

    private const string FeedbackSecretContractFixture = """
        A raw tracking secret is disclosed only to the requester at issuance and is
        never persisted. Only a one-way verifier, the non-secret `trackingRef`, and
        metadata strictly necessary for expiry, failed-attempt throttling, recovery,
        rotation, and revocation may be stored. The raw secret never appears in a
        URL path, query, fragment, browser history, referrer, log, trace, metric,
        alert, queue field, provider metadata, backup, replica, export, analytics
        dataset, or support tool.
        """;

    private readonly string _repoRoot = RepositoryPaths.FindRoot();

    [TestMethod]
    public void FeedbackTrackingSecretVerifierOnlyContractPasses()
    {
        DocsConsistencyChecker.ValidateFeedbackTrackingSecretContract(FeedbackSecretContractFixture);
        DocsConsistencyChecker.ValidateFeedbackTrackingSecretContract(
            RepositoryPaths.ReadLfText(_repoRoot, "docs/frameworks/feedback-support.md"));
    }

    [TestMethod]
    public void FeedbackTrackingSecretRecoverableSecretPersistenceIsRejected()
    {
        var document = FeedbackSecretContractFixture + " Tracking secrets are stored outside the envelope.";

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.ValidateFeedbackTrackingSecretContract(document));
        Assert.Contains("custody contract drifted", ex.Message);
    }

    [TestMethod]
    public void FeedbackTrackingSecretRawPersistenceIsRejected()
    {
        string[] statements =
        [
            "The raw tracking secret is persisted in encrypted storage.",
            "The raw receipt-secret is stored in encrypted storage.",
        ];

        foreach (var document in statements.Select(statement => FeedbackSecretContractFixture + " " + statement))
        {
            var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateFeedbackTrackingSecretContract(document));
            Assert.Contains("custody contract drifted", ex.Message);
        }
    }

    [TestMethod]
    public void FeedbackTrackingSecretExfiltrationBoundariesAreEnforced()
    {
        string[] boundaries = ["URL path", "referrer", "log", "export", "analytics", "support"];
        foreach (var document in boundaries.Select(boundary => ReplaceFirst(FeedbackSecretContractFixture, boundary, "omitted")))
        {
            var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateFeedbackTrackingSecretContract(document));
            Assert.Contains("custody contract drifted", ex.Message);
        }
    }

    [TestMethod]
    public void StatusArtifactsTrackFeedbackSecurityAndIntegratedImpactAssessment()
    {
        Assert.IsTrue(DocsConsistencyChecker.ExpectedStatusArtifacts.Contains(
            "docs/frameworks/feedback-security-abuse-incident-routing.md"));
        Assert.IsTrue(DocsConsistencyChecker.ExpectedStatusArtifacts.Contains(
            "docs/impact-assessment.md"));
        Assert.HasCount(12, DocsConsistencyChecker.ExpectedStatusArtifacts);
    }

    [TestMethod]
    public void ProposedPlanHashPreservesSeparateAcceptedHash()
    {
        var accepted = new string('a', 64);
        var proposed = new string('b', 64);
        var adr = string.Join(
            "\n",
            $"- **Accepted Plan SHA-256:** `{accepted}`",
            $"- **Current proposed Plan SHA-256:** `{proposed}`",
            "## Decision",
            "### 2026-08-25 — accepted status",
            $"- **Plan SHA-256:** `{accepted}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment",
            "### 2026-08-26 — proposed assistant-provider phase scope",
            $"- **Plan SHA-256:** `{proposed}`",
            "- **Approver:** Cyrus Jamula; **pending**",
            "- **Classification:** Proposed editorial/status amendment");

        var metadata = DocsConsistencyChecker.ParsePlanHashMetadata(adr);

        Assert.AreEqual("proposed", metadata.State);
        Assert.AreEqual(proposed, metadata.ActiveHash);
        Assert.AreEqual(accepted, metadata.AcceptedHash);
    }

    [TestMethod]
    public void ProposedPlanHashRejectsMissingPendingRecord()
    {
        var accepted = new string('a', 64);
        var proposed = new string('b', 64);
        var adr = string.Join(
            "\n",
            $"- **Accepted Plan SHA-256:** `{accepted}`",
            $"- **Current proposed Plan SHA-256:** `{proposed}`",
            "## Decision",
            "### 2026-08-25 — accepted status",
            $"- **Plan SHA-256:** `{accepted}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.ParsePlanHashMetadata(adr));
        Assert.Contains("pending, Proposed amendment", ex.Message);
    }

    [TestMethod]
    public void ProposedPlanHashRejectsUnverifiedAcceptedHash()
    {
        var adr = string.Join(
            "\n",
            $"- **Accepted Plan SHA-256:** `{new string('c', 64)}`",
            $"- **Current proposed Plan SHA-256:** `{new string('b', 64)}`",
            "## Decision",
            "### 2026-08-25 — accepted status",
            $"- **Plan SHA-256:** `{new string('a', 64)}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment",
            "### 2026-08-26 — proposed assistant-provider phase scope",
            $"- **Plan SHA-256:** `{new string('b', 64)}`",
            "- **Approver:** Cyrus Jamula; **pending**",
            "- **Classification:** Proposed editorial/status amendment");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.ParsePlanHashMetadata(adr));
        Assert.Contains("latest accepted amendment", ex.Message);
    }

    [TestMethod]
    public void ProposedPlanHashRejectsPendingProseWithoutPendingGrammar()
    {
        var accepted = new string('a', 64);
        var proposed = new string('b', 64);
        var adr = string.Join(
            "\n",
            $"- **Accepted Plan SHA-256:** `{accepted}`",
            $"- **Current proposed Plan SHA-256:** `{proposed}`",
            "## Decision",
            "### 2026-08-25 — accepted status",
            $"- **Plan SHA-256:** `{accepted}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment",
            "### 2026-08-26 — proposed assistant-provider phase scope",
            $"- **Plan SHA-256:** `{proposed}`",
            "- **Approver:** Cyrus Jamula (approved; follow-up note pending)",
            "- **Classification:** Proposed editorial/status amendment");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.ParsePlanHashMetadata(adr));
        Assert.Contains("'<name>; **pending**'", ex.Message);
    }

    [TestMethod]
    public void CurrentPlanHashRequiresLatestExplicitApproval()
    {
        var digest = new string('a', 64);
        var adr = string.Join(
            "\n",
            $"- **Current Plan SHA-256:** `{digest}`",
            "## Decision",
            "### accepted status",
            $"- **Plan SHA-256:** `{digest}`",
            "- **Approver:** Cyrus Jamula; **pending**",
            "- **Classification:** Proposed status amendment");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.ParsePlanHashMetadata(adr));
        Assert.Contains("explicitly approved amendment", ex.Message);
    }

    [TestMethod]
    public void LegacyAndCurrentPlanHashesAcceptLatestApprovedAmendment()
    {
        var legacyDigest = new string('a', 64);
        var legacyAdr = string.Join(
            "\n",
            $"- **Plan SHA-256:** `{legacyDigest}`",
            "## Decision",
            "### accepted status",
            $"- **Plan SHA-256:** `{legacyDigest}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment");

        var legacyMeta = DocsConsistencyChecker.ParsePlanHashMetadata(legacyAdr);
        Assert.AreEqual("legacy", legacyMeta.State);
        Assert.AreEqual(legacyDigest, legacyMeta.ActiveHash);

        var currentDigest = new string('b', 64);
        var currentAdr = string.Join(
            "\n",
            $"- **Current Plan SHA-256:** `{currentDigest}`",
            "## Decision",
            "### older accepted status",
            $"- **Plan SHA-256:** `{legacyDigest}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment",
            "### current accepted status",
            $"- **Plan SHA-256:** `{currentDigest}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged editorial amendment");

        var currentMeta = DocsConsistencyChecker.ParsePlanHashMetadata(currentAdr);
        Assert.AreEqual("current", currentMeta.State);
        Assert.AreEqual(currentDigest, currentMeta.ActiveHash);
    }

    [TestMethod]
    public void ProposedHashFindsLatestPendingAmendmentWithLaterAcceptedRecord()
    {
        var accepted = new string('a', 64);
        var proposed = new string('b', 64);
        var adr = string.Join(
            "\n",
            $"- **Accepted Plan SHA-256:** `{accepted}`",
            $"- **Current proposed Plan SHA-256:** `{proposed}`",
            "## Decision",
            "### accepted status",
            $"- **Plan SHA-256:** `{accepted}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged status amendment",
            "### proposed provider scope",
            $"- **Plan SHA-256:** `{proposed}`",
            "- **Approver:** Cyrus Jamula; **pending**",
            "- **Classification:** Proposed editorial/status amendment",
            "### later accepted record",
            $"- **Plan SHA-256:** `{accepted}`",
            "- **Approver:** Cyrus Jamula",
            "- **Classification:** Logged evidence-pointer amendment");

        var metadata = DocsConsistencyChecker.ParsePlanHashMetadata(adr);

        Assert.AreEqual("proposed", metadata.State);
        Assert.AreEqual(proposed, metadata.ActiveHash);
    }

    [TestMethod]
    public void StatusArtifactCompleteCurrentInventoryPasses()
    {
        var actual = BuildFixtureDigests();
        var plan = BuildPlanTable(actual);
        var parsed = DocsConsistencyChecker.ExtractStatusArtifactHashes(plan);

        DocsConsistencyChecker.ValidateStatusArtifactHashes(
            parsed,
            DocsConsistencyChecker.ExpectedStatusArtifacts,
            DigestForPath);
        DocsConsistencyChecker.ValidateCanonicalBaselineRows(plan);
        DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(BuildCanonicalDocuments());
    }

    [TestMethod]
    public void CanonicalBaselineRequiresExactIssueAndPrDestinations()
    {
        var actual = BuildFixtureDigests();
        (string Expected, string Replacement)[] mutations =
        [
            ("https://github.com/Jamula/Andreja/issues/116", "https://github.com/Other/Repository/issues/116"),
            ("https://github.com/Jamula/Andreja/pull/117", "https://github.com/Jamula/Andreja/pull/999"),
        ];

        foreach (var (expected, replacement) in mutations)
        {
            var plan = ReplaceFirst(BuildPlanTable(actual), expected, replacement);
            var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateCanonicalBaselineRows(plan));
            Assert.Contains("authority drifted", ex.Message);
        }
    }

    [TestMethod]
    public void CanonicalBaselineRequiresNotRatifiedChallengePendingAcceptanceAndOpenAssessment()
    {
        var basePlan = BuildPlanTable(BuildFixtureDigests());

        var ratifiedPlan = ReplaceFirst(
            basePlan,
            "Canonical descriptive baseline; not ratified",
            "Canonical descriptive baseline");
        Assert.Contains(
            "authority drifted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateCanonicalBaselineRows(ratifiedPlan)).Message);

        string[] requirements =
        [
            "Privacy lead",
            "Security lead",
            "AI safety reviewer",
            "Cyrus",
            "residual-risk acceptance",
        ];
        foreach (var mutated in requirements.Select(requirement => ReplaceFirst(basePlan, requirement, "omitted")))
        {
            Assert.Contains(
                "authority drifted",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineRows(mutated)).Message);
        }

        var granted = ReplaceFirst(
            basePlan,
            "Cyrus residual-risk acceptance remains pending",
            "Cyrus residual-risk acceptance is granted");
        Assert.Contains(
            "authority drifted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateCanonicalBaselineRows(granted)).Message);

        foreach (var mutated in new[] { "The classification/impact assessment is complete.", string.Empty }
            .Select(replacement => ReplaceFirst(basePlan, DocsConsistencyChecker.CanonicalOpenAssessment, replacement)))
        {
            Assert.Contains(
                "authority drifted",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineRows(mutated)).Message);
        }
    }

    [TestMethod]
    public void CanonicalBaselineHeadersEnforceOpenAssessmentStatusChallengeAndMetadataBoundaries()
    {
        foreach (var (path, required) in DocsConsistencyChecker.CanonicalDocumentOpenAssessments)
        {
            var completedDocs = BuildCanonicalDocuments();
            completedDocs[path] = ReplaceFirst(completedDocs[path], required, "Completed");
            Assert.Contains(
                "header drifted",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(completedDocs)).Message);

            var removedDocs = BuildCanonicalDocuments();
            removedDocs[path] = ReplaceFirst(
                removedDocs[path],
                $"- **Classification/impact assessment:** {required}\n",
                string.Empty);
            Assert.Contains(
                "header drifted",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(removedDocs)).Message);
        }

        var requiredStaticFields = new (string Label, string ExpectedValue, string DriftedValue)[]
        {
            ("Status", "Canonical descriptive baseline; not ratified", "Ratified"),
            ("Residual-risk acceptance", "Cyrus; pending", "Cyrus; accepted"),
        };
        foreach (var path in DocsConsistencyChecker.CanonicalBaselineRequirements.Keys)
        {
            foreach (var (label, expectedValue, driftedValue) in requiredStaticFields)
            {
                var driftedDocs = BuildCanonicalDocuments();
                driftedDocs[path] = ReplaceFirst(
                    driftedDocs[path],
                    $"- **{label}:** {expectedValue}",
                    $"- **{label}:** {driftedValue}");
                var driftedMessage = Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(driftedDocs)).Message;
                Assert.Contains("header drifted", driftedMessage);
                Assert.Contains($"[{label}]", driftedMessage);

                var removedFieldDocs = BuildCanonicalDocuments();
                removedFieldDocs[path] = ReplaceFirst(
                    removedFieldDocs[path],
                    $"- **{label}:** {expectedValue}\n",
                    string.Empty);
                var removedFieldMessage = Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(removedFieldDocs)).Message;
                Assert.Contains("header drifted", removedFieldMessage);
                Assert.Contains($"[{label}]", removedFieldMessage);
            }
        }

        foreach (var (path, challengers) in DocsConsistencyChecker.CanonicalBaselineRequirements)
        {
            var challengeLine = $"- **Required challenge:** {string.Join(", ", challengers)}";
            var removedChallengeDocs = BuildCanonicalDocuments();
            removedChallengeDocs[path] = ReplaceFirst(
                removedChallengeDocs[path],
                $"{challengeLine}\n",
                string.Empty);
            Assert.Contains(
                "Missing required challengers",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(removedChallengeDocs)).Message);

            foreach (var challenger in new[] { "Privacy lead", "Security lead", "AI safety reviewer" })
            {
                var docs = BuildCanonicalDocuments();
                docs[path] = ReplaceFirst(
                    docs[path],
                    challengeLine,
                    ReplaceFirst(challengeLine, challenger, "omitted"));
                Assert.Contains(
                    "Missing required challengers",
                    Assert.ThrowsExactly<InvalidDataException>(() =>
                        DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(docs)).Message);
            }
        }

        var extraMetaDocs = BuildCanonicalDocuments();
        extraMetaDocs["docs/privacy.md"] = ReplaceFirst(
            extraMetaDocs["docs/privacy.md"],
            "- **Residual-risk acceptance:** Cyrus; pending",
            "- **Additional context:** Legitimate metadata\n- **Residual-risk acceptance:** Cyrus; pending");
        DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(extraMetaDocs);

        var outsideBlockDocs = BuildCanonicalDocuments();
        var privacyRequired = DocsConsistencyChecker.CanonicalDocumentOpenAssessments["docs/privacy.md"];
        var fieldLine = $"- **Classification/impact assessment:** {privacyRequired}";
        outsideBlockDocs["docs/privacy.md"] =
            ReplaceFirst(outsideBlockDocs["docs/privacy.md"], $"{fieldLine}\n", string.Empty)
            + $"\n{fieldLine}\n";
        Assert.Contains(
            "header drifted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(outsideBlockDocs)).Message);

        string[] malformedBlocks =
        [
            "# Baseline\n\n\nBaseline body.",
            ReplaceFirst(
                BuildCanonicalDocuments()["docs/privacy.md"],
                "- **Residual-risk acceptance:** Cyrus; pending",
                "not metadata"),
            ReplaceFirst(BuildCanonicalDocuments()["docs/privacy.md"], "\n\nBaseline body.", string.Empty),
        ];
        foreach (var malformed in malformedBlocks)
        {
            var docs = BuildCanonicalDocuments();
            docs["docs/privacy.md"] = malformed;
            Assert.Contains(
                "metadata block",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateCanonicalBaselineDocuments(docs)).Message);
        }
    }

    [TestMethod]
    public void StatusArtifactMissingStaleOrMalformedEntriesAreRejected()
    {
        var actual = BuildFixtureDigests();
        var firstKey = actual.Keys.First();

        var missing = new Dictionary<string, string>(actual, StringComparer.Ordinal);
        missing.Remove(firstKey);
        Assert.Contains(
            "inventory drifted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateStatusArtifactHashes(
                    missing,
                    DocsConsistencyChecker.ExpectedStatusArtifacts,
                    DigestForPath)).Message);

        var stale = new Dictionary<string, string>(actual, StringComparer.Ordinal)
        {
            [firstKey] = new string('0', 64),
        };
        Assert.Contains(
            "hash mismatch",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ValidateStatusArtifactHashes(
                    stale,
                    DocsConsistencyChecker.ExpectedStatusArtifacts,
                    DigestForPath)).Message);

        var malformed = ReplaceFirst(BuildPlanTable(actual), actual[firstKey], "not-a-sha256");
        Assert.Contains(
            "Malformed status-artifact row",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ExtractStatusArtifactHashes(malformed)).Message);
    }

    [TestMethod]
    public void Phase1AAdrHashesExtractAndValidateProposedAndAcceptedStates()
    {
        var acceptedTable = BuildPhase1AHashTable("Content hashes", 0);
        var acceptedHashes = DocsConsistencyChecker.ExtractPhase1AAdrHashes(
            acceptedTable + "\n## Specialist challenge record\n");
        Assert.HasCount(5, acceptedHashes);
        Assert.AreEqual(1.ToString("x64", CultureInfo.InvariantCulture), acceptedHashes["0001"]);

        Assert.Contains(
            "exactly one hash",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ExtractPhase1AAdrHashes(
                    $"## Content hashes\n| 0001 | `{new string('a', 64)}` |\n")).Message);

        var proposedTable = BuildPhase1AHashTable("Current proposed ADR hashes", 10);
        var decision = acceptedTable + proposedTable + "- **Status:** Proposed; not accepted\n";
        var proposedHashes = DocsConsistencyChecker.ExtractPhase1AAdrHashes(decision);
        Assert.AreEqual(11.ToString("x64", CultureInfo.InvariantCulture), proposedHashes["0001"]);

        Assert.Contains(
            "not accepted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ExtractPhase1AAdrHashes(acceptedTable + proposedTable)).Message);
        Assert.Contains(
            "not accepted",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ExtractPhase1AAdrHashes(
                    acceptedTable + proposedTable + "<!-- - **Status:** Proposed; not accepted -->\n")).Message);
        Assert.Contains(
            "Content hashes",
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocsConsistencyChecker.ExtractPhase1AAdrHashes(
                    proposedTable + "- **Status:** Proposed; not accepted\n")).Message);

        const string marker = "- **Current copy:** Proposed editorial update; not accepted\n";
        var validDocs = Enumerable.Range(1, 5).ToDictionary(
            i => $"000{i}",
            _ => marker,
            StringComparer.Ordinal);
        DocsConsistencyChecker.ValidateProposedPhase1AAdrCopies(proposedTable, validDocs);

        foreach (var invalidDocs in new[] { string.Empty, $"<!-- {marker.TrimEnd()} -->\n", marker + marker }
            .Select(badCopy => new Dictionary<string, string>(validDocs, StringComparer.Ordinal)
            {
                ["0003"] = badCopy,
            }))
        {
            Assert.Contains(
                "ADR 0003",
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    DocsConsistencyChecker.ValidateProposedPhase1AAdrCopies(proposedTable, invalidDocs)).Message);
        }

        DocsConsistencyChecker.ValidateProposedPhase1AAdrCopies(
            acceptedTable,
            Enumerable.Range(1, 5).ToDictionary(i => $"000{i}", _ => string.Empty, StringComparer.Ordinal));
    }

    [TestMethod]
    public void CharterHashProposedAcceptedStaleAndDuplicateChecksMatchContract()
    {
        Assert.IsNull(DocsConsistencyChecker.CheckCharterHashContent(
            "candidate charter\n",
            "- **Status:** Proposed\n- **Charter SHA-256:** `CANDIDATE_HASH_PENDING_FINAL_REVIEW`\n"));

        const string charter = "ratified charter\n";
        var digest = RepositoryPaths.Sha256LfText(charter);
        Assert.AreEqual(
            digest,
            DocsConsistencyChecker.CheckCharterHashContent(
                charter,
                $"- **Status:** Accepted\n- **Charter SHA-256:** `{digest}`\n"));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterHashContent(
                "changed charter\n",
                $"- **Status:** Accepted\n- **Charter SHA-256:** `{new string('0', 64)}`\n"));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterHashContent(
                "candidate charter\n",
                $"- **Status:** Proposed\n- **Status:** Accepted\n- **Charter SHA-256:** `{new string('0', 64)}`\n"));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterHashContent(
                charter,
                $"- **Status:** Accepted\n- **Charter SHA-256:** `{digest}`\n- **Charter SHA-256:** `{new string('1', 64)}`\n"));
    }

    [TestMethod]
    public void CharterAtomicityProposedBundleAndRejectionsMatchContract()
    {
        Assert.AreEqual(
            "Proposed",
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                ProposedAdr0006,
                "body: []\n",
                ProposedPrTemplate,
                ProposedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                ProposedAdr0006,
                RequiredCharterField,
                ProposedPrTemplate,
                ProposedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                ProposedAdr0006,
                "body: []\n",
                RequiredPrTemplate,
                ProposedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                ProposedAdr0006,
                RequiredCharterField,
                ProposedPrTemplate,
                ProposedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                ProposedAdr0006 + AcceptedAdr0006,
                "body: []\n",
                ProposedPrTemplate,
                ProposedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                ProposedAdr0006,
                "body: []\n",
                ProposedPrTemplate,
                AcceptedReadme));
    }

    [TestMethod]
    public void CharterAtomicityAcceptedBundleAndRejectionsMatchContract()
    {
        Assert.AreEqual(
            "Accepted",
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                RequiredCharterField,
                RequiredPrTemplate,
                AcceptedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                "- **Status:** Not ratified\n",
                AcceptedAdr0006,
                RequiredCharterField,
                RequiredPrTemplate,
                AcceptedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                ProposedCharter,
                AcceptedAdr0006,
                RequiredCharterField,
                RequiredPrTemplate,
                AcceptedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                RequiredCharterField,
                RequiredPrTemplate,
                ProposedReadme));

        const string optionalField = """
            body:
              - type: textarea
                id: charter
                attributes:
                  label: Charter impact
                validations:
                  required: false
            """;
        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                optionalField,
                RequiredPrTemplate,
                AcceptedReadme));

        const string otherRequiredField = """
            body:
              - type: textarea
                id: evidence
                validations:
                  required: true
              - type: textarea
                id: charter
            """;
        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                otherRequiredField,
                RequiredPrTemplate,
                AcceptedReadme));

        const string nestedValidationsField = """
            body:
              - type: textarea
                id: charter
                attributes:
                  validations:
                    required: true
            """;
        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                nestedValidationsField,
                RequiredPrTemplate,
                AcceptedReadme));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            DocsConsistencyChecker.CheckCharterAtomicityContent(
                AcceptedCharter,
                AcceptedAdr0006,
                RequiredCharterField,
                HalfAcceptedPrTemplate,
                AcceptedReadme));
    }

    [TestMethod]
    public void CliCheckConsistencyCurrentRepositoryExitsZeroAndPrintsAllChecks()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = Program.Execute(["check-consistency"], stdout, stderr, _repoRoot);

        Assert.AreEqual(0, exitCode, stdout + stderr.ToString());
        var output = stdout.ToString();
        Assert.Contains("OK: 12 status-artifact hashes match.", output);
        Assert.Contains("OK: 19 first-party skills match between plan.md and the catalog.", output);
        Assert.Contains("OK: 23 connector categories match between plan.md and the catalog.", output);
        Assert.Contains("All documentation consistency checks passed.", output);
    }

    [TestMethod]
    public void CliCheckConsistencyMissingRequiredFileInWorkspaceExitsOneWithErrorAnnotation()
    {
        var scratch = Path.Join(_repoRoot, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = Program.Execute(["check-consistency"], stdout, stderr, scratch);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("::error::Required file not found: docs/plan.md", stdout.ToString());
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static Dictionary<string, string> BuildFixtureDigests()
    {
        return DocsConsistencyChecker.ExpectedStatusArtifacts
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => path, DigestForPath, StringComparer.Ordinal);
    }

    private static string DigestForPath(string path) => RepositoryPaths.Sha256LfText(path);

    private static string BuildPlanTable(IReadOnlyDictionary<string, string> artifacts)
    {
        var rows = new List<string>
        {
            "#### Phase 0 policy and governance artifact classification",
            string.Empty,
            "| Artifact | Source and current SHA-256 | Current authority |",
            "|---|---|---|",
        };

        foreach (var (path, digest) in artifacts)
        {
            var linkTarget = path.StartsWith("docs/", StringComparison.Ordinal) ? path[5..] : path;
            if (DocsConsistencyChecker.CanonicalBaselineRequirements.TryGetValue(path, out var challengers))
            {
                var challengeText = string.Join(", ", challengers[..^1]);
                rows.Add(
                    $"| [`{path}`]({linkTarget}) | " +
                    $"{DocsConsistencyChecker.CanonicalIssueSource}, " +
                    $"{DocsConsistencyChecker.CanonicalPrSource}; `{digest}` | " +
                    $"**Canonical descriptive baseline; not ratified.** {challengeText}, {challengers[^1]}. " +
                    $"Cyrus residual-risk acceptance remains pending. " +
                    $"{DocsConsistencyChecker.CanonicalOpenAssessment} |");
            }
            else
            {
                rows.Add($"| [`{path}`]({linkTarget}) | `{digest}` | Draft |");
            }
        }

        return string.Join("\n", rows);
    }

    private static Dictionary<string, string> BuildCanonicalDocuments()
    {
        var docs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, challengers) in DocsConsistencyChecker.CanonicalBaselineRequirements)
        {
            docs[path] = string.Join(
                "\n",
                "# Baseline",
                string.Empty,
                "- **Status:** Canonical descriptive baseline; not ratified",
                "- **Owner:** Trust — Security lead, Privacy lead, AI safety reviewer",
                $"- **Required challenge:** {string.Join(", ", challengers)}",
                "- **Residual-risk acceptance:** Cyrus; pending",
                $"- **Classification/impact assessment:** {DocsConsistencyChecker.CanonicalDocumentOpenAssessments[path]}",
                string.Empty,
                "Baseline body.");
        }

        return docs;
    }

    private static string BuildPhase1AHashTable(string heading, int offset)
    {
        var rows = string.Join(
            "\n",
            Enumerable.Range(1, 5).Select(i =>
                $"| 000{i} | `{(i + offset).ToString("x64", CultureInfo.InvariantCulture)}` |"));
        return $"## {heading}\n\n| ADR | SHA-256 |\n| --- | --- |\n{rows}\n";
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, index, $"Expected to find '{oldValue}' in source text.");
        return string.Concat(text.AsSpan(0, index), newValue, text.AsSpan(index + oldValue.Length));
    }
}
