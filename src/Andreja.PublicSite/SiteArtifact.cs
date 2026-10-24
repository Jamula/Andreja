using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Andreja.PublicSite;

public sealed record SiteConfig(string Mode, string BaseUrl, string HeaderProfile);
public sealed record SiteContent(string Version, string Locale, SitePage[] Pages);
public sealed record SitePage(
    string Slug, string Title, string Owner, DateOnly Reviewed, DateOnly Expires,
    string[] Evidence, string[] Claims, SiteSection[] Sections, string[] Related);
public sealed record SiteSection(string Heading, string[] Paragraphs);
public sealed record SearchEntry(string Url, string Title, string Text);
public sealed record SiteManifest(
    string Format, string Version, string Locale, string Mode, string BaseUrl, DateOnly AsOf,
    string Generator, string Dependencies, string SecurityTxt,
    SortedDictionary<string, string> Inputs, SortedDictionary<string, string> Files);

public static partial class SiteArtifact
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public const string ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'none'; img-src 'none'; font-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static IReadOnlyDictionary<string, string> SecurityHeaders { get; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Cache-Control"] = "no-store",
            ["Content-Security-Policy"] = ContentSecurityPolicy,
            ["Cross-Origin-Opener-Policy"] = "same-origin",
            ["Cross-Origin-Resource-Policy"] = "same-origin",
            ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()",
            ["Referrer-Policy"] = "no-referrer",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["X-Robots-Tag"] = "noindex, nofollow, noarchive",
        };

    public static void Build(string source, string configFile, string output, string repository, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(source);
        var config = Read<SiteConfig>(configFile);
        ValidateConfig(config);
        var contentPath = Path.Join(source, "site.json");
        var content = Read<SiteContent>(contentPath);
        ValidateContent(content, repository, asOf);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new InvalidDataException("Output must be a new or empty dedicated directory; existing files are never deleted.");
        }

        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["content/site.json"] = Hash(File.ReadAllBytes(contentPath)),
            ["config"] = Hash(File.ReadAllBytes(configFile)),
        };
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var asset in new[] { "site.css", "search.js" })
        {
            var bytes = File.ReadAllBytes(Path.Join(source, asset));
            ValidateAsset(asset, Encoding.UTF8.GetString(bytes));
            inputs[$"content/{asset}"] = Hash(bytes);
            files[asset] = bytes;
        }

        foreach (var page in content.Pages)
        {
            foreach (var evidence in page.Evidence)
            {
                inputs[evidence] = Hash(File.ReadAllBytes(ResolveEvidence(repository, evidence)));
            }

            files[$"{page.Slug}.html"] = Utf8(Render(content, page));
        }

        var search = content.Pages.Select(page => new SearchEntry(
            $"{page.Slug}.html", page.Title,
            string.Join(" ", page.Sections.Select(section =>
                $"{section.Heading} {string.Join(" ", section.Paragraphs)}")))).ToArray();
        files["search-index.js"] = Utf8($"window.siteIndex = {JsonSerializer.Serialize(search, JsonOptions)};\n");
        files["robots.txt"] = Utf8("User-agent: *\nDisallow: /\n");
        files["headers.json"] = Utf8(JsonSerializer.Serialize(SecurityHeaders, JsonOptions) + "\n");
        files["site-metadata.json"] = Utf8(JsonSerializer.Serialize(content, JsonOptions) + "\n");
        files["UNPUBLISHED.txt"] = Utf8("Local review only. Do not publish or bind beyond loopback. No launch approval is represented.\nsecurity.txt is intentionally absent: no approved monitored contact or domain. No RFC 9116 compliance is claimed.\n");

        var generatorRoot = Path.GetFullPath(Path.Join(repository, "src", "Andreja.PublicSite"));
        foreach (var name in new[] { "Andreja.PublicSite.csproj", "Program.cs", "SiteArtifact.cs" })
        {
            inputs[$"src/Andreja.PublicSite/{name}"] = Hash(File.ReadAllBytes(Path.Join(generatorRoot, name)));
        }

        foreach (var name in new[] { "global.json", "Directory.Build.props", "Directory.Packages.props" })
        {
            inputs[name] = Hash(File.ReadAllBytes(Path.Join(repository, name)));
        }

        var manifest = new SiteManifest(
            "andreja.local-site.v1", content.Version, content.Locale, config.Mode, config.BaseUrl, asOf,
            "Andreja.PublicSite .NET 10; source hashes in inputs",
            "Framework: Microsoft.NETCore.App net10.0; no NuGet, npm, remote assets, or app project references",
            "disabled-no-approved-contact",
            inputs, new SortedDictionary<string, string>(files.ToDictionary(pair => pair.Key, pair => Hash(pair.Value)), StringComparer.Ordinal));
        files["manifest.json"] = Utf8(JsonSerializer.Serialize(manifest, JsonOptions) + "\n");
        Directory.CreateDirectory(output);
        foreach (var (name, bytes) in files)
        {
            File.WriteAllBytes(Path.Join(output, name), bytes);
        }

        Verify(output, asOf);
    }

    public static void ValidateConfig(SiteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Mode != "local-unpublished" || config.HeaderProfile != "local-strict-v1")
        {
            throw new InvalidDataException("mode/headerProfile: only local-unpublished/local-strict-v1 is authorized. No publication activation switch exists.");
        }

        if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != "http" || uri.Host != "127.0.0.1"
            || uri.Port is < 1024 or > 65535 || uri.UserInfo.Length != 0
            || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || config.BaseUrl != $"http://127.0.0.1:{uri.Port.ToString(CultureInfo.InvariantCulture)}/")
        {
            throw new InvalidDataException("baseUrl: require canonical http://127.0.0.1:<1024-65535>/ with no credentials, path, query or fragment.");
        }
    }

    public static void Verify(string output, DateOnly? asOf = null)
    {
        var manifest = Read<SiteManifest>(Path.Join(output, "manifest.json"));
        if (manifest.Format != "andreja.local-site.v1" || manifest.SecurityTxt != "disabled-no-approved-contact")
        {
            throw new InvalidDataException("manifest format/security.txt boundary is invalid.");
        }

        ValidateConfig(new SiteConfig(manifest.Mode, manifest.BaseUrl, "local-strict-v1"));
        var actual = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(output, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
        var expected = manifest.Files.Keys.Append("manifest.json").Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Artifact inventory differs from manifest (unexpected/missing files).");
        }

        foreach (var (name, hash) in manifest.Files)
        {
            if (!SafeArtifactName().IsMatch(name) || Hash(File.ReadAllBytes(Path.Join(output, name))) != hash)
            {
                throw new InvalidDataException($"Artifact hash/path mismatch: {name}");
            }
        }

        var content = Read<SiteContent>(Path.Join(output, "site-metadata.json"));
        ValidateContent(content, null, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow));
        if (manifest.Version != content.Version || manifest.Locale != content.Locale)
        {
            throw new InvalidDataException("manifest version/locale differs from site metadata.");
        }

        var expectedFiles = content.Pages.Select(page => $"{page.Slug}.html").Concat(
            ["site.css", "search.js", "search-index.js", "robots.txt", "headers.json", "site-metadata.json", "UNPUBLISHED.txt"])
            .Order(StringComparer.Ordinal);
        if (!manifest.Files.Keys.Order(StringComparer.Ordinal).SequenceEqual(expectedFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Artifact boundary contains unapproved routes or files.");
        }

        var headers = Read<SortedDictionary<string, string>>(Path.Join(output, "headers.json"));
        if (JsonSerializer.Serialize(headers, JsonOptions) != JsonSerializer.Serialize(SecurityHeaders, JsonOptions))
        {
            throw new InvalidDataException("headers.json differs from strict security header profile.");
        }

        if (File.ReadAllText(Path.Join(output, "robots.txt")) != "User-agent: *\nDisallow: /\n")
        {
            throw new InvalidDataException("robots.txt must disallow indexing of the unpublished artifact.");
        }

        foreach (var page in content.Pages)
        {
            var html = File.ReadAllText(Path.Join(output, $"{page.Slug}.html"));
            if (html != Render(content, page))
            {
                throw new InvalidDataException($"Generated page differs from metadata/boundary: {page.Slug}");
            }

            foreach (var target in LocalLink().Matches(html).Select(match => match.Groups[1].Value))
            {
                if (target.StartsWith('#'))
                {
                    if (!html.Contains($"id=\"{target[1..]}\"", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Broken fragment on {page.Slug}: {target}");
                    }
                }
                else if (!SafeArtifactName().IsMatch(target) || !manifest.Files.ContainsKey(target))
                {
                    throw new InvalidDataException($"Broken/nonlocal link on {page.Slug}: {target}");
                }
            }
        }

        var expectedIndex = content.Pages.Select(page => new SearchEntry($"{page.Slug}.html", page.Title,
            string.Join(" ", page.Sections.Select(section => $"{section.Heading} {string.Join(" ", section.Paragraphs)}")))).ToArray();
        if (File.ReadAllText(Path.Join(output, "search-index.js"))
            != $"window.siteIndex = {JsonSerializer.Serialize(expectedIndex, JsonOptions)};\n")
        {
            throw new InvalidDataException("Search index differs from versioned page content.");
        }

        foreach (var asset in new[] { "site.css", "search.js" })
        {
            ValidateAsset(asset, File.ReadAllText(Path.Join(output, asset)));
        }
    }

    private static void ValidateContent(SiteContent content, string? repository, DateOnly asOf)
    {
        if (!VersionPattern().IsMatch(content.Version) || content.Locale != "en" || content.Pages.Length == 0)
        {
            throw new InvalidDataException("version/locale: require local-v<number>, en, and nonempty pages.");
        }

        var slugs = content.Pages.Select(page => page.Slug).ToHashSet(StringComparer.Ordinal);
        foreach (var page in content.Pages)
        {
            if (!SlugPattern().IsMatch(page.Slug))
            {
                throw new InvalidDataException($"Page {page.Slug}: invalid local chapter slug.");
            }
        }

        if (slugs.Count != content.Pages.Length || !slugs.Contains("index"))
        {
            throw new InvalidDataException("pages: duplicate slugs or missing index.");
        }

        foreach (var page in content.Pages)
        {
            if (!SlugPattern().IsMatch(page.Slug) || string.IsNullOrWhiteSpace(page.Title)
                || string.IsNullOrWhiteSpace(page.Owner) || page.Claims.Length != 0
                || page.Reviewed > asOf || page.Expires <= asOf || page.Expires <= page.Reviewed
                || page.Expires.DayNumber - page.Reviewed.DayNumber > 90
                || page.Evidence.Length == 0 || page.Sections.Length == 0
                || page.Sections.Any(section => string.IsNullOrWhiteSpace(section.Heading)
                    || section.Paragraphs.Length == 0 || section.Paragraphs.Any(string.IsNullOrWhiteSpace)))
            {
                throw new InvalidDataException($"Page {page.Slug}: invalid owner/content/claims/version dates (expired or over 90 days). Public claims are not supported.");
            }

            foreach (var related in page.Related)
            {
                if (!slugs.Contains(related))
                {
                    throw new InvalidDataException($"Page {page.Slug}: broken related link {related}.");
                }
            }

            foreach (var evidence in page.Evidence)
            {
                if (repository is not null && !File.Exists(ResolveEvidence(repository, evidence)))
                {
                    throw new InvalidDataException($"Page {page.Slug}: missing evidence {evidence}.");
                }
            }
        }
    }

    private static string ResolveEvidence(string repository, string evidence)
    {
        if (!EvidencePattern().IsMatch(evidence) || evidence.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unapproved evidence path: {evidence}");
        }

        return Path.Join(repository, evidence.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ValidateAsset(string asset, string text)
    {
        if (ForbiddenAsset().IsMatch(text))
        {
            throw new InvalidDataException($"{asset}: remote/network/storage/dynamic-code boundary violation.");
        }
    }

    private static string Render(SiteContent content, SitePage page)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<meta name=\"robots\" content=\"noindex, nofollow, noarchive\"><meta name=\"referrer\" content=\"no-referrer\">");
        // frame-ancestors is enforced by the HTTP adapter, not supported in a meta policy.
        html.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"")
            .Append(E(ContentSecurityPolicy.Replace("; frame-ancestors 'none'", "", StringComparison.Ordinal))).Append("\">");
        html.Append("<title>").Append(E(page.Title)).Append(" | Andreja local field guide</title><link rel=\"stylesheet\" href=\"site.css\">");
        html.Append("<script defer src=\"search-index.js\"></script><script defer src=\"search.js\"></script></head><body>");
        html.Append("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a><header><p class=\"eyebrow\">Andreja · working name · private local field guide</p>");
        html.Append("<p class=\"notice\">Unpublished planning and repository guidance. No product availability, public launch, or conformance is represented.</p>");
        html.Append("<nav aria-label=\"Guide chapters\"><ul>");
        foreach (var item in content.Pages)
        {
            html.Append("<li><a href=\"").Append(E(item.Slug)).Append(".html\"");
            if (item.Slug == page.Slug)
            {
                html.Append(" aria-current=\"page\"");
            }

            html.Append('>').Append(E(item.Title)).Append("</a></li>");
        }

        html.Append("</ul></nav></header><main id=\"main-content\" tabindex=\"-1\" aria-labelledby=\"page-title\"><h1 id=\"page-title\">")
            .Append(E(page.Title)).Append("</h1><p class=\"metadata\">")
            .Append(E(content.Version)).Append(" · ").Append(E(content.Locale)).Append(" · Owner: ").Append(E(page.Owner))
            .Append(" · Reviewed: ").Append(page.Reviewed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append(" · Revalidate before: ").Append(page.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</p>");
        html.Append("<section aria-labelledby=\"search-heading\"><h2 id=\"search-heading\">Find local guidance</h2><label for=\"site-search\">Search this version (stays in browser memory)</label>");
        html.Append("<input id=\"site-search\" type=\"search\" maxlength=\"200\" autocomplete=\"off\" aria-describedby=\"search-help\" aria-controls=\"search-results\"><p id=\"search-help\">Type words, then Tab to matching chapter links. Escape clears the search. No queries are sent or saved.</p>");
        html.Append("<p id=\"search-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\">Search all chapters, or use the chapter navigation.</p><ul id=\"search-results\"></ul><noscript>Search needs JavaScript. All guidance remains available through the chapter navigation.</noscript></section>");
        foreach (var section in page.Sections)
        {
            html.Append("<section><h2>").Append(E(section.Heading)).Append("</h2>");
            foreach (var paragraph in section.Paragraphs)
            {
                html.Append("<p>").Append(E(paragraph)).Append("</p>");
            }

            html.Append("</section>");
        }

        html.Append("<section><h2>Evidence and limits</h2><p>Repository source references for local review; not public claim approvals:</p><ul>");
        foreach (var evidence in page.Evidence)
        {
            html.Append("<li><code>").Append(E(evidence)).Append("</code></li>");
        }

        html.Append("</ul></section><nav aria-label=\"Related guidance\"><ul>");
        foreach (var related in page.Related)
        {
            html.Append("<li><a href=\"").Append(E(related)).Append(".html\">")
                .Append(E(content.Pages.Single(item => item.Slug == related).Title)).Append("</a></li>");
        }

        html.Append("</ul></nav></main><footer><p>Local review only. Analytics, remote search, feedback submission, sign-in and product data are absent. Hosting, monitored security contact, legal wording and launch approval remain separate gates.</p></footer></body></html>\n");
        return html.ToString();
    }

    private static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Empty JSON: {Path.GetFileName(path)}");

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    [GeneratedRegex(@"^local-v[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
    [GeneratedRegex(@"^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
    [GeneratedRegex(@"^[a-z][a-z0-9.-]*\.(html|css|js|json|txt)$|^UNPUBLISHED\.txt$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeArtifactName();
    [GeneratedRegex(@"^(docs|src)/[a-zA-Z0-9_./-]+\.(md|cs|razor)$", RegexOptions.CultureInvariant)]
    private static partial Regex EvidencePattern();
    [GeneratedRegex("(?:href|src)=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LocalLink();
    [GeneratedRegex(@"https?:|//|@import|url\s*\(|\b(fetch|XMLHttpRequest|WebSocket|EventSource|sendBeacon|localStorage|sessionStorage|indexedDB|serviceWorker|cookie|eval|Function|importScripts)\b|import\s*\(|innerHTML|outerHTML|document\.write", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenAsset();
}
