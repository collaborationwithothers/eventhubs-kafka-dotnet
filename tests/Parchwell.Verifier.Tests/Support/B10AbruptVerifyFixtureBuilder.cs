using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Parchwell.Verifier.Tests.Support;

internal sealed partial class B10AbruptVerifyEvidence
{
    private static readonly string[] OrdinaryArtifacts = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    internal const string Broker = "sasl_ssl://broker-a:9093/1";
    internal const string Timeout = "synthetic-timeout";
    internal static string RepositoryRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scenarios"))) root = Directory.GetParent(root)!.FullName;
        return root;
    }

    // The ordinary synthetic writer is independent of the abrupt readers and is pinned to E-T by parity.
    private void CompleteFixtures()
    {
        using var ordinary = new HostEvidenceBuilder(instanceId: Timeout, clientId: Timeout, scenarioId: "B10", phase: "C1");
        foreach ((string name, byte[]? bytes) in ordinary.Inputs)
            Files["timeout/publication/" + name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes!).Replace("synthetic-c5", Run, StringComparison.Ordinal));
        foreach ((string path, byte[] bytes) in ordinary.LedgerObjects.Where(pair => pair.Key.EndsWith("/registration.json", StringComparison.Ordinal) || pair.Key.EndsWith("/completion.json", StringComparison.Ordinal)))
            Files["timeout/" + Path.GetFileName(path)] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("synthetic-c5", Run, StringComparison.Ordinal));
        foreach (string name in new[] { "registration.json", "completion.json" })
            Change("timeout/" + name, root => root["pod_name"] = Timeout);
        SetTimeoutRows();
        foreach (string name in new[] { "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            Files["successor/" + name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Files["timeout/publication/" + name]).Replace(Timeout, Second, StringComparison.Ordinal));
        Change("successor/capabilities.json", root =>
        {
            JsonNode timeout = root["resolved_configuration"]!["settings"]!["message.timeout.ms"]!;
            timeout["value"] = "300000"; timeout["override"] = null;
        });
        JsonObject report = JsonNode.Parse(Encoding.UTF8.GetString(Files["timeout/publication/statistics.jsonl"]).Split('\n')[0])!.AsObject();
        JsonObject native = JsonNode.Parse(report["raw_json"]!.GetValue<string>())!.AsObject();
        native["topics"] = new JsonObject { ["document-events"] = new JsonObject { ["partitions"] = new JsonObject { ["0"] = new JsonObject { ["leader"] = 1 } } } };
        foreach (JsonObject frame in Frames.Where(frame => frame["kind"]!.GetValue<string>() is "pre_acceptance_report" or "post_drain_report"))
        {
            string name = frame["data"]!["name"]!.GetValue<string>();
            byte[] bytes = Encoding.UTF8.GetBytes(native.ToJsonString()); Files["first/" + name] = bytes;
            frame["data"]!["length"] = bytes.Length; frame["data"]!["sha256"] = Hash(bytes);
        }
    }

    internal static Dictionary<string, byte[]> RemapRun(Dictionary<string, byte[]> files, string run)
    {
        var evidence = new B10AbruptVerifyEvidence(); evidence.Files.Clear(); evidence.Frames.Clear();
        foreach ((string name, byte[] bytes) in files)
        {
            string json = Encoding.UTF8.GetString(bytes);
            evidence.Files[name] = Encoding.UTF8.GetBytes(name.EndsWith(".jsonl", StringComparison.Ordinal)
                ? string.Join('\n', json.Split('\n').Select(RemapDocument)) : RemapDocument(json));
        }
        foreach (string line in Encoding.UTF8.GetString(evidence.Files["first/journal.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            evidence.Frames.Add(JsonNode.Parse(line)!.AsObject());
        foreach (JsonObject frame in evidence.Frames.Where(frame => frame["kind"]!.GetValue<string>() is "pre_acceptance_report" or "post_drain_report"))
        {
            byte[] bytes = evidence.Files["first/" + frame["data"]!["name"]!.GetValue<string>()];
            frame["data"]!["length"] = bytes.Length; frame["data"]!["sha256"] = Hash(bytes);
        }
        evidence.RehashTimeout(); evidence.Rehash(); return evidence.Files;

        string RemapDocument(string json) => string.IsNullOrWhiteSpace(json) ? json : Remap(JsonNode.Parse(json))!.ToJsonString();
        JsonNode? Remap(JsonNode? node) => node switch
        {
            JsonObject value => new JsonObject(value.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, Remap(pair.Value)))),
            JsonArray value => new JsonArray(value.Select(Remap).ToArray()),
            JsonValue value when value.TryGetValue(out string? text) && text is not null => JsonValue.Create(text.Replace(Run, run, StringComparison.Ordinal)),
            _ => node?.DeepClone()
        };
    }

    internal static B10AbruptVerifyEvidence FromExports(Dictionary<string, byte[]> abrupt, Dictionary<string, byte[]> timeout)
    {
        var evidence = new B10AbruptVerifyEvidence(); evidence.Files.Clear(); evidence.Frames.Clear();
        foreach ((string name, byte[] bytes) in abrupt)
            evidence.Files[name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)
                .Replace("broker-1", Broker, StringComparison.Ordinal).Replace("\"broker\"", "\"" + Broker + "\"", StringComparison.Ordinal));
        foreach ((string name, byte[] bytes) in timeout)
            evidence.Files["timeout/" + name["producer/pod-first/".Length..]] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)
                .Replace("run-1", Run, StringComparison.Ordinal).Replace("pod-first", Timeout, StringComparison.Ordinal).Replace("broker-1", Broker, StringComparison.Ordinal));
        var normalizedStatistics = new List<string>();
        foreach (string line in Encoding.UTF8.GetString(evidence.Files["timeout/publication/statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonObject envelope = JsonNode.Parse(line)!.AsObject();
            JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            foreach (KeyValuePair<string, JsonNode?> broker in native["brokers"]!.AsObject())
                if (!broker.Value!.AsObject().ContainsKey("source")) broker.Value["source"] = "learned";
            envelope["raw_json"] = native.ToJsonString(); normalizedStatistics.Add(envelope.ToJsonString());
        }
        evidence.Files["timeout/publication/statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', normalizedStatistics) + "\n");
        foreach (string name in new[] { "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            evidence.Files["successor/" + name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(evidence.Files["timeout/publication/" + name])
                .Replace(Timeout, Second, StringComparison.Ordinal));
        evidence.Change("successor/capabilities.json", root =>
        {
            JsonNode timeoutSetting = root["resolved_configuration"]!["settings"]!["message.timeout.ms"]!;
            timeoutSetting["value"] = "300000"; timeoutSetting["override"] = null;
        });
        foreach (string line in Encoding.UTF8.GetString(evidence.Files["first/journal.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            evidence.Frames.Add(JsonNode.Parse(line)!.AsObject());
        foreach (JsonObject frame in evidence.Frames.Where(frame => frame["kind"]!.GetValue<string>() is "pre_acceptance_report" or "post_drain_report"))
        {
            byte[] bytes = evidence.Files["first/" + frame["data"]!["name"]!.GetValue<string>()];
            frame["data"]!["length"] = bytes.Length; frame["data"]!["sha256"] = Hash(bytes);
        }
        evidence.RehashTimeout(); evidence.Rehash(); return evidence;
    }

    internal void SetTimeoutRows(params JsonObject[] rows)
    {
        Files["timeout/publication/ledger.jsonl"] = Encoding.UTF8.GetBytes(string.Concat(rows.Select(row => row.ToJsonString() + "\n")));
        Change("timeout/publication/manifest.json", root =>
        {
            root["planned_attempt_count"] = rows.Select(row => row["attempt"]!["key"]!.GetValue<string>() + "/" + row["attempt"]!["sequence"]).Distinct(StringComparer.Ordinal).Count();
            root["attempt_count"] = rows.Length;
            root["inventory"] = new JsonArray(rows.Select(row => row["attempt"]!.DeepClone()).ToArray());
        });
        RehashTimeout();
    }
    internal static JsonObject TimeoutRow(string id, string key, int ordinal, bool delivered = true)
    {
        using var seed = new HostEvidenceBuilder(instanceId: Timeout, clientId: Timeout, scenarioId: "B10", phase: "C1");
        JsonObject row = JsonNode.Parse(Encoding.UTF8.GetString(seed.Inputs["ledger.jsonl"]!).Split('\n')[0].Replace("synthetic-c5", Run, StringComparison.Ordinal))!.AsObject();
        row["attempt"]!["attempt_id"] = id; row["attempt"]!["key"] = key; row["attempt"]!["ordinal"] = ordinal; row["attempt"]!["sequence"] = 0;
        row["attempt"]!["invoked_microseconds"] = ordinal + 1; row["attempt"]!["planned_microseconds"] = 0;
        row["delivered"] = delivered; row["error_code"] = delivered ? 0 : -192; row["error_name"] = delivered ? "NoError" : "Local_MsgTimedOut"; row["persistence_status"] = delivered ? 2 : 1;
        row["completed_microseconds"] = ordinal + 2;
        return row;
    }
    internal void RehashTimeout() => Change("timeout/publication/index.json", root => root["artifacts"] = new JsonArray(OrdinaryArtifacts
        .Select(name => (JsonNode)Descriptor(name, Files["timeout/publication/" + name])).ToArray()));
}
