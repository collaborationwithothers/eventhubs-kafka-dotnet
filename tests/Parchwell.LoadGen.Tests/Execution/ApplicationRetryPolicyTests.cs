using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class ApplicationRetryPolicyTests
{
    [Theory]
    [InlineData("B10", 1)]
    [InlineData("B04", 0)]
    [InlineData("b10", 0)]
    [InlineData("A01", 0)]
    public void Given_ScenarioId_When_SelectingPolicy_Then_OnlyB10Retries(string scenarioId, int expected)
    {
        Assert.Equal(expected, ApplicationRetryPolicy.ForScenario(scenarioId).MaxRetriesPerEvent);
    }

    [Theory]
    [InlineData("callback", false, -192, 0, true)]
    [InlineData("callback", false, -192, 1, true)]
    [InlineData("callback", false, -192, 2, false)]
    [InlineData("callback", false, -192, null, false)]
    [InlineData("callback", false, -195, 1, false)]
    [InlineData("callback", true, 0, 2, false)]
    [InlineData("synchronous_rejection", false, -192, null, false)]
    public void Given_LedgerRow_When_Classified_Then_OnlyPermittedTimeoutsRetry(string source, bool delivered,
        int errorCode, int? status, bool expected)
    {
        var row = new LedgerRow(1, new RunIdentity("B10", "baseline", "run-1", "instance-1", "instance-1", "loadgen"),
            new AttemptEntry("instance-1", "instance-1-0", 0, "tenant", "d0", "tenant-d0", 0, "uploaded", 0, 0),
            source, delivered, errorCode, null, status, status.HasValue ? "native_callback" : "missing", 0, 1, null);

        Assert.Equal(expected, ApplicationRetryPolicy.IsPermittedTimeout(row));
    }
}
