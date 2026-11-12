using System.Security.Cryptography;
using System.Text.Json;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal static class AbruptEvidenceCodec
{
    internal static readonly string[] FirstFiles = ["registration.json", "journal.jsonl", "abrupt-manifest.json", "termination.json"];
    internal static readonly string[] ReportFiles = ["same_instance-pre-acceptance-report.json", "control-post-drain-report.json", "cross_restart-pre-acceptance-report.json"];
    internal static readonly string[] SuccessorFiles = ["registration.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "completion.json"];
    internal static readonly string[] ProducingKinds = ["child_started", "warmup_prepared", "prepared", "pre_acceptance_report", "accepted", "synchronous_rejection", "cut", "control_drained", "post_drain_report", "kill_intent", "signal_failed", "no_kill"];
    private static readonly Dictionary<string, string[]> DataMembers = new(StringComparer.Ordinal)
    {
        ["registration"] = ["registered_utc"],
        ["child_started"] = ["pid", "start_time_ticks", "boot_id"],
        ["warmup_prepared"] = ["attempt_id", "arm", "key", "payload_length", "payload_sha256"],
        ["warmup_terminal"] = ["attempt_id", "completion_source", "delivered", "error_code", "persistence_status", "partition", "offset"],
        ["prepared"] = ["attempt_id", "arm", "role", "ordinal", "probe_id", "key", "sequence", "payload_length", "payload_sha256", "predecessor_attempt_id"],
        ["accepted"] = ["attempt_id", "prepared_sequence"],
        ["synchronous_rejection"] = ["attempt_id", "prepared_sequence", "error_code", "error_reason"],
        ["terminal"] = ["attempt_id", "completion_source", "delivered", "error_code", "persistence_status"],
        ["native_error"] = ["capture_sequence", "error_code", "error_name", "is_error", "is_fatal", "callback_monotonic_ticks"],
        ["gap"] = ["reason", "first_missing_sequence", "capture_status"],
        ["pre_acceptance_report"] = ["arm", "attempt_id", "name", "length", "sha256", "report_identity", "report_sequence", "report_capture_utc", "warmup_attempt_id", "partition"],
        ["post_drain_report"] = ["arm", "attempt_id", "name", "length", "sha256", "report_identity", "report_sequence", "report_capture_utc"],
        ["cut"] = ["arm", "cut_attempt_id", "release_utc", "unconfirmed_attempt_ids"],
        ["kill_intent"] = ["arm", "cut_attempt_id", "release_utc"],
        ["control_drained"] = ["flush_returned", "out_queue_length"],
        ["signal_failed"] = ["return_value", "errno"],
        ["no_kill"] = ["reason", "cross_restart_admitted"],
        ["abrupt_manifest"] = ["journal", "journal_records", "report_files", "gaps"],
        ["termination"] = ["termination", "outcome", "detail", "signal_observation", "successor_permitted", "child", "exit_observed", "exit_code_local_observation", "runtime_version", "wait_bound_milliseconds", "cgroup_before", "cgroup_after", "control_unconfirmed_attempt_ids", "kill_unconfirmed_attempt_ids", "capture_gaps", "native_error_count", "native_fatal_count", "native_tail", "manifest"],
        ["successor_receipt"] = ["first_termination", "journal", "kill_unconfirmed_attempt_ids", "registration", "artifacts", "ready_utc", "release_utc", "invocations", "cleanup_completed", "cross_host_timing"],
    };
    internal static JsonElement Parse(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        CheckDuplicates(document.RootElement);
        return document.RootElement.Clone();
    }

    internal static JsonElement Envelope(byte[] bytes, string kind, VerifierHostOptions options,
        string pod, string? predecessor, string? uid = null, bool journal = false)
    {
        JsonElement root = Parse(bytes);
        Exact(root, journal
            ? ["schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data", "sequence", "observed_utc", "monotonic_ticks", "clock_frequency"]
            : ["schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data"]);
        Require(root.GetProperty("schema_version").GetInt32() == 3 && Text(root, "dialect") == "b10_abrupt" &&
            Text(root, "purpose") == "diagnostic_trial" && root.GetProperty("synthetic").GetBoolean() &&
            !root.GetProperty("scientific_admissible").GetBoolean() && Text(root, "kind") == kind, "envelope");
        JsonElement identity = root.GetProperty("identity");
        Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
        Require(Text(identity, "run_id") == options.RunId && Text(identity, "scenario_id") == "B10" &&
            Text(identity, "configuration_id") == "C1" && Text(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == 1 &&
            Text(identity, "source_commit") == options.VerifierCommit && Text(identity, "image_reference") == options.LoadGenImage &&
            Text(identity, "job_name") == options.RunId + (predecessor is null ? "-c1-r1-loadgen-restart" : "-c1-r1-loadgen-successor") &&
            Text(identity, "pod_name") == pod && Text(identity, "instance_id") == pod &&
            NullableText(identity, "predecessor_instance_id") == predecessor && (uid is null || Text(identity, "pod_uid") == uid), "identity");
        _ = Text(identity, "pod_uid");
        Require(DataMembers.TryGetValue(kind, out string[]? fields), "kind");
        Exact(root.GetProperty("data"), fields!);
        return root;
    }

    internal static void Bind(JsonElement descriptor, string name, byte[] bytes)
    {
        Exact(descriptor, "name", "length", "sha256");
        Require(Text(descriptor, "name") == name && descriptor.GetProperty("length").GetInt64() == bytes.LongLength &&
            Text(descriptor, "sha256") == Convert.ToHexStringLower(SHA256.HashData(bytes)), "hash_length");
    }
    internal static string Text(JsonElement element, string name)
    {
        string? value = element.GetProperty(name).GetString();
        Require(!string.IsNullOrEmpty(value), name);
        return value!;
    }
    internal static string? NullableText(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Text(element, name);
    internal static string[] Strings(JsonElement value) => value.EnumerateArray().Select(item => item.GetString() ?? throw new FormatException("string_array")).ToArray();
    internal static void Exact(JsonElement value, params string[] fields)
    {
        Require(value.ValueKind == JsonValueKind.Object, "object");
        string[] actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        Require(actual.Length == fields.Length && actual.Distinct(StringComparer.Ordinal).Count() == fields.Length &&
            !actual.Except(fields, StringComparer.Ordinal).Any(), "members");
    }
    internal static void Require(bool condition, string detail)
    {
        if (!condition) throw new FormatException(detail);
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Require(seen.Add(property.Name), "duplicate_member");
                CheckDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) CheckDuplicates(item);
    }
}
