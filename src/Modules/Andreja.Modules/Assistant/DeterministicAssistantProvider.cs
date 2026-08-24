using Andreja.Platform.Contracts.Assistant;

namespace Andreja.Modules.Assistant;

public sealed class DeterministicAssistantProvider : IAssistantProvider
{
    private readonly AssistantProviderCapabilities capabilities;
    private readonly Func<AssistantRequest, CancellationToken, ValueTask<AssistantResponse>> responder;

    public DeterministicAssistantProvider(
        AssistantProviderCapabilities capabilities,
        Func<AssistantRequest, CancellationToken, ValueTask<AssistantResponse>> responder)
    {
        this.capabilities = capabilities;
        this.responder = responder;
    }

    public ValueTask<AssistantProviderCapabilities> GetCapabilitiesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(capabilities);
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

        IAssistantSession session = new Session(request, responder);
        return ValueTask.FromResult(session);
    }

    private sealed class Session(
        AssistantSessionRequest sessionRequest,
        Func<AssistantRequest, CancellationToken, ValueTask<AssistantResponse>> responder)
        : IAssistantSession
    {
        private readonly CancellationTokenSource sessionCancellation = new();
        private bool disposed;

        public async ValueTask<AssistantResponse> CompleteAsync(
            AssistantRequest request,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (request.AllowedToolNames.Any(
                requested => !sessionRequest.AllowedTools.Any(
                    allowed => string.Equals(allowed.Name, requested, StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("The request widened the session tool allowlist.");
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation.Token);
            return await responder(request, linked.Token).ConfigureAwait(false);
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
}
