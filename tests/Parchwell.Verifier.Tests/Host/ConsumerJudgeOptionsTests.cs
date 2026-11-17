using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

/// <summary>Option validation of the consumer pairs for the judge and for the modes that must keep rejecting them.</summary>
#pragma warning disable CA1707
public sealed class ConsumerJudgeOptionsTests
{
    private const string Run = "synthetic-c5";

    public static TheoryData<string, string> ConsumerPairs
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (string scenario in new[] { "C01", "C03", "C10" })
                foreach (string phase in new[] { "C0", "C1" })
                    cases.Add(scenario, phase);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ConsumerPairs))]
    public void Given_ConsumerPairJudge_When_Validated_Then_Succeeds(string scenario, string phase)
    {
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, Judge(scenario, phase, phase));
        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }

    [Theory]
    [InlineData("C3", "C3")]
    [InlineData("C1", "C0")]
    [InlineData("probe", "C0")]
    public void Given_ConsumerScenarioJudgeWithAnotherPhaseOrConfiguration_When_Validated_Then_Fails(string phase, string configuration)
    {
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, Judge("C03", phase, configuration));
        Assert.True(result.Failed);
        Assert.Contains("Verifier:ConfigurationId does not match Verifier:Phase.", result.Failures!);
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("b10-sweep-line")]
    [InlineData("b10-sweep")]
    [InlineData("b10-final-sweep")]
    public void Given_ConsumerScenarioInAModeThatRejectsIt_When_Validated_Then_Fails(string mode)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
        options.ScenarioId = "C03";
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("Verifier:ScenarioId must be A01 or B04 for C5 modes.", result.Failures!);
    }

    [Fact]
    public void Given_ConsumerVerifyWithTheLoadGenJobName_When_Validated_Then_ItDoesNotMatchTheDerivedJob()
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("consumer-verify");
        options.ScenarioId = "C03"; options.RunId = Run; options.JobName = $"{Run}-c0-r1-loadgen";
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains("Verifier:JobName does not match the derived LoadGen Job.", result.Failures!);
    }

    private static VerifierHostOptions Judge(string scenario, string phase, string configuration)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("judge");
        options.ScenarioId = scenario; options.RunId = Run; options.Phase = phase; options.ConfigurationId = configuration;
        options.Repeat = null; options.RepeatPrefix = null; options.ExpectedInstanceId = null;
        options.CandidateOutputName = $"{Run}/{phase}/candidate.json";
        options.LoadGenImage = "loadgen:tag@sha256:" + new string('b', 64); options.VerifierImage = "verifier:tag@sha256:" + new string('c', 64);
        options.JobName = PhaseIdentity.JudgeJobName(Run, phase);
        return options;
    }
}
