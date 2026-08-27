using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;

namespace Andreja.Adapters.PostgreSql;

public sealed class AspNetIdentityUser : IdentityUser<Guid>
{
    public AppUserId AppUserId { get; set; }
}
