using System.Security.Cryptography;
using System.Text;

namespace Andreja.Docs;

public static class RepositoryPaths
{
    private const int MaxSymlinkExpansions = 40;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string FindRoot(string? startDirectory = null)
    {
        var current = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Join(current.FullName, "Andreja.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidDataException("Could not locate repository root containing Andreja.slnx.");
    }

    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return relativePath.Replace('\\', '/');
    }

    public static bool IsWithinRoot(string candidatePath, string rootPath, StringComparison? comparison = null)
    {
        ArgumentNullException.ThrowIfNull(candidatePath);
        ArgumentNullException.ThrowIfNull(rootPath);

        var effectiveComparison = comparison ?? PathComparison;
        var trimmedRoot = Path.TrimEndingDirectorySeparator(rootPath);
        var trimmedCandidate = Path.TrimEndingDirectorySeparator(candidatePath);
        if (string.Equals(trimmedCandidate, trimmedRoot, effectiveComparison))
        {
            return true;
        }

        var rootPrefix = trimmedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? trimmedRoot
            : trimmedRoot + Path.DirectorySeparatorChar;
        return trimmedCandidate.StartsWith(rootPrefix, effectiveComparison);
    }

    public static bool TryResolveRealPath(
        string path,
        out string resolvedPath,
        string? disallowedOpaqueReparseRoot = null,
        Func<string, string?>? linkTargetResolver = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            resolvedPath = string.Empty;
            return false;
        }

        var qualified = MakeQualifiedWithoutCollapsingSegments(path);
        if (!TrySplitRootAndSegments(qualified, out var currentRoot, out var initialSegments))
        {
            resolvedPath = string.Empty;
            return false;
        }

        var pending = new Stack<string>(initialSegments.Length);
        for (var i = initialSegments.Length - 1; i >= 0; i--)
        {
            pending.Push(initialSegments[i]);
        }

        var resolvedSegments = new List<string>(initialSegments.Length);
        var expansions = 0;

        while (pending.Count > 0)
        {
            var segment = pending.Pop();
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (resolvedSegments.Count > 0)
                {
                    resolvedSegments.RemoveAt(resolvedSegments.Count - 1);
                }

                continue;
            }

            resolvedSegments.Add(segment);
            var candidate = CombineRootAndSegments(currentRoot, resolvedSegments);

            string? linkTarget;
            if (linkTargetResolver is not null)
            {
                linkTarget = linkTargetResolver(candidate);
            }
            else if (!TryReadFileSystemLinkTarget(candidate, disallowedOpaqueReparseRoot, out linkTarget))
            {
                resolvedPath = string.Empty;
                return false;
            }

            if (linkTarget is null)
            {
                continue;
            }

            if (++expansions > MaxSymlinkExpansions)
            {
                resolvedPath = string.Empty;
                return false;
            }

