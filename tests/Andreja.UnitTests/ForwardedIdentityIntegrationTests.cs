using System.Net;
using System.Net.Http.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Andreja.UnitTests;

public sealed class ForwardedIdentityIntegrationTests
{
    [Fact]
    public async Task TrustedLoopbackProxyRestoresExternalHttpsHostAndClient()
    {
        await using var host = await ProxyTestHost.StartAsync(CreateOptions());

        using var response = await host.SendProbeAsync(
            forwardedFor: "203.0.113.10",
            forwardedProto: "https",
            forwardedHost: "localhost",
            origin: "https://localhost");
        var result = await response.Content.ReadFromJsonAsync<ProxyProbe>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal("https", result.Scheme);
        Assert.Equal("localhost", result.Host);
        Assert.Equal("203.0.113.10", result.RemoteAddress);
    }

    [Fact]
    public async Task UntrustedForwarderCannotSpoofSchemeHostOrClient()
    {
        var configured = CreateOptions() with
        {
            TrustedProxyAddresses = ["192.0.2.44"],
        };
        await using var host = await ProxyTestHost.StartAsync(configured);

        using var response = await host.SendProbeAsync(
            forwardedFor: "203.0.113.10",
            forwardedProto: "https",
            forwardedHost: "localhost",
            origin: "https://localhost");
        var result = await response.Content.ReadFromJsonAsync<ProxyProbe>();

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Equal("http", result.Scheme);
        Assert.Equal("127.0.0.1", result.RemoteAddress);
    }

    [Theory]
    [InlineData("evil.example", "https://localhost")]
    [InlineData("localhost", "https://evil.example")]
    [InlineData("localhost:444", "https://localhost:444")]
    public async Task WrongForwardedHostPortOrOriginFailsClosed(
        string forwardedHost,
        string origin)
    {
        await using var host = await ProxyTestHost.StartAsync(CreateOptions());

        using var response = await host.SendProbeAsync(
            forwardedFor: "203.0.113.10",
            forwardedProto: "https",
            forwardedHost,
            origin);
        var result = await response.Content.ReadFromJsonAsync<ProxyProbe>();

        Assert.NotNull(result);
        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task ConfiguredExternalPortMustMatchExactly()
    {
        var configured = CreateOptions() with
        {
            AllowedOrigins = ["https://localhost:444"],
        };
        await using var host = await ProxyTestHost.StartAsync(configured);

        using var response = await host.SendProbeAsync(
            forwardedFor: "203.0.113.10",
            forwardedProto: "https",
            forwardedHost: "localhost:444",
            origin: "https://localhost:444");
        var result = await response.Content.ReadFromJsonAsync<ProxyProbe>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal("localhost:444", result.Host);
    }

    [Fact]
    public async Task ForwardLimitUsesNearestValueAndCannotPromoteSpoofedHttps()
    {
        await using var host = await ProxyTestHost.StartAsync(CreateOptions());

        using var response = await host.SendProbeAsync(
            forwardedFor: "198.51.100.1, 203.0.113.10",
            forwardedProto: "https, http",
            forwardedHost: "evil.example, localhost",
            origin: "https://localhost");
        var result = await response.Content.ReadFromJsonAsync<ProxyProbe>();

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Equal("http", result.Scheme);
        Assert.Equal("203.0.113.10", result.RemoteAddress);
    }

    [Fact]
    public async Task RecoveryLimiterPartitionsByValidatedForwardedClient()
    {
        var configured = CreateOptions() with
        {
            RecoveryRateLimitAttempts = 2,
            RecoveryGlobalRateLimitAttempts = 20,
        };
        await using var host = await ProxyTestHost.StartAsync(configured);

        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.1"));
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await host.SendRecoveryAsync("203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.2"));
        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.2"));
    }

