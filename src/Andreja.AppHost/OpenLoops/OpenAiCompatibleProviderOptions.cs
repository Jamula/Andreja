using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.OpenLoops;

public sealed class OpenAiCompatibleProviderOptions
{
    public string Endpoint { get; init; } = string.Empty;

    public string[] AllowedEndpoints { get; init; } = [];

    public string Model { get; init; } = string.Empty;

    public string CredentialHandle { get; init; } = string.Empty;

    public string CredentialFile { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    [Required]
    public string ProviderDisclosure { get; init; } =
        "Operator-selected OpenAI-compatible provider receives the submitted task request.";

    [Required]
    public string RetentionDisclosure { get; init; } =
        "Review the selected provider's retention policy before enabling this profile.";

    [Range(1, long.MaxValue)]
    public long MaximumInputUnits { get; init; } = 10_000;

    [Range(1, long.MaxValue)]
    public long MaximumOutputUnits { get; init; } = 2_000;

    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumResponseBodyBytes { get; init; } = 1024 * 1024;

    [Range(0, 5)]
    public int MaximumRetries { get; init; } = 2;

    [Range(0, 5000)]
    public int RetryBaseDelayMilliseconds { get; init; } = 100;

    [Range(0, long.MaxValue)]
    public long ApprovedExternalTotalUnits { get; init; }
}
