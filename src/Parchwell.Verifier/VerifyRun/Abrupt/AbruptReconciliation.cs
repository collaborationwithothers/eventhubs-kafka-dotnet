using System.Globalization;
using System.Text.Json;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

/// <summary>Independently reconciles the retained journal and successor declarations.</summary>
internal static class AbruptReconciliation
{
    internal static AbruptFirstEvidence First(AbruptFirstEvidence first, JsonElement termination)
    {
        var reasons = new HashSet<int>();
        JsonElement[] native = Frames("native_error");
        long errors = native.LongCount(frame => Data(frame).GetProperty("is_error").GetBoolean());
        long fatal = native.LongCount(frame => Data(frame).GetProperty("is_fatal").GetBoolean());
        if (native.Any(frame => Data(frame).GetProperty("is_error").GetBoolean() && !Data(frame).GetProperty("is_fatal").GetBoolean())) reasons.Add(5);
        if (fatal > 0) reasons.Add(6);
        Check(3, () => native.Select(frame => Data(frame).GetProperty("capture_sequence").GetInt64())
            .SequenceEqual(Enumerable.Range(1, native.Length).Select(value => (long)value)) &&
            termination.GetProperty("native_error_count").GetInt64() == native.Length &&
            termination.GetProperty("native_fatal_count").GetInt64() == fatal &&
            Text(termination, "native_tail") == "unknown_due_to_intended_kill");
        Check(4, () =>
        {
            JsonElement[] children = Frames("child_started");
            if (children.Length != 1 || first.Frames[0].GetProperty("kind").GetString() != "child_started") return false;
            JsonElement child = termination.GetProperty("child"), started = Data(children[0]);
            Exact(child, "pid", "start_time_ticks", "boot_id", "parent_observed_start_time_ticks");
            if (child.GetProperty("pid").GetInt32() != started.GetProperty("pid").GetInt32() ||
                child.GetProperty("start_time_ticks").GetInt64() != started.GetProperty("start_time_ticks").GetInt64() ||
                child.GetProperty("parent_observed_start_time_ticks").GetInt64() != child.GetProperty("start_time_ticks").GetInt64() ||
                Text(child, "boot_id") != Text(started, "boot_id")) return false;
            JsonElement before = termination.GetProperty("cgroup_before"), after = termination.GetProperty("cgroup_after");
            Exact(before, "boot_id", "cgroup_path", "directory_identity", "oom_kill");
            Exact(after, "boot_id", "cgroup_path", "directory_identity", "oom_kill");
            return new[] { "boot_id", "cgroup_path", "directory_identity" }.All(name => Text(before, name) == Text(after, name)) &&
                Text(before, "boot_id") == Text(child, "boot_id") && before.GetProperty("oom_kill").GetInt64() == after.GetProperty("oom_kill").GetInt64();
        });
        var warmups = new Dictionary<string, AbruptWarmup>(StringComparer.Ordinal);
        Check(3, () =>
        {
            foreach (string arm in new[] { "same_instance", "cross_restart" })
            {
                JsonElement[] prepared = Frames("warmup_prepared").Where(frame => Text(Data(frame), "arm") == arm).ToArray();
                if (prepared.Length != 1) return false;
                JsonElement warmup = Data(prepared[0]);
                string id = first.Pod + "-" + arm + "-warmup";
                string key = first.Attempts.Values.First(attempt => attempt.Arm == arm).Key;
                if (Text(warmup, "attempt_id") != id || Text(warmup, "key") != key) return false;
                warmups.Add(id, new(id, arm, key, Text(warmup, "payload_sha256")));
                JsonElement[] terminal = Frames("warmup_terminal").Where(frame => Text(Data(frame), "attempt_id") == id).ToArray();
                JsonElement original = first.Frames.Single(frame => Text(frame, "kind") == "prepared" && Text(Data(frame), "arm") == arm &&
                    Text(Data(frame), "role") == "original" && Data(frame).GetProperty("ordinal").GetInt64() == 0);
                if (terminal.Length != 1 || Sequence(prepared[0]) >= Sequence(terminal[0]) || Sequence(terminal[0]) >= Sequence(original)) return false;
                JsonElement result = Data(terminal[0]);
                if (!result.GetProperty("delivered").GetBoolean() || result.GetProperty("error_code").GetInt32() != 0 ||
                    result.GetProperty("persistence_status").GetInt32() is not (0 or 2) ||
                    result.GetProperty("partition").GetInt32() < 0 || result.GetProperty("offset").GetInt64() < 0) return false;
                if (arm == "cross_restart" && !ControlCompleteBefore(Sequence(prepared[0]))) return false;
            }
            return Frames("warmup_prepared").Length == 2 && Frames("warmup_terminal").Length == 2;
        });
        Check(3, () =>
        {
            foreach (string arm in new[] { "same_instance", "cross_restart" })
            {
                JsonElement[] reports = Frames("pre_acceptance_report").Where(frame => Text(Data(frame), "arm") == arm).ToArray();
                if (reports.Length != 1) return false;
                JsonElement report = Data(reports[0]);
                string id = first.Pod + "-" + arm + "-warmup";
                JsonElement[] terminals = Frames("warmup_terminal").Where(frame => Text(Data(frame), "attempt_id") == id).ToArray();
                if (terminals.Length != 1) return false;
                int partition = Data(terminals[0]).GetProperty("partition").GetInt32();
                if (Text(report, "warmup_attempt_id") != id || report.GetProperty("partition").GetInt32() != partition ||
                    Utc(Text(report, "report_capture_utc")) <= Utc(Text(terminals[0], "observed_utc"))) return false;
                string name = Text(report, "name");
                if (name != arm + "-pre-acceptance-report.json" || !first.Files.TryGetValue(name, out byte[]? bytes)) return false;
                Bind(Descriptor(report), name, bytes);
                if (!LeaderUp(Parse(bytes), partition)) return false;
            }
            JsonElement[] drains = Frames("control_drained"), post = Frames("post_drain_report");
            if (drains.Length != 1 || post.Length != 1 || Sequence(post[0]) <= Sequence(drains[0])) return false;
            JsonElement descriptor = Data(post[0]);
            if (Text(descriptor, "name") != "control-post-drain-report.json") return false;
            Bind(Descriptor(descriptor), "control-post-drain-report.json", first.Files["control-post-drain-report.json"]);
            return true;
        });
        if (first.Attempts.Values.Any(attempt => attempt.Arm == "same_instance" && first.Accepted.Contains(attempt.Id) && !first.Terminals.ContainsKey(attempt.Id)))
        { reasons.Add(3); reasons.Add(8); }
        return first with
        {
            Reasons = reasons, Invalid = reasons.Overlaps([3, 4, 8]), Warmups = warmups,
            NativeErrors = new(native.Length, fatal, "unknown_due_to_intended_kill")
        };

        JsonElement[] Frames(string kind) => first.Frames.Where(frame => Text(frame, "kind") == kind).ToArray();
        void Check(int reason, Func<bool> predicate)
        {
            try { if (!predicate()) reasons.Add(reason); }
            catch (Exception exception) when (Malformed(exception)) { reasons.Add(reason); }
        }
        bool ControlCompleteBefore(long sequence)
        {
            JsonElement[] prior = first.Frames.Where(frame => Sequence(frame) < sequence).ToArray();
            JsonElement[] drains = prior.Where(frame => Text(frame, "kind") == "control_drained").ToArray();
            JsonElement[] post = prior.Where(frame => Text(frame, "kind") == "post_drain_report").ToArray();
            JsonElement[] cuts = prior.Where(frame => Text(frame, "kind") == "cut").ToArray();
            if (drains.Length != 1 || post.Length != 1 || cuts.Length != 1 || !Data(drains[0]).GetProperty("flush_returned").GetBoolean() ||
                Data(drains[0]).GetProperty("out_queue_length").GetInt64() != 0 || Sequence(drains[0]) <= Sequence(cuts[0]) || Sequence(post[0]) <= Sequence(drains[0])) return false;
            DateTimeOffset release = Utc(Text(Data(cuts[0]), "release_utc"));
            if (first.Attempts.Values.Any(attempt => attempt.Arm == "same_instance" && attempt.Role == "resend" &&
                (attempt.JournalSequence >= Sequence(drains[0]) || Utc(Text(first.Frames.Single(frame => Sequence(frame) == attempt.JournalSequence), "observed_utc")) < release))) return false;
            return first.Attempts.Values.Where(attempt => attempt.Arm == "same_instance").All(attempt =>
                !first.Accepted.Contains(attempt.Id) || prior.Any(frame => Text(frame, "kind") == "terminal" && Text(Data(frame), "attempt_id") == attempt.Id));
        }
    }

