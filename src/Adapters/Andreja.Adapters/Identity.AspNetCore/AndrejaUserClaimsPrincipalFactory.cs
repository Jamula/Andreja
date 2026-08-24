using System.Security.Claims;
using Andreja.Adapters.PostgreSql;
using Andreja.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Andreja.Adapters.Identity.AspNetCore;

public static class LocalIdentityClaimTypes
{
    public const string TenantId = "andreja:tenant_id";
    public const string AppUserId = "andreja:app_user_id";
    public const string PrincipalId = "andreja:principal_id";
}

public sealed class AndrejaUserClaimsPrincipalFactory(
    UserManager<AspNetIdentityUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options,
    AndrejaIdentityDbContext database)
    : UserClaimsPrincipalFactory<AspNetIdentityUser, IdentityRole<Guid>>(
        userManager,
        roleManager,
        options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
        AspNetIdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var membership = await database.Memberships
            .IgnoreQueryFilters()
            .Where(candidate =>
                candidate.AppUserId == user.AppUserId
                && candidate.Status == MembershipStatus.Active)
            .Select(candidate => new
            {
                candidate.TenantId,
                candidate.PrincipalId,
            })
            .SingleAsync();

        identity.AddClaims(
        [
            new(LocalIdentityClaimTypes.TenantId, membership.TenantId.Value.ToString("D")),
            new(LocalIdentityClaimTypes.AppUserId, user.AppUserId.Value.ToString("D")),
            new(LocalIdentityClaimTypes.PrincipalId, membership.PrincipalId.Value.ToString("D")),
        ]);
        return identity;
    }
}
