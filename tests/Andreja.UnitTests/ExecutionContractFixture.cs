using Andreja.Modules.Channels;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Sharing;
using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.UnitTests;

internal static class ExecutionContractFixture
{
    public static readonly DateTimeOffset Now =
        new(2026, 8, 24, 16, 0, 0, TimeSpan.Zero);

    public static readonly Guid TenantId =
        Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976201");

    public static readonly Guid AppUserId =
        Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976202");

    public static readonly Guid PrincipalId =
        Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976203");

    public static readonly Guid AuthorityPrincipalId =
        Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976204");

    public const string Purpose = "task.capture";
    public const string Capability = "tasks.propose";
    public const string Operation = "propose";
    public const string DataClass = "tasks";
    public const string Resource = "open-loops/tasks";
    public const string ToolName = "open-loops.propose-task";
    public const string ChannelOperationName = "local.tasks.propose";

    public static SkillManifest SkillManifest(
        IReadOnlyList<ToolDefinition>? tools = null) =>
        new(
            "andreja.skill-manifest.v1",
            "local.open-loops",
            "1.0.0",
            "Local Open Loops",
            "A deterministic local contract fixture.",
            ["Productivity"],
            Publisher(),
            Lifecycle(5),
            Permissions(),
            Execution(ToolName),
            DataHandling(),
            HelpSupport(),
            Compatibility(),
            Integrity(),
            ManifestField.NotApplicable<IReadOnlyList<string>>(
                "The local fixture has no channel dependency."),
            tools ??
            [
                new(
                    ToolName,
                    "1.0.0",
                    "Propose a task.",
                    Operation,
                    DataClass,
                    DisclosureLevel.Summary,
                    [new("title", ToolValueKind.Text, true)],
                    [Capability],
                    [Purpose]),
            ]);

    public static ChannelManifest ChannelManifest() =>
        new(
            "andreja.channel-manifest.v1",
            "local.tasks",
            "1.0.0",
            "Local Task Channel",
            "A deterministic channel contract fixture with no external provider.",
            "local-fixture",
            Publisher(),
            Lifecycle(5),
            Permissions(),
            Execution(ChannelOperationName),
            DataHandling(),
            HelpSupport(),
            Compatibility(),
            Integrity(),
            new(
                NotApplicable<string>("No external provider is used."),
                NotApplicable<IReadOnlyList<string>>("No provider account is used."),
                NotApplicable<IReadOnlyList<string>>("No OAuth scopes are requested."),
                NotApplicable<string>("The fixture does not query a provider."),
                NotApplicable<string>("The fixture does not synchronize provider data."),
                NotApplicable<string>("The fixture does not publish externally."),
                NotApplicable<string>("The fixture has no webhook."),
                NotApplicable<string>("The fixture has no change feed."),
                NotApplicable<string>("The fixture has no external cache."),
                NotApplicable<string>("The fixture has no provider cost."),
                NotApplicable<string>("The fixture has no delivery topology.")),
            [
                new(
                    ChannelOperationName,
                    "1.0.0",
                    "Propose a task through a local fixture.",
                    Capability,
                    Operation,
                    DataClass,
                    DisclosureLevel.Summary,
                    [Purpose],
                    [new("title", ToolValueKind.Text, true)]),
            ]);

    public static ExecutionAuthorizationContext Authorization() =>
        Authorization(
            UserPolicy(),
            Grant(),
            Consent(),
            Now);

    public static ExecutionAuthorizationContext Authorization(
        UserExecutionPolicy policy,
        Grant grant,
        ConsentRecord consent,
        DateTimeOffset evaluatedAt) =>
        new(policy, grant, consent, evaluatedAt);

    public static UserExecutionPolicy UserPolicy() =>
        new(
            Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976205"),
            "1",
            TenantId,
            AppUserId,
            PrincipalId,
            Set(Purpose),
            Set(Capability),
            Set(Operation),
            Set(DataClass),
            DisclosureLevel.Summary,
            Now.AddMinutes(-5),
            Now.AddMinutes(5),
            false,
            null);

