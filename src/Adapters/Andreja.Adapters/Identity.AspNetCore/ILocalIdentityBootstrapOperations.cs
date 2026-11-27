using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Andreja.Adapters.Identity.AspNetCore;

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
