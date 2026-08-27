extern alias PortabilityCli;

using Npgsql;
using PortabilityCommand = PortabilityCli::PortabilityCommand;

namespace Andreja.UnitTests;

public sealed class PortabilityCommandTests
{
    [Theory]
    [MemberData(nameof(InfrastructureFailures))]
    public void InfrastructureFailuresUseFixedContentMinimizedMessages(
        Exception exception,
        string expected)
    {
        var message = PortabilityCommand.GetFailureMessage(exception);

        Assert.Equal(expected, message);
        Assert.DoesNotContain("CANARY-SENSITIVE", message, StringComparison.Ordinal);
    }

    public static TheoryData<Exception, string> InfrastructureFailures() =>
        new()
        {
            {
                new IOException("CANARY-SENSITIVE path"),
                "Portability operation failed: file access failed."
            },
            {
                new NpgsqlException("CANARY-SENSITIVE database detail"),
                "Portability operation failed: database access failed."
            },
        };
}
