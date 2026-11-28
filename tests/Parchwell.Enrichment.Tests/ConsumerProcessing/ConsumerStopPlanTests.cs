#pragma warning disable CA1707
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerStopPlanTests
{
    // Each case breaks exactly one rule; every other input is valid, so a missing check cannot hide behind another.
    [Theory]
    [InlineData("V1-witness")]
    [InlineData("V2-kill-count")]
    [InlineData("V2-slow-message")]
    [InlineData("V3-unknown-mode")]
    [InlineData("V3-no-mode")]
    [InlineData("V4-mode-without-stop")]
    public async Task Rejects_invalid_stop_inputs(string caseName)
    {
        using var harness = new ConsumerTestHarness();
        var stop = harness.Plan with { StopConfigured = true, ShutdownMode = "close" };
        harness.Plan = caseName switch
        {
            "V1-witness" => stop,
            "V2-kill-count" => stop with { KillCount = 1 },
            "V2-slow-message" => stop with { WindowMs = 120000, SlowMessage = new ConsumerSlowMessage(100, 90000, "inline") },
            "V3-unknown-mode" => stop with { ShutdownMode = "drain" },
            "V3-no-mode" => stop with { ShutdownMode = null },
            _ => harness.Plan with { ShutdownMode = "close" }
        };
        if (caseName == "V1-witness")
        {
            harness.Options.Role = "witness";
            harness.Options.JobName = "run1-c1-r2-consumer-witness";
            harness.Options.PodName = "run1-c1-r2-consumer-witness-abcde";
        }
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_plan_invalid ", harness.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("registration", harness.Calls);
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("dispose")]
    public async Task Accepts_a_valid_stop_plan(string mode)
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { StopConfigured = true, ShutdownMode = mode };
        Assert.Equal(0, await harness.RunAsync());
        Assert.Contains("registration", harness.Calls);
    }
}
#pragma warning restore CA1707
