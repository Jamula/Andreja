using Andreja.AppHost.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Andreja.AppHost.OpenLoops;

public static class CircuitDelegation
{
    public const string AuthenticationScheme = "Andreja.CircuitDelegation";
    public const string AuthorizationScheme = "Andreja-Circuit";
    public const string OpenLoopsAudience = "andreja.internal.open-loops.v1";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);
}

public sealed record CircuitDelegationValidation(
    bool Succeeded,
    ClaimsPrincipal? Principal,
    string? FailureCode);

public interface ICircuitDelegationTokenService
{
    string Issue(ClaimsPrincipal principal, string audience);

    CircuitDelegationValidation ValidateAndConsume(string token, string audience);
}

public sealed class CircuitDelegationTokenService(
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : ICircuitDelegationTokenService
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "Andreja.AppHost.CircuitDelegation",
        "v1");
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> consumedNonces = [];

    public string Issue(ClaimsPrincipal principal, string audience)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException(
                "An authenticated circuit principal is required.");
        }

        var tenantId = RequireSingleGuid(principal, AndrejaClaimTypes.TenantId);
        var appUserId = RequireSingleGuid(principal, AndrejaClaimTypes.AppUserId);
        var principalId = RequireSingleGuid(principal, AndrejaClaimTypes.PrincipalId);
        var subject = RequireSingleValue(principal, ClaimTypes.NameIdentifier);
        var now = timeProvider.GetUtcNow();
        var payload = new DelegationPayload(
            CurrentVersion,
            audience,
            tenantId,
            appUserId,
            principalId,
            subject,
            now,
            now.Add(CircuitDelegation.TokenLifetime),
            Guid.CreateVersion7());
        return Convert.ToBase64String(
            protector.Protect(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions)));
    }

    public CircuitDelegationValidation ValidateAndConsume(string token, string audience)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(audience))
        {
            return Failed("delegation-token-missing");
        }

        DelegationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DelegationPayload>(
                protector.Unprotect(Convert.FromBase64String(token)),
                JsonOptions);
        }
        catch (Exception exception) when (
            exception is System.Security.Cryptography.CryptographicException
                or FormatException
                or JsonException)
        {
            return Failed("delegation-token-invalid");
        }

        var now = timeProvider.GetUtcNow();
        RemoveExpiredNonces(now);
        if (payload is null
            || payload.Version != CurrentVersion
            || !string.Equals(payload.Audience, audience, StringComparison.Ordinal)
            || payload.TenantId == Guid.Empty
            || payload.AppUserId == Guid.Empty
            || payload.PrincipalId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Subject)
            || payload.Nonce == Guid.Empty)
        {
            return Failed("delegation-token-invalid");
        }

        if (payload.IssuedAt > now.AddSeconds(5) || payload.ExpiresAt <= now)
        {
            return Failed("delegation-token-expired");
        }

        if (payload.ExpiresAt - payload.IssuedAt > CircuitDelegation.TokenLifetime
            || !consumedNonces.TryAdd(payload.Nonce, payload.ExpiresAt))
        {
            return Failed("delegation-token-replayed");
        }

        var identity = new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, payload.Subject),
                new(AndrejaClaimTypes.TenantId, payload.TenantId.ToString("D")),
                new(AndrejaClaimTypes.AppUserId, payload.AppUserId.ToString("D")),
                new(AndrejaClaimTypes.PrincipalId, payload.PrincipalId.ToString("D")),
            ],
            CircuitDelegation.AuthenticationScheme);
        return new(true, new ClaimsPrincipal(identity), null);
    }

    private static Guid RequireSingleGuid(ClaimsPrincipal principal, string type)
    {
        var value = RequireSingleValue(principal, type);
        return Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new InvalidOperationException(
                $"The circuit principal claim '{type}' is invalid.");
    }

    private static string RequireSingleValue(ClaimsPrincipal principal, string type)
    {
        var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
        return values.Length == 1 && !string.IsNullOrWhiteSpace(values[0])
            ? values[0]
            : throw new InvalidOperationException(
                $"The circuit principal requires exactly one '{type}' claim.");
    }

    private void RemoveExpiredNonces(DateTimeOffset now)
    {
        foreach (var consumed in consumedNonces.Where(entry => entry.Value <= now))
        {
            consumedNonces.TryRemove(consumed.Key, out _);
        }
    }

    private static CircuitDelegationValidation Failed(string code) =>
        new(false, null, code);

    private sealed record DelegationPayload(
        int Version,
        string Audience,
        Guid TenantId,
        Guid AppUserId,
        Guid PrincipalId,
        string Subject,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt,
        Guid Nonce);
}

public sealed class CircuitDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICircuitDelegationTokenService tokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(
                Request.Headers.Authorization,
                out var authorization)
            || !string.Equals(
                authorization.Scheme,
                CircuitDelegation.AuthorizationScheme,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var validation = tokenService.ValidateAndConsume(
            authorization.Parameter,
            CircuitDelegation.OpenLoopsAudience);
        return Task.FromResult(
            validation.Succeeded && validation.Principal is not null
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    validation.Principal,
                    CircuitDelegation.AuthenticationScheme))
                : AuthenticateResult.Fail(
                    validation.FailureCode ?? "delegation-token-invalid"));
    }
}
