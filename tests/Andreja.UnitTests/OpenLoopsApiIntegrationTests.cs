using Andreja.Api.Contracts.OpenLoops;
using Andreja.AppHost.Identity;
using Andreja.AppHost.OpenLoops;
using Andreja.AppHost.Components.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Security;
using System.Security.Claims;
using System.Reflection;

namespace Andreja.UnitTests;

public sealed class OpenLoopsApiIntegrationTests : IClassFixture<OpenLoopsWebApplicationFactory>
{
    private readonly OpenLoopsWebApplicationFactory factory;

    public OpenLoopsApiIntegrationTests(OpenLoopsWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AnonymousUiRedirectsToExistingLoginAndApiReturnsJson401()
    {
        using var anonymous = factory.CreateAnonymousClient();

        var uiResponse = await anonymous.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, uiResponse.StatusCode);
        Assert.Equal("/Account/Login", uiResponse.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2F", uiResponse.Headers.Location?.Query);
        var loginResponse = await anonymous.GetAsync(uiResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            "Sign in to the development workspace",
            await loginResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var apiResponse = await anonymous.GetAsync($"{OpenLoopsApi.RoutePrefix}/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, apiResponse.StatusCode);
        Assert.Null(apiResponse.Headers.Location);
        Assert.Equal("application/json", apiResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "authentication-required",
            (await apiResponse.Content.ReadFromJsonAsync<ApiErrorDto>())?.Code);

        anonymous.DefaultRequestHeaders.Add("X-Andreja-Test-Authenticate", "true");
        var fakeHeaderResponse = await anonymous.GetAsync($"{OpenLoopsApi.RoutePrefix}/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, fakeHeaderResponse.StatusCode);
        Assert.Null(fakeHeaderResponse.Headers.Location);
    }

    [Fact]
    public async Task DevelopmentSignInUsesLocalReturnUrlAndEnablesUiAndApi()
    {
        using var authenticated = await factory.CreateDevelopmentSignedInClientAsync("/");

        var home = await authenticated.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        var unsafeResponse = await authenticated.PostAsJsonAsync(
            $"{OpenLoopsApi.RoutePrefix}/assistant/proposals",
            new AssistantTaskRequest { Message = "Must not be accepted" });

        Assert.Equal(HttpStatusCode.BadRequest, unsafeResponse.StatusCode);
        var error = await unsafeResponse.Content.ReadFromJsonAsync<ApiErrorDto>();
        Assert.Equal("invalid-antiforgery-token", error?.Code);
    }

    [Fact]
    public async Task CookieAccessDeniedEventReturnsJson403ForApiWithoutRedirect()
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/open-loops/forbidden";
        httpContext.Response.Body = new MemoryStream();
        var redirectContext = new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            new(
                IdentityConstants.ApplicationScheme,
                displayName: null,
                typeof(CookieAuthenticationHandler)),
            options,
            new AuthenticationProperties(),
            "https://localhost/Account/Login?ReturnUrl=%2Fapi");

        await options.Events.RedirectToAccessDenied(redirectContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        Assert.Contains(
            "\"code\":\"access-denied\"",
            await reader.ReadToEndAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedTypedApiCompletesTaskScenario()
    {
        using var client = await factory.CreateDevelopmentSignedInClientAsync("/");
        var provider = Assert.IsType<AssistantProviderDto>(
            await client.GetFromJsonAsync<AssistantProviderDto>(
                $"{OpenLoopsApi.RoutePrefix}/assistant/provider"));
        Assert.True(provider.Ready);
        Assert.Equal("deterministic", provider.Selection);
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

    [Fact]
    public async Task CircuitDelegationUsesRealTypedClientWithoutHttpContext()
    {
        Assert.Null(new HttpContextAccessor().HttpContext);
        using var client = factory.CreateCircuitApiClient();

        var provider = await client.Api.GetProviderAsync();
        Assert.True(provider.Ready);
        Assert.Empty(await client.Api.ListAsync());

        var proposed = await client.Api.ProposeAsync("Circuit delegated task");
        var proposal = Assert.IsType<TaskProposalDto>(proposed.Proposal);
        var confirmed = await client.Api.ConfirmAsync(
            proposal.Id,
            proposal.Version,
            "circuit-confirm-0001");
        var task = Assert.IsType<TaskDto>(confirmed.Task);
        var completed = await client.Api.CompleteAsync(
            task.Id,
            task.Version,
            "circuit-complete-0001");
        Assert.Equal(TaskStatusDto.Completed, completed.Task?.Status);
        Assert.Equal(
            TaskStatusDto.Completed,
            Assert.Single((await client.Api.ExportAsync()).Tasks).Status);
        var deleted = await client.Api.DeleteAsync(
            task.Id,
            completed.Task!.Version,
            "circuit-delete-0001");

        Assert.Equal("Applied", deleted.Outcome);
        Assert.Empty(await client.Api.ListAsync());
    }

    [Fact]
    public async Task InteractiveInitializationFailureBecomesSafeUiState()
    {
        var page = new Home();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        typeof(Home).GetProperty("Api", flags)!.SetValue(
            page,
            new OpenLoopsWebApplicationFactory.UnreachableOpenLoopsApiClient());
        var initialize = typeof(Home).GetMethod("OnInitializedAsync", flags)!;

        await Assert.IsAssignableFrom<Task>(initialize.Invoke(page, null));

        Assert.True((bool)typeof(Home).GetProperty("Loaded", flags)!.GetValue(page)!);
        Assert.Equal(
            "Andreja could not reach the task API. Nothing was changed.",
            typeof(Home).GetProperty("ErrorMessage", flags)!.GetValue(page));
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/tasks?view=today", true)]
    [InlineData("//example.test", false)]
    [InlineData("/\\example.test", false)]
    [InlineData("https://example.test", false)]
    [InlineData("", false)]
    public void ReturnUrlMustBeLocal(string returnUrl, bool expected) =>
        Assert.Equal(expected, LocalAccountEndpoints.IsLocalReturnUrl(returnUrl));

    [Fact]
    public async Task UnsafeReturnUrlIsReplacedAndDevelopmentSignInIsAbsentInProduction()
    {
        using var development = factory.CreateAnonymousClient();
        var login = await development.GetStringAsync(
            "/Account/Login?ReturnUrl=https%3A%2F%2Fexample.test");
        Assert.Contains("""name="returnUrl" value="/" """.Trim(), login, StringComparison.Ordinal);
        Assert.DoesNotContain(
            """name="returnUrl" value="https://example.test" """.Trim(),
            login,
            StringComparison.Ordinal);

        using var productionFactory = new ProductionWebApplicationFactory();
        using var production = productionFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var productionRoot = await production.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, productionRoot.StatusCode);
        Assert.Equal(
            LocalAccountEndpoints.LoginPath,
            productionRoot.Headers.Location?.AbsolutePath);
        var productionLogin = await production.GetAsync(productionRoot.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, productionLogin.StatusCode);
        Assert.Contains(
            "Production passkey sign-in is not shipped yet.",
            await productionLogin.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        var developmentEndpoint = await production.GetAsync(
            LocalAccountEndpoints.DevelopmentSignInPath);
        Assert.Equal(HttpStatusCode.NotFound, developmentEndpoint.StatusCode);
    }

    [Fact]
    public void DevelopmentPublicOriginMatchesCommittedHttpsLaunchProfile()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var developmentSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Andreja.AppHost",
            "appsettings.Development.json")));
        var launchSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Andreja.AppHost",
            "Properties",
            "launchSettings.json")));

        var publicOrigin = developmentSettings.RootElement
            .GetProperty("Andreja")
            .GetProperty("OpenLoops")
            .GetProperty("PublicOrigin")
            .GetString();
        var applicationUrl = launchSettings.RootElement
            .GetProperty("profiles")
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString();

        Assert.Equal("https://localhost:5001", publicOrigin);
        Assert.Contains(publicOrigin!, applicationUrl!, StringComparison.Ordinal);
        Assert.Equal(
            "Development",
            launchSettings.RootElement
                .GetProperty("profiles")
                .GetProperty("https")
                .GetProperty("environmentVariables")
                .GetProperty("ASPNETCORE_ENVIRONMENT")
                .GetString());
    }

