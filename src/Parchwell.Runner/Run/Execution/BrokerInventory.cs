using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public static class BrokerInventory
{
    public static IReadOnlyList<string> Read(ReadOnlyMemory<byte> bytes, WorkerIdentity identity, string commit)
    {
        if (bytes.Length > 16777216) throw new InvalidDataException("inventory_limit");
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Text(root, "source") != "kafka_metadata" ||
            WorkerIdentity.Text(root, "source_commit") != commit || WorkerIdentity.Read(root.GetProperty("identity")) != identity)
            throw new InvalidDataException("inventory_identity");
        JsonElement brokers = root.GetProperty("brokers");
        if (brokers.GetArrayLength() is < 1 or > 64) throw new InvalidDataException("inventory_count");
        var ids = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var sorted = new SortedDictionary<int, string>();
        foreach (JsonElement broker in brokers.EnumerateArray())
        {
            int id = broker.GetProperty("id").GetInt32();
            string name = WorkerIdentity.Text(broker, "name");
            if (!ids.Add(id) || !names.Add(name) || name != NativeName(WorkerIdentity.Text(broker, "host"), broker.GetProperty("port").GetInt32(), id))
                throw new InvalidDataException("inventory_broker");
            sorted.Add(id, name);
        }
        return sorted.Values.ToArray();
    }

    // librdkafka v2.16.0 rdkafka_broker.c rd_kafka_mk_brokername, lines196-224.
    public static string NativeName(string host, int port, int id)
    {
        if (port is < 1 or > 65535 || id < 0 || string.IsNullOrWhiteSpace(host) ||
            host.Any(character => char.IsWhiteSpace(character) || character is '/' or '@' or '[' or ']') ||
            Uri.CheckHostName(host) == UriHostNameType.Unknown) throw new InvalidDataException("inventory_host_or_port");
        string authority = host.Contains(':', StringComparison.Ordinal) ? "[" + host + "]" : host;
        return string.Create(CultureInfo.InvariantCulture, $"sasl_ssl://{authority}:{port}/{id}");
    }
}
