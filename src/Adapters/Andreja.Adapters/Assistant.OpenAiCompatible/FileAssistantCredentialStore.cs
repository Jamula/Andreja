using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public sealed class FileAssistantCredentialStore(
    IReadOnlyDictionary<string, string> credentialFiles)
    : IAssistantCredentialStore
{
    private const int MaximumCredentialBytes = 4096;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly Dictionary<string, string> credentialFiles =
        Validate(credentialFiles);

    public async ValueTask<AssistantCredential?> ResolveAsync(
        string credentialHandle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialHandle);
        cancellationToken.ThrowIfCancellationRequested();

        if (!credentialFiles.TryGetValue(credentialHandle, out var path))
        {
            throw new InvalidDataException(
                "The assistant credential handle is not mapped to the approved secret store.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The assistant credential file is unavailable.",
                path);
        }

        EnsureReadOnly(path);
        var buffer = ArrayPool<byte>.Shared.Rent(MaximumCredentialBytes + 1);
        CryptographicOperations.ZeroMemory(buffer);
        try
        {
            var stream = File.Open(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    BufferSize = 4096,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });
            var totalRead = 0;
            await using (stream.ConfigureAwait(false))
            {
                while (totalRead < MaximumCredentialBytes + 1)
                {
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(totalRead, MaximumCredentialBytes + 1 - totalRead),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }
            }

            if (totalRead > MaximumCredentialBytes)
            {
                throw new InvalidDataException("The assistant credential file exceeds the allowed size.");
            }

            var credentialBytes = buffer.AsSpan(0, totalRead).TrimAsciiWhitespace();
            if (credentialBytes.IsEmpty)
            {
                return null;
            }

            char[]? characters = null;
            try
            {
                var characterCount = StrictUtf8.GetCharCount(credentialBytes);
                characters = ArrayPool<char>.Shared.Rent(characterCount);
                var written = StrictUtf8.GetChars(credentialBytes, characters);
                return new AssistantCredential(characters.AsSpan(0, written));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException(
                    "The assistant credential file must contain valid UTF-8.",
                    exception);
            }
            finally
            {
                if (characters is not null)
                {
                    CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
                    ArrayPool<char>.Shared.Return(characters);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static Dictionary<string, string> Validate(
        IReadOnlyDictionary<string, string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var validated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (handle, path) in candidates)
        {
            if (!Uri.TryCreate(handle, UriKind.Absolute, out var parsedHandle)
                || parsedHandle.Scheme != "credential"
                || string.IsNullOrWhiteSpace(path)
                || !Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException(
                    "Credential mappings require a credential:// handle and an absolute file path.",
                    nameof(candidates));
            }

            validated.Add(handle, path);
        }

        return validated;
    }

    private static void EnsureReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            {
                throw new InvalidDataException(
                    "The assistant credential file must be mounted read-only.");
            }

            return;
        }

        if (File.GetUnixFileMode(path) != UnixFileMode.UserRead)
        {
            throw new InvalidDataException(
                "The assistant credential file must be readable only by its owner.");
        }
    }
}
