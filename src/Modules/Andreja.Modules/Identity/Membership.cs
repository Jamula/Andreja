namespace Andreja.Modules.Identity;

public sealed class Membership
{
    private Membership()
    {
    }

    public Membership(
        MembershipId id,
        TenantId tenantId,
        AppUserId appUserId,
        PrincipalId principalId,
        MembershipRole role,
        MembershipStatus status = MembershipStatus.Active)
    {
        if (id.Value == Guid.Empty
            || tenantId.Value == Guid.Empty
            || appUserId.Value == Guid.Empty
            || principalId.Value == Guid.Empty)
        {
            throw new ArgumentException("IDs are required.");
        }

        Id = id;
        TenantId = tenantId;
        AppUserId = appUserId;
        PrincipalId = principalId;
        Role = role;
        Status = status;
    }

    public MembershipId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public AppUserId AppUserId { get; private set; }

    public PrincipalId PrincipalId { get; private set; }

    public MembershipRole Role { get; private set; }

    public MembershipStatus Status { get; private set; }
}
