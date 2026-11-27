namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public sealed record OpenAiCompatibleTransportPolicy(
    IReadOnlyList<Uri> AllowedEndpoints,
    int MaximumResponseBodyBytes,
    int MaximumRetries,
    TimeSpan RetryBaseDelay,
    long ApprovedExternalTotalUnits);
