namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public interface IAssistantCredentialStore
{
    ValueTask<AssistantCredential?> ResolveAsync(
        string credentialHandle,
        CancellationToken cancellationToken);
}
