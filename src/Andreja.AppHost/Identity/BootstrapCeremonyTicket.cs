namespace Andreja.AppHost.Identity;

internal sealed record BootstrapCeremonyTicket(
    Guid CredentialUserId,
    string Token,
    string TenantName,
    string UserDisplayName,
    string Challenge);
