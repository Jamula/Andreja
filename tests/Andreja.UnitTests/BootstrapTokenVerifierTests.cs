using System.Security.Cryptography;
using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

public sealed class BootstrapTokenVerifierTests
{
    [Fact]
    public async Task ValidTokenMatchesWithFileWhitespace()
    {
        var token = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(token);
        var path = await WriteTokenFileAsync($" \r\n{encoded}\n\t");
        try
        {
            var verifier = CreateVerifier(path);

            var verified = await verifier.VerifyAsync($"\t{encoded}\r\n");

            Assert.True(verified);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidTokenReturnsFalse()
    {
        var expected = RandomNumberGenerator.GetBytes(32);
        var supplied = RandomNumberGenerator.GetBytes(32);
        var path = await WriteTokenFileAsync(Convert.ToBase64String(expected));
        try
        {
            var verifier = CreateVerifier(path);

            Assert.False(await verifier.VerifyAsync(Convert.ToBase64String(supplied)));
            Assert.False(await verifier.VerifyAsync("not-a-base64-secret"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(supplied);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MalformedFileDoesNotExposeContents()
    {
        const string secret = "not-base64-private-bootstrap-secret";
        var path = await WriteTokenFileAsync(secret);
        try
        {
            var verifier = CreateVerifier(path);

            var result = await verifier.VerifyAsync(Convert.ToBase64String(new byte[32]));

            Assert.False(result);
        }
        catch (Exception exception)
        {
            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
            throw;
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OversizedFileExceptionDoesNotExposeContents()
    {
        var secret = new string('s', 4097);
        var path = await WriteTokenFileAsync(secret);
        try
        {
            var verifier = CreateVerifier(path);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await verifier.VerifyAsync(Convert.ToBase64String(new byte[32])));

            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret[..64], exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PreCanceledVerificationDoesNotReadTokenFile()
    {
        var missingPath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.token");
        var verifier = CreateVerifier(missingPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await verifier.VerifyAsync(
                Convert.ToBase64String(new byte[32]),
                cancellation.Token));
    }

    [Fact]
    public void SensitiveBufferZeroesOwnedBytesOnDisposal()
    {
        var bytes = Enumerable.Repeat((byte)0xA5, 64).ToArray();
        var buffer = new SensitiveBuffer(bytes, bytes.Length, returnToPool: false);

        buffer.Dispose();

        Assert.All(bytes, value => Assert.Equal(0, value));
        Assert.Throws<ObjectDisposedException>(() => _ = buffer.Span);
    }

    private static BootstrapTokenVerifier CreateVerifier(string path) =>
        new(
            Options.Create(
                new LocalIdentityOptions
                {
                    BootstrapTokenFile = path,
                    BootstrapTokenBytes = 32,
                }));

    private static async Task<string> WriteTokenFileAsync(string contents)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.token");
        await File.WriteAllTextAsync(path, contents);
        return path;
    }
}
