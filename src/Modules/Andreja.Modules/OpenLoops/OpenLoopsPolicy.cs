using Andreja.Modules.Identity;

namespace Andreja.Modules.OpenLoops;

public static class OpenLoopsPolicy
{
    public const string Purpose = "task.capture";
    public const string ProposeCapability = "tasks.propose";
    public const string ProposeOperation = "propose";
    public const string TaskDataClass = "tasks";
    public const string ResourceReference = "open-loops/tasks";
    public const string ManageCapability = "tasks.manage";

    public static void Require(TenantPrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId.Value == Guid.Empty
            || context.AppUserId.Value == Guid.Empty
            || context.PrincipalId.Value == Guid.Empty
            || !string.Equals(context.Purpose, Purpose, StringComparison.Ordinal))
        {
            throw new IdentityAccessDeniedException("Task access is not authorized.");
        }
    }
}
