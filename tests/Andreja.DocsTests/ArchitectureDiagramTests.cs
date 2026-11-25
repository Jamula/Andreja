using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Andreja.Docs;

namespace Andreja.DocsTests;

[TestClass]
public sealed class ArchitectureDiagramTests
{
    private readonly string _repoRoot = RepositoryPaths.FindRoot();
    private string _scratch = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _scratch = Path.Join(_repoRoot, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    [TestMethod]
    public void CommittedArtifactsCheckCliAndByteParityMatchGeneratorOutput()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var diagram = ArchitectureDiagramTool.BuildDiagram();
        var expectedExcalidraw = ArchitectureDiagramTool.ExcalidrawBytes(diagram);
        var sourceHash = RepositoryPaths.Sha256Hex(expectedExcalidraw);
        var expectedSvg = ArchitectureDiagramTool.SvgBytes(diagram, sourceHash);

        var committedExcalidraw = RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath));
        var committedSvg = RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath));
        CollectionAssert.AreEqual(expectedExcalidraw, committedExcalidraw);
        CollectionAssert.AreEqual(expectedSvg, committedSvg);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = Program.Execute(["architecture-diagram", "--check"], stdout, stderr, _repoRoot);

        Assert.AreEqual(0, exitCode, stdout + stderr.ToString());
        Assert.Contains("Architecture artifacts are consistent", stdout.ToString());
    }

    [TestMethod]
    public void DiagramStructureTrustBoundariesFlowsAndGatedControlsMatchContract()
    {
        var diagram = ArchitectureDiagramTool.BuildDiagram();
        using var document = JsonDocument.Parse(ArchitectureDiagramTool.ExcalidrawBytes(diagram));
        var root = document.RootElement;

        Assert.AreEqual("excalidraw", root.GetProperty("type").GetString());
        Assert.AreEqual(2, root.GetProperty("version").GetInt32());

        var texts = new List<string>();
        var textElements = new List<(string NormalizedText, int X)>();
        var rectangleStyles = new HashSet<string>(StringComparer.Ordinal);
        var flows = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var element in root.GetProperty("elements").EnumerateArray())
        {
            var type = element.GetProperty("type").GetString();
            if (string.Equals(type, "text", StringComparison.Ordinal))
            {
                var rawText = element.GetProperty("text").GetString() ?? string.Empty;
                texts.Add(rawText);
                var normalized = string.Join(" ", rawText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                textElements.Add((normalized, element.GetProperty("x").GetInt32()));
            }
            else if (string.Equals(type, "rectangle", StringComparison.Ordinal))
            {
                rectangleStyles.Add(element.GetProperty("strokeStyle").GetString() ?? string.Empty);
            }
            else if (string.Equals(type, "arrow", StringComparison.Ordinal))
            {
                var flowId = element.GetProperty("customData").GetProperty("flowId").GetString() ?? string.Empty;
                flows[flowId] = element;
            }
        }

        var joinedText = string.Join("\n", texts);
        var normalizedVisibleText = string.Join(" ", joinedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        foreach (var statusLabel in new[] { "CURRENT PHASE 1A", "CURRENT CONTRACT-ONLY", "FUTURE / GATED" })
        {
            Assert.Contains(statusLabel, joinedText);
        }

        foreach (var style in new[] { "solid", "dashed", "dotted" })
        {
            Assert.Contains(style, rectangleStyles);
        }

        for (var boundary = 1; boundary <= 6; boundary++)
        {
            Assert.Contains($"TB{boundary}", joinedText);
        }

        string[] expectedFlows =
        [
            "F1", "F2a", "F2b", "F2c", "F2d", "F3a", "F3b", "F3c", "F4",
            "F5a", "F5b", "F5c", "F6", "F7", "F8a", "F8b", "F8c", "F8d",
            "F9", "F10 ↔", "S1",
        ];
        CollectionAssert.AreEquivalent(expectedFlows, flows.Keys.ToArray());

        Assert.AreEqual("dotted", flows["F2d"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F6"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("solid", flows["F7"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F8a"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F8b"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F8c"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F8d"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("dotted", flows["F10 ↔"].GetProperty("strokeStyle").GetString());
        Assert.AreEqual("arrow", flows["F10 ↔"].GetProperty("startArrowhead").GetString());
        Assert.AreEqual("arrow", flows["F10 ↔"].GetProperty("endArrowhead").GetString());
        var f10Points = flows["F10 ↔"].GetProperty("points");
        Assert.IsGreaterThan(
            f10Points[0][0].GetInt32(),
            f10Points[f10Points.GetArrayLength() - 1][0].GetInt32());
        Assert.AreEqual("dashed", flows["S1"].GetProperty("strokeStyle").GetString());

        var localFake = textElements.First(item => item.NormalizedText.StartsWith("Assistant adapter + local fake", StringComparison.Ordinal));
        var externalProvider = textElements.First(item => item.NormalizedText.StartsWith("EXTERNAL BYOK MODEL PROVIDER", StringComparison.Ordinal));
        var publicHelp = textElements.First(item => item.NormalizedText.StartsWith("PUBLIC / HELP HOSTING", StringComparison.Ordinal));
        var peerInstance = textElements.First(item => item.NormalizedText.StartsWith("INDEPENDENT PEER INSTANCE", StringComparison.Ordinal));

        Assert.IsGreaterThanOrEqualTo(1280, localFake.X);
        Assert.IsLessThan(1600, localFake.X);
        Assert.IsGreaterThanOrEqualTo(1600, externalProvider.X);
        Assert.IsGreaterThanOrEqualTo(1600, publicHelp.X);

        Assert.Contains("Non-secret credential handle stays here", localFake.NormalizedText);
        Assert.Contains("Secret value: TB5 custody", localFake.NormalizedText);
        Assert.Contains(
            "Transport discloses provider credential only to operator configured allowlisted provider endpoint for authentication",
            localFake.NormalizedText);
        Assert.Contains("Fake: no egress", localFake.NormalizedText);

        Assert.Contains("Owning-tenant policy authoritative", externalProvider.NormalizedText);
        Assert.Contains("Provider credential: authentication only", externalProvider.NormalizedText);
        Assert.Contains("other tenant secrets", externalProvider.NormalizedText);
        Assert.DoesNotContain("NO passkeys / recovery / secrets", externalProvider.NormalizedText);

        Assert.Contains("Receives NO app cookie, token", publicHelp.NormalizedText);
        Assert.Contains("or product / user data", publicHelp.NormalizedText);

        Assert.Contains("May receive user-approved purpose-scoped minimum disclosure", peerInstance.NormalizedText);
        Assert.Contains("May submit a proposal; never an authoritative write", peerInstance.NormalizedText);
        Assert.Contains("CURRENT: no content / traffic", peerInstance.NormalizedText);

        Assert.Contains("ADR 0008 Proposed", normalizedVisibleText);
        Assert.Contains("CompleteAsync → one AssistantResponse", normalizedVisibleText);
        Assert.Contains("Confirm before task write", normalizedVisibleText);
        Assert.Contains("May receive current request +", normalizedVisibleText);
        Assert.Contains("Disclosed content ceiling applies", normalizedVisibleText);
        Assert.Contains("User-selected supported data +", normalizedVisibleText);
        Assert.Contains("purpose-scoped minimum disclosure", normalizedVisibleText);
        Assert.Contains("User-approved assertions +", normalizedVisibleText);
        Assert.Contains("minimized source refs/digests", normalizedVisibleText);
        Assert.Contains("No durable tables", normalizedVisibleText);
        Assert.Contains("Passkey public credential data", normalizedVisibleText);
        Assert.Contains("Hashed recovery-code state", normalizedVisibleText);
        Assert.Contains("LOGICAL DB includes all durable rows", normalizedVisibleText);
        Assert.Contains("token rows per ADR 0003", normalizedVisibleText);
        Assert.Contains("Raw recovery codes not stored in DB", normalizedVisibleText);
        Assert.Contains("outside DB and dump", normalizedVisibleText);
        Assert.Contains("Separate origin + deployment", normalizedVisibleText);
    }

    [TestMethod]
    public void DiagramReadabilityGitHubPresentationScaleMeetsMinimumFontAndZeroOverflows()
    {
        var diagram = ArchitectureDiagramTool.BuildDiagram();
        var report = ArchitectureDiagramTool.ValidateDiagramReadability(diagram);

        Assert.AreEqual(ArchitectureDiagramTool.GitHubPresentationWidth, report.PresentationWidth);
        Assert.IsGreaterThanOrEqualTo(ArchitectureDiagramTool.MinSourceFontSize, report.MinimumSourceFont);
        Assert.IsGreaterThanOrEqualTo(ArchitectureDiagramTool.MinEffectiveFontSize, report.MinimumEffectiveFont);
        Assert.AreEqual(0, report.OverflowCount);
        Assert.IsGreaterThanOrEqualTo(50, report.ContainedLabels);
    }

    [TestMethod]
    public void ReadabilityValidatorOverflowAndUndersizedTextThrows()
    {
        var diagram = new DiagramBuilder();
        var box = diagram.Rectangle(10, 10, 120, 80, "Fits", fontSize: ArchitectureDiagramTool.MinSourceFontSize);
        diagram.Text(
            12,
            12,
            "Tiny overflowing text line that cannot fit inside this narrow box",
            size: ArchitectureDiagramTool.MinSourceFontSize - 2,
            width: 80,
            containerId: box.Id);

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.ValidateDiagramReadability(diagram));
        Assert.Contains("below 11px", ex.Message);
        Assert.Contains("wide in 80px", ex.Message);
    }

    [TestMethod]
    public void ReadabilityValidatorOverlappingFlowLabelThrows()
    {
        var diagram = new DiagramBuilder();
        diagram.Rectangle(100, 100, 260, 160, "Node label", fontSize: ArchitectureDiagramTool.MinSourceFontSize);
        diagram.Arrow(50, 180, 420, 180, "F1", labelAt: (220, 180));

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.ValidateDiagramReadability(diagram));
        Assert.Contains("overlaps node/callout", ex.Message);
    }

    [TestMethod]
    public void ReadabilityValidatorBoundaryLabelOverlappingNodeThrows()
    {
        var diagram = new DiagramBuilder();
        diagram.Rectangle(
            40,
            40,
            320,
            240,
            "TB1 • TRUST BOUNDARY",
            container: true,
            fontSize: ArchitectureDiagramTool.MinSourceFontSize,
            role: "boundary");
        diagram.Rectangle(70, 52, 260, 120, "Overlaps header", fontSize: ArchitectureDiagramTool.MinSourceFontSize);

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.ValidateDiagramReadability(diagram));
        Assert.Contains("overlaps node/callout", ex.Message);
    }

    [TestMethod]
    public void ReadabilityValidatorCenteredSiblingTextOverlapThrows()
    {
        var diagram = new DiagramBuilder();
        diagram.Text(100, 100, "First centered line", size: ArchitectureDiagramTool.MinSourceFontSize, width: 300, align: "center");
        diagram.Text(100, 112, "Second centered line", size: ArchitectureDiagramTool.MinSourceFontSize, width: 300, align: "center");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.ValidateDiagramReadability(diagram));
        Assert.Contains("text overlaps", ex.Message);
    }

    [TestMethod]
    public void WrapTextOversizedTokenThrowsWithoutSplitting()
    {
        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.WrapText(
                "ISuperLongUnbreakableIdentifierThatMustNotSplitSilently",
                120,
                ArchitectureDiagramTool.MinSourceFontSize));
        Assert.Contains("Label token does not fit", ex.Message);
    }

    [TestMethod]
    public void SvgOutputContainsAccessibleMetadataAndFlowIds()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var svgText = File.ReadAllText(paths.SvgPath);
        var doc = XDocument.Parse(svgText);
        var root = doc.Root!;
        XNamespace ns = "http://www.w3.org/2000/svg";

        Assert.AreEqual("svg", root.Name.LocalName);
        Assert.AreEqual("img", root.Attribute("role")?.Value);
        Assert.AreEqual("title desc", root.Attribute("aria-labelledby")?.Value);

        var expectedSourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        Assert.AreEqual(expectedSourceHash, root.Attribute("data-source-sha256")?.Value);
        Assert.IsNotNull(root.Element(ns + "title"));
        Assert.IsNotNull(root.Element(ns + "desc"));

        var svgAllText = string.Concat(root.Descendants().Select(el => el.Value));
        Assert.Contains("F2d", svgAllText);
        Assert.Contains("F10", svgAllText);
        Assert.Contains("S1", svgAllText);
    }

    [TestMethod]
    public void PngArtifactHeaderManifestAndProvenanceMatchSourceAndSvg()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var pngBytes = File.ReadAllBytes(paths.PngPath);

        Assert.IsTrue(pngBytes.AsSpan(0, 8).SequenceEqual(ArchitectureDiagramTool.PngSignature));
        Assert.AreEqual(
            ((uint)ArchitectureDiagramTool.Width, (uint)ArchitectureDiagramTool.Height),
            ArchitectureDiagramTool.PngDimensions(pngBytes));

        var excalidrawHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));
        var rasterHash = ArchitectureDiagramTool.PngRasterHash(pngBytes, requireProvenance: true);
        var bindingHash = ArchitectureDiagramTool.PngBindingHash(excalidrawHash, svgHash, rasterHash);
        var metadata = ArchitectureDiagramTool.PngTextMetadata(pngBytes);

        Assert.AreEqual(excalidrawHash, metadata[ArchitectureDiagramTool.SourceMetadataKey]);
        Assert.AreEqual(svgHash, metadata[ArchitectureDiagramTool.SvgMetadataKey]);
        Assert.AreEqual(rasterHash, metadata[ArchitectureDiagramTool.RasterMetadataKey]);
        Assert.AreEqual(bindingHash, metadata[ArchitectureDiagramTool.BindingMetadataKey]);
        Assert.AreEqual(
            $"{RepositoryPaths.Sha256Hex(pngBytes)}  andreja-high-level.png\n",
            RepositoryPaths.NormalizeLf(File.ReadAllText(paths.PngHashPath)));
    }

    [TestMethod]
    public void PngVerifierEditedPngWithRefreshedManifestFlagsRasterAndBindingMismatch()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));
        var edited = ReplacePngIdat(original);

        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(edited)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(edited, manifestPath, sourceHash, svgHash);

        CollectionAssert.Contains(stale.ToArray(), "docs/architecture/andreja-high-level.png (Andreja-Raster-SHA256)");
        CollectionAssert.Contains(stale.ToArray(), "docs/architecture/andreja-high-level.png (Andreja-Binding-SHA256)");
    }

    [TestMethod]
    public void PngVerifierEditedPngWithForgedTextChunksFlagsBindingMismatch()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));
        var edited = ReplacePngIdat(original);
        var forgedRasterHash = ArchitectureDiagramTool.PngRasterHash(edited, requireProvenance: true);

        using var forgedStream = new MemoryStream();
        forgedStream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(edited))
        {
            if (string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal)
                && chunk.DataBytes.StartsWith(Encoding.ASCII.GetBytes($"{ArchitectureDiagramTool.RasterMetadataKey}\0")))
            {
                var forgedPayload = Encoding.ASCII.GetBytes(
                    $"{ArchitectureDiagramTool.RasterMetadataKey}\0{forgedRasterHash}");
                forgedStream.Write(ArchitectureDiagramTool.PngChunk("tEXt"u8, forgedPayload));
            }
            else
            {
                forgedStream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
            }
        }

        var forgedBytes = forgedStream.ToArray();
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(forgedBytes)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(forgedBytes, manifestPath, sourceHash, svgHash);

        CollectionAssert.Contains(stale.ToArray(), "docs/architecture/andreja-high-level.png (Andreja-Binding-SHA256)");
    }

    [TestMethod]
    public void PngVerifierAncillaryChunkFlagsNonAllowlistedChunk()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        using var ancillaryStream = new MemoryStream();
        ancillaryStream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                ancillaryStream.Write(ArchitectureDiagramTool.PngChunk("zTXt"u8, "Note\0\0sneaky"u8));
            }

            ancillaryStream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        var ancillaryBytes = ancillaryStream.ToArray();
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(ancillaryBytes)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(ancillaryBytes, manifestPath, sourceHash, svgHash);

        Assert.ContainsSingle(stale);
        Assert.Contains("Non-allowlisted PNG chunk: zTXt", stale[0]);
    }

    [TestMethod]
    public void PngVerifierSplitIdatChunksFlagsNonContiguousIdat()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        var chunks = ArchitectureDiagramTool.PngChunks(original);
        var ihdr = chunks.First(chunk => string.Equals(chunk.TypeName, "IHDR", StringComparison.Ordinal));
        var idats = chunks.Where(chunk => string.Equals(chunk.TypeName, "IDAT", StringComparison.Ordinal)).ToArray();
        var texts = chunks.Where(chunk => string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal)).ToArray();
        var iend = chunks.First(chunk => string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal));

        using var combinedIdat = new MemoryStream();
        foreach (var idat in idats)
        {
            combinedIdat.Write(idat.DataBytes);
        }

        var idatPayload = combinedIdat.ToArray();
        using var splitStream = new MemoryStream();
        splitStream.Write(ArchitectureDiagramTool.PngSignature);
        splitStream.Write(ArchitectureDiagramTool.PngChunk(ihdr.TypeBytes, ihdr.DataBytes));
        splitStream.Write(ArchitectureDiagramTool.PngChunk("IDAT"u8, idatPayload.AsSpan(0, 32)));
        splitStream.Write(ArchitectureDiagramTool.PngChunk(texts[0].TypeBytes, texts[0].DataBytes));
        splitStream.Write(ArchitectureDiagramTool.PngChunk("IDAT"u8, idatPayload.AsSpan(32)));
        foreach (var textChunk in texts.Skip(1))
        {
            splitStream.Write(ArchitectureDiagramTool.PngChunk(textChunk.TypeBytes, textChunk.DataBytes));
        }

        splitStream.Write(ArchitectureDiagramTool.PngChunk(iend.TypeBytes, iend.DataBytes));
        var splitBytes = splitStream.ToArray();

        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(splitBytes)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(splitBytes, manifestPath, sourceHash, svgHash);

        Assert.ContainsSingle(stale);
        Assert.Contains("PNG IDAT chunks must be contiguous.", stale[0]);
    }

    [TestMethod]
    public void PngVerifierNonEmptyIendFlagsInvalidIendPayload()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        using var invalidStream = new MemoryStream();
        invalidStream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                invalidStream.Write(ArchitectureDiagramTool.PngChunk("IEND"u8, "x"u8));
            }
            else
            {
                invalidStream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
            }
        }

        var invalidBytes = invalidStream.ToArray();
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(invalidBytes)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(invalidBytes, manifestPath, sourceHash, svgHash);

        Assert.ContainsSingle(stale);
        Assert.Contains("PNG policy requires the IEND payload to be empty.", stale[0]);
    }

    [TestMethod]
    public void PngVerifierTruncatedIhdrFlagsInvalidIhdrLength()
    {
        using var ihdrPayload = new MemoryStream();
        Span<byte> dims = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(dims[..4], ArchitectureDiagramTool.Width);
        BinaryPrimitives.WriteUInt32BigEndian(dims.Slice(4, 4), ArchitectureDiagramTool.Height);
        ihdrPayload.Write(dims);
        ihdrPayload.Write([8, 6, 0, 0]); // 12 bytes instead of 13

        using var pngStream = new MemoryStream();
        pngStream.Write(ArchitectureDiagramTool.PngSignature);
        pngStream.Write(ArchitectureDiagramTool.PngChunk("IHDR"u8, ihdrPayload.ToArray()));
        pngStream.Write(ArchitectureDiagramTool.PngChunk("IDAT"u8, ZlibCompress([0, 0, 0, 0, 0])));
        pngStream.Write(ArchitectureDiagramTool.PngChunk("IEND"u8, []));
        var truncatedIhdrPng = pngStream.ToArray();

        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(truncatedIhdrPng)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(
            truncatedIhdrPng,
            manifestPath,
            new string('0', 64),
            new string('1', 64));

        Assert.ContainsSingle(stale);
        Assert.Contains("IHDR", stale[0]);
    }

    [TestMethod]
    public void PngVerifierInvalidTextEncodingOrDigestFlagsProvenanceError()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");

        byte[][] badPayloads =
        [
            [.. Encoding.ASCII.GetBytes($"{ArchitectureDiagramTool.SourceMetadataKey}\0"), 0xFF],
            Encoding.ASCII.GetBytes($"{ArchitectureDiagramTool.SourceMetadataKey}\0not-a-sha256"),
        ];

        foreach (var badPayload in badPayloads)
        {
            using var stream = new MemoryStream();
            stream.Write(ArchitectureDiagramTool.PngSignature);
            foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
            {
                if (string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal)
                    && chunk.DataBytes.StartsWith(Encoding.ASCII.GetBytes($"{ArchitectureDiagramTool.SourceMetadataKey}\0")))
                {
                    stream.Write(ArchitectureDiagramTool.PngChunk("tEXt"u8, badPayload));
                }
                else
                {
                    stream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
                }
            }

            var pngCandidate = stream.ToArray();
            File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(pngCandidate)}  andreja-high-level.png\n");

            var stale = ArchitectureDiagramTool.VerifyPngArtifact(pngCandidate, manifestPath, sourceHash, svgHash);
            Assert.ContainsSingle(stale);
            Assert.Contains("tEXt", stale[0]);
        }
    }

    [TestMethod]
    public void RenderPngSubprocessFailureRemovesStagingFile()
    {
        var svgPath = Path.Join(_scratch, "input.svg");
        var stagingPng = Path.Join(_scratch, ".andreja-high-level.rendering.png");
        var outPng = Path.Join(_scratch, "output.png");
        File.WriteAllText(svgPath, "<svg xmlns='http://www.w3.org/2000/svg'/>\n");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.RenderPng(
                svgPath,
                outPng,
                new string('0', 64),
                new string('1', 64),
                stagingPngPath: stagingPng,
                browserRenderer: (_, staging) =>
                {
                    File.WriteAllBytes(staging, "partial"u8);
                    throw new InvalidDataException("Simulated Edge crash");
                }));

        Assert.Contains("Simulated Edge crash", ex.Message);
        Assert.IsFalse(File.Exists(stagingPng));
        Assert.IsFalse(File.Exists(outPng));
    }

    [TestMethod]
    public void ExecuteCliRenderPngHashWriteFailureRollsBackPreviousArtifacts()
    {
        var paths = ArchitectureArtifactPaths.FromOutputDirectory(_scratch);
        File.WriteAllBytes(paths.PngPath, "previous-png"u8);
        File.WriteAllBytes(paths.PngHashPath, "previous-hash\n"u8);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        Assert.ThrowsExactly<IOException>(() =>
            ArchitectureDiagramTool.ExecuteCli(
                paths,
                check: false,
                renderPng: true,
                stdout,
                stderr,
                pngRenderer: (_, outputPath, _, _) => File.WriteAllBytes(outputPath, "new-png"u8),
                atomicWriter: (targetPath, data) =>
                {
                    if (string.Equals(targetPath, paths.PngHashPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("disk full");
                    }

                    ArchitectureDiagramTool.WriteBytesAtomically(targetPath, data);
                }));

        Assert.AreEqual("previous-png", Encoding.ASCII.GetString(File.ReadAllBytes(paths.PngPath)));
        Assert.AreEqual("previous-hash\n", Encoding.ASCII.GetString(File.ReadAllBytes(paths.PngHashPath)));
        Assert.IsFalse(File.Exists(paths.ExcalidrawPath));
        Assert.IsFalse(File.Exists(paths.SvgPath));
        Assert.IsEmpty(Directory.GetFiles(_scratch, "*.tmp"));
    }

    [TestMethod]
    public void SvgAndPngBindTheReadabilityScale()
    {
        var diagram = ArchitectureDiagramTool.BuildDiagram();
        var textElements = diagram.Elements.Where(el => string.Equals(el.Type, "text", StringComparison.Ordinal)).ToArray();

        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var doc = XDocument.Parse(File.ReadAllText(paths.SvgPath));
        var root = doc.Root!;
        XNamespace ns = "http://www.w3.org/2000/svg";

        Assert.AreEqual(ArchitectureDiagramTool.Width.ToString(CultureInfo.InvariantCulture), root.Attribute("width")?.Value);
        Assert.AreEqual(ArchitectureDiagramTool.Height.ToString(CultureInfo.InvariantCulture), root.Attribute("height")?.Value);
        Assert.AreEqual(
            $"0 0 {ArchitectureDiagramTool.Width} {ArchitectureDiagramTool.Height}",
            root.Attribute("viewBox")?.Value);

        var svgTexts = root.Elements(ns + "text").ToArray();
        Assert.HasCount(textElements.Length, svgTexts);

        var minimumSvgFont = svgTexts.Min(node => double.Parse(node.Attribute("font-size")!.Value, CultureInfo.InvariantCulture));
        Assert.IsGreaterThanOrEqualTo(
            ArchitectureDiagramTool.MinEffectiveFontSize,
            minimumSvgFont * ArchitectureDiagramTool.GitHubPresentationWidth / ArchitectureDiagramTool.Width);

        var containedSvgLabels = svgTexts.Count(node => node.Attribute("data-container-id") is not null);
        var expectedContained = textElements.Count(el => !string.IsNullOrEmpty(el.ContainerId));
        Assert.AreEqual(expectedContained, containedSvgLabels);
    }

    [TestMethod]
    [DataRow(-1, 100, 100, 100)]
    [DataRow(100, 100, ArchitectureDiagramTool.Width + 1, 100)]
    [DataRow(100, -1, 100, 100)]
    [DataRow(100, 100, 100, ArchitectureDiagramTool.Height + 1)]
    public void ReadabilityValidatorArrowPointsOutsideCanvasThrows(int x1, int y1, int x2, int y2)
    {
        var diagram = new DiagramBuilder();
        diagram.Arrow(x1, y1, x2, y2, "T", color: "gray", status: "current");

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            ArchitectureDiagramTool.ValidateDiagramReadability(diagram));
        Assert.Contains("outside the", ex.Message);
    }

    [TestMethod]
    [DataRow("iTXt")]
    [DataRow("eXIf")]
    [DataRow("acTL")]
    [DataRow("fdAT")]
    public void PngVerifierDisallowedChunkTypesFlagNonAllowlistedChunk(string chunkName)
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        using var stream = new MemoryStream();
        stream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                stream.Write(ArchitectureDiagramTool.PngChunk(Encoding.ASCII.GetBytes(chunkName), "payload123"u8));
            }

            stream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        var mutated = stream.ToArray();
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(mutated)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(mutated, manifestPath, sourceHash, svgHash);
        Assert.ContainsSingle(stale);
        Assert.Contains($"Non-allowlisted PNG chunk: {chunkName}", stale[0]);
    }

    [TestMethod]
    public void PngVerifierOversizedIhdrWithRegeneratedProvenanceAndManifestFlagsInvalidIhdrLength()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        var oversizedChunks = ArchitectureDiagramTool.PngChunks(original)
            .Select(chunk => string.Equals(chunk.TypeName, "IHDR", StringComparison.Ordinal)
                ? new PngChunkRecord(chunk.TypeBytes, [.. chunk.DataBytes, 0])
                : chunk)
            .ToArray();
        var rasterHash = ArchitectureDiagramTool.PngRasterHash(oversizedChunks);
        var bindingHash = ArchitectureDiagramTool.PngBindingHash(sourceHash, svgHash, rasterHash);

        using var stream = new MemoryStream();
        stream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in oversizedChunks)
        {
            if (string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal))
            {
                var data = chunk.DataBytes;
                var sep = data.IndexOf((byte)0);
                var key = Encoding.ASCII.GetString(data[..sep]);
                if (string.Equals(key, ArchitectureDiagramTool.RasterMetadataKey, StringComparison.Ordinal))
                {
                    stream.Write(ArchitectureDiagramTool.PngChunk("tEXt"u8, Encoding.ASCII.GetBytes($"{key}\0{rasterHash}")));
                    continue;
                }

                if (string.Equals(key, ArchitectureDiagramTool.BindingMetadataKey, StringComparison.Ordinal))
                {
                    stream.Write(ArchitectureDiagramTool.PngChunk("tEXt"u8, Encoding.ASCII.GetBytes($"{key}\0{bindingHash}")));
                    continue;
                }
            }

            stream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        var mutated = stream.ToArray();
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(mutated)}  andreja-high-level.png\n");

        var stale = ArchitectureDiagramTool.VerifyPngArtifact(mutated, manifestPath, sourceHash, svgHash);
        Assert.ContainsSingle(stale);
        Assert.Contains("13", stale[0]);
    }

    [TestMethod]
    public void PngVerifierUnknownKeywordAndDuplicateProvenanceFlagErrors()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));
        var manifestPath = Path.Join(_scratch, "andreja-high-level.png.sha256");

        using var unknownKeywordStream = new MemoryStream();
        unknownKeywordStream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                unknownKeywordStream.Write(ArchitectureDiagramTool.PngChunk(
                    "tEXt"u8,
                    Encoding.ASCII.GetBytes($"Unexpected-Keyword\0{new string('0', 64)}")));
            }

            unknownKeywordStream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        var unknownBytes = unknownKeywordStream.ToArray();
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(unknownBytes)}  andreja-high-level.png\n");
        var unknownStale = ArchitectureDiagramTool.VerifyPngArtifact(unknownBytes, manifestPath, sourceHash, svgHash);
        Assert.ContainsSingle(unknownStale);
        Assert.Contains("Non-allowlisted PNG tEXt keyword", unknownStale[0]);

        var existingText = ArchitectureDiagramTool.PngChunks(original)
            .First(chunk => string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal));
        using var duplicateStream = new MemoryStream();
        duplicateStream.Write(ArchitectureDiagramTool.PngSignature);
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(original))
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                duplicateStream.Write(ArchitectureDiagramTool.PngChunk("tEXt"u8, existingText.DataBytes));
            }

            duplicateStream.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        var duplicateBytes = duplicateStream.ToArray();
        File.WriteAllText(manifestPath, $"{RepositoryPaths.Sha256Hex(duplicateBytes)}  andreja-high-level.png\n");
        var duplicateStale = ArchitectureDiagramTool.VerifyPngArtifact(duplicateBytes, manifestPath, sourceHash, svgHash);
        Assert.ContainsSingle(duplicateStale);
        Assert.Contains("Duplicate PNG provenance tEXt keyword", duplicateStale[0]);
    }

    [TestMethod]
    public void EmbedPngProvenanceOnAlreadyProvenancedPngIsIdempotent()
    {
        var paths = ArchitectureArtifactPaths.FromRepositoryRoot(_repoRoot);
        var original = File.ReadAllBytes(paths.PngPath);
        var sourceHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)));
        var svgHash = RepositoryPaths.Sha256Hex(
            RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)));

        var reembedded = ArchitectureDiagramTool.EmbedPngProvenance(original, sourceHash, svgHash);
        Assert.IsTrue(original.AsSpan().SequenceEqual(reembedded));
    }

    private static byte[] ReplacePngIdat(byte[] pngBytes)
    {
        const int rowStride = 1 + (ArchitectureDiagramTool.Width * 4);
        var rawScanlines = new byte[rowStride * ArchitectureDiagramTool.Height];
        var compressedIdat = ZlibCompress(rawScanlines);

        using var output = new MemoryStream();
        output.Write(ArchitectureDiagramTool.PngSignature);
        var replacedIdat = false;
        foreach (var chunk in ArchitectureDiagramTool.PngChunks(pngBytes))
        {
            if (string.Equals(chunk.TypeName, "IDAT", StringComparison.Ordinal))
            {
                if (!replacedIdat)
                {
                    output.Write(ArchitectureDiagramTool.PngChunk("IDAT"u8, compressedIdat));
                    replacedIdat = true;
                }

                continue;
            }

            output.Write(ArchitectureDiagramTool.PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        return output.ToArray();
    }

    private static byte[] ZlibCompress(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(input);
        }

        return output.ToArray();
    }
}
