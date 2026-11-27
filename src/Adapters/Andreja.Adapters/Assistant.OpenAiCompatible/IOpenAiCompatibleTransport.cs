using Andreja.Platform.Contracts.Assistant;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public interface IOpenAiCompatibleTransport
{
    ValueTask<AssistantResponse> CompleteAsync(
        AssistantProviderProfile profile,
        AssistantSessionRequest session,
        AssistantRequest request,
        CancellationToken cancellationToken);
}
