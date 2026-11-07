using System.Buffers.Binary;
using System.Globalization;
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
    internal sealed record Message(string Kind, long Sequence, JsonElement Data);
    internal sealed record Checkpoint(Identity Identity, JsonElement Data);
    internal sealed record Selection(Identity Identity, JsonElement Data);
    internal sealed record CohortSeal(Identity Identity, JsonElement Data);
    internal sealed record AbruptManifest(Identity Identity, JsonElement Data);
    internal sealed record TerminationReceipt(Identity Identity, JsonElement Data);
    internal sealed record SuccessorReceipt(Identity Identity, JsonElement Data);

    private static readonly Dictionary<string, string[]> DataMembers = new(StringComparer.Ordinal)
    {
        ["registration"] = ["registered_utc"],
        ["cohort_inventory"] = ["instance_id", "cohort", "inventory_count", "inventory", "bookkeeping_complete"],
        ["capabilities"] = ["request_capture", "fatal_capture_status"],
        ["hello"] = ["challenge", "pid", "start_time_ticks", "boot_id"],
        ["ack"] = ["acknowledged_sequence"],
        ["prepared"] = ["attempt_id", "invocation_ordinal", "cohort", "probe_id", "key", "sequence", "payload_length", "payload_sha256", "predecessor_attempt_id"],
        ["accepted"] = ["attempt_id", "prepared_sequence"],
        ["synchronous_rejection"] = ["attempt_id", "prepared_sequence", "error_code", "error_reason"],
        ["terminal"] = ["attempt_id", "completion_source", "delivered", "error_code", "persistence_status"],
        ["capture_gap"] = ["reason", "first_missing_sequence", "capture_status"],
        ["gap"] = ["reason", "first_missing_sequence", "capture_status"],
        ["pre_acceptance_report"] = ["arm", "attempt_id", "report_length", "report_sha256", "report_identity", "report_sequence", "report_capture_utc"],
        ["checkpoint"] = ["arm", "accepted_attempt_ids", "selected_attempt_ids", "last_complete_sequence", "pre_acceptance_report", "process", "checkpoint_utc", "checkpoint_monotonic_ticks", "release_utc"],
        ["signal"] = ["arm", "requested_signal", "process", "intent_sequence", "request_utc", "request_monotonic_ticks", "return_value", "errno"],
        ["exit"] = ["process", "wait_for_exit_observed", "observed_utc", "monotonic_ticks", "exit_code_local_observation", "runtime_version"],
        ["selection"] = ["checkpoint", "checkpoint_descriptor", "candidate_attempt_ids", "terminal_exclusions", "entries"],
        ["cohort_seal"] = ["cohort", "inventory", "count", "ledger", "drain_complete", "post_drain_statistics", "guard_complete", "fatal_capture_status"],
        ["abrupt_manifest"] = ["journal", "journal_entry_count", "timeout_seal", "control_seal", "selection", "gaps", "registration"],
        ["termination"] = ["manifest", "process", "signals", "exit", "cgroup_before", "cgroup_after", "eof_complete", "mechanical_status", "fatal_capture_status", "native_tail"],
        ["successor_receipt"] = ["first_termination", "selection", "registration", "artifacts", "ready_utc", "release_utc", "invocations", "cleanup_completed", "cross_host_timing"],
    };

    internal static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);
    internal static byte[] SerializeEnvelope(string kind, Identity identity, object data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 2, dialect = "b10_abrupt", purpose = "diagnostic_trial",
            synthetic = true, scientific_admissible = false, kind, identity, data }, JsonOptions);
        if (bytes.Length > MaximumBodyBytes) throw Gap("frame_oversize");
        return bytes;
    }

    internal static JsonElement ReadEnvelope(ReadOnlyMemory<byte> bytes, string kind, params string[] extraMembers)
    {
        var root = Parse(bytes);
        ExactMembers(root, ["schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data", .. extraMembers]);
        if (Nonnegative(root.GetProperty("schema_version")) != 2 || RequiredString(root.GetProperty("dialect")) != "b10_abrupt" ||
            RequiredString(root.GetProperty("purpose")) != "diagnostic_trial" || root.GetProperty("synthetic").ValueKind != JsonValueKind.True ||
            root.GetProperty("scientific_admissible").ValueKind != JsonValueKind.False || RequiredString(root.GetProperty("kind")) != kind)
            throw Gap("malformed_frame");
        ValidateIdentity(root.GetProperty("identity"));
        ValidateData(kind, root.GetProperty("data"));
        return root;
    }

    internal static byte[] WriteFrame(Frame frame)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 2, dialect = "b10_abrupt", purpose = "diagnostic_trial",
            synthetic = true, scientific_admissible = false, frame.Kind, frame.Identity, frame.Sequence, frame.ObservedUtc,
            frame.MonotonicTicks, frame.ClockFrequency, frame.Data }, JsonOptions);
        return bytes;
    }

    internal static Frame ReadFrame(ReadOnlyMemory<byte> bytes)
    {
        var parsed = Parse(bytes, 16777216);
        if (parsed.ValueKind != JsonValueKind.Object || !parsed.TryGetProperty("kind", out var tag)) throw Gap("malformed_frame");
        var kind = RequiredString(tag);
        if (kind is not ("prepared" or "accepted" or "synchronous_rejection" or "terminal" or "checkpoint" or "signal" or "exit" or "gap")) throw Gap("malformed_frame");
        var root = ReadEnvelopeCore(parsed, kind);
        var sequence = Nonnegative(root.GetProperty("sequence"));
        var frequency = Nonnegative(root.GetProperty("clock_frequency"));
        if (sequence == 0 || frequency == 0) throw Gap("malformed_frame");
        var utc = RequiredString(root.GetProperty("observed_utc"));
        if (!IsUtc(utc)) throw Gap("malformed_frame");
        return new(root.GetProperty("identity").Deserialize<Identity>(JsonOptions)!, sequence, utc,
            Nonnegative(root.GetProperty("monotonic_ticks")), frequency, kind, root.GetProperty("data").Clone());
    }

    private static JsonElement ReadEnvelopeCore(JsonElement root, string kind)
    {
        ExactMembers(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "sequence", "observed_utc", "monotonic_ticks", "clock_frequency", "data");
        if (Nonnegative(root.GetProperty("schema_version")) != 2 || RequiredString(root.GetProperty("dialect")) != "b10_abrupt" ||
            RequiredString(root.GetProperty("purpose")) != "diagnostic_trial" || root.GetProperty("synthetic").ValueKind != JsonValueKind.True ||
            root.GetProperty("scientific_admissible").ValueKind != JsonValueKind.False) throw Gap("malformed_frame");
        ValidateIdentity(root.GetProperty("identity"));
        ValidateData(kind, root.GetProperty("data"));
        return root;
    }

    internal static void ValidateData(string kind, JsonElement data)
    {
        if (!DataMembers.TryGetValue(kind, out var members)) throw Gap("malformed_frame");
        ExactMembers(data, members);
        foreach (var property in data.EnumerateObject())
        {
            if (property.Name is "sequence" or "invocation_ordinal" or "payload_length" or "prepared_sequence" or "acknowledged_sequence" or "start_time_ticks" or "last_complete_sequence" or "checkpoint_monotonic_ticks" or "request_monotonic_ticks" or "monotonic_ticks")
                _ = Nonnegative(property.Value);
        }
        if (kind is "prepared" or "accepted" or "synchronous_rejection" or "terminal") _ = RequiredString(data.GetProperty("attempt_id"));
        if (kind == "prepared")
        {
            if (RequiredString(data.GetProperty("cohort")) is not ("timeout" or "same_instance" or "cross_restart")) throw Gap("malformed_frame");
            _ = RequiredString(data.GetProperty("probe_id"));
            _ = RequiredString(data.GetProperty("key"));
            var hash = RequiredString(data.GetProperty("payload_sha256"));
            if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || Nonnegative(data.GetProperty("payload_length")) > 900000) throw Gap("malformed_frame");
            if (data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null) _ = RequiredString(data.GetProperty("predecessor_attempt_id"));
        }
        if (kind == "terminal")
        {
            _ = RequiredString(data.GetProperty("completion_source"));
            if (data.GetProperty("delivered").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Gap("malformed_frame");
        }
        if (kind is "checkpoint" or "signal" or "exit") ValidateProcess(data.GetProperty("process"));
        if (kind == "checkpoint")
        {
            if (RequiredString(data.GetProperty("arm")) is not ("same_instance" or "cross_restart")) throw Gap("malformed_frame");
            ValidateStringArray(data.GetProperty("accepted_attempt_ids"));
            ValidateStringArray(data.GetProperty("selected_attempt_ids"));
            ValidateReportDescriptor(data.GetProperty("pre_acceptance_report"));
            if (!IsUtc(RequiredString(data.GetProperty("checkpoint_utc"))) || !IsUtc(RequiredString(data.GetProperty("release_utc")))) throw Gap("malformed_frame");
        }
        if (kind == "selection")
        {
            ValidateDescriptor(data.GetProperty("checkpoint_descriptor"));
            ValidateStringArray(data.GetProperty("candidate_attempt_ids"));
            if (data.GetProperty("terminal_exclusions").ValueKind != JsonValueKind.Array || data.GetProperty("entries").ValueKind != JsonValueKind.Array) throw Gap("malformed_frame");
            foreach (var exclusion in data.GetProperty("terminal_exclusions").EnumerateArray())
            {
                ExactMembers(exclusion, "attempt_id", "terminal_sequence");
                _ = RequiredString(exclusion.GetProperty("attempt_id"));
                if (Nonnegative(exclusion.GetProperty("terminal_sequence")) == 0) throw Gap("malformed_frame");
            }
            foreach (var entry in data.GetProperty("entries").EnumerateArray())
            {
                ExactMembers(entry, "attempt_id", "key", "sequence", "payload_length", "payload_sha256", "payload_base64");
                _ = RequiredString(entry.GetProperty("attempt_id"));
                _ = RequiredString(entry.GetProperty("key"));
                _ = Nonnegative(entry.GetProperty("sequence"));
                _ = Nonnegative(entry.GetProperty("payload_length"));
                ValidateHash(entry.GetProperty("payload_sha256"));
                _ = RequiredString(entry.GetProperty("payload_base64"));
            }
        }
        if (kind == "cohort_seal")
        {
            foreach (string name in new[] { "inventory", "ledger", "post_drain_statistics" }) ValidateDescriptor(data.GetProperty(name));
            _ = Nonnegative(data.GetProperty("count"));
            if (RequiredString(data.GetProperty("cohort")) is not ("timeout" or "same_instance" or "cross_restart")) throw Gap("malformed_frame");
            Boolean(data.GetProperty("drain_complete")); Boolean(data.GetProperty("guard_complete"));
        }
        if (kind == "abrupt_manifest")
        {
            foreach (string name in new[] { "journal", "timeout_seal", "control_seal", "selection", "registration" }) ValidateDescriptor(data.GetProperty(name));
            if (Nonnegative(data.GetProperty("journal_entry_count")) > MaximumEntries) throw Gap("malformed_frame");
            ValidateStringArray(data.GetProperty("gaps"));
        }
        if (kind == "termination")
        {
            ValidateDescriptor(data.GetProperty("manifest"));
            ValidateProcess(data.GetProperty("process"));
            if (data.GetProperty("signals").ValueKind != JsonValueKind.Array) throw Gap("malformed_frame");
            foreach (var signal in data.GetProperty("signals").EnumerateArray()) ValidateData("signal", signal);
            ExactMembers(data.GetProperty("exit"), "wait_for_exit_observed", "exit_code_local_observation", "runtime_version");
            foreach (string name in new[] { "cgroup_before", "cgroup_after" })
            {
                var observation = data.GetProperty(name);
                ExactMembers(observation, "boot_id", "cgroup_path", "directory_identity", "oom_kill");
                _ = RequiredString(observation.GetProperty("boot_id"));
                _ = RequiredString(observation.GetProperty("cgroup_path"));
                _ = RequiredString(observation.GetProperty("directory_identity"));
                _ = Nonnegative(observation.GetProperty("oom_kill"));
            }
            Boolean(data.GetProperty("eof_complete"));
            if (RequiredString(data.GetProperty("mechanical_status")) is not ("complete" or "incomplete")) throw Gap("malformed_frame");
        }
        if (kind == "successor_receipt")
        {
            foreach (string name in new[] { "first_termination", "selection", "registration" }) ValidateDescriptor(data.GetProperty(name));
            if (data.GetProperty("artifacts").ValueKind != JsonValueKind.Array || data.GetProperty("invocations").ValueKind != JsonValueKind.Array) throw Gap("malformed_frame");
            foreach (var artifact in data.GetProperty("artifacts").EnumerateArray()) ValidateDescriptor(artifact);
            if (!IsUtc(RequiredString(data.GetProperty("ready_utc"))) || !IsUtc(RequiredString(data.GetProperty("release_utc"))) ||
                RequiredString(data.GetProperty("cross_host_timing")) != "not_verified") throw Gap("malformed_frame");
            Boolean(data.GetProperty("cleanup_completed"));
            foreach (var invocation in data.GetProperty("invocations").EnumerateArray())
            {
                ExactMembers(invocation, "attempt_id", "predecessor_attempt_id", "key", "sequence", "payload_sha256", "invocation_utc", "monotonic_ticks");
                _ = RequiredString(invocation.GetProperty("attempt_id")); _ = RequiredString(invocation.GetProperty("predecessor_attempt_id"));
                _ = RequiredString(invocation.GetProperty("key")); _ = Nonnegative(invocation.GetProperty("sequence"));
                _ = Nonnegative(invocation.GetProperty("monotonic_ticks")); ValidateHash(invocation.GetProperty("payload_sha256"));
                if (!IsUtc(RequiredString(invocation.GetProperty("invocation_utc")))) throw Gap("malformed_frame");
            }
        }
        if (kind == "cohort_inventory")
        {
            _ = RequiredString(data.GetProperty("instance_id"));
            if (RequiredString(data.GetProperty("cohort")) is not ("same_instance" or "cross_restart") || data.GetProperty("inventory").ValueKind != JsonValueKind.Array ||
                Nonnegative(data.GetProperty("inventory_count")) != data.GetProperty("inventory").GetArrayLength()) throw Gap("malformed_frame");
            Boolean(data.GetProperty("bookkeeping_complete"));
            foreach (var entry in data.GetProperty("inventory").EnumerateArray())
            {
                ExactMembers(entry, "attempt_id", "invocation_ordinal", "predecessor_attempt_id", "key", "sequence", "payload_length", "payload_sha256");
                _ = RequiredString(entry.GetProperty("attempt_id")); _ = RequiredString(entry.GetProperty("key"));
                _ = Nonnegative(entry.GetProperty("invocation_ordinal")); _ = Nonnegative(entry.GetProperty("sequence"));
                _ = Nonnegative(entry.GetProperty("payload_length")); ValidateHash(entry.GetProperty("payload_sha256"));
                if (entry.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null) _ = RequiredString(entry.GetProperty("predecessor_attempt_id"));
            }
        }
        if (kind == "registration" && !IsUtc(RequiredString(data.GetProperty("registered_utc")))) throw Gap("malformed_frame");
        if (kind is "cohort_seal" or "termination" or "capabilities")
            if (RequiredString(data.GetProperty("fatal_capture_status")) != "not_implemented_pr3") throw Gap("malformed_frame");
        if (kind is "gap" or "capture_gap")
        {
            if (RequiredString(data.GetProperty("reason")) is not ("frame_oversize" or "queue_full" or "journal_bytes_exceeded" or "journal_entries_exceeded" or "writer_failed" or "malformed_frame" or "sequence_gap")) throw Gap("malformed_frame");
            if (data.GetProperty("first_missing_sequence").ValueKind != JsonValueKind.Null) _ = Nonnegative(data.GetProperty("first_missing_sequence"));
            _ = RequiredString(data.GetProperty("capture_status"));
        }
    }

    private static void Boolean(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Gap("malformed_frame");
    }

    private static void ValidateStringArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > MaximumEntries) throw Gap("malformed_frame");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray()) if (!seen.Add(RequiredString(item))) throw Gap("malformed_frame");
    }

    private static void ValidateProcess(JsonElement process)
    {
        ExactMembers(process, "pid", "parent_pid", "boot_id", "start_identity");
        if (Nonnegative(process.GetProperty("pid")) is 0 or > int.MaxValue || Nonnegative(process.GetProperty("parent_pid")) is 0 or > int.MaxValue) throw Gap("malformed_frame");
        _ = RequiredString(process.GetProperty("boot_id"));
        _ = Nonnegative(process.GetProperty("start_identity"));
    }

    private static void ValidateHash(JsonElement value)
    {
        string hash = RequiredString(value);
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Gap("malformed_frame");
    }

    internal static void ValidateDescriptor(JsonElement descriptor)
    {
        ExactMembers(descriptor, "name", "length", "sha256");
        string name = RequiredString(descriptor.GetProperty("name"));
        if (System.IO.Path.IsPathRooted(name) || name.Contains('\\') || name.Split('/').Any(part => part is ".." or "." or "")) throw Gap("malformed_frame");
        _ = Nonnegative(descriptor.GetProperty("length"));
        ValidateHash(descriptor.GetProperty("sha256"));
    }

    private static void ValidateReportDescriptor(JsonElement descriptor)
    {
        ExactMembers(descriptor, "name", "length", "sha256", "report_identity", "report_sequence", "report_capture_utc");
        if (RequiredString(descriptor.GetProperty("name")) is not ("abrupt-child/same_instance-pre-acceptance-report.json" or "abrupt-child/cross_restart-pre-acceptance-report.json") ||
            Nonnegative(descriptor.GetProperty("length")) is 0 or > 1048576 || Nonnegative(descriptor.GetProperty("report_sequence")) == 0 ||
            !IsUtc(RequiredString(descriptor.GetProperty("report_capture_utc")))) throw Gap("malformed_frame");
        ValidateHash(descriptor.GetProperty("sha256"));
        var report = descriptor.GetProperty("report_identity");
        ExactMembers(report, "scenario_id", "run_id", "role", "client_id");
        if (RequiredString(report.GetProperty("scenario_id")) != "B10" || RequiredString(report.GetProperty("role")) != "LoadGenProducer") throw Gap("malformed_frame");
        _ = RequiredString(report.GetProperty("run_id")); _ = RequiredString(report.GetProperty("client_id"));
    }

    private static string RequiredString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString())) throw Gap("malformed_frame");
        return value.GetString()!;
    }

    internal static async Task WriteMessageAsync(Stream stream, Message message, CancellationToken cancellationToken = default)
    {
        ValidateData(message.Kind, message.Data);
        if (message.Sequence <= 0) throw Gap("sequence_gap");
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (body.Length > MaximumBodyBytes) throw Gap("frame_oversize");
        var prefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, checked((uint)body.Length));
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<Message?> ReadMessageAsync(Stream stream, long expectedSequence, CancellationToken cancellationToken = default)
    {
        var header = new byte[4];
        var first = await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (first == 0) return null;
        try { await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false); }
        catch (EndOfStreamException) { throw Gap("malformed_frame"); }
        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length > MaximumBodyBytes) throw Gap("frame_oversize");
        if (length == 0) throw Gap("malformed_frame");
        var body = new byte[(int)length];
        try { await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false); }
        catch (EndOfStreamException) { throw Gap("malformed_frame"); }
        var root = Parse(body);
        ExactMembers(root, "kind", "sequence", "data");
        var sequence = Nonnegative(root.GetProperty("sequence"));
        if (sequence != expectedSequence || sequence == 0) throw Gap("sequence_gap");
        var kind = RequiredString(root.GetProperty("kind"));
        if (kind is not ("hello" or "prepared" or "accepted" or "synchronous_rejection" or "terminal" or "capture_gap" or "pre_acceptance_report" or "ack")) throw Gap("malformed_frame");
        ValidateData(kind, root.GetProperty("data"));
        return new(kind, sequence, root.GetProperty("data").Clone());
    }

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
            expected.Name.Contains("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(expected.Name) || expected.Name.Contains('\\')) throw new InvalidDataException("b10_abrupt_publication_failed");
    }

    internal static InvalidDataException Gap(string reason) => new("b10_abrupt_capture_gap:" + reason);
}
