namespace Parchwell.Kafka.Configuration;

/// <summary>Distinguishes emitted settings from intentional omissions.</summary>
public enum KafkaSettingDisposition
{
    /// <summary>The value is emitted into the native client configuration.</summary>
    Explicit,
    /// <summary>The setting is deliberately excluded from the baseline.</summary>
    Excluded,
    /// <summary>The workload does not depend on a selected value for this setting.</summary>
    Unspecified
}
