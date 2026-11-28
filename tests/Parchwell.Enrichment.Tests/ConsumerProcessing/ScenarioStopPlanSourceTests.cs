#pragma warning disable CA1707
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

// Protects the production consumer plan source for C18 (the packaged scenario file, the subject and witness plans).
public sealed class ScenarioStopPlanSourceTests
{
    private static ScenarioConsumerProcessingPlanSource Source(ConsumerProcessingOptions options) =>
        new(Microsoft.Extensions.Options.Options.Create(options), AppContext.BaseDirectory);

    [Theory]
    [InlineData("subject", "C0", "close")]
    [InlineData("subject", "C1", "dispose")]
    [InlineData("witness", "C0", null)]
    [InlineData("witness", "C1", null)]
    public void Given_C18_role_and_phase_When_asked_for_the_plan_Then_it_carries_the_stop_and_records_consumer_errors(string role, string phase, string? mode)
    {
        ConsumerProcessingPlan plan = Source(ScenarioPlanSourceTests.Options("C18", phase, role)).GetPlan("C18", phase)!;
        Assert.Equal(phase, plan.ConfigurationId);
        Assert.Equal(role == "subject", plan.StopConfigured);
        Assert.Equal(mode, plan.ShutdownMode);
        Assert.True(plan.RecordsConsumerErrors);
        Assert.Null(plan.KillCount);
        Assert.Null(plan.SlowMessage);
        Assert.Empty(plan.ConsumerSettings);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
    }

    [Theory]
    [InlineData("C2")]
    [InlineData("probe")]
    public void Given_a_configuration_C18_does_not_have_When_asked_for_the_plan_Then_it_returns_null(string phase) =>
        Assert.Null(Source(ScenarioPlanSourceTests.Options("C18", "C0", "subject")).GetPlan("C18", phase));
}
