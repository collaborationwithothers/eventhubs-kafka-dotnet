using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.Tests.Consumer;

internal sealed class ConsumerWireFixture : IProducerArtifactStore, IVerifierArtifactStore
{
    internal const string Prefix = "synthetic-consumer/C0/r1/";
    internal const string Group = "synthetic-consumer-c0-r1-consumers";
    internal const string Stamp = "2026-11-15T00:00:00.0000000Z";
    internal Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);
    internal List<string> Reads { get; } = [];
    internal Func<string, CancellationToken, Task<byte[]?>>? ReadOverride { get; set; }
    internal bool ListingComplete { get; set; } = true;
    internal Action<string>? OnRead { get; set; }
    internal InputLimits Limits { get; set; } = new(32, 100_000_000, 200_000_000, 65536, 100000, 10000000, 256);
    internal TimeProvider Clock { get; set; } = TimeProvider.System;
    internal TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    internal ConsumerWireFixture()
    {
        foreach (string role in new[] { "subject", "witness" })
        {
            Set(role, "registration.json", Registration(role));
            SetRows(role, [Assignment(role, role == "subject" ? [0] : [])]);
            Set(role, "completion.json", Completion(role, 1, 1, 0));
        }
        Blobs[Prefix + "consumer-start.json"] = ConsumerArtifactCodec.WriteSnapshot(new(Group,
            DateTimeOffset.Parse(Stamp, CultureInfo.InvariantCulture), DateTimeOffset.Parse(Stamp, CultureInfo.InvariantCulture),
            [new("subject", "subject", 0, 0), new("witness", "witness", 0, 0)]));
    }

    internal static string Name(string role, string suffix) => Prefix + "consumer/" + role + "/" + suffix;
    internal static JsonObject Registration(string role) => new()
    {
        ["schema_version"] = 1, ["kind"] = "consumer_registration", ["run_id"] = "synthetic-consumer",
        ["scenario_id"] = "C03", ["configuration_id"] = "C0", ["phase"] = "C0", ["repeat_number"] = 1,
        ["role"] = role, ["instance_id"] = role, ["job_name"] = "job-" + role, ["pod_name"] = role,
        ["pod_uid"] = "uid-" + role, ["node_name"] = "n1", ["source_commit"] = new string('a', 40), ["group_id"] = Group,
        ["effective_settings"] = new JsonObject { ["bootstrap.servers"] = "synthetic:9092", ["client.id"] = role,
            ["group.id"] = Group, ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000" },
        ["registered_utc"] = Stamp
    };
    internal static JsonObject Completion(string role, long rows, int blobs, long processed) => new()
    {
        ["schema_version"] = 1, ["kind"] = "consumer_completion", ["run_id"] = "synthetic-consumer",
        ["scenario_id"] = "C03", ["configuration_id"] = "C0", ["phase"] = "C0", ["repeat_number"] = 1,
        ["role"] = role, ["instance_id"] = role, ["job_name"] = "job-" + role, ["pod_uid"] = "uid-" + role,
        ["source_commit"] = new string('a', 40), ["rows"] = rows, ["record_blobs"] = blobs,
        ["messages_processed"] = processed, ["closed_utc"] = Stamp
    };
    internal static JsonObject Assignment(string role, int[] partitions, long sequence = 0) => new()
    {
        ["schema_version"] = 1, ["kind"] = "partition_event", ["instance_id"] = role, ["row_sequence"] = sequence,
        ["event"] = "assigned", ["partitions"] = new JsonArray(partitions.Select(p => JsonValue.Create(p)).ToArray<JsonNode?>()), ["event_utc"] = Stamp
    };
    internal static JsonObject Commit(string role, long sequence = 1, string source = "callback", string outcome = "callback", int error = 0) => new()
    {
        ["schema_version"] = 1, ["kind"] = "commit", ["instance_id"] = role, ["row_sequence"] = sequence,
        ["source"] = source, ["outcome"] = outcome, ["error_code"] = error, ["error_reason"] = error == 0 ? null : "synthetic",
        ["submitted"] = source == "callback" ? null : new JsonArray(Offset(110)),
        ["offsets"] = new JsonArray(Offset(110, true)), ["event_utc"] = Stamp
    };
    internal static JsonObject Offset(long offset, bool error = false, int partition = 0)
    {
        var value = new JsonObject { ["topic"] = "document-events", ["partition"] = partition, ["offset"] = offset };
        if (error) value["error_code"] = 0;
        return value;
    }
    internal static JsonObject Processed(string role, long offset = 100, long sequence = 0) => new()
    {
        ["schema_version"] = 1, ["kind"] = "processed", ["instance_id"] = role, ["row_sequence"] = sequence,
        ["topic"] = "document-events", ["partition"] = 0, ["offset"] = offset, ["producer_instance_id"] = "producer",
        ["attempt_id"] = "attempt-" + offset.ToString(CultureInfo.InvariantCulture), ["key"] = "key-" + offset.ToString(CultureInfo.InvariantCulture),
        ["sequence"] = offset, ["slow"] = false, ["processing_started_utc"] = Stamp, ["processing_ended_utc"] = Stamp
    };
    internal void Set(string role, string suffix, JsonObject value) => Blobs[Name(role, suffix)] = JsonSerializer.SerializeToUtf8Bytes(value);
    internal void SetRows(string role, IEnumerable<JsonObject> rows, int index = 0) => Blobs[Name(role, $"record/{index:D6}.jsonl")] = Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(r => r.ToJsonString())) + "\n");
    internal ConsumerArtifactReader Reader() => new(this, Limits, Timeout, Clock);
    internal Task<ConsumerArtifacts> Load(bool completions = true, InputBudget? budget = null) => Reader().ReadAsync(Prefix, budget ?? new(Limits), TestContext.Current.CancellationToken, completions);
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(Blobs.Keys.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, ListingComplete));
    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        Reads.Add(name); OnRead?.Invoke(name);
        return ReadOverride?.Invoke(name, cancellationToken) ?? Task.FromResult(Blobs.GetValueOrDefault(name));
    }
    public Task<byte[]?> ReadBoundedAsync(string name, long maximumBytes, CancellationToken cancellationToken) => ReadAsync(name, cancellationToken);
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken) { Blobs.Add(name, content); return Task.CompletedTask; }
}
