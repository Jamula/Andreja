using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Docs;

public sealed record ArchitectureArtifactPaths(
    string OutputDirectory,
    string ExcalidrawPath,
    string SvgPath,
    string PngPath,
    string PngHashPath,
    string CheckPngPath,
    string StagingPngPath)
{
    public static ArchitectureArtifactPaths FromRepositoryRoot(string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        var outDir = Path.Join(repositoryRoot, "docs", "architecture");
        return FromOutputDirectory(outDir);
    }

    public static ArchitectureArtifactPaths FromOutputDirectory(string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        return new ArchitectureArtifactPaths(
            outputDirectory,
            Path.Join(outputDirectory, "andreja-high-level.excalidraw"),
            Path.Join(outputDirectory, "andreja-high-level.svg"),
            Path.Join(outputDirectory, "andreja-high-level.png"),
            Path.Join(outputDirectory, "andreja-high-level.png.sha256"),
            Path.Join(outputDirectory, ".andreja-high-level.check.png"),
            Path.Join(outputDirectory, ".andreja-high-level.rendering.png"));
    }
}

public sealed record DiagramBoundElement(string Id, string Type);

public sealed record DiagramPoint(int X, int Y);

public sealed record ReadabilityReport(
    int PresentationWidth,
    double Scale,
    int MinimumSourceFont,
    double MinimumEffectiveFont,
    int ContainedLabels,
    int OverflowCount);

public sealed class PngChunkRecord
{
    private readonly byte[] _chunkType;
    private readonly byte[] _data;

    public PngChunkRecord(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        if (chunkType.Length != 4)
        {
            throw new InvalidDataException("PNG chunk type must be 4 bytes.");
        }

        _chunkType = chunkType.ToArray();
        _data = data.ToArray();
    }

    public ReadOnlySpan<byte> TypeBytes => _chunkType;

    public ReadOnlySpan<byte> DataBytes => _data;

    public string TypeName => Encoding.ASCII.GetString(_chunkType);
}

public sealed class DiagramElement
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required int X { get; set; }

    public required int Y { get; set; }

    public required int Width { get; set; }

    public required int Height { get; set; }

    public int Angle { get; init; }

    public required int StrokeWidth { get; set; }

    public required string StrokeStyle { get; set; }

    public int Roughness { get; init; }

    public int Opacity { get; init; } = 100;

    public required string Index { get; init; }

    public required int RoundnessType { get; set; }

    public required int Seed { get; init; }

    public int Version { get; init; } = 1;

    public required int VersionNonce { get; init; }

    public List<DiagramBoundElement> BoundElements { get; } = [];

    public int Updated { get; init; } = 1;

    public required string StrokeColor { get; set; }

    public required string BackgroundColor { get; set; }

    public string FillStyle { get; init; } = "solid";

    public required string DiagramRole { get; set; }

    public string? FontWeight { get; set; }

    public string? FlowId { get; set; }

    public string? Text { get; set; }

    public int FontSize { get; set; }

    public int FontFamily { get; init; } = 2;

    public string TextAlign { get; set; } = "left";

    public string VerticalAlign { get; init; } = "top";

    public string? ContainerId { get; set; }

    public string? OriginalText { get; set; }

    public bool AutoResize { get; init; }

    public double LineHeight { get; init; } = ArchitectureDiagramTool.TextLineHeight;

    public List<DiagramPoint> Points { get; } = [];

    public string? StartArrowhead { get; set; }

    public string? EndArrowhead { get; set; }

    public bool Elbowed { get; init; }
}

public sealed class DiagramBuilder
{
    private readonly List<DiagramElement> _elements = [];
    private readonly List<string> _svg = [];
    private int _counter;

    public IReadOnlyList<DiagramElement> Elements => _elements;

    public IReadOnlyList<string> Svg => _svg;

    public List<DiagramElement> MutableElements => _elements;

    public DiagramElement Text(
        int x,
        int y,
        string text,
        int size = 18,
        int width = 300,
        string align = "left",
        string weight = "normal",
        string? containerId = null,
        string role = "annotation")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(align);
        ArgumentNullException.ThrowIfNull(weight);
        ArgumentNullException.ThrowIfNull(role);

        var splitLines = RepositoryPaths.SplitLines(text);
        var lines = splitLines.Length > 0 ? splitLines : [string.Empty];
        var height = (int)Math.Ceiling(size * ArchitectureDiagramTool.TextLineHeight * lines.Length);
        var elementId = NextId("text");
        var item = CreateBase("text", elementId, x, y, width, height);
        item.StrokeWidth = 1;
        item.StrokeColor = "#000000";
        item.BackgroundColor = "transparent";
        item.Text = text;
        item.FontSize = size;
        item.TextAlign = align;
        item.ContainerId = containerId;
        item.OriginalText = text;
        item.DiagramRole = role;
        item.FontWeight = weight;
        _elements.Add(item);

        var anchor = align switch
        {
            "left" => "start",
            "center" => "middle",
            "right" => "end",
            _ => throw new InvalidDataException($"Unsupported text alignment: {align}"),
        };

        var svgX = string.Equals(align, "left", StringComparison.Ordinal)
            ? x
            : string.Equals(align, "center", StringComparison.Ordinal)
                ? x + (width / 2.0)
                : x + width;
        var svgXFormatted = FormatSvgCoordinate(svgX);
        var svgYFormatted = FormatSvgCoordinate(y + size);

        var tspans = new StringBuilder();
        for (var index = 0; index < lines.Length; index++)
        {
            var dy = index == 0
                ? "0"
                : FormatPythonFloat(size * ArchitectureDiagramTool.TextLineHeight);
            tspans.Append(
                CultureInfo.InvariantCulture,
                $"<tspan x=\"{svgXFormatted}\" dy=\"{dy}\">{ArchitectureDiagramTool.EscapeHtml(lines[index])}</tspan>");
        }

