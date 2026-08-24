using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public interface IBootstrapTokenVerifier
{
    ValueTask<bool> VerifyAsync(string suppliedToken, CancellationToken cancellationToken = default);
}

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
        using var encodedExpected = await ReadTokenFileAsync(
            configured.BootstrapTokenFile,
            cancellationToken);
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

        return expectedValid
            & suppliedValid
            & (expectedWritten == configured.BootstrapTokenBytes)
            & (suppliedWritten == configured.BootstrapTokenBytes)
            & tokensMatch;
    }

    private static async ValueTask<SensitiveBuffer> ReadTokenFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var buffer = SensitiveBuffer.Rent(MaximumTokenFileBytes + 1);
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var totalRead = 0;
            while (totalRead < buffer.Capacity)
            {
                var read = await stream.ReadAsync(
                    buffer.CapacityMemory[totalRead..],
                    cancellationToken);
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

internal sealed class SensitiveBuffer : IDisposable
{
    private byte[]? buffer;
    private readonly bool returnToPool;

    internal SensitiveBuffer(byte[] buffer, int length, bool returnToPool)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)buffer.Length);
        this.buffer = buffer;
        this.returnToPool = returnToPool;
        Length = length;
    }

    public int Capacity => GetBuffer().Length;

    public Memory<byte> CapacityMemory => GetBuffer();

    public Span<byte> Span => GetBuffer().AsSpan(0, Length);

    public int Length { get; private set; }

    public static SensitiveBuffer Rent(int capacity)
    {
        var rented = ArrayPool<byte>.Shared.Rent(capacity);
        CryptographicOperations.ZeroMemory(rented);
        return new SensitiveBuffer(rented, capacity, returnToPool: true);
    }

    public void SetLength(int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)Capacity);
        Length = length;
    }

    public void Dispose()
    {
        var toClear = Interlocked.Exchange(ref buffer, null);
        if (toClear is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(toClear);
        Length = 0;
        if (returnToPool)
        {
            ArrayPool<byte>.Shared.Return(toClear);
        }
    }

    private byte[] GetBuffer() =>
        buffer ?? throw new ObjectDisposedException(nameof(SensitiveBuffer));
}
