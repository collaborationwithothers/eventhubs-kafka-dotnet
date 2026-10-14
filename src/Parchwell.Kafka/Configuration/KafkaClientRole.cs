namespace Parchwell.Kafka.Configuration;

/// <summary>The workload configuration profiles supplied by the baseline.</summary>
public enum KafkaClientRole
{
    /// <summary>The load generator producer.</summary>
    LoadGenProducer,
    /// <summary>The enrichment output producer.</summary>
    EnrichmentProducer,
    /// <summary>The enrichment input consumer.</summary>
    EnrichmentConsumer,
    /// <summary>The explicitly assigned verifier consumer.</summary>
    VerifierConsumer
}
