using Andreja.Adapters.PostgreSql;

namespace Andreja.Adapters.Identity.AspNetCore;

public sealed record BootstrapIdentityResult(
    AspNetIdentityUser User,
    IReadOnlyList<string> RecoveryCodes);
