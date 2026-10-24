using System.Globalization;
using System.Text.Json;
using Andreja.PublicSite;

try
{
    if (args.Length == 2 && args[0] == "verify")
    {
        SiteArtifact.Verify(args[1]);
        Console.WriteLine("Local artifact integrity verified; publication remains prohibited.");
        return 0;
    }

    if (args.Length is not (4 or 5) || args[0] != "build")
    {
        Console.Error.WriteLine("Usage: build <content-directory> <config.json> <empty-output-directory> [yyyy-MM-dd] | verify <output-directory>");
        return 2;
    }

    var date = args.Length == 5
        ? DateOnly.ParseExact(args[4], "yyyy-MM-dd", CultureInfo.InvariantCulture)
        : DateOnly.FromDateTime(DateTime.UtcNow);
    SiteArtifact.Build(args[1], args[2], args[3], Directory.GetCurrentDirectory(), date);
    Console.WriteLine("Unpublished local artifact built and verified. Serve only on loopback; noindex is not access control.");
    return 0;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or FormatException or ArgumentException)
{
    Console.Error.WriteLine($"Site rejected: {exception.Message}");
    return 1;
}
