using Andreja.Api.Contracts.OpenLoops;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;

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

public sealed class OpenLoopsApiClient(
    HttpClient httpClient,
    AuthenticationStateProvider authenticationStateProvider,
    ICircuitDelegationTokenService tokenService) : IOpenLoopsApiClient
{
    private string? antiforgeryToken;
    private string? antiforgeryCookie;

    public async Task<IReadOnlyList<TaskDto>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await GetAsync<TaskDto[]>(
            $"{OpenLoopsApi.RoutePrefix}/tasks",
            cancellationToken) ?? [];

    public async Task<AssistantProviderDto> GetProviderAsync(
        CancellationToken cancellationToken = default) =>
        await GetAsync<AssistantProviderDto>(
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
        await GetAsync<TaskExportDto>(
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

        using var response = await SendAuthenticatedAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            antiforgeryToken = null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new OpenLoopsApiException("invalid-response", "The response was empty.");
    }

    private async Task<T?> GetAsync<T>(
        string route,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        using var response = await SendAuthenticatedAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authenticationState =
            await authenticationStateProvider.GetAuthenticationStateAsync();
        if (authenticationState.User.Identity?.IsAuthenticated != true)
        {
            throw new OpenLoopsApiException(
                "authentication-required",
                "Sign in again to continue.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            CircuitDelegation.AuthorizationScheme,
            tokenService.Issue(
                authenticationState.User,
                CircuitDelegation.OpenLoopsAudience));
        if (!string.IsNullOrWhiteSpace(antiforgeryCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", antiforgeryCookie);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            var cookie = values
                .Select(value => value.Split(';', 2)[0])
                .LastOrDefault(value =>
                    value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
            if (cookie is not null)
            {
                antiforgeryCookie = cookie;
            }
        }

        return response;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorDto? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(
                cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
        }

        throw new OpenLoopsApiException(
            error?.Code ?? $"http-{(int)response.StatusCode}",
            error?.Message ?? "The request could not be completed.");
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        var response = await GetAsync<AntiforgeryTokenDto>(
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
