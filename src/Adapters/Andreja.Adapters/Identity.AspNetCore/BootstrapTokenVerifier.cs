using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public interface IBootstrapTokenVerifier
{
    ValueTask<bool> VerifyAsync(string suppliedToken, CancellationToken cancellationToken = default);
}

public sealed class BootstrapTokenVerifier(IOptions<LocalIdentityOptions> options)
    : IBootstrapTokenVerifier
{
    public async ValueTask<bool> VerifyAsync(
        string suppliedToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suppliedToken);
        var configured = options.Value;
        var expectedText = await File.ReadAllTextAsync(
            configured.BootstrapTokenFile,
            cancellationToken);

        byte[] expected;
        byte[] supplied;
        try
        {
            expected = Convert.FromBase64String(expectedText.Trim());
            supplied = Convert.FromBase64String(suppliedToken.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            return expected.Length == configured.BootstrapTokenBytes
                && supplied.Length == configured.BootstrapTokenBytes
                && CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(supplied);
            CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(expectedText));
        }
    }
}