    public static Grant Grant() =>
        new(
            Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976206"),
            "1",
            TenantId,
            Resource,
            DataClass,
            PrincipalId,
            Purpose,
            DisclosureLevel.Summary,
            Set(Operation),
            Now.AddMinutes(-5),
            Now.AddMinutes(5),
            false,
            null,
            Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976207"));

    public static ConsentRecord Consent() =>
        new(
            Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976207"),
            "1",
            Guid.Parse("01991c58-c7b0-7f5b-8c54-4c68ae976206"),
            AuthorityPrincipalId,
            PrincipalId,
            new(
                Purpose,
                DisclosureLevel.Summary,
                Set(Operation),
                Now.AddMinutes(-5),
                Now.AddMinutes(5)),
            [
                new(ConsentState.Offered, AuthorityPrincipalId, Now.AddMinutes(-8)),
                new(ConsentState.Accepted, PrincipalId, Now.AddMinutes(-7)),
                new(ConsentState.Active, AuthorityPrincipalId, Now.AddMinutes(-6)),
            ]);

    public static SkillExecutionContext SkillContext(
        ExecutionAuthorizationContext? authorization = null) =>
        new(TenantId, AppUserId, PrincipalId, Purpose, authorization ?? Authorization());

    public static SkillInvocation SkillInvocation(
        SkillManifest manifest,
        SkillExecutionContext? context = null)
    {
        context ??= SkillContext();
        return new(
            manifest.SkillId,
            manifest.Version,
            ToolName,
            context.TenantId,
            context.AppUserId,
            context.PrincipalId,
            context.Purpose,
            Operation,
            DataClass,
            DisclosureLevel.Summary,
            Resource,
            Arguments(),
            InMemorySkillHost.ComputeManifestDigest(manifest));
    }

    public static ChannelExecutionContext ChannelContext(
        ExecutionAuthorizationContext? authorization = null) =>
        new(TenantId, AppUserId, PrincipalId, Purpose, authorization ?? Authorization());

    public static ChannelInvocation ChannelInvocation(
        ChannelManifest manifest,
        ChannelExecutionContext? context = null)
    {
        context ??= ChannelContext();
        return new(
            manifest.ChannelId,
            manifest.Version,
            ChannelOperationName,
            context.TenantId,
            context.AppUserId,
            context.PrincipalId,
            context.Purpose,
            Operation,
            DataClass,
            DisclosureLevel.Summary,
            Resource,
            Arguments(),
            InMemoryChannelHost.ComputeManifestDigest(manifest));
    }

    public static IReadOnlyDictionary<string, JsonElement> Arguments() =>
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["title"] = JsonSerializer.SerializeToElement("Book dentist"),
        };

    public static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static PublisherMetadata Publisher() =>
        new("andreja.first-party", "Andreja");

    private static LifecycleMetadata Lifecycle(int stage) =>
        new(
            stage,
            "Implementation",
            NotApplicable<string>("The local fixture is not deprecated."),
            NotApplicable<string>("The local fixture has no replacement."));

    private static PermissionMetadata Permissions() =>
        new([Capability], [Purpose], [DataClass], DisclosureLevel.Summary);

    private static ExecutionMetadata Execution(string entrypoint) =>
        new(
            ManifestExecutionMode.FirstPartyInProcess,
            [entrypoint],
            NotApplicable<IReadOnlyList<string>>("The local fixture has no network destination."),
            NotApplicable<string>("The local fixture has no remote protocol."));

    private static DataHandlingMetadata DataHandling() =>
        new(
            NotApplicable<string>("The local fixture has no settings."),
            "No content is retained by the execution fixture.",
            NotApplicable<string>("The application host owns in-process resource limits."));

    private static HelpSupportMetadata HelpSupport() =>
        new(
            new("https://github.com/Jamula/Andreja/issues/75"),
            "GitHub issue #75",
            "Seven of Nine and Jett Reno");

    private static CompatibilityMetadata Compatibility() =>
        new(
            "1.0.0",
            NotApplicable<string>("The local fixture has no federation protocol."),
            ["1.0.0"]);

    private static IntegrityMetadata Integrity() =>
        new(
            NotApplicable<string>("The fixture has no separate package."),
            NotApplicable<string>("The fixture has no separate package signature."),
            NotApplicable<string>("The application release owns provenance."),
            NotApplicable<string>("The fixture has no separate SBOM."));

    private static ManifestField<T> NotApplicable<T>(string reason) =>
        ManifestField.NotApplicable<T>(reason);
}
