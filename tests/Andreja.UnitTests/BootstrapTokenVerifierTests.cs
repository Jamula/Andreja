using System.Security.Cryptography;
using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

[TestClass]
public sealed class BootstrapTokenVerifierTests
{
    [TestMethod]
    public async Task ValidTokenMatchesWithFileWhitespace()
    {
        var token = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(token);
        var path = await WriteTokenFileAsync($" \r\n{encoded}\n\t");
        try
        {
            var verifier = CreateVerifier(path);

            var verified = await verifier.VerifyAsync($"\t{encoded}\r\n");

            Assert.IsTrue(verified);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
            DeleteTokenFile(path);
        }
    }

    [TestMethod]
    public async Task InvalidTokenReturnsFalse()
    {
        var expected = RandomNumberGenerator.GetBytes(32);
        var supplied = RandomNumberGenerator.GetBytes(32);
        var path = await WriteTokenFileAsync(Convert.ToBase64String(expected));
        try
        {
            var verifier = CreateVerifier(path);

            Assert.IsFalse(await verifier.VerifyAsync(Convert.ToBase64String(supplied)));
            Assert.IsFalse(await verifier.VerifyAsync("not-a-base64-secret"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(supplied);
            DeleteTokenFile(path);
        }
    }

    [TestMethod]
    public async Task MalformedFileDoesNotExposeContents()
    {
        const string secret = "not-base64-private-bootstrap-secret";
        var path = await WriteTokenFileAsync(secret);
        try
        {
            var verifier = CreateVerifier(path);

            var result = await verifier.VerifyAsync(Convert.ToBase64String(new byte[32]));

            Assert.IsFalse(result);
        }
        catch (Exception exception)
        {
            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
            throw;
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [TestMethod]
    public async Task OversizedFileExceptionDoesNotExposeContents()
    {
        var secret = new string('s', 4097);
        var path = await WriteTokenFileAsync(secret);
        try
        {
            var verifier = CreateVerifier(path);

            var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
                async () => await verifier.VerifyAsync(Convert.ToBase64String(new byte[32])));

            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret[..64], exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTokenFile(path);
        }
    }

    [TestMethod]
    public async Task PreCanceledVerificationDoesNotReadTokenFile()
    {
        var missingPath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.token");
        var verifier = CreateVerifier(missingPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await verifier.VerifyAsync(
                Convert.ToBase64String(new byte[32]),
                cancellation.Token));
    }

    [TestMethod]
    public async Task UnixTokenFileRejectsGroupOrOtherPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var token = RandomNumberGenerator.GetBytes(32);
        var path = await WriteTokenFileAsync(Convert.ToBase64String(token));
        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.GroupRead);
            var verifier = CreateVerifier(path);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                async () => await verifier.VerifyAsync(
                    Convert.ToBase64String(token)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
            DeleteTokenFile(path);
        }
    }

    [TestMethod]
    public void SensitiveBufferZeroesOwnedBytesOnDisposal()
    {
        var bytes = Enumerable.Repeat((byte)0xA5, 64).ToArray();
        var buffer = new SensitiveBuffer(bytes, bytes.Length, returnToPool: false);

        buffer.Dispose();

        foreach (var value in bytes)
        {
            Assert.AreEqual(0, value);
        }
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = buffer.Span);
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
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }

        return path;
    }

    private static void DeleteTokenFile(string path)
    {
        if (OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        File.Delete(path);
    }
}
