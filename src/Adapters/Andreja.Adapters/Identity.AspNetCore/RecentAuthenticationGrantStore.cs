using Andreja.Adapters.PostgreSql;
using Microsoft.EntityFrameworkCore;

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

public sealed class PostgreSqlRecentAuthenticationGrantStore(
    AndrejaIdentityDbContext database,
    TimeProvider timeProvider) : IRecentAuthenticationGrantStore
{
    public async Task IssueAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await database.IdentityRecentAuthenticationGrants
            .Where(grant =>
                grant.UserId == userId
                && (grant.ConsumedAt != null
                    || grant.ExpiresAt <= timeProvider.GetUtcNow()))
            .ExecuteDeleteAsync(cancellationToken);
        database.IdentityRecentAuthenticationGrants.Add(
            new(
                Guid.CreateVersion7(),
                userId,
                nonceHash.ToArray(),
                expiresAt));
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsValidAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        database.IdentityRecentAuthenticationGrants.AnyAsync(
            grant =>
                grant.UserId == userId
                && grant.NonceHash == nonceHash
                && grant.ConsumedAt == null
                && grant.ExpiresAt > now,
            cancellationToken);

    public async Task<bool> TryConsumeAsync(
        Guid userId,
        byte[] nonceHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var affected = await database.IdentityRecentAuthenticationGrants
            .Where(grant =>
                grant.UserId == userId
                && grant.NonceHash == nonceHash
                && grant.ConsumedAt == null
                && grant.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    grant => grant.ConsumedAt,
                    now),
                cancellationToken);
        return affected == 1;
    }
}
