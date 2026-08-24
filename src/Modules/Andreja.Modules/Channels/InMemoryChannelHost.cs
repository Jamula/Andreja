using Andreja.Modules.Execution;
using Andreja.Modules.Skills;
using Andreja.Platform.Contracts.Channels;
using Andreja.Platform.Contracts.Execution;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Andreja.Modules.Channels;

public sealed class InMemoryChannelHost : IChannelHost
{
    private readonly ConcurrentDictionary<(string Id, string Version), Registration> registrations = [];
    private readonly IExecutionAuthorizationEvaluator evaluator;
    private readonly IExecutionAuditSink auditSink;

    public InMemoryChannelHost()
    {
        var sink = new InMemoryExecutionAuditSink();
        evaluator = new ExecutionAuthorizationEvaluator(sink);
        auditSink = sink;
    }

    public InMemoryChannelHost(
        IExecutionAuthorizationEvaluator evaluator,
        IExecutionAuditSink auditSink)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
    }

    public IReadOnlyList<ExecutionAuditEntry> AuditEntries =>
        auditSink is InMemoryExecutionAuditSink sink ? sink.Entries : [];

    public void Register(
        ChannelManifest manifest,
        IReadOnlyDictionary<string, ChannelOperationHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(handlers);
        ManifestContract.Validate(manifest);

        var declared = manifest.Operations
            .Select(operation => operation.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (declared.Count != manifest.Operations.Count || !declared.SetEquals(handlers.Keys))
        {
            throw new ArgumentException(
                "Handlers must exactly match declared channel operations.",
                nameof(handlers));
        }

        if (!registrations.TryAdd(
                (manifest.ChannelId, manifest.Version),
                new(manifest, ComputeManifestDigest(manifest), handlers)))
        {
            throw new InvalidOperationException("The channel version is already registered.");
        }
    }

    public ValueTask<ChannelManifest?> ResolveManifestAsync(
        string channelId,
        string version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registrations.TryGetValue((channelId, version), out var registration);
        return ValueTask.FromResult(registration?.Manifest);
    }

    public async ValueTask<ChannelResult> InvokeAsync(
        ChannelInvocation invocation,
        ChannelExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (!registrations.TryGetValue(
                    (invocation.ChannelId, invocation.ChannelVersion),
                    out var registration))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "channel-not-declared",
                    "The requested channel version is not registered.").ConfigureAwait(false);
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

            var operation = registration.Manifest.Operations.SingleOrDefault(
                candidate => string.Equals(
                    candidate.Name,
                    invocation.OperationName,
                    StringComparison.Ordinal));
            if (operation is null
                || !registration.Handlers.TryGetValue(invocation.OperationName, out var handler))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "operation-not-declared",
                    "The requested channel operation is not declared.").ConfigureAwait(false);
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
                || !operation.AllowedPurposes.Contains(invocation.Purpose, StringComparer.Ordinal)
                || !registration.Manifest.Permissions.AllowedPurposes.Contains(
                    invocation.Purpose,
                    StringComparer.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "wrong-purpose",
                    "The purpose is not authorized for this channel operation.").ConfigureAwait(false);
            }

            if (!string.Equals(invocation.Operation, operation.Operation, StringComparison.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "operation-denied",
                    "The operation does not match the channel declaration.").ConfigureAwait(false);
            }

            if (!string.Equals(invocation.DataClass, operation.DataClass, StringComparison.Ordinal))
            {
                return await DeniedAsync(
                    invocation,
                    context,
                    "data-class-denied",
                    "The data class does not match the channel declaration.").ConfigureAwait(false);
            }

            var schemaFailure = ArgumentSchemaValidator.Validate(
                operation.InputSchema,
                invocation.Arguments);
            if (schemaFailure is not null)
            {
                await ExecutionAudit.DeniedAsync(
                    auditSink,
                    "channel",
                    invocation.ChannelId,
                    invocation.ChannelVersion,
                    invocation.TenantId,
                    invocation.AppUserId,
                    invocation.PrincipalId,
                    invocation.Purpose,
                    invocation.Operation,
                    invocation.DataClass,
                    invocation.RequestedDisclosure,
                    context.Authorization,
                    schemaFailure.Code).ConfigureAwait(false);
                return new(
                    ChannelResultStatus.Invalid,
                    null,
                    new(schemaFailure.Code, schemaFailure.Message));
            }

            var decision = await evaluator.EvaluateAsync(
                new(
                    "channel",
                    invocation.ChannelId,
                    invocation.ChannelVersion,
                    invocation.TenantId,
                    invocation.AppUserId,
                    invocation.PrincipalId,
                    invocation.Purpose,
                    [operation.Capability],
                    invocation.Operation,
                    invocation.DataClass,
                    invocation.RequestedDisclosure,
                    operation.MaximumDisclosure < registration.Manifest.Permissions.MaximumDisclosure
                        ? operation.MaximumDisclosure
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
                ChannelResultStatus.Cancelled,
                null,
                new("cancelled", "The invocation was cancelled."));
        }
    }

    public static string ComputeManifestDigest(ChannelManifest manifest) =>
        ManifestContract.ComputeDigest(manifest);

    private static (string Code, string Message)? IdentityDenial(
        ChannelInvocation invocation,
        ChannelExecutionContext context)
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

    private async ValueTask<ChannelResult> DeniedAsync(
        ChannelInvocation invocation,
        ChannelExecutionContext context,
        string code,
        string message)
    {
        await ExecutionAudit.DeniedAsync(
            auditSink,
            "channel",
            invocation.ChannelId,
            invocation.ChannelVersion,
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

    private static ChannelResult Denied(string code, string message) =>
        new(ChannelResultStatus.Denied, null, new(code, message));

    private sealed record Registration(
        ChannelManifest Manifest,
        string ManifestDigest,
        IReadOnlyDictionary<string, ChannelOperationHandler> Handlers);
}
