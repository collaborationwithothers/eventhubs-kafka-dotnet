namespace Parchwell.Kafka.Configuration;

/// <summary>The workload configuration profiles supplied by the baseline.</summary>
/// <remarks>
/// A role is one Kafka client in one harness workload, such as the load generator's producer.
/// Each role selects its baseline entries and has its own named options.
/// The workloads are described in docs/blueprint.md section 3, "Workloads".
/// The verifier consumer is harness code: the oracle that classifies a run's messages.
/// Its settings are harness settings, tagged with the inventory item each one guards against
/// (ADR-013, ADR-015).
/// </remarks>
public enum KafkaClientRole
{
    /// <summary>The load generator producer.</summary>
    LoadGenProducer,
    /// <summary>The enrichment output producer.</summary>
    EnrichmentProducer,
    /// <summary>The enrichment input consumer.</summary>
    EnrichmentConsumer,
    /// <summary>The explicitly assigned verifier consumer.</summary>
    /// <remarks>
    /// "Assigned" means the verifier assigns each partition itself, at an explicit start offset.
    /// It reads by assignment, not from committed offsets, between the high watermarks recorded
    /// before and after the load generator runs (ADR-013).
    /// </remarks>
    VerifierConsumer
}
