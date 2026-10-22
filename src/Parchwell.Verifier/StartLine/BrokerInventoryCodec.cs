using System.Globalization;
using System.Net;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.StartLine;

/// <summary>Projects independent admin metadata into the admission inventory.</summary>
internal static class BrokerInventoryCodec
{
    internal const int MaximumBrokers = 64;

    internal static byte[] Write(RunIdentity identity, IReadOnlyList<BrokerMetadata> brokers,
        string sourceCommit, long maximumBytes)
    {
        if (brokers.Count is 0 or > MaximumBrokers)
            throw new InvalidOperationException("Broker inventory must contain between 1 and 64 brokers.");
        var identifiers = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var projected = brokers.OrderBy(broker => broker.BrokerId).Select(broker =>
        {
            string host = broker.Host;
            if (broker.BrokerId < 0 || broker.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(host)
                || host.Length > 253 || host.Any(char.IsWhiteSpace)
                || Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new InvalidOperationException("Broker inventory contains malformed broker metadata.");
            // Native nodename IPv6 brackets: same file lines 141-154. Broker name protocol/id format:
            // https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L196-L224
            string node = IPAddress.TryParse(host, out IPAddress? address)
                && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{host.Trim('[', ']')}]" : host;
            string name = string.Create(CultureInfo.InvariantCulture, $"sasl_ssl://{node}:{broker.Port}/{broker.BrokerId}");
            if (!identifiers.Add(broker.BrokerId) || !names.Add(name))
                throw new InvalidOperationException("Broker inventory contains duplicate identifiers or names.");
            return (Broker: broker, Name: name);
        }).ToArray();
        return DeploymentJson.Write(maximumBytes, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", 1);
            DeploymentJson.WriteIdentity(writer, identity);
            writer.WritePropertyName("brokers");
            writer.WriteStartArray();
            foreach (var item in projected)
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", item.Broker.BrokerId);
                writer.WriteString("host", item.Broker.Host);
                writer.WriteNumber("port", item.Broker.Port);
                writer.WriteString("name", item.Name);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("source", "kafka_metadata");
            writer.WriteString("source_commit", sourceCommit);
            writer.WriteEndObject();
        });
    }
}
