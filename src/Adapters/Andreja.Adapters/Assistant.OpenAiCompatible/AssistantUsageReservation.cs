namespace Andreja.Adapters.Assistant.OpenAiCompatible;

internal sealed class AssistantUsageReservation : IDisposable
{
    public static AssistantUsageReservation Unmetered { get; } = new();
    private AssistantUsageBudget? budget;
    private readonly long reservedUnits;
    private long consumedUnits;

    private AssistantUsageReservation()
    {
    }

    public AssistantUsageReservation(AssistantUsageBudget budget, long reservedUnits)
    {
        this.budget = budget;
        this.reservedUnits = reservedUnits;
    }

    public void Commit(long units)
    {
        consumedUnits = units;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref budget, null)?.Complete(reservedUnits, consumedUnits);
    }
}
