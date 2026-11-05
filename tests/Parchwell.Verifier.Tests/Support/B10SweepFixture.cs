using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;

namespace Parchwell.Verifier.Tests.Support;

internal static class B10SweepFixture
{
    internal static HostEvidenceBuilder CreateRepeat(int repeat, string phase = "C0", bool secondHasAttempt = false)
    {
        string first = repeat == 1 ? "pod-first" : $"pod-first-r{repeat}";
        string secondId = repeat == 1 ? "pod-second" : $"pod-second-r{repeat}";
        var fixture = new HostEvidenceBuilder(instanceId: first, clientId: first, scenarioId: "B10", phase: phase, scenarioSourceId: "A01");
        fixture.ConfigureRepeat(repeat, phase);
        fixture.Options.PodName = first;
        fixture.Options.PodUid = first;
        fixture.RebuildPublicationReferences();
        if (secondHasAttempt)
        {
            ChangePayload(fixture, first, "ledger.jsonl", row =>
            {
                row["delivered"] = false;
                row["error_code"] = -192;
                row["error_name"] = "Local_MsgTimedOut";
                row["persistence_status"] = 1;
            });
            ChangePayload(fixture, first, "manifest.json", firstManifest =>
            {
                firstManifest["guard_observations"]!.AsArray().Add("async_delivery_failure_observed");
                firstManifest["guard_observations"]!.AsArray().Add("possibly_persisted_observed");
            });
            fixture.RebuildPublicationReferences();
        }
        using var second = new HostEvidenceBuilder(instanceId: secondId, clientId: secondId, scenarioId: "B10", phase: phase, scenarioSourceId: "A01");
        second.ConfigureRepeat(repeat, phase);
        second.Options.PodName = secondId;
        second.Options.PodUid = secondId;
        JsonObject manifest = second.Json("manifest.json");
        if (!secondHasAttempt)
        {
            manifest["planned_attempt_count"] = 0;
            manifest["attempt_count"] = 0;
            manifest["inventory"] = new JsonArray();
            second.Inputs["ledger.jsonl"] = [];
        }
        IReadOnlyList<string>? predecessorAttemptIds = secondHasAttempt
            ? PayloadJson(fixture, first, "manifest.json")["inventory"]!.AsArray()
                .Select(attempt => attempt!["attempt_id"]!.GetValue<string>()).ToArray()
            : null;
        manifest["restart"] = Restart(manifest, first, predecessorAttemptIds);
        second.Set("manifest.json", manifest);
        second.RebuildPublicationReferences();
        foreach ((string path, byte[] bytes) in second.LedgerObjects) fixture.AddLedgerObject(path, bytes);
        ChangeBlob(fixture, RegistrationPath(fixture, secondId), registration =>
        {
            registration["job_name"] = fixture.Options.ExpectedProducerJobNames[1];
            registration["predecessor_instance_id"] = first;
        });
        ChangeBlob(fixture, CompletionPath(fixture, secondId), completion => completion["job_name"] = fixture.Options.ExpectedProducerJobNames[1]);
        fixture.PreservePublicationReferences = true;
        SetRecords(fixture, secondHasAttempt ? [Record(first, 0), Record(secondId, 1)] : [Record(first, 0)]);
        return fixture;
    }

