using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Statistics;

/// <summary>Creates an isolated statistics capture session for one Kafka client lifetime.</summary>
public sealed class KafkaStatisticsBridgeFactory
{
    private readonly KafkaStatisticsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IKafkaStatisticsEvidenceSink _sink;
    private readonly KafkaStatisticsMetrics _metrics;
    private readonly bool _nativeClockCompatible;
    private readonly Action? _completionBoundaryObserver;

    internal KafkaStatisticsBridgeFactory(
        IOptions<KafkaStatisticsOptions> options,
        TimeProvider timeProvider,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics,
        bool nativeClockCompatible,
        Action? completionBoundaryObserver = null)
        : this(options.Value, timeProvider, sink, metrics, nativeClockCompatible, completionBoundaryObserver)
    {
    }

    internal KafkaStatisticsBridgeFactory(
        KafkaStatisticsOptions options,
        TimeProvider timeProvider,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics,
        bool nativeClockCompatible,
        Action? completionBoundaryObserver = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = CopyOptions(options);
        if (!_options.IsValid(out string? failure))
        {
            throw new OptionsValidationException(
                Options.DefaultName, typeof(KafkaStatisticsOptions), [failure!]);
        }

        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _nativeClockCompatible = nativeClockCompatible;
        _completionBoundaryObserver = completionBoundaryObserver;
    }

    /// <summary>Creates a new session and its counter state for the supplied client identity.</summary>
    /// <param name="identity">The scenario, run, role and client identity attached to each report.</param>
    /// <returns>A fresh session that must be attached to exactly one native client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException">The identity is incomplete or has an unsupported role.</exception>
    public KafkaStatisticsSession Create(KafkaStatisticsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!IsIdentifier(identity.ScenarioId)
            || !IsIdentifier(identity.RunId)
            || !IsIdentifier(identity.ClientId)
            || !Enum.IsDefined(identity.Role))
        {
            throw new ArgumentException("Statistics identity fields must be nonempty and the role must be defined.", nameof(identity));
        }

        if (!_options.IsValid(out string? failure))
        {
            throw new OptionsValidationException(
                Options.DefaultName, typeof(KafkaStatisticsOptions), [failure!]);
        }

        return new KafkaStatisticsSession(identity, CopyOptions(_options), _timeProvider, _sink, _metrics,
            _nativeClockCompatible, _completionBoundaryObserver);
    }

    private static bool IsIdentifier(string value) =>
        !string.IsNullOrEmpty(value)
        && !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));

    private static KafkaStatisticsOptions CopyOptions(KafkaStatisticsOptions options) => new()
    {
        MaxSnapshotCharacters = options.MaxSnapshotCharacters,
        QueueCapacity = options.QueueCapacity,
        MaxBrokerSeries = options.MaxBrokerSeries,
        CompletionTimeout = options.CompletionTimeout
    };
}
