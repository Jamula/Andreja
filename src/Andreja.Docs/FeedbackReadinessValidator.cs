using System.Globalization;
using System.Text.RegularExpressions;

namespace Andreja.Docs;

public static partial class FeedbackReadinessValidator
{
    public const string PacketPath = "docs/frameworks/feedback-readiness.md";
    public const string ScenariosPath = "docs/frameworks/feedback-readiness-scenarios.md";
    public const string RunbooksPath = "docs/runbooks/feedback-operations.md";
    public const string HelpPath = "docs/help/feedback.md";
    public const string FrameworkPath = "docs/frameworks/feedback-support.md";

    private static readonly string[] DocumentPaths =
    [
        PacketPath,
        ScenariosPath,
        RunbooksPath,
        HelpPath,
    ];

    private static readonly string[] RequiredScenarioFields =
    [
        "**Fixture/setup:**",
        "**Action:**",
        "**Observable pass assertions:**",
        "**Negative/edge:**",
        "**Failure evidence:**",
        "**Owner:**",
        "**Status:** NOT RUN.",
    ];

    private static readonly string[] RequiredRunbookFields =
    [
        "**Prerequisites:**",
        "1. ",
        "**Evidence:**",
        "**Fail/stop:**",
        "**Recovery:**",
    ];

    private static readonly string[] RequiredPacketSafetyPhrases =
    [
        "disabled/off/no-collection/deny",
        "UNSATISFIED BY TEMPLATES ALONE",
        "Current record: MISSING. No exercise is authorized.",
        "NEW explicit authorization",
        "no later than 14 days after issuance",
        "qualified-counsel",
        "documented binding",
        "proceed / extend-learning / de-scope / stop",
    ];

    private static readonly string[] RequiredHelpSafetyPhrases =
    [
        "not active",
        "operational/accessibility review: NOT RUN",
        "Silence is never verification.",
        "non-user subjects",
    ];

    public static IReadOnlyList<string> RequiredDocumentPaths => DocumentPaths;

    public static Dictionary<string, string> LoadDocuments(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        var docs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in DocumentPaths)
        {
            docs[path] = RepositoryPaths.ReadLfText(repositoryRoot, path);
        }

