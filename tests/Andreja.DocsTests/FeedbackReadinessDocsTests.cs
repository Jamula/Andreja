using System.Text.RegularExpressions;
using Andreja.Docs;

namespace Andreja.DocsTests;

[TestClass]
public sealed class FeedbackReadinessDocsTests
{
    private readonly string _repoRoot = RepositoryPaths.FindRoot();

    [TestMethod]
    public void PacketContractCurrentRepositoryHasNoErrors()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void MissingScenarioIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.ScenariosPath] = Regex.Replace(
            docs[FeedbackReadinessValidator.ScenariosPath],
            @"(?ms)^## S04 .*?(?=^## S05 )",
            string.Empty);

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        CollectionAssert.Contains(errors.ToArray(), "scenario inventory must be exactly S01-S14 in order");
    }

    [TestMethod]
    public void ReducedRequirementIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.ScenariosPath] = ReplaceFirst(
            docs[FeedbackReadinessValidator.ScenariosPath],
            "> proportionate identity/relationship proof, conflicting instructions,",
            "> proportionate identity/relationship proof,");

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        CollectionAssert.Contains(errors.ToArray(), "S04: complete framework requirement differs");
    }

    [TestMethod]
    public void CompletedDrillClaimReportsMissingNotRunRegister()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.RunbooksPath] = ReplaceFirst(
            docs[FeedbackReadinessValidator.RunbooksPath],
            "| REQUIRED / NOT RUN |",
            "| PASSED |");

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        CollectionAssert.Contains(errors.ToArray(), "all 11 runbooks need cadence and NOT RUN exercise register");
    }

    [TestMethod]
    public void UnsafeDefaultIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.PacketPath] = ReplaceFirst(
            docs[FeedbackReadinessValidator.PacketPath],
            "disabled/off/no-collection/deny",
            "enabled/on/collect/allow");

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        Assert.IsTrue(
            errors.Any(error => error.Contains(
                "missing safety statement disabled/off/no-collection/deny",
                StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void BrokenLocalLinkIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.HelpPath] = ReplaceFirst(
            docs[FeedbackReadinessValidator.HelpPath],
            "../frameworks/feedback-readiness.md",
            "../frameworks/feedback-missing.md");

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        Assert.IsTrue(
            errors.Any(error =>
                error.Contains("broken local link", StringComparison.Ordinal)
                && error.Contains("feedback-missing.md", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void BrokenAnchorIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.ScenariosPath] = ReplaceFirst(
            docs[FeedbackReadinessValidator.ScenariosPath],
            "#r01-intake-health-backpressure-and-recovery",
            "#r01-missing");

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        Assert.IsTrue(
            errors.Any(error =>
                error.Contains("broken anchor", StringComparison.Ordinal)
                && error.Contains("r01-missing", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void UnreviewedExternalLinkIsRejected()
    {
        var docs = FeedbackReadinessValidator.LoadDocuments(_repoRoot);
        docs[FeedbackReadinessValidator.HelpPath] += "\nUnapproved [vendor](https://status.example.invalid).\n";

        var errors = FeedbackReadinessValidator.ValidateContractErrors(_repoRoot, docs);

        Assert.IsTrue(
            errors.Any(error => error.Contains("unreviewed external link", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void CliCheckFeedbackReadinessCurrentRepositoryExitsZero()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = Program.Execute(["check-feedback-readiness"], stdout, stderr, _repoRoot);

        Assert.AreEqual(0, exitCode, stdout + stderr.ToString());
        Assert.Contains("OK: feedback readiness documentation packet checks passed.", stdout.ToString());
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, index, $"Expected to find '{oldValue}' in document.");
        return string.Concat(text.AsSpan(0, index), newValue, text.AsSpan(index + oldValue.Length));
    }
}
