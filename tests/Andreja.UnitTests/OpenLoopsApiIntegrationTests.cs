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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
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
#if DEBUG
        Assert.Contains(
            "Sign in to the development workspace",
            await loginResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
#else
        Assert.Contains(
            "Sign in with a passkey",
            await loginResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
#endif

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

#if DEBUG
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
    public async Task SignOutClearsTheAuthenticatedCookie()
    {
        using var client = await factory.CreateDevelopmentSignedInClientAsync("/");
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; level < 5; level++)
        {
            repositoryRoot = repositoryRoot.Parent
                ?? throw new DirectoryNotFoundException();
        }
        var home = await File.ReadAllTextAsync(Path.Join(
            repositoryRoot.FullName,
            "src",
            "Andreja.AppHost",
            "Components",
            "Pages",
            "Home.razor"));
        var passkeys = await File.ReadAllTextAsync(Path.Join(
            repositoryRoot.FullName,
            "src",
            "Andreja.AppHost",
            "Components",
            "Pages",
            "Passkeys.razor"));
        using var passkeysResponse =
            await client.GetAsync(LocalAccountEndpoints.PasskeysPath);
        passkeysResponse.EnsureSuccessStatusCode();
        Assert.Contains(
            "href=\"@LocalAccountEndpoints.PasskeysPath\"",
            home,
            StringComparison.Ordinal);
        Assert.Contains("Account and security", home, StringComparison.Ordinal);
        Assert.Contains(
            "action=\"@LocalAccountEndpoints.LogoutPath\"",
            passkeys,
            StringComparison.Ordinal);
        Assert.Contains("Sign out", passkeys, StringComparison.Ordinal);
        var login = await client.GetStringAsync(LocalAccountEndpoints.LoginPath);
        var tokenMatch = Regex.Match(
            login,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success);

        var response = await client.PostAsync(
            LocalAccountEndpoints.LogoutPath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] =
                    WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            }));
        using var anonymousHome = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(LocalAccountEndpoints.LoginPath, response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, anonymousHome.StatusCode);
        Assert.Equal(
            LocalAccountEndpoints.LoginPath,
            anonymousHome.Headers.Location?.AbsolutePath);
    }
#endif

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

