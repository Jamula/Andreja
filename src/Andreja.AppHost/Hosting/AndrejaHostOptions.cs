using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.Hosting;

public sealed class AndrejaHostOptions
{
    public const string SectionName = "Andreja";

    [Required]
    public string InstanceName { get; init; } = string.Empty;
}
