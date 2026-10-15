using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Parchwell.Kafka.Statistics;

/// <summary>Publishes selected librdkafka broker statistics as low-cardinality OpenTelemetry metrics.</summary>
/// <remarks>
/// Scenario and run identity accompany every measurement so client observations can be joined to
/// platform evidence (ADR-007). Window values are emitted only when the native window contains a
/// selected value; cumulative counters are emitted as validated deltas supplied by the session.
/// </remarks>
internal sealed class KafkaStatisticsMetrics : IDisposable
{
    internal const string MeterName = "Parchwell.Kafka.Statistics";

    private readonly Meter _meter = new(MeterName);
    private readonly Histogram<long> _throttle;
    private readonly Histogram<long> _roundTripTime;
    private readonly Counter<long> _requestTimeouts;
    private readonly Counter<long> _disconnects;

    /// <summary>Creates the reusable meter and its four stable instruments.</summary>
    internal KafkaStatisticsMetrics()
    {
        _throttle = _meter.CreateHistogram<long>("kafka.broker.throttle.max_ms", "ms");
        _roundTripTime = _meter.CreateHistogram<long>("kafka.broker.rtt.p99_us", "us");
        _requestTimeouts = _meter.CreateCounter<long>("kafka.broker.req_timeouts", "count");
        _disconnects = _meter.CreateCounter<long>("kafka.broker.disconnects", "count");
    }

    /// <summary>Records available broker windows and validated cumulative-counter deltas.</summary>
    /// <param name="identity">The scenario, run, and client identity for these measurements.</param>
    /// <param name="broker">The broker whose statistics were received.</param>
    /// <param name="requestTimeoutDelta">The validated timeout delta, or null when quarantined.</param>
    /// <param name="disconnectDelta">The validated disconnect delta, or null when quarantined.</param>
    internal void Record(
        KafkaStatisticsIdentity identity,
        KafkaBrokerStatistics broker,
        long? requestTimeoutDelta,
        long? disconnectDelta)
    {
        var tags = new TagList
        {
            { "scenario", identity.ScenarioId },
            { "run", identity.RunId },
            { "role", identity.Role.ToString() },
            { "client_id", identity.ClientId },
            { "broker", broker.Name }
        };

        if (broker.Throttle.Count > 0 && broker.Throttle.Value is long throttleValue)
        {
            _throttle.Record(throttleValue, in tags);
        }

        if (broker.RoundTripTime.Count > 0 && broker.RoundTripTime.Value is long roundTripTimeValue)
        {
            _roundTripTime.Record(roundTripTimeValue, in tags);
        }

        if (requestTimeoutDelta is long timeoutDelta)
        {
            _requestTimeouts.Add(timeoutDelta, in tags);
        }

        if (disconnectDelta is long disconnectDeltaValue)
        {
            _disconnects.Add(disconnectDeltaValue, in tags);
        }
    }

    /// <summary>Disposes the meter and its instruments.</summary>
    public void Dispose() => _meter.Dispose();
}
