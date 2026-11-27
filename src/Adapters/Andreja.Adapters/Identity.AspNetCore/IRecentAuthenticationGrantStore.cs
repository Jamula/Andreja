namespace Andreja.Adapters.Identity.AspNetCore;

public interface IRecentAuthenticationGrantStore
{
    Task IssueAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> IsValidAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> TryConsumeAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
