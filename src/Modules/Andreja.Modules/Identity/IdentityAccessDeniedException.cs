namespace Andreja.Modules.Identity;

public sealed class IdentityAccessDeniedException(string message) : InvalidOperationException(message);
