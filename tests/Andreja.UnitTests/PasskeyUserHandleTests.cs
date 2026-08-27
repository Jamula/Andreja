using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

public sealed class PasskeyUserHandleTests
{
    [Fact]
    public async Task BuiltInAssertionResolvesExactReservedBootstrapUserHandle()
    {
        using var key = ECDsa.Create();
        key.KeySize = 256;
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var reservedUserId = Guid.CreateVersion7();
        var user = new AspNetIdentityUser
        {
            Id = reservedUserId,
            AppUserId = AppUserId.New(),
            UserName = "owner",
        };
        var passkey = new UserPasskeyInfo(
            credentialId,
            EncodeCosePublicKey(key),
            DateTimeOffset.UtcNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [1],
            clientDataJson: [2]);
        var store = new PasskeyUserStore(user, passkey);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUserStore<AspNetIdentityUser>>(store);
        services.AddIdentityCore<AspNetIdentityUser>();
        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<UserManager<AspNetIdentityUser>>();
        var handler = new PasskeyHandler<AspNetIdentityUser>(
            manager,
            Options.Create(new IdentityPasskeyOptions
            {
                ServerDomain = "localhost",
                ValidateOrigin = context => ValueTask.FromResult(
                    !context.CrossOrigin
                    && context.Origin == "https://localhost"),
            }));
        var context = CreateHttpsContext(provider);

        var successful = await PerformAssertionAsync(
            handler,
            context,
            key,
            credentialId,
            reservedUserId.ToString("D"));
        var mismatched = await PerformAssertionAsync(
            handler,
            context,
            key,
            credentialId,
            Guid.CreateVersion7().ToString("D"));

        Assert.True(successful.Succeeded, successful.Failure?.Message);
        Assert.Same(user, successful.User);
        Assert.False(mismatched.Succeeded);
    }

    private static async Task<PasskeyAssertionResult<AspNetIdentityUser>>
        PerformAssertionAsync(
            PasskeyHandler<AspNetIdentityUser> handler,
            HttpContext context,
            ECDsa key,
            byte[] credentialId,
            string userHandle)
    {
        var options = await handler.MakeRequestOptionsAsync(null, context);
        using var optionsJson = JsonDocument.Parse(options.RequestOptionsJson);
        var challenge = optionsJson.RootElement.GetProperty("challenge").GetString()
            ?? throw new InvalidOperationException("The challenge is required.");
        var clientData = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "webauthn.get",
            challenge,
            origin = "https://localhost",
            crossOrigin = false,
        });
        var authenticatorData = CreateAuthenticatorData("localhost");
        var signedData = new byte[authenticatorData.Length + 32];
        authenticatorData.CopyTo(signedData, 0);
        SHA256.HashData(clientData).CopyTo(signedData, authenticatorData.Length);
        var signature = key.SignData(
            signedData,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        var credentialJson = JsonSerializer.Serialize(new
        {
            id = WebEncoders.Base64UrlEncode(credentialId),
            rawId = WebEncoders.Base64UrlEncode(credentialId),
            type = "public-key",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = WebEncoders.Base64UrlEncode(clientData),
                authenticatorData = WebEncoders.Base64UrlEncode(authenticatorData),
                signature = WebEncoders.Base64UrlEncode(signature),
                userHandle = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(userHandle)),
            },
        });

        return await handler.PerformAssertionAsync(new()
        {
            HttpContext = context,
            CredentialJson = credentialJson,
            AssertionState = options.AssertionState,
        });
    }

    private static byte[] EncodeCosePublicKey(ECDsa key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[] CreateAuthenticatorData(string relyingPartyId)
    {
        var result = new byte[37];
        SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)).CopyTo(result, 0);
        result[32] = 0x05;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(33), 1);
        return result;
    }

    private static DefaultHttpContext CreateHttpsContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Headers.Origin = "https://localhost";
        return context;
    }

    private sealed class PasskeyUserStore(
        AspNetIdentityUser user,
        UserPasskeyInfo passkey)
        : IUserStore<AspNetIdentityUser>, IUserPasskeyStore<AspNetIdentityUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidate.Id.ToString("D"));

        public Task<string?> GetUserNameAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidate.UserName);

        public Task SetUserNameAsync(
            AspNetIdentityUser candidate,
            string? userName,
            CancellationToken cancellationToken)
        {
            candidate.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidate.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(
            AspNetIdentityUser candidate,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            candidate.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<AspNetIdentityUser?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                string.Equals(user.Id.ToString("D"), userId, StringComparison.Ordinal)
                    ? user
                    : null);

        public Task<AspNetIdentityUser?> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken) =>
            Task.FromResult<AspNetIdentityUser?>(null);

        public Task AddOrUpdatePasskeyAsync(
            AspNetIdentityUser candidate,
            UserPasskeyInfo updatedPasskey,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
            AspNetIdentityUser candidate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IList<UserPasskeyInfo>>([passkey]);

        public Task<AspNetIdentityUser?> FindByPasskeyIdAsync(
            byte[] credentialId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                credentialId.AsSpan().SequenceEqual(passkey.CredentialId)
                    ? user
                    : null);

        public Task<UserPasskeyInfo?> FindPasskeyAsync(
            AspNetIdentityUser candidate,
            byte[] credentialId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                candidate.Id == user.Id
                && credentialId.AsSpan().SequenceEqual(passkey.CredentialId)
                    ? passkey
                    : null);

        public Task RemovePasskeyAsync(
            AspNetIdentityUser candidate,
            byte[] credentialId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
