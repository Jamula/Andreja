using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public sealed class BootstrapTokenVerifier(IOptions<LocalIdentityOptions> options)
    : IBootstrapTokenVerifier
{
    private const int MaximumTokenFileBytes = 4096;

    public async ValueTask<bool> VerifyAsync(
        string suppliedToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suppliedToken);
        cancellationToken.ThrowIfCancellationRequested();

        var configured = options.Value;
        EnsureTokenFileIsReadOnly(configured.BootstrapTokenFile);
        using var encodedExpected = await ReadTokenFileAsync(
            configured.BootstrapTokenFile,
            cancellationToken).ConfigureAwait(false);
        using var expected = SensitiveBuffer.Rent(configured.BootstrapTokenBytes);
        using var supplied = SensitiveBuffer.Rent(configured.BootstrapTokenBytes);

        var expectedValid = TryDecodeExpected(
            encodedExpected.Span,
            expected.Span,
            out var expectedWritten);
        var suppliedValid = Convert.TryFromBase64Chars(
            suppliedToken.AsSpan().Trim(),
            supplied.Span,
            out var suppliedWritten);
        var tokensMatch = CryptographicOperations.FixedTimeEquals(
            expected.Span,
            supplied.Span);

        // Both decoders and the fixed-time comparison run before validation is combined.
        return expectedValid
            && suppliedValid
            && expectedWritten == configured.BootstrapTokenBytes
            && suppliedWritten == configured.BootstrapTokenBytes
            && tokensMatch;
    }

    private static void EnsureTokenFileIsReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            {
                throw new InvalidDataException(
                    "The bootstrap token file must be mounted read-only.");
            }

            return;
        }

        var mode = File.GetUnixFileMode(path);
        if (mode != UnixFileMode.UserRead)
        {
            throw new InvalidDataException(
                "The bootstrap token file must be readable only by its owner.");
        }
    }

    private static async ValueTask<SensitiveBuffer> ReadTokenFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var buffer = SensitiveBuffer.Rent(MaximumTokenFileBytes + 1);
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
            await using (stream.ConfigureAwait(false))
            {
                var totalRead = 0;
                while (totalRead < buffer.Capacity)
                {
                    var read = await stream.ReadAsync(
                        buffer.CapacityMemory[totalRead..],
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead > MaximumTokenFileBytes)
                {
                    throw new InvalidDataException(
                        "The bootstrap token file exceeds the allowed size.");
                }

                buffer.SetLength(totalRead);
                return buffer;
            }
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static bool TryDecodeExpected(
        Span<byte> encoded,
        Span<byte> decoded,
        out int bytesWritten)
    {
        var compactedLength = RemoveAsciiWhitespace(encoded);
        var status = Base64.DecodeFromUtf8(
            encoded[..compactedLength],
            decoded,
            out var bytesConsumed,
            out bytesWritten,
            isFinalBlock: true);

        return status == OperationStatus.Done && bytesConsumed == compactedLength;
    }

    private static int RemoveAsciiWhitespace(Span<byte> value)
    {
        var writeIndex = 0;
        foreach (var current in value)
        {
            if (current is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                value[writeIndex++] = current;
            }
        }

        value[writeIndex..].Clear();
        return writeIndex;
    }
}
