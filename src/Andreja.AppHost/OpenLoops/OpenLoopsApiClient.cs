using Andreja.Api.Contracts.OpenLoops;
using System.Net;
using System.Net.Http.Json;

namespace Andreja.AppHost.OpenLoops;

public interface IOpenLoopsApiClient
{
    Task<IReadOnlyList<TaskDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AssistantProviderDto> GetProviderAsync(CancellationToken cancellationToken = default);

    Task<AssistantTaskResponse> ProposeAsync(
        string message,
        CancellationToken cancellationToken = default);

    Task<ProposalOutcomeDto> ConfirmAsync(
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TaskMutationOutcomeDto> CompleteAsync(
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TaskMutationOutcomeDto> DeleteAsync(
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TaskExportDto> ExportAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenLoopsApiClient(HttpClient httpClient) : IOpenLoopsApiClient
{
    private string? antiforgeryToken;

    public async Task<IReadOnlyList<TaskDto>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<TaskDto[]>(
            $"{OpenLoopsApi.RoutePrefix}/tasks",
            cancellationToken) ?? [];

    public async Task<AssistantProviderDto> GetProviderAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<AssistantProviderDto>(
            $"{OpenLoopsApi.RoutePrefix}/assistant/provider",
            cancellationToken)
        ?? throw new OpenLoopsApiException(
            "invalid-response",
            "The assistant provider response was empty.");

    public Task<AssistantTaskResponse> ProposeAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        SendAsync<AssistantTaskResponse>(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/assistant/proposals",
            new AssistantTaskRequest { Message = message },
            cancellationToken);

    public Task<ProposalOutcomeDto> ConfirmAsync(
        Guid proposalId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync<ProposalOutcomeDto>(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/proposals/{proposalId:D}/confirm",
            new ConfirmProposalRequest
            {
                ExpectedVersion = expectedVersion,
                IdempotencyKey = idempotencyKey,
            },
            cancellationToken);

    public Task<TaskMutationOutcomeDto> CompleteAsync(
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync<TaskMutationOutcomeDto>(
            HttpMethod.Post,
            $"{OpenLoopsApi.RoutePrefix}/tasks/{taskId:D}/complete",
            new CompleteTaskRequest
            {
                ExpectedVersion = expectedVersion,
                IdempotencyKey = idempotencyKey,
            },
            cancellationToken);

    public Task<TaskMutationOutcomeDto> DeleteAsync(
        Guid taskId,
        long expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendAsync<TaskMutationOutcomeDto>(
            HttpMethod.Delete,
            $"{OpenLoopsApi.RoutePrefix}/tasks/{taskId:D}?version={expectedVersion}&idempotencyKey={Uri.EscapeDataString(idempotencyKey)}",
            content: null,
            cancellationToken);

    public async Task<TaskExportDto> ExportAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<TaskExportDto>(
            $"{OpenLoopsApi.RoutePrefix}/export",
            cancellationToken)
        ?? throw new OpenLoopsApiException("invalid-response", "The export response was empty.");

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string route,
        object? content,
        CancellationToken cancellationToken)
    {
        antiforgeryToken ??= await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(method, route);
        request.Headers.Add(OpenLoopsApi.AntiforgeryHeader, antiforgeryToken);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            antiforgeryToken = null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(
                cancellationToken: cancellationToken);
            throw new OpenLoopsApiException(
                error?.Code ?? $"http-{(int)response.StatusCode}",
                error?.Message ?? "The request could not be completed.");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new OpenLoopsApiException("invalid-response", "The response was empty.");
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<AntiforgeryTokenDto>(
            OpenLoopsApi.AntiforgeryRoute,
            cancellationToken);
        return response?.Token
            ?? throw new OpenLoopsApiException(
                "antiforgery-unavailable",
                "A request verification token could not be created.");
    }
}

public sealed class OpenLoopsApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
