using Andreja.Platform.Contracts.Skills;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.Modules.Skills;

public sealed class InMemorySkillHost : ISkillHost
{
    private readonly Dictionary<(string Id, string Version), Registration> registrations = [];

    public void Register(SkillManifest manifest, IReadOnlyDictionary<string, SkillToolHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(handlers);

        var declaredTools = manifest.Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        if (declaredTools.Count != manifest.Tools.Count || !declaredTools.SetEquals(handlers.Keys))
        {
            throw new ArgumentException("Handlers must exactly match declared tools.", nameof(handlers));
        }

        registrations.Add(
            (manifest.SkillId, manifest.Version),
            new Registration(manifest, ComputeManifestDigest(manifest), handlers));
    }

    public ValueTask<SkillManifest?> ResolveManifestAsync(
        string skillId,
        string version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registrations.TryGetValue((skillId, version), out var registration);
        return ValueTask.FromResult(registration?.Manifest);
    }

    public async ValueTask<SkillResult> InvokeAsync(
        SkillInvocation invocation,
        SkillExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!registrations.TryGetValue((invocation.SkillId, invocation.SkillVersion), out var registration))
            {
                return Denied("skill-not-declared", "The requested skill version is not registered.");
            }

            var suppliedDigest = Encoding.UTF8.GetBytes(invocation.ManifestDigest);
            var registeredDigest = Encoding.UTF8.GetBytes(registration.ManifestDigest);
            var currentDigest = Encoding.UTF8.GetBytes(ComputeManifestDigest(registration.Manifest));
            if (!CryptographicOperations.FixedTimeEquals(suppliedDigest, registeredDigest)
                || !CryptographicOperations.FixedTimeEquals(currentDigest, registeredDigest))
            {
                return Denied("manifest-tampered", "The supplied manifest digest does not match.");
            }

            var tool = registration.Manifest.Tools.SingleOrDefault(
                candidate => string.Equals(candidate.Name, invocation.ToolName, StringComparison.Ordinal));
            if (tool is null || !registration.Handlers.TryGetValue(invocation.ToolName, out var handler))
            {
                return Denied("tool-not-declared", "The requested tool is not declared.");
            }

            if (invocation.TenantId != context.TenantId)
            {
                return Denied("wrong-tenant", "The invocation tenant does not match the execution context.");
            }

            if (!string.Equals(invocation.Purpose, context.Purpose, StringComparison.Ordinal)
                || !tool.AllowedPurposes.Contains(invocation.Purpose, StringComparer.Ordinal))
            {
                return Denied("wrong-purpose", "The purpose is not authorized for this tool.");
            }

            if (tool.RequiredCapabilities.Any(
                required => !context.GrantedCapabilities.Contains(required)))
            {
                return Denied("capability-denied", "A required capability is not granted.");
            }

            var schemaFailure = ValidateArguments(tool.InputSchema, invocation.Arguments);
            if (schemaFailure is not null)
            {
                return new SkillResult(SkillResultStatus.Invalid, null, null, schemaFailure);
            }

            return await handler(invocation, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new SkillResult(
                SkillResultStatus.Cancelled,
                null,
                null,
                new SkillFailure("cancelled", "The invocation was cancelled."));
        }
    }

    public static string ComputeManifestDigest(SkillManifest manifest)
    {
        var canonical = new StringBuilder();
        Append(canonical, manifest.SkillId);
        Append(canonical, manifest.Version);
        Append(canonical, manifest.DisplayName);

        foreach (var tool in manifest.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal))
        {
            Append(canonical, tool.Name);
            Append(canonical, tool.Version);
            Append(canonical, tool.Description);
            foreach (var field in tool.InputSchema.OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                Append(canonical, field.Name);
                Append(canonical, field.Kind.ToString());
                Append(canonical, field.Required ? "1" : "0");
            }

            foreach (var capability in tool.RequiredCapabilities.Order(StringComparer.Ordinal))
            {
                Append(canonical, capability);
            }

            foreach (var purpose in tool.AllowedPurposes.Order(StringComparer.Ordinal))
            {
                Append(canonical, purpose);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static SkillFailure? ValidateArguments(
        IReadOnlyList<ToolFieldSchema> schema,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var declared = schema.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        if (arguments.Keys.Any(argument => !declared.Contains(argument)))
        {
            return new("undeclared-argument", "An argument is not declared by the tool schema.");
        }

        foreach (var field in schema)
        {
            if (!arguments.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
                {
                    return new("missing-argument", $"Required argument '{field.Name}' is missing.");
                }

                continue;
            }

            if (!Matches(field.Kind, value.ValueKind))
            {
                return new("argument-type", $"Argument '{field.Name}' has the wrong type.");
            }
        }

        return null;
    }

    private static bool Matches(ToolValueKind expected, JsonValueKind actual) =>
        expected switch
        {
            ToolValueKind.Text => actual == JsonValueKind.String,
            ToolValueKind.Numeric => actual == JsonValueKind.Number,
            ToolValueKind.Logical => actual is JsonValueKind.True or JsonValueKind.False,
            ToolValueKind.Structured => actual == JsonValueKind.Object,
            ToolValueKind.Sequence => actual == JsonValueKind.Array,
            _ => false,
        };

    private static SkillResult Denied(string code, string message) =>
        new(SkillResultStatus.Denied, null, null, new SkillFailure(code, message));

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append(';');

    private sealed record Registration(
        SkillManifest Manifest,
        string ManifestDigest,
        IReadOnlyDictionary<string, SkillToolHandler> Handlers);
}
