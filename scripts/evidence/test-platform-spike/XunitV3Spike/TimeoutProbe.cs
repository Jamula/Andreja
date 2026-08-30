using Xunit;

namespace XunitV3Spike;

public sealed class TimeoutProbe
{
    [Fact(Timeout = 250)]
    public async Task TimeoutEnforcementProbe() =>
        await Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
}
