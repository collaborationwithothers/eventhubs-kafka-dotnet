namespace Parchwell.Kafka.Statistics;

/// <summary>Contains one broker's copied librdkafka counters and rolling-window samples.</summary>
/// <remarks>
/// Counter values are cumulative as defined by librdkafka v2.16.0. State age and the selected
/// window values retain their native units: microseconds for state age and RTT, milliseconds for
/// throttle time. Source:
/// <see href="https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md">STATISTICS.md</see>.
/// </remarks>
/// <param name="Name">The native broker name.</param>
/// <param name="NodeId">The broker node identifier, with -1 representing a bootstrap broker.</param>
/// <param name="State">The broker connection state.</param>
/// <param name="StateAgeMicroseconds">Elapsed time since the state changed, in microseconds.</param>
/// <param name="RequestTimeouts">The cumulative number of broker requests that timed out.</param>
/// <param name="Disconnects">The cumulative number of broker disconnects.</param>
/// <param name="TransmissionRetries">The cumulative number of request retries.</param>
/// <param name="TransmissionErrors">The cumulative number of transmission errors.</param>
/// <param name="ReceiveErrors">The cumulative number of receive errors.</param>
/// <param name="Throttle">The throttle-time window maximum in milliseconds and its sample count.</param>
/// <param name="RoundTripTime">The RTT window p99 in microseconds and its sample count.</param>
public sealed record KafkaBrokerStatistics(
    string Name,
    int NodeId,
    string State,
    long StateAgeMicroseconds,
    long RequestTimeouts,
    long Disconnects,
    long TransmissionRetries,
    long TransmissionErrors,
    long ReceiveErrors,
    KafkaWindowStatistics Throttle,
    KafkaWindowStatistics RoundTripTime);
