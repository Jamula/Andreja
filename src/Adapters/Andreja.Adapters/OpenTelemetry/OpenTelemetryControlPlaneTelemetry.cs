using System.Diagnostics;
using Andreja.Platform.Contracts.Observability;

namespace Andreja.Adapters.OpenTelemetry;

public sealed class OpenTelemetryControlPlaneTelemetry : IControlPlaneTelemetry
{
    public IDisposable? StartOperation(string operationName, string artifactKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKind);

        var activity = AndrejaTelemetry.ActivitySource.StartActivity(
            operationName,
            ActivityKind.Internal);
        activity?.SetTag("andreja.operation", operationName);
        activity?.SetTag("andreja.artifact.kind", artifactKind);
        return activity;
    }

    public void RecordProposalEvent(string eventName, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Activity.Current?.SetTag("andreja.outcome", outcome);
        var tag = new KeyValuePair<string, object?>("andreja.outcome", outcome);
        switch (eventName)
        {
            case "proposal_created":
                AndrejaTelemetry.ProposalsCreated.Add(1, tag);
                break;
            case "proposal_confirmed":
                AndrejaTelemetry.ProposalsConfirmed.Add(1, tag);
                break;
            case "proposal_expired":
                AndrejaTelemetry.ProposalsExpired.Add(1, tag);
                break;
            case "idempotent_replay":
                AndrejaTelemetry.ProposalsIdempotentReplay.Add(1, tag);
                break;
        }
    }

    public void RecordAuthorizationDenied(string artifactKind, string denialCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(denialCode);

        Activity.Current?.SetTag("andreja.outcome", "denied");
        Activity.Current?.SetTag("andreja.denial.code", denialCode);
        AndrejaTelemetry.ExecutionDenied.Add(
            1,
            new KeyValuePair<string, object?>("andreja.artifact.kind", artifactKind),
            new KeyValuePair<string, object?>("andreja.denial.code", denialCode));
    }

    public void RecordHostInvocation(string hostKind, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Activity.Current?.SetTag("andreja.outcome", outcome);
        AndrejaTelemetry.HostInvocations.Add(
            1,
            new KeyValuePair<string, object?>("andreja.artifact.kind", hostKind),
            new KeyValuePair<string, object?>("andreja.outcome", outcome));
    }
}
