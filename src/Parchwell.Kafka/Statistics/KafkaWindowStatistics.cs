namespace Parchwell.Kafka.Statistics;

/// <summary>Contains a native rolling-window sample count and its selected summary value.</summary>
/// <remarks>
/// A value is absent exactly when the window has no observations. This preserves empty-window
/// evidence without inventing a measured zero. librdkafka v2.16.0 defines <c>cnt</c> as the number
/// of sampled values and the selected <c>max</c> or <c>p99</c> as a window statistic in
/// <see href="https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md">STATISTICS.md</see>.
/// </remarks>
/// <param name="Count">The number of values sampled in the native window.</param>
/// <param name="Value">The native window maximum or percentile, or null for an empty window.</param>
public sealed record KafkaWindowStatistics(long Count, long? Value);
