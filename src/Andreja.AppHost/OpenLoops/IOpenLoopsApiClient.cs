using Andreja.Api.Contracts.OpenLoops;

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
