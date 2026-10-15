using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Counter deltas serve monitoring; raw cumulative values stay separate as attributable client evidence.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsCounterTests
{
    private const string MeterName = "Parchwell.Kafka.Statistics";
    private const string Scenario = "counter-edge-scenario";

    [Fact]
    public async Task Given_CumulativeCountersAdvance_When_ReportsAreProcessed_Then_EmitsExactFirstAndSubsequentDeltas()
    {
        const string runId = "counter-delta-run";
        using var listener = new CounterMetricsListener(runId);
        using var metrics = new KafkaStatisticsMetrics();
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        await using var session = NewSession(clock, sink, metrics, runId, "counter-delta-client");

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("counter-delta-client", 11_000_000, 1_000_000, 4, 2));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("counter-delta-client", 11_500_000, 1_500_000, 7, 5));
        await sink.WaitForReportsAsync(2);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("counter-delta-client", 12_000_000, 2_000_000, 10, 8));
        await sink.WaitForReportsAsync(3);

        Assert.Equal([3L, 3L, 4L], listener.Values("kafka.broker.req_timeouts"));
        Assert.Equal([2L, 3L, 3L], listener.Values("kafka.broker.disconnects"));
        Assert.All(listener.Measurements, item =>
        {
            Assert.Equal("counter-delta-client", item.Tags["client_id"]);
            Assert.Equal(runId, item.Tags["run"]);
            Assert.Equal("broker/1", item.Tags["broker"]);
        });
    }

    [Fact]
    public async Task Given_GuardOnlyCounterResets_When_CountersLaterRecover_Then_SessionRemainsIncompleteAndQuarantined()
    {
        const string runId = "guard-reset-run";
        using var metrics = new KafkaStatisticsMetrics();
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        await using var session = NewSession(clock, sink, metrics, runId, "guard-reset-client");

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(WithGuardCounters(StatisticsTestSupport.Report("guard-reset-client", 11_000_000, 1_000_000), 8, 4, 3));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(WithGuardCounters(StatisticsTestSupport.Report("guard-reset-client", 11_500_000, 1_500_000), 7, 5, 4));
        await sink.WaitForReportsAsync(2);
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(["broker/1"], TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(WithGuardCounters(StatisticsTestSupport.Report("guard-reset-client", 12_900_000, 2_900_000), 9, 6, 5));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "counter_reset");
        KafkaStatisticsReport[] reports = sink.Reports.ToArray();
        Assert.Equal(3, reports.Length);
        Assert.DoesNotContain(reports[0].Diagnostics, item => item.Code == "counter_reset");
        Assert.Contains(reports[1].Diagnostics, item => item.Code == "counter_reset");
        Assert.Contains(reports[2].Diagnostics, item => item.Code == "counter_reset");
    }

    [Fact]
    public async Task Given_SeparateClientsAndSessions_When_CountersAreProcessed_Then_DeltasAndResetsStayIsolated()
    {
        const string firstRun = "isolation-run-first";
        const string secondRun = "isolation-run-second";
        using var listener = new CounterMetricsListener(firstRun, secondRun);
        using var metrics = new KafkaStatisticsMetrics();
        var clock = new ManualTimeProvider();
        var firstSink = new RecordingStatisticsSink();
        var secondSink = new RecordingStatisticsSink();
        await using var first = NewSession(clock, firstSink, metrics, firstRun, "isolation-client-first");
        await using var second = NewSession(clock, secondSink, metrics, secondRun, "isolation-client-second");

        clock.Advance(TimeSpan.FromSeconds(2));
        first.Capture(StatisticsTestSupport.Report("isolation-client-first", 11_000_000, 1_000_000, 8, 5));
        second.Capture(StatisticsTestSupport.Report("isolation-client-second", 11_000_000, 1_000_000, 3, 2));
        await firstSink.WaitForReportsAsync(1);
        await secondSink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        first.Capture(StatisticsTestSupport.Report("isolation-client-first", 11_500_000, 1_500_000, 2, 6));
        second.Capture(StatisticsTestSupport.Report("isolation-client-second", 11_500_000, 1_500_000, 6, 4));
        await firstSink.WaitForReportsAsync(2);
        await secondSink.WaitForReportsAsync(2);

        Assert.Contains(firstSink.Reports.Last().Diagnostics, item => item.Code == "counter_reset");
        Assert.DoesNotContain(secondSink.Reports, report => report.Diagnostics.Any(item => item.Code == "counter_reset"));
        Task<KafkaStatisticsCompletion> firstCompletionTask = first.CompleteAsync(["broker/1"], TestContext.Current.CancellationToken);
        Task<KafkaStatisticsCompletion> secondCompletionTask = second.CompleteAsync(["broker/1"], TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        first.Capture(StatisticsTestSupport.Report("isolation-client-first", 12_900_000, 2_900_000, 10, 7));
        second.Capture(StatisticsTestSupport.Report("isolation-client-second", 12_900_000, 2_900_000, 9, 6));
        KafkaStatisticsCompletion firstCompletion = await firstCompletionTask;
        KafkaStatisticsCompletion secondCompletion = await secondCompletionTask;

        Assert.False(firstCompletion.IsComplete);
        Assert.True(secondCompletion.IsComplete);
        Assert.Equal([3L, 3L, 3L, 8L], listener.Values("kafka.broker.req_timeouts"));
        Assert.Equal([1L, 1L, 2L, 2L, 2L, 5L], listener.Values("kafka.broker.disconnects"));
        Assert.True(listener.Measurements.All(item => item.Tags.ContainsKey("run") && item.Tags.ContainsKey("client_id")));
    }

    private static string WithGuardCounters(string json, long retries, long transmissionErrors, long receiveErrors) => json
        .Replace("\"txretries\": 0", $"\"txretries\": {retries}", StringComparison.Ordinal)
        .Replace("\"txerrs\": 0", $"\"txerrs\": {transmissionErrors}", StringComparison.Ordinal)
        .Replace("\"rxerrs\": 0", $"\"rxerrs\": {receiveErrors}", StringComparison.Ordinal);

    private static KafkaStatisticsSession NewSession(
        ManualTimeProvider clock,
        RecordingStatisticsSink sink,
        KafkaStatisticsMetrics metrics,
        string runId,
        string clientId) => new(
            new KafkaStatisticsIdentity(Scenario, runId, KafkaClientRole.LoadGenProducer, clientId),
            StatisticsTestSupport.Options(),
            clock,
            sink,
            metrics,
            nativeClockCompatible: true);

    private sealed record MetricMeasurement(string Name, long Value, IReadOnlyDictionary<string, object?> Tags);

    private sealed class CounterMetricsListener : IDisposable
    {
        private readonly HashSet<string> _runIds;
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<MetricMeasurement> _measurements = [];

        internal CounterMetricsListener(params string[] runIds)
        {
            _runIds = new HashSet<string>(runIds, StringComparer.Ordinal);
            _listener.InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                if (instrument.Name is not ("kafka.broker.req_timeouts" or "kafka.broker.disconnects"))
                {
                    return;
                }

                Dictionary<string, object?> copiedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
                if (copiedTags.TryGetValue("run", out object? run) && run is string runId && _runIds.Contains(runId))
                {
                    _measurements.Add(new MetricMeasurement(instrument.Name, value, copiedTags));
                }
            });
            _listener.Start();
        }

        internal long[] Values(string name) => _measurements
            .Where(measurement => measurement.Name == name)
            .Select(measurement => measurement.Value)
            .Order()
            .ToArray();

        internal IReadOnlyCollection<MetricMeasurement> Measurements => _measurements.ToArray();

        public void Dispose() => _listener.Dispose();
    }
}
