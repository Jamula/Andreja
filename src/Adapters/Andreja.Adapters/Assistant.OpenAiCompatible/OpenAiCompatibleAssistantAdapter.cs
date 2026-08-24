using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Composition;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public interface IOpenAiCompatibleTransport
{
    ValueTask<AssistantResponse> CompleteAsync(
        AssistantProviderProfile profile,
        AssistantSessionRequest session,
        AssistantRequest request,
        CancellationToken cancellationToken);
}

public sealed class OpenAiCompatibleAssistantAdapter(
    AssistantProviderProfile profile,
    IOpenAiCompatibleTransport transport)
    : IAdapterBoundary, IAssistantProvider
{
    private readonly AssistantProviderProfile profile = Validate(profile);

    public OpenAiCompatibleAssistantAdapter()
        : this(
            new(
                new Uri("http://localhost/v1"),
                "not-configured",
                "credential://assistant/not-configured",
                TimeSpan.FromSeconds(30),
                "No provider has been configured.",
                "No provider has been configured.",
                0,
                0),
            new DisabledTransport())
    {
    }

    public ValueTask<AssistantProviderCapabilities> GetCapabilitiesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new AssistantProviderCapabilities(
            "openai-compatible",
            profile.Model,
            AssistantCapability.TextCompletion
                | AssistantCapability.TypedTools
                | AssistantCapability.UsageReporting
                | AssistantCapability.Cancellation));
    }

    public ValueTask<IAssistantSession> CreateSessionAsync(
        AssistantSessionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("A session ID is required.", nameof(request));
        }

        IAssistantSession session = new Session(profile, request, transport);
        return ValueTask.FromResult(session);
    }

    public static AssistantProviderProfile Validate(AssistantProviderProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.Endpoint.IsAbsoluteUri
            || candidate.Endpoint.OriginalString.Length > 2048
            || !string.IsNullOrEmpty(candidate.Endpoint.UserInfo)
            || !string.IsNullOrEmpty(candidate.Endpoint.Query)
            || !string.IsNullOrEmpty(candidate.Endpoint.Fragment)
            || (candidate.Endpoint.Scheme != Uri.UriSchemeHttps
                && !(candidate.Endpoint.Scheme == Uri.UriSchemeHttp && candidate.Endpoint.IsLoopback)))
        {
            throw new ArgumentException(
                "The endpoint must be absolute HTTPS, or HTTP only for a loopback host, with no user information or fragment.",
                nameof(candidate));
        }

        if (string.IsNullOrWhiteSpace(candidate.Model)
            || candidate.Model.Length > 256)
        {
            throw new ArgumentException("A model is required.", nameof(candidate));
        }

        if (string.IsNullOrWhiteSpace(candidate.CredentialHandle)
            || candidate.CredentialHandle.Length > 512
            || !Uri.TryCreate(candidate.CredentialHandle, UriKind.Absolute, out var handle)
            || handle.Scheme != "credential"
            || !string.IsNullOrEmpty(handle.UserInfo)
            || !string.IsNullOrEmpty(handle.Query)
            || !string.IsNullOrEmpty(handle.Fragment)
            || string.IsNullOrWhiteSpace(handle.Host)
            || handle.AbsolutePath == "/")
        {
            throw new ArgumentException(
                "A non-secret credential:// handle is required.",
                nameof(candidate));
        }

        if (candidate.Timeout <= TimeSpan.Zero
            || candidate.Timeout > TimeSpan.FromMinutes(5)
            || string.IsNullOrWhiteSpace(candidate.ProviderDisclosure)
            || candidate.ProviderDisclosure.Length > 2000
            || string.IsNullOrWhiteSpace(candidate.RetentionDisclosure)
            || candidate.RetentionDisclosure.Length > 2000
            || candidate.MaximumInputUnits is < 0
            || candidate.MaximumOutputUnits is < 0)
        {
            throw new ArgumentException("The provider policy is invalid.", nameof(candidate));
        }

        return candidate;
    }

    private sealed class Session(
        AssistantProviderProfile profile,
        AssistantSessionRequest sessionRequest,
        IOpenAiCompatibleTransport transport)
        : IAssistantSession
    {
        private readonly CancellationTokenSource sessionCancellation = new();
        private bool disposed;

        public async ValueTask<AssistantResponse> CompleteAsync(
            AssistantRequest assistantRequest,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (assistantRequest.RequestId == Guid.Empty)
            {
                throw new ArgumentException("A request ID is required.", nameof(assistantRequest));
            }

            if (assistantRequest.AllowedToolNames.Any(
                requested => !sessionRequest.AllowedTools.Any(
                    allowed => string.Equals(allowed.Name, requested, StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("The request widened the session tool allowlist.");
            }

            using var timeout = new CancellationTokenSource(profile.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation.Token,
                timeout.Token);
            try
            {
                return await transport.CompleteAsync(
                    profile,
                    sessionRequest,
                    assistantRequest,
                    linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                timeout.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
                && !sessionCancellation.IsCancellationRequested)
            {
                return Failure(
                    assistantRequest.RequestId,
                    "provider-timeout",
                    "The assistant provider timed out.",
                    isTransient: true,
                    profile);
            }
            catch (OperationCanceledException)
            {
                var usage = Usage(profile, "cancelled");
                OpenAiCompatibleMetrics.Record(usage);
                return new(
                    assistantRequest.RequestId,
                    AssistantResponseStatus.Cancelled,
                    null,
                    [],
                    usage,
                    null);
            }
        }

        public ValueTask CancelAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sessionCancellation.Cancel();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                sessionCancellation.Cancel();
                sessionCancellation.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private static AssistantResponse Failure(
        Guid requestId,
        string code,
        string message,
        bool isTransient,
        AssistantProviderProfile profile)
    {
        var usage = Usage(profile, code);
        OpenAiCompatibleMetrics.Record(usage);
        return new(
            requestId,
            AssistantResponseStatus.Failed,
            null,
            [],
            usage,
            new(code, message, isTransient));
    }

    private static AssistantUsage Usage(
        AssistantProviderProfile profile,
        string resultClass) =>
        new(
            "openai-compatible",
            profile.Model,
            null,
            null,
            TimeSpan.Zero,
            resultClass,
            0,
            0);

    private sealed class DisabledTransport : IOpenAiCompatibleTransport
    {
        public ValueTask<AssistantResponse> CompleteAsync(
            AssistantProviderProfile profile,
            AssistantSessionRequest session,
            AssistantRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new AssistantResponse(
                request.RequestId,
                AssistantResponseStatus.Failed,
                null,
                [],
                Usage(profile, "not-configured"),
                new(
                    "provider-not-configured",
                    "The OpenAI-compatible provider is not configured.",
                    false)));
        }
    }
}
