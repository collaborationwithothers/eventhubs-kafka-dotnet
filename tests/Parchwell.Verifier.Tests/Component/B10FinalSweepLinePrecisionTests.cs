using System.Text.Json;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707 // Repository test naming convention.
// Issue #483: run 43635822755 built its sweep line from whole-second pod finish times and every repeat failed
// b10_final_sweep_line_invalid. These tests run the real final sweep and Judge on a sweep line built that way.
[Trait("Tier", "Docker")]
public sealed class B10FinalSweepLinePrecisionTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_WholeSecondPodTimeRaisedToItsLastTick_When_FinalSweepAndJudgeRun_Then_CandidateIsCompleteValid(string phase)
    {
        using B10FinalSweepHarness fixture = phase == "C0" ? await B10FinalSweepHarness.CreateC0Async(kafka) : await B10FinalSweepHarness.CreateAsync(kafka);
        await fixture.CaptureSweepFromPodSecondsAsync(raise: true);
        Assert.Equal(0, await fixture.RunAsync());
        using JsonDocument projection = JsonDocument.Parse(fixture.Verifier[PhasePaths.B10FinalSweep(B10FinalSweepHarness.Run, phase)]);
        Assert.True(projection.RootElement.GetProperty("data").GetProperty("scan_complete").GetBoolean());
        fixture.Configuration["Verifier:Mode"] = "judge";
        fixture.Configuration["Verifier:JobName"] = PhaseIdentity.JudgeJobName(B10FinalSweepHarness.Run, phase);
        fixture.Configuration["Verifier:CandidateOutputName"] = PhasePaths.Candidate(B10FinalSweepHarness.Run, phase);
        Assert.Equal(0, await fixture.RunAsync());
        using JsonDocument candidate = JsonDocument.Parse(fixture.Verifier[PhasePaths.Candidate(B10FinalSweepHarness.Run, phase)]);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        Assert.All(candidate.RootElement.GetProperty("repeats").EnumerateArray(),
            repeat => Assert.Equal("complete_valid", repeat.GetProperty("status").GetString()));
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_WholeSecondPodTimeNotRaised_When_FinalSweepRuns_Then_EveryRepeatFailsTheSweepLine(string phase)
    {
        using B10FinalSweepHarness fixture = phase == "C0" ? await B10FinalSweepHarness.CreateC0Async(kafka) : await B10FinalSweepHarness.CreateAsync(kafka);
        DateTimeOffset receipt = await fixture.CaptureSweepFromPodSecondsAsync(raise: false);
        // A receipt exactly on a whole second would not reproduce the live failure.
        Assert.NotEqual(0, receipt.UtcTicks % TimeSpan.TicksPerSecond);
        await fixture.RunAsync();
        using JsonDocument projection = JsonDocument.Parse(fixture.Verifier[PhasePaths.B10FinalSweep(B10FinalSweepHarness.Run, phase)]);
        JsonElement data = projection.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("scan_complete").GetBoolean());
        Assert.All(data.GetProperty("repeats").EnumerateArray(), repeat =>
            Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(), reason => reason.GetString() == "b10_final_sweep_line_invalid"));
    }
}
#pragma warning restore CA1707
