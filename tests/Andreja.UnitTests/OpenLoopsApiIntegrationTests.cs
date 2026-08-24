using Andreja.Api.Contracts.OpenLoops;
using Andreja.AppHost.OpenLoops;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Andreja.UnitTests;

public sealed class OpenLoopsApiIntegrationTests : IClassFixture<OpenLoopsWebApplicationFactory>
{
    private readonly OpenLoopsWebApplicationFactory factory;

    public OpenLoopsApiIntegrationTests(OpenLoopsWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AnonymousAndMissingAntiforgeryRequestsFailClosed()
    {
        using var anonymous = factory.CreateClient();
        var anonymousResponse = await anonymous.GetAsync($"{OpenLoopsApi.RoutePrefix}/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var authenticated = factory.CreateAuthenticatedClient();
        var unsafeResponse = await authenticated.PostAsJsonAsync(
            $"{OpenLoopsApi.RoutePrefix}/assistant/proposals",
            new AssistantTaskRequest { Message = "Must not be accepted" });

        Assert.Equal(HttpStatusCode.BadRequest, unsafeResponse.StatusCode);
        var error = await unsafeResponse.Content.ReadFromJsonAsync<ApiErrorDto>();
        Assert.Equal("invalid-antiforgery-token", error?.Code);
    }

    [Fact]
    public async Task AuthenticatedTypedApiCompletesTaskScenario()
    {
        using var client = factory.CreateAuthenticatedClient();
        var token = await client.GetFromJsonAsync<AntiforgeryTokenDto>(
            OpenLoopsApi.AntiforgeryRoute);
        Assert.NotNull(token);

        using var propose = new HttpRequestMessage(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/assistant/proposals")
        {
            Content = JsonContent.Create(new AssistantTaskRequest
            {
                Message = "API integration task",
            }),
        };
        propose.Headers.Add(OpenLoopsApi.AntiforgeryHeader, token.Token);
        using var proposedResponse = await client.SendAsync(propose);
        proposedResponse.EnsureSuccessStatusCode();
        var proposed = await proposedResponse.Content.ReadFromJsonAsync<AssistantTaskResponse>();
        var proposal = Assert.IsType<TaskProposalDto>(proposed?.Proposal);

        Assert.Empty(await client.GetFromJsonAsync<TaskDto[]>(
            $"{OpenLoopsApi.RoutePrefix}/tasks") ?? []);

        using var confirm = new HttpRequestMessage(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/proposals/{proposal.Id:D}/confirm")
        {
            Content = JsonContent.Create(new ConfirmProposalRequest
            {
                ExpectedVersion = proposal.Version,
                IdempotencyKey = "api-confirm-0001",
            }),
        };
        confirm.Headers.Add(OpenLoopsApi.AntiforgeryHeader, token.Token);
        using var confirmedResponse = await client.SendAsync(confirm);
        confirmedResponse.EnsureSuccessStatusCode();
        var confirmed = await confirmedResponse.Content.ReadFromJsonAsync<ProposalOutcomeDto>();
        var task = Assert.IsType<TaskDto>(confirmed?.Task);

        using var complete = new HttpRequestMessage(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/tasks/{task.Id:D}/complete")
        {
            Content = JsonContent.Create(new CompleteTaskRequest
            {
                ExpectedVersion = task.Version,
                IdempotencyKey = "api-complete-0001",
            }),
        };
        complete.Headers.Add(OpenLoopsApi.AntiforgeryHeader, token.Token);
        using var completedResponse = await client.SendAsync(complete);
        completedResponse.EnsureSuccessStatusCode();

        var export = await client.GetFromJsonAsync<TaskExportDto>(
            $"{OpenLoopsApi.RoutePrefix}/export");
        Assert.Equal(TaskStatusDto.Completed, Assert.Single(export!.Tasks).Status);

        using var delete = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{OpenLoopsApi.RoutePrefix}/tasks/{task.Id:D}?version={task.Version + 1}&idempotencyKey=api-delete-0001");
        delete.Headers.Add(OpenLoopsApi.AntiforgeryHeader, token.Token);
        using var deletedResponse = await client.SendAsync(delete);
        deletedResponse.EnsureSuccessStatusCode();
        Assert.Empty(await client.GetFromJsonAsync<TaskDto[]>(
            $"{OpenLoopsApi.RoutePrefix}/tasks") ?? []);
    }
}

public sealed class OpenLoopsWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly Guid TenantId = Guid.Parse("0198D117-3D00-7000-8000-000000000001");
    private static readonly Guid AppUserId = Guid.Parse("0198D117-3D00-7000-8000-000000000002");
    private static readonly Guid PrincipalId = Guid.Parse("0198D117-3D00-7000-8000-000000000003");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, "true");
        return client;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "andreja-test-only";
        public const string Header = "X-Andreja-Test-Authenticate";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var value)
                || !string.Equals(value, "true", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, AppUserId.ToString("D")),
                new Claim(AndrejaClaimTypes.TenantId, TenantId.ToString("D")),
                new Claim(AndrejaClaimTypes.AppUserId, AppUserId.ToString("D")),
                new Claim(AndrejaClaimTypes.PrincipalId, PrincipalId.ToString("D")),
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
