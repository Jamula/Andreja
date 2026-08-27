namespace Andreja.Modules.Identity;

public sealed record BootstrapRequest(
    string Token,
    Uri Origin,
    string TenantName,
    string UserDisplayName,
    string PasskeyAttestation);

public sealed record BootstrapResult(
    TenantId TenantId,
    AppUserId AppUserId,
    PrincipalId PrincipalId);

public interface IIdentityBootstrapService
{
    Task<BootstrapResult> BootstrapAsync(
        BootstrapRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIdentityBootstrapTransaction
{
    Task<BootstrapResult> CreateInitialIdentityAsync(
        TenantId tenantId,
        AppUserId appUserId,
        PrincipalId principalId,
        string tenantName,
        string userDisplayName,
        CancellationToken cancellationToken = default);
}

public sealed record PasskeyDescriptor(
    string CredentialId,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

public interface IPasskeyCredentialService
{
    Task RegisterAsync(
        AppUserId userId,
        string attestation,
        string deviceName,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        AppUserId userId,
        string credentialId,
        CancellationToken cancellationToken = default);
}

public sealed record RecoveryRequest(string RecoveryCode, string NewPasskeyAttestation);

public sealed record RecoveryResult(IReadOnlyList<string> ReplacementRecoveryCodes);

public interface IRecoveryService
{
    Task<RecoveryResult> RecoverAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryAttemptLimiter
{
    Task<bool> TryAcquireAsync(
        AppUserId userId,
        CancellationToken cancellationToken = default);
}

public interface IUserSessionRevoker
{
    Task RevokeAllAsync(
        AppUserId userId,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryCodeStore
{
    Task<IReadOnlyList<string>> ConsumeAndRotateHashedCodesAsync(
        AppUserId userId,
        string presentedCode,
        int replacementCount,
        CancellationToken cancellationToken = default);
}

public sealed record IdentitySecurityAuditEvent(
    AppUserId AppUserId,
    string Operation,
    bool Succeeded,
    DateTimeOffset OccurredAt);

public interface IIdentitySecurityAudit
{
    Task WriteAsync(
        IdentitySecurityAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalIdentityLinkRequest(
    Uri Issuer,
    string Subject,
    string ProviderProof,
    DateTimeOffset AuthenticatedAt);

public interface IExternalIdentityLinkService
{
    Task<ExternalIdentityId> LinkAsync(
        AppUserId userId,
        ExternalIdentityLinkRequest request,
        CancellationToken cancellationToken = default);
}

public readonly record struct AuthenticationPathState(
    int PasskeyCount,
    int ExternalIdentityCount,
    int UnusedRecoveryCodeCount);

public static class IdentityCredentialPolicy
{
    public static void EnsureCanBootstrap(
        bool identityAlreadyInitialized,
        bool isHttps,
        bool originAccepted,
        bool bootstrapTokenVerified)
    {
        if (identityAlreadyInitialized
            || !isHttps
            || !originAccepted
            || !bootstrapTokenVerified)
        {
            throw new InvalidOperationException("Identity bootstrap requirements were not satisfied.");
        }
    }

    public static void EnsureCanRegisterPasskey(
        int currentPasskeyCount,
        int maximumPasskeys,
        bool credentialAlreadyRegistered)
    {
        if (credentialAlreadyRegistered)
        {
            throw new InvalidOperationException("The passkey credential is already registered.");
        }

        if (maximumPasskeys < 1 || currentPasskeyCount >= maximumPasskeys)
        {
            throw new InvalidOperationException("The passkey device limit has been reached.");
        }
    }

    public static void EnsureCanRevokePasskey(AuthenticationPathState paths)
    {
        if (paths.PasskeyCount < 1)
        {
            throw new InvalidOperationException("No registered passkey can be revoked.");
        }

        var remainingPaths =
            paths.PasskeyCount - 1 + paths.ExternalIdentityCount + paths.UnusedRecoveryCodeCount;
        if (remainingPaths < 1)
        {
            throw new InvalidOperationException("The last usable sign-in or recovery path cannot be removed.");
        }
    }

    public static void EnsureCanLinkExternalIdentity(
        ExternalIdentityLinkRequest request,
        DateTimeOffset now,
        TimeSpan recentAuthenticationWindow)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderProof);

        if (request.Issuer.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("External identity issuers must use HTTPS.");
        }

        if (request.AuthenticatedAt > now
            || now - request.AuthenticatedAt > recentAuthenticationWindow)
        {
            throw new InvalidOperationException("Recent authentication is required before identity linking.");
        }
    }

    public static void EnsureCanRecover(
        bool rateLimitAcquired,
        bool recoveryCodeVerified,
        string newPasskeyAttestation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasskeyAttestation);
        if (!rateLimitAcquired || !recoveryCodeVerified)
        {
            throw new InvalidOperationException("Recovery requirements were not satisfied.");
        }
    }
}
