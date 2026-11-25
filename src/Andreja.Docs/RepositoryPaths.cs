using System.Security.Cryptography;
using System.Text;

namespace Andreja.Docs;

public static class RepositoryPaths
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

    public static string ResolveRepositoryPath(string repositoryRoot, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(relativePath);

        var rootFull = Path.GetFullPath(repositoryRoot);
        var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Join(rootFull, relativePath));
        if (!combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes repository root: {NormalizeRelativePath(relativePath)}");
        }

        return combined;
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
