using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestMtpSpike;

[TestClass]
public sealed class AnalyzerProbe
{
    [TestMethod]
    public void AlwaysTrueAssertion() => Assert.IsTrue(true);
}
