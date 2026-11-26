using System.Diagnostics;
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
    public void OutOfRootSymlinkOrReparseLinkIsRejectedWithoutReadingExternalFile()
    {
        var workspace = Path.Join(Path.GetTempPath(), $"andreja-docs-symlink-{Guid.NewGuid():N}");
        var tempRepo = Path.Join(workspace, "repo");
        var outsideDir = Path.Join(workspace, "outside");
        var nestedOutsideDir = Path.Join(outsideDir, "nested");
        Directory.CreateDirectory(nestedOutsideDir);

        try
        {
            PopulateFeedbackPacketCopy(_repoRoot, tempRepo);
            var outsideSecret = Path.Join(outsideDir, "secret.md");
            File.WriteAllText(outsideSecret, "# Secret Heading\n\nExternal content.\n");

            var baselineDocs = FeedbackReadinessValidator.LoadDocuments(tempRepo);
            Assert.IsEmpty(FeedbackReadinessValidator.ValidateContractErrors(tempRepo, baselineDocs));

            var linkDir = Path.Join(tempRepo, "docs", "help", "escape-dir");
            CreateDirectoryLinkOrJunction(linkDir, outsideDir);
            Assert.IsNotNull(new DirectoryInfo(linkDir).LinkTarget);
            Assert.IsTrue(File.Exists(Path.Join(linkDir, "secret.md")));

            var dirEscapeDocs = new Dictionary<string, string>(baselineDocs, StringComparer.Ordinal)
            {
                [FeedbackReadinessValidator.HelpPath] =
                    baselineDocs[FeedbackReadinessValidator.HelpPath]
                    + "\nEscaped [dir](escape-dir/secret.md#secret-heading).\n",
            };
            var dirEscapeErrors = FeedbackReadinessValidator.ValidateContractErrors(tempRepo, dirEscapeDocs);
            Assert.IsTrue(
                dirEscapeErrors.Any(error =>
                    error.Contains("broken local link escape-dir/secret.md#secret-heading", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, dirEscapeErrors));
            Assert.IsFalse(
                dirEscapeErrors.Any(error => error.Contains("broken anchor", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, dirEscapeErrors));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                RepositoryPaths.ResolveRepositoryPath(tempRepo, "docs/help/escape-dir/secret.md"));

            if (!OperatingSystem.IsWindows())
            {
                var linkFile = Path.Join(tempRepo, "docs", "help", "escape-file.md");
                File.CreateSymbolicLink(linkFile, Path.Join("..", "..", "..", "outside", "secret.md"));

                var fileEscapeDocs = new Dictionary<string, string>(baselineDocs, StringComparer.Ordinal)
                {
                    [FeedbackReadinessValidator.HelpPath] =
                        baselineDocs[FeedbackReadinessValidator.HelpPath]
                        + "\nEscaped [file](escape-file.md#secret-heading).\n",
                };
                var fileEscapeErrors = FeedbackReadinessValidator.ValidateContractErrors(tempRepo, fileEscapeDocs);
                Assert.IsTrue(
                    fileEscapeErrors.Any(error =>
                        error.Contains("broken local link escape-file.md#secret-heading", StringComparison.Ordinal)),
                    string.Join(Environment.NewLine, fileEscapeErrors));
            }

            Assert.IsTrue(RepositoryPaths.TryResolveRealPath(tempRepo, out var resolvedTempRepo));
            Assert.IsTrue(RepositoryPaths.TryResolveRealPath(outsideDir, out var resolvedOutsideDir));
            var virtualFileLink = Path.Join(resolvedTempRepo, "docs", "help", "virtual-escape.md");
            var virtualNestedLink = Path.Join(resolvedTempRepo, "docs", "help", "virtual-nested");
            var virtualLoopA = Path.Join(resolvedTempRepo, "docs", "help", "loop-a.md");
            var virtualLoopB = Path.Join(resolvedTempRepo, "docs", "help", "loop-b.md");

            string? ResolveSimulatedLinks(string candidate)
            {
                if (string.Equals(candidate, virtualFileLink, RepositoryPaths.PathComparison))
                {
                    return Path.Join("..", "..", "..", "outside", "secret.md");
                }

                if (string.Equals(candidate, virtualNestedLink, RepositoryPaths.PathComparison))
                {
                    return Path.Join(resolvedOutsideDir, "nested");
                }

                if (string.Equals(candidate, virtualLoopA, RepositoryPaths.PathComparison))
                {
                    return "loop-b.md";
                }

                if (string.Equals(candidate, virtualLoopB, RepositoryPaths.PathComparison))
                {
                    return "loop-a.md";
                }

                return null;
            }

            var simulatedDocs = new Dictionary<string, string>(baselineDocs, StringComparer.Ordinal)
            {
                [FeedbackReadinessValidator.HelpPath] =
                    baselineDocs[FeedbackReadinessValidator.HelpPath]
                    + "\n[rel-file](virtual-escape.md#secret-heading)"
                    + "\n[parent-after-link](virtual-nested/../secret.md#secret-heading)"
                    + "\n[rooted](/outside/secret.md#secret-heading)"
                    + "\n[loop](loop-a.md#secret-heading)\n",
            };
            var simulatedErrors = FeedbackReadinessValidator.ValidateContractErrors(
                tempRepo,
                simulatedDocs,
                ResolveSimulatedLinks);
            Assert.HasCount(4, simulatedErrors, string.Join(Environment.NewLine, simulatedErrors));
            Assert.IsTrue(
                simulatedErrors.All(error => error.Contains("broken local link", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, simulatedErrors));
        }
        finally
        {
            var linkDir = Path.Join(tempRepo, "docs", "help", "escape-dir");
            if (Directory.Exists(linkDir))
            {
                Directory.Delete(linkDir, recursive: false);
            }

            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ContainmentUsesPlatformCaseSensitivityAndRejectsCaseMismatchedRootUnderOrdinal()
    {
        var expectedComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        Assert.AreEqual(expectedComparison, RepositoryPaths.PathComparison);

        var syntheticParent = Path.Join(Path.GetTempPath(), $"andreja-case-{Guid.NewGuid():N}");
        var lowerRoot = Path.Join(syntheticParent, "repo");
        var upperSiblingFile = Path.Join(syntheticParent, "REPO", "secret.md");
        var prefixCollisionFile = Path.Join(syntheticParent, "repo-other", "secret.md");
        var validChildFile = Path.Join(lowerRoot, "docs", "help", "feedback.md");

        Assert.IsTrue(RepositoryPaths.IsWithinRoot(validChildFile, lowerRoot, StringComparison.Ordinal));
        Assert.IsFalse(RepositoryPaths.IsWithinRoot(upperSiblingFile, lowerRoot, StringComparison.Ordinal));
        Assert.IsFalse(RepositoryPaths.IsWithinRoot(prefixCollisionFile, lowerRoot, StringComparison.Ordinal));
        Assert.IsFalse(RepositoryPaths.IsWithinRoot(prefixCollisionFile, lowerRoot, StringComparison.OrdinalIgnoreCase));

        Directory.CreateDirectory(lowerRoot);
        var upperSiblingRoot = Path.Join(syntheticParent, "REPO");
        Directory.CreateDirectory(upperSiblingRoot);

        try
        {
            var probeInLower = Path.Join(lowerRoot, "case-probe.txt");
            File.WriteAllText(probeInLower, "probe\n");
            var isCaseSensitive = !File.Exists(Path.Join(upperSiblingRoot, "case-probe.txt"));
            File.Delete(probeInLower);

            if (isCaseSensitive)
            {
                PopulateFeedbackPacketCopy(_repoRoot, lowerRoot);
                File.WriteAllText(Path.Join(upperSiblingRoot, "secret.md"), "# Secret Heading\n");

                var docs = FeedbackReadinessValidator.LoadDocuments(lowerRoot);
                docs[FeedbackReadinessValidator.HelpPath] += "\nSibling [case](../../../REPO/secret.md#secret-heading).\n";

                var errors = FeedbackReadinessValidator.ValidateContractErrors(lowerRoot, docs);
                Assert.IsTrue(
                    errors.Any(error =>
                        error.Contains("broken local link ../../../REPO/secret.md#secret-heading", StringComparison.Ordinal)),
                    string.Join(Environment.NewLine, errors));
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    RepositoryPaths.ResolveRepositoryPath(lowerRoot, "../REPO/secret.md"));
            }
        }
        finally
        {
            if (Directory.Exists(syntheticParent))
            {
                Directory.Delete(syntheticParent, recursive: true);
            }
        }
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

    private static void PopulateFeedbackPacketCopy(string sourceRepoRoot, string destinationRepoRoot)
    {
        string[] relativePaths =
        [
            FeedbackReadinessValidator.PacketPath,
            FeedbackReadinessValidator.ScenariosPath,
            FeedbackReadinessValidator.RunbooksPath,
            FeedbackReadinessValidator.HelpPath,
            FeedbackReadinessValidator.FrameworkPath,
            "docs/frameworks/feedback-security-abuse-incident-routing.md",
            "docs/frameworks/feedback-security-156-decision-supplement.md",
            "docs/legal/feedback-privacy-retention-dsr.md",
            "docs/frameworks/feedback-platform-decision.md",
        ];

        foreach (var relativePath in relativePaths)
        {
            var sourcePath = RepositoryPaths.ResolveRepositoryPath(sourceRepoRoot, relativePath);
            var destinationPath = Path.Join(destinationRepoRoot, relativePath);
            var destinationDir = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(destinationDir);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void CreateDirectoryLinkOrJunction(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return;
        }

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/J");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(targetPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start cmd.exe for mklink /J.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, $"mklink /J failed: {stdout}{stderr}");
        }
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, index, $"Expected to find '{oldValue}' in document.");
        return string.Concat(text.AsSpan(0, index), newValue, text.AsSpan(index + oldValue.Length));
    }
}
