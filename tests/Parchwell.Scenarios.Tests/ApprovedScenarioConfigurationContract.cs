using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios.Tests;

// Independent literal fixtures transcribed from Hari's approved #54 plan, not the implementation.
// Microsoft: librdkafka section, read 2026-10-14 (updated 2026-01-28).
// Native and managed values: their pinned v2.16.0 configuration references.
// Managed alias normalization: native CONFIGURATION.md aliases (compression.type/max.in.flight),
// plus Config_gen.cs getters and Config.cs GetEnum at v2.16.0. Native largest/latest are synonyms.
// https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config.cs#L179-L187
// Harness choices and inventory tags: blueprint ADRs and frozen section 12 at the cited commit.
/// <summary>
/// The approved baseline settings per Kafka role, written out by hand so that a change to the
/// implementation cannot also change what the tests expect. Tags and sources are kept for the reader.
/// </summary>
internal static class ApprovedConfigurationContract
{
    internal const string Microsoft = "https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations";
    internal const string Native = "https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md";
    internal const string Managed = "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config_gen.cs";
    internal const string Blueprint = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md";
    internal const string Plan = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/54#issuecomment-6174869137";

    internal sealed record Setting(string Value, string[] Tags, string Source);

    internal static Dictionary<string, Setting> Settings(KafkaClientRole role)
    {
        bool producer = role is KafkaClientRole.LoadGenProducer or KafkaClientRole.EnrichmentProducer;
        Dictionary<string, Setting> expected = new(StringComparer.Ordinal);
        void Add(string key, string value, string tags, string source) => expected.Add(key, new(value, tags.Split(','), source));

        Add("bootstrap.servers", "synthetic-namespace.servicebus.windows.net:9093", "A05,A06", Microsoft);
        Add("client.id", "synthetic-client", "H02", Blueprint);
        Add("security.protocol", "sasl_ssl", "A05,F01", Microsoft);
        Add("sasl.mechanism", "OAUTHBEARER", "F01", Blueprint);
        Add("sasl.oauthbearer.method", "default", "F01,F02", Native);
        Add("sasl.oauthbearer.metadata.authentication.type", "none", "F02", Native);
        Add("enable.sasl.oauthbearer.unsecure.jwt", "false", "F01", Native);
        Add("enable.ssl.certificate.verification", "true", "F01", Native);
        Add("ssl.endpoint.identification.algorithm", "https", "F01", Native);
        Add("socket.keepalive.enable", "true", "A02", Microsoft);
        Add("metadata.max.age.ms", "180000", "A03", Microsoft);
        Add("connections.max.idle.ms", "0", "A04", Native);
        Add("broker.address.ttl", "1000", "K04", Native);
        Add("socket.timeout.ms", "60000", producer ? "A01" : "C04,C05", Native);
        Add("socket.max.fails", "1", "A01", Native);
        Add("statistics.interval.ms", "15000", producer ? "H01,H02,A01,B10" : "H01,H02", Blueprint);
        Add("allow.auto.create.topics", producer ? "true" : "false", "D01", Native);
        if (producer)
        {
            Add("request.timeout.ms", "60000", "A01", Microsoft);
            Add("message.timeout.ms", "300000", "A01,B10,C18", Native);
            Add("message.send.max.retries", "2147483647", "A01,B10", Microsoft);
            Add("compression.type", "none", "B03", Native);
            Add("acks", "-1", "B06", Native);
            Add("enable.idempotence", "false", "B04", Native);
            Add("linger.ms", "5", "B07", Native);
            Add("partitioner", "consistent_random", "B08", Microsoft);
            Add("message.max.bytes", "1000000", "B01,B02", Native);
            Add("batch.size", "1000000", "B02", Native);
            Add("max.in.flight", "1000000", "B04,B10", Native);
            Add("queue.buffering.max.messages", "100000", "K03", Native);
            Add("queue.buffering.max.kbytes", "1048576", "K03", Native);
            Add("dotnet.producer.enable.background.poll", "true", "F01,A01", Managed);
            Add("dotnet.producer.enable.delivery.reports", "true", "A01,B10", Managed);
            Add("dotnet.producer.delivery.report.fields", "all", "A01,B10", Managed);
        }
        else
        {
            Add("group.protocol", "classic", "C08", Native);
            Add("partition.assignment.strategy", "range,roundrobin", "C07", Native);
            Add("session.timeout.ms", "30000", "C01,C04", Microsoft);
            Add("heartbeat.interval.ms", "3000", "C02", Microsoft);
            Add("max.poll.interval.ms", "300000", "C03", Microsoft);
            Add("fetch.wait.max.ms", "500", "C05", Native);
            Add("queued.min.messages", "100000", "K03", Native);
            Add("queued.max.messages.kbytes", "65536", "K03", Native);
            Add("fetch.max.bytes", "52428800", "K03,C05", Native);
            Add("fetch.message.max.bytes", "1048576", "K03", Native);
            Add("receive.message.max.bytes", "100000000", "K03", Native);
            if (role == KafkaClientRole.EnrichmentConsumer)
            {
                Add("group.id", "synthetic-group", "C12,C13,C14", Plan);
                Add("enable.auto.commit", "true", "C09,C10,C18", Blueprint);
                Add("auto.commit.interval.ms", "5000", "C09,C10,C15", Native);
                Add("enable.auto.offset.store", "false", "C10,C03", Blueprint);
                Add("auto.offset.reset", "latest", "C15", Native);
                Add("isolation.level", "read_committed", "B05", Native);
            }
            else
            {
                Add("group.id", "parchwell-verifier", "C13", Blueprint);
                Add("enable.auto.commit", "false", "C10", Blueprint);
                Add("auto.offset.reset", "error", "C15", Blueprint);
                Add("isolation.level", "read_committed", "B05", Blueprint);
            }
        }
        return expected;
    }
}
