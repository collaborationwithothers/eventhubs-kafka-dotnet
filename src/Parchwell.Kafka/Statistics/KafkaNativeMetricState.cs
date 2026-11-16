using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

internal sealed record KafkaNativeMetricTuple(
    KafkaStatisticsIdentity Identity, string NativeName, string NativeType, string? Broker,
    string? BrokerAddress, string? Topic, int? Partition, string? Request, string? State);

// One instance belongs to one native client build. The meter owns admission across instances.
internal sealed class KafkaNativeMetricState
{
    private const int MaxLifetimes = 32;
    private const int MaxTuples = 2000;
    private readonly KafkaStatisticsMetrics _metrics;
    private readonly KafkaStatisticsIdentity _identity;
    private readonly bool _compatibilityBacked;
    private readonly Dictionary<CounterKey, CounterValue> _counters = new();
    private readonly HashSet<string> _brokers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _topics = new(StringComparer.Ordinal);
    private readonly HashSet<(string Topic, int Partition)> _partitions = new();
    private readonly HashSet<KafkaNativeMetricDiagnostic> _diagnostics = [];
    private bool _admitted;

    internal KafkaNativeMetricState(KafkaStatisticsMetrics metrics, KafkaStatisticsIdentity identity, bool compatibilityBacked)
    {
        _metrics = metrics;
        _identity = identity;
        _compatibilityBacked = compatibilityBacked;
    }

    internal bool IsComplete { get { lock (_metrics.NativeLock) return _diagnostics.Count == 0; } }
    internal ImmutableArray<KafkaNativeMetricDiagnostic> Diagnostics
    {
        get
        {
            lock (_metrics.NativeLock)
                return _diagnostics.OrderBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Field, StringComparer.Ordinal).ToImmutableArray();
        }
    }

    internal void Record(KafkaNativeMetricProjection projection)
    {
        lock (_metrics.NativeLock)
        {
            foreach (var diagnostic in projection.Diagnostics)
            {
                if (_diagnostics.Count < 256) _diagnostics.Add(diagnostic);
            }
            if (!projection.IsValid || projection.Identity is null)
            {
                _diagnostics.Add(new("invalid_projection", null));
                return;
            }

            var points = projection.Points.Where(point => !_compatibilityBacked || !IsCanonical(point.Definition)).ToArray();
            if (!Admit(projection.Identity, points)) return;
            foreach (var point in points)
            {
                if (!point.Definition.Kind.StartsWith("Counter", StringComparison.Ordinal))
                {
                    _metrics.RecordNative(point, projection.Identity, _identity, point.Value);
                    continue;
                }

                var key = new CounterKey(point.Definition.Name, point.Broker, point.Topic, point.Partition, point.Request);
                if (_counters.TryGetValue(key, out var previous))
                {
                    if (previous.Quarantined) continue;
                    if (point.Value < previous.Value)
                    {
                        _counters[key] = previous with { Quarantined = true };
                        _diagnostics.Add(new("counter_reset", null));
                        continue;
                    }
                    _metrics.RecordNative(point, projection.Identity, _identity, point.Value - previous.Value);
                }
                else _metrics.RecordNative(point, projection.Identity, _identity, point.Value);
                _counters[key] = new CounterValue(point.Value, false);
            }
        }
    }

    internal void RecordCanonical(KafkaNativeMetricIdentity nativeIdentity, string broker, string? brokerAddress,
        long? timeoutDelta, long? disconnectDelta)
    {
        if (!_compatibilityBacked) return;
        lock (_metrics.NativeLock)
        {
            var points = new List<KafkaNativeMetricPoint>(2);
            AddCanonical(points, "kafka.librdkafka.broker.req_timeouts", broker, brokerAddress, timeoutDelta);
            AddCanonical(points, "kafka.librdkafka.broker.disconnects", broker, brokerAddress, disconnectDelta);
            if (points.Count == 0 || !Admit(nativeIdentity, points)) return;
            foreach (var point in points) _metrics.RecordNative(point, nativeIdentity, _identity, point.Value);
        }
    }

    private static bool IsCanonical(KafkaNativeMetricDefinition definition) =>
        definition.Name is "kafka.librdkafka.broker.req_timeouts" or "kafka.librdkafka.broker.disconnects";

    private static void AddCanonical(List<KafkaNativeMetricPoint> points, string name, string broker, string? address, long? delta)
    {
        if (delta is not long value) return;
        var definition = KafkaNativeMetricCatalogue.Definitions.First(item => item.Name == name);
        points.Add(new KafkaNativeMetricPoint(definition, value, broker, null, null, null, null, address));
    }

    private bool Admit(KafkaNativeMetricIdentity nativeIdentity, IReadOnlyCollection<KafkaNativeMetricPoint> points)
    {
        var brokers = new HashSet<string>(_brokers, StringComparer.Ordinal);
        var topics = new HashSet<string>(_topics, StringComparer.Ordinal);
        var partitions = new HashSet<(string Topic, int Partition)>(_partitions);
        var proposed = new Dictionary<string, HashSet<KafkaNativeMetricTuple>>(StringComparer.Ordinal);
        foreach (var point in points)
        {
            if (point.Broker is not null) brokers.Add(point.Broker);
            if (point.Topic is not null) topics.Add(point.Topic);
            if (point.Topic is not null && point.Partition is int partition) partitions.Add((point.Topic, partition));
            var tuple = new KafkaNativeMetricTuple(_identity, nativeIdentity.Name, nativeIdentity.Type,
                point.Broker, point.BrokerAddress, point.Topic, point.Partition, point.Request, point.State);
            if (!proposed.TryGetValue(point.Definition.Name, out var set))
                proposed.Add(point.Definition.Name, set = new HashSet<KafkaNativeMetricTuple>());
            set.Add(tuple);
        }
        if ((!_admitted && _metrics.NativeLifetimes >= MaxLifetimes) || brokers.Count > 64 || topics.Count > 3 ||
            partitions.GroupBy(item => item.Topic, StringComparer.Ordinal).Any(group => group.Count() > 33) ||
            proposed.Any(pair => _metrics.NativeTuples.TryGetValue(pair.Key, out var existing)
                ? existing.Count + pair.Value.Count(tuple => !existing.Contains(tuple)) > MaxTuples
                : pair.Value.Count > MaxTuples))
        {
            _diagnostics.Add(new("admission_limit", null));
            return false;
        }
        if (!_admitted) { _metrics.NativeLifetimes++; _admitted = true; }
        _brokers.UnionWith(brokers);
        _topics.UnionWith(topics);
        _partitions.UnionWith(partitions);
        foreach (var (name, tuples) in proposed)
        {
            if (!_metrics.NativeTuples.TryGetValue(name, out var existing))
                _metrics.NativeTuples.Add(name, existing = new HashSet<KafkaNativeMetricTuple>());
            existing.UnionWith(tuples);
        }
        return true;
    }

    private sealed record CounterKey(string Name, string? Broker, string? Topic, int? Partition, string? Request);
    private sealed record CounterValue(long Value, bool Quarantined);
}
