using Andreja.Api.Contracts.OpenLoops;
using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Platform.Contracts.Proposals;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using Andreja.Platform.Contracts.Assistant;

namespace Andreja.AppHost.OpenLoops;

public static class OpenLoopsEndpoints
{
    public static IEndpointRouteBuilder MapOpenLoopsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(OpenLoopsApi.AntiforgeryRoute, (
            HttpContext httpContext,
            IAntiforgery antiforgery) =>
        {
            _ = TaskRequestContext.Require(httpContext);
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new AntiforgeryTokenDto(
                tokens.RequestToken
                    ?? throw new InvalidOperationException("An antiforgery token was not created.")));
        }).RequireAuthorization("andreja-user");

        var group = endpoints.MapGroup(OpenLoopsApi.RoutePrefix)
            .RequireAuthorization("andreja-user");

        group.MapGet("/assistant/provider", async (
            IAssistantProvider provider,
            IOptions<OpenLoopsOptions> options,
            CancellationToken cancellationToken) =>
        {
            var capabilities = await provider.GetCapabilitiesAsync(cancellationToken);
            var deterministic = options.Value.AssistantProvider == "deterministic";
            var compatible = options.Value.OpenAiCompatible;
            var configuredEndpoint = Uri.TryCreate(
                compatible.Endpoint,
                UriKind.Absolute,
                out var endpoint)
                ? endpoint
                : null;
            var ready = deterministic
                || (configuredEndpoint is not null
                    && File.Exists(compatible.CredentialFile)
                    && (configuredEndpoint.IsLoopback
                        || HasInitialExternalBudget(compatible)));
            return Results.Ok(new AssistantProviderDto(
                capabilities.Provider,
                capabilities.Model,
                options.Value.AssistantProvider,
                ready,
                deterministic
                    ? "Local deterministic test provider; no content leaves this process."
                    : $"{compatible.ProviderDisclosure} Retention: {compatible.RetentionDisclosure}"));
        });

        group.MapGet("/tasks", async (
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var context = TaskRequestContext.Require(httpContext);
            var tasks = await application.ListAsync(context, cancellationToken);
            return Results.Ok(tasks.Select(ToDto));
        });

        group.MapPost("/assistant/proposals", async (
            AssistantTaskRequest request,
            HttpContext httpContext,
            OpenLoopsAssistantService assistant,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateMessage(request.Message);
            if (validation is not null)
            {
                return Results.BadRequest(validation);
            }

            var result = await assistant.ProposeTaskAsync(
                TaskRequestContext.Require(httpContext),
                request.Message,
                cancellationToken);
            return result.ErrorCode is null
                ? Results.Ok(new AssistantTaskResponse(
                    result.Proposal is null ? null : ToDto(result.Proposal),
                    result.Message,
                    null))
                : SafeFailure(result.ErrorCode);
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapGet("/proposals/{proposalId:guid}", async (
            Guid proposalId,
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var proposal = await application.GetProposalAsync(
                TaskRequestContext.Require(httpContext),
                proposalId,
                cancellationToken);
            return proposal is null ? Results.NotFound() : Results.Ok(ToDto(proposal));
        });

        group.MapPost("/proposals/{proposalId:guid}/confirm", async (
            Guid proposalId,
            ConfirmProposalRequest request,
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateMutation(request.ExpectedVersion, request.IdempotencyKey);
            if (validation is not null)
            {
                return Results.BadRequest(validation);
            }

            var result = await application.ConfirmAsync(
                TaskRequestContext.Require(httpContext),
                proposalId,
                request.ExpectedVersion,
                request.IdempotencyKey,
                cancellationToken);
            var response = new ProposalOutcomeDto(
                result.Outcome.ToString(),
                result.Task is null ? null : ToDto(result.Task),
                result.Proposal is null ? null : ToDto(result.Proposal));
            return result.Outcome switch
            {
                ProposalTransitionOutcome.Applied
                    or ProposalTransitionOutcome.IdempotentReplay => Results.Ok(response),
                ProposalTransitionOutcome.NotFound => Results.NotFound(),
                ProposalTransitionOutcome.Expired => Results.Conflict(response),
                ProposalTransitionOutcome.Denied => Results.Forbid(),
                _ => Results.Conflict(response),
            };
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPost("/tasks/{taskId:guid}/complete", async (
            Guid taskId,
            CompleteTaskRequest request,
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateMutation(request.ExpectedVersion, request.IdempotencyKey);
            if (validation is not null)
            {
                return Results.BadRequest(validation);
            }

            var result = await application.CompleteAsync(
                TaskRequestContext.Require(httpContext),
                taskId,
                request.ExpectedVersion,
                request.IdempotencyKey,
                cancellationToken);
            return MutationResult(result);
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapDelete("/tasks/{taskId:guid}", async (
            Guid taskId,
            long version,
            string idempotencyKey,
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateMutation(version, idempotencyKey);
            if (validation is not null)
            {
                return Results.BadRequest(validation);
            }

            var result = await application.DeleteAsync(
                TaskRequestContext.Require(httpContext),
                taskId,
                version,
                idempotencyKey,
                cancellationToken);
            return MutationResult(result);
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapGet("/export", async (
            HttpContext httpContext,
            OpenLoopsTaskApplication application,
            CancellationToken cancellationToken) =>
        {
            var export = await application.ExportAsync(
                TaskRequestContext.Require(httpContext),
                cancellationToken);
            return Results.Ok(new TaskExportDto(
                export.SchemaVersion,
                export.CreatedAtUtc,
                export.Tasks.Select(ToDto).ToArray(),
                export.Exclusions));
        });

        return endpoints;
    }

    public sealed class AntiforgeryValidationFilter(IAntiforgery antiforgery) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new ApiErrorDto(
                    "invalid-antiforgery-token",
                    "The request verification token is missing or expired."));
            }

            return await next(context);
        }
    }

    public static TaskDto ToDto(OpenLoopTask task) =>
        new(
            task.Id,
            task.Version,
            task.Title,
            task.Details,
            task.DueAt,
            task.Status == OpenLoopTaskStatus.Open ? TaskStatusDto.Open : TaskStatusDto.Completed,
            task.SourceKind,
            task.SourceReference,
            task.CreatedAt,
            task.CompletedAt);

    public static TaskProposalDto ToDto(Proposal proposal) =>
        new(
            proposal.ProposalId,
            proposal.Version,
            proposal.State.ToString(),
            proposal.Operation.Operation,
            proposal.Operation.ResourceReference,
            proposal.Diff.BeforeCanonical,
            proposal.Diff.AfterCanonical,
            proposal.Operation.PayloadDigest,
            proposal.Source.Kind,
            proposal.Source.Reference,
            proposal.ExpiresAt,
            new(
                proposal.Purpose,
                OpenLoopsPolicy.ProposeCapability,
                true,
                "Creating a task changes your saved Open Loops data and requires confirmation."));

    private static IResult MutationResult(TaskMutationResult result)
    {
        var response = new TaskMutationOutcomeDto(
            result.Outcome.ToString(),
            result.Task is null ? null : ToDto(result.Task));
        return result.Outcome switch
        {
            TaskMutationOutcome.Applied or TaskMutationOutcome.IdempotentReplay =>
                Results.Ok(response),
            TaskMutationOutcome.NotFound => Results.NotFound(),
            TaskMutationOutcome.Denied => Results.Forbid(),
            _ => Results.Conflict(response),
        };
    }

    private static ApiErrorDto? ValidateMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) || message.Trim().Length > 500
            ? new("invalid-request", "Enter a task request between 1 and 500 characters.")
            : null;

    private static ApiErrorDto? ValidateMutation(long version, string? idempotencyKey) =>
        version < 1 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 128
            ? new("invalid-request", "Version and a valid idempotency key are required.")
            : null;

    private static IResult SafeFailure(string code)
    {
        var status = code switch
        {
            "cancelled" => StatusCodes.Status408RequestTimeout,
            "not-authorized" => StatusCodes.Status403Forbidden,
            "invalid-task" => StatusCodes.Status400BadRequest,
            "provider-temporarily-unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status502BadGateway,
        };
        var message = status switch
        {
            StatusCodes.Status408RequestTimeout => "The request was cancelled.",
            StatusCodes.Status403Forbidden => "The task proposal is not authorized.",
            StatusCodes.Status400BadRequest => "The task proposal is invalid.",
            StatusCodes.Status503ServiceUnavailable => "The assistant provider is temporarily unavailable.",
            _ => "The assistant could not create a proposal.",
        };
        return Results.Json(new ApiErrorDto(code, message), statusCode: status);
    }

    private static bool HasInitialExternalBudget(
        OpenAiCompatibleProviderOptions options)
    {
        try
        {
            return options.ApprovedExternalTotalUnits >= checked(
                options.MaximumInputUnits + options.MaximumOutputUnits);
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
