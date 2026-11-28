namespace Andreja.Platform.Contracts.Observability;

public interface IControlPlaneTelemetry
{
    IDisposable? StartOperation(string operationName, string artifactKind);

    void RecordProposalEvent(string eventName, string outcome);

    void RecordAuthorizationDenied(string artifactKind, string denialCode);

    void RecordHostInvocation(string hostKind, string outcome);
}

public sealed class NullControlPlaneTelemetry : IControlPlaneTelemetry
{
    public static NullControlPlaneTelemetry Instance { get; } = new();

    private NullControlPlaneTelemetry()
    {
    }

    public IDisposable? StartOperation(string operationName, string artifactKind) => null;

    public void RecordProposalEvent(string eventName, string outcome)
    {
    }

    public void RecordAuthorizationDenied(string artifactKind, string denialCode)
    {
    }

    public void RecordHostInvocation(string hostKind, string outcome)
    {
    }
}
