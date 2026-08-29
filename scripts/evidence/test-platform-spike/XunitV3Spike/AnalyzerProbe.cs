using Xunit;

namespace XunitV3Spike;

public sealed class AnalyzerProbe
{
    [Fact]
    public void AlwaysTrueAssertion() => Assert.True(true);
}
