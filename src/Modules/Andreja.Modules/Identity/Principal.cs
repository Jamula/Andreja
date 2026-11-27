namespace Andreja.Modules.Identity;

public sealed class Principal
{
    private Principal()
    {
    }

    public Principal(PrincipalId id, TenantId tenantId, AppUserId appUserId, string displayName)
    {
        EnsureIds(id.Value, tenantId.Value, appUserId.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        TenantId = tenantId;
        AppUserId = appUserId;
        DisplayName = displayName.Trim();
    }

    public PrincipalId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public AppUserId AppUserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    private static void EnsureIds(params Guid[] ids)
    {
        foreach (var id in ids)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("IDs are required.");
            }
        }
    }
}
