namespace Andreja.Modules.Identity;

public sealed record ContactProjection(ContactId Id, string DisplayName, PrincipalId? LinkedPrincipalId);
