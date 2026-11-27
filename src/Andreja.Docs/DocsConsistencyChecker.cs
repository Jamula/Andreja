using System.Text.RegularExpressions;

namespace Andreja.Docs;

public sealed record PlanHashMetadata(string State, string ActiveHash, string? AcceptedHash);

public sealed record StatusArtifactRow(string ArtifactCell, string SourceCell, string AuthorityCell);

public static partial class DocsConsistencyChecker
{
    public const string CanonicalOpenAssessment =
        "The classification/impact assessment remains open unless explicitly approved with cited evidence.";

    public const string CanonicalIssueSource =
        "Issue [#116](https://github.com/Jamula/Andreja/issues/116)";

    public const string CanonicalPrSource =
        "PR [#117](https://github.com/Jamula/Andreja/pull/117)";

    private const string ProposedPrSentence =
        "This section is optional while ADR 0006 is Proposed and required after it is Accepted.";

    private const string ProposedPrGate =
        "Company charter impact recorded or not applicable (required after ADR 0006 acceptance)";

    private const string AcceptedPrGate =
        "Company charter impact recorded or not applicable (required)";

    private static readonly string[] ExpectedStatusArtifactList =
    [
        "docs/operating-model.md",
        "docs/cost-model.md",
        "docs/privacy.md",
        "docs/threat-model.md",
        "docs/impact-assessment.md",
        "docs/frameworks/feedback-support.md",
        "docs/frameworks/feedback-security-abuse-incident-routing.md",
        "docs/frameworks/prioritization-launch.md",
        "docs/charter.md",
        "docs/legal/license-evaluation.md",
        "docs/legal/regulatory-applicability.md",
        "docs/legal/feedback-privacy-retention-dsr.md",
    ];

    private static readonly HashSet<string> ExpectedStatusArtifactSet =
        new(ExpectedStatusArtifactList, StringComparer.Ordinal);

    private static readonly Dictionary<string, string[]> CanonicalBaselineRequirementsMap =
        new(StringComparer.Ordinal)
        {
            ["docs/privacy.md"] = ["Privacy lead", "Security lead", "AI safety reviewer (AI safety); pending"],
            ["docs/threat-model.md"] = ["Security lead", "Privacy lead", "AI safety reviewer (AI safety); pending"],
        };

    private static readonly Dictionary<string, string> CanonicalDocumentOpenAssessmentsMap =
        new(StringComparer.Ordinal)
        {
            ["docs/privacy.md"] =
                "Open; this inventory does not satisfy that gate without an explicitly approved assessment and cited evidence",
            ["docs/threat-model.md"] =
                "Open; this model does not satisfy that gate without an explicitly approved assessment and cited evidence",
        };

    private static readonly Dictionary<string, string> AcceptedPhase1AAdrPaths =
        new(StringComparer.Ordinal)
        {
            ["0001"] = "docs/adr/0001-phase-1a-modular-boundaries.md",
            ["0002"] = "docs/adr/0002-phase-1a-identity-tenancy.md",
            ["0003"] = "docs/adr/0003-phase-1a-persistence-portability.md",
            ["0004"] = "docs/adr/0004-phase-1a-assistant-skill-channel-contracts.md",
            ["0005"] = "docs/adr/0005-phase-1a-self-host-operations.md",
        };

    private static readonly string[] RequiredFeedbackSecretClauses =
    [
        "A raw tracking secret is disclosed only to the requester at issuance and is never persisted",
        "Only a one-way verifier, the non-secret `trackingRef`, and metadata strictly necessary for expiry, failed-attempt throttling, recovery, rotation, and revocation may be stored.",
        "The raw secret never appears in a URL path, query, fragment, browser history, referrer, log, trace, metric, alert, queue field, provider metadata, backup, replica, export, analytics dataset, or support tool.",
    ];

    private static readonly string[] ForbiddenFeedbackSecretClauses =
    [
        "tracking secrets are stored outside the envelope",
        "tracking credential as separately protected records",
    ];

    public static IReadOnlySet<string> ExpectedStatusArtifacts => ExpectedStatusArtifactSet;

    public static IReadOnlyDictionary<string, string[]> CanonicalBaselineRequirements =>
        CanonicalBaselineRequirementsMap;

    public static IReadOnlyDictionary<string, string> CanonicalDocumentOpenAssessments =>
        CanonicalDocumentOpenAssessmentsMap;

    public static IReadOnlyDictionary<string, string> AcceptedPhase1AAdrs => AcceptedPhase1AAdrPaths;