        return docs;
    }

    public static IReadOnlyList<string> ExtractFrameworkScenarios(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        var text = RepositoryPaths.ReadLfText(repositoryRoot, FrameworkPath);
        const string startMarker = "## Phase 1B acceptance evidence\n";
        const string endMarker = "\nSupport lead, Privacy lead";

        var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            throw new InvalidDataException($"Missing '{startMarker.Trim()}' in {FrameworkPath}");
        }

        var afterStart = text[(startIndex + startMarker.Length)..];
        var endIndex = afterStart.IndexOf(endMarker, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new InvalidDataException($"Missing acceptance evidence footer in {FrameworkPath}");
        }

        var section = afterStart[..endIndex];
        var items = new List<string>();
        foreach (Match match in NumberedItemRegex().Matches(section))
        {
            items.Add(RepositoryPaths.NormalizeWhitespace(match.Groups[1].Value));
        }

        return items;
    }

    public static IReadOnlyList<string> ValidateContractErrors(
        string repositoryRoot,
        IReadOnlyDictionary<string, string> documents,
        Func<string, string?>? linkTargetResolver = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(documents);

        var errors = new List<string>();
        var expected = ExtractFrameworkScenarios(repositoryRoot);
        var scenariosText = RepositoryPaths.NormalizeLf(documents[ScenariosPath]);
        var scenarioMatches = ScenarioSectionRegex().Matches(scenariosText);
        var numbers = scenarioMatches
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        if (!numbers.SequenceEqual(Enumerable.Range(1, 14)))
        {
            errors.Add("scenario inventory must be exactly S01-S14 in order");
        }

        if (expected.Count != 14)
        {
            errors.Add("framework scenario inventory changed; review mapping");
        }

        foreach (Match match in scenarioMatches)
        {
            var numberText = match.Groups[1].Value;
            var index = int.Parse(numberText, CultureInfo.InvariantCulture) - 1;
            var code = $"S{numberText}";
            var body = match.Groups[3].Value;
            var quoteLines = BlockquoteLineRegex().Matches(body)
                .Select(quote => quote.Groups[1].Value);
            var quote = RepositoryPaths.NormalizeWhitespace(string.Join("\n", quoteLines));
            if (index < 0 || index >= expected.Count || !string.Equals(quote, expected[index], StringComparison.Ordinal))
            {
                errors.Add($"{code}: complete framework requirement differs");
            }

            foreach (var field in RequiredScenarioFields.Where(field => !body.Contains(field, StringComparison.Ordinal)))
            {
                errors.Add($"{code}: missing {field}");
            }

            if (!body.Contains("../runbooks/feedback-operations.md", StringComparison.Ordinal))
            {
                errors.Add($"{code}: missing runbook link");
            }
        }

        var runbooksText = RepositoryPaths.NormalizeLf(documents[RunbooksPath]);
        var runbookMatches = RunbookSectionRegex().Matches(runbooksText);
        var runbookNumbers = runbookMatches
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
        if (!runbookNumbers.SequenceEqual(Enumerable.Range(1, 11)))
        {
            errors.Add("runbook inventory must be exactly R01-R11 in order");
        }

        foreach (Match match in runbookMatches)
        {
            var code = $"R{match.Groups[1].Value}";
            var body = match.Groups[2].Value;
            foreach (var field in RequiredRunbookFields.Where(field => !body.Contains(field, StringComparison.Ordinal)))
            {
                errors.Add($"{code}: missing {field}");
            }
        }

        var registerRows = RequiredNotRunRowRegex().Matches(runbooksText);
        if (registerRows.Count != 11)
        {
            errors.Add("all 11 runbooks need cadence and NOT RUN exercise register");
        }

        var packetText = RepositoryPaths.NormalizeLf(documents[PacketPath]);
        foreach (var phrase in RequiredPacketSafetyPhrases.Where(phrase => !packetText.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"{PacketPath}: missing safety statement {phrase}");
        }

        var helpText = RepositoryPaths.NormalizeLf(documents[HelpPath]);
        foreach (var phrase in RequiredHelpSafetyPhrases.Where(phrase => !helpText.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"{HelpPath}: missing safety statement {phrase}");
        }

        if (!RepositoryPaths.TryResolveRealPath(repositoryRoot, out var resolvedRoot, linkTargetResolver: linkTargetResolver))
        {
            throw new InvalidDataException($"Could not resolve repository root: {repositoryRoot}");
        }

        foreach (var (path, rawText) in documents)
        {
            var normalizedText = RepositoryPaths.NormalizeLf(rawText);
            if (Path.IsPathRooted(path)
                || !RepositoryPaths.TryResolveRealPath(
                    Path.Join(resolvedRoot, path),
                    out var sourceFile,
                    disallowedOpaqueReparseRoot: resolvedRoot,
                    linkTargetResolver: linkTargetResolver)
                || !RepositoryPaths.IsWithinRoot(sourceFile, resolvedRoot))
            {
                throw new InvalidDataException($"Document path escapes repository root: {path}");
            }

            var sourceDir = Path.GetDirectoryName(sourceFile)
                ?? throw new InvalidDataException($"Could not determine parent directory for {path}");

            foreach (Match linkMatch in MarkdownLinkRegex().Matches(normalizedText))
            {
                var link = linkMatch.Groups[1].Value;
                if (UriSchemePrefixRegex().IsMatch(link))
                {
                    if (!link.StartsWith("https://github.com/Jamula/Andreja/", StringComparison.Ordinal))
                    {
                        errors.Add($"{path}: unreviewed external link {link}");
                    }

                    continue;
                }

                var hashIndex = link.IndexOf('#');
                var filename = hashIndex >= 0 ? link[..hashIndex] : link;
                var fragment = hashIndex >= 0 ? link[(hashIndex + 1)..] : string.Empty;

                string target;
                if (filename.Length == 0)
                {
                    target = sourceFile;
                }
                else if (Path.IsPathRooted(filename)
                    || !RepositoryPaths.TryResolveRealPath(
                        Path.Join(sourceDir, filename),
                        out target,
                        disallowedOpaqueReparseRoot: resolvedRoot,
                        linkTargetResolver: linkTargetResolver))
                {
                    errors.Add($"{path}: broken local link {link}");
                    continue;
                }

                if (!RepositoryPaths.IsWithinRoot(target, resolvedRoot) || !File.Exists(target))
                {
                    errors.Add($"{path}: broken local link {link}");
                    continue;
                }

                if (fragment.Length > 0)
                {
                    var targetText = RepositoryPaths.NormalizeLf(File.ReadAllText(target));
                    var anchors = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Match headingMatch in MarkdownHeadingRegex().Matches(targetText))
                    {
                        anchors.Add(GithubHeadingSlug(headingMatch.Groups[1].Value));
                    }

                    if (!anchors.Contains(fragment))
                    {
                        errors.Add($"{path}: broken anchor {link}");
                    }
                }
            }
        }

        return errors;
    }

    public static int RunCheck(string repositoryRoot, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);

        try
        {
            var docs = LoadDocuments(repositoryRoot);
            var errors = ValidateContractErrors(repositoryRoot, docs);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    output.WriteLine($"::error::{error}");
                }

                return 1;
            }

            output.WriteLine("OK: feedback readiness documentation packet checks passed.");
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

    private static string GithubHeadingSlug(string heading)
    {
        var lowered = string.Create(heading.Length, heading, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.ToLowerInvariant(source[i]);
            }
        });

        var stripped = NonSlugCharacterRegex().Replace(lowered, string.Empty);
        return stripped.Replace(' ', '-');
    }

    [GeneratedRegex(@"(?ms)^\d+\. (.*?)(?=(?:^\d+\. )|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedItemRegex();

    [GeneratedRegex(@"(?ms)^## S(\d{2}) ([^\n]+)\n(.*?)(?=(?:^## S\d{2} )|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioSectionRegex();

    [GeneratedRegex(@"(?m)^> ?([^\n]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockquoteLineRegex();

    [GeneratedRegex(@"(?ms)^## R(\d{2}) [^\n]+\n(.*?)(?=(?:^## R\d{2} )|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex RunbookSectionRegex();

    [GeneratedRegex(@"(?m)^\| R\d{2} [^\n]+\| REQUIRED / NOT RUN \|$", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredNotRunRowRegex();

    [GeneratedRegex(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemePrefixRegex();

    [GeneratedRegex(@"(?m)^#{1,6} ([^\n]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"[^\w\- ]", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugCharacterRegex();
}
