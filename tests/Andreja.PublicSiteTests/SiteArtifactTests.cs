using System.Text.Json;
using System.Text.Json.Nodes;
using Andreja.PublicSite;

namespace Andreja.PublicSiteTests;

[TestClass]
public sealed class SiteArtifactTests
{
    private static readonly DateOnly ReviewDate = new(2026, 10, 15);
    private string root = "";
    private string scratch = "";
    private string source = "";
    private string config = "";
    private string output = "";

    [TestInitialize]
    public void Initialize()
    {
        root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "Andreja.slnx")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Repository not found.");
        }

        scratch = Path.Combine(root, "artifacts", "public-site-tests", Guid.NewGuid().ToString("N"));
        source = Path.Combine(scratch, "content");
        config = Path.Combine(source, "local.config.json");
        output = Path.Combine(scratch, "site");
        Directory.CreateDirectory(source);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "src", "Andreja.PublicSite", "Content")))
        {
            File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        }
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(scratch, true);

    [TestMethod]
    public void BuildsUsefulVersionedLocalArtifactReproducibly()
    {
        Build();
        var second = Path.Combine(scratch, "second");
        SiteArtifact.Build(source, config, second, root, ReviewDate);
        foreach (var file in Directory.GetFiles(output))
        {
            CollectionAssert.AreEqual(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(second, Path.GetFileName(file))), file);
        }

        var html = File.ReadAllText(Path.Combine(output, "identity.html"));
        StringAssert.Contains(html, "Sign in with a passkey");
        StringAssert.Contains(html, "local-v1");
        StringAssert.Contains(html, "Revalidate before: 2026-11-14");
        StringAssert.Contains(html, "aria-live=\"polite\"");
        StringAssert.Contains(html, "noindex, nofollow, noarchive");
        var search = File.ReadAllText(Path.Combine(output, "search-index.js"));
        StringAssert.Contains(search, "recovery");
        StringAssert.Contains(search, "developer-local-v1.html");
        Assert.IsFalse(File.Exists(Path.Combine(output, ".well-known", "security.txt")));
        var headers = File.ReadAllText(Path.Combine(output, "headers.json"));
        StringAssert.Contains(headers, "Content-Security-Policy");
        StringAssert.Contains(headers, "connect-src");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")))!;
        Assert.IsNotNull(manifest["inputs"]!["docs/help/local-identity.md"]);
        Assert.IsNotNull(manifest["inputs"]!["src/Andreja.PublicSite/SiteArtifact.cs"]);
        SiteArtifact.Verify(output, ReviewDate);
    }

    [TestMethod]
    [DataRow("mode", "public")]
    [DataRow("mode", "private-preview")]
    [DataRow("mode", "production")]
    [DataRow("headerProfile", "permissive")]
    [DataRow("baseUrl", "https://example.org/")]
    [DataRow("baseUrl", "http://localhost:4173/")]
    [DataRow("baseUrl", "http://127.0.0.1:80/")]
    [DataRow("baseUrl", "http://127.0.0.1:4173/app/")]
    [DataRow("baseUrl", "http://user:secret@127.0.0.1:4173/")]
    [DataRow("baseUrl", "http://127.0.0.1:4173/?publish=true")]
    [DataRow("baseUrl", "http://127.0.0.1:4173/#fragment")]
    public void RejectsUnsafeActualConfiguration(string field, string value)
    {
        var json = JsonNode.Parse(File.ReadAllText(config))!;
        json[field] = value;
        File.WriteAllText(config, json.ToJsonString());
        var error = Assert.ThrowsExactly<InvalidDataException>(Build);
        StringAssert.Contains(error.Message, field == "baseUrl" ? "baseUrl" : "mode/headerProfile");
        Assert.IsFalse(Directory.Exists(output), "Rejected input must not emit an artifact.");
    }

    [TestMethod]
    [DataRow("owner", "")]
    [DataRow("expires", "2026-10-15")]
    [DataRow("expires", "2027-10-15")]
    [DataRow("reviewed", "2026-10-16")]
    [DataRow("slug", "../Account/Login")]
    public void RejectsInvalidActualPageMetadata(string field, string value)
    {
        MutateContent(json => json["pages"]![0]![field] = value);
        var error = Assert.ThrowsExactly<InvalidDataException>(Build);
        StringAssert.Contains(error.Message, "Page");
    }

    [TestMethod]
    public void RejectsUnknownFieldsClaimsBrokenLinksAndMissingEvidence()
    {
        MutateContent(json => json["publishApproved"] = true);
        Assert.ThrowsExactly<JsonException>(Build);
        File.Copy(Path.Combine(root, "src", "Andreja.PublicSite", "Content", "site.json"), Path.Combine(source, "site.json"), true);
        MutateContent(json => json["version"] = "production-v1");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "version/locale");
        MutateContent(json =>
        {
            json["version"] = "local-v1";
            json["pages"]![0]!["claims"]!.AsArray().Add("CLM-001");
        });
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "claims");
        MutateContent(json =>
        {
            json["pages"]![0]!["claims"]!.AsArray().Clear();
            json["pages"]![0]!["related"]!.AsArray().Add("missing");
        });
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "broken related link missing");
        MutateContent(json =>
        {
            json["pages"]![0]!["related"]!.AsArray().RemoveAt(json["pages"]![0]!["related"]!.AsArray().Count - 1);
            json["pages"]![0]!["evidence"]![0] = "docs/missing.md";
        });
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "missing evidence");
        MutateContent(json => json["pages"]![0]!["evidence"]![0] = "docs/../../.env");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "Unapproved evidence path");
    }

    [TestMethod]
    [DataRow("fetch('https://example.org')")]
    [DataRow("localStorage.setItem('query', 'text')")]
    [DataRow("document.cookie = 'test'")]
    [DataRow("results.innerHTML = input.value")]
    public void RejectsActualAssetBoundaryMutations(string mutation)
    {
        File.AppendAllText(Path.Combine(source, "search.js"), mutation);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "search.js");
    }

    [TestMethod]
    public void EncodesUntrustedTextRatherThanAddingExecutableMarkup()
    {
        MutateContent(json => json["pages"]![0]!["sections"]![0]!["paragraphs"]![0] = "<script src=\"https://example.org\"></script>");
        Build();
        var html = File.ReadAllText(Path.Combine(output, "index.html"));
        StringAssert.Contains(html, "&lt;script");
        Assert.IsFalse(html.Contains("<script src=\"https:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RejectsTamperingExtraRoutesStaleVersionsAndRehashedBrokenSearch()
    {
        Build();
        var file = Path.Combine(output, "identity.html");
        var original = File.ReadAllText(file);
        File.AppendAllText(file, "<a href=\"/Account/Login\">Sign in</a>");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "identity.html");
        File.WriteAllText(file, original);
        File.WriteAllText(Path.Combine(output, "account.html"), "unexpected");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "inventory");
        File.Delete(Path.Combine(output, "account.html"));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, new DateOnly(2026, 11, 14))).Message, "expired");

        var indexPath = Path.Combine(output, "search-index.js");
        File.WriteAllText(indexPath, "window.siteIndex = [];\n");
        Rehash("search-index.js");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "Search index");
    }

    [TestMethod]
    public void RejectsRehashedLinkHeaderAndVersionMutations()
    {
        Build();
        var page = Path.Combine(output, "index.html");
        File.WriteAllText(page, File.ReadAllText(page).Replace("identity.html", "missing.html", StringComparison.Ordinal));
        Rehash("index.html");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "Generated page");
        Directory.Delete(output, true);
        Build();
        File.WriteAllText(Path.Combine(output, "headers.json"), "{}");
        Rehash("headers.json");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "headers.json");
        Directory.Delete(output, true);
        Build();
        var manifestPath = Path.Combine(output, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["version"] = "local-v2";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "version/locale");
    }

    [TestMethod]
    public void RejectsRehashedPublicationRobotsMutation()
    {
        Build();
        File.WriteAllText(Path.Combine(output, "robots.txt"), "User-agent: *\nAllow: /\n");
        Rehash("robots.txt");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => SiteArtifact.Verify(output, ReviewDate)).Message, "robots.txt");
    }

    [TestMethod]
    public void NeverOverwritesExistingOutput()
    {
        Build();
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(Build).Message, "never deleted");
    }

    [TestMethod]
    public void GeneratorAndTestsRemainIndependentOfAppAndDataPlanes()
    {
        var generator = System.Xml.Linq.XDocument.Load(
            Path.Combine(root, "src", "Andreja.PublicSite", "Andreja.PublicSite.csproj"));
        Assert.IsFalse(generator.Descendants("ProjectReference").Any(), "Static generator must not reference the application.");
        Assert.IsFalse(generator.Descendants("PackageReference").Any(), "Generator must remain framework-only.");
        var tests = System.Xml.Linq.XDocument.Load(
            Path.Combine(root, "tests", "Andreja.PublicSiteTests", "Andreja.PublicSiteTests.csproj"));
        var references = tests.Descendants("ProjectReference").Select(reference => reference.Attribute("Include")!.Value).ToArray();
        Assert.HasCount(1, references);
        Assert.AreEqual(@"..\..\src\Andreja.PublicSite\Andreja.PublicSite.csproj", references[0]);
        Build();
        foreach (var html in Directory.GetFiles(output, "*.html"))
        {
            var text = File.ReadAllText(html);
            Assert.IsFalse(text.Contains("<form", StringComparison.OrdinalIgnoreCase), html);
            Assert.IsFalse(text.Contains("href=\"/Account", StringComparison.OrdinalIgnoreCase), html);
            Assert.IsFalse(text.Contains("src=\"http", StringComparison.OrdinalIgnoreCase), html);
            Assert.IsFalse(text.Contains("href=\"http", StringComparison.OrdinalIgnoreCase), html);
        }
    }

    private void Build() => SiteArtifact.Build(source, config, output, root, ReviewDate);

    private void MutateContent(Action<JsonNode> mutation)
    {
        var path = Path.Combine(source, "site.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        mutation(json);
        File.WriteAllText(path, json.ToJsonString());
    }

    private void Rehash(string name)
    {
        var path = Path.Combine(output, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        manifest["files"]![name] = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(output, name))));
        File.WriteAllText(path, manifest.ToJsonString());
    }
}
