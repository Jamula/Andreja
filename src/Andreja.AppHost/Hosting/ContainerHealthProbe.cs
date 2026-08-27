namespace Andreja.AppHost.Hosting;

public static class ContainerHealthProbe
{
    public static async Task<bool> TryRunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--health-check", StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length != 2 ||
            !Uri.TryCreate(args[1], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp ||
            !endpoint.IsLoopback)
        {
            Environment.ExitCode = 2;
            return true;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync(endpoint, cancellationToken);
            Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            Environment.ExitCode = 1;
        }
        catch (TaskCanceledException)
        {
            Environment.ExitCode = 1;
        }

        return true;
    }
}
