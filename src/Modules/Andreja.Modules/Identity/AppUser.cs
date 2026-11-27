namespace Andreja.Modules.Identity;

public sealed class AppUser
{
    private AppUser()
    {
    }

    public AppUser(AppUserId id, string displayName)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("An ID is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        DisplayName = displayName.Trim();
    }

    public AppUserId Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public ExternalIdentityId? PrimaryExternalIdentityId { get; private set; }

    public void SelectPrimaryIdentity(ExternalIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.AppUserId != Id)
        {
            throw new InvalidOperationException("The primary identity must belong to this user.");
        }

        PrimaryExternalIdentityId = identity.Id;
    }
}