    public static int RunAll(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        try
        {
            CheckPlanHash(repositoryRoot, output);
            CheckCharterHash(repositoryRoot, output);
            CheckFeedbackTrackingSecretContract(repositoryRoot, output);
            CheckStatusArtifactHashes(repositoryRoot, output);
            CheckPhase1AAdrHashes(repositoryRoot, output);
            CheckSkillCatalog(repositoryRoot, output);
            CheckConnectorCatalog(repositoryRoot, output);
            output.WriteLine("All documentation consistency checks passed.");
            return 0;
        }
        catch (InvalidDataException ex)
        {
            output.WriteLine($"::error::{ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            output.WriteLine($"::error::{ex.Message}");
            return 1;
        }
    }

    public static PlanHashMetadata ParsePlanHashMetadata(string adrText)
    {
        var normalized = RepositoryPaths.NormalizeLf(adrText);
        var decisionIndex = normalized.IndexOf("## Decision", StringComparison.Ordinal);
        var metadata = decisionIndex >= 0 ? normalized[..decisionIndex] : normalized;

        var present = new List<(string State, string Digest)>();
        var proposedMatches = ProposedPlanShaRegex().Matches(metadata);
        if (proposedMatches.Count > 1)
        {
            throw new InvalidDataException(
                "ADR 0000 must contain exactly one current, current-proposed, or legacy plan SHA-256 metadata line.");
        }

        if (proposedMatches.Count == 1)
        {
            present.Add(("proposed", LowerHex(proposedMatches[0].Groups[1].Value)));
        }

        var currentMatches = CurrentPlanShaRegex().Matches(metadata);
        if (currentMatches.Count > 1)
        {
            throw new InvalidDataException(
                "ADR 0000 must contain exactly one current, current-proposed, or legacy plan SHA-256 metadata line.");
        }

        if (currentMatches.Count == 1)
        {
            present.Add(("current", LowerHex(currentMatches[0].Groups[1].Value)));
        }

        var legacyMatches = LegacyPlanShaRegex().Matches(metadata);
        if (legacyMatches.Count > 1)
        {
            throw new InvalidDataException(
                "ADR 0000 must contain exactly one current, current-proposed, or legacy plan SHA-256 metadata line.");
        }

        if (legacyMatches.Count == 1)
        {
            present.Add(("legacy", LowerHex(legacyMatches[0].Groups[1].Value)));
        }

        if (present.Count != 1)
        {
            throw new InvalidDataException(
                "ADR 0000 must contain exactly one current, current-proposed, or legacy plan SHA-256 metadata line.");
        }

        var (state, activeHash) = present[0];
        var acceptedMatch = AcceptedPlanShaRegex().Match(metadata);
        var acceptedHash = acceptedMatch.Success ? LowerHex(acceptedMatch.Groups[1].Value) : null;

        var amendmentParts = AmendmentHeadingSplitRegex().Split(normalized);
        var classifiedSections = new List<(string Digest, bool IsProposed)>();
        for (var i = 1; i < amendmentParts.Length; i++)
        {
            var section = amendmentParts[i];
            var hashMatch = AmendmentPlanShaRegex().Match(section);
            if (!hashMatch.Success)
            {
                continue;
            }

            var approverMatch = AmendmentApproverRegex().Match(section);
            var classificationMatch = AmendmentClassificationRegex().Match(section);
            var approver = approverMatch.Success ? approverMatch.Groups[1].Value.Trim() : string.Empty;
            var classification = classificationMatch.Success ? classificationMatch.Groups[1].Value.Trim() : string.Empty;
            if (approver.Length == 0 || classification.Length == 0)
            {
                throw new InvalidDataException(
                    "Every hashed ADR 0000 amendment requires Approver and Classification fields.");
            }

            var isProposed = classification.StartsWith("proposed", StringComparison.OrdinalIgnoreCase);
            var hasPending = approver.Contains("pending", StringComparison.OrdinalIgnoreCase);
            var hasPendingGrammar = PendingApproverGrammarRegex().IsMatch(approver);
            if (isProposed && !hasPendingGrammar)
            {
                throw new InvalidDataException(
                    "A Proposed amendment Approver must use '<name>; **pending**'.");
            }

            if (!isProposed && hasPending)
            {
                throw new InvalidDataException(
                    "A pending Approver requires a Classification beginning with 'Proposed'.");
            }

            classifiedSections.Add((LowerHex(hashMatch.Groups[1].Value), isProposed));
        }

        if (classifiedSections.Count == 0)
        {
            throw new InvalidDataException("ADR 0000 has no hashed amendment record.");
        }

        if (string.Equals(state, "proposed", StringComparison.Ordinal))
        {
            if (acceptedHash is null)
            {
                throw new InvalidDataException(
                    "A current-proposed plan hash requires a separate accepted hash.");
            }

            var proposedSections = classifiedSections.Where(record => record.IsProposed).ToArray();
            if (proposedSections.Length == 0
                || !string.Equals(proposedSections[^1].Digest, activeHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The current-proposed plan hash must be backed by the latest pending, Proposed amendment with the same hash.");
            }

            var acceptedSections = classifiedSections.Where(record => !record.IsProposed).ToArray();
            if (acceptedSections.Length == 0
                || !string.Equals(acceptedSections[^1].Digest, acceptedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The accepted plan hash must match the latest accepted amendment.");
            }

            return new PlanHashMetadata(state, activeHash, acceptedHash);
        }

        var approvedSections = classifiedSections.Where(record => !record.IsProposed).ToArray();
        if (approvedSections.Length == 0
            || !string.Equals(approvedSections[^1].Digest, activeHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The current/legacy plan hash must match the latest explicitly approved amendment.");
        }

        return new PlanHashMetadata(state, activeHash, acceptedHash);
    }

    public static void CheckPlanHash(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var planText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/plan.md");
        var actualHash = RepositoryPaths.Sha256LfText(planText);
        var adrText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/adr/0000-plan-ratification.md");
        var metadata = ParsePlanHashMetadata(adrText);
        if (!string.Equals(metadata.ActiveHash, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "docs/plan.md has changed since ADR 0000 was ratified.\n" +
                $"  Recorded hash: {metadata.ActiveHash}\n" +
                $"  Actual hash:   {actualHash}\n" +
                "Log a proposed/accepted amendment (or re-ratify) in docs/adr/0000-plan-ratification.md with the new hash.");
        }

        output.WriteLine(
            $"OK: docs/plan.md content hash matches ADR 0000 ({metadata.State}; hash state does not imply approval).");
    }

    public static string CharterAdrStatus(string adrText)
    {
        var normalized = RepositoryPaths.NormalizeLf(adrText);
        var matches = CharterAdrStatusRegex().Matches(normalized);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                "docs/adr/0006-charter-ratification.md must contain exactly one '- **Status:** Proposed' or '- **Status:** Accepted' metadata line.");
        }

        return matches[0].Groups[1].Value;
    }

    public static string CharterDocumentStatus(string charterText)
    {
        var normalized = RepositoryPaths.NormalizeLf(charterText);
        var matches = CharterStatusRegex().Matches(normalized);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                "docs/charter.md must contain exactly one '- **Status:** <status>' metadata line.");
        }

        return matches[0].Groups[1].Value;
    }

