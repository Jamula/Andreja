using System.Buffers;
using System.Security.Cryptography;

namespace Andreja.Adapters.Identity.AspNetCore;

internal sealed class SensitiveBuffer(
    byte[] buffer,
    int length,
    bool returnToPool) : IDisposable
{
    private byte[]? backingBuffer = ValidateBuffer(buffer, length);

    public int Capacity => GetBuffer().Length;

    public Memory<byte> CapacityMemory => GetBuffer();

    public Span<byte> Span => GetBuffer().AsSpan(0, Length);

    public int Length { get; private set; } = length;

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
        var toClear = Interlocked.Exchange(ref backingBuffer, null);
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

    private static byte[] ValidateBuffer(byte[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)buffer.Length);
        return buffer;
    }

    private byte[] GetBuffer() =>
        backingBuffer ?? throw new ObjectDisposedException(nameof(SensitiveBuffer));
}
