using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace MSTestMtpSpike;

[TestClass]
public sealed class TimeoutProbe(TestContext testContext)
{
    [TestMethod]
    [Timeout(250, CooperativeCancellation = true)]
    public async Task TimeoutEnforcementProbe() =>
        await Task.Delay(TimeSpan.FromSeconds(30), testContext.CancellationToken);
}