    private static JsonObject Restart(JsonObject manifest, string predecessor, IReadOnlyList<string>? predecessorAttemptIds = null)
    {
        var planned = new JsonArray();
        foreach (JsonNode? item in manifest["inventory"]!.AsArray())
        {
            JsonObject attempt = item!.AsObject();
            var entry = new JsonObject { ["predecessor_attempt_id"] = predecessorAttemptIds?[planned.Count] ?? "predecessor-" + planned.Count };
            foreach (string field in new[] { "ordinal", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds" }) entry[field] = attempt[field]!.DeepClone();
            planned.Add(entry);
        }
        return new JsonObject { ["predecessor_instance_id"] = predecessor, ["planned"] = planned };
    }

    private static string Prefix(HostEvidenceBuilder fixture, string instance) => PhasePaths.ProducerPrefix(fixture.Options.RunId!, fixture.Options.Phase!, fixture.Options.Repeat!.Value, instance);
    private static string PublicationPath(HostEvidenceBuilder fixture, string instance, string name) => Prefix(fixture, instance) + "publication/" + name;
    private static string RegistrationPath(HostEvidenceBuilder fixture, string instance) => Prefix(fixture, instance) + "registration.json";
    private static string CompletionPath(HostEvidenceBuilder fixture, string instance) => Prefix(fixture, instance) + "completion.json";
    private static byte[]? Payload(HostEvidenceBuilder fixture, string instance, string name) => instance == fixture.Options.InstanceId ? fixture.Inputs[name] : fixture.LedgerObjects.GetValueOrDefault(PublicationPath(fixture, instance, name));
    internal static JsonObject PayloadJson(HostEvidenceBuilder fixture, string instance, string name) => JsonNode.Parse(Payload(fixture, instance, name)!)!.AsObject();
    internal static void SetPayload(HostEvidenceBuilder fixture, string instance, string name, byte[]? bytes)
    {
        if (instance == fixture.Options.InstanceId) fixture.Inputs[name] = bytes;
        string path = PublicationPath(fixture, instance, name);
        if (bytes is null) fixture.RemoveLedgerObject(path);
        else fixture.AddLedgerObject(path, bytes);
    }
    internal static void ChangePayload(HostEvidenceBuilder fixture, string instance, string name, Action<JsonObject> change, bool refresh = true)
    {
        JsonObject value = PayloadJson(fixture, instance, name);
        change(value);
        SetPayload(fixture, instance, name, Encoding.UTF8.GetBytes(value.ToJsonString() + (name.EndsWith(".jsonl", StringComparison.Ordinal) ? "\n" : "")));
        if (refresh && name != "index.json") RefreshDescriptors(fixture, instance);
    }
    internal static void ChangeRows(HostEvidenceBuilder fixture, string instance, string name, Action<JsonObject> change)
    {
        if (!name.EndsWith(".jsonl", StringComparison.Ordinal)) { ChangePayload(fixture, instance, name, change); return; }
        string[] rows = Encoding.UTF8.GetString(Payload(fixture, instance, name)!).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < rows.Length; index++)
        {
            JsonObject row = JsonNode.Parse(rows[index])!.AsObject();
            change(row);
            rows[index] = row.ToJsonString();
        }
        SetPayload(fixture, instance, name, Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n"));
    }
    private static void ChangeBlob(HostEvidenceBuilder fixture, string path, Action<JsonObject> change)
    {
        JsonObject value = JsonNode.Parse(fixture.LedgerObjects[path])!.AsObject();
        change(value);
        fixture.AddLedgerObject(path, Encoding.UTF8.GetBytes(value.ToJsonString()));
    }
    internal static void RefreshDescriptors(HostEvidenceBuilder fixture, string instance)
    {
        ChangePayload(fixture, instance, "index.json", index =>
        {
            foreach (JsonNode? item in index["artifacts"]!.AsArray())
            {
                JsonObject descriptor = item!.AsObject();
                if (Payload(fixture, instance, descriptor["name"]!.GetValue<string>()) is not { } bytes) continue;
                descriptor["length"] = bytes.LongLength;
                descriptor["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            }
        }, false);
    }
    private static ConsumeResult<byte[], byte[]> Record(string instance, long offset)
    {
        ConsumeResult<byte[], byte[]> record = HostEvidenceBuilder.Record();
        record.Offset = offset;
        ReplaceHeader(record, "instance-id", instance);
        return record;
    }
    private static void ReplaceHeader(ConsumeResult<byte[], byte[]> record, string key, string value)
    {
        record.Message.Headers.Remove(key);
        record.Message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
    private static void SetRecords(HostEvidenceBuilder fixture, params ConsumeResult<byte[], byte[]>[] records)
    {
        int position = 0;
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, records.Length);
        fixture.Kafka.Position = _ => new Offset(position);
        fixture.ConsumeRecord = () => position < records.Length ? records[position++] : null;
    }
}
