using System.ComponentModel.DataAnnotations;

namespace Andreja.AppHost.Hosting;

public sealed class AndrejaOperationsOptions
{
    public const string SectionName = "Andreja:Operations";

    [Required]
    public string DataProtectionKeysPath { get; init; } = string.Empty;

    public DatabaseReadinessOptions Database { get; init; } = new();
}

public sealed class DatabaseReadinessOptions
{
    public bool Enabled { get; init; }

    [Required]
    public string Host { get; init; } = "postgres";

    [Range(1, 65535)]
    public int Port { get; init; } = 5432;

    [Required]
    public string Name { get; init; } = "andreja";

    [Required]
    public string Username { get; init; } = "andreja";

    public string PasswordFile { get; init; } = string.Empty;
}