            var normalizedTarget = StripWindowsDevicePrefix(linkTarget);
            if (Path.IsPathRooted(normalizedTarget))
            {
                var qualifiedTarget = MakeQualifiedWithoutCollapsingSegments(normalizedTarget, currentRoot);
                if (!TrySplitRootAndSegments(qualifiedTarget, out var targetRoot, out var targetSegments))
                {
                    resolvedPath = string.Empty;
                    return false;
                }

                currentRoot = targetRoot;
                resolvedSegments.Clear();
                for (var i = targetSegments.Length - 1; i >= 0; i--)
                {
                    pending.Push(targetSegments[i]);
                }
            }
            else
            {
                resolvedSegments.RemoveAt(resolvedSegments.Count - 1);
                var relativeSegments = SplitRelativeSegments(normalizedTarget);
                for (var i = relativeSegments.Length - 1; i >= 0; i--)
                {
                    pending.Push(relativeSegments[i]);
                }
            }
        }

        resolvedPath = CombineRootAndSegments(currentRoot, resolvedSegments);
        return true;
    }

    public static string ResolveRepositoryPath(string repositoryRoot, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(relativePath);

        if (Path.IsPathRooted(relativePath)
            || !TryResolveRealPath(repositoryRoot, out var resolvedRoot)
            || !TryResolveRealPath(
                Path.Join(resolvedRoot, relativePath),
                out var resolvedTarget,
                disallowedOpaqueReparseRoot: resolvedRoot)
            || !IsWithinRoot(resolvedTarget, resolvedRoot))
        {
            throw new InvalidDataException($"Path escapes repository root: {NormalizeRelativePath(relativePath)}");
        }

        return resolvedTarget;
    }

    private static bool TryReadFileSystemLinkTarget(
        string candidatePath,
        string? disallowedOpaqueReparseRoot,
        out string? linkTarget)
    {
        var fileInfo = new FileInfo(candidatePath);
        if (fileInfo.LinkTarget is { } fileTarget)
        {
            linkTarget = fileTarget;
            return true;
        }

        var directoryInfo = new DirectoryInfo(candidatePath);
        if (directoryInfo.LinkTarget is { } directoryTarget)
        {
            linkTarget = directoryTarget;
            return true;
        }

        if (disallowedOpaqueReparseRoot is not null && IsWithinRoot(candidatePath, disallowedOpaqueReparseRoot))
        {
            var attributes = fileInfo.Attributes;
            if ((int)attributes != -1 && (attributes & FileAttributes.ReparsePoint) != 0)
            {
                linkTarget = null;
                return false;
            }
        }

        linkTarget = null;
        return true;
    }

    private static string MakeQualifiedWithoutCollapsingSegments(string rawPath, string? baseRoot = null)
    {
        var stripped = StripWindowsDevicePrefix(rawPath);
        if (!Path.IsPathRooted(stripped))
        {
            var currentDirectory = StripWindowsDevicePrefix(Directory.GetCurrentDirectory());
            return Path.Join(currentDirectory, stripped);
        }

        if (OperatingSystem.IsWindows() && !Path.IsPathFullyQualified(stripped))
        {
            if (stripped[0] is '\\' or '/')
            {
                var referenceRoot = Path.GetPathRoot(baseRoot ?? StripWindowsDevicePrefix(Directory.GetCurrentDirectory()))
                    ?? @"C:\";
                return Path.Join(referenceRoot, stripped.TrimStart('\\', '/'));
            }

            if (stripped.Length >= 2 && char.IsAsciiLetter(stripped[0]) && stripped[1] == ':')
            {
                var driveCurrent = StripWindowsDevicePrefix(Path.GetFullPath(stripped[..2] + "."));
                return Path.Join(driveCurrent, stripped[2..]);
            }
        }

        return stripped;
    }

    private static bool TrySplitRootAndSegments(
        string qualifiedPath,
        out string normalizedRoot,
        out string[] segments)
    {
        var rawRoot = Path.GetPathRoot(qualifiedPath);
        if (string.IsNullOrEmpty(rawRoot))
        {
            normalizedRoot = string.Empty;
            segments = [];
            return false;
        }

        var canonicalRoot = OperatingSystem.IsWindows()
            ? rawRoot.Replace('/', '\\')
            : rawRoot;
        if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            canonicalRoot += Path.DirectorySeparatorChar;
        }

        normalizedRoot = canonicalRoot;
        segments = SplitRelativeSegments(qualifiedPath[rawRoot.Length..]);
        return true;
    }

    private static string[] SplitRelativeSegments(string relativePath)
    {
        return relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CombineRootAndSegments(string normalizedRoot, List<string> segments)
    {
        return segments.Count == 0
            ? normalizedRoot
            : normalizedRoot + string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static string StripWindowsDevicePrefix(string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            return value;
        }

        if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + value[8..];
        }

        if ((value.StartsWith(@"\\?\", StringComparison.Ordinal) || value.StartsWith(@"\??\", StringComparison.Ordinal))
            && value.Length >= 6
            && char.IsAsciiLetter(value[4])
            && value[5] == ':')
        {
            return value[4..];
        }

        return value;
    }

    public static byte[] ReadGitLfBytes(string repositoryRoot, string relativePath)
    {
        var fullPath = ResolveRepositoryPath(repositoryRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidDataException($"Required file not found: {NormalizeRelativePath(relativePath)}");
        }

        return NormalizeCrLfBytes(File.ReadAllBytes(fullPath));
    }

    public static string ReadLfText(string repositoryRoot, string relativePath)
    {
        return StrictUtf8.GetString(ReadGitLfBytes(repositoryRoot, relativePath));
    }

    public static byte[] NormalizeCrLfBytes(ReadOnlySpan<byte> bytes)
    {
        var crLfCount = 0;
        for (var i = 0; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n')
            {
                crLfCount++;
                i++;
            }
        }

        if (crLfCount == 0)
        {
            return bytes.ToArray();
        }

        var normalized = new byte[bytes.Length - crLfCount];
        var writeIndex = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i < bytes.Length - 1 && bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n')
            {
                normalized[writeIndex++] = (byte)'\n';
                i++;
            }
            else
            {
                normalized[writeIndex++] = bytes[i];
            }
        }

        return normalized;
    }

    public static string NormalizeLf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains("\r\n", StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal)
            : text;
    }

    public static string[] SplitLines(string text)
    {
        var normalized = NormalizeLf(text);
        if (normalized.Length == 0)
        {
            return [];
        }

        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        return normalized.Split('\n');
    }

    public static string NormalizeWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        var inWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0)
                {
                    inWhitespace = true;
                }
            }
            else
            {
                if (inWhitespace)
                {
                    builder.Append(' ');
                    inWhitespace = false;
                }

                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public static string Sha256LfText(string text)
    {
        var normalized = NormalizeLf(text);
        return Sha256Hex(StrictUtf8.GetBytes(normalized));
    }

    public static byte[] Utf8Bytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return StrictUtf8.GetBytes(text);
    }

    public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        return StrictUtf8.GetString(bytes);
    }
}
