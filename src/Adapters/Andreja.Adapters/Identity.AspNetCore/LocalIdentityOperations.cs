using System.Data;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public sealed record BootstrapIdentityResult(
    AspNetIdentityUser User,
    IReadOnlyList<string> RecoveryCodes);

public sealed record RecoveryStartResult(
    Guid RecoveryCodeId,
    Guid UserId,
    string SecurityStamp,
    string UserName,
    string DisplayName);

public sealed record RecoveryCompletionResult(
    IReadOnlyList<string> RecoveryCodes);

public interface ILocalIdentityBootstrapOperations
{
    Task<bool> CanBeginBootstrapAsync(
        HttpRequest request,
        string suppliedToken,
        CancellationToken cancellationToken = default);

    Task<BootstrapIdentityResult> CompleteBootstrapAsync(
        HttpRequest request,
        string suppliedToken,
        string tenantName,
        string userDisplayName,
        Guid credentialUserId,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken = default);
}

public interface ILocalIdentityRecoveryOperations
{
    Task<RecoveryStartResult?> BeginRecoveryAsync(
        string? recoveryCode,
        CancellationToken cancellationToken = default);

    Task<RecoveryCompletionResult> CompleteRecoveryAsync(
        RecoveryStartResult recovery,
        UserPasskeyInfo newPasskey,
        CancellationToken cancellationToken = default);

