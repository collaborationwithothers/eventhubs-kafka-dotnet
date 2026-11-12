using System.Text.Json;
using Parchwell.Verifier.Common.Storage;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed record AbruptAttempt(string Id, string Arm, string Role, long Ordinal,
    string Key, long Sequence, string PayloadSha256, string? Predecessor, long JournalSequence);
internal sealed record AbruptTerminal(bool Delivered, int ErrorCode, int? PersistenceStatus);
internal sealed record AbruptFirstEvidence(string Pod, string Uid,
    IReadOnlyDictionary<string, AbruptAttempt> Attempts, IReadOnlyList<string> Control,
    IReadOnlyList<string> Kill, IReadOnlyDictionary<string, string> ControlResends,
    IReadOnlyDictionary<string, AbruptTerminal> Terminals, byte[] JournalBytes, byte[] TerminationBytes);
internal sealed record AbruptSuccessorEvidence(string Pod,
    IReadOnlyDictionary<string, string> Resends, IReadOnlyDictionary<string, AbruptTerminal> Terminals);

internal sealed class AbruptEvidenceReader(VerifierHostOptions options,
    Func<string, long, CancellationToken, Task<byte[]?>> read)
{
    internal async Task<string?> ReadRegistrationAsync(string prefix, string pod, string? predecessor, CancellationToken ct)
    {
        byte[] bytes = await RequiredAsync(prefix + pod + "/registration.json", 65536, ct).ConfigureAwait(false);
        JsonElement registration = Envelope(bytes, "registration", options, pod, predecessor);
        return Text(registration.GetProperty("identity"), "pod_uid");
    }

    internal async Task<AbruptFirstEvidence> ReadFirstAsync(string prefix, string pod, string uid,
        IReadOnlyList<string> names, CancellationToken ct)
    {
        Dictionary<string, byte[]> files = await ReadFilesAsync(prefix, pod, names, ct).ConfigureAwait(false);
        Require(FirstFiles.All(files.ContainsKey), "first_files");
        Envelope(files["registration.json"], "registration", options, pod, null, uid);
        JsonElement termination = Envelope(files["termination.json"], "termination", options, pod, null, uid).GetProperty("data");
        JsonElement manifest = Envelope(files["abrupt-manifest.json"], "abrupt_manifest", options, pod, null, uid).GetProperty("data");
        Bind(termination.GetProperty("manifest"), "abrupt-manifest.json", files["abrupt-manifest.json"]);
        Bind(manifest.GetProperty("journal"), "journal.jsonl", files["journal.jsonl"]);
        var objectNames = new HashSet<string>(FirstFiles, StringComparer.Ordinal);
        foreach (JsonElement descriptor in manifest.GetProperty("report_files").EnumerateArray())
        {
            string name = Text(descriptor, "name");
            Require(ReportFiles.Contains(name, StringComparer.Ordinal) && objectNames.Add(name) && files.ContainsKey(name), "report_files");
            Bind(descriptor, name, files[name]);
        }
        Require(objectNames.SetEquals(files.Keys), "first_object_set");
        Require(Text(termination, "termination") == "intended_self_kill" && termination.GetProperty("exit_observed").GetBoolean() &&
            termination.GetProperty("exit_code_local_observation").GetInt32() == 137 &&
            termination.GetProperty("capture_gaps").GetArrayLength() == 0 && manifest.GetProperty("gaps").GetArrayLength() == 0 &&
            termination.GetProperty("successor_permitted").GetBoolean(), "termination");

        byte[] journal = files["journal.jsonl"];
        Require(journal.Length > 0 && journal[^1] == 10, "journal_newline");
        var frames = new List<JsonElement>();
        int start = 0;
        for (int offset = 0; offset < journal.Length; offset++)
        {
            if (journal[offset] != 10) continue;
            Require(frames.Count < 4096, "journal_records");
            byte[] line = journal[start..offset];
            string kind = Text(Parse(line), "kind");
            Require(JournalKinds.Contains(kind, StringComparer.Ordinal), "journal_kind");
            JsonElement frame = Envelope(line, kind, options, pod, null, uid, journal: true);
            Require(kind != "gap" && frame.GetProperty("sequence").GetInt64() == frames.Count + 1L, "journal_sequence");
            frames.Add(frame);
            start = offset + 1;
        }
        Require(manifest.GetProperty("journal_records").GetInt64() == frames.Count, "journal_records");
        var attempts = new Dictionary<string, AbruptAttempt>(StringComparer.Ordinal);
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);
        var terminals = new Dictionary<string, AbruptTerminal>(StringComparer.Ordinal);
        string[]? control = null;
        long cutSequence = 0;
        string? lastProducing = null;
        foreach (JsonElement frame in frames)
        {
            string kind = Text(frame, "kind");
            JsonElement data = frame.GetProperty("data");
            long frameSequence = frame.GetProperty("sequence").GetInt64();
            if (kind == "prepared")
            {
                string id = Text(data, "attempt_id"), arm = Text(data, "arm"), role = Text(data, "role");
                long ordinal = data.GetProperty("ordinal").GetInt64();
                string key = Text(data, "key"), hash = Text(data, "payload_sha256");
                Require(arm is "same_instance" or "cross_restart" && role is "original" or "resend" && ordinal is >= 0 and <= 4 &&
                    data.GetProperty("sequence").GetInt64() == ordinal && key == "B10/" + options.RunId + "/C1/1/" + arm &&
                    hash.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "prepared");
                Require(attempts.TryAdd(id, new(id, arm, role, ordinal, key, ordinal, hash,
                    NullableText(data, "predecessor_attempt_id"), frameSequence)), "attempt_duplicate");
            }
            else if (kind is "accepted" or "synchronous_rejection" or "terminal")
            {
                string id = Text(data, "attempt_id");
                Require(attempts.TryGetValue(id, out AbruptAttempt? attempt), "prepared_missing");
                if (kind == "terminal") Require(terminals.TryAdd(id, Terminal(data)), "terminal_duplicate");
                else
                {
                    Require(data.GetProperty("prepared_sequence").GetInt64() == attempt!.JournalSequence, "prepared_binding");
                    Require((kind == "accepted" ? accepted : rejected).Add(id), "acceptance_duplicate");
                }
            }
            else if (kind == "cut")
            {
                Require(control is null, "cut_duplicate");
                control = Select("same_instance");
                Require(control.SequenceEqual(Strings(data.GetProperty("unconfirmed_attempt_ids")), StringComparer.Ordinal), "control_set");
                cutSequence = frameSequence;
            }
            if (ProducingKinds.Contains(kind, StringComparer.Ordinal)) lastProducing = kind;
        }
        foreach (string arm in new[] { "same_instance", "cross_restart" })
        {
            AbruptAttempt[] originals = attempts.Values.Where(attempt => attempt.Arm == arm && attempt.Role == "original").OrderBy(attempt => attempt.JournalSequence).ToArray();
            Require(originals.Select(attempt => attempt.Ordinal).SequenceEqual(Enumerable.Range(0, 5).Select(value => (long)value)) &&
                originals.Select(attempt => attempt.PayloadSha256).Distinct(StringComparer.Ordinal).Count() == 1, "originals");
        }
        Require(control is { Length: > 0 } && control.SequenceEqual(Strings(termination.GetProperty("control_unconfirmed_attempt_ids")), StringComparer.Ordinal), "control_set");
        AbruptAttempt[] resends = attempts.Values.Where(attempt => attempt.Role == "resend").OrderBy(attempt => attempt.JournalSequence).ToArray();
        Require(resends.Select(attempt => attempt.Predecessor).SequenceEqual(control!, StringComparer.Ordinal), "control_resends");
        var controlResends = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AbruptAttempt resend in resends)
        {
            AbruptAttempt original = attempts[resend.Predecessor!];
            Require(resend.Arm == "same_instance" && resend.JournalSequence > cutSequence &&
                resend.Key == original.Key && resend.Sequence == original.Sequence && resend.PayloadSha256 == original.PayloadSha256, "control_resend_binding");
            controlResends.Add(original.Id, resend.Id);
        }
        string[] kill = Select("cross_restart");
        Require(kill.Length > 0 && kill.SequenceEqual(Strings(termination.GetProperty("kill_unconfirmed_attempt_ids")), StringComparer.Ordinal) &&
            lastProducing == "kill_intent", "kill_set");
        return new(pod, uid, attempts, control!, kill, controlResends, terminals, journal, files["termination.json"]);

        string[] Select(string arm) => attempts.Values.Where(attempt => attempt.Arm == arm && attempt.Role == "original" &&
            accepted.Contains(attempt.Id) && !terminals.ContainsKey(attempt.Id) && !rejected.Contains(attempt.Id)).OrderBy(attempt => attempt.JournalSequence).Select(attempt => attempt.Id).ToArray();
    }

    internal async Task<AbruptSuccessorEvidence> ReadSuccessorAsync(string prefix, string pod,
        AbruptFirstEvidence first, IReadOnlyList<string> names, CancellationToken ct)
    {
        Dictionary<string, byte[]> files = await ReadFilesAsync(prefix, pod, names, ct).ConfigureAwait(false);
        Require(files.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(SuccessorFiles), "successor_files");
        JsonElement registration = Envelope(files["registration.json"], "registration", options, pod, first.Pod);
        string uid = Text(registration.GetProperty("identity"), "pod_uid");
        Require(uid != first.Uid, "pod_uid_duplicate");
        Envelope(files["manifest.json"], "cohort_inventory", options, pod, first.Pod, uid);
        JsonElement completion = Envelope(files["completion.json"], "successor_receipt", options, pod, first.Pod, uid).GetProperty("data");
        Bind(completion.GetProperty("registration"), "registration.json", files["registration.json"]);
        var boundNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement descriptor in completion.GetProperty("artifacts").EnumerateArray())
        {
            string name = Text(descriptor, "name");
            Require(name is not ("registration.json" or "completion.json") && files.ContainsKey(name) && boundNames.Add(name), "successor_artifacts");
            Bind(descriptor, name, files[name]);
        }
        Require(boundNames.SetEquals(SuccessorFiles.Except(["registration.json", "completion.json"], StringComparer.Ordinal)), "successor_artifacts");
        Bind(completion.GetProperty("first_termination"), "termination.json", first.TerminationBytes);
        Bind(completion.GetProperty("journal"), "journal.jsonl", first.JournalBytes);
        Require(completion.GetProperty("cleanup_completed").GetBoolean() &&
            Strings(completion.GetProperty("kill_unconfirmed_attempt_ids")).SequenceEqual(first.Kill, StringComparer.Ordinal), "successor_completion");
        JsonElement[] invocations = completion.GetProperty("invocations").EnumerateArray().ToArray();
        Require(invocations.Length == first.Kill.Count, "successor_invocations");
        var resends = new Dictionary<string, string>(StringComparer.Ordinal);
        var attemptIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < invocations.Length; index++)
        {
            JsonElement invocation = invocations[index];
            Exact(invocation, "attempt_id", "predecessor_attempt_id", "key", "sequence", "payload_sha256", "invocation_utc", "monotonic_ticks");
            AbruptAttempt original = first.Attempts[first.Kill[index]];
            string id = Text(invocation, "attempt_id");
            Require(Text(invocation, "predecessor_attempt_id") == original.Id && id != original.Id && attemptIds.Add(id) &&
                Text(invocation, "key") == original.Key && invocation.GetProperty("sequence").GetInt64() == original.Sequence &&
                Text(invocation, "payload_sha256") == original.PayloadSha256, "successor_invocation_binding");
            resends.Add(original.Id, id);
        }
        byte[] ledger = files["ledger.jsonl"];
        Require(ledger.Length > 0 && ledger[^1] == 10, "ledger_newline");
        var terminals = new Dictionary<string, AbruptTerminal>(StringComparer.Ordinal);
        int start = 0;
        for (int offset = 0; offset < ledger.Length; offset++)
        {
            if (ledger[offset] != 10) continue;
            JsonElement row = Parse(ledger.AsMemory(start, offset - start));
            Exact(row, "attempt_id", "completion_source", "delivered", "error_code", "persistence_status");
            string id = Text(row, "attempt_id");
            Require(attemptIds.Contains(id) && terminals.TryAdd(id, Terminal(row)), "successor_ledger");
            start = offset + 1;
        }
        Require(terminals.Count == attemptIds.Count, "successor_ledger");
        return new(pod, resends, terminals);
    }

    private async Task<Dictionary<string, byte[]>> ReadFilesAsync(string prefix, string pod,
        IReadOnlyList<string> names, CancellationToken ct)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (string path in names)
        {
            string name = path[(prefix.Length + pod.Length + 1)..];
            long limit = name switch
            {
                "journal.jsonl" => 16777216,
                "ledger.jsonl" => 268435456,
                "manifest.json" => 67108864,
                "statistics.jsonl" or "request-lifecycle.jsonl" => 134217728,
                _ => ReportFiles.Contains(name, StringComparer.Ordinal) ? 1048576 : 65536
            };
            byte[] bytes = await RequiredAsync(path, Math.Min(limit, 1073741824 - total), ct, name == "request-lifecycle.jsonl").ConfigureAwait(false);
            total += bytes.LongLength;
            Require(total <= 1073741824 && files.TryAdd(name, bytes), "pod_byte_limit");
        }
        return files;
    }

    private async Task<byte[]> RequiredAsync(string name, long limit, CancellationToken ct, bool allowEmpty = false)
    {
        byte[]? bytes = await read(name, limit, ct).ConfigureAwait(false);
        Require(bytes is not null && bytes.LongLength <= limit && (allowEmpty || bytes.Length > 0), "file_bytes");
        return bytes!;
    }
    private static AbruptTerminal Terminal(JsonElement data) => new(data.GetProperty("delivered").GetBoolean(),
        data.GetProperty("error_code").GetInt32(), data.GetProperty("persistence_status").ValueKind == JsonValueKind.Null
            ? null : data.GetProperty("persistence_status").GetInt32());
}
