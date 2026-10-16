using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Routes the single worker's statistics session to the evidence sink created for its run.</summary>
public sealed class RunEvidenceSinkRouter : IKafkaStatisticsEvidenceSink
{
    private IKafkaStatisticsEvidenceSink? _target;

    internal void Attach(IKafkaStatisticsEvidenceSink target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Interlocked.CompareExchange(ref _target, target, null) is not null)
            throw new InvalidOperationException("statistics_evidence_sink_already_attached");
    }

    public ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        IKafkaStatisticsEvidenceSink target = Volatile.Read(ref _target)
            ?? throw new InvalidOperationException("statistics_evidence_sink_not_attached");
        return target.WriteAsync(report, cancellationToken);
    }
}