#if DEBUG
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
#endif

    [Fact]
    public async Task ActualDiTypedClientsKeepConcurrentCircuitIdentitiesIsolated()
    {
        Assert.Null(new HttpContextAccessor().HttpContext);
        var handlerBuildsBefore = factory.HandlerBuildCount;
        using var first = factory.CreateCircuitApiClient(
            Guid.Parse("0198D117-3D00-7000-8000-00000000C101"),
            Guid.Parse("0198D117-3D00-7000-8000-00000000C102"),
            Guid.Parse("0198D117-3D00-7000-8000-00000000C103"));
        using var second = factory.CreateCircuitApiClient(
            Guid.Parse("0198D117-3D00-7000-8000-00000000C201"),
            Guid.Parse("0198D117-3D00-7000-8000-00000000C202"),
            Guid.Parse("0198D117-3D00-7000-8000-00000000C203"));

        var proposals = await Task.WhenAll(
            first.Api.ProposeAsync("First circuit task"),
            second.Api.ProposeAsync("Second circuit task"));
        var firstProposal = Assert.IsType<TaskProposalDto>(proposals[0].Proposal);
        var secondProposal = Assert.IsType<TaskProposalDto>(proposals[1].Proposal);
        var confirmations = await Task.WhenAll(
            first.Api.ConfirmAsync(
                firstProposal.Id,
                firstProposal.Version,
                "circuit-first-confirm"),
            second.Api.ConfirmAsync(
                secondProposal.Id,
                secondProposal.Version,
                "circuit-second-confirm"));
        var firstTask = Assert.IsType<TaskDto>(confirmations[0].Task);
        var secondTask = Assert.IsType<TaskDto>(confirmations[1].Task);

        var lists = await Task.WhenAll(
            first.Api.ListAsync(),
            second.Api.ListAsync());
        Assert.Equal("First circuit task", Assert.Single(lists[0]).Title);
        Assert.Equal("Second circuit task", Assert.Single(lists[1]).Title);
        Assert.Equal(handlerBuildsBefore + 1, factory.HandlerBuildCount);

        var completions = await Task.WhenAll(
            first.Api.CompleteAsync(
                firstTask.Id,
                firstTask.Version,
                "circuit-first-complete"),
            second.Api.CompleteAsync(
                secondTask.Id,
                secondTask.Version,
                "circuit-second-complete"));
        Assert.All(
            completions,
            completion => Assert.Equal(TaskStatusDto.Completed, completion.Task?.Status));
        var exports = await Task.WhenAll(
            first.Api.ExportAsync(),
            second.Api.ExportAsync());
        Assert.Equal("First circuit task", Assert.Single(exports[0].Tasks).Title);
        Assert.Equal("Second circuit task", Assert.Single(exports[1].Tasks).Title);

        await Task.WhenAll(
            first.Api.DeleteAsync(
                firstTask.Id,
                completions[0].Task!.Version,
                "circuit-first-delete"),
            second.Api.DeleteAsync(
                secondTask.Id,
                completions[1].Task!.Version,
                "circuit-second-delete"));
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
    [InlineData("/\n/evil.example", false)]
    [InlineData("/\r/evil.example", false)]
    [InlineData("/\t/evil.example", false)]
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
            "Sign in with a passkey",
            await productionLogin.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        var developmentEndpoint = await production.GetAsync(
            "/Account/DevelopmentSignIn");
        Assert.Equal(HttpStatusCode.NotFound, developmentEndpoint.StatusCode);
#if !DEBUG
        Assert.Null(typeof(LocalAccountEndpoints).GetField(
            "DevelopmentSignInPath",
            BindingFlags.Public | BindingFlags.Static));
#endif
    }

    [Fact]
    public async Task AccountPagesUseExternalPasskeyScriptAndStrictSecurityHeaders()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(LocalAccountEndpoints.LoginPath);
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("identity-passkeys", content, StringComparison.Ordinal);
        Assert.DoesNotContain("navigator.credentials", content, StringComparison.Ordinal);
        Assert.Contains("Sign in with a passkey", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Account and security", content, StringComparison.Ordinal);
        Assert.True(response.Headers.TryGetValues(
            "Content-Security-Policy",
            out var policies));
        var policy = Assert.Single(policies);
        Assert.Contains("script-src 'self' 'nonce-", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        var nonce = Regex.Match(
            policy,
            """script-src 'self' 'nonce-([^']+)'""",
            RegexOptions.CultureInvariant);
        Assert.True(nonce.Success);
        Assert.Contains(
            $"nonce=\"{nonce.Groups[1].Value}\"",
            content,
            StringComparison.Ordinal);

        var bootstrap = await client.GetStringAsync(LocalAccountEndpoints.BootstrapPath);
        Assert.Contains("Create the first administrator", bootstrap, StringComparison.Ordinal);
        Assert.Contains("Save your recovery codes now", bootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "method=\"post\" data-passkey-bootstrap",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", bootstrap, StringComparison.Ordinal);
        var recovery = await client.GetStringAsync(LocalAccountEndpoints.RecoveryPath);
        Assert.Contains("Replace lost passkeys", recovery, StringComparison.Ordinal);
        Assert.Contains("signs out existing sessions", recovery, StringComparison.Ordinal);
        Assert.Contains(
            "method=\"post\" data-passkey-recovery",
            recovery,
            StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", recovery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountEnhancedNavigationKeepsIdempotentDelegatedHandlers()
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; level < 5; level++)
        {
            repositoryRoot = repositoryRoot.Parent
                ?? throw new DirectoryNotFoundException();
        }
        var script = await File.ReadAllTextAsync(Path.Join(
            repositoryRoot.FullName,
            "src",
            "Andreja.AppHost",
            "wwwroot",
            "identity-passkeys.js"));

        Assert.Contains(
            "window.andrejaIdentityPasskeysInitialized",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            $"const antiforgeryHeader = \"{OpenLoopsApi.AntiforgeryHeader}\"",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "X-Andreja-Antiforgery",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"submit\", handleSubmit)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Blazor.addEventListener(\"enhancedload\", initializeIdentityPage)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "globalThis.Blazor?.addEventListener",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.andrejaIdentityEnhancedLoadSubscribed",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "document.addEventListener(\"enhancedload\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "form.querySelectorAll(\"button\")",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "querySelectorAll(\"button, input\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "revoke.setAttribute(\"aria-label\", `Remove passkey ${passkey.name}`)",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "querySelector(\"[data-passkey-signin]\")?.addEventListener",
            script,
            StringComparison.Ordinal);

        using var client = factory.CreateAnonymousClient();
        foreach (var request in new[]
                 {
                     LocalAccountEndpoints.LoginPath,
                     LocalAccountEndpoints.BootstrapPath,
                     LocalAccountEndpoints.RecoveryPath,
                     LocalAccountEndpoints.LoginPath,
                 }.Select(path => new HttpRequestMessage(HttpMethod.Get, path)))
        {
            using (request)
            {
                request.Headers.TryAddWithoutValidation("blazor-enhanced-nav", "on");
                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
        }
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
    private int handlerBuildCount;

    public int HandlerBuildCount => Volatile.Read(ref handlerBuildCount);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AuthenticationStateProvider>();
            services.AddScoped<CircuitTestIdentity>();
            services.AddScoped<AuthenticationStateProvider, ScopedAuthenticationStateProvider>();
            services.ConfigureAll<HttpClientFactoryOptions>(
                options => options.HttpMessageHandlerBuilderActions.Add(httpBuilder =>
                {
                    Interlocked.Increment(ref handlerBuildCount);
                    httpBuilder.PrimaryHandler = Server.CreateHandler();
                }));
        });
    }

    public CircuitClient CreateCircuitApiClient(
        Guid tenantId,
        Guid appUserId,
        Guid principalId)
    {
        _ = Server;
        var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CircuitTestIdentity>().Principal =
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, appUserId.ToString("D")),
                new(AndrejaClaimTypes.TenantId, tenantId.ToString("D")),
                new(AndrejaClaimTypes.AppUserId, appUserId.ToString("D")),
                new(AndrejaClaimTypes.PrincipalId, principalId.ToString("D")),
            ],
            "circuit-test"));
        return new(
            scope.ServiceProvider.GetRequiredService<IOpenLoopsApiClient>(),
            scope);
    }

    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });

#if DEBUG
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
#endif

    private sealed class CircuitTestIdentity
    {
        public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());
    }

    private sealed class ScopedAuthenticationStateProvider(CircuitTestIdentity identity)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(identity.Principal));
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
        IServiceScope scope) : IDisposable
    {
        public IOpenLoopsApiClient Api { get; } = api;

        public void Dispose() => scope.Dispose();
    }
}

public sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Production");
}
