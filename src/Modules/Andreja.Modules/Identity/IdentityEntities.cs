namespace Andreja.Modules.Identity;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Deleting = 3,
}

public enum MembershipRole
{
    Owner = 1,
    Member = 2,
}

public enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
}

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Tenant(
        TenantId id,
        string normalizedName,
        string displayName,
        string dataResidency,
        string plan = "SelfHosted",
        TenantStatus status = TenantStatus.Active)
    {
        Id = RequireId(id, nameof(id));
        NormalizedName = RequireText(normalizedName, nameof(normalizedName), 128);
        DisplayName = RequireText(displayName, nameof(displayName), 200);
        DataResidency = RequireText(dataResidency, nameof(dataResidency), 64);
        Plan = RequireText(plan, nameof(plan), 64);
        Status = status;
    }

    public TenantId Id { get; private set; }

    public string NormalizedName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string DataResidency { get; private set; } = string.Empty;

    public string Plan { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    private static TenantId RequireId(TenantId value, string parameterName) =>
        value.Value == Guid.Empty ? throw new ArgumentException("An ID is required.", parameterName) : value;

    private static string RequireText(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var result = value.Trim();
        return result.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(parameterName, $"Maximum length is {maximumLength}.")
            : result;
    }
}

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

public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
    }

    public ExternalIdentity(
        ExternalIdentityId id,
        AppUserId appUserId,
        string issuer,
        string subject)
    {
        if (id.Value == Guid.Empty || appUserId.Value == Guid.Empty)
        {
            throw new ArgumentException("Identity IDs are required.");
        }

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || issuerUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(issuerUri.Query)
            || !string.IsNullOrEmpty(issuerUri.Fragment))
        {
            throw new ArgumentException(
                "Issuer must be an absolute HTTPS URI without query or fragment.",
                nameof(issuer));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var normalizedSubject = subject.Trim();
        if (normalizedSubject.Length > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(subject), "Maximum length is 512.");
        }

        Id = id;
        AppUserId = appUserId;
        Issuer = issuerUri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped).TrimEnd('/');
        Subject = normalizedSubject;
    }

    public ExternalIdentityId Id { get; private set; }

    public AppUserId AppUserId { get; private set; }

    public string Issuer { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
}

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
