using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class B10ScientificThreeJobOptionsTests
{
    [Theory]
    [InlineData("b10-1-1", "C0", 1)]
    [InlineData("b10-1-1", "C1", 3)]
    [InlineData("b10d-1-1", "C1", 3)]
    [InlineData("run-1", "C0", 2)]
    [InlineData("run-1", "C1", 2)]
    public void Given_RunAndConfiguration_When_ProducerNamesDerived_Then_RetainsEachAuthorizedShape(
        string run, string configuration, int count)
    {
        var options = new VerifierHostOptions
        { ScenarioId = "B10", ConfigurationId = configuration, Phase = configuration, Repeat = 5, RunId = run };
        string first = PhaseIdentity.JobName(run, configuration, 5);
        string[] expected = count switch
        {
            1 => [first],
            3 => [first, first + "-restart", first + "-successor"],
            _ => [first, first + "2"]
        };
        Assert.Equal(expected, options.ExpectedProducerJobNames);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Given_ScientificAbruptRepeat_When_OptionsValidated_Then_RequiresOneThroughFive(int repeat, bool accepted)
    {
        VerifierHostOptions options = AbruptOptions(repeat);
        Assert.Equal(accepted, new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Given_ScientificAbruptWrongRepeatPrefix_When_OptionsValidated_Then_RejectsBeforeReads()
    {
        VerifierHostOptions options = AbruptOptions(3);
        options.ArtifactPrefix = "b10-1-1/C1/r2/producer/";
        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("b10-1-1", true)]
    [InlineData("b10-01-12", true)]
    [InlineData("b10d-1-1", false)]
    [InlineData("b10-1", false)]
    [InlineData("xb10-1-1", false)]
    [InlineData("b10-1-1\n", false)]
    [InlineData(null, false)]
    public void Given_RunIdAtScientificPatternBoundary_When_Recognized_Then_RequiresExactPattern(string? run, bool matches) =>
        Assert.Equal(matches, VerifierHostOptions.IsB10ScientificRunId(run));

    private static VerifierHostOptions AbruptOptions(int repeat)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("verify");
        options.Mode = "b10-abrupt-verify";
        options.ScenarioId = "B10";
        options.ConfigurationId = options.Phase = "C1";
        options.RunId = "b10-1-1";
        options.Repeat = repeat;
        options.ArtifactPrefix = $"b10-1-1/C1/r{repeat}/producer/";
        options.LoadGenImage = "example/loadgen@sha256:" + new string('a', 64);
        options.VerifierImage = "example/verifier@sha256:" + new string('b', 64);
        return options;
    }
}
#pragma warning restore CA1707
