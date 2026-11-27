using System.ComponentModel;

namespace Andreja.Docs;

public static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Execute(args, Console.Out, Console.Error);
    }

    public static int Execute(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        string? repositoryRootOverride = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || HasHelpFlag(args))
        {
            WriteUsage(args.Length == 0 ? stderr : stdout);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            var root = repositoryRootOverride ?? RepositoryPaths.FindRoot();
            var command = args[0];
            if (string.Equals(command, "check-consistency", StringComparison.Ordinal))
            {
                if (args.Length > 1)
                {
                    stderr.WriteLine($"Unknown option for check-consistency: {args[1]}");
                    WriteUsage(stderr);
                    return 2;
                }

                return DocsConsistencyChecker.RunAll(root, stdout);
            }

            if (string.Equals(command, "check-feedback-readiness", StringComparison.Ordinal))
            {
                if (args.Length > 1)
                {
                    stderr.WriteLine($"Unknown option for check-feedback-readiness: {args[1]}");
                    WriteUsage(stderr);
                    return 2;
                }

                return FeedbackReadinessValidator.RunCheck(root, stdout);
            }

            if (string.Equals(command, "architecture-diagram", StringComparison.Ordinal))
            {
                var check = false;
                var renderPng = false;
                for (var i = 1; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--check", StringComparison.Ordinal))
                    {
                        check = true;
                    }
                    else if (string.Equals(args[i], "--render-png", StringComparison.Ordinal))
                    {
                        renderPng = true;
                    }
                    else
                    {
                        stderr.WriteLine($"Unknown option for architecture-diagram: {args[i]}");
                        WriteUsage(stderr);
                        return 2;
                    }
                }

                var paths = ArchitectureArtifactPaths.FromRepositoryRoot(root);
                return ArchitectureDiagramTool.ExecuteCli(paths, check, renderPng, stdout, stderr);
            }

            stderr.WriteLine($"Unknown subcommand: {command}");
            WriteUsage(stderr);
            return 2;
        }
        catch (Exception ex) when (ex is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or Win32Exception
            or TimeoutException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool HasHelpFlag(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--help", StringComparison.Ordinal)
                || string.Equals(arg, "-h", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project src/Andreja.Docs -- check-consistency");
        writer.WriteLine("  dotnet run --project src/Andreja.Docs -- check-feedback-readiness");
        writer.WriteLine("  dotnet run --project src/Andreja.Docs -- architecture-diagram [--check] [--render-png]");
    }
}