    public static bool IssueFormFieldIsRequired(string formText, string fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        var lines = RepositoryPaths.SplitLines(formText);
        var matchingEntries = new List<string[]>();

        for (var index = 0; index < lines.Length; index++)
        {
            var entryMatch = IssueFormTypeLineRegex().Match(lines[index]);
            if (!entryMatch.Success)
            {
                continue;
            }

            var entryIndent = entryMatch.Groups[1].Value.Length;
            var end = index + 1;
            while (end < lines.Length)
            {
                var sibling = IssueFormSiblingTypeRegex().Match(lines[end]);
                if (sibling.Success && sibling.Groups[1].Value.Length == entryIndent)
                {
                    break;
                }

                end++;
            }

            var entry = lines[index..end];
            var idPattern = $@"^\s*id:\s*{Regex.Escape(fieldId)}\s*\z";
            if (entry.Any(item => Regex.IsMatch(item, idPattern, RegexOptions.CultureInvariant)))
            {
                matchingEntries.Add(entry);
            }
        }

        if (matchingEntries.Count != 1)
        {
            return false;
        }

        var targetEntry = matchingEntries[0];
        var targetIdPattern = $@"^\s*id:\s*{Regex.Escape(fieldId)}\s*\z";
        var idLine = targetEntry.First(item => Regex.IsMatch(item, targetIdPattern, RegexOptions.CultureInvariant));
        var fieldPropertyIndent = idLine.Length - idLine.TrimStart().Length;

        for (var index = 0; index < targetEntry.Length; index++)
        {
            var validationsMatch = IssueFormValidationsRegex().Match(targetEntry[index]);
            if (!validationsMatch.Success || validationsMatch.Groups[1].Value.Length != fieldPropertyIndent)
            {
                continue;
            }

            var validationsIndent = validationsMatch.Groups[1].Value.Length;
            for (var itemIndex = index + 1; itemIndex < targetEntry.Length; itemIndex++)
            {
                var item = targetEntry[itemIndex];
                var itemIndent = item.Length - item.TrimStart().Length;
                if (item.Trim().Length > 0 && itemIndent <= validationsIndent)
                {
                    break;
                }

                if (IssueFormRequiredTrueRegex().IsMatch(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string ExtractMarkdownSection(string text, string heading, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(sourceName);

        var normalized = RepositoryPaths.NormalizeLf(text);
        var headingTitle = heading.StartsWith("## ", StringComparison.Ordinal) ? heading[3..] : heading;
        var pattern = $@"(?m)^## {Regex.Escape(headingTitle)}[ \t]*$";
        var matches = Regex.Matches(normalized, pattern, RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException($"{sourceName} must contain exactly one '## {headingTitle}' section.");
        }

        var start = matches[0].Index + matches[0].Length;
        var rest = normalized[start..];
        var nextMatch = NextLevelTwoHeadingRegex().Match(rest);
        return nextMatch.Success ? rest[..nextMatch.Index] : rest;
    }

    public static string? MarkdownCheckboxText(string section, string labelPrefix)
    {
        ArgumentNullException.ThrowIfNull(labelPrefix);
        var lines = RepositoryPaths.SplitLines(section);
        var matches = new List<string>();
        var index = 0;
        while (index < lines.Length)
        {
            var item = UncheckedMarkdownCheckboxRegex().Match(lines[index]);
            if (!item.Success)
            {
                index++;
                continue;
            }

            var content = new List<string> { item.Groups[1].Value.Trim() };
            index++;
            while (index < lines.Length && !UncheckedMarkdownCheckboxPrefixRegex().IsMatch(lines[index]))
            {
                var trimmed = lines[index].Trim();
                if (trimmed.Length > 0)
                {
                    content.Add(trimmed);
                }

                index++;
            }

            var normalized = string.Join(" ", content);
            if (normalized.StartsWith(labelPrefix, StringComparison.Ordinal))
            {
                matches.Add(normalized);
            }
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    public static void CheckPrTemplateState(string prTemplateText, string adrStatus)
    {
        var normalized = RepositoryPaths.NormalizeLf(prTemplateText);
        var impactSection = ExtractMarkdownSection(
            normalized,
            "Charter impact",
            ".github/pull_request_template.md");
        var gatesSection = ExtractMarkdownSection(
            normalized,
            "Gates",
            ".github/pull_request_template.md");
        var stateMatches = PrCharterStateCommentRegex().Matches(impactSection);
        var marker = stateMatches.Count == 1 ? stateMatches[0].Groups[1].Value : null;
        var impactText = RepositoryPaths.NormalizeWhitespace(impactSection);
        var gateText = MarkdownCheckboxText(
            gatesSection,
            "Company charter impact recorded or not applicable");

        if (string.Equals(adrStatus, "Proposed", StringComparison.Ordinal))
        {
            if (!string.Equals(marker, "proposed", StringComparison.Ordinal)
                || !impactText.Contains(ProposedPrSentence, StringComparison.Ordinal)
                || !string.Equals(gateText, ProposedPrGate, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "While ADR 0006 is Proposed, .github/pull_request_template.md must retain the 'charter-impact-state: proposed' marker, the explicit optional-before/required-after wording, and the pre-ratification charter gate.");
            }

            return;
        }

        if (!string.Equals(marker, "required", StringComparison.Ordinal)
            || !impactText.Contains("This section is required.", StringComparison.Ordinal)
            || impactText.Contains("optional while ADR 0006 is Proposed", StringComparison.Ordinal)
            || !string.Equals(gateText, AcceptedPrGate, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "When ADR 0006 is Accepted, .github/pull_request_template.md must use the 'charter-impact-state: required' marker, state that the Charter impact section is required, and make its checklist gate unconditionally required.");
        }
    }

    public static void CheckReadmeAtomicityContent(string readmeText, string adrStatus)
    {
        var normalized = RepositoryPaths.NormalizeLf(readmeText);
        var hasPending = ReadmeCharterPendingRegex().IsMatch(normalized);
        var hasRatified = ReadmeCharterRatifiedRegex().IsMatch(normalized);
        if (string.Equals(adrStatus, "Proposed", StringComparison.Ordinal))
        {
            if (!hasPending || hasRatified)
            {
                throw new InvalidDataException(
                    "While ADR 0006 is Proposed, README.md must describe the charter as pending ratification and must not yet call it ratified.");
            }

            return;
        }

        if (hasPending || !hasRatified)
        {
            throw new InvalidDataException(
                "When ADR 0006 is Accepted, README.md must describe the charter as ratified and must no longer call it pending.");
        }
    }

    public static string CheckCharterAtomicityContent(
        string charterText,
        string adrText,
        string decisionFormText,
        string prTemplateText,
        string readmeText,
        TextWriter? output = null)
    {
        var adrStatus = CharterAdrStatus(adrText);
        var statusText = CharterDocumentStatus(charterText);
        var saysProposed = ProposedWordRegex().IsMatch(statusText);
        var saysNotAuthoritative = NotAuthoritativeRegex().IsMatch(statusText);

        if (string.Equals(adrStatus, "Proposed", StringComparison.Ordinal))
        {
            if (!saysProposed || !saysNotAuthoritative)
            {
                throw new InvalidDataException(
                    "While ADR 0006 is Proposed, docs/charter.md must remain explicitly Proposed and not authoritative.");
            }

            if (IssueFormFieldIsRequired(decisionFormText, "charter"))
            {
                throw new InvalidDataException(
                    "While ADR 0006 is Proposed, the 'charter' field in .github/ISSUE_TEMPLATE/decision.yml must not be required; that change belongs to the atomic acceptance pull request.");
            }

            CheckPrTemplateState(prTemplateText, adrStatus);
            CheckReadmeAtomicityContent(readmeText, adrStatus);
            output?.WriteLine(
                "OK: Proposed ADR 0006 keeps the charter non-authoritative, the decision form optional, README pending, and the PR template in its pre-ratification state.");
            return adrStatus;
        }

        if (saysProposed
            || saysNotAuthoritative
            || !AcceptedOrRatifiedLeadRegex().IsMatch(statusText))
        {
            throw new InvalidDataException(
                "When ADR 0006 is Accepted, docs/charter.md status must start with Accepted or Ratified and must no longer say Proposed or not authoritative.");
        }

        if (!IssueFormFieldIsRequired(decisionFormText, "charter"))
        {
            throw new InvalidDataException(
                "When ADR 0006 is Accepted, the 'charter' field in .github/ISSUE_TEMPLATE/decision.yml must have 'validations: required: true'.");
        }

        CheckPrTemplateState(prTemplateText, adrStatus);
        CheckReadmeAtomicityContent(readmeText, adrStatus);
        output?.WriteLine(
            "OK: Accepted ADR 0006 has atomic charter, decision-form, README, and PR-template state.");
        return adrStatus;
    }

    public static string? CheckCharterHashContent(
        string charterText,
        string adrText,
        TextWriter? output = null)
    {
        var status = CharterAdrStatus(adrText);
        if (string.Equals(status, "Proposed", StringComparison.Ordinal))
        {
            output?.WriteLine("OK: ADR 0006 is Proposed; charter hash enforcement is deferred.");
            return null;
        }

        var actualHash = RepositoryPaths.Sha256LfText(charterText);
        var normalizedAdr = RepositoryPaths.NormalizeLf(adrText);
        var hashMatches = CharterShaRegex().Matches(normalizedAdr);
        if (hashMatches.Count != 1)
        {
            throw new InvalidDataException(
                "Accepted ADR 0006 must contain exactly one '- **Charter SHA-256:** `<64-character hash>`' line.");
        }

        var recordedHash = LowerHex(hashMatches[0].Groups[1].Value);
        if (!string.Equals(recordedHash, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "docs/charter.md has changed since ADR 0006 was accepted.\n" +
                $"  Recorded hash: {recordedHash}\n" +
                $"  Actual hash:   {actualHash}\n" +
                "Log an amendment (or re-ratify) in docs/adr/0006-charter-ratification.md with the new hash.");
        }

        output?.WriteLine("OK: docs/charter.md hash matches Accepted ADR 0006.");
        return actualHash;
    }

    public static void CheckCharterHash(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var charterText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/charter.md");
        var adrText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/adr/0006-charter-ratification.md");
        var decisionFormText = RepositoryPaths.ReadLfText(repositoryRoot, ".github/ISSUE_TEMPLATE/decision.yml");
        var prTemplateText = RepositoryPaths.ReadLfText(repositoryRoot, ".github/pull_request_template.md");
        var readmeText = RepositoryPaths.ReadLfText(repositoryRoot, "README.md");

        CheckCharterAtomicityContent(
            charterText,
            adrText,
            decisionFormText,
            prTemplateText,
            readmeText,
            output);
        CheckCharterHashContent(charterText, adrText, output);
    }

    public static List<string[]> ExtractTableRows(
        string text,
        string sectionHeading,
        string headerPrefix)
    {
        ArgumentNullException.ThrowIfNull(sectionHeading);
        ArgumentNullException.ThrowIfNull(headerPrefix);

        var normalized = RepositoryPaths.NormalizeLf(text);
        var headingIndex = normalized.IndexOf(sectionHeading, StringComparison.Ordinal);
        if (headingIndex < 0)
        {
            throw new InvalidDataException($"Could not find section heading '{sectionHeading}'.");
        }

        var lines = RepositoryPaths.SplitLines(normalized[headingIndex..]);
        int? headerIndex = null;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().StartsWith(headerPrefix, StringComparison.Ordinal))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex is null)
        {
            throw new InvalidDataException(
                $"Could not find a table under '{sectionHeading}' with header starting with '{headerPrefix}'.");
        }

        var rows = new List<string[]>();
        var rowIndex = headerIndex.Value + 2; // skip header + separator row
        while (rowIndex < lines.Length && lines[rowIndex].Trim().StartsWith('|'))
        {
            rows.Add(SplitTableRow(lines[rowIndex].Trim()));
            rowIndex++;
        }

        return rows;
    }

    public static string FirstSegment(string cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var trimmed = cell.Trim().Trim('*').Trim('`');
        var dashIndex = trimmed.IndexOf('—');
        if (dashIndex >= 0)
        {
            trimmed = trimmed[..dashIndex].Trim();
        }

        return TrailingParentheticalRegex().Replace(trimmed, string.Empty).Trim();
    }

    public static Dictionary<string, string> ExtractStatusArtifactHashes(string planText)
    {
        var rows = ExtractTableRows(
            planText,
            "#### Phase 0 policy and governance artifact classification",
            "| Artifact | Source and current SHA-256 | Current authority |");
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Length < 2)
            {
                throw new InvalidDataException("A status-artifact row has fewer than two columns.");
            }

            var pathMatch = LinkedBacktickPathRegex().Match(row[0]);
            var hashMatch = InlineBacktickHex64Regex().Match(row[1]);
            if (!pathMatch.Success || !hashMatch.Success)
            {
                throw new InvalidDataException($"Malformed status-artifact row: {string.Join(" | ", row)}");
            }

            var path = pathMatch.Groups[1].Value;
            if (!artifacts.TryAdd(path, LowerHex(hashMatch.Groups[1].Value)))
            {
                throw new InvalidDataException($"Duplicate status-artifact row: {path}");
            }
        }

        return artifacts;
    }

    public static Dictionary<string, StatusArtifactRow> ExtractStatusArtifactCells(string planText)
    {
        var rows = ExtractTableRows(
            planText,
            "#### Phase 0 policy and governance artifact classification",
            "| Artifact | Source and current SHA-256 | Current authority |");
        var artifacts = new Dictionary<string, StatusArtifactRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Length != 3)
            {
                throw new InvalidDataException($"Malformed status-artifact row: {string.Join(" | ", row)}");
            }

            var pathMatch = LinkedBacktickPathRegex().Match(row[0]);
            if (!pathMatch.Success)
            {
                throw new InvalidDataException($"Malformed status-artifact row: {string.Join(" | ", row)}");
            }

            artifacts[pathMatch.Groups[1].Value] = new StatusArtifactRow(row[0], row[1], row[2]);
        }

        return artifacts;
    }

    public static void ValidateCanonicalBaselineRows(string planText)
    {
        var rows = ExtractStatusArtifactCells(planText);
        foreach (var (path, challengers) in CanonicalBaselineRequirementsMap)
        {
            if (!rows.TryGetValue(path, out var row))
            {
                throw new InvalidDataException($"Missing canonical baseline status row: {path}");
            }

            string[] requiredSourceParts = [CanonicalIssueSource, CanonicalPrSource];
            var missingSource = requiredSourceParts
                .Where(part => !row.SourceCell.Contains(part, StringComparison.Ordinal))
                .ToArray();

            var requiredAuthorityParts = new List<string>(challengers.Length + 3)
            {
                "Canonical descriptive baseline; not ratified",
            };
            requiredAuthorityParts.AddRange(challengers);
            requiredAuthorityParts.Add("Cyrus residual-risk acceptance remains pending");
            requiredAuthorityParts.Add(CanonicalOpenAssessment);

            var missingAuthority = requiredAuthorityParts
                .Where(part => !row.AuthorityCell.Contains(part, StringComparison.Ordinal))
                .ToArray();

            if (missingSource.Length > 0 || missingAuthority.Length > 0)
            {
                throw new InvalidDataException(
                    $"Canonical baseline authority drifted for {path}.\n" +
                    $"  Missing source text: [{string.Join(", ", missingSource)}]\n" +
                    $"  Missing authority text: [{string.Join(", ", missingAuthority)}]");
            }
        }
    }

    public static Dictionary<string, string> ParseInitialMetadataBlock(string documentText, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var lines = RepositoryPaths.SplitLines(documentText);
        if (lines.Length < 4
            || !lines[0].StartsWith("# ", StringComparison.Ordinal)
            || lines[1].Trim().Length > 0)
        {
            throw new InvalidDataException($"Canonical baseline metadata block malformed for {path}.");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        string? currentField = null;
        for (var index = 2; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Trim().Length == 0)
            {
                if (metadata.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Canonical baseline metadata block absent for {path}.");
                }

                return metadata;
            }

            var fieldMatch = BulletMetadataFieldLineRegex().Match(line);
            if (fieldMatch.Success)
            {
                currentField = fieldMatch.Groups[1].Value;
                var value = fieldMatch.Groups[2].Value.Trim();
                if (!metadata.TryAdd(currentField, value))
                {
                    throw new InvalidDataException(
                        $"Duplicate canonical baseline metadata field '{currentField}' for {path}.");
                }

                continue;
            }

            if (currentField is not null
                && (line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith('\t'))
                && line.Trim().Length > 0)
            {
                metadata[currentField] = RepositoryPaths.NormalizeWhitespace(
                    $"{metadata[currentField]} {line.Trim()}");
                continue;
            }

            throw new InvalidDataException(
                $"Canonical baseline metadata block malformed for {path}.");
        }

        throw new InvalidDataException(
            $"Canonical baseline metadata block has no terminating blank line for {path}.");
    }

    public static void ValidateCanonicalBaselineDocuments(IReadOnlyDictionary<string, string> documentTexts)
    {
        ArgumentNullException.ThrowIfNull(documentTexts);

        foreach (var (path, challengers) in CanonicalBaselineRequirementsMap)
        {
            if (!documentTexts.TryGetValue(path, out var text))
            {
                throw new InvalidDataException($"Missing canonical baseline content for {path}.");
            }

            var metadata = ParseInitialMetadataBlock(text, path);
            var requiredFields = new (string Field, string Expected)[]
            {
                ("Status", "Canonical descriptive baseline; not ratified"),
                ("Residual-risk acceptance", "Cyrus; pending"),
                ("Classification/impact assessment", CanonicalDocumentOpenAssessmentsMap[path]),
            };

            var driftedFields = requiredFields
                .Where(entry => !metadata.TryGetValue(entry.Field, out var actual)
                    || !string.Equals(actual, entry.Expected, StringComparison.Ordinal))
                .Select(entry => entry.Field)
                .ToArray();

            var challenge = metadata.GetValueOrDefault("Required challenge", string.Empty);
            var missingChallengers = challengers
                .Where(challenger => !challenge.Contains(challenger, StringComparison.Ordinal))
                .ToArray();

            if (driftedFields.Length > 0 || missingChallengers.Length > 0)
            {
                throw new InvalidDataException(
                    $"Canonical baseline header drifted for {path}.\n" +
                    $"  Missing or drifted fields: [{string.Join(", ", driftedFields)}]\n" +
                    $"  Missing required challengers: [{string.Join(", ", missingChallengers)}]");
            }
        }
    }

    public static void ValidateStatusArtifactHashes(
        IReadOnlyDictionary<string, string> artifacts,
        IReadOnlySet<string> expectedPaths,
        Func<string, string> digestForPath)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(expectedPaths);
        ArgumentNullException.ThrowIfNull(digestForPath);

        if (!expectedPaths.SetEquals(artifacts.Keys))
        {
            var missing = expectedPaths.Except(artifacts.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var unexpected = artifacts.Keys.Except(expectedPaths, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            throw new InvalidDataException(
                "Status-artifact inventory drifted.\n" +
                $"  Missing:    [{string.Join(", ", missing)}]\n" +
                $"  Unexpected: [{string.Join(", ", unexpected)}]");
        }

        foreach (var (path, recordedHash) in artifacts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var actualHash = LowerHex(digestForPath(path));
            if (!string.Equals(LowerHex(recordedHash), actualHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Status-artifact hash mismatch for {path}.\n" +
                    $"  Recorded hash: {recordedHash}\n" +
                    $"  Actual hash:   {actualHash}");
            }
        }
    }

    public static void ValidateFeedbackTrackingSecretContract(string documentText)
    {
        var normalized = RepositoryPaths.NormalizeWhitespace(documentText);
        var missing = RequiredFeedbackSecretClauses
            .Where(clause => !normalized.Contains(clause, StringComparison.Ordinal))
            .ToArray();

        var presentForbidden = ForbiddenFeedbackSecretClauses
            .Where(forbidden => normalized.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ForbiddenSecretPersistenceRegex().IsMatch(normalized))
        {
            presentForbidden.Add("raw tracking/receipt secret persistence");
        }

        if (missing.Length > 0 || presentForbidden.Count > 0)
        {
            throw new InvalidDataException(
                "Feedback tracking/receipt-secret custody contract drifted.\n" +
                $"  Missing required clauses: [{string.Join(", ", missing)}]\n" +
                $"  Forbidden persistence clauses: [{string.Join(", ", presentForbidden)}]");
        }
    }

    public static void CheckFeedbackTrackingSecretContract(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var feedbackText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/frameworks/feedback-support.md");
        ValidateFeedbackTrackingSecretContract(feedbackText);
        output.WriteLine("OK: feedback tracking and receipt secrets use verifier-only custody.");
    }

    public static void CheckStatusArtifactHashes(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var planText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/plan.md");
        var artifacts = ExtractStatusArtifactHashes(planText);
        ValidateStatusArtifactHashes(
            artifacts,
            ExpectedStatusArtifactSet,
            path => RepositoryPaths.Sha256Hex(RepositoryPaths.ReadGitLfBytes(repositoryRoot, path)));
        ValidateCanonicalBaselineRows(planText);

        var baselineDocs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in CanonicalBaselineRequirementsMap.Keys)
        {
            baselineDocs[path] = RepositoryPaths.ReadLfText(repositoryRoot, path);
        }

        ValidateCanonicalBaselineDocuments(baselineDocs);
        output.WriteLine($"OK: {artifacts.Count} status-artifact hashes match.");
    }

    public static Dictionary<string, string> ExtractPhase1AAdrHashes(string decisionText)
    {
        var normalized = RepositoryPaths.NormalizeLf(decisionText);

        (string Section, Dictionary<string, string> Hashes) ExtractSection(string heading)
        {
            var pattern = $@"(?m)^## {Regex.Escape(heading)}[ \t]*$";
            var sections = Regex.Split(normalized, pattern, RegexOptions.CultureInvariant);
            if (sections.Length != 2)
            {
                throw new InvalidDataException(
                    $"Phase 1A decision record must contain one {heading} section.");
            }

            var rest = sections[1];
            var nextHeading = NextLevelTwoHeadingWithSpaceRegex().Match(rest);
            var section = nextHeading.Success ? rest[..nextHeading.Index] : rest;
            var matches = Phase1AAdrRowRegex().Matches(section);
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in matches)
            {
                rows[match.Groups[1].Value] = LowerHex(match.Groups[2].Value);
            }

            if (matches.Count != AcceptedPhase1AAdrPaths.Count
                || !AcceptedPhase1AAdrPaths.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(rows.Keys))
            {
                throw new InvalidDataException(
                    $"{heading} must contain exactly one hash for ADRs 0001–0005.");
            }

            return (section, rows);
        }

        var (_, accepted) = ExtractSection("Content hashes");
        if (!CurrentProposedAdrHashesHeadingRegex().IsMatch(normalized))
        {
            return accepted;
        }

        var (proposedSection, proposed) = ExtractSection("Current proposed ADR hashes");
        var statusMarkers = ProposedNotAcceptedStatusRegex().Matches(proposedSection);
        if (statusMarkers.Count != 1)
        {
            throw new InvalidDataException("Proposed ADR hashes must be explicitly marked not accepted.");
        }

        return proposed;
    }

