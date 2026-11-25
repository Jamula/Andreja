using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.OpenLoops;

public sealed class OpenLoopsOptions
{
    public const string SectionName = "Andreja:OpenLoops";

    public bool Enabled { get; init; }

    [Required]
    [Url]
    public string PublicOrigin { get; init; } = "https://localhost:5001";

    public string AssistantProvider { get; init; } = "deterministic";

    public OpenAiCompatibleProviderOptions OpenAiCompatible { get; init; } = new();
}