        var containerAttribute = containerId is not null
            ? $" data-container-id=\"{ArchitectureDiagramTool.EscapeHtml(containerId)}\""
            : string.Empty;
        _svg.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<text id=\"{elementId}\" data-role=\"{role}\"{containerAttribute} x=\"{svgXFormatted}\" y=\"{svgYFormatted}\" text-anchor=\"{anchor}\" font-size=\"{size}\" font-weight=\"{weight}\" fill=\"#000000\">{tspans}</text>"));

        return item;
    }

    public DiagramElement Rectangle(
        int x,
        int y,
        int width,
        int height,
        string label,
        string color = "gray",
        string status = "current",
        bool container = false,
        int fontSize = 17,
        string labelAlign = "center",
        string role = "node")
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(labelAlign);
        ArgumentNullException.ThrowIfNull(role);

        var (stroke, rawFill) = ArchitectureDiagramTool.GetColorPair(color);
        var fill = container ? "transparent" : rawFill;
        var style = MapStrokeStyle(status);
        var elementId = NextId("rect");
        var effectiveRole = container ? "boundary" : role;
        var item = CreateBase("rectangle", elementId, x, y, width, height);
        item.StrokeColor = stroke;
        item.BackgroundColor = fill;
        item.StrokeStyle = style;
        item.DiagramRole = effectiveRole;
        _elements.Add(item);

        var dash = MapDashAttribute(style);
        var svgFill = container ? "none" : fill;
        _svg.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<rect id=\"{elementId}\" data-role=\"{effectiveRole}\" x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" rx=\"12\" fill=\"{svgFill}\" stroke=\"{stroke}\" stroke-width=\"2\"{dash}/>"));

        if (label.Length > 0)
        {
            if (fontSize < ArchitectureDiagramTool.MinSourceFontSize)
            {
                throw new InvalidDataException(
                    $"{role} label font {fontSize}px is below the {ArchitectureDiagramTool.MinSourceFontSize}px source minimum.");
            }

            const int horizontalPadding = 18;
            const int verticalPadding = 12;
            var labelWidth = width - (horizontalPadding * 2);
            var weight = container ? "bold" : "normal";
            var wrappedLabel = ArchitectureDiagramTool.WrapText(label, labelWidth, fontSize, weight);
            var lineCount = RepositoryPaths.SplitLines(wrappedLabel).Length;
            var textHeight = (int)Math.Ceiling(fontSize * ArchitectureDiagramTool.TextLineHeight * lineCount);
            var textY = container
                ? y + verticalPadding
                : y + FloorDiv2(height - textHeight);
            if (labelWidth <= 0
                || textY < y + verticalPadding
                || textY + textHeight > y + height - verticalPadding)
            {
                throw new InvalidDataException(
                    $"Label does not fit inside {role} rectangle at ({x}, {y}, {width}, {height}): '{wrappedLabel}'");
            }

            var textItem = Text(
                x + horizontalPadding,
                textY,
                wrappedLabel,
                size: fontSize,
                width: labelWidth,
                align: labelAlign,
                weight: weight,
                containerId: elementId,
                role: $"{role}-label");
            item.BoundElements.Add(new DiagramBoundElement(textItem.Id, "text"));
        }

        return item;
    }

    public DiagramElement Arrow(
        int x1,
        int y1,
        int x2,
        int y2,
        string label,
        string color = "gray",
        string status = "current",
        int labelDx = 0,
        int labelDy = -24,
        IReadOnlyList<(int X, int Y)>? via = null,
        (int X, int Y)? labelAt = null,
        bool bidirectional = false)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(status);

        var (stroke, _) = ArchitectureDiagramTool.GetColorPair(color);
        var style = MapStrokeStyle(status);

        var absolutePoints = new List<(int X, int Y)> { (x1, y1) };
        if (via is not null)
        {
            absolutePoints.AddRange(via);
        }

        absolutePoints.Add((x2, y2));

        var localPoints = new List<DiagramPoint>(absolutePoints.Count);
        var minX = int.MaxValue;
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        foreach (var (px, py) in absolutePoints)
        {
            var lx = px - x1;
            var ly = py - y1;
            localPoints.Add(new DiagramPoint(lx, ly));
            if (lx < minX)
            {
                minX = lx;
            }

            if (lx > maxX)
            {
                maxX = lx;
            }

            if (ly < minY)
            {
                minY = ly;
            }

            if (ly > maxY)
            {
                maxY = ly;
            }
        }

        var elementId = NextId("arrow");
        var item = CreateBase("arrow", elementId, x1, y1, maxX - minX, maxY - minY);
        item.StrokeColor = stroke;
        item.BackgroundColor = "transparent";
        item.StrokeStyle = style;
        item.RoundnessType = 2;
        item.Points.AddRange(localPoints);
        item.StartArrowhead = bidirectional ? "arrow" : null;
        item.EndArrowhead = "arrow";
        item.DiagramRole = "flow";
        item.FlowId = label;
        _elements.Add(item);

        var dash = MapDashAttribute(style);
        var pointsAttr = string.Join(
            " ",
            absolutePoints.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.X},{point.Y}")));
        var markerStart = bidirectional ? $" marker-start=\"url(#arrow-{color})\"" : string.Empty;
        _svg.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<polyline id=\"{elementId}\" data-role=\"flow\" points=\"{pointsAttr}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"3\"{dash}{markerStart} marker-end=\"url(#arrow-{color})\"/>"));

        var labelX = labelAt?.X ?? (((x1 + x2) / 2) + labelDx);
        var labelY = labelAt?.Y ?? (((y1 + y2) / 2) + labelDy);
        var labelWidth = label.Length > 4 ? 86 : 46;
        const int labelHeight = 36;
        var labelPadding = label.Length > 4 ? 4 : 1;
        var labelBoxId = NextId("flow-label");
        var labelBoxX = labelX - FloorDiv2(labelWidth);
        var labelBoxY = labelY - FloorDiv2(labelHeight);

        var labelItem = CreateBase("rectangle", labelBoxId, labelBoxX, labelBoxY, labelWidth, labelHeight);
        labelItem.StrokeColor = ArchitectureDiagramTool.GetColorPair("gray").Stroke;
        labelItem.BackgroundColor = "#ffffff";
        labelItem.StrokeStyle = "solid";
        labelItem.DiagramRole = "flow-label";
        _elements.Add(labelItem);

        _svg.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<rect id=\"{labelBoxId}\" data-role=\"flow-label\" x=\"{labelBoxX}\" y=\"{labelBoxY}\" width=\"{labelWidth}\" height=\"{labelHeight}\" rx=\"8\" fill=\"#ffffff\" stroke=\"#495057\" stroke-width=\"1\"/>"));

        var textItem = Text(
            labelBoxX + labelPadding,
            labelY - (int)Math.Ceiling(ArchitectureDiagramTool.MinSourceFontSize * ArchitectureDiagramTool.TextLineHeight / 2.0),
            label,
            size: ArchitectureDiagramTool.MinSourceFontSize,
            width: labelWidth - (labelPadding * 2),
            align: "center",
            weight: "bold",
            containerId: labelBoxId,
            role: "flow-label-text");
        labelItem.BoundElements.Add(new DiagramBoundElement(textItem.Id, "text"));
        return item;
    }

    internal static int FloorDiv2(int value)
    {
        return value >= 0 ? value / 2 : (value - 1) / 2;
    }

    private static string MapStrokeStyle(string status)
    {
        return status switch
        {
            "current" => "solid",
            "contract" => "dashed",
            "future" => "dotted",
            _ => throw new InvalidDataException($"Unsupported diagram status: {status}"),
        };
    }

    private static string MapDashAttribute(string style)
    {
        return style switch
        {
            "solid" => string.Empty,
            "dashed" => " stroke-dasharray=\"12 8\"",
            "dotted" => " stroke-dasharray=\"3 8\"",
            _ => throw new InvalidDataException($"Unsupported stroke style: {style}"),
        };
    }

    private static string FormatSvgCoordinate(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatPythonFloat(double value)
    {
        return value == Math.Truncate(value)
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    private string NextId(string prefix)
    {
        _counter++;
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}-{_counter:D3}");
    }

    private DiagramElement CreateBase(string kind, string elementId, int x, int y, int width, int height)
    {
        return new DiagramElement
        {
            Id = elementId,
            Type = kind,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Angle = 0,
            StrokeWidth = 2,
            StrokeStyle = "solid",
            Roughness = 0,
            Opacity = 100,
            Index = string.Create(CultureInfo.InvariantCulture, $"a{_counter:D3}"),
            RoundnessType = 3,
            Seed = 1000 + _counter,
            Version = 1,
            VersionNonce = 2000 + _counter,
            Updated = 1,
            StrokeColor = "#000000",
            BackgroundColor = "transparent",
            DiagramRole = kind,
        };
    }
}

public static class ArchitectureDiagramTool
{
    public const int Width = 1920;
    public const int Height = 2710;
    public const int GitHubPresentationWidth = 960;
    public const int MinEffectiveFontSize = 11;
    public const int MinSourceFontSize = 22; // ceil(11 * 1920 / 960)
    public const double TextLineHeight = 1.25;

    public const string SourceMetadataKey = "Andreja-Source-SHA256";
    public const string SvgMetadataKey = "Andreja-SVG-SHA256";
    public const string RasterMetadataKey = "Andreja-Raster-SHA256";
    public const string BindingMetadataKey = "Andreja-Binding-SHA256";

    private static readonly byte[] PngSignatureBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly (string Name, string Stroke, string Fill)[] OrderedColors =
    [
        ("blue", "#1864ab", "#d0ebff"),
        ("green", "#2f9e44", "#d3f9d8"),
        ("orange", "#e67700", "#fff3bf"),
        ("purple", "#862e9c", "#f3d9fa"),
        ("teal", "#0c8599", "#c5f6fa"),
        ("gray", "#495057", "#f1f3f5"),
        ("red", "#c92a2a", "#ffe3e3"),
    ];

    private static readonly Dictionary<string, (string Stroke, string Fill)> ColorLookup =
        OrderedColors.ToDictionary(item => item.Name, item => (item.Stroke, item.Fill), StringComparer.Ordinal);

    private static readonly (string Name, string MetadataKey)[] OrderedProvenanceKeys =
    [
        ("source", SourceMetadataKey),
        ("svg", SvgMetadataKey),
        ("raster", RasterMetadataKey),
        ("binding", BindingMetadataKey),
    ];