    public static void ValidateProposedPhase1AAdrCopies(
        string decisionText,
        IReadOnlyDictionary<string, string> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var normalized = RepositoryPaths.NormalizeLf(decisionText);
        if (!CurrentProposedAdrHashesHeadingRegex().IsMatch(normalized))
        {
            return;
        }

        foreach (var adr in AcceptedPhase1AAdrPaths.Keys)
        {
            if (!documents.TryGetValue(adr, out var docText))
            {
                throw new InvalidDataException($"ADR {adr} must identify its current copy as proposed, not accepted.");
            }

            var copyMarkers = ProposedEditorialCopyRegex().Matches(RepositoryPaths.NormalizeLf(docText));
            if (copyMarkers.Count != 1)
            {
                throw new InvalidDataException(
                    $"ADR {adr} must identify its current copy as proposed, not accepted.");
            }
        }
    }

    public static void CheckPhase1AAdrHashes(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var decisionText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/phase-1a/packet-decision-66.md");
        var recorded = ExtractPhase1AAdrHashes(decisionText);
        var adrTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (adr, path) in AcceptedPhase1AAdrPaths)
        {
            adrTexts[adr] = RepositoryPaths.ReadLfText(repositoryRoot, path);
        }

        ValidateProposedPhase1AAdrCopies(decisionText, adrTexts);
        ValidateStatusArtifactHashes(
            recorded,
            AcceptedPhase1AAdrPaths.Keys.ToHashSet(StringComparer.Ordinal),
            adr => RepositoryPaths.Sha256Hex(RepositoryPaths.ReadGitLfBytes(repositoryRoot, AcceptedPhase1AAdrPaths[adr])));
        output.WriteLine("OK: current Phase 1A ADR hashes match the decision record.");
    }

    public static void CheckSkillCatalog(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var planText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/plan.md");
        var skillsText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/roadmap/first-party-skills.md");

        var planRows = ExtractTableRows(
            planText,
            "## Initial first-party skill catalog",
            "| Skill | Initial capabilities |");
        var planSkills = planRows
            .Where(row => row.Length > 0 && row[0].Length > 0)
            .Select(row => FirstSegment(row[0]))
            .ToHashSet(StringComparer.Ordinal);

        var catalogRows = ExtractTableRows(
            skillsText,
            "## First-party skill catalog",
            "| # | Skill | Capability |");
        var catalogSkills = catalogRows
            .Where(row => row.Length > 1)
            .Select(row => FirstSegment(row[1]))
            .ToHashSet(StringComparer.Ordinal);

        var missingInCatalog = planSkills.Except(catalogSkills, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var missingInPlan = catalogSkills.Except(planSkills, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (missingInCatalog.Length > 0 || missingInPlan.Length > 0)
        {
            throw new InvalidDataException(
                "docs/plan.md's seed skill table and docs/roadmap/first-party-skills.md have drifted.\n" +
                $"  In plan.md but not in catalog: [{string.Join(", ", missingInCatalog)}]\n" +
                $"  In catalog but not in plan.md: [{string.Join(", ", missingInPlan)}]");
        }

        output.WriteLine($"OK: {catalogSkills.Count} first-party skills match between plan.md and the catalog.");
    }

    public static void CheckConnectorCatalog(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        var planText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/plan.md");
        var connectorsText = RepositoryPaths.ReadLfText(repositoryRoot, "docs/roadmap/channel-connectors.md");

        var planRows = ExtractTableRows(
            planText,
            "### Connector catalog and release bands",
            "| Category | Baseline connectors |");
        var planCategories = planRows
            .Where(row => row.Length > 0 && row[0].Length > 0)
            .Select(row => FirstSegment(row[0]))
            .ToHashSet(StringComparer.Ordinal);

        var catalogRows = ExtractTableRows(
            connectorsText,
            "## Channel catalog",
            "| # | Category | Capability |");
        var catalogCategories = catalogRows
            .Where(row => row.Length > 1)
            .Select(row => FirstSegment(row[1]))
            .ToHashSet(StringComparer.Ordinal);

        var missingInCatalog = planCategories.Except(catalogCategories, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var missingInPlan = catalogCategories.Except(planCategories, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (missingInCatalog.Length > 0 || missingInPlan.Length > 0)
        {
            throw new InvalidDataException(
                "docs/plan.md's seed connector table and docs/roadmap/channel-connectors.md have drifted.\n" +
                $"  In plan.md but not in catalog: [{string.Join(", ", missingInCatalog)}]\n" +
                $"  In catalog but not in plan.md: [{string.Join(", ", missingInPlan)}]");
        }

        output.WriteLine(
            $"OK: {catalogCategories.Count} connector categories match between plan.md and the catalog.");
    }

    private static string[] SplitTableRow(string strippedRow)
    {
        var inner = strippedRow.Trim('|');
        var rawCells = inner.Split('|');
        var trimmed = new string[rawCells.Length];
        for (var i = 0; i < rawCells.Length; i++)
        {
            trimmed[i] = rawCells[i].Trim();
        }

        return trimmed;
    }

    private static string LowerHex(string value)
    {
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.ToLowerInvariant(source[i]);
            }
        });
    }

    [GeneratedRegex(@"\*\*Current proposed Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex ProposedPlanShaRegex();

    [GeneratedRegex(@"\*\*Current Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentPlanShaRegex();

    [GeneratedRegex(@"\*\*Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyPlanShaRegex();

    [GeneratedRegex(@"\*\*Accepted Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex AcceptedPlanShaRegex();

    [GeneratedRegex(@"(?m)^### ", RegexOptions.CultureInvariant)]
    private static partial Regex AmendmentHeadingSplitRegex();

    [GeneratedRegex(@"(?m)^- \*\*Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex AmendmentPlanShaRegex();

    [GeneratedRegex(@"(?m)^- \*\*Approver:\*\*\s*([^\n]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex AmendmentApproverRegex();

    [GeneratedRegex(@"(?m)^- \*\*Classification:\*\*\s*([^\n]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex AmendmentClassificationRegex();

    [GeneratedRegex(@"^.+;\s*\*\*pending\*\*\s*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PendingApproverGrammarRegex();

    [GeneratedRegex(@"(?m)^- \*\*Status:\*\*\s*(Proposed|Accepted)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CharterAdrStatusRegex();

    [GeneratedRegex(@"(?m)^- \*\*Status:\*\*\s*([^\n]+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CharterStatusRegex();

    [GeneratedRegex(@"(?m)^- \*\*Charter SHA-256:\*\*\s*`([0-9a-fA-F]{64})`\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CharterShaRegex();

    [GeneratedRegex(@"^(\s*)-\s+type:\s*\S+\s*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IssueFormTypeLineRegex();

    [GeneratedRegex(@"^(\s*)-\s+type:", RegexOptions.CultureInvariant)]
    private static partial Regex IssueFormSiblingTypeRegex();

    [GeneratedRegex(@"^(\s*)validations:\s*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IssueFormValidationsRegex();

    [GeneratedRegex(@"^\s*required:\s*true\s*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IssueFormRequiredTrueRegex();

    [GeneratedRegex(@"(?m)^##\s+", RegexOptions.CultureInvariant)]
    private static partial Regex NextLevelTwoHeadingRegex();

    [GeneratedRegex(@"(?m)^## ", RegexOptions.CultureInvariant)]
    private static partial Regex NextLevelTwoHeadingWithSpaceRegex();

    [GeneratedRegex(@"^- \[ \]\s+(.+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex UncheckedMarkdownCheckboxRegex();

    [GeneratedRegex(@"^- \[ \]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex UncheckedMarkdownCheckboxPrefixRegex();

    [GeneratedRegex(@"<!--\s*charter-impact-state:\s*(proposed|required)\s*-->", RegexOptions.CultureInvariant)]
    private static partial Regex PrCharterStateCommentRegex();

    [GeneratedRegex(@"charter[^.]*\bpending\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReadmeCharterPendingRegex();

    [GeneratedRegex(@"charter[^.]*\bratified\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReadmeCharterRatifiedRegex();

    [GeneratedRegex(@"\bproposed\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProposedWordRegex();

    [GeneratedRegex(@"\bnot\s+(?:yet\s+)?authoritative\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotAuthoritativeRegex();

    [GeneratedRegex(@"^(?:accepted|ratified)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AcceptedOrRatifiedLeadRegex();

    [GeneratedRegex(@"\[`([^`]+)`\]\([^)]+\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkedBacktickPathRegex();

    [GeneratedRegex(@"`([0-9a-fA-F]{64})`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineBacktickHex64Regex();

    [GeneratedRegex(@"^- \*\*([^*]+):\*\*\s+(\S.*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex BulletMetadataFieldLineRegex();

    [GeneratedRegex(@"\s*\([^)]*\)\s*\z", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingParentheticalRegex();

    [GeneratedRegex(@"\braw\s+(?:tracking|receipt)[ -]secrets?\b(?![^.!?]{0,120}\b(?:never|not|no|without)\b)[^.!?]{0,120}\b(?:persist\w*|stor(?:e|ed|es|ing|age)|retain\w*|sav\w*|writ\w*|record\w*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenSecretPersistenceRegex();

    [GeneratedRegex(@"(?m)^## Current proposed ADR hashes[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentProposedAdrHashesHeadingRegex();

    [GeneratedRegex(@"(?m)^\|\s*(000[1-5])\s*\|\s*`([0-9a-fA-F]{64})`\s*\|$", RegexOptions.CultureInvariant)]
    private static partial Regex Phase1AAdrRowRegex();

    [GeneratedRegex(@"(?m)^- \*\*Status:\*\* Proposed; not accepted[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProposedNotAcceptedStatusRegex();

    [GeneratedRegex(@"(?m)^- \*\*Current copy:\*\* Proposed editorial update; not accepted[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProposedEditorialCopyRegex();
}
