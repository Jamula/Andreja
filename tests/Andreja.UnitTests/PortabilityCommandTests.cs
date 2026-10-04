extern alias PortabilityCli;

using Npgsql;
using PortabilityCommand = PortabilityCli::PortabilityCommand;

namespace Andreja.UnitTests;

[TestClass]
public sealed class PortabilityCommandTests
{
    [TestMethod]
    [DynamicData(nameof(InfrastructureFailures))]
    public void InfrastructureFailuresUseFixedContentMinimizedMessages(
        Exception exception,
        string expected)
    {
        var message = PortabilityCommand.GetFailureMessage(exception);

        Assert.AreEqual(expected, message);
        Assert.DoesNotContain("CANARY-SENSITIVE", message, StringComparison.Ordinal);
    }

    public static IEnumerable<(Exception exception, string expected)> InfrastructureFailures =>
    [
        (
            new IOException("CANARY-SENSITIVE path"),
            "Portability operation failed: file access failed."),
        (
            new NpgsqlException("CANARY-SENSITIVE database detail"),
            "Portability operation failed: database access failed."),
    ];
}
