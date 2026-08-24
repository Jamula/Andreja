using Andreja.Modules.Identity;
using Andreja.Modules.OpenLoops;
using Andreja.Adapters.PostgreSql;
using Andreja.Api.Contracts.OpenLoops;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Andreja.AppHost.OpenLoops;

public static class AndrejaClaimTypes
{
    public const string TenantId = "andreja:tenant_id";
    public const string AppUserId = "andreja:app_user_id";
    public const string PrincipalId = "andreja:principal_id";
}

public static class TaskRequestContext
{
    public static TenantPrincipalContext Require(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var resolved = httpContext.RequestServices?
            .GetService<ITenantPrincipalContextAccessor>()?.Current;
        if (resolved is not null)
        {
            return resolved;
        }

        if (httpContext.User.Identity?.IsAuthenticated != true
            || !TryReadGuid(httpContext.User, AndrejaClaimTypes.TenantId, out var tenantId)
            || !TryReadGuid(httpContext.User, AndrejaClaimTypes.AppUserId, out var appUserId)
            || !TryReadGuid(httpContext.User, AndrejaClaimTypes.PrincipalId, out var principalId))
        {
            throw new IdentityAccessDeniedException(
                "A complete authenticated tenant and principal context is required.");
        }

        return new(
            new(tenantId),
            new(appUserId),
            new(principalId),
            OpenLoopsPolicy.Purpose);
    }

    public static async Task ResolveAsync(HttpContext httpContext, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(next);

        if (httpContext.Request.Path.StartsWithSegments("/api/v1/open-loops")
            || httpContext.Request.Path.StartsWithSegments("/api/v1/security/antiforgery"))
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                await next(httpContext);
                return;
            }

            try
            {
                TenantPrincipalContext context;
                try
                {
                    context = Require(httpContext);
                }
                catch (IdentityAccessDeniedException)
                {
                    context = await ResolveIdentityMembershipAsync(httpContext);
                }

                httpContext.RequestServices.GetRequiredService<ScopedTenantPrincipalContext>().Set(context);
            }
            catch (IdentityAccessDeniedException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsJsonAsync(
                    new ApiErrorDto(
                        "identity-context-required",
                        "A valid tenant membership is required."),
                    httpContext.RequestAborted);
                return;
            }
        }

        await next(httpContext);
    }

    private static async Task<TenantPrincipalContext> ResolveIdentityMembershipAsync(
        HttpContext httpContext)
    {
        var database = httpContext.RequestServices.GetService<AndrejaIdentityDbContext>()
            ?? throw new IdentityAccessDeniedException(
                "The authenticated identity could not be resolved.");
        var nameIdentifiers = httpContext.User.FindAll(ClaimTypes.NameIdentifier)
            .Select(claim => claim.Value)
            .ToArray();
        if (nameIdentifiers.Length != 1
            || !Guid.TryParseExact(nameIdentifiers[0], "D", out var credentialUserId)
            || credentialUserId == Guid.Empty)
        {
            throw new IdentityAccessDeniedException(
                "The authenticated identity could not be resolved.");
        }

        var identityUser = await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == credentialUserId,
                httpContext.RequestAborted)
            ?? throw new IdentityAccessDeniedException(
                "The authenticated identity could not be resolved.");
        var memberships = database.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership =>
                membership.AppUserId == identityUser.AppUserId
                && membership.Status == MembershipStatus.Active);

        var tenantClaim = httpContext.User.FindFirst(AndrejaClaimTypes.TenantId)?.Value;
        if (Guid.TryParseExact(tenantClaim, "D", out var selectedTenantId))
        {
            memberships = memberships.Where(
                membership => membership.TenantId == new TenantId(selectedTenantId));
        }

        var membership = await memberships.SingleOrDefaultAsync(httpContext.RequestAborted)
            ?? throw new IdentityAccessDeniedException(
                "A single active tenant membership is required.");
        return new(
            membership.TenantId,
            membership.AppUserId,
            membership.PrincipalId,
            OpenLoopsPolicy.Purpose);
    }

    private static bool TryReadGuid(ClaimsPrincipal principal, string claimType, out Guid value)
    {
        var values = principal.FindAll(claimType).Select(claim => claim.Value).ToArray();
        value = Guid.Empty;
        return values.Length == 1
            && Guid.TryParseExact(values[0], "D", out value)
            && value != Guid.Empty;
    }
}
