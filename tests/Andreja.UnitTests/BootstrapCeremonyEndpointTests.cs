using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Andreja.Adapters.PostgreSql;
using Andreja.AppHost.Identity;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Andreja.UnitTests;

public sealed class BootstrapCeremonyEndpointTests
{
    [Fact]
    public async Task BootstrapSignOutAndDiscoverableSignInResolveReservedUser()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        var reservedUserId = options.User.Id;

        using var completed = await host.CompleteBootstrapAsync(options, token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(reservedUserId, host.Store.User?.Id.ToString("D"));

        var authenticatedToken = await host.GetAntiforgeryTokenAsync();
        using var signedOut = await host.PostAsync(
            LocalAccountEndpoints.LogoutPath,
            content: null,
            authenticatedToken);
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, await host.GetWhoAmIStatusAsync());

        var anonymousToken = await host.GetAntiforgeryTokenAsync();
        var assertion = await host.BeginSignInAsync(anonymousToken);
        using var signedIn = await host.CompleteSignInAsync(
            assertion,
            reservedUserId,
            anonymousToken);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await host.GetWhoAmIStatusAsync());
        Assert.Equal(reservedUserId, host.Handler.LastResolvedUserId);
    }

    [Fact]
    public async Task TamperedBootstrapTicketFailsBeforePersistence()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        host.Cookies.Mutate(
            BootstrapEndpointHost.BootstrapCookieName,
            value => value[..^1] + (value[^1] == 'A' ? "B" : "A"));

        using var response = await host.CompleteBootstrapAsync(options, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(host.Store.User);
    }

    [Fact]
    public async Task ExpiredBootstrapTicketFailsBeforePersistence()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        var protectedTicket = host.Cookies.Get(
            BootstrapEndpointHost.BootstrapCookieName);
        var protector = host.Application.Services
            .GetRequiredService<BootstrapCeremonyTicketProtector>();
        Assert.True(protector.TryUnprotect(protectedTicket, out var ticket));
        host.Cookies.Set(
            BootstrapEndpointHost.BootstrapCookieName,
            protector.ProtectUntil(
                ticket!,
                DateTimeOffset.UtcNow.AddSeconds(-1)));

        using var response = await host.CompleteBootstrapAsync(options, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(host.Store.User);
    }

    [Fact]
    public async Task MismatchedTicketUserEntityFailsBeforePersistence()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        var protectedTicket = host.Cookies.Get(
            BootstrapEndpointHost.BootstrapCookieName);
        var protector = host.Application.Services
            .GetRequiredService<BootstrapCeremonyTicketProtector>();
        Assert.True(protector.TryUnprotect(protectedTicket, out var ticket));
        host.Cookies.Set(
            BootstrapEndpointHost.BootstrapCookieName,
            protector.Protect(ticket! with
            {
                CredentialUserId = Guid.CreateVersion7(),
            }));

        using var response = await host.CompleteBootstrapAsync(options, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(host.Store.User);
    }

    [Fact]
    public async Task MismatchedTicketChallengeFailsBeforePersistence()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        var protectedTicket = host.Cookies.Get(
            BootstrapEndpointHost.BootstrapCookieName);
        var protector = host.Application.Services
            .GetRequiredService<BootstrapCeremonyTicketProtector>();
        Assert.True(protector.TryUnprotect(protectedTicket, out var ticket));
        host.Cookies.Set(
            BootstrapEndpointHost.BootstrapCookieName,
            protector.Protect(ticket! with
            {
                Challenge = "different-protected-challenge",
            }));

        using var response = await host.CompleteBootstrapAsync(options, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(host.Store.User);
    }

    [Fact]
    public async Task ReplayedBootstrapTicketAndAttestationStateFailClosed()
    {
        await using var host = await BootstrapEndpointHost.StartAsync();
        var token = await host.GetAntiforgeryTokenAsync();
        var options = await host.BeginBootstrapAsync(token);
        var ceremonyCookies = host.Cookies.Snapshot();

        using var first = await host.CompleteBootstrapAsync(options, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        host.Cookies.Restore(ceremonyCookies);

        using var replay = await host.CompleteBootstrapAsync(options, token);

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(1, host.Bootstrap.CompletionCount);
    }

    private sealed record CreationOptions(
        string Challenge,
        PasskeyUser User);

    private sealed record PasskeyUser(string Id, string DisplayName);

    private sealed record AssertionOptions(string Challenge);

    private sealed record AntiforgeryResponse(string Token);

    private sealed class BootstrapEndpointHost(
        WebApplication application,
        HttpClient client,
        CookieJar cookies,
        FakePasskeyStore store,
        FakePasskeyHandler handler,
        FakeBootstrapOperations bootstrap) : IAsyncDisposable
    {
        public const string BootstrapCookieName = "__Host-Andreja.Bootstrap";

        public WebApplication Application => application;

        public CookieJar Cookies { get; } = cookies;

        public FakePasskeyStore Store { get; } = store;

        public FakePasskeyHandler Handler { get; } = handler;

        public FakeBootstrapOperations Bootstrap { get; } = bootstrap;

        public static async Task<BootstrapEndpointHost> StartAsync()
        {
            var identity = new LocalIdentityOptions
            {
                RelyingPartyId = "localhost",
                AllowedOrigins = ["https://localhost"],
                BootstrapTokenFile = Path.GetFullPath("unused"),
            };
            var store = new FakePasskeyStore();
            var handler = new FakePasskeyHandler(store);
            var bootstrap = new FakeBootstrapOperations(store);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddLogging();
            builder.Services.AddDataProtection();
            builder.Services.AddAntiforgery(
                configured => configured.HeaderName =
                    LocalAccountEndpoints.AntiforgeryHeader);
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton(Options.Create(identity));
            builder.Services.AddSingleton<IUserStore<AspNetIdentityUser>>(store);
            builder.Services
                .AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddIdentityCookies();
            builder.Services
                .AddIdentityCore<AspNetIdentityUser>()
                .AddSignInManager();
            builder.Services.AddScoped<IPasskeyHandler<AspNetIdentityUser>>(
                _ => handler);
            builder.Services.AddSingleton<ILocalIdentityBootstrapOperations>(
                bootstrap);
            builder.Services.ConfigureAndrejaCookieBehavior();

            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.UseAntiforgery();
            application.MapGet(
                    "/antiforgery",
                    (IAntiforgery antiforgery, HttpContext context) =>
                        Results.Ok(new AntiforgeryResponse(
                            antiforgery.GetAndStoreTokens(context).RequestToken!)))
                .AllowAnonymous();
            application.MapGet(
                    "/whoami",
                    (HttpContext context) => Results.Ok(
                        context.User.FindFirst(
                            identity.AuthenticationScheme)?.Value))
                .RequireAuthorization();
            application.MapBootstrapAccountEndpoints();
            application.MapPasskeySignInEndpoints();
            await application.StartAsync();

            var client = application.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");
            return new(
                application,
                client,
                new CookieJar(),
                store,
                handler,
                bootstrap);
        }

        public async Task<string> GetAntiforgeryTokenAsync()
        {
            using var response = await SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/antiforgery"));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AntiforgeryResponse>())!
                .Token;
        }

        public async Task<CreationOptions> BeginBootstrapAsync(string token)
        {
            using var response = await PostAsync(
                "/Account/Passkeys/BootstrapOptions",
                JsonContent.Create(new LocalAccountEndpoints.BootstrapOptionsRequest(
                    "bootstrap-token",
                    "Personal workspace",
                    "Local owner")),
                token);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CreationOptions>())!;
        }

        public Task<HttpResponseMessage> CompleteBootstrapAsync(
            CreationOptions options,
            string token) =>
            PostAsync(
                "/Account/Passkeys/BootstrapComplete",
                JsonContent.Create(
                    new LocalAccountEndpoints.BootstrapCompleteRequest(
                        JsonSerializer.Serialize(new
                        {
                            options.Challenge,
                        }),
                        "/")),
                token);

        public async Task<AssertionOptions> BeginSignInAsync(string token)
        {
            using var response = await PostAsync(
                "/Account/Passkeys/SignInOptions",
                JsonContent.Create(new { }),
                token);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AssertionOptions>())!;
        }

        public Task<HttpResponseMessage> CompleteSignInAsync(
            AssertionOptions options,
            string userHandle,
            string token) =>
            PostAsync(
                "/Account/Passkeys/SignInComplete",
                JsonContent.Create(new LocalAccountEndpoints.SignInRequest(
                    JsonSerializer.Serialize(new
                    {
                        options.Challenge,
                        UserHandle = userHandle,
                    }),
                    "/")),
                token);

        public async Task<HttpStatusCode> GetWhoAmIStatusAsync()
        {
            using var response = await SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/whoami"));
            return response.StatusCode;
        }

        public Task<HttpResponseMessage> PostAsync(
            string path,
            HttpContent? content,
            string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = content,
            };
            request.Headers.TryAddWithoutValidation("Origin", "https://localhost");
            request.Headers.TryAddWithoutValidation(
                LocalAccountEndpoints.AntiforgeryHeader,
                token);
            return SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await application.StopAsync();
            await application.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            Cookies.Apply(request);
            var response = await client.SendAsync(request);
            Cookies.Capture(response);
            return response;
        }
    }

    private sealed class CookieJar
    {
        private readonly Dictionary<string, string> values =
            new(StringComparer.Ordinal);

        public void Apply(HttpRequestMessage request)
        {
            if (values.Count > 0)
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    string.Join(
                        "; ",
                        values.Select(pair => $"{pair.Key}={pair.Value}")));
            }
        }

        public void Capture(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
            {
                return;
            }

            foreach (var header in headers)
            {
                var pair = header.Split(';', 2)[0];
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = pair[..separator];
                var value = pair[(separator + 1)..];
                if (string.IsNullOrEmpty(value)
                    || header.Contains("max-age=0", StringComparison.OrdinalIgnoreCase))
                {
                    values.Remove(name);
                }
                else
                {
                    values[name] = value;
                }
            }
        }

        public string Get(string name) => values[name];

        public void Set(string name, string value) => values[name] = value;

        public void Mutate(string name, Func<string, string> mutate) =>
            values[name] = mutate(values[name]);

        public Dictionary<string, string> Snapshot() =>
            new(values, StringComparer.Ordinal);

        public void Restore(Dictionary<string, string> snapshot)
        {
            values.Clear();
            foreach (var pair in snapshot)
            {
                values.Add(pair.Key, pair.Value);
            }
        }
    }

    private sealed class FakeBootstrapOperations(FakePasskeyStore store)
        : ILocalIdentityBootstrapOperations
    {
        private bool initialized;

        public int CompletionCount { get; private set; }

        public Task<bool> CanBeginBootstrapAsync(
            HttpRequest request,
            string suppliedToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                !initialized
                && suppliedToken == "bootstrap-token"
                && request.IsHttps);

        public Task<BootstrapIdentityResult> CompleteBootstrapAsync(
            HttpRequest request,
            string suppliedToken,
            string tenantName,
            string userDisplayName,
            Guid credentialUserId,
            UserPasskeyInfo passkey,
            CancellationToken cancellationToken = default)
        {
            if (initialized
                || suppliedToken != "bootstrap-token"
                || tenantName != "Personal workspace"
                || userDisplayName != "Local owner")
            {
                throw new InvalidOperationException(
                    "Identity bootstrap requirements were not satisfied.");
            }

            var user = new AspNetIdentityUser
            {
                Id = credentialUserId,
                AppUserId = AppUserId.New(),
                UserName = $"owner-{credentialUserId:N}",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
            };
            store.Set(user, passkey);
            initialized = true;
            CompletionCount++;
            return Task.FromResult<BootstrapIdentityResult>(
                new(user, ["offline-recovery-code"]));
        }
    }

    private sealed class FakePasskeyHandler(FakePasskeyStore store)
        : IPasskeyHandler<AspNetIdentityUser>
    {
        public string? LastResolvedUserId { get; private set; }

        public Task<PasskeyCreationOptionsResult> MakeCreationOptionsAsync(
            PasskeyUserEntity userEntity,
            HttpContext httpContext)
        {
            var state = new FakeAttestationState(
                userEntity,
                NewChallenge());
            return Task.FromResult(new PasskeyCreationOptionsResult
            {
                CreationOptionsJson = JsonSerializer.Serialize(new
                {
                    challenge = state.Challenge,
                    user = new
                    {
                        id = userEntity.Id,
                        displayName = userEntity.DisplayName,
                    },
                }),
                AttestationState = JsonSerializer.Serialize(state),
            });
        }

        public Task<PasskeyRequestOptionsResult> MakeRequestOptionsAsync(
            AspNetIdentityUser? user,
            HttpContext httpContext)
        {
            var challenge = NewChallenge();
            return Task.FromResult(new PasskeyRequestOptionsResult
            {
                RequestOptionsJson = JsonSerializer.Serialize(new
                {
                    challenge,
                }),
                AssertionState = challenge,
            });
        }

        public Task<PasskeyAttestationResult> PerformAttestationAsync(
            PasskeyAttestationContext context)
        {
            var state = JsonSerializer.Deserialize<FakeAttestationState>(
                context.AttestationState!);
            var credential = JsonSerializer.Deserialize<FakeCredential>(
                context.CredentialJson);
            if (state is null
                || credential is null
                || credential.Challenge != state.Challenge)
            {
                throw new InvalidOperationException("Invalid fake attestation.");
            }

            return Task.FromResult(PasskeyAttestationResult.Success(
                CreatePasskey(state.Challenge),
                state.User));
        }

        public async Task<PasskeyAssertionResult<AspNetIdentityUser>>
            PerformAssertionAsync(PasskeyAssertionContext context)
        {
            var credential = JsonSerializer.Deserialize<FakeCredential>(
                context.CredentialJson);
            if (credential is null
                || credential.Challenge != context.AssertionState)
            {
                throw new InvalidOperationException("Invalid fake assertion.");
            }

            var user = await store.FindByIdAsync(
                credential.UserHandle!,
                context.HttpContext.RequestAborted);
            if (user is null || store.Passkey is null)
            {
                throw new InvalidOperationException("Unknown fake user handle.");
            }

            LastResolvedUserId = credential.UserHandle;
            return PasskeyAssertionResult.Success(store.Passkey, user);
        }

        private static UserPasskeyInfo CreatePasskey(string challenge) =>
            new(
                credentialId: [1, 2, 3, 4],
                publicKey: [5, 6, 7, 8],
                createdAt: DateTimeOffset.UtcNow,
                signCount: 0,
                transports: ["internal"],
                isUserVerified: true,
                isBackupEligible: false,
                isBackedUp: false,
                attestationObject: [1],
                clientDataJson: JsonSerializer.SerializeToUtf8Bytes(new
                {
                    challenge,
                }));

        private static string NewChallenge() =>
            Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
                RandomNumberGenerator.GetBytes(32));

        private sealed record FakeAttestationState(
            PasskeyUserEntity User,
            string Challenge);

        private sealed record FakeCredential(
            string Challenge,
            string? UserHandle = null);
    }

    private sealed class FakePasskeyStore
        : IUserStore<AspNetIdentityUser>, IUserPasskeyStore<AspNetIdentityUser>,
            IUserSecurityStampStore<AspNetIdentityUser>
    {
        public AspNetIdentityUser? User { get; private set; }

        public UserPasskeyInfo? Passkey { get; private set; }

        public void Set(AspNetIdentityUser user, UserPasskeyInfo passkey)
        {
            User = user;
            Passkey = passkey;
        }

        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.Id.ToString("D"));

        public Task<string?> GetUserNameAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task SetUserNameAsync(
            AspNetIdentityUser user,
            string? userName,
            CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(
            AspNetIdentityUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken)
        {
            User = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken)
        {
            User = null;
            Passkey = null;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<AspNetIdentityUser?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                User?.Id.ToString("D") == userId ? User : null);

        public Task<AspNetIdentityUser?> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken) =>
            Task.FromResult<AspNetIdentityUser?>(null);

        public Task AddOrUpdatePasskeyAsync(
            AspNetIdentityUser user,
            UserPasskeyInfo passkey,
            CancellationToken cancellationToken)
        {
            Passkey = passkey;
            return Task.CompletedTask;
        }

        public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult<IList<UserPasskeyInfo>>(
                Passkey is null ? [] : [Passkey]);

        public Task<AspNetIdentityUser?> FindByPasskeyIdAsync(
            byte[] credentialId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Passkey is not null
                && credentialId.AsSpan().SequenceEqual(Passkey.CredentialId)
                    ? User
                    : null);

        public Task<UserPasskeyInfo?> FindPasskeyAsync(
            AspNetIdentityUser user,
            byte[] credentialId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Passkey is not null
                && credentialId.AsSpan().SequenceEqual(Passkey.CredentialId)
                    ? Passkey
                    : null);

        public Task RemovePasskeyAsync(
            AspNetIdentityUser user,
            byte[] credentialId,
            CancellationToken cancellationToken)
        {
            Passkey = null;
            return Task.CompletedTask;
        }

        public Task SetSecurityStampAsync(
            AspNetIdentityUser user,
            string stamp,
            CancellationToken cancellationToken)
        {
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecurityStampAsync(
            AspNetIdentityUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.SecurityStamp);
    }
}
