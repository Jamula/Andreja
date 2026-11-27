namespace Andreja.Adapters.Identity.AspNetCore;

public interface IBootstrapTokenVerifier
{
    ValueTask<bool> VerifyAsync(string suppliedToken, CancellationToken cancellationToken = default);
}