    Task AuditRecoveryFailureAsync(
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public interface ILocalPasskeyManagementOperations
{
    Task RegisterPasskeyAsync(
        AspNetIdentityUser user,
        UserPasskeyInfo passkey,
        string deviceName,
        CancellationToken cancellationToken = default);

    Task RevokePasskeyAsync(
        AspNetIdentityUser user,
        byte[] credentialId,
        CancellationToken cancellationToken = default);
}

public sealed class LocalIdentityOperations(
    AndrejaIdentityDbContext database,
    ScopedTenantPrincipalContext tenantContext,
    UserManager<AspNetIdentityUser> userManager,
    IBootstrapTokenVerifier bootstrapTokenVerifier,
    IOptions<LocalIdentityOptions> options,
    TimeProvider timeProvider)
    : ILocalIdentityBootstrapOperations,
        ILocalIdentityRecoveryOperations,
        ILocalPasskeyManagementOperations
{
    public const int RecoveryCodeMinimumLength = 43;
    public const int RecoveryCodeMaximumLength = 64;
    private const int RecoveryHashIterations = 210_000;
    private const long BootstrapAdvisoryLock = 0x414E4452454A41;

    public Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default) =>
        database.IdentityBootstrapStates.AnyAsync(cancellationToken);

    public async Task<bool> CanBeginBootstrapAsync(
        HttpRequest request,
        string suppliedToken,
        CancellationToken cancellationToken = default)
    {
        if (!IsAcceptedRelyingPartyRequest(request, options.Value)
            || await IsInitializedAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            return await bootstrapTokenVerifier.VerifyAsync(suppliedToken, cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException)
        {
            return false;
        }
    }

    public async Task<BootstrapIdentityResult> CompleteBootstrapAsync(
        HttpRequest request,
        string suppliedToken,
        string tenantName,
        string userDisplayName,
        Guid credentialUserId,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentNullException.ThrowIfNull(passkey);
        if (credentialUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A reserved credential user ID is required.",
                nameof(credentialUserId));
        }

        if (!await CanBeginBootstrapAsync(request, suppliedToken, cancellationToken))
        {
            throw new InvalidOperationException("Identity bootstrap requirements were not satisfied.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await AcquireBootstrapLockAsync(cancellationToken);

        if (await database.IdentityBootstrapStates.AnyAsync(cancellationToken)
            || await database.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken)
            || await database.Memberships.IgnoreQueryFilters()
                .AnyAsync(
                    membership => membership.Role == MembershipRole.Owner,
                    cancellationToken))
        {
            throw new InvalidOperationException("Identity bootstrap requirements were not satisfied.");
        }

        var existingCredential = await userManager.FindByPasskeyIdAsync(passkey.CredentialId);
        var existingCredentialUser = await userManager.FindByIdAsync(
            credentialUserId.ToString("D"));
        if (existingCredential is not null || existingCredentialUser is not null)
        {
            throw new InvalidOperationException("The passkey credential is already registered.");
        }

        var now = timeProvider.GetUtcNow();
        var tenantId = TenantId.New();
        var appUserId = AppUserId.New();
        var principalId = PrincipalId.New();
        tenantContext.Set(new(
            tenantId,
            appUserId,
            principalId,
            "identity-bootstrap"));

        var normalizedTenantName = NormalizeTenantName(tenantName);
        var displayName = NormalizeDisplayName(userDisplayName);
        database.AddRange(
            new Tenant(tenantId, normalizedTenantName, tenantName.Trim(), "local"),
            new AppUser(appUserId, displayName),
            new Principal(principalId, tenantId, appUserId, displayName),
            new Membership(
                MembershipId.New(),
                tenantId,
                appUserId,
                principalId,
                MembershipRole.Owner));

        var credentialUser = new AspNetIdentityUser
        {
            Id = credentialUserId,
            AppUserId = appUserId,
            UserName = $"owner-{appUserId.Value:N}",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        var createResult = await userManager.CreateAsync(credentialUser);
        ThrowIfIdentityFailed(createResult);

        var currentPasskeys = await userManager.GetPasskeysAsync(credentialUser);
        IdentityCredentialPolicy.EnsureCanRegisterPasskey(
            currentPasskeys.Count,
            options.Value.MaximumPasskeysPerUser,
            credentialAlreadyRegistered: false);
        passkey.Name = "Primary passkey";
        ThrowIfIdentityFailed(
            await userManager.AddOrUpdatePasskeyAsync(credentialUser, passkey));

        var recoveryCodes = AddReplacementRecoveryCodes(credentialUser.Id, now);
        database.AddRange(
            new IdentityBootstrapState(credentialUser.Id, now),
            new IdentitySecurityAuditRecord(
                Guid.CreateVersion7(),
                credentialUser.Id,
                "bootstrap",
                succeeded: true,
                now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(credentialUser, recoveryCodes);
    }

    public async Task<RecoveryStartResult?> BeginRecoveryAsync(
        string? recoveryCode,
        CancellationToken cancellationToken = default)
    {
        if (!IsPlausibleRecoveryCode(recoveryCode))
        {
            return null;
        }

        var normalizedCode = recoveryCode.AsSpan().Trim().ToString();
        var now = timeProvider.GetUtcNow();
        var lookupHash = ComputeLookupHash(normalizedCode);
        try
        {
            var stored = await database.IdentityRecoveryCodes
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.LookupHash == lookupHash
                        && candidate.ConsumedAt == null
                        && candidate.ExpiresAt > now,
                    cancellationToken);
            if (stored is null || !VerifyRecoveryCode(stored, normalizedCode))
            {
                database.IdentitySecurityAuditRecords.Add(
                    new IdentitySecurityAuditRecord(
                        Guid.CreateVersion7(),
                        null,
                        "recovery-start",
                        succeeded: false,
                        now));
                await database.SaveChangesAsync(cancellationToken);
                return null;
            }

            var recentFailures = await database.IdentitySecurityAuditRecords.CountAsync(
                audit =>
                    audit.UserId == stored.UserId
                    && audit.Operation == "recovery-start"
                    && !audit.Succeeded
                    && audit.OccurredAt >= now - options.Value.RecoveryRateLimitWindow,
                cancellationToken);
            if (recentFailures >= options.Value.RecoveryRateLimitAttempts)
            {
                return null;
            }

            var user = await userManager.FindByIdAsync(stored.UserId.ToString("D"));
            if (user is null)
            {
                return null;
            }

            var appUser = await database.AppUsers
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == user.AppUserId, cancellationToken);
            database.IdentitySecurityAuditRecords.Add(
                new IdentitySecurityAuditRecord(
                    Guid.CreateVersion7(),
                    user.Id,
                    "recovery-start",
                    succeeded: true,
                    now));
            await database.SaveChangesAsync(cancellationToken);
            return new(
                stored.Id,
                stored.UserId,
                user.SecurityStamp ?? string.Empty,
                user.UserName ?? $"user-{user.Id:N}",
                appUser.DisplayName);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(lookupHash);
        }
    }

    public static bool IsPlausibleRecoveryCode(string? value)
    {
        if (value is null
            || value.Length is < RecoveryCodeMinimumLength
                or > RecoveryCodeMaximumLength)
        {
            return false;
        }

        var trimmed = value.AsSpan().Trim();
        return trimmed.Length is >= RecoveryCodeMinimumLength
            and <= RecoveryCodeMaximumLength;
    }

    public async Task<RecoveryCompletionResult> CompleteRecoveryAsync(
        RecoveryStartResult recovery,
        UserPasskeyInfo newPasskey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(newPasskey);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var code = await database.IdentityRecoveryCodes
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == recovery.RecoveryCodeId
                    && candidate.UserId == recovery.UserId
                    && candidate.ConsumedAt == null
                    && candidate.ExpiresAt > now,
                cancellationToken)
            ?? throw new InvalidOperationException("Recovery requirements were not satisfied.");
        var user = await userManager.FindByIdAsync(recovery.UserId.ToString("D"))
            ?? throw new InvalidOperationException("Recovery requirements were not satisfied.");
        if (!string.Equals(user.SecurityStamp, recovery.SecurityStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Recovery requirements were not satisfied.");
        }

        var collision = await userManager.FindByPasskeyIdAsync(newPasskey.CredentialId);
        if (collision is not null && collision.Id != user.Id)
        {
            throw new InvalidOperationException("The passkey credential is already registered.");
        }

        var existingPasskeys = await userManager.GetPasskeysAsync(user);
        foreach (var existingPasskey in existingPasskeys)
        {
            ThrowIfIdentityFailed(
                await userManager.RemovePasskeyAsync(user, existingPasskey.CredentialId));
        }

        IdentityCredentialPolicy.EnsureCanRegisterPasskey(
            currentPasskeyCount: 0,
            options.Value.MaximumPasskeysPerUser,
            credentialAlreadyRegistered: false);
        newPasskey.Name = "Recovery passkey";
        ThrowIfIdentityFailed(await userManager.AddOrUpdatePasskeyAsync(user, newPasskey));

        code.Consume(now);
        var remainingCodes = await database.IdentityRecoveryCodes
            .Where(candidate =>
                candidate.UserId == user.Id
                && candidate.Id != code.Id
                && candidate.ConsumedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var remaining in remainingCodes)
        {
            remaining.Consume(now);
        }

        var replacements = AddReplacementRecoveryCodes(user.Id, now);
        ThrowIfIdentityFailed(await userManager.UpdateSecurityStampAsync(user));
        database.IdentitySecurityAuditRecords.Add(
            new IdentitySecurityAuditRecord(
                Guid.CreateVersion7(),
                user.Id,
                "recovery-complete",
                succeeded: true,
                now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(replacements);
    }

    public async Task AuditRecoveryFailureAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        database.ChangeTracker.Clear();
        database.IdentitySecurityAuditRecords.Add(
            new IdentitySecurityAuditRecord(
                Guid.CreateVersion7(),
                userId,
                "recovery-complete",
                succeeded: false,
                timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokePasskeyAsync(
        AspNetIdentityUser user,
        byte[] credentialId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await AcquireUserMutationLockAsync(user.Id, cancellationToken);
        var passkeys = await userManager.GetPasskeysAsync(user);
        var recoveryCount = await database.IdentityRecoveryCodes.CountAsync(
            code =>
                code.UserId == user.Id
                && code.ConsumedAt == null
                && code.ExpiresAt > timeProvider.GetUtcNow(),
            cancellationToken);
        IdentityCredentialPolicy.EnsureCanRevokePasskey(
            new(passkeys.Count, ExternalIdentityCount: 0, recoveryCount));
        ThrowIfIdentityFailed(await userManager.RemovePasskeyAsync(user, credentialId));
        database.IdentitySecurityAuditRecords.Add(
            new IdentitySecurityAuditRecord(
                Guid.CreateVersion7(),
                user.Id,
                "passkey-revoke",
                succeeded: true,
                timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RegisterPasskeyAsync(
        AspNetIdentityUser user,
        UserPasskeyInfo passkey,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(passkey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await AcquireUserMutationLockAsync(user.Id, cancellationToken);
        var existing = await userManager.FindByPasskeyIdAsync(passkey.CredentialId);
        var passkeys = await userManager.GetPasskeysAsync(user);
        IdentityCredentialPolicy.EnsureCanRegisterPasskey(
            passkeys.Count,
            options.Value.MaximumPasskeysPerUser,
            existing is not null);
        passkey.Name = NormalizeDeviceName(deviceName);
        ThrowIfIdentityFailed(await userManager.AddOrUpdatePasskeyAsync(user, passkey));
        database.IdentitySecurityAuditRecords.Add(
            new IdentitySecurityAuditRecord(
                Guid.CreateVersion7(),
                user.Id,
                "passkey-register",
                succeeded: true,
                timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static bool IsAcceptedRelyingPartyRequest(
        HttpRequest request,
        LocalIdentityOptions configured)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configured);
        if (!request.IsHttps || !request.Headers.TryGetValue("Origin", out var originHeader))
        {
            return false;
        }

        var requestOrigin = $"https://{request.Host.Value}";
        if (!Uri.TryCreate(requestOrigin, UriKind.Absolute, out var requestOriginUri)
            || !OriginHostMatchesRelyingParty(
                requestOriginUri.Host,
                configured.RelyingPartyId))
        {
            return false;
        }

        return configured.AllowedOrigins.Any(
            allowed =>
                string.Equals(
                    NormalizeOrigin(allowed),
                    NormalizeOrigin(requestOrigin),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    NormalizeOrigin(allowed),
                    NormalizeOrigin(originHeader.ToString()),
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool OriginHostMatchesRelyingParty(string host, string relyingPartyId) =>
        host.Equals(relyingPartyId, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith($".{relyingPartyId}", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
                .TrimEnd('/')
            : string.Empty;

    private async Task AcquireBootstrapLockAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(
                database.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            await database.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_xact_lock({BootstrapAdvisoryLock})",
                cancellationToken);
        }
    }

    private async Task AcquireUserMutationLockAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                database.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return;
        }

        var userBytes = userId.ToByteArray();
        var digest = SHA256.HashData(userBytes);
        try
        {
            var lockKey = BinaryPrimitives.ReadInt64LittleEndian(digest);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(userBytes);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private List<string> AddReplacementRecoveryCodes(
        Guid userId,
        DateTimeOffset now)
    {
        var plaintextCodes = new List<string>(options.Value.RecoveryCodeCount);
        for (var index = 0; index < options.Value.RecoveryCodeCount; index++)
        {
            var random = RandomNumberGenerator.GetBytes(32);
            var code = WebEncoders.Base64UrlEncode(random);
            CryptographicOperations.ZeroMemory(random);
            var lookupHash = ComputeLookupHash(code);
            var salt = RandomNumberGenerator.GetBytes(16);
            var verificationHash = ComputeVerificationHash(code, salt);
            database.IdentityRecoveryCodes.Add(
                new(
                    Guid.CreateVersion7(),
                    userId,
                    lookupHash,
                    salt,
                    verificationHash,
                    now,
                    now + options.Value.RecoveryCodeLifetime));
            plaintextCodes.Add(code);
        }

        return plaintextCodes;
    }

    private static bool VerifyRecoveryCode(IdentityRecoveryCode stored, string suppliedCode)
    {
        var calculated = ComputeVerificationHash(suppliedCode, stored.Salt);
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                calculated,
                stored.VerificationHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(calculated);
        }
    }

    private static byte[] ComputeLookupHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Trim());
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static byte[] ComputeVerificationHash(string value, byte[] salt)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Trim());
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                bytes,
                salt,
                RecoveryHashIterations,
                HashAlgorithmName.SHA256,
                32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string NormalizeTenantName(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return normalized;
    }

    private static string NormalizeDeviceName(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return normalized;
    }

    private static void ThrowIfIdentityFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("The identity operation could not be completed.");
        }
    }
}
