namespace Andreja.Adapters.Assistant.OpenAiCompatible;

internal sealed class AssistantUsageBudget(long approvedTotalUnits)
{
    private readonly object sync = new();
    private long consumed;
    private long reserved;

    public AssistantUsageReservation? TryReserve(long maximumUnits)
    {
        lock (sync)
        {
            if (maximumUnits <= 0
                || maximumUnits > approvedTotalUnits - consumed - reserved)
            {
                return null;
            }

            reserved += maximumUnits;
            return new(this, maximumUnits);
        }
    }

    internal void Complete(long reservedUnits, long consumedUnits)
    {
        lock (sync)
        {
            reserved -= reservedUnits;
            consumed = checked(consumed + Math.Min(consumedUnits, reservedUnits));
        }
    }
}