    [Fact]
    public void DevelopmentTrustRelaxationIsLoopbackOnlyAndProductionRemainsStrict()
    {
        var developmentEnvironment = factory.Services
            .GetRequiredService<IWebHostEnvironment>();
        using var developmentHandler =
            OpenLoopsServiceCollectionExtensions.CreateSameOriginHandler(
                developmentEnvironment);
        var callback = Assert.IsType<
            Func<HttpRequestMessage, System.Security.Cryptography.X509Certificates.X509Certificate2?,
                System.Security.Cryptography.X509Certificates.X509Chain?, SslPolicyErrors, bool>>(
                developmentHandler.ServerCertificateCustomValidationCallback);

        Assert.True(callback(
            new(HttpMethod.Get, "https://localhost:5001"),
            null,
            null,
            SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(
            new(HttpMethod.Get, "https://example.test"),
            null,
            null,
            SslPolicyErrors.RemoteCertificateChainErrors));

        using var productionFactory = new ProductionWebApplicationFactory();
        var productionEnvironment = productionFactory.Services
            .GetRequiredService<IWebHostEnvironment>();
        using var productionHandler =
            OpenLoopsServiceCollectionExtensions.CreateSameOriginHandler(
                productionEnvironment);
        Assert.Null(productionHandler.ServerCertificateCustomValidationCallback);
    }
}

public sealed class OpenLoopsWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public CircuitClient CreateCircuitApiClient()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, TestAppUserId.ToString("D")),
                new(AndrejaClaimTypes.TenantId, TestTenantId.ToString("D")),
                new(AndrejaClaimTypes.AppUserId, TestAppUserId.ToString("D")),
                new(AndrejaClaimTypes.PrincipalId, TestPrincipalId.ToString("D")),
            ],
            "circuit-test"));
        var delegation = new CircuitDelegationHandler(
            new FixedAuthenticationStateProvider(principal),
            Services.GetRequiredService<ICircuitDelegationTokenService>())
        {
            InnerHandler = Server.CreateHandler(),
        };
        var httpClient = new HttpClient(delegation)
        {
            BaseAddress = new Uri("https://localhost"),
        };
        return new(new OpenLoopsApiClient(httpClient), httpClient);
    }

    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });

    public async Task<HttpClient> CreateDevelopmentSignedInClientAsync(string returnUrl)
    {
        var client = CreateAnonymousClient();
        var login = await client.GetStringAsync(
            $"{LocalAccountEndpoints.LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var tokenMatch = Regex.Match(
            login,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success);
        var token = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
        var response = await client.PostAsync(
            LocalAccountEndpoints.DevelopmentSignInPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnUrl"] = returnUrl,
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);
        return client;
    }

    private static readonly Guid TestTenantId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000C001");
    private static readonly Guid TestAppUserId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000C002");
    private static readonly Guid TestPrincipalId =
        Guid.Parse("0198D117-3D00-7000-8000-00000000C003");

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    public sealed class UnreachableOpenLoopsApiClient : IOpenLoopsApiClient
    {
        public Task<IReadOnlyList<TaskDto>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException();

        public Task<AssistantProviderDto> GetProviderAsync(
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException();

        public Task<AssistantTaskResponse> ProposeAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProposalOutcomeDto> ConfirmAsync(
            Guid proposalId,
            long expectedVersion,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TaskMutationOutcomeDto> CompleteAsync(
            Guid taskId,
            long expectedVersion,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TaskMutationOutcomeDto> DeleteAsync(
            Guid taskId,
            long expectedVersion,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TaskExportDto> ExportAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    public sealed class CircuitClient(
        IOpenLoopsApiClient api,
        HttpClient httpClient) : IDisposable
    {
        public IOpenLoopsApiClient Api { get; } = api;

        public void Dispose() => httpClient.Dispose();
    }
}

public sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Production");
}
