using Andreja.Adapters.Assistant.OpenAiCompatible;
using Andreja.Modules.Assistant;
using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Skills;

namespace Andreja.UnitTests;

public sealed class AssistantProviderTests
{
    [Fact]
    public async Task DeterministicProviderReturnsStructuredFailure()
    {
        var expected = Response(
            AssistantResponseStatus.Failed,
            new("provider-unavailable", "Provider is unavailable.", true));
        var provider = new DeterministicAssistantProvider(Capabilities(), (_, _) => ValueTask.FromResult(expected));
        await using var session = await provider.CreateSessionAsync(SessionRequest(), CancellationToken.None);

        var actual = await session.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.Equal("provider-unavailable", actual.Failure?.Code);
    }

    [Fact]
    public async Task SessionCancellationStopsInFlightRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new DeterministicAssistantProvider(
            Capabilities(),
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Response(AssistantResponseStatus.Completed, null);
            });
        await using var session = await provider.CreateSessionAsync(SessionRequest(), CancellationToken.None);

        var completion = session.CompleteAsync(Request(), CancellationToken.None).AsTask();
        await started.Task;
        await session.CancelAsync(CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
    }

    [Fact]
    public void OpenAiProfileRejectsUnsafeEndpointAndSecretInsteadOfHandle()
    {
        var unsafeEndpoint = Profile() with { Endpoint = new("http://api.example.test/v1") };
        var rawSecret = Profile() with { CredentialHandle = "sk-not-a-handle" };

        Assert.Throws<ArgumentException>(() => OpenAiCompatibleAssistantAdapter.Validate(unsafeEndpoint));
        Assert.Throws<ArgumentException>(() => OpenAiCompatibleAssistantAdapter.Validate(rawSecret));
        Assert.Equal(Profile(), OpenAiCompatibleAssistantAdapter.Validate(Profile()));
    }

    private static AssistantProviderCapabilities Capabilities() =>
        new("fake", "deterministic-v1", AssistantCapability.TextCompletion | AssistantCapability.Cancellation);

    private static AssistantSessionRequest SessionRequest() =>
        new(
            Guid.CreateVersion7(),
            new(Guid.CreateVersion7(), Guid.CreateVersion7(), "task.capture"),
            [Tool()]);

    private static AssistantRequest Request() =>
        new(Guid.CreateVersion7(), "Create a task.", ["open-loops.propose-task"]);

    private static ToolDefinition Tool() =>
        new(
            "open-loops.propose-task",
            "1",
            "Propose a task.",
            [],
            ["tasks.propose"],
            ["task.capture"]);

    private static AssistantResponse Response(
        AssistantResponseStatus status,
        AssistantFailure? failure) =>
        new(
            Guid.CreateVersion7(),
            status,
            null,
            [],
            new("fake", "deterministic-v1", 4, 2, TimeSpan.Zero, status.ToString(), 0, 0),
            failure);

    private static AssistantProviderProfile Profile() =>
        new(
            new Uri("https://api.example.test/v1"),
            "compatible-model",
            "credential://assistant/byok-primary",
            TimeSpan.FromSeconds(30),
            "Provider retention disclosed.",
            10_000,
            2_000);
}
