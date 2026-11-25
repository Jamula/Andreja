using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.Hosting;

public sealed class AndrejaOperationsOptions
{
    public const string SectionName = "Andreja:Operations";

    [Required]
    public string DataProtectionKeysPath { get; init; } = string.Empty;

    public DatabaseReadinessOptions Database { get; init; } = new();
}
