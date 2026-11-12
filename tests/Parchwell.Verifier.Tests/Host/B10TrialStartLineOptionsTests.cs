using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
// Trial recognition changes only the B10 expected Job list; scientific names retain their original shape.
public sealed class B10TrialStartLineOptionsTests
{
    [Theory]
    [InlineData("b10d-1-1", true)]
    [InlineData("b10d-01-1", true)]
    [InlineData("b10d-1", false)]
    [InlineData("xb10d-1-1", false)]
    [InlineData("b10-1-1", false)]
    public void Given_RunIdAtTrialPatternBoundary_When_StartLineOptionsValidated_Then_DerivesExpectedJobs(
        string runId, bool trial)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("start-line");
        options.ScenarioId = "B10";
        options.ConfigurationId = "C1";
        options.RunId = runId;
        options.Phase = "C1";
        options.Repeat = 1;
        string first = PhaseIdentity.JobName(runId, "C1", 1);
        options.JobName = PhaseIdentity.StartLineJobName(runId, "C1", 1);
        options.ExpectedInstanceId = first;
        options.InstanceId = first;
        options.ClientId = first;
        options.RepeatPrefix = $"{runId}/C1/r1/";
        options.PhasePrefix = $"{runId}/C1/";
        options.StartLineOutputName = PhasePaths.StartLine(runId, "C1", 1);
        options.BrokerInventoryOutputName = PhasePaths.BrokerInventory(runId, "C1", 1);

        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
        string[] expected = trial ? [first, first + "-restart", first + "-successor"] : [first, first + "2"];
        Assert.Equal(expected, options.ExpectedProducerJobNames);
    }

    [Theory]
    [InlineData("A01")]
    [InlineData("B04")]
    [InlineData("B06")]
    [InlineData("B07")]
    public void Given_TrialShapedRunIdForOtherScenario_When_NamesDerived_Then_RetainsOneJob(string scenario)
    {
        var options = new VerifierHostOptions { ScenarioId = scenario, RunId = "b10d-1-1", Phase = "C1", Repeat = 1 };
        Assert.Equal(["b10d-1-1-c1-r1-loadgen"], options.ExpectedProducerJobNames);
    }
}
#pragma warning restore CA1707