    internal static AbruptSuccessorEvidence Successor(AbruptSuccessorEvidence successor)
    {
        var reasons = new HashSet<int>();
        JsonElement[] invocations = successor.Completion.GetProperty("invocations").EnumerateArray().ToArray();
        try
        {
            JsonElement manifest = successor.Manifest;
            JsonElement[] entries = manifest.GetProperty("inventory").EnumerateArray().ToArray();
            bool valid = Text(manifest, "instance_id") == successor.Pod && Text(manifest, "cohort") == "cross_restart" &&
                manifest.GetProperty("bookkeeping_complete").GetBoolean() && manifest.GetProperty("inventory_count").GetInt64() == invocations.Length && entries.Length == invocations.Length;
            for (int index = 0; valid && index < entries.Length; index++)
            {
                JsonElement entry = entries[index], invocation = invocations[index];
                Exact(entry, "attempt_id", "invocation_ordinal", "predecessor_attempt_id", "key", "sequence", "payload_length", "payload_sha256");
                valid = entry.GetProperty("invocation_ordinal").GetInt64() == index && entry.GetProperty("sequence").GetInt64() == invocation.GetProperty("sequence").GetInt64() &&
                    new[] { "attempt_id", "predecessor_attempt_id", "key", "payload_sha256" }.All(name => Text(entry, name) == Text(invocation, name));
            }
            if (!valid) reasons.Add(9);
        }
        catch (Exception exception) when (Malformed(exception)) { reasons.Add(9); }
        try
        {
            DateTimeOffset release = Utc(Text(successor.Completion, "release_utc"));
            if (Utc(Text(successor.Completion, "ready_utc")) > release || invocations.Any(invocation => Utc(Text(invocation, "invocation_utc")) < release)) reasons.Add(11);
        }
        catch (Exception exception) when (Malformed(exception)) { reasons.Add(9); }
        return successor with { Reasons = reasons, Invalid = reasons.Contains(9) };
    }

