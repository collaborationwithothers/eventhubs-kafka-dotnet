using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// These assertions keep the exported metric contract stable for the KQL queries in blueprint section 10.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsMetricsTests
{
    private const string MeterName = "Parchwell.Kafka.Statistics";
    private const string Scenario = "metric-test-scenario";
    private const string RunWithMeasurements = "metric-test-run-with-measurements";
    private const string ClientId = "metric-test-client";
    private const string BrokerName = "broker-test-1";

    [Fact]
    public void Given_BrokerWindowsAndValidatedDeltas_When_Recording_Then_EmitsStableMetricsAndTags()
    {
        using var listener = new MetricsListener(RunWithMeasurements);
        using var foreignMeter = new Meter(MeterName);
        var foreignCounter = foreignMeter.CreateCounter<long>("kafka.librdkafka.client.tx", "count");
        foreignCounter.Add(1, new KeyValuePair<string, object?>("run", "foreign-run"));
        using var metrics = new KafkaStatisticsMetrics();
        var identity = new KafkaStatisticsIdentity(Scenario, RunWithMeasurements, KafkaClientRole.LoadGenProducer, ClientId);

        metrics.Record(identity, Broker(12, 600, 3, 42), 1, 0);
        metrics.Record(identity, Broker(870, 900, 5, 11), 2, 1);
        metrics.Record(identity, Broker(25, 700, 2, 13), 3, 0);
        metrics.Record(identity, Broker(0, 0, 1, 1), 0, 0);

        Assert.True(listener.HasPublished(foreignMeter));
        AssertMetricContract(listener.PublishedInstruments);
        Assert.DoesNotContain(listener.PublishedInstruments, instrument => ReferenceEquals(instrument.Meter, foreignMeter));
        AssertMeasurements(listener.Measurements, "kafka.broker.throttle.max_ms", [12, 870, 25, 0], "ms", "LoadGenProducer", RunWithMeasurements);
        AssertMeasurements(listener.Measurements, "kafka.broker.rtt.p99_us", [600, 900, 700, 0], "us", "LoadGenProducer", RunWithMeasurements);
        AssertMeasurements(listener.Measurements, "kafka.broker.req_timeouts", [1, 2, 3, 0], "count", "LoadGenProducer", RunWithMeasurements);
        AssertMeasurements(listener.Measurements, "kafka.broker.disconnects", [0, 1, 0, 0], "count", "LoadGenProducer", RunWithMeasurements);
    }

    [Fact]
    public void Given_EmptyWindowsAndQuarantinedDeltas_When_Recording_Then_EmitsNoInventedValues()
    {
        const string runId = "metric-test-run-empty";
        using var listener = new MetricsListener(runId);
        using var metrics = new KafkaStatisticsMetrics();
        var identity = new KafkaStatisticsIdentity(Scenario, runId, KafkaClientRole.EnrichmentConsumer, ClientId);

        metrics.Record(identity, Broker(null, null, 0, 0), null, null);
        metrics.Record(identity, Broker(999, 999, 0, 0), null, null);

        Assert.Empty(listener.Measurements);

        metrics.Record(identity, Broker(null, null, 0, 0), 0, null);

        var timeout = Assert.Single(listener.Measurements);
        Assert.Equal("kafka.broker.req_timeouts", timeout.Name);
        Assert.Equal(0, timeout.Value);
        AssertTags(timeout.Tags, runId, "EnrichmentConsumer");
    }

    private static KafkaBrokerStatistics Broker(long? throttle, long? roundTripTime, long throttleCount, long roundTripTimeCount) =>
        new(BrokerName, 1, "UP", 100, 7, 2, 0, 0, 0,
            new KafkaWindowStatistics(throttleCount, throttle),
            new KafkaWindowStatistics(roundTripTimeCount, roundTripTime));

    private static void AssertMetricContract(IReadOnlyCollection<PublishedInstrument> instruments)
    {
        var expected = new Dictionary<string, (string Unit, string Kind)>
        {
            ["kafka.broker.throttle.max_ms"] = ("ms", "Histogram`1"),
            ["kafka.broker.rtt.p99_us"] = ("us", "Histogram`1"),
            ["kafka.broker.req_timeouts"] = ("count", "Counter`1"),
            ["kafka.broker.disconnects"] = ("count", "Counter`1")
        };

        var legacy = instruments.Where(instrument => expected.ContainsKey(instrument.Name)).ToArray();
        foreach (var instrument in legacy)
        {
            Assert.Equal(MeterName, instrument.MeterName);
            Assert.True(expected.TryGetValue(instrument.Name, out var contract), $"Unexpected instrument {instrument.Name}.");
            Assert.Equal(contract.Unit, instrument.Unit);
            Assert.Equal(contract.Kind, instrument.Kind);
        }

        Assert.Equal(expected.Keys.Order(), legacy.Select(instrument => instrument.Name).Distinct().Order());

        var native = instruments.Where(instrument => instrument.Name.StartsWith("kafka.librdkafka.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(NativeMetricContract.Rows.Select(row => row.Name).Order(), native.Select(instrument => instrument.Name).Order());
        foreach (var row in NativeMetricContract.Rows)
        {
            var instrument = Assert.Single(native, item => item.Name == row.Name);
            Assert.Equal(MeterName, instrument.MeterName);
            Assert.Equal(row.Unit, instrument.Unit);
            Assert.Equal(row.Kind, instrument.Kind);
        }
        Assert.Equal(expected.Count + NativeMetricContract.Rows.Length, instruments.Count);
    }

    private static void AssertMeasurements(
        IReadOnlyCollection<Measurement> measurements,
        string name,
        long[] expectedValues,
        string expectedUnit,
        string expectedRole,
        string runId)
    {
        var actual = measurements.Where(measurement => measurement.Name == name).ToArray();
        Assert.Equal(expectedValues.Order(), actual.Select(measurement => measurement.Value).Order());
        foreach (var measurement in actual)
        {
            Assert.Equal(expectedUnit, measurement.Unit);
            AssertTags(measurement.Tags, runId, expectedRole);
        }
    }

    private static void AssertTags(IReadOnlyDictionary<string, object?> tags, string runId, string expectedRole)
    {
        Assert.Equal(5, tags.Count);
        Assert.Equal(Scenario, tags["scenario"]);
        Assert.Equal(runId, tags["run"]);
        Assert.Equal(expectedRole, tags["role"]);
        Assert.Equal(ClientId, tags["client_id"]);
        Assert.Equal(BrokerName, tags["broker"]);
        Assert.DoesNotContain("session", tags.Keys);
        Assert.DoesNotContain("topic", tags.Keys);
        Assert.DoesNotContain("partition", tags.Keys);
    }

    private sealed record PublishedInstrument(Meter Meter, string Name, string Unit, string Kind)
    {
        internal string MeterName => Meter.Name;
    }

    private sealed record Measurement(string Name, long Value, string Unit, IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricsListener : IDisposable
    {
        private readonly string _runId;
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<PublishedInstrument> _published = [];
        private readonly ConcurrentBag<Measurement> _measurements = [];
        private Meter? _ownedMeter;

        internal MetricsListener(string runId)
        {
            _runId = runId;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    _published.Add(new PublishedInstrument(
                        instrument.Meter,
                        instrument.Name,
                        instrument.Unit ?? string.Empty,
                        instrument.GetType().Name));
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var copiedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
                if (copiedTags.TryGetValue("run", out var run) && Equals(run, _runId))
                {
                    Interlocked.CompareExchange(ref _ownedMeter, instrument.Meter, null);
                    _measurements.Add(new Measurement(instrument.Name, value, instrument.Unit ?? string.Empty, copiedTags));
                }
            });
            _listener.Start();
        }

        internal IReadOnlyCollection<PublishedInstrument> PublishedInstruments =>
            _published.Where(instrument => ReferenceEquals(instrument.Meter, Volatile.Read(ref _ownedMeter))).ToArray();

        internal bool HasPublished(Meter meter) => _published.Any(instrument => ReferenceEquals(instrument.Meter, meter));

        internal IReadOnlyCollection<Measurement> Measurements => _measurements.ToArray();

        public void Dispose() => _listener.Dispose();
    }
}
