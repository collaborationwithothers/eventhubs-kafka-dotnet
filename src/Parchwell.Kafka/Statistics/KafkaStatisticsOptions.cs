namespace Parchwell.Kafka.Statistics;

/// <summary>Required resource and timing limits for a statistics capture session.</summary>
public sealed class KafkaStatisticsOptions
{
    /// <summary>Gets or initializes the maximum JSON length, measured in UTF-16 characters.</summary>
    public int MaxSnapshotCharacters { get; set; }

    /// <summary>Gets or initializes the bounded number of reports waiting for processing.</summary>
    public int QueueCapacity { get; set; }

    /// <summary>Gets or initializes the maximum broker series retained by one session.</summary>
    public int MaxBrokerSeries { get; set; }

    /// <summary>Gets or initializes the positive deadline for final coverage and evidence drain.</summary>
    public TimeSpan CompletionTimeout { get; set; }

    internal bool IsValid(out string? failure)
    {
        if (MaxSnapshotCharacters <= 0)
        {
            failure = "MaxSnapshotCharacters must be positive.";
            return false;
        }

        if (QueueCapacity <= 0)
        {
            failure = "QueueCapacity must be positive.";
            return false;
        }

        if (MaxBrokerSeries <= 0)
        {
            failure = "MaxBrokerSeries must be positive.";
            return false;
        }

        if (CompletionTimeout <= TimeSpan.Zero || CompletionTimeout.TotalMilliseconds > uint.MaxValue - 1d)
        {
            failure = "CompletionTimeout must be finite and positive.";
            return false;
        }

        failure = null;
        return true;
    }
}
