using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707
[Trait("Category", "Unit")]
public sealed class KafkaNativeMetricStateTests
{
    private static readonly KafkaNativeMetricIdentity Native = new("native", "client", "producer", 1, 1, 1);
    private static readonly KafkaNativeMetricDefinition Tx = Definition("kafka.librdkafka.client.tx");
    private static readonly KafkaNativeMetricDefinition Timeout = Definition("kafka.librdkafka.broker.req_timeouts");
    private static KafkaStatisticsIdentity Identity(string run) => new("scenario", run, KafkaClientRole.LoadGenProducer, "client");

    [Fact]
    public void Given_CounterReset_When_LaterValueRises_Then_QuarantineRemains()
    {
        using var listener = new Capture();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(Identity("reset"), false);
        state.Record(Projection(Point(Tx, 5)));
        state.Record(Projection(Point(Tx, 2)));
        state.Record(Projection(Point(Tx, 9)));
        Assert.Equal(new long[] { 5 }, listener.Values(Tx.Name));
        Assert.Contains(state.Diagnostics, item => item.Code == "counter_reset");
        var next = metrics.CreateNativeState(Identity("next"), false);
        next.Record(Projection(Point(Tx, 2)));
        Assert.Equal(new long[] { 5, 2 }, listener.Values(Tx.Name));
    }

    [Fact]
    public void Given_MalformedProjection_When_NextReportArrives_Then_StateWasNotPartlyAdvanced()
    {
        using var listener = new Capture();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(Identity("malformed"), true);
        state.Record(Projection(Point(Tx, 2), Point(Timeout, 2, "broker")));
        state.RecordCanonical(Native, "broker", null, 2, null);
        state.Record(new KafkaNativeMetricProjection(false, Native, [Point(Tx, 5)], []));
        state.RecordCanonical(Native, "broker", null, 3, null);
        state.Record(Projection(Point(Tx, 7), Point(Timeout, 7, "broker")));
        state.RecordCanonical(Native, "broker", null, 2, null);
        Assert.Equal(new long[] { 2, 5 }, listener.Values(Tx.Name));
        Assert.Equal(new long[] { 2, 3, 2 }, listener.Values(Timeout.Name));
        Assert.False(state.IsComplete);
    }

    [Fact]
    public void Given_AbsentCounter_When_ItReturns_Then_PreviousBaselineIsRetained()
    {
        using var listener = new Capture();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(Identity("absence"), false);
        state.Record(Projection(Point(Tx, 2)));
        state.Record(Projection());
        state.Record(Projection(Point(Tx, 5)));
        state.Record(Projection(Point(Tx, 5)));
        Assert.Equal(new long[] { 2, 3, 0 }, listener.Values(Tx.Name));
    }

    [Fact]
    public void Given_ProviderLifetimeLimit_When_AnUnobservedBuildExists_Then_OnlyValidReportsReserveSlots()
    {
        using var metrics = new KafkaStatisticsMetrics();
        _ = metrics.CreateNativeState(Identity("unused"), false);
        for (var i = 0; i < 32; i++)
        {
            var state = metrics.CreateNativeState(Identity(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), false);
            state.Record(Projection(Point(Tx, 1)));
            Assert.True(state.IsComplete);
        }
        var rejected = metrics.CreateNativeState(Identity("overflow"), false);
        rejected.Record(Projection(Point(Tx, 1)));
        Assert.Contains(rejected.Diagnostics, item => item.Code == "admission_limit");
    }

    [Fact]
    public void Given_TupleLimit_When_AReportOverflows_Then_NoCounterBaselineIsAdvanced()
    {
        using var listener = new Capture();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(Identity("tuples"), false);
        for (var i = 0; i < 2000; i++)
            state.Record(Projection(Point(Timeout, i, "broker", i.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        state.Record(Projection(Point(Tx, 5), Point(Timeout, 2000, "broker", "overflow")));
        state.Record(Projection(Point(Tx, 7)));
        Assert.Equal(new long[] { 7 }, listener.Values(Tx.Name));
        Assert.Contains(state.Diagnostics, item => item.Code == "admission_limit");
    }

    [Fact]
    public void Given_DiagnosticLimit_When_NewFailuresContinue_Then_TruncationAndCoverageGapsStayVisible()
    {
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(Identity("diagnostics"), false);
        var details = Enumerable.Range(0, 252)
            .Select(index => new KafkaNativeMetricDiagnostic("invalid_field", index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToImmutableArray();
        state.Record(new KafkaNativeMetricProjection(false, Native, [], details));
        Assert.Equal(253, state.Diagnostics.Length);
        Assert.DoesNotContain(state.Diagnostics, item => item.Code == "diagnostics_truncated");

        var overflow = new KafkaNativeMetricProjection(false, Native, [], [new("invalid_field", "overflow")]);
        state.Record(overflow);
        state.Record(overflow);
        state.Record(Projection(Point(Tx, 5)));
        state.Record(Projection(Point(Tx, 2)));
        Assert.Equal(255, state.Diagnostics.Length);
        Assert.Single(state.Diagnostics, item => item.Code == "diagnostics_truncated");
        Assert.Contains(state.Diagnostics, item => item.Code == "invalid_projection");
        Assert.Contains(state.Diagnostics, item => item.Code == "counter_reset");
        for (var index = 0; index < 65; index++)
            state.Record(Projection(Point(Timeout, 1, index.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(256, state.Diagnostics.Length);
        Assert.Contains(state.Diagnostics, item => item.Code == "admission_limit");
        state.Record(overflow);
        Assert.Equal(256, state.Diagnostics.Length);
        Assert.False(state.IsComplete);
    }

    private static KafkaNativeMetricDefinition Definition(string name) => KafkaNativeMetricCatalogue.Definitions.Single(item => item.Name == name);
    private static KafkaNativeMetricPoint Point(KafkaNativeMetricDefinition definition, long value, string? broker = null, string? address = null) =>
        new(definition, value, broker, null, null, null, null, address);
    private static KafkaNativeMetricProjection Projection(params KafkaNativeMetricPoint[] points) => new(true, Native, points.ToImmutableArray(), []);

    private sealed class Capture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<(string Name, long Value)> _values = [];
        internal Capture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == KafkaStatisticsMetrics.MeterName) listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                foreach (var tag in tags)
                    if (tag.Key == "scenario" && Equals(tag.Value, "scenario"))
                        _values.Add((instrument.Name, value));
            });
            _listener.Start();
        }
        internal long[] Values(string name) => _values.Where(value => value.Name == name).Select(value => value.Value).ToArray();
        public void Dispose() => _listener.Dispose();
    }
}
