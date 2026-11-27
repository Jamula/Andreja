namespace Andreja.AppHost.Identity;

public static partial class LocalAccountEndpoints
{
    public sealed record BootstrapOptionsRequest(
        string Token,
        string TenantName,
        string UserDisplayName);

    public sealed record BootstrapCompleteRequest(
        string CredentialJson,
        string? ReturnUrl);

    public sealed record SignInRequest(string CredentialJson, string? ReturnUrl);

    public sealed record RecoveryOptionsRequest(string RecoveryCode);

    public sealed record CredentialRequest(string CredentialJson);

    public sealed record RegistrationOptionsRequest(string DeviceName);

    public sealed record RegistrationCompleteRequest(
        string DeviceName,
        string CredentialJson);

    public sealed record RevokePasskeyRequest(string CredentialId);

    public sealed record IdentityCompletionDto(
        string RedirectUrl,
        IReadOnlyList<string>? RecoveryCodes);

    public sealed record PasskeyDto(
        string CredentialId,
        string Name,
        DateTimeOffset CreatedAt,
        bool IsBackedUp);
}
