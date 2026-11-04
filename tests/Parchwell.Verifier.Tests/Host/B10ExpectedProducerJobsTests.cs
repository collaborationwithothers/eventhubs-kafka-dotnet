using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
// Later repeat consumers get both names while the supplied expected instance remains the first Job.
public sealed class B10ExpectedProducerJobsTests
{
    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    public void Given_ValidB10Repeat_When_OptionsValidated_Then_ExposesFirstAndSecondJobs(string mode)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
        options.ScenarioId = "B10";
        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(["run-1-c0-r1-loadgen", "run-1-c0-r1-loadgen2"], options.ExpectedProducerJobNames);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    public void Given_LastB10C1Repeat_When_OptionsValidated_Then_DerivesBothNamesFromCoordinates(string mode)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
        options.ScenarioId = "B10";
        options.ConfigurationId = "C1";
        options.Phase = "C1";
        options.Repeat = 5;
        options.JobName = mode == "start-line" ? "run-1-c1-r5-start-line" : "run-1-c1-r5-verify";
        options.ExpectedInstanceId = "run-1-c1-r5-loadgen";
        options.InstanceId = "run-1-c1-r5-loadgen";
        options.ClientId = "run-1-c1-r5-loadgen";
        options.RepeatPrefix = "run-1/C1/r5/";
        options.PhasePrefix = "run-1/C1/";
        options.ArtifactPrefix = PhasePaths.ProducersPrefix("run-1", "C1", 5);
        options.StartLineInputName = PhasePaths.StartLine("run-1", "C1", 5);
        options.StartLineOutputName = PhasePaths.StartLine("run-1", "C1", 5);
        options.BrokerInventoryOutputName = PhasePaths.BrokerInventory("run-1", "C1", 5);
        options.ResultOutputName = PhasePaths.Result("run-1", "C1", 5);
        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(["run-1-c1-r5-loadgen", "run-1-c1-r5-loadgen2"], options.ExpectedProducerJobNames);
    }

    [Theory]
    [InlineData("start-line", "A01")]
    [InlineData("start-line", "B04")]
    [InlineData("start-line", "B06")]
    [InlineData("start-line", "B07")]
    [InlineData("verify", "A01")]
    [InlineData("verify", "B04")]
    [InlineData("verify", "B06")]
    [InlineData("verify", "B07")]
    public void Given_OtherScenario_When_OptionsValidated_Then_ExposesOnlyFirstJob(string mode, string scenario)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
        options.ScenarioId = scenario;
        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(["run-1-c0-r1-loadgen"], options.ExpectedProducerJobNames);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    public void Given_ExpectedInstanceNamesSecondJob_When_OptionsValidated_Then_Rejects(string mode)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
        options.ScenarioId = "B10";
        options.ExpectedInstanceId = "run-1-c0-r1-loadgen2";
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, options);
        Assert.False(result.Succeeded);
        Assert.Contains("Verifier:ExpectedInstanceId must equal the derived LoadGen Job name.", result.Failures!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Given_InvalidRepeat_When_OptionsValidated_Then_RejectsBeforeDerivedCollectionUse(int repeat)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("verify");
        options.ScenarioId = "B10";
        options.Repeat = repeat;
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, options);
        Assert.False(result.Succeeded);
        Assert.Contains("Verifier:Repeat must be between 1 and 5.", result.Failures!);
    }
}
#pragma warning restore CA1707
