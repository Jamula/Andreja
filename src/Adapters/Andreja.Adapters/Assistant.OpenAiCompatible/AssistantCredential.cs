using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public sealed class AssistantCredential : IDisposable
{
    private char[]? value;

    public AssistantCredential(ReadOnlySpan<char> value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value.Length);
        this.value = value.ToArray();
    }

    public ReadOnlySpan<char> Value =>
        value ?? throw new ObjectDisposedException(nameof(AssistantCredential));

    public void Dispose()
    {
        var toClear = Interlocked.Exchange(ref value, null);
        if (toClear is not null)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(toClear.AsSpan()));
        }
    }
}
