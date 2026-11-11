using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parchwell.LoadGen.Run.Abrupt;

internal static class B10AbruptWire
{
    internal const int MaximumBodyBytes = 65536;
    internal const int MaximumEntries = 4096;
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
    };
    internal sealed record Identity(string RunId, string ScenarioId, string ConfigurationId, string Phase, int Repeat,
        string SourceCommit, string ImageReference, string JobName, string PodName, string PodUid, string InstanceId,
        string? PredecessorInstanceId);
    internal sealed record Descriptor(string Name, long Length, string Sha256);
    internal sealed record Frame(Identity Identity, long Sequence, string ObservedUtc, long MonotonicTicks,
        long ClockFrequency, string Kind, JsonElement Data);
    private static readonly Dictionary<string, string[]> DataMembers = new(StringComparer.Ordinal)
    {
        ["registration"] = ["registered_utc"],
        ["cohort_inventory"] = ["instance_id", "cohort", "inventory_count", "inventory", "bookkeeping_complete"],
        ["capabilities"] = ["request_capture", "fatal_capture_status"],
        ["child_started"] = ["pid", "start_time_ticks", "boot_id"],
        ["prepared"] = ["attempt_id", "arm", "role", "ordinal", "probe_id", "key", "sequence", "payload_length", "payload_sha256", "predecessor_attempt_id"],
        ["accepted"] = ["attempt_id", "prepared_sequence"],
        ["synchronous_rejection"] = ["attempt_id", "prepared_sequence", "error_code", "error_reason"],
        ["terminal"] = ["attempt_id", "completion_source", "delivered", "error_code", "persistence_status"],
        ["native_error"] = ["capture_sequence", "error_code", "error_name", "is_error", "is_fatal", "callback_monotonic_ticks"],
        ["gap"] = ["reason", "first_missing_sequence", "capture_status"],
        ["pre_acceptance_report"] = ["arm", "attempt_id", "name", "length", "sha256", "report_identity", "report_sequence", "report_capture_utc"],
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
    internal static bool IsProducingKind(string kind) => kind is "child_started" or "prepared" or "pre_acceptance_report" or "accepted" or "synchronous_rejection" or "cut" or "control_drained" or "post_drain_report" or "kill_intent" or "signal_failed" or "no_kill";
    internal static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);
    internal static byte[] SerializeEnvelope(string kind, Identity identity, object data)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 3, dialect = "b10_abrupt", purpose = "diagnostic_trial", synthetic = true, scientific_admissible = false, kind, identity, data }, JsonOptions);
        if (bytes.Length > MaximumBodyBytes) throw Gap("frame_oversize");
        return bytes;
    }
    internal static JsonElement ReadEnvelope(ReadOnlyMemory<byte> bytes, string kind, params string[] extraMembers)
    {
        JsonElement root = Parse(bytes);
        ExactMembers(root, ["schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data", ..extraMembers]);
        ValidateEnvelope(root, kind); return root;
    }
    private static void ValidateEnvelope(JsonElement root, string kind)
    {
        if (Nonnegative(root.GetProperty("schema_version")) != 3 || RequiredString(root.GetProperty("dialect")) != "b10_abrupt" || RequiredString(root.GetProperty("purpose")) != "diagnostic_trial" || root.GetProperty("synthetic").ValueKind != JsonValueKind.True || root.GetProperty("scientific_admissible").ValueKind != JsonValueKind.False || RequiredString(root.GetProperty("kind")) != kind) throw Gap("malformed_frame");
        ValidateIdentity(root.GetProperty("identity")); ValidateData(kind, root.GetProperty("data"));
    }
    internal static byte[] WriteFrame(Frame frame) => JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 3, dialect = "b10_abrupt", purpose = "diagnostic_trial", synthetic = true, scientific_admissible = false, frame.Kind, frame.Identity, frame.Sequence, frame.ObservedUtc, frame.MonotonicTicks, frame.ClockFrequency, frame.Data }, JsonOptions);
    internal static Frame ReadFrame(ReadOnlyMemory<byte> bytes)
    {
        JsonElement root = Parse(bytes);
        ExactMembers(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "sequence", "observed_utc", "monotonic_ticks", "clock_frequency", "data");
        string kind = RequiredString(root.GetProperty("kind"));
        if (!IsProducingKind(kind) && kind is not ("terminal" or "native_error" or "gap")) throw Gap("malformed_frame");
        ValidateEnvelope(root, kind);
        long sequence = Nonnegative(root.GetProperty("sequence")), frequency = Nonnegative(root.GetProperty("clock_frequency"));
        string utc = RequiredString(root.GetProperty("observed_utc"));
        if (sequence == 0 || frequency == 0 || !IsUtc(utc)) throw Gap("malformed_frame");
        return new(root.GetProperty("identity").Deserialize<Identity>(JsonOptions)!, sequence, utc, Nonnegative(root.GetProperty("monotonic_ticks")), frequency, kind, root.GetProperty("data").Clone());
    }
    internal static void ValidateData(string kind, JsonElement data)
    {
        if (!DataMembers.TryGetValue(kind, out string[]? members)) throw Gap("malformed_frame");
        ExactMembers(data, members);
        switch (kind)
        {
            case "child_started": Positive(data.GetProperty("pid")); _ = Nonnegative(data.GetProperty("start_time_ticks")); _ = RequiredString(data.GetProperty("boot_id")); break;
            case "prepared":
                Attempt(data); Arm(data); if (RequiredString(data.GetProperty("role")) is not ("original" or "resend")) throw Gap("malformed_frame");
                long ordinal = Nonnegative(data.GetProperty("ordinal")); if (ordinal > 4 || Nonnegative(data.GetProperty("sequence")) != ordinal || Nonnegative(data.GetProperty("payload_length")) > 900000) throw Gap("malformed_frame");
                _ = RequiredString(data.GetProperty("key")); _ = RequiredString(data.GetProperty("probe_id")); ValidateHash(data.GetProperty("payload_sha256"));
                bool original = data.GetProperty("role").GetString() == "original";
                if (original != (data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null)) throw Gap("malformed_frame");
                if (!original) _ = RequiredString(data.GetProperty("predecessor_attempt_id")); break;
            case "accepted": Attempt(data); Positive(data.GetProperty("prepared_sequence")); break;
            case "synchronous_rejection": Attempt(data); Positive(data.GetProperty("prepared_sequence")); Integer(data.GetProperty("error_code")); _ = RequiredString(data.GetProperty("error_reason")); break;
            case "terminal": Attempt(data); if (RequiredString(data.GetProperty("completion_source")) != "callback") throw Gap("malformed_frame"); Boolean(data.GetProperty("delivered")); Integer(data.GetProperty("error_code")); if (data.GetProperty("persistence_status").ValueKind != JsonValueKind.Null) Integer(data.GetProperty("persistence_status")); break;
            case "native_error": Positive(data.GetProperty("capture_sequence")); Integer(data.GetProperty("error_code")); if (RequiredString(data.GetProperty("error_name")).Length > 128) throw Gap("malformed_frame"); Boolean(data.GetProperty("is_error")); Boolean(data.GetProperty("is_fatal")); _ = Nonnegative(data.GetProperty("callback_monotonic_ticks")); break;
            case "pre_acceptance_report": case "post_drain_report": ValidateReportDescriptor(data, kind); break;
            case "cut": case "kill_intent": Arm(data); if (data.GetProperty("arm").GetString() != (kind == "cut" ? "same_instance" : "cross_restart")) throw Gap("malformed_frame"); _ = RequiredString(data.GetProperty("cut_attempt_id")); Utc(data.GetProperty("release_utc")); if (kind == "cut") ValidateStringArray(data.GetProperty("unconfirmed_attempt_ids")); break;
            case "control_drained": Boolean(data.GetProperty("flush_returned")); _ = Nonnegative(data.GetProperty("out_queue_length")); break;
            case "signal_failed": Integer(data.GetProperty("return_value")); Integer(data.GetProperty("errno")); break;
            case "no_kill": if (!NoKillReasons.Contains(RequiredString(data.GetProperty("reason")), StringComparer.Ordinal)) throw Gap("malformed_frame"); Boolean(data.GetProperty("cross_restart_admitted")); break;
            case "gap": if (B10AbruptDiagnostics.GapReasonCode(RequiredString(data.GetProperty("reason"))) < 0 || RequiredString(data.GetProperty("capture_status")) != "invalid") throw Gap("malformed_frame"); if (data.GetProperty("first_missing_sequence").ValueKind != JsonValueKind.Null) Positive(data.GetProperty("first_missing_sequence")); break;
            case "registration": Utc(data.GetProperty("registered_utc")); break;
            case "abrupt_manifest": ValidateDescriptor(data.GetProperty("journal")); if (Nonnegative(data.GetProperty("journal_records")) > MaximumEntries) throw Gap("malformed_frame"); ValidateStringArray(data.GetProperty("gaps")); Descriptors(data.GetProperty("report_files")); break;
            case "termination": ValidateTermination(data); break;
            case "successor_receipt": foreach (string name in new[] { "first_termination", "journal", "registration" }) ValidateDescriptor(data.GetProperty(name)); ValidateStringArray(data.GetProperty("kill_unconfirmed_attempt_ids")); Descriptors(data.GetProperty("artifacts")); Utc(data.GetProperty("ready_utc")); Utc(data.GetProperty("release_utc")); Boolean(data.GetProperty("cleanup_completed")); if (RequiredString(data.GetProperty("cross_host_timing")) != "not_verified" || data.GetProperty("invocations").ValueKind != JsonValueKind.Array) throw Gap("malformed_frame"); foreach (var invocation in data.GetProperty("invocations").EnumerateArray()) { ExactMembers(invocation,"attempt_id","predecessor_attempt_id","key","sequence","payload_sha256","invocation_utc","monotonic_ticks"); Attempt(invocation); _ = RequiredString(invocation.GetProperty("predecessor_attempt_id")); _ = RequiredString(invocation.GetProperty("key")); _ = Nonnegative(invocation.GetProperty("sequence")); _ = Nonnegative(invocation.GetProperty("monotonic_ticks")); ValidateHash(invocation.GetProperty("payload_sha256")); Utc(invocation.GetProperty("invocation_utc")); } break;
            case "cohort_inventory": _ = RequiredString(data.GetProperty("instance_id")); if (RequiredString(data.GetProperty("cohort")) != "cross_restart" || data.GetProperty("inventory").ValueKind != JsonValueKind.Array || Nonnegative(data.GetProperty("inventory_count")) != data.GetProperty("inventory").GetArrayLength()) throw Gap("malformed_frame"); Boolean(data.GetProperty("bookkeeping_complete")); foreach (var entry in data.GetProperty("inventory").EnumerateArray()) { ExactMembers(entry,"attempt_id","invocation_ordinal","predecessor_attempt_id","key","sequence","payload_length","payload_sha256"); Attempt(entry); _ = RequiredString(entry.GetProperty("predecessor_attempt_id")); _ = RequiredString(entry.GetProperty("key")); _ = Nonnegative(entry.GetProperty("invocation_ordinal")); _ = Nonnegative(entry.GetProperty("sequence")); _ = Nonnegative(entry.GetProperty("payload_length")); ValidateHash(entry.GetProperty("payload_sha256")); } break;
            case "capabilities": _ = RequiredString(data.GetProperty("request_capture")); _ = RequiredString(data.GetProperty("fatal_capture_status")); break;
        }
    }
    internal static readonly string[] NoKillReasons = ["b10_abrupt_exposure_inconclusive", "b10_abrupt_control_incomplete", "kill_too_early", "b10_abrupt_cut_rejected", "b10_abrupt_sample_missing", "b10_abrupt_report_binding_invalid", "b10_abrupt_guard_failed", "b10_abrupt_signal_preflight_failed", "b10_abrupt_deadline_exhausted", "b10_abrupt_capture_gap"];
    private static void ValidateTermination(JsonElement data)
    {
        if (RequiredString(data.GetProperty("termination")) is not ("intended_self_kill" or "no_kill" or "unexpected_exit" or "unknown")) throw Gap("malformed_frame");
        foreach (string name in new[] { "outcome", "detail", "native_tail" }) if (data.GetProperty(name).ValueKind != JsonValueKind.Null) _ = RequiredString(data.GetProperty(name));
        Boolean(data.GetProperty("successor_permitted")); Boolean(data.GetProperty("exit_observed"));
        if (data.GetProperty("exit_code_local_observation").ValueKind != JsonValueKind.Null) Integer(data.GetProperty("exit_code_local_observation"));
        _ = RequiredString(data.GetProperty("runtime_version")); _ = Nonnegative(data.GetProperty("wait_bound_milliseconds"));
        foreach (string name in new[] { "control_unconfirmed_attempt_ids", "kill_unconfirmed_attempt_ids", "capture_gaps" }) ValidateStringArray(data.GetProperty(name));
        _ = Nonnegative(data.GetProperty("native_error_count")); _ = Nonnegative(data.GetProperty("native_fatal_count")); ValidateDescriptor(data.GetProperty("manifest"));
        if (data.GetProperty("signal_observation").ValueKind != JsonValueKind.Null) ValidateData("signal_failed", data.GetProperty("signal_observation"));
        JsonElement child = data.GetProperty("child"); if (child.ValueKind != JsonValueKind.Null) { ExactMembers(child,"pid","start_time_ticks","boot_id","parent_observed_start_time_ticks"); Positive(child.GetProperty("pid")); foreach (string key in new[] { "start_time_ticks", "parent_observed_start_time_ticks" }) if (child.GetProperty(key).ValueKind != JsonValueKind.Null) _ = Nonnegative(child.GetProperty(key)); _ = RequiredString(child.GetProperty("boot_id")); }
        foreach (string name in new[] { "cgroup_before", "cgroup_after" }) { JsonElement observation = data.GetProperty(name); if (observation.ValueKind == JsonValueKind.Null) continue; ExactMembers(observation,"boot_id","cgroup_path","directory_identity","oom_kill"); foreach (string key in new[] { "boot_id", "cgroup_path", "directory_identity" }) _ = RequiredString(observation.GetProperty(key)); _ = Nonnegative(observation.GetProperty("oom_kill")); }
    }
    private static void ValidateReportDescriptor(JsonElement data, string kind)
    {
        Arm(data); bool post = kind == "post_drain_report";
        if (post ? data.GetProperty("attempt_id").ValueKind != JsonValueKind.Null : string.IsNullOrEmpty(RequiredString(data.GetProperty("attempt_id")))) throw Gap("malformed_frame");
        string name = post ? "control-post-drain-report.json" : data.GetProperty("arm").GetString() + "-pre-acceptance-report.json";
        if (RequiredString(data.GetProperty("name")) != name || Nonnegative(data.GetProperty("length")) is 0 or > 1048576 || post && data.GetProperty("arm").GetString() != "same_instance") throw Gap("malformed_frame");
        ValidateHash(data.GetProperty("sha256")); Positive(data.GetProperty("report_sequence")); Utc(data.GetProperty("report_capture_utc"));
        var report = data.GetProperty("report_identity"); ExactMembers(report,"scenario_id","run_id","role","client_id"); if (RequiredString(report.GetProperty("scenario_id")) != "B10" || RequiredString(report.GetProperty("role")) != "LoadGenProducer") throw Gap("malformed_frame"); _ = RequiredString(report.GetProperty("run_id")); _ = RequiredString(report.GetProperty("client_id"));
    }
    private static void Attempt(JsonElement data) => _ = RequiredString(data.GetProperty("attempt_id"));
    private static void Arm(JsonElement data) { if (RequiredString(data.GetProperty("arm")) is not ("same_instance" or "cross_restart")) throw Gap("malformed_frame"); }
    private static void Utc(JsonElement value) { if (!IsUtc(RequiredString(value))) throw Gap("malformed_frame"); }
    private static void Integer(JsonElement value) { if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _)) throw Gap("malformed_frame"); }
    private static void Positive(JsonElement value) { if (Nonnegative(value) == 0) throw Gap("malformed_frame"); }
    private static void Boolean(JsonElement value) { if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Gap("malformed_frame"); }
    private static string RequiredString(JsonElement value) { if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString())) throw Gap("malformed_frame"); return value.GetString()!; }
    private static void ValidateHash(JsonElement value) { string hash = RequiredString(value); if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Gap("malformed_frame"); }
    internal static void ValidateDescriptor(JsonElement value) { ExactMembers(value,"name","length","sha256"); string name = RequiredString(value.GetProperty("name")); if (name.Contains("..",StringComparison.Ordinal) || Path.IsPathRooted(name) || name.Contains('\\')) throw Gap("malformed_frame"); _ = Nonnegative(value.GetProperty("length")); ValidateHash(value.GetProperty("sha256")); }
    private static void Descriptors(JsonElement value) { if (value.ValueKind != JsonValueKind.Array) throw Gap("malformed_frame"); foreach (var descriptor in value.EnumerateArray()) ValidateDescriptor(descriptor); }
    private static void ValidateStringArray(JsonElement value) { if (value.ValueKind != JsonValueKind.Array) throw Gap("malformed_frame"); HashSet<string> seen = new(StringComparer.Ordinal); foreach (var item in value.EnumerateArray()) if (!seen.Add(RequiredString(item))) throw Gap("malformed_frame"); }
    internal static JsonElement Parse(ReadOnlyMemory<byte> bytes, int maximum = MaximumBodyBytes)
    {
        if (bytes.Length > maximum) throw Gap("frame_oversize");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            CheckTree(document.RootElement);
            return document.RootElement.Clone();
        }
        catch (JsonException) { throw Gap("malformed_frame"); }
    }

    private static void CheckTree(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in element.EnumerateObject())
            {
                if (!seen.Add(member.Name)) throw Gap("malformed_frame");
                CheckTree(member.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() > MaximumEntries) throw Gap("malformed_frame");
            foreach (var item in element.EnumerateArray()) CheckTree(item);
        }
    }

    internal static void ExactMembers(JsonElement value, params string[] members)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Gap("malformed_frame");
        var actual = value.EnumerateObject().Select(member => member.Name).ToArray();
        if (actual.Length != members.Length || actual.Distinct(StringComparer.Ordinal).Count() != members.Length ||
            actual.Except(members, StringComparer.Ordinal).Any()) throw Gap("malformed_frame");
    }

    internal static long Nonnegative(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result) || result < 0) throw Gap("malformed_frame");
        return result;
    }

    internal static bool IsUtc(string value) => value.Length == 33 && value.EndsWith("+00:00", StringComparison.Ordinal) &&
        DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
        parsed.ToString("O", CultureInfo.InvariantCulture) == value;

    internal static void ValidateIdentity(JsonElement value)
    {
        ExactMembers(value, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
        foreach (var member in value.EnumerateObject())
        {
            if (member.Name == "repeat") { if (Nonnegative(member.Value) != 1) throw Gap("malformed_frame"); continue; }
            if (member.Name == "predecessor_instance_id" && member.Value.ValueKind == JsonValueKind.Null) continue;
            if (member.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(member.Value.GetString()) ||
                member.Value.GetString()!.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) throw Gap("malformed_frame");
        }
        string source = value.GetProperty("source_commit").GetString()!;
        string image = value.GetProperty("image_reference").GetString()!;
        int imageDigest = image.LastIndexOf("@sha256:", StringComparison.Ordinal);
        if (source.Length != 40 || source.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || imageDigest <= 0 ||
            image.Length - imageDigest - 8 != 64 || image[(imageDigest + 8)..].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Gap("malformed_frame");
        if (value.GetProperty("instance_id").GetString() != value.GetProperty("pod_name").GetString()) throw Gap("malformed_frame");
        if (value.GetProperty("scenario_id").GetString() != "B10" || value.GetProperty("configuration_id").GetString() != "C1" || value.GetProperty("phase").GetString() != "C1") throw Gap("malformed_frame");
    }

    internal static Descriptor Describe(string path, string name)
    {
        using var stream = File.OpenRead(path);
        return new(name, stream.Length, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    internal static void Reconcile(Descriptor expected, ReadOnlySpan<byte> actual)
    {
        if (expected.Length != actual.Length || expected.Sha256 != Convert.ToHexStringLower(SHA256.HashData(actual)) ||
            expected.Name.Contains("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(expected.Name) || expected.Name.Contains('\\')) throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_publication_failed"));
    }

    internal static InvalidDataException Gap(string reason, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_capture_gap:" + reason), [B10AbruptDiagnostics.Fact(B10AbruptFact.GapReason, B10AbruptDiagnostics.GapReasonCode(reason))], file, member, line);
}