    private static JsonElement Descriptor(JsonElement report) => JsonSerializer.SerializeToElement(new
    { name = Text(report, "name"), length = report.GetProperty("length").GetInt64(), sha256 = Text(report, "sha256") });
    private static JsonElement Data(JsonElement frame) => frame.GetProperty("data");
    private static long Sequence(JsonElement frame) => frame.GetProperty("sequence").GetInt64();
    internal static DateTimeOffset Utc(string value)
    {
        Require(value.Length == 33 && value.EndsWith("+00:00", StringComparison.Ordinal), "utc");
        return DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture);
    }
    internal static bool Malformed(Exception exception) => exception is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or OverflowException or ArgumentException;
    private static bool LeaderUp(JsonElement report, int partition)
    {
        long leader = report.GetProperty("topics").GetProperty("document-events").GetProperty("partitions")
            .GetProperty(partition.ToString(CultureInfo.InvariantCulture)).GetProperty("leader").GetInt64();
        if (leader < 0) return false;
        JsonElement[] brokers = report.GetProperty("brokers").EnumerateObject().Select(property => property.Value)
            .Where(broker => broker.GetProperty("nodeid").GetInt64() == leader).ToArray();
        return brokers.Length == 1 && Text(brokers[0], "state") == "UP";
    }
}
