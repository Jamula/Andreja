using Andreja.Adapters.PostgreSql;
using System.Security.Cryptography;

return await PortabilityCommand.RunAsync(args);

internal static class PortabilityCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        byte[]? key = null;
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                WriteUsage();
                return args.Length == 0 ? 2 : 0;
            }

            var options = ParseOptions(args[1..]);
            var connectionEnvironment = options.GetValueOrDefault(
                "--connection-env",
                "ANDREJA_PORTABILITY_POSTGRES");
            var keyEnvironment = options.GetValueOrDefault(
                "--key-env",
                "ANDREJA_PORTABILITY_KEY");
            var connectionString = Environment.GetEnvironmentVariable(connectionEnvironment)
                ?? throw new InvalidOperationException(
                    $"Required connection environment variable {connectionEnvironment} is not set.");
            var keyText = Environment.GetEnvironmentVariable(keyEnvironment)
                ?? throw new InvalidOperationException(
                    $"Required key environment variable {keyEnvironment} is not set.");
            key = Convert.FromBase64String(keyText);
            if (key.Length != 32)
            {
                throw new InvalidOperationException("The archive key must decode to exactly 32 bytes.");
            }

            return args[0] switch
            {
                "export" => await ExportAsync(
                    options,
                    connectionString,
                    key,
                    cancellation.Token),
                "import" => await ImportAsync(
                    options,
                    connectionString,
                    key,
                    cancellation.Token),
                _ => throw new ArgumentException("Expected the export or import command."),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Portability operation cancelled; no partial commit was retained.");
            return 130;
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            Console.Error.WriteLine(GetFailureMessage(exception));
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    private static async Task<int> ExportAsync(
        Dictionary<string, string> options,
        string connectionString,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var tenant = Guid.Parse(Require(options, "--tenant"));
        var output = Path.GetFullPath(Require(options, "--output"));
        var result = await PostgreSqlApplicationPortability.ExportAsync(
            connectionString,
            tenant,
            output,
            key,
            typeof(PortabilityCommand).Assembly.GetName().Version?.ToString() ?? "unknown",
            cancellationToken);
        Console.WriteLine(
            $"Export complete: id={result.ExportId:D} tenant={result.TenantReference} "
            + $"records={result.Counts.Values.Sum()} bytes={result.ArchiveBytes}.");
        return 0;
    }

    private static async Task<int> ImportAsync(
        Dictionary<string, string> options,
        string connectionString,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var dryRun = options.ContainsKey("--dry-run");
        var commit = options.ContainsKey("--commit");
        if (dryRun == commit)
        {
            throw new ArgumentException("Import requires exactly one of --dry-run or --commit.");
        }
        Guid? approved = null;
        if (commit)
        {
            approved = Guid.Parse(Require(options, "--approve-export"));
        }
        var archive = Path.GetFullPath(Require(options, "--archive"));
        if ((File.GetAttributes(archive) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Archive reparse points are forbidden.");
        }
        var report = await PostgreSqlApplicationPortability.ImportAsync(
            connectionString,
            archive,
            key,
            commit,
            approved,
            cancellationToken);
        var mode = report.DryRun ? "Dry run valid" : "Import committed";
        var replay = report.IdempotentReplay ? " idempotent-replay=true" : string.Empty;
        Console.WriteLine(
            $"{mode}: id={report.ExportId:D} tenant={report.TenantReference} "
            + $"records={report.Counts.Values.Sum()} excluded={report.Exclusions.Count} "
            + $"reauthorization={report.Reauthorization.Count}.{replay}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(name, "true"))
            {
                throw new ArgumentException($"Invalid or duplicate option: {name}");
            }
            if (name is not "--dry-run" and not "--commit")
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Option {name} requires a value.");
                }
                options[name] = args[index];
            }
        }
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "--tenant", "--output", "--archive", "--dry-run", "--commit",
            "--approve-export", "--connection-env", "--key-env",
        };
        if (options.Keys.Any(key => !known.Contains(key)))
        {
            throw new ArgumentException("An unsupported option was supplied.");
        }
        return options;
    }

    private static string Require(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required option {name} is missing.");

    internal static string GetFailureMessage(Exception exception) =>
        exception switch
        {
            TimeoutException =>
                "Portability operation failed: the import lock timed out.",
            Npgsql.NpgsqlException =>
                "Portability operation failed: database access failed.",
            IOException or UnauthorizedAccessException =>
                "Portability operation failed: file access failed.",
            InvalidDataException or System.Text.Json.JsonException =>
                "Portability operation rejected: archive validation failed.",
            ArgumentException or FormatException =>
                "Portability operation rejected: command arguments are invalid.",
            InvalidOperationException =>
                "Portability operation rejected: operation preconditions were not met.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                "The exception is not an expected portability failure."),
        };

    private static bool IsExpectedFailure(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or Npgsql.NpgsqlException
            or FormatException
            or TimeoutException
            or System.Text.Json.JsonException;

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Andreja application portability
              export --tenant <uuid> --output <archive> [--connection-env <name>] [--key-env <name>]
              import --archive <archive> --dry-run [--connection-env <name>] [--key-env <name>]
              import --archive <archive> --commit --approve-export <uuid> [--connection-env <name>] [--key-env <name>]

            Connection strings and base64-encoded 32-byte archive keys are accepted only through
            environment variables (defaults: ANDREJA_PORTABILITY_POSTGRES and ANDREJA_PORTABILITY_KEY).
            Apply database migrations explicitly before import.
            """);
    }
}
