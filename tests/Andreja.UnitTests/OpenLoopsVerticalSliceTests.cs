using Andreja.Adapters.OpenTelemetry;
using Andreja.Api.Contracts.OpenLoops;
using Andreja.AppHost.OpenLoops;
using Andreja.Modules.Assistant;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Modules.Proposals;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Proposals;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

namespace Andreja.UnitTests;

public sealed class OpenLoopsVerticalSliceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 24, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AssistantProposalConfirmationTaskLifecycleIsExplicitAndIdempotent()
    {
        var fixture = CreateFixture();

        var assistantResult = await fixture.Assistant.ProposeTaskAsync(
            fixture.Context,
            "Renew the library card");

        Assert.NotNull(assistantResult.Proposal);
        Assert.Null(assistantResult.ErrorCode);
        Assert.Empty(await fixture.Application.ListAsync(fixture.Context));
        Assert.Equal("{}", assistantResult.Proposal.Diff.BeforeCanonical);
        Assert.Contains(
            "\"title\":\"Renew the library card\"",
            assistantResult.Proposal.Diff.AfterCanonical,
            StringComparison.Ordinal);

        var confirmation = await fixture.Application.ConfirmAsync(
            fixture.Context,
            assistantResult.Proposal.ProposalId,
            assistantResult.Proposal.Version,
            "confirm-0001");
        var confirmationRetry = await fixture.Application.ConfirmAsync(
            fixture.Context,
            assistantResult.Proposal.ProposalId,
            assistantResult.Proposal.Version,
            "confirm-0001");

        Assert.Equal(ProposalTransitionOutcome.Applied, confirmation.Outcome);
        Assert.Equal(ProposalTransitionOutcome.IdempotentReplay, confirmationRetry.Outcome);
        var task = Assert.Single(await fixture.Application.ListAsync(fixture.Context));
        Assert.Equal(task.Id, confirmation.Task?.Id);
        Assert.Equal(task.Id, confirmationRetry.Task?.Id);
        Assert.Single(fixture.TaskStore.AuditEntries, audit => audit.Operation == "create");

        var openVersion = task.Version;
        var completed = await fixture.Application.CompleteAsync(
            fixture.Context,
            task.Id,
            openVersion,
            "complete-0001");
        var completedRetry = await fixture.Application.CompleteAsync(
            fixture.Context,
            task.Id,
            openVersion,
            "complete-0001");
        var export = await fixture.Application.ExportAsync(fixture.Context);

        Assert.Equal(TaskMutationOutcome.Applied, completed.Outcome);
        Assert.Equal(TaskMutationOutcome.IdempotentReplay, completedRetry.Outcome);
        Assert.Equal(OpenLoopTaskStatus.Completed, completed.Task?.Status);
        Assert.Equal("andreja.open-loops.tasks.v1", export.SchemaVersion);
        Assert.Single(export.Tasks);
        Assert.Contains("credentials", export.Exclusions);

        var deleted = await fixture.Application.DeleteAsync(
            fixture.Context,
            task.Id,
            completed.Task!.Version,
            "delete-0001");

        Assert.Equal(TaskMutationOutcome.Applied, deleted.Outcome);
        Assert.Empty(await fixture.Application.ListAsync(fixture.Context));
        Assert.Equal(
            ["create", "complete", "delete"],
            fixture.TaskStore.AuditEntries.Select(entry => entry.Operation));
        Assert.All(
            fixture.TaskStore.AuditEntries,
            entry => Assert.DoesNotContain("Renew", entry.SourceReference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongTenantAndPrincipalCannotReadOrMutateTaskOrProposal()
    {
        var fixture = CreateFixture();
        var proposal = await fixture.Application.ProposeAsync(
            fixture.Context,
            new("Private task", null, null),
            "assistant:permission-test");
        var confirmed = await fixture.Application.ConfirmAsync(
            fixture.Context,
            proposal.ProposalId,
            proposal.Version,
            "confirm-owner");
        var task = Assert.IsType<OpenLoopTask>(confirmed.Task);
        var otherTenant = NewContext();
        var otherPrincipal = fixture.Context with { PrincipalId = PrincipalId.New() };

        Assert.Null(await fixture.Application.GetProposalAsync(otherTenant, proposal.ProposalId));
        Assert.Empty(await fixture.Application.ListAsync(otherTenant));
        Assert.Empty(await fixture.Application.ListAsync(otherPrincipal));

        var crossTenant = await fixture.TaskStore.CompleteAsync(
            otherTenant,
            task.Id,
            task.Version,
            "complete-cross-tenant",
            Now);
        var crossPrincipal = await fixture.TaskStore.DeleteAsync(
            otherPrincipal,
            task.Id,
            task.Version,
            "delete-cross-principal",
            Now);

        Assert.Equal(TaskMutationOutcome.NotFound, crossTenant.Outcome);
        Assert.Equal(TaskMutationOutcome.Denied, crossPrincipal.Outcome);
        Assert.Equal(OpenLoopTaskStatus.Open, task.Status);
    }

    [Fact]
    public async Task IdempotencyReceiptsArePrincipalScoped()
    {
        var fixture = CreateFixture();
        var proposal = await fixture.Application.ProposeAsync(
            fixture.Context,
            new("Shared tenant task", null, null),
            "assistant:principal-receipt");
        var confirmed = await fixture.Application.ConfirmAsync(
            fixture.Context,
            proposal.ProposalId,
            proposal.Version,
            "confirm-principal-receipt");
        var task = Assert.IsType<OpenLoopTask>(confirmed.Task);
        var otherPrincipal = fixture.Context with { PrincipalId = PrincipalId.New() };

        var denied = await fixture.TaskStore.CompleteAsync(
            otherPrincipal,
            task.Id,
            task.Version,
            "same-principal-key",
            Now);
        var owner = await fixture.TaskStore.CompleteAsync(
            fixture.Context,
            task.Id,
            task.Version,
            "same-principal-key",
            Now);

        Assert.Equal(TaskMutationOutcome.Denied, denied.Outcome);
        Assert.Equal(TaskMutationOutcome.Applied, owner.Outcome);
    }

    [Fact]
    public async Task ExpiredProposalProviderFailureAndCancellationHaveNoTaskEffect()
    {
        var fixture = CreateFixture();
        var proposal = await fixture.Application.ProposeAsync(
            fixture.Context,
            new("Expires", null, null),
            "assistant:expired");
        fixture.Time.Advance(TimeSpan.FromMinutes(11));

        var expired = await fixture.Application.ConfirmAsync(
            fixture.Context,
            proposal.ProposalId,
            proposal.Version,
            "confirm-expired");

        Assert.Equal(ProposalTransitionOutcome.Expired, expired.Outcome);
        Assert.Empty(await fixture.Application.ListAsync(fixture.Context));

        var failedAssistant = new OpenLoopsAssistantService(
            CreateProvider(AssistantResponseStatus.Failed),
            OpenLoopsSkill.CreateHost(fixture.Application));
        var failed = await failedAssistant.ProposeTaskAsync(fixture.Context, "Provider fails");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await fixture.Assistant.ProposeTaskAsync(
            fixture.Context,
            "Cancelled",
            cancellation.Token);

        Assert.Equal("provider-temporarily-unavailable", failed.ErrorCode);
        Assert.Equal("cancelled", cancelled.ErrorCode);
        Assert.Empty(await fixture.Application.ListAsync(fixture.Context));
    }

    [Fact]
    public void TaskDomainRejectsInvalidContentAndDuplicateCompletion()
    {
        var context = NewContext();

        Assert.Throws<ArgumentException>(() => new OpenLoopTask(
            Guid.CreateVersion7(),
            context.TenantId,
            context.PrincipalId,
            " ",
            null,
            null,
            "assistant",
            "assistant:test",
            Now));

        var task = new OpenLoopTask(
            Guid.CreateVersion7(),
            context.TenantId,
            context.PrincipalId,
            "Valid",
            null,
            null,
            "assistant",
            "assistant:test",
            Now);
        task.Complete(Now);

        Assert.Throws<InvalidOperationException>(() => task.Complete(Now));
    }

    [Fact]
    public void RequestContextRejectsAnonymousMissingDuplicateAndMalformedClaims()
    {
        var context = new DefaultHttpContext();
        Assert.Throws<IdentityAccessDeniedException>(() => TaskRequestContext.Require(context));

        context.User = Principal(
            (AndrejaClaimTypes.TenantId, Guid.CreateVersion7().ToString("D")),
            (AndrejaClaimTypes.AppUserId, Guid.CreateVersion7().ToString("D")));
        Assert.Throws<IdentityAccessDeniedException>(() => TaskRequestContext.Require(context));

        context.User = Principal(
            (AndrejaClaimTypes.TenantId, "not-a-guid"),
            (AndrejaClaimTypes.AppUserId, Guid.CreateVersion7().ToString("D")),
            (AndrejaClaimTypes.PrincipalId, Guid.CreateVersion7().ToString("D")));
        Assert.Throws<IdentityAccessDeniedException>(() => TaskRequestContext.Require(context));

        var tenant = Guid.CreateVersion7().ToString("D");
        context.User = Principal(
            (AndrejaClaimTypes.TenantId, tenant),
            (AndrejaClaimTypes.TenantId, tenant),
            (AndrejaClaimTypes.AppUserId, Guid.CreateVersion7().ToString("D")),
            (AndrejaClaimTypes.PrincipalId, Guid.CreateVersion7().ToString("D")));
        Assert.Throws<IdentityAccessDeniedException>(() => TaskRequestContext.Require(context));
    }

    [Fact]
    public async Task TypedClientObtainsAntiforgeryTokenAndSendsOnlyDtos()
    {
        var context = NewContext();
        var principal = Principal(
            (ClaimTypes.NameIdentifier, context.AppUserId.Value.ToString("D")),
            (AndrejaClaimTypes.TenantId, context.TenantId.Value.ToString("D")),
            (AndrejaClaimTypes.AppUserId, context.AppUserId.Value.ToString("D")),
            (AndrejaClaimTypes.PrincipalId, context.PrincipalId.Value.ToString("D")));
        var tokenService = new CircuitDelegationTokenService(
            new EphemeralDataProtectionProvider(),
            new MutableTimeProvider(Now));
        var handler = new RecordingHandler();
        var client = new OpenLoopsApiClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://andreja.test"),
            },
            new FixedAuthenticationStateProvider(principal),
            tokenService);

        var result = await client.ProposeAsync("Call the dentist");

        Assert.NotNull(result.Proposal);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(OpenLoopsApi.AntiforgeryRoute, handler.Requests[0].Path);
        Assert.Equal("test-token", handler.Requests[1].Antiforgery);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(
                CircuitDelegation.AuthorizationScheme,
                request.AuthorizationScheme);
            var validation = tokenService.ValidateAndConsume(
                Assert.IsType<string>(request.AuthorizationParameter),
                CircuitDelegation.OpenLoopsAudience);
            Assert.True(validation.Succeeded);
            Assert.Equal(
                context.PrincipalId.Value.ToString("D"),
                validation.Principal?.FindFirst(AndrejaClaimTypes.PrincipalId)?.Value);
        });
        Assert.Contains(
            ".AspNetCore.Antiforgery.test=cookie-value",
            handler.Requests[1].Cookie,
            StringComparison.Ordinal);
        Assert.Contains("\"message\":\"Call the dentist\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskContentIsRemovedFromTelemetry()
    {
        using var activity = new Activity("open-loops").Start();
        activity.SetTag("http.route", "/api/v1/open-loops/tasks");
        activity.SetTag("task.title", "CANARY-TASK-CONTENT");
        activity.SetTag("proposal.payload", "CANARY-PROPOSAL-CONTENT");

        new ContentSuppressingActivityProcessor().OnEnd(activity);

        Assert.Equal("/api/v1/open-loops/tasks", activity.GetTagItem("http.route"));
        Assert.Null(activity.GetTagItem("task.title"));
        Assert.Null(activity.GetTagItem("proposal.payload"));
    }

    private static Fixture CreateFixture()
    {
        var time = new MutableTimeProvider(Now);
        var taskStore = new InMemoryOpenLoopsTaskStore();
        var proposalStore = new InMemoryProposalStore();
        var application = new OpenLoopsTaskApplication(taskStore, proposalStore, time);
        var host = OpenLoopsSkill.CreateHost(application);
        var assistant = new OpenLoopsAssistantService(
            OpenLoopsSkill.CreateDeterministicProvider(),
            host);
        return new(NewContext(), time, taskStore, application, assistant);
    }

    private static TenantPrincipalContext NewContext() =>
        new(TenantId.New(), AppUserId.New(), PrincipalId.New(), OpenLoopsPolicy.Purpose);

    private static DeterministicAssistantProvider CreateProvider(AssistantResponseStatus status) =>
        new DeterministicAssistantProvider(
            new(
                "failure-test",
                "failure-test",
                AssistantCapability.TextCompletion | AssistantCapability.TypedTools),
            (request, _) => ValueTask.FromResult(new AssistantResponse(
                request.RequestId,
                status,
                null,
                [],
                new("failure-test", "failure-test", null, null, TimeSpan.Zero, "failed", 0, 0),
                new("provider-failed", "Provider detail that must not escape.", true))));

    private static System.Security.Claims.ClaimsPrincipal Principal(
        params (string Type, string Value)[] claims)
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            claims.Select(claim => new System.Security.Claims.Claim(claim.Type, claim.Value)),
            "unit-test");
        return new(identity);
    }

    private sealed record Fixture(
        TenantPrincipalContext Context,
        MutableTimeProvider Time,
        InMemoryOpenLoopsTaskStore TaskStore,
        OpenLoopsTaskApplication Application,
        OpenLoopsAssistantService Assistant);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues(OpenLoopsApi.AntiforgeryHeader, out var values)
                    ? values.Single()
                    : null,
                request.Headers.TryGetValues("Cookie", out var cookies)
                    ? cookies.Single()
                    : null,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));

            if (request.RequestUri.AbsolutePath == OpenLoopsApi.AntiforgeryRoute)
            {
                var response = Json(HttpStatusCode.OK, new AntiforgeryTokenDto("test-token"));
                response.Headers.Add(
                    "Set-Cookie",
                    ".AspNetCore.Antiforgery.test=cookie-value; path=/; secure; httponly");
                return response;
            }

            var proposal = new TaskProposalDto(
                Guid.CreateVersion7(),
                1,
                "Pending",
                "open-loops.create-task",
                "tasks/new",
                "{}",
                """{"title":"Call the dentist"}""",
                new string('A', 64),
                "assistant",
                "assistant:test",
                Now.AddMinutes(10),
                new(
                    OpenLoopsPolicy.Purpose,
                    OpenLoopsPolicy.ProposeCapability,
                    true,
                    "Review required."));
            return Json(HttpStatusCode.OK, new AssistantTaskResponse(proposal, "Review.", null));
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
            new(status) { Content = JsonContent.Create(value) };
    }

    private sealed record RecordedRequest(
        string Path,
        string? Antiforgery,
        string? Cookie,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Body);
}