    private static readonly HashSet<string> ExpectedProvenanceKeySet =
        new(OrderedProvenanceKeys.Select(item => item.MetadataKey), StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedChunkNames =
        new(["IHDR", "IDAT", "tEXt", "IEND"], StringComparer.Ordinal);

    private static readonly uint[] Crc32Table = CreateCrc32Table();

    public static ReadOnlySpan<byte> PngSignature => PngSignatureBytes;

    public static (string Stroke, string Fill) GetColorPair(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (!ColorLookup.TryGetValue(color, out var pair))
        {
            throw new InvalidDataException($"Unknown diagram color: {color}");
        }

        return pair;
    }

    public static double EstimatedTextWidth(string text, int size, string weight = "normal")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(weight);

        var widthUnits = 0.0;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                widthUnits += 0.32;
            }
            else if ("ilI.,:;|'`![]()".Contains(character))
            {
                widthUnits += 0.34;
            }
            else if ("mwMW@#%&".Contains(character))
            {
                widthUnits += 0.9;
            }
            else if (char.IsUpper(character))
            {
                widthUnits += 0.68;
            }
            else if (char.IsDigit(character))
            {
                widthUnits += 0.58;
            }
            else
            {
                widthUnits += 0.55;
            }
        }

        var weightFactor = string.Equals(weight, "bold", StringComparison.Ordinal) ? 1.06 : 1.0;
        return widthUnits * size * weightFactor;
    }

    public static string WrapText(string text, int width, int size, string weight = "normal")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(weight);

        var splitParagraphs = RepositoryPaths.SplitLines(text);
        var paragraphs = splitParagraphs.Length > 0 ? splitParagraphs : [string.Empty];
        var output = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var words = SplitByWhitespace(paragraph);
            if (words.Count == 0)
            {
                output.Add(string.Empty);
                continue;
            }

            var line = words[0];
            if (EstimatedTextWidth(line, size, weight) > width)
            {
                throw new InvalidDataException($"Label token does not fit in {width}px: '{line}'");
            }

            for (var index = 1; index < words.Count; index++)
            {
                var word = words[index];
                var candidate = $"{line} {word}";
                if (EstimatedTextWidth(candidate, size, weight) <= width)
                {
                    line = candidate;
                    continue;
                }

                output.Add(line);
                line = word;
                if (EstimatedTextWidth(line, size, weight) > width)
                {
                    throw new InvalidDataException($"Label token does not fit in {width}px: '{line}'");
                }
            }

            output.Add(line);
        }

        return string.Join("\n", output);
    }

    public static DiagramBuilder BuildDiagram()
    {
        var d = new DiagramBuilder();

        d.Text(
            35,
            20,
            "Andreja high-level architecture and data flows",
            size: 40,
            width: 1200,
            weight: "bold",
            role: "title");
        d.Text(
            35,
            82,
            "Ratified-plan evidence view • ADRs 0001–0005 Accepted; ADR 0008 Proposed • one modular-monolith deployable",
            size: 22,
            width: 1260,
            role: "subtitle");
        d.Rectangle(
            1320,
            15,
            565,
            145,
            "CONTENT RULE\nUser content follows policy-labelled flows only.\nNever telemetry, public/help, or secret files.",
            color: "red",
            fontSize: 22,
            role: "callout");

        d.Rectangle(
            35,
            175,
            300,
            130,
            "CURRENT PHASE 1A\nsolid border / flow",
            color: "gray",
            status: "current",
            fontSize: 22,
            role: "legend");
        d.Rectangle(
            355,
            175,
            340,
            130,
            "CURRENT CONTRACT-ONLY\nlocal conformance; dashed",
            color: "gray",
            status: "contract",
            fontSize: 22,
            role: "legend");
        d.Rectangle(
            715,
            175,
            300,
            130,
            "FUTURE / GATED\nnot deployed; dotted",
            color: "gray",
            status: "future",
            fontSize: 22,
            role: "legend");
        d.Rectangle(
            1035,
            175,
            850,
            130,
            "Trust boundaries are labelled TB1–TB6.\nFlows F1–F10 and semantic flow S1 map to the companion.\nStatus is always written; color only groups areas.",
            color: "gray",
            fontSize: 22,
            role: "legend");

        d.Rectangle(
            25,
            340,
            290,
            1420,
            "TB1 • CLIENT / BROWSER\nTRUST BOUNDARY",
            color: "blue",
            container: true,
            fontSize: 22);
        d.Rectangle(
            335,
            340,
            925,
            1420,
            "TB2 • AUTHENTICATED APP TRUST BOUNDARY",
            color: "green",
            container: true,
            fontSize: 22);
        d.Rectangle(
            365,
            640,
            865,
            1090,
            "TB3 • TENANT + PRINCIPAL + PURPOSE ISOLATION",
            color: "orange",
            container: true,
            fontSize: 22);
        d.Rectangle(
            1280,
            340,
            275,
            1420,
            "TB4 • LOCAL ADAPTER + CREDENTIAL HANDLE\nTRUST BOUNDARY",
            color: "purple",
            container: true,
            fontSize: 22);
        d.Rectangle(
            335,
            1790,
            1245,
            880,
            "TB5 • LOCAL OPERATOR CUSTODY BOUNDARY",
            color: "teal",
            container: true,
            fontSize: 22);
        d.Rectangle(
            1605,
            340,
            290,
            2330,
            "TB6 • EXTERNAL / PEER\nTRUST BOUNDARY",
            color: "red",
            container: true,
            fontSize: 22);

        d.Rectangle(
            50,
            470,
            240,
            150,
            "Responsive Blazor web\nTyped API client",
            color: "blue",
            fontSize: 22);
        d.Rectangle(
            50,
            740,
            240,
            170,
            "Future native + third-party clients\nFUTURE / GATED",
            color: "purple",
            status: "future",
            fontSize: 22);

        d.Rectangle(
            365,
            440,
            245,
            160,
            "AppHost\nComposition + Blazor host",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            645,
            440,
            255,
            160,
            "Versioned HTTP API\nTyped contracts / clients",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            935,
            440,
            285,
            160,
            "Adapters\nIdentity • PostgreSQL • OTel\nAssistant adapter",
            color: "green",
            fontSize: 22);

        d.Rectangle(
            390,
            730,
            240,
            180,
            "Identity + Tenancy\nPasskeys / scoped context",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            655,
            700,
            260,
            210,
            "Assistant Runtime\nProvider-neutral session\nCompleteAsync → one\nAssistantResponse",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            920,
            730,
            280,
            180,
            "Skills\nISkillHost + manifests",
            color: "green",
            fontSize: 22);

        d.Rectangle(
            390,
            950,
            240,
            180,
            "Policy evaluator\nTenant • principal • purpose",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            655,
            950,
            240,
            210,
            "Proposal / Control Plane\nDurable proposal state\nConfirm before task write",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            920,
            950,
            280,
            180,
            "Open Loops / Tasks\nAccess-scoped domain write",
            color: "green",
            fontSize: 22);

        d.Rectangle(
            390,
            1170,
            240,
            270,
            "Sharing / Federation\nCURRENT CONTRACT-ONLY\nGrant • consent • disclosure\nLocal fixture: no content sent",
            color: "orange",
            status: "contract",
            fontSize: 22);
        d.Rectangle(
            655,
            1170,
            240,
            300,
            "Semantic / Provenance\nCURRENT CONTRACT-ONLY\nUser-approved assertions +\nminimized source refs/digests\nNo durable tables",
            color: "orange",
            status: "contract",
            fontSize: 22);
        d.Rectangle(
            920,
            1170,
            280,
            220,
            "Audit + idempotency\nTransactional evidence",
            color: "green",
            fontSize: 22);

        d.Rectangle(
            390,
            1510,
            240,
            180,
            "Portability\nCURRENT PHASE 1A\nVersioned export / import",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            655,
            1510,
            240,
            180,
            "Observability\nOTel allowlist / suppression",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            920,
            1510,
            280,
            180,
            "Channels\nCURRENT CONTRACT-ONLY\nIChannelHost + manifests",
            color: "orange",
            status: "contract",
            fontSize: 22);

        d.Rectangle(
            1292,
            500,
            250,
            520,
            "Assistant adapter + local fake\nCURRENT PHASE 1A\nNon-secret credential handle stays here\nSecret value: TB5 custody\nTransport discloses provider credential only to operator configured allowlisted provider endpoint for authentication\nFake: no egress",
            color: "green",
            fontSize: 22);
        d.Rectangle(
            1292,
            1100,
            250,
            190,
            "Optional OIDC adapter\nFUTURE / GATED\nNo provider selected",
            color: "purple",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1292,
            1450,
            250,
            170,
            "OTel exporter\nOperational fields only",
            color: "teal",
            fontSize: 22);

        d.Rectangle(
            365,
            1880,
            280,
            320,
            "Tenant-scoped PostgreSQL\nTasks • proposals • identity\nPasskey public credential data\nHashed recovery-code state\nAudit • idempotency • refs",
            color: "teal",
            fontSize: 22);
        d.Rectangle(
            660,
            1880,
            180,
            200,
            "Attachments\nOutside DB when added\nFUTURE / GATED",
            color: "purple",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            855,
            1880,
            200,
            220,
            "Data Protection + encryption key history",
            color: "teal",
            fontSize: 22);
        d.Rectangle(
            1070,
            1880,
            220,
            260,
            "File-backed operator secrets / configuration\nProvider credential value",
            color: "teal",
            fontSize: 22);
        d.Rectangle(
            1305,
            1880,
            245,
            200,
            "Local OTel Collector\nOptional local evidence backend",
            color: "teal",
            fontSize: 22);
        d.Rectangle(
            365,
            2240,
            330,
            390,
            "F7 • APPLICATION ARCHIVE\nUser-selected supported data +\nminimized provenance may flow\nOwning tenant controls export\nNO reusable authority / credentials",
            color: "teal",
            fontSize: 22);
        d.Rectangle(
            730,
            2240,
            330,
            420,
            "F8 • UNPROVEN RECOVERY SET\nLOGICAL DB includes all durable rows (passkey credential data, hashed recovery-code state, token rows per ADR 0003). Raw recovery codes not stored in DB.\nFile-backed secrets + private keys:\noutside DB and dump.\nSeparate protected key/config inventory.",
            color: "purple",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1095,
            2240,
            455,
            220,
            "EXTENSION SEAMS\nIAssistantProvider • ISkillHost • IChannelHost • identity/OIDC\nTyped clients • persistence/portability • OTel • IPeerChannel",
            color: "gray",
            fontSize: 22);

        d.Rectangle(
            1615,
            480,
            270,
            570,
            "EXTERNAL BYOK MODEL PROVIDER\nFUTURE / GATED ACTIVATION\nMay receive current request +\nallowlisted tool schema only\nPurpose: task proposal\nDisclosed content ceiling applies\nProvider credential: authentication only\nOwning-tenant policy authoritative\nNO passkeys, recovery, or other tenant secrets",
            color: "red",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1615,
            1080,
            270,
            440,
            "INDEPENDENT PEER INSTANCE\nFUTURE / GATED\nMay receive user-approved purpose-scoped minimum disclosure\nMay submit a proposal; never an authoritative write\nOwning tenant stays authoritative\nCURRENT: no content / traffic",
            color: "red",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1615,
            1550,
            270,
            260,
            "Future external channel / connector systems\nUntrusted inbound content\nFUTURE / GATED",
            color: "red",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1615,
            1840,
            270,
            340,
            "PUBLIC / HELP HOSTING\nFUTURE / GATED external surface\nSeparate origin + deployment\nReceives NO app cookie, token,\nor product / user data",
            color: "red",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1615,
            2210,
            270,
            240,
            "Future tenant-less feedback / support intake\nSeparate governance\nFUTURE / GATED",
            color: "red",
            status: "future",
            fontSize: 22);
        d.Rectangle(
            1615,
            2480,
            270,
            210,
            "GATED operator-controlled\nbackup destination\nNo destination approved",
            color: "red",
            status: "future",
            fontSize: 22);

        d.Arrow(
            290,
            545,
            390,
            820,
            "F1",
            color: "blue",
            labelAt: (275, 680));
        d.Arrow(
            290,
            520,
            645,
            520,
            "F2a",
            color: "blue",
            via: [(320, 400), (630, 400), (630, 520)],
            labelAt: (480, 390));
        d.Arrow(
            775,
            600,
            775,
            700,
            "F2b",
            color: "blue",
            labelAt: (825, 620));
        d.Arrow(
            915,
            800,
            1292,
            620,
            "F2c",
            color: "green",
            via: [(910, 680), (1260, 680), (1260, 620)],
            labelAt: (1080, 705));
        d.Arrow(
            1542,
            620,
            1615,
            620,
            "F2d",
            color: "red",
            status: "future",
            labelAt: (1580, 620));
        d.Arrow(
            915,
            820,
            920,
            820,
            "F3a",
            color: "green",
            labelAt: (1200, 700));
        d.Arrow(
            1200,
            820,
            775,
            950,
            "F3b",
            color: "green",
            via: [(1245, 820), (1245, 930), (775, 930)],
            labelAt: (1180, 930));
        d.Arrow(
            775,
            1130,
            475,
            1880,
            "F3c",
            color: "green",
            via: [(640, 1150), (640, 1760), (475, 1760)],
            labelAt: (665, 1710));
        d.Arrow(
            700,
            600,
            655,
            1040,
            "F4",
            color: "green",
            via: [(640, 630), (640, 1040)],
            labelAt: (675, 620));
        d.Arrow(
            895,
            1040,
            920,
            1040,
            "F5a",
            color: "green",
            labelAt: (908, 930));
        d.Arrow(
            1060,
            1130,
            1060,
            1170,
            "F5b",
            color: "green",
            labelAt: (1110, 1150));
        d.Arrow(
            1200,
            1280,
            475,
            1880,
            "F5c",
            color: "green",
            via: [(1245, 1280), (1245, 1760), (475, 1760)],
            labelAt: (1200, 1710));
        d.Arrow(
            1615,
            1680,
            390,
            1040,
            "F6",
            color: "purple",
            status: "future",
            via: [(1590, 1680), (1590, 1740), (275, 1740), (275, 1040)],
            labelAt: (275, 1650));
        d.Arrow(
            510,
            1690,
            530,
            2240,
            "F7",
            color: "teal",
            via: [(530, 1760)],
            labelAt: (575, 1815));
        d.Arrow(
            775,
            1470,
            630,
            1600,
            "S1",
            color: "orange",
            status: "contract",
            labelAt: (690, 1490));
        d.Arrow(
            475,
            2200,
            780,
            2240,
            "F8a",
            color: "purple",
            status: "future",
            labelAt: (625, 2220));
        d.Arrow(
            945,
            2100,
            945,
            2240,
            "F8b",
            color: "purple",
            status: "future",
            labelAt: (995, 2140));
        d.Arrow(
            1180,
            2140,
            1030,
            2240,
            "F8c",
            color: "purple",
            status: "future",
            via: [(1100, 2150)],
            labelAt: (1095, 2190));
        d.Arrow(
            1060,
            2440,
            1615,
            2585,
            "F8d",
            color: "purple",
            status: "future",
            via: [(1080, 2145), (1590, 2145), (1590, 2585)],
            labelAt: (1450, 2125));
        d.Arrow(
            775,
            1690,
            1430,
            1880,
            "F9",
            color: "teal",
            via: [(775, 1760), (1430, 1760)],
            labelAt: (1350, 1710));
        d.Arrow(
            510,
            1440,
            1615,
            1300,
            "F10 ↔",
            color: "red",
            status: "future",
            via:
            [
                (510, 1490),
                (1270, 1490),
                (1270, 1050),
                (1590, 1050),
                (1590, 1300),
            ],
            labelAt: (1450, 1050),
            bidirectional: true);

        return d;
    }

    public static ReadabilityReport ValidateDiagramReadability(DiagramBuilder diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);

        var scale = (double)GitHubPresentationWidth / Width;
        var rectangles = new Dictionary<string, DiagramElement>(StringComparer.Ordinal);
        var textElements = new List<DiagramElement>();
        foreach (var element in diagram.Elements)
        {
            if (string.Equals(element.Type, "rectangle", StringComparison.Ordinal))
            {
                rectangles[element.Id] = element;
            }
            else if (string.Equals(element.Type, "text", StringComparison.Ordinal))
            {
                textElements.Add(element);
            }
        }

        var errors = new List<string>();
        foreach (var element in textElements)
        {
            if (element.FontSize * scale < MinEffectiveFontSize)
            {
                errors.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{element.Id} renders at {element.FontSize * scale:F1}px, below {MinEffectiveFontSize}px"));
            }

            var fontWeight = element.FontWeight ?? "normal";
            var textValue = element.Text ?? string.Empty;
            foreach (var line in RepositoryPaths.SplitLines(textValue))
            {
                var estimatedWidth = EstimatedTextWidth(line, element.FontSize, fontWeight);
                if (estimatedWidth > element.Width)
                {
                    errors.Add(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{element.Id} line is {estimatedWidth:F1}px wide in {element.Width}px: '{line}'"));
                }
            }

            var right = element.X + element.Width;
            var bottom = element.Y + element.Height;
            if (element.X < 0 || element.Y < 0 || right > Width || bottom > Height)
            {
                errors.Add($"{element.Id} leaves the {Width}x{Height} canvas");
            }

            if (!string.IsNullOrEmpty(element.ContainerId))
            {
                if (!rectangles.TryGetValue(element.ContainerId, out var container))
                {
                    errors.Add($"{element.Id} refers to missing container {element.ContainerId}");
                }
                else if (element.X < container.X
                    || element.Y < container.Y
                    || right > container.X + container.Width
                    || bottom > container.Y + container.Height)
                {
                    errors.Add($"{element.Id} crosses container {element.ContainerId}");
                }
            }
        }

        foreach (var element in diagram.Elements)
        {
            if (!string.Equals(element.Type, "arrow", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var point in element.Points)
            {
                var absX = element.X + point.X;
                var absY = element.Y + point.Y;
                if (absX < 0 || absX > Width || absY < 0 || absY > Height)
                {
                    errors.Add(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Arrow {element.Id} point ({absX}, {absY}) is outside the {Width}x{Height} canvas"));
                }
            }
        }

        var flowLabels = rectangles.Values
            .Where(rect => string.Equals(rect.DiagramRole, "flow-label", StringComparison.Ordinal))
            .ToArray();
        var nodes = rectangles.Values
            .Where(rect => !string.Equals(rect.DiagramRole, "boundary", StringComparison.Ordinal)
                && !string.Equals(rect.DiagramRole, "flow-label", StringComparison.Ordinal))
            .ToArray();
        var boundaries = rectangles.Values
            .Where(rect => string.Equals(rect.DiagramRole, "boundary", StringComparison.Ordinal))
            .ToArray();

        var actualTextRectangles = new List<(DiagramElement Element, (int X, int Y, int Width, int Height) Bounds)>(
            textElements.Count);
        foreach (var element in textElements)
        {
            var fontWeight = element.FontWeight ?? "normal";
            var lines = RepositoryPaths.SplitLines(element.Text ?? string.Empty);
            var maxLineWidth = 0.0;
            foreach (var line in lines)
            {
                var lineWidth = EstimatedTextWidth(line, element.FontSize, fontWeight);
                if (lineWidth > maxLineWidth)
                {
                    maxLineWidth = lineWidth;
                }
            }

            var actualWidth = (int)Math.Ceiling(maxLineWidth);
            var x = element.X;
            if (string.Equals(element.TextAlign, "center", StringComparison.Ordinal))
            {
                x += DiagramBuilder.FloorDiv2(element.Width - actualWidth);
            }
            else if (string.Equals(element.TextAlign, "right", StringComparison.Ordinal))
            {
                x += element.Width - actualWidth;
            }

            actualTextRectangles.Add((element, (x, element.Y, actualWidth, element.Height)));
        }

        var boundaryLabels = actualTextRectangles
            .Where(entry => string.Equals(entry.Element.DiagramRole, "boundary-label", StringComparison.Ordinal))
            .ToArray();
        foreach (var (boundaryLabel, bounds) in boundaryLabels)
        {
            foreach (var node in nodes)
            {
                if (RectanglesOverlap(bounds, (node.X, node.Y, node.Width, node.Height), margin: 4))
                {
                    errors.Add($"{boundaryLabel.Id} overlaps node/callout {node.Id}");
                }
            }
        }

        foreach (var flowLabel in flowLabels)
        {
            var flowBounds = (flowLabel.X, flowLabel.Y, flowLabel.Width, flowLabel.Height);
            foreach (var node in nodes)
            {
                if (RectanglesOverlap(flowBounds, (node.X, node.Y, node.Width, node.Height), margin: 2))
                {
                    errors.Add($"{flowLabel.Id} overlaps node/callout {node.Id}");
                }
            }

            foreach (var (boundaryLabel, bounds) in boundaryLabels)
            {
                if (RectanglesOverlap(flowBounds, bounds, margin: 4))
                {
                    errors.Add($"{flowLabel.Id} overlaps boundary label {boundaryLabel.Id}");
                }
            }

            foreach (var boundary in boundaries)
            {
                var boundaryBounds = (boundary.X, boundary.Y, boundary.Width, boundary.Height);
                if (!RectanglesOverlap(flowBounds, boundaryBounds, margin: 0))
                {
                    continue;
                }

                var fullyInside = flowLabel.X >= boundary.X + 2
                    && flowLabel.Y >= boundary.Y + 2
                    && flowLabel.X + flowLabel.Width <= boundary.X + boundary.Width - 2
                    && flowLabel.Y + flowLabel.Height <= boundary.Y + boundary.Height - 2;
                if (!fullyInside)
                {
                    errors.Add($"{flowLabel.Id} crosses boundary {boundary.Id}");
                }
            }
        }

        for (var index = 0; index < flowLabels.Length; index++)
        {
            var first = flowLabels[index];
            var firstBounds = (first.X, first.Y, first.Width, first.Height);
            for (var secondIndex = index + 1; secondIndex < flowLabels.Length; secondIndex++)
            {
                var second = flowLabels[secondIndex];
                if (RectanglesOverlap(firstBounds, (second.X, second.Y, second.Width, second.Height), margin: 2))
                {
                    errors.Add($"{first.Id} overlaps flow label {second.Id}");
                }
            }
        }

        for (var index = 0; index < actualTextRectangles.Count; index++)
        {
            var (first, firstBounds) = actualTextRectangles[index];
            for (var secondIndex = index + 1; secondIndex < actualTextRectangles.Count; secondIndex++)
            {
                var (second, secondBounds) = actualTextRectangles[secondIndex];
                if (RectanglesOverlap(firstBounds, secondBounds, margin: 2))
                {
                    errors.Add($"{first.Id} text overlaps {second.Id} text");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Diagram readability geometry failed:\n- " + string.Join("\n- ", errors));
        }

        var minSourceFont = textElements.Min(element => element.FontSize);
        var minEffectiveFont = textElements.Min(element => element.FontSize * scale);
        var containedLabels = textElements.Count(element => !string.IsNullOrEmpty(element.ContainerId));
        return new ReadabilityReport(
            GitHubPresentationWidth,
            scale,
            minSourceFont,
            minEffectiveFont,
            containedLabels,
            OverflowCount: 0);
    }

    public static string ReadabilitySummary(ReadabilityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Readability at {report.PresentationWidth}px: {report.Scale:F3} scale, {report.MinimumEffectiveFont:F1}px minimum text, {report.ContainedLabels} contained labels, {report.OverflowCount} geometry overflows.");
    }

    public static byte[] ExcalidrawBytes(DiagramBuilder diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        var sb = new StringBuilder(160_000);
        sb.Append("{\n");
        sb.Append("  \"type\": \"excalidraw\",\n");
        sb.Append("  \"version\": 2,\n");
        sb.Append("  \"source\": \"https://github.com/Jamula/Andreja\",\n");
        sb.Append("  \"elements\": [");

        for (var i = 0; i < diagram.Elements.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append('\n');
            AppendExcalidrawElement(sb, diagram.Elements[i]);
        }

        if (diagram.Elements.Count > 0)
        {
            sb.Append("\n  ],\n");
        }
        else
        {
            sb.Append("],\n");
        }

        sb.Append("  \"appState\": {\n");
        sb.Append("    \"viewBackgroundColor\": \"#ffffff\",\n");
        sb.Append("    \"gridSize\": 20,\n");
        sb.Append("    \"gridStep\": 5,\n");
        sb.Append("    \"gridModeEnabled\": false,\n");
        sb.Append("    \"zoom\": {\n");
        sb.Append("      \"value\": 0.55\n");
        sb.Append("    }\n");
        sb.Append("  },\n");
        sb.Append("  \"files\": {}\n");
        sb.Append("}\n");

        return RepositoryPaths.Utf8Bytes(sb.ToString());
    }

    public static byte[] SvgBytes(DiagramBuilder diagram, string sourceHash)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(sourceHash);

        var markers = new StringBuilder();
        foreach (var (name, stroke, _) in OrderedColors)
        {
            markers.Append(
                CultureInfo.InvariantCulture,
                $"<marker id=\"arrow-{name}\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"8\" markerHeight=\"8\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"{stroke}\"/></marker>");
        }

        var body = string.Join("\n  ", diagram.Svg);
        var output = string.Create(
            CultureInfo.InvariantCulture,
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\"\n" +
            $"     role=\"img\" aria-labelledby=\"title desc\" data-source-sha256=\"{sourceHash}\">\n" +
            $"  <title id=\"title\">Andreja high-level architecture and data flows</title>\n" +
            $"  <desc id=\"desc\">Current Phase 1A components, current local conformance contracts, future gated components,\n" +
            $"  six labelled trust boundaries, ten numbered data flows, a semantic provenance flow, local data custody,\n" +
            $"  and extension seams.</desc>\n" +
            $"  <defs>\n" +
            $"    {markers}\n" +
            $"    <style>text {{ font-family: Arial, Helvetica, sans-serif; }}</style>\n" +
            $"  </defs>\n" +
            $"  <rect width=\"{Width}\" height=\"{Height}\" fill=\"#ffffff\"/>\n" +
            $"  {body}\n" +
            $"</svg>\n");

        return RepositoryPaths.Utf8Bytes(output);
    }

    public static string FindEdgeExecutable()
    {
        string[] candidates =
        [
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        ];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Microsoft Edge was not found; cannot render PNG.");
    }

    public static IReadOnlyList<PngChunkRecord> PngChunks(ReadOnlySpan<byte> data)
    {
        if (data.Length < PngSignatureBytes.Length
            || !data[..PngSignatureBytes.Length].SequenceEqual(PngSignatureBytes))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }

        var chunks = new List<PngChunkRecord>();
        var offset = PngSignatureBytes.Length;
        while (offset < data.Length)
        {
            if (offset + 12 > data.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > int.MaxValue - 12 || offset + 12L + length > data.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk data.");
            }

            var intLength = (int)length;
            var chunkType = data.Slice(offset + 4, 4);
            var chunkData = data.Slice(offset + 8, intLength);
            var chunkEnd = offset + 12 + intLength;
            var recordedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + intLength, 4));
            var actualCrc = ComputeCrc32(chunkType, chunkData);
            if (recordedCrc != actualCrc)
            {
                throw new InvalidDataException($"Invalid PNG CRC for {DecodeAsciiSafe(chunkType)}.");
            }

            chunks.Add(new PngChunkRecord(chunkType, chunkData));
            offset = chunkEnd;
            if (chunkType.SequenceEqual("IEND"u8))
            {
                if (offset != data.Length)
                {
                    throw new InvalidDataException("Unexpected data after PNG IEND.");
                }

                break;
            }
        }

        if (chunks.Count == 0 || !chunks[^1].TypeBytes.SequenceEqual("IEND"u8))
        {
            throw new InvalidDataException("PNG is missing IEND.");
        }

        return chunks;
    }

    public static byte[] PngChunk(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        if (chunkType.Length != 4)
        {
            throw new InvalidDataException("PNG chunk type must be 4 bytes.");
        }

        var buffer = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)data.Length);
        chunkType.CopyTo(buffer.AsSpan(4, 4));
        data.CopyTo(buffer.AsSpan(8, data.Length));
        var crc = ComputeCrc32(chunkType, data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8 + data.Length, 4), crc);
        return buffer;
    }

    public static void ValidatePngPolicy(IReadOnlyList<PngChunkRecord> chunks, bool requireProvenance)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var disallowed = chunks
            .Select(chunk => chunk.TypeName)
            .Where(name => !AllowedChunkNames.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (disallowed.Length > 0)
        {
            throw new InvalidDataException($"Non-allowlisted PNG chunk: {string.Join(", ", disallowed)}");
        }

        var ihdrCount = chunks.Count(chunk => string.Equals(chunk.TypeName, "IHDR", StringComparison.Ordinal));
        if (chunks.Count == 0 || !string.Equals(chunks[0].TypeName, "IHDR", StringComparison.Ordinal) || ihdrCount != 1)
        {
            throw new InvalidDataException("PNG policy requires exactly one leading IHDR.");
        }

        if (chunks[0].DataBytes.Length != 13)
        {
            throw new InvalidDataException("PNG policy requires the IHDR payload to be exactly 13 bytes.");
        }

        var iendCount = chunks.Count(chunk => string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal));
        if (!string.Equals(chunks[^1].TypeName, "IEND", StringComparison.Ordinal) || iendCount != 1)
        {
            throw new InvalidDataException("PNG policy requires exactly one trailing IEND.");
        }

        if (chunks[^1].DataBytes.Length != 0)
        {
            throw new InvalidDataException("PNG policy requires the IEND payload to be empty.");
        }

        var idatIndexes = new List<int>();
        for (var index = 0; index < chunks.Count; index++)
        {
            if (string.Equals(chunks[index].TypeName, "IDAT", StringComparison.Ordinal))
            {
                idatIndexes.Add(index);
            }
        }

        if (idatIndexes.Count == 0)
        {
            throw new InvalidDataException("PNG is missing IDAT.");
        }

        for (var i = 0; i < idatIndexes.Count; i++)
        {
            if (idatIndexes[i] != idatIndexes[0] + i)
            {
                throw new InvalidDataException("PNG IDAT chunks must be contiguous.");
            }
        }

        var textChunks = chunks.Where(chunk => string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal)).ToArray();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var textChunk in textChunks)
        {
            var dataBytes = textChunk.DataBytes;
            var nullIndex = dataBytes.IndexOf((byte)0);
            if (nullIndex < 0)
            {
                throw new InvalidDataException("Malformed PNG tEXt chunk.");
            }

            var rawKey = dataBytes[..nullIndex];
            var rawValue = dataBytes[(nullIndex + 1)..];
            if (!TryDecodeStrictAscii(rawKey, out var key) || !TryDecodeStrictAscii(rawValue, out var value))
            {
                throw new InvalidDataException("PNG tEXt provenance must be ASCII.");
            }

            if (!ExpectedProvenanceKeySet.Contains(key))
            {
                throw new InvalidDataException($"Non-allowlisted PNG tEXt keyword: {key}");
            }

            if (!seenKeys.Add(key))
            {
                throw new InvalidDataException($"Duplicate PNG provenance tEXt keyword: {key}");
            }

            if (!IsLowerHex64(value))
            {
                throw new InvalidDataException($"PNG tEXt {key} must be a 64-character lowercase SHA-256 digest.");
            }
        }

        if (requireProvenance && !ExpectedProvenanceKeySet.SetEquals(seenKeys))
        {
            throw new InvalidDataException("PNG provenance requires exact source, SVG, raster, and binding tEXt chunks.");
        }

        if (!requireProvenance && seenKeys.Count > 0 && !ExpectedProvenanceKeySet.SetEquals(seenKeys))
        {
            throw new InvalidDataException("PNG policy permits either no provenance or the exact provenance set.");
        }
    }

    public static (uint Width, uint Height) PngDimensions(ReadOnlySpan<byte> data)
    {
        var chunks = PngChunks(data);
        if (!chunks[0].TypeBytes.SequenceEqual("IHDR"u8) || chunks[0].DataBytes.Length != 13)
        {
            throw new InvalidDataException("PNG is missing a valid 13-byte IHDR chunk.");
        }

        var ihdr = chunks[0].DataBytes;
        return (
            BinaryPrimitives.ReadUInt32BigEndian(ihdr[..4]),
            BinaryPrimitives.ReadUInt32BigEndian(ihdr.Slice(4, 4)));
    }

    public static Dictionary<string, string> PngTextMetadata(ReadOnlySpan<byte> data)
    {
        var chunks = PngChunks(data);
        ValidatePngPolicy(chunks, requireProvenance: true);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            if (!string.Equals(chunk.TypeName, "tEXt", StringComparison.Ordinal))
            {
                continue;
            }

            var chunkBytes = chunk.DataBytes;
            var separator = chunkBytes.IndexOf((byte)0);
            var key = Encoding.ASCII.GetString(chunkBytes[..separator]);
            var value = Encoding.ASCII.GetString(chunkBytes[(separator + 1)..]);
            metadata[key] = value;
        }

        return metadata;
    }

    public static byte[] NormalizedPngRasterBytes(ReadOnlySpan<byte> data, bool requireProvenance = false)
    {
        var chunks = PngChunks(data);
        ValidatePngPolicy(chunks, requireProvenance);
        using var stream = new MemoryStream(data.Length);
        stream.Write(PngSignatureBytes);
        foreach (var chunk in chunks)
        {
            if (string.Equals(chunk.TypeName, "IHDR", StringComparison.Ordinal)
                || string.Equals(chunk.TypeName, "IDAT", StringComparison.Ordinal)
                || string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                stream.Write(PngChunk(chunk.TypeBytes, chunk.DataBytes));
            }
        }

        return stream.ToArray();
    }

    public static string PngRasterHash(IReadOnlyList<PngChunkRecord> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        using var material = new MemoryStream();
        foreach (var chunk in chunks)
        {
            if (string.Equals(chunk.TypeName, "IHDR", StringComparison.Ordinal)
                || string.Equals(chunk.TypeName, "IDAT", StringComparison.Ordinal))
            {
                material.Write(chunk.TypeBytes);
                material.Write(chunk.DataBytes);
            }
        }

        if (material.Length == 0)
        {
            throw new InvalidDataException("PNG contains no raster data.");
        }

        return RepositoryPaths.Sha256Hex(material.ToArray());
    }

    public static string PngRasterHash(ReadOnlySpan<byte> data, bool requireProvenance = false)
    {
        var chunks = PngChunks(data);
        ValidatePngPolicy(chunks, requireProvenance);
        return PngRasterHash(chunks);
    }

    public static string PngBindingHash(string sourceHash, string svgHash, string rasterHash)
    {
        ArgumentNullException.ThrowIfNull(sourceHash);
        ArgumentNullException.ThrowIfNull(svgHash);
        ArgumentNullException.ThrowIfNull(rasterHash);

        var sourceBytes = Convert.FromHexString(sourceHash);
        var svgBytes = Convert.FromHexString(svgHash);
        var rasterBytes = Convert.FromHexString(rasterHash);
        var material = new byte[sourceBytes.Length + svgBytes.Length + rasterBytes.Length];
        sourceBytes.CopyTo(material.AsSpan(0, sourceBytes.Length));
        svgBytes.CopyTo(material.AsSpan(sourceBytes.Length, svgBytes.Length));
        rasterBytes.CopyTo(material.AsSpan(sourceBytes.Length + svgBytes.Length, rasterBytes.Length));
        return RepositoryPaths.Sha256Hex(material);
    }

    public static byte[] EmbedPngProvenance(ReadOnlySpan<byte> data, string sourceHash, string svgHash)
    {
        ArgumentNullException.ThrowIfNull(sourceHash);
        ArgumentNullException.ThrowIfNull(svgHash);

        var normalizedData = NormalizedPngRasterBytes(data);
        var chunks = PngChunks(normalizedData);
        var rasterHash = PngRasterHash(chunks);
        var bindingHash = PngBindingHash(sourceHash, svgHash, rasterHash);
        var metadataValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = sourceHash,
            ["svg"] = svgHash,
            ["raster"] = rasterHash,
            ["binding"] = bindingHash,
        };

        using var output = new MemoryStream(normalizedData.Length + 512);
        output.Write(PngSignatureBytes);
        foreach (var chunk in chunks)
        {
            if (string.Equals(chunk.TypeName, "IEND", StringComparison.Ordinal))
            {
                foreach (var (name, metadataKey) in OrderedProvenanceKeys)
                {
                    var value = metadataValues[name];
                    var payload = Encoding.ASCII.GetBytes($"{metadataKey}\0{value}");
                    output.Write(PngChunk("tEXt"u8, payload));
                }
            }

            output.Write(PngChunk(chunk.TypeBytes, chunk.DataBytes));
        }

        return output.ToArray();
    }

    public static IReadOnlyList<string> VerifyPngArtifact(
        ReadOnlySpan<byte> pngBytes,
        string pngHashPath,
        string expectedSourceHash,
        string expectedSvgHash)
    {
        ArgumentNullException.ThrowIfNull(pngHashPath);
        ArgumentNullException.ThrowIfNull(expectedSourceHash);
        ArgumentNullException.ThrowIfNull(expectedSvgHash);

        var stale = new List<string>();
        var relativeHashName = "docs/architecture/" + Path.GetFileName(pngHashPath);
        const string pngDisplayPath = "docs/architecture/andreja-high-level.png";

        Dictionary<string, string> metadata;
        string actualRasterHash;
        try
        {
            var (width, height) = PngDimensions(pngBytes);
            if ((width, height) != ((uint)Width, (uint)Height))
            {
                stale.Add($"{pngDisplayPath} dimensions are {width}x{height}; expected {Width}x{Height}");
            }

            metadata = PngTextMetadata(pngBytes);
            actualRasterHash = PngRasterHash(pngBytes, requireProvenance: true);
        }
        catch (InvalidDataException ex)
        {
            return [$"{pngDisplayPath} ({ex.Message})"];
        }

        var expectedBindingHash = PngBindingHash(expectedSourceHash, expectedSvgHash, actualRasterHash);
        if (!metadata.TryGetValue(SourceMetadataKey, out var recordedSource)
            || !string.Equals(recordedSource, expectedSourceHash, StringComparison.Ordinal))
        {
            stale.Add($"{pngDisplayPath} ({SourceMetadataKey})");
        }

        if (!metadata.TryGetValue(SvgMetadataKey, out var recordedSvg)
            || !string.Equals(recordedSvg, expectedSvgHash, StringComparison.Ordinal))
        {
            stale.Add($"{pngDisplayPath} ({SvgMetadataKey})");
        }

        if (!metadata.TryGetValue(RasterMetadataKey, out var recordedRaster)
            || !string.Equals(recordedRaster, actualRasterHash, StringComparison.Ordinal))
        {
            stale.Add($"{pngDisplayPath} ({RasterMetadataKey})");
        }

        if (!metadata.TryGetValue(BindingMetadataKey, out var recordedBinding)
            || !string.Equals(recordedBinding, expectedBindingHash, StringComparison.Ordinal))
        {
            stale.Add($"{pngDisplayPath} ({BindingMetadataKey})");
        }

        if (!File.Exists(pngHashPath))
        {
            stale.Add($"{relativeHashName} (missing)");
        }
        else
        {
            var expectedManifest = $"{RepositoryPaths.Sha256Hex(pngBytes)}  andreja-high-level.png\n";
            var actualManifest = RepositoryPaths.NormalizeLf(File.ReadAllText(pngHashPath));
            if (!string.Equals(actualManifest, expectedManifest, StringComparison.Ordinal))
            {
                stale.Add(relativeHashName);
            }
        }

        return stale;
    }

    public static void WriteBytesAtomically(string targetPath, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(targetPath);

        var parentDir = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidDataException($"Could not determine parent directory for {targetPath}");
        var tempPath = Path.Join(parentDir, $".{Path.GetFileName(targetPath)}.tmp");
        try
        {
            File.WriteAllBytes(tempPath, data.ToArray());
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public static void RenderPng(
        string svgPath,
        string outputPath,
        string sourceHash,
        string svgHash,
        string? stagingPngPath = null,
        Action<string, string>? browserRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(svgPath);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(sourceHash);
        ArgumentNullException.ThrowIfNull(svgHash);

        var effectiveStaging = stagingPngPath
            ?? Path.Join(
                Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory(),
                ".andreja-high-level.rendering.png");
        TryDeleteFile(effectiveStaging);

        try
        {
            if (browserRenderer is not null)
            {
                browserRenderer(svgPath, effectiveStaging);
            }
            else
            {
                RunEdgeHeadless(svgPath, effectiveStaging);
            }

            if (!File.Exists(effectiveStaging))
            {
                throw new InvalidDataException("Microsoft Edge completed without writing the staging PNG.");
            }

            var rawPng = File.ReadAllBytes(effectiveStaging);
            var (width, height) = PngDimensions(rawPng);
            if ((width, height) != ((uint)Width, (uint)Height))
            {
                throw new InvalidDataException(
                    $"Rendered PNG dimensions were {width}x{height}; expected {Width}x{Height}.");
            }

            var embedded = EmbedPngProvenance(rawPng, sourceHash, svgHash);
            WriteBytesAtomically(outputPath, embedded);
        }
        finally
        {
            TryDeleteFile(effectiveStaging);
        }
    }

    public static int ExecuteCli(
        ArchitectureArtifactPaths paths,
        bool check,
        bool renderPng,
        TextWriter stdout,
        TextWriter stderr,
        Action<string, string, string, string>? pngRenderer = null,
        Action<string, ReadOnlySpan<byte>>? atomicWriter = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        Directory.CreateDirectory(paths.OutputDirectory);
        var diagram = BuildDiagram();
        var readability = ValidateDiagramReadability(diagram);
        var excalidraw = ExcalidrawBytes(diagram);
        var sourceHash = RepositoryPaths.Sha256Hex(excalidraw);
        var svg = SvgBytes(diagram, sourceHash);
        var svgHash = RepositoryPaths.Sha256Hex(svg);

        if (check)
        {
            var stale = new List<string>();
            if (!File.Exists(paths.ExcalidrawPath)
                || !RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.ExcalidrawPath)).AsSpan().SequenceEqual(excalidraw))
            {
                stale.Add("docs/architecture/andreja-high-level.excalidraw");
            }

            if (!File.Exists(paths.SvgPath)
                || !RepositoryPaths.NormalizeCrLfBytes(File.ReadAllBytes(paths.SvgPath)).AsSpan().SequenceEqual(svg))
            {
                stale.Add("docs/architecture/andreja-high-level.svg");
            }

            if (!File.Exists(paths.PngPath))
            {
                stale.Add("docs/architecture/andreja-high-level.png (missing)");
            }
            else
            {
                var pngBytes = File.ReadAllBytes(paths.PngPath);
                stale.AddRange(VerifyPngArtifact(pngBytes, paths.PngHashPath, sourceHash, svgHash));
            }

            if (stale.Count > 0)
            {
                stderr.WriteLine("Architecture artifacts are stale: " + string.Join(", ", stale));
                return 1;
            }

            stdout.WriteLine($"Architecture artifacts are consistent (source SHA-256 {sourceHash}).");
            stdout.WriteLine(ReadabilitySummary(readability));
            return 0;
        }

        var writer = atomicWriter ?? WriteBytesAtomically;
        string? stagedRenderedPng = null;
        if (renderPng)
        {
            var tempSvgPath = Path.Join(paths.OutputDirectory, $".{Path.GetFileName(paths.SvgPath)}.render-input.svg");
            stagedRenderedPng = Path.Join(paths.OutputDirectory, $".{Path.GetFileName(paths.PngPath)}.staged");
            TryDeleteFile(stagedRenderedPng);
            try
            {
                writer(tempSvgPath, svg);
                if (pngRenderer is not null)
                {
                    pngRenderer(tempSvgPath, stagedRenderedPng, sourceHash, svgHash);
                }
                else
                {
                    RenderPng(tempSvgPath, stagedRenderedPng, sourceHash, svgHash, paths.StagingPngPath);
                }
            }
            catch
            {
                TryDeleteFile(stagedRenderedPng);
                throw;
            }
            finally
            {
                TryDeleteFile(tempSvgPath);
            }
        }

        var targets = new List<string> { paths.ExcalidrawPath, paths.SvgPath };
        if (renderPng)
        {
            targets.Add(paths.PngPath);
            targets.Add(paths.PngHashPath);
        }

        var backups = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            backups[target] = File.Exists(target) ? File.ReadAllBytes(target) : null;
        }

        try
        {
            writer(paths.ExcalidrawPath, excalidraw);
            writer(paths.SvgPath, svg);
            if (stagedRenderedPng is not null)
            {
                var pngBytes = File.ReadAllBytes(stagedRenderedPng);
                var pngHash = RepositoryPaths.Sha256Hex(pngBytes);
                File.Move(stagedRenderedPng, paths.PngPath, overwrite: true);
                stagedRenderedPng = null;
                var manifestBytes = Encoding.ASCII.GetBytes($"{pngHash}  {Path.GetFileName(paths.PngPath)}\n");
                writer(paths.PngHashPath, manifestBytes);
            }
        }
        catch
        {
            if (stagedRenderedPng is not null)
            {
                TryDeleteFile(stagedRenderedPng);
            }

            foreach (var (target, previousBytes) in backups)
            {
                if (previousBytes is null)
                {
                    TryDeleteFile(target);
                }
                else
                {
                    WriteBytesAtomically(target, previousBytes);
                }
            }

            throw;
        }

        if (renderPng)
        {
            var pngHash = RepositoryPaths.Sha256Hex(File.ReadAllBytes(paths.PngPath));
            stdout.WriteLine($"Wrote docs/architecture/{Path.GetFileName(paths.PngPath)}");
            stdout.WriteLine($"Wrote docs/architecture/{Path.GetFileName(paths.PngHashPath)} ({pngHash})");
        }

        stdout.WriteLine($"Wrote docs/architecture/{Path.GetFileName(paths.ExcalidrawPath)} ({sourceHash})");
        stdout.WriteLine($"Wrote docs/architecture/{Path.GetFileName(paths.SvgPath)}");
        stdout.WriteLine(ReadabilitySummary(readability));
        return 0;
    }

    public static uint ComputeCrc32(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in chunkType)
        {
            crc = Crc32Table[(crc ^ b) & 0xFFu] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = Crc32Table[(crc ^ b) & 0xFFu] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    internal static string EscapeHtml(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '\'':
                    sb.Append("&#x27;");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }

    private static void RunEdgeHeadless(string svgPath, string stagingPngPath)
    {
        var edgePath = FindEdgeExecutable();
        var svgFileUri = new Uri(Path.GetFullPath(svgPath)).AbsoluteUri;
        var startInfo = new ProcessStartInfo(edgePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--disable-gpu");
        startInfo.ArgumentList.Add("--hide-scrollbars");
        startInfo.ArgumentList.Add("--force-device-scale-factor=1");
        startInfo.ArgumentList.Add(string.Create(CultureInfo.InvariantCulture, $"--window-size={Width},{Height}"));
        startInfo.ArgumentList.Add($"--screenshot={Path.GetFullPath(stagingPngPath)}");
        startInfo.ArgumentList.Add(svgFileUri);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidDataException("Failed to start Microsoft Edge process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Microsoft Edge timed out after 60 seconds while rendering PNG.");
        }

        Task.WaitAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"Microsoft Edge failed with exit code {process.ExitCode}: {stderrTask.Result.Trim()}");
        }
    }

    private static List<string> SplitByWhitespace(string text)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                if (start >= 0)
                {
                    words.Add(text[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            words.Add(text[start..]);
        }

        return words;
    }

    private static bool RectanglesOverlap(
        (int X, int Y, int Width, int Height) first,
        (int X, int Y, int Width, int Height) second,
        int margin)
    {
        return first.X < second.X + second.Width + margin
            && first.X + first.Width + margin > second.X
            && first.Y < second.Y + second.Height + margin
            && first.Y + first.Height + margin > second.Y;
    }

    private static void AppendExcalidrawElement(StringBuilder sb, DiagramElement element)
    {
        sb.Append("    {\n");
        sb.Append("      \"id\": ");
        AppendJsonString(sb, element.Id);
        sb.Append(",\n      \"type\": ");
        AppendJsonString(sb, element.Type);
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"x\": {element.X}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"y\": {element.Y}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"width\": {element.Width}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"height\": {element.Height}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"angle\": {element.Angle}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"strokeWidth\": {element.StrokeWidth}");
        sb.Append(",\n      \"strokeStyle\": ");
        AppendJsonString(sb, element.StrokeStyle);
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"roughness\": {element.Roughness}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"opacity\": {element.Opacity}");
        sb.Append(",\n      \"groupIds\": []");
        sb.Append(",\n      \"frameId\": null");
        sb.Append(",\n      \"index\": ");
        AppendJsonString(sb, element.Index);
        sb.Append(",\n      \"roundness\": {\n");
        sb.Append(CultureInfo.InvariantCulture, $"        \"type\": {element.RoundnessType}\n");
        sb.Append("      }");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"seed\": {element.Seed}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"version\": {element.Version}");
        sb.Append(CultureInfo.InvariantCulture, $",\n      \"versionNonce\": {element.VersionNonce}");
        sb.Append(",\n      \"isDeleted\": false");

        if (element.BoundElements.Count == 0)
        {
            sb.Append(",\n      \"boundElements\": []");
        }
        else
        {
            sb.Append(",\n      \"boundElements\": [");
            for (var i = 0; i < element.BoundElements.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append("\n        {\n          \"id\": ");
                AppendJsonString(sb, element.BoundElements[i].Id);
                sb.Append(",\n          \"type\": ");
                AppendJsonString(sb, element.BoundElements[i].Type);
                sb.Append("\n        }");
            }

            sb.Append("\n      ]");
        }

        sb.Append(CultureInfo.InvariantCulture, $",\n      \"updated\": {element.Updated}");
        sb.Append(",\n      \"link\": null");
        sb.Append(",\n      \"locked\": false");
        sb.Append(",\n      \"strokeColor\": ");
        AppendJsonString(sb, element.StrokeColor);
        sb.Append(",\n      \"backgroundColor\": ");
        AppendJsonString(sb, element.BackgroundColor);
        sb.Append(",\n      \"fillStyle\": ");
        AppendJsonString(sb, element.FillStyle);

        if (string.Equals(element.Type, "text", StringComparison.Ordinal))
        {
            sb.Append(",\n      \"text\": ");
            AppendJsonString(sb, element.Text ?? string.Empty);
            sb.Append(CultureInfo.InvariantCulture, $",\n      \"fontSize\": {element.FontSize}");
            sb.Append(CultureInfo.InvariantCulture, $",\n      \"fontFamily\": {element.FontFamily}");
            sb.Append(",\n      \"textAlign\": ");
            AppendJsonString(sb, element.TextAlign);
            sb.Append(",\n      \"verticalAlign\": ");
            AppendJsonString(sb, element.VerticalAlign);
            sb.Append(",\n      \"containerId\": ");
            if (element.ContainerId is null)
            {
                sb.Append("null");
            }
            else
            {
                AppendJsonString(sb, element.ContainerId);
            }

            sb.Append(",\n      \"originalText\": ");
            AppendJsonString(sb, element.OriginalText ?? string.Empty);
            sb.Append(",\n      \"autoResize\": ");
            sb.Append(element.AutoResize ? "true" : "false");
            sb.Append(CultureInfo.InvariantCulture, $",\n      \"lineHeight\": {element.LineHeight:G}");
            sb.Append(",\n      \"customData\": {\n        \"diagramRole\": ");
            AppendJsonString(sb, element.DiagramRole);
            sb.Append(",\n        \"fontWeight\": ");
            AppendJsonString(sb, element.FontWeight ?? "normal");
            sb.Append("\n      }\n");
        }
        else if (string.Equals(element.Type, "rectangle", StringComparison.Ordinal))
        {
            sb.Append(",\n      \"customData\": {\n        \"diagramRole\": ");
            AppendJsonString(sb, element.DiagramRole);
            sb.Append("\n      }\n");
        }
        else if (string.Equals(element.Type, "arrow", StringComparison.Ordinal))
        {
            sb.Append(",\n      \"points\": [");
            for (var i = 0; i < element.Points.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append("\n        [\n          ");
                sb.Append(element.Points[i].X.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\n          ");
                sb.Append(element.Points[i].Y.ToString(CultureInfo.InvariantCulture));
                sb.Append("\n        ]");
            }

            sb.Append("\n      ]");
            sb.Append(",\n      \"lastCommittedPoint\": null");
            sb.Append(",\n      \"startBinding\": null");
            sb.Append(",\n      \"endBinding\": null");
            sb.Append(",\n      \"startArrowhead\": ");
            if (element.StartArrowhead is null)
            {
                sb.Append("null");
            }
            else
            {
                AppendJsonString(sb, element.StartArrowhead);
            }

            sb.Append(",\n      \"endArrowhead\": ");
            if (element.EndArrowhead is null)
            {
                sb.Append("null");
            }
            else
            {
                AppendJsonString(sb, element.EndArrowhead);
            }

            sb.Append(",\n      \"elbowed\": ");
            sb.Append(element.Elbowed ? "true" : "false");
            sb.Append(",\n      \"customData\": {\n        \"diagramRole\": ");
            AppendJsonString(sb, element.DiagramRole);
            sb.Append(",\n        \"flowId\": ");
            AppendJsonString(sb, element.FlowId ?? string.Empty);
            sb.Append("\n      }\n");
        }

        sb.Append("    }");
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append(CultureInfo.InvariantCulture, $"\\u{(int)ch:x4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
    }

    private static bool TryDecodeStrictAscii(ReadOnlySpan<byte> bytes, out string decoded)
    {
        foreach (var b in bytes)
        {
            if (b > 0x7F)
            {
                decoded = string.Empty;
                return false;
            }
        }

        decoded = Encoding.ASCII.GetString(bytes);
        return true;
    }

    private static bool IsLowerHex64(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private static string DecodeAsciiSafe(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b is >= 0x20 and <= 0x7E ? (char)b : '?';
        }

        return new string(chars);
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var c = index;
            for (var bit = 0; bit < 8; bit++)
            {
                c = (c & 1u) != 0u ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[index] = c;
        }

        return table;
    }
}
