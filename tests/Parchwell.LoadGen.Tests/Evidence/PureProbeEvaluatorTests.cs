using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class PureProbeEvaluatorTests
{
    [Fact]
    public void Given_FiveAttributedWindowsWithThrottleAboveThreshold_When_Evaluating_Then_PassesEvenWhenOracleIsInvalid()
    {
        ProbeEvaluationResult result = PureProbeEvaluator.Evaluate(Gate(), "A01",
            Enumerable.Range(1, 5).Select(index => Repeat(index, 20_001, oracleValid: false)).ToImmutableArray());

        Assert.Equal(ProbeEvaluationStatus.Pass, result.Status);
    }

    [Fact]
    public void Given_FiveUsableWindowsWithoutThrottleAboveThreshold_When_Evaluating_Then_IsUntestableAtBuildScale()
    {
        ProbeEvaluationResult result = PureProbeEvaluator.Evaluate(Gate(), "A01",
            Enumerable.Range(1, 5).Select(index => Repeat(index, 20_000)).ToImmutableArray());

        Assert.Equal(ProbeEvaluationStatus.UntestableAtBuildScale, result.Status);
    }

    [Fact]
    public void Given_EmptyWindowOrMissingRepeat_When_Evaluating_Then_IsInconclusiveWithoutZeroFabrication()
    {
        var repeats = Enumerable.Range(1, 5).Select(index => Repeat(index, index == 2 ? null : 0)).ToImmutableArray();

        ProbeEvaluationResult result = PureProbeEvaluator.Evaluate(Gate(), "A01", repeats[..4]);

        Assert.Equal(ProbeEvaluationStatus.Inconclusive, result.Status);
        Assert.Contains("required_repeat_count_mismatch", result.Diagnostics);
    }

    [Fact]
    public void Given_FiveReportsWithOnlyEmptyThrottleWindows_When_Evaluating_Then_IsInconclusive()
    {
        ImmutableArray<ProbeRepeatEvidence> repeats = Enumerable.Range(1, 5).Select(index => Repeat(index, null)).ToImmutableArray();

        ProbeEvaluationResult result = PureProbeEvaluator.Evaluate(Gate(), "A01", repeats);

        Assert.Equal(ProbeEvaluationStatus.Inconclusive, result.Status);
        Assert.Contains("repeat_has_no_nonempty_throttle_window", result.Diagnostics);
    }

    [Fact]
    public void Given_DuplicateRunIdentityOrWrongConfiguration_When_Evaluating_Then_IsInconclusive()
    {
        ImmutableArray<ProbeRepeatEvidence> duplicate = Enumerable.Range(1, 5).Select(index => Repeat(index == 5 ? 4 : index, 25_000)).ToImmutableArray();
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, PureProbeEvaluator.Evaluate(Gate(), "A01", duplicate).Status);

        ImmutableArray<ProbeRepeatEvidence> wrongConfiguration = Enumerable.Range(1, 5)
            .Select(index => Repeat(index, 25_000) with { ConfigurationId = "C2" }).ToImmutableArray();
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, PureProbeEvaluator.Evaluate(Gate(), "A01", wrongConfiguration).Status);
    }

    [Fact]
    public void Given_ProbeRepeatReportsFromOneSession_When_Evaluating_Then_AcceptsThemButRejectsMixedOrEmptySessions()
    {
        ProbeRepeatEvidence repeat = Repeat(1, 25_000);
        KafkaStatisticsReport report = repeat.Reports[0];
        ProbeRepeatEvidence sameSession = repeat with { Reports = [report, report with { ReceivedAt = report.ReceivedAt.AddSeconds(1) }] };
        ImmutableArray<ProbeRepeatEvidence> sameSessionRepeats = Enumerable.Range(1, 5)
            .Select(index => index == 1 ? sameSession : Repeat(index, 25_000)).ToImmutableArray();
        Assert.Equal(ProbeEvaluationStatus.Pass, PureProbeEvaluator.Evaluate(Gate(), "A01", sameSessionRepeats).Status);

        ProbeRepeatEvidence mixedSession = repeat with { Reports = [report, report with { SessionId = "other-session" }] };
        ImmutableArray<ProbeRepeatEvidence> mixedSessionRepeats = Enumerable.Range(1, 5)
            .Select(index => index == 1 ? mixedSession : Repeat(index, 25_000)).ToImmutableArray();
        ProbeEvaluationResult mixedResult = PureProbeEvaluator.Evaluate(Gate(), "A01", mixedSessionRepeats);
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, mixedResult.Status);
        Assert.Contains("statistics_report_session_mismatch", mixedResult.Diagnostics);

        ProbeRepeatEvidence emptySession = repeat with { Reports = [report with { SessionId = " " }] };
        ImmutableArray<ProbeRepeatEvidence> emptySessionRepeats = Enumerable.Range(1, 5)
            .Select(index => index == 1 ? emptySession : Repeat(index, 25_000)).ToImmutableArray();
        ProbeEvaluationResult emptyResult = PureProbeEvaluator.Evaluate(Gate(), "A01", emptySessionRepeats);
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, emptyResult.Status);
        Assert.Contains("statistics_report_session_mismatch", emptyResult.Diagnostics);
    }

    [Fact]
    public void Given_ProbeRepeatWithConsumerSnapshotOrMixedNativeNames_When_Evaluating_Then_IsInconclusive()
    {
        ProbeRepeatEvidence repeat = Repeat(1, 25_000);
        KafkaStatisticsReport report = repeat.Reports[0];

        ProbeRepeatEvidence consumerSnapshot = repeat with
        {
            Reports = [report with { Snapshot = report.Snapshot! with { Type = "consumer" } }]
        };
        ImmutableArray<ProbeRepeatEvidence> consumerRepeats = Enumerable.Range(1, 5)
            .Select(index => index == 1 ? consumerSnapshot : Repeat(index, 25_000)).ToImmutableArray();
        ProbeEvaluationResult consumerResult = PureProbeEvaluator.Evaluate(Gate(), "A01", consumerRepeats);
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, consumerResult.Status);
        Assert.Contains("statistics_report_client_type_mismatch", consumerResult.Diagnostics);

        ProbeRepeatEvidence mixedNativeNames = repeat with
        {
            Reports = [report, report with { Snapshot = report.Snapshot! with { Name = "another-native-producer" } }]
        };
        ImmutableArray<ProbeRepeatEvidence> mixedNameRepeats = Enumerable.Range(1, 5)
            .Select(index => index == 1 ? mixedNativeNames : Repeat(index, 25_000)).ToImmutableArray();
        ProbeEvaluationResult mixedNameResult = PureProbeEvaluator.Evaluate(Gate(), "A01", mixedNameRepeats);
        Assert.Equal(ProbeEvaluationStatus.Inconclusive, mixedNameResult.Status);
        Assert.Contains("statistics_report_native_name_mismatch", mixedNameResult.Diagnostics);
    }

    private static ProbeGate Gate() => new("C0", new MeasurementRequest("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer),
        ProbeOperator.GreaterThan, 20_000, 5, ["C0", "C2", "C3"]);

    private static ProbeRepeatEvidence Repeat(int repeat, long? throttle, bool oracleValid = true)
    {
        var identity = new RunIdentity("A01", "C0", "probe-" + repeat, "instance-" + repeat, "instance-" + repeat, "loadgen-pod-" + repeat);
        KafkaWindowStatistics window = throttle is null
            ? new KafkaWindowStatistics(0, null)
            : new KafkaWindowStatistics(1, throttle.Value);
        var broker = new KafkaBrokerStatistics("broker-1", 1, "UP", 100, 0, 0, 0, 0, 0, window, new KafkaWindowStatistics(0, null));
        var snapshot = new KafkaStatisticsSnapshot("producer-1", identity.ClientId, "producer", 1, 1, 1, [broker]);
        var report = new KafkaStatisticsReport(new KafkaStatisticsIdentity("A01", identity.RunId, KafkaClientRole.LoadGenProducer, identity.ClientId),
            "stats-session-" + repeat, DateTimeOffset.UnixEpoch, "{}", snapshot, ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
        return new(identity, "A01", "C0", KafkaClientRole.LoadGenProducer, [report], true, oracleValid);
    }
}
