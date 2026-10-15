namespace Parchwell.Kafka.Configuration;

/// <summary>Distinguishes emitted settings from intentional omissions.</summary>
/// <remarks>
/// A disposition says what the baseline does with one setting key for one role.
/// An omission is a key the baseline knowingly does not write. It is still recorded, with its
/// inventory items, sources and reason, so a reader can tell a deliberate gap from a forgotten one.
/// </remarks>
public enum KafkaSettingDisposition
{
    /// <summary>The value is emitted into the native client configuration.</summary>
    Explicit,
    /// <summary>The setting is deliberately excluded from the baseline.</summary>
    /// <remarks>Used when a decision rules the setting out, for example transactions (ADR-015).</remarks>
    Excluded,
    /// <summary>The workload does not depend on a selected value for this setting.</summary>
    /// <remarks>Used when the role's behaviour does not use the setting, so no value is chosen.</remarks>
    Unspecified
}
