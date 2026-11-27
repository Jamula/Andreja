using Andreja.Adapters.PostgreSql;
using Microsoft.AspNetCore.Identity;

namespace Andreja.Adapters.Identity.AspNetCore;

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
