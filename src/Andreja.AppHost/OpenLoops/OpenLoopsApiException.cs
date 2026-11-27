namespace Andreja.AppHost.OpenLoops;

public sealed class OpenLoopsApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
