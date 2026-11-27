namespace Andreja.Modules.Identity;

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

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var normalizedSubject = subject.Trim();
        if (normalizedSubject.Length > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(subject), "Maximum length is 512.");
        }

        Id = id;
        AppUserId = appUserId;
        Issuer = NormalizeIssuer(issuer);
        Subject = normalizedSubject;
    }

    public ExternalIdentityId Id { get; private set; }

    public AppUserId AppUserId { get; private set; }

    public string Issuer { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public static string NormalizeIssuer(string issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || issuerUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(issuerUri.Query)
            || !string.IsNullOrEmpty(issuerUri.Fragment))
        {
            throw new ArgumentException(
                "Issuer must be an absolute HTTPS URI without query or fragment.",
                nameof(issuer));
        }

        return issuerUri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped).TrimEnd('/');
    }
}
