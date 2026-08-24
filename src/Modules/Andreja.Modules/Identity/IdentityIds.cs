namespace Andreja.Modules.Identity;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.CreateVersion7());
}

public readonly record struct AppUserId(Guid Value)
{
    public static AppUserId New() => new(Guid.CreateVersion7());
}

public readonly record struct ExternalIdentityId(Guid Value)
{
    public static ExternalIdentityId New() => new(Guid.CreateVersion7());
}

public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.CreateVersion7());
}

public readonly record struct PrincipalId(Guid Value)
{
    public static PrincipalId New() => new(Guid.CreateVersion7());
}

public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.CreateVersion7());
}
