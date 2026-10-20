using System.Collections.Immutable;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.ProbeGate;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ProbeGateEvaluatorTests
{
    internal static Parchwell.Scenarios.ProbeGate Gate() => new("C0", new("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer),
        ProbeOperator.GreaterThan, 20_000, 5, ["C0", "C2", "C3"]);
    internal static RunIdentity[] Expected(int count = 5) => Enumerable.Range(1, count).Select(ProbeGateEvidenceBuilder.Identity).ToArray();
    internal static ProbeGateInput[] Inputs(int count = 5) => Enumerable.Range(1, count).Select(index =>
        new ProbeGateInput($"synthetic-path-{index}.json", ProbeGateInputStatus.Readable,
            ProbeGateEvidenceBuilder.Result(ProbeGateEvidenceBuilder.Identity(index), qualifying: index == 1))).ToArray();
    private static ProbeGateResult Evaluate(ProbeGateInput[]? inputs = null, RunIdentity[]? expected = null) =>
        ProbeGateEvaluator.Evaluate(new(Gate(), expected ?? Expected(), inputs ?? Inputs()), "synthetic-commit");

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Given_ExpectedCardinality_When_Evaluating_Then_RequiresExactlyFive(int count)
    {
        ProbeGateResult result = Evaluate(expected: Expected(count));
        Assert.Equal(count == 5 ? "pass" : "inconclusive", result.Outcome);
        if (count == 5) Assert.Empty(result.Reasons);
        else Assert.Equal(["expected_repeat_count"], result.Reasons);
        Assert.Equal(count == 5 ? 5 : 0, result.Repeats.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Given_ActualCardinality_When_Evaluating_Then_NeverSelectsFiveOfSix(int count)
    {
        ProbeGateResult result = Evaluate(Inputs(count));
        Assert.Equal(count == 5 ? "pass" : "inconclusive", result.Outcome);
        if (count == 5) Assert.Empty(result.Reasons);
        else Assert.Equal(["actual_repeat_count"], result.Reasons);
        Assert.Equal(count == 5 ? 5 : 0, result.Repeats.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Given_BothWrongCounts_When_Evaluating_Then_ListsBothReasonsInOrder(int count)
    {
        ProbeGateResult result = Evaluate(Inputs(count), Expected(count));
        Assert.Equal(["expected_repeat_count", "actual_repeat_count"], result.Reasons);
        Assert.Empty(result.Repeats);
    }

    [Fact]
    public void Given_AllFourRootFailures_When_Evaluating_Then_RetainsEveryReasonWithoutReadingRepeats()
    {
        RunIdentity[] expected = Expected(6);
        expected[1] = expected[0];
        expected[5] = expected[5] with { ConfigurationId = "C2" };
        ProbeGateResult result = Evaluate(Inputs(4), expected);
        Assert.Equal("inconclusive", result.Outcome);
        Assert.Equal(["expected_repeat_count", "actual_repeat_count", "duplicate_expected_run", "foreign_expected_run"], result.Reasons);
        Assert.Empty(result.Repeats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_DuplicateExpectedIdentityOrRunId_When_Evaluating_Then_RejectsBeforeRepeatJudgement(bool differentIdentity)
    {
        RunIdentity[] expected = Expected();
        expected[1] = differentIdentity ? expected[1] with { RunId = expected[0].RunId } : expected[0];
        ProbeGateResult result = Evaluate(expected: expected);
        Assert.Equal(["duplicate_expected_run"], result.Reasons);
        Assert.Empty(result.Repeats);
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("configuration")]
    public void Given_ForeignExpectedIdentity_When_Evaluating_Then_UsesRootReason(string field)
    {
        RunIdentity[] expected = Expected();
        expected[0] = field == "scenario" ? expected[0] with { ScenarioId = "A02" } : expected[0] with { ConfigurationId = "C2" };
        ProbeGateResult result = Evaluate(expected: expected);
        Assert.Equal(["foreign_expected_run"], result.Reasons);
        Assert.Empty(result.Repeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20_000)]
    public void Given_FiveUsableNonqualifyingObservations_When_Evaluating_Then_IsUntestable(long maximum)
    {
        ProbeGateInput[] inputs = Inputs();
        for (int index = 0; index < inputs.Length; index++)
            inputs[index] = inputs[index] with { Result = inputs[index].Result! with
                { ProbeObservation = new(true, false, "broker", 0, 1, maximum) } };
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal("untestable_at_build_scale", result.Outcome);
        Assert.Empty(result.Reasons);
        Assert.All(result.Repeats, repeat => Assert.Equal("usable", repeat.Reason));
    }

    [Fact]
    public void Given_OneQualifyingRepeat_When_Evaluating_Then_PassesWithInputOrderRetained()
    {
        ProbeGateInput[] inputs = Inputs();
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal("pass", result.Outcome);
        Assert.Empty(result.Reasons);
        Assert.Equal(inputs.Select(input => input.Path), result.Repeats.Select(repeat => repeat.Path));
        Assert.All(result.Repeats, repeat => Assert.Equal("usable", repeat.Reason));
    }

    [Theory]
    [InlineData("missing", "missing_result")]
    [InlineData("unreadable", "unreadable_result")]
    [InlineData("foreign", "foreign_identity")]
    [InlineData("duplicate", "duplicate_identity")]
    [InlineData("unusable", "unusable_repeat")]
    [InlineData("absent", "unusable_repeat")]
    public void Given_OneUnacceptableRepeat_When_Evaluating_Then_IsInconclusive(string fault, string reason)
    {
        ProbeGateInput[] inputs = Inputs();
        VerifierResult first = inputs[0].Result!;
        inputs[0] = fault switch
        {
            "missing" => inputs[0] with { Status = ProbeGateInputStatus.Missing },
            "unreadable" => inputs[0] with { Status = ProbeGateInputStatus.Unreadable },
            "foreign" => inputs[0] with { Result = first with { Identity = first.Identity with { RunId = "foreign" } } },
            "duplicate" => inputs[0] with { Result = inputs[1].Result },
            "absent" => inputs[0] with { Result = first with { ProbeObservation = null } },
            _ => inputs[0] with { Result = first with { ProbeObservation = new(false, true, "broker", 3, 1, 20_001) } }
        };
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal("inconclusive", result.Outcome);
        Assert.Empty(result.Reasons);
        Assert.Equal(reason, result.Repeats[0].Reason);
        Assert.Equal(5, result.Repeats.Count);
        if (fault is "missing" or "unreadable")
        {
            Assert.Null(result.Repeats[0].Identity);
            Assert.Null(result.Repeats[0].Observation);
            Assert.False(result.Repeats[0].Usable);
            Assert.False(result.Repeats[0].Qualifying);
        }
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("configuration")]
    [InlineData("run")]
    [InlineData("instance")]
    [InlineData("expected-instance")]
    [InlineData("client")]
    public void Given_OneIdentityFieldDiffers_When_Evaluating_Then_IsForeign(string field)
    {
        ProbeGateInput[] inputs = Inputs();
        VerifierResult first = inputs[0].Result!;
        RunIdentity identity = first.Identity;
        RunIdentity changed = field switch
        {
            "scenario" => identity with { ScenarioId = "a01" },
            "configuration" => identity with { ConfigurationId = "c0" },
            "run" => identity with { RunId = "Probe-1" },
            "instance" => identity with { InstanceId = "Instance-1" },
            "expected-instance" => identity with { ExpectedInstanceId = "Instance-1" },
            _ => identity with { ClientId = "Synthetic-producer" }
        };
        inputs[0] = inputs[0] with { Result = first with { Identity = changed } };
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal("inconclusive", result.Outcome);
        Assert.Equal("foreign_identity", result.Repeats[0].Reason);
    }

    [Fact]
    public void Given_RepeatedForeignAndMatchingUnusableDuplicates_When_Evaluating_Then_UsesApprovedPrecedence()
    {
        ProbeGateInput[] inputs = Inputs();
        VerifierResult foreign = inputs[0].Result! with { Identity = ProbeGateEvidenceBuilder.Identity(99), ProbeObservation = null };
        inputs[0] = inputs[0] with { Result = foreign };
        inputs[1] = inputs[1] with { Result = foreign };
        inputs[2] = inputs[2] with { Result = inputs[3].Result! with { ProbeObservation = new(false, true, null, null, null, null) } };
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal(["foreign_identity", "foreign_identity", "duplicate_identity", "duplicate_identity", "usable"], result.Repeats.Select(repeat => repeat.Reason));
    }

    [Fact]
    public void Given_InvalidStatusAndInconsistentReferences_When_Evaluating_Then_TrustsSuppliedFlags()
    {
        ProbeGateInput[] inputs = Inputs();
        inputs[0] = inputs[0] with { Result = inputs[0].Result! with
        { ProbeObservation = new(true, true, null, -999, -1, 0), A01RecordedEvidence = new([], []) } };
        ProbeGateResult result = Evaluate(inputs);
        Assert.Equal("pass", result.Outcome);
        Assert.All(result.Repeats, repeat => Assert.Equal("usable", repeat.Reason));
        Assert.Equal(-999, result.Repeats[0].Observation!.ReportIndex);
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("measurement")]
    [InlineData("role")]
    [InlineData("operator")]
    [InlineData("threshold")]
    [InlineData("repeats")]
    [InlineData("full-configurations")]
    [InlineData("full-order")]
    public void Given_UnsupportedGateShape_When_CheckingSupport_Then_Rejects(string field)
    {
        Parchwell.Scenarios.ProbeGate gate = Gate();
        gate = field switch
        {
            "configuration" => gate with { Configuration = "C2" },
            "measurement" => gate with { Measurement = gate.Measurement with { Id = "stats.broker.rtt.p99" } },
            "role" => gate with { Measurement = gate.Measurement with { Role = KafkaClientRole.VerifierConsumer } },
            "operator" => gate with { Operator = (ProbeOperator)99 },
            "threshold" => gate with { ThresholdMs = 20_001 },
            "repeats" => gate with { Repeats = 6 },
            "full-order" => gate with { Configurations = ["C0", "C3", "C2"] },
            _ => gate with { Configurations = ["C0", "C2"] }
        };
        Assert.False(ProbeGateEvaluator.IsSupportedGate("A01", gate));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ProbeGateEvaluator.Evaluate(new(gate, Expected(), Inputs()), "commit"));
        Assert.Equal("The selected scenario has no supported probe gate.", exception.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Given_GateRepeatCountAroundSupportedValue_When_CheckingSupport_Then_RequiresExactlyFive(int repeats)
    {
        Parchwell.Scenarios.ProbeGate gate = Gate() with { Repeats = repeats };
        Assert.Equal(repeats == 5, ProbeGateEvaluator.IsSupportedGate("A01", gate));
        if (repeats != 5)
            Assert.Throws<InvalidOperationException>(() => ProbeGateEvaluator.Evaluate(new(gate, Expected(), Inputs()), "commit"));
    }

    [Fact]
    public void Given_NoGateOrDifferentScenario_When_CheckingSupport_Then_Rejects()
    {
        Assert.False(ProbeGateEvaluator.IsSupportedGate("A01", new NoGate()));
        Assert.False(ProbeGateEvaluator.IsSupportedGate("A02", Gate()));
        Assert.True(ProbeGateEvaluator.IsSupportedGate("A01", Gate()));
    }

    [Theory]
    [InlineData(19_999)]
    [InlineData(20_000)]
    [InlineData(20_001)]
    public void Given_GateThresholdAroundSupportedValue_When_CheckingSupport_Then_RequiresExactDefinition(int threshold)
    {
        Assert.Equal(threshold == 20_000, ProbeGateEvaluator.IsSupportedGate("A01", Gate() with { ThresholdMs = threshold }));
    }

    [Fact]
    public void Given_GateProjection_When_Writing_Then_ContainsNoRunStatusVerdictOrTotals()
    {
        using JsonDocument json = JsonDocument.Parse(ProbeGateResultCodec.Write(Evaluate(), 100_000));
        Assert.Equal(["schema_version", "scenario_id", "outcome", "reasons", "repeats", "verifier_commit"], json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("A01", json.RootElement.GetProperty("scenario_id").GetString());
        JsonElement observation = json.RootElement.GetProperty("repeats")[0].GetProperty("observation");
        Assert.Equal(3, observation.GetProperty("report_index").GetInt64());
        Assert.False(observation.TryGetProperty("report_timestamp", out _));
    }
}
#pragma warning restore CA1707
