namespace Parchwell.Kafka.Configuration;

/// <summary>The form of value a scenario override supplies for one setting.</summary>
public enum KafkaScenarioOverrideValueKind
{
    /// <summary>A plain decimal Int32 within the setting's native range.</summary>
#pragma warning disable CA1720 // The public enum member name is fixed by the approved scenario contract.
    Integer,
#pragma warning restore CA1720

    /// <summary>The native boolean literal <c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>A case-sensitive string admitted by the setting policy.</summary>
#pragma warning disable CA1720 // Approved contract name.
    String,
#pragma warning restore CA1720

    /// <summary>The plain scalar omit, removing an emitted baseline setting.</summary>
    Omission
}
