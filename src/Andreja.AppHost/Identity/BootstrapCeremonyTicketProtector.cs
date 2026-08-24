using System.Security.Cryptography;
using System.Text.Json;
using Andreja.Adapters.Identity.AspNetCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Andreja.AppHost.Identity;

internal sealed record BootstrapCeremonyTicket(
    Guid CredentialUserId,
    string Token,
    string TenantName,
    string UserDisplayName,
    string Challenge);

internal sealed class BootstrapCeremonyTicketProtector
{
    private const string ProtectionPurpose =
        "Andreja.Identity.BootstrapCeremony.v1";
    private readonly ITimeLimitedDataProtector protector;
    private readonly IOptions<LocalIdentityOptions> options;

    public BootstrapCeremonyTicketProtector(
        IDataProtectionProvider dataProtection,
        IOptions<LocalIdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        this.options = options;
        protector = dataProtection
            .CreateProtector(ProtectionPurpose)
            .ToTimeLimitedDataProtector();
    }

    public string Protect(BootstrapCeremonyTicket ticket) =>
        ProtectUntil(
            ticket,
            DateTimeOffset.UtcNow + options.Value.BootstrapCeremonyLifetime);

    public TimeSpan Lifetime => options.Value.BootstrapCeremonyLifetime;

    public bool TryUnprotect(
        string protectedTicket,
        out BootstrapCeremonyTicket? ticket)
    {
        ticket = null;
        if (string.IsNullOrWhiteSpace(protectedTicket))
        {
            return false;
        }

        try
        {
            ticket = JsonSerializer.Deserialize<BootstrapCeremonyTicket>(
                protector.Unprotect(protectedTicket));
            return IsValid(ticket);
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException)
        {
            return false;
        }
    }

    internal string ProtectUntil(
        BootstrapCeremonyTicket ticket,
        DateTimeOffset expiration)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return protector.Protect(JsonSerializer.Serialize(ticket), expiration);
    }

    private static bool IsValid(BootstrapCeremonyTicket? ticket) =>
        ticket is
        {
            CredentialUserId: var userId,
            Token.Length: > 0,
            TenantName.Length: > 0,
            UserDisplayName.Length: > 0,
            Challenge.Length: > 0 and <= 1024,
        }
        && userId != Guid.Empty;
}
