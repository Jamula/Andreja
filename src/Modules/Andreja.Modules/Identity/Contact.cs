namespace Andreja.Modules.Identity;

public sealed class Contact
{
    private Contact()
    {
    }

    public Contact(
        ContactId id,
        TenantId tenantId,
        string normalizedName,
        string displayName,
        PrincipalId? linkedPrincipalId = null)
    {
        if (id.Value == Guid.Empty || tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException("IDs are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        TenantId = tenantId;
        NormalizedName = normalizedName.Trim();
        DisplayName = displayName.Trim();
        LinkedPrincipalId = linkedPrincipalId;
    }

    public ContactId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string NormalizedName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public PrincipalId? LinkedPrincipalId { get; private set; }
}
