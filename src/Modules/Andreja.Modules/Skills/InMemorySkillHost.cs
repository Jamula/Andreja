using Andreja.Modules.Execution;
using Andreja.Platform.Contracts.Execution;
using Andreja.Platform.Contracts.Skills;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.Modules.Skills;

public sealed class InMemorySkillHost : ISkillHost
{
    private readonly ConcurrentDictionary<(string Id, string Version), Registration> registrations = [];
    private readonly IExecutionAuthorizationEvaluator evaluator;
    private readonly IExecutionAuditSink auditSink;

    public InMemorySkillHost()
    {
        var sink = new InMemoryExecutionAuditSink();
        evaluator = new ExecutionAuthorizationEvaluator(sink);
        auditSink = sink;
    }

    public InMemorySkillHost(
        IExecutionAuthorizationEvaluator evaluator,
        IExecutionAuditSink auditSink)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
    }

    public IReadOnlyList<ExecutionAuditEntry> AuditEntries =>
        auditSink is InMemoryExecutionAuditSink sink ? sink.Entries : [];

    public void Register(
        SkillManifest manifest,
        IReadOnlyDictionary<string, SkillToolHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(handlers);
        ManifestContract.Validate(manifest);

        var declaredTools = manifest.Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        if (declaredTools.Count != manifest.Tools.Count || !declaredTools.SetEquals(handlers.Keys))
        {
            throw new ArgumentException("Handlers must exactly match declared tools.", nameof(handlers));
        }

        if (!registrations.TryAdd(
                (manifest.SkillId, manifest.Version),
                new(manifest, ComputeManifestDigest(manifest), handlers)))
        {
            throw new InvalidOperationException("The skill version is already registered.");
        }
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
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (!registrations.TryGetValue((invocation.SkillId, invocation.SkillVersion), out var registration))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "skill-not-declared",
                    "The requested skill version is not registered.").ConfigureAwait(false);
            }

            var suppliedDigest = Encoding.UTF8.GetBytes(invocation.ManifestDigest);
            var registeredDigest = Encoding.UTF8.GetBytes(registration.ManifestDigest);
            var currentDigest = Encoding.UTF8.GetBytes(ComputeManifestDigest(registration.Manifest));
            if (!CryptographicOperations.FixedTimeEquals(suppliedDigest, registeredDigest)
                || !CryptographicOperations.FixedTimeEquals(currentDigest, registeredDigest))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "manifest-tampered",
                    "The supplied manifest digest does not match.").ConfigureAwait(false);
            }

            var tool = registration.Manifest.Tools.SingleOrDefault(
                candidate => string.Equals(candidate.Name, invocation.ToolName, StringComparison.Ordinal));
            if (tool is null || !registration.Handlers.TryGetValue(invocation.ToolName, out var handler))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "tool-not-declared",
                    "The requested tool is not declared.").ConfigureAwait(false);
            }

            var identityDenial = IdentityDenial(invocation, context);
            if (identityDenial is not null)
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    identityDenial.Value.Code,
                    identityDenial.Value.Message).ConfigureAwait(false);
            }

            if (!string.Equals(invocation.Purpose, context.Purpose, StringComparison.Ordinal)
                || !tool.AllowedPurposes.Contains(invocation.Purpose, StringComparer.Ordinal)
                || !registration.Manifest.Permissions.AllowedPurposes.Contains(
                    invocation.Purpose,
                    StringComparer.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "wrong-purpose",
                    "The purpose is not authorized for this tool.").ConfigureAwait(false);
            }

            if (!string.Equals(invocation.Operation, tool.Operation, StringComparison.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "operation-denied",
                    "The operation does not match the declared tool operation.").ConfigureAwait(false);
            }

            if (!string.Equals(invocation.DataClass, tool.DataClass, StringComparison.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "data-class-denied",
                    "The data class does not match the declared tool data class.").ConfigureAwait(false);
            }

            var schemaFailure = ArgumentSchemaValidator.Validate(
                tool.InputSchema,
                invocation.Arguments);
            if (schemaFailure is not null)
            {
                await ExecutionAudit.DeniedAsync(
                    auditSink,
                    "skill",
                    invocation.SkillId,
                    invocation.SkillVersion,
                    invocation.TenantId,
                    invocation.AppUserId,
                    invocation.PrincipalId,
                    invocation.Purpose,
                    invocation.Operation,
                    invocation.DataClass,
                    invocation.RequestedDisclosure,
                    context.Authorization,
                    schemaFailure.Code).ConfigureAwait(false);
                return new(SkillResultStatus.Invalid, null, null, schemaFailure);
            }

            var decision = await evaluator.EvaluateAsync(
                new(
                    "skill",
                    invocation.SkillId,
                    invocation.SkillVersion,
                    invocation.TenantId,
                    invocation.AppUserId,
                    invocation.PrincipalId,
                    invocation.Purpose,
                    tool.RequiredCapabilities,
                    invocation.Operation,
                    invocation.DataClass,
                    invocation.RequestedDisclosure,
                    tool.MaximumDisclosure < registration.Manifest.Permissions.MaximumDisclosure
                        ? tool.MaximumDisclosure
                        : registration.Manifest.Permissions.MaximumDisclosure,
                    invocation.ResourceReference,
                    context.Authorization),
                cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed)
            {
                return Denied(decision.Code, "Execution authorization was denied.");
            }

            return await handler(
                invocation,
                context with { EffectiveDisclosure = decision.EffectiveDisclosure },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(
                SkillResultStatus.Cancelled,
                null,
                null,
                new("cancelled", "The invocation was cancelled."));
        }
    }

    public static string ComputeManifestDigest(SkillManifest manifest) =>
        ManifestContract.ComputeDigest(manifest);

    private static (string Code, string Message)? IdentityDenial(
        SkillInvocation invocation,
        SkillExecutionContext context)
    {
        if (invocation.TenantId != context.TenantId)
        {
            return ("wrong-tenant", "The invocation tenant does not match the execution context.");
        }

        if (invocation.AppUserId != context.AppUserId)
        {
            return ("wrong-user", "The invocation user does not match the execution context.");
        }

        if (invocation.PrincipalId != context.PrincipalId)
        {
            return ("wrong-principal", "The invocation principal does not match the execution context.");
        }

        return null;
    }

    private async ValueTask<SkillResult> DeniedAsync(
        SkillInvocation invocation,
        SkillExecutionContext context,
        string code,
        string message)
    {
        await ExecutionAudit.DeniedAsync(
            auditSink,
            "skill",
            invocation.SkillId,
            invocation.SkillVersion,
            invocation.TenantId,
            invocation.AppUserId,
            invocation.PrincipalId,
            invocation.Purpose,
            invocation.Operation,
            invocation.DataClass,
            invocation.RequestedDisclosure,
            context.Authorization,
            code).ConfigureAwait(false);
        return Denied(code, message);
    }

    private static SkillResult Denied(string code, string message) =>
        new(SkillResultStatus.Denied, null, null, new(code, message));

    private sealed record Registration(
        SkillManifest Manifest,
        string ManifestDigest,
        IReadOnlyDictionary<string, SkillToolHandler> Handlers);
}

internal static class ArgumentSchemaValidator
{
    public static SkillFailure? Validate(
        IReadOnlyList<ToolFieldSchema> schema,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var declared = schema.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        if (arguments.Keys.Any(argument => !declared.Contains(argument)))
        {
            return new("undeclared-argument", "An argument is not declared by the schema.");
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
}
