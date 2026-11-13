using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707 // Repository test naming convention.
// Both scientific configurations carry real per-repeat bytes through the final sweep and Judge entry points.
[Trait("Tier", "Docker")]
public sealed class B10FinalJudgeIntegrationTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    [Theory]
    [InlineData("C0", 0)]
    [InlineData("C1", 0)]
    [InlineData("C1", 2)]
    public async Task Given_RealFinalSweep_When_RealJudgeRuns_Then_CandidateCarriesCheckedRepeatsAndPassesPhaseTransport(string phase, int differentVersionRepeat)
    {
        using B10FinalSweepHarness fixture = phase == "C0" ? await B10FinalSweepHarness.CreateC0Async(kafka) : await B10FinalSweepHarness.CreateAsync(kafka, differentVersionRepeat: differentVersionRepeat);
        Assert.Equal(0, await fixture.RunAsync());
        byte[] projectionBytes = fixture.Verifier[PhasePaths.B10FinalSweep(B10FinalSweepHarness.Run, phase)];
        using JsonDocument projection = JsonDocument.Parse(projectionBytes);
        Assert.True(projection.RootElement.GetProperty("data").GetProperty("scan_complete").GetBoolean());
        fixture.Configuration["Verifier:Mode"] = "judge";
        fixture.Configuration["Verifier:JobName"] = PhaseIdentity.JudgeJobName(B10FinalSweepHarness.Run, phase);
        fixture.Configuration["Verifier:CandidateOutputName"] = PhasePaths.Candidate(B10FinalSweepHarness.Run, phase);
        Assert.Equal(0, await fixture.RunAsync());
        byte[] bytes = fixture.Verifier[PhasePaths.Candidate(B10FinalSweepHarness.Run, phase)];
        using JsonDocument candidate = JsonDocument.Parse(bytes);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        Assert.True(JsonElement.DeepEquals(projection.RootElement.GetProperty("data").GetProperty("repeats"),
            candidate.RootElement.GetProperty("b10_final").GetProperty("repeats")));
        Assert.All(candidate.RootElement.GetProperty("repeats").EnumerateArray(), repeat =>
        {
            Assert.Equal("complete_valid", repeat.GetProperty("status").GetString());
            Assert.True(repeat.GetProperty("classification_final").GetBoolean());
            Assert.True(repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
            Assert.True(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
            Assert.True(repeat.GetProperty("checks").GetProperty("j6_broker_inventory_coverage").GetBoolean());
        });
        if (phase == "C0")
        {
            JsonElement timeoutRule = candidate.RootElement.GetProperty("b10_final").GetProperty("timeout_rule");
            Assert.Equal(5, timeoutRule.GetProperty("complete_valid_final_repeats").GetInt32());
            Assert.Equal(1, timeoutRule.GetProperty("duplicate_total").GetInt64());
            Assert.Equal("confirmed", timeoutRule.GetProperty("rule_selection").GetString());
        }
        JsonElement versions = candidate.RootElement.GetProperty("versions");
        Assert.Equal(B10FinalSweepHarness.ManagedVersion, versions.GetProperty("confluent_kafka").GetString());
        Assert.Equal(B10FinalSweepHarness.DotnetVersion, versions.GetProperty("dotnet").GetString());
        Assert.Equal(differentVersionRepeat == 0 ? B10FinalSweepHarness.NativeVersion : null, versions.GetProperty("librdkafka").GetString());
        Assert.Equal(differentVersionRepeat != 0, candidate.RootElement.GetProperty("reasons").EnumerateArray().Any(reason => reason.GetString() == "version_unavailable"));
        Assert.False(candidate.RootElement.TryGetProperty("b10_sweep", out _));
        await B10FinalJudgeTests.CheckPythonTransportAsync(Encoding.UTF8.GetString(bytes));
    }
}
#pragma warning restore CA1707
