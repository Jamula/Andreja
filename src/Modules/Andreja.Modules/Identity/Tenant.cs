namespace Andreja.Modules.Identity;

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