    [Fact]
    public async Task UntrustedForwardedClientsShareSafeRemoteAddressBucket()
    {
        var configured = CreateOptions() with
        {
            TrustedProxyAddresses = ["192.0.2.44"],
            RecoveryRateLimitAttempts = 2,
            RecoveryGlobalRateLimitAttempts = 20,
        };
        await using var host = await ProxyTestHost.StartAsync(configured);

        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await host.SendRecoveryAsync("203.0.113.2"));
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await host.SendRecoveryAsync("198.51.100.1"));
    }

    [Fact]
    public async Task RecoveryLimiterHasBoundedGlobalAbuseCap()
    {
        var configured = CreateOptions() with
        {
            RecoveryRateLimitAttempts = 2,
            RecoveryGlobalRateLimitAttempts = 20,
        };
        await using var host = await ProxyTestHost.StartAsync(configured);

        for (var index = 1; index <= 20; index++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                await host.SendRecoveryAsync($"203.0.113.{index}"));
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            await host.SendRecoveryAsync("198.51.100.1"));
    }

    private static LocalIdentityOptions CreateOptions() =>
        new()
        {
            RelyingPartyId = "localhost",
            AllowedOrigins = ["https://localhost"],
            TrustedProxyAddresses = ["127.0.0.1"],
            BootstrapTokenFile = Path.GetFullPath("unused"),
            RecoveryRateLimitWindow = TimeSpan.FromMinutes(1),
        };

    private sealed record ProxyProbe(
        bool Accepted,
        string Scheme,
        string Host,
        string? RemoteAddress);

    private sealed class ProxyTestHost(
        WebApplication application,
        HttpClient client,
        LocalIdentityOptions identity) : IAsyncDisposable
    {
        public static async Task<ProxyTestHost> StartAsync(
            LocalIdentityOptions identity)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
            });
            builder.WebHost.ConfigureKestrel(
                server => server.Listen(IPAddress.Loopback, 0));
            builder.Services.Configure<ForwardedHeadersOptions>(
                forwarded => LocalIdentityNetworkSecurity.ConfigureForwardedHeaders(
                    forwarded,
                    identity));
            builder.Services.AddRateLimiter();
            builder.Services.Configure<RateLimiterOptions>(
                limiter => LocalIdentityNetworkSecurity.ConfigureRateLimiting(
                    limiter,
                    identity));

            var application = builder.Build();
            application.UseForwardedHeaders();
            application.UseRouting();
            application.UseRateLimiter();
            application.MapGet(
                "/probe",
                (HttpContext context) => Results.Ok(new ProxyProbe(
                    LocalIdentityOperations.IsAcceptedRelyingPartyRequest(
                        context.Request,
                        identity),
                    context.Request.Scheme,
                    context.Request.Host.Value ?? string.Empty,
                    context.Connection.RemoteIpAddress?.ToString())));
            application.MapPost(
                    LocalIdentityNetworkSecurity.RecoveryOptionsPath,
                    () => Results.Ok())
                .RequireRateLimiting(
                    LocalIdentityNetworkSecurity.RecoveryRateLimitPolicy);
            await application.StartAsync();

            var server = application.Services.GetRequiredService<IServer>();
            var address = Assert.Single(
                server.Features.Get<IServerAddressesFeature>()!.Addresses);
            var client = new HttpClient(new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
            })
            {
                BaseAddress = new Uri(address),
            };
            return new(application, client, identity);
        }

        public async Task<HttpResponseMessage> SendProbeAsync(
            string forwardedFor,
            string forwardedProto,
            string forwardedHost,
            string origin)
        {
            using var request = CreateForwardedRequest(
                HttpMethod.Get,
                "/probe",
                forwardedFor,
                forwardedProto,
                forwardedHost,
                origin);
            return await client.SendAsync(request);
        }

        public async Task<HttpStatusCode> SendRecoveryAsync(string clientAddress)
        {
            using var request = CreateForwardedRequest(
                HttpMethod.Post,
                LocalIdentityNetworkSecurity.RecoveryOptionsPath,
                clientAddress,
                "https",
                "localhost",
                "https://localhost");
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await application.StopAsync();
            await application.DisposeAsync();
        }

        private HttpRequestMessage CreateForwardedRequest(
            HttpMethod method,
            string path,
            string forwardedFor,
            string forwardedProto,
            string forwardedHost,
            string origin)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Host = identity.RelyingPartyId;
            request.Headers.TryAddWithoutValidation("Origin", origin);
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", forwardedProto);
            request.Headers.TryAddWithoutValidation("X-Forwarded-Host", forwardedHost);
            return request;
        }
    }
}
