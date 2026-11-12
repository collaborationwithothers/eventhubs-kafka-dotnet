using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

/// <summary>Independent synthetic writer for the diagnostic contract, pinned to the real publisher by T0.</summary>
internal sealed class B10AbruptVerifyEvidence
{
    internal const string Run = "synthetic";
    internal const string First = "synthetic-first";
    internal const string Second = "synthetic-second";
    internal const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string Image = "registry.invalid/loadgen@sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    internal const string Utc = "2026-11-12T00:00:00.0000000+00:00";
    internal static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic-payload");
    private static readonly string[] ControlSet = ["s4"];
    private static readonly string[] KillSet = ["c4"];
    private static readonly string[] SuccessorArtifacts = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
    internal List<JsonObject> Frames { get; } = [];
    internal static string Key(string arm) => "B10/synthetic/C1/1/" + arm;
    internal static string Prefix => "synthetic/C1/r1/b10-abrupt-diagnostic/";

    internal B10AbruptVerifyEvidence()
    {
        Put("first/registration.json", Envelope("registration", new { registered_utc = Utc }));
        Add("child_started", new { pid = 1, start_time_ticks = 1, boot_id = "synthetic-boot" });
        foreach (string arm in new[] { "same_instance", "cross_restart" })
        {
            string letter = arm == "same_instance" ? "s" : "c";
            string warmup = First + "-" + arm + "-warmup";
            Add("warmup_prepared", new { attempt_id = warmup, arm, key = Key(arm), payload_length = Payload.Length, payload_sha256 = Hash(Payload) });
            Add("warmup_terminal", new { attempt_id = warmup, completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2, partition = 0, offset = 0 });
            for (int ordinal = 0; ordinal < 5; ordinal++)
            {
                string id = letter + ordinal;
                AddPrepared(id, arm, ordinal);
                if (ordinal == 0) AddReport(arm, id, warmup);
                long prepared = Frames.Last(frame => frame["kind"]!.GetValue<string>() == "prepared")["sequence"]!.GetValue<long>();
                Add("accepted", new { attempt_id = id, prepared_sequence = prepared });
                if (ordinal < 4) AddTerminal(id);
            }
            if (arm == "same_instance")
            {
                Add("cut", new { arm, cut_attempt_id = "s4", release_utc = Utc, unconfirmed_attempt_ids = ControlSet });
                AddPrepared("t4", arm, 4, "s4");
                Add("accepted", new { attempt_id = "t4", prepared_sequence = Frames[^1]["sequence"]!.GetValue<long>() });
                AddTerminal("s4"); AddTerminal("t4");
                Add("control_drained", new { flush_returned = true, out_queue_length = 0 });
                AddReport(arm, null, null);
            }
            else Add("kill_intent", new { arm, cut_attempt_id = "c4", release_utc = Utc });
        }
        Put("first/abrupt-manifest.json", Envelope("abrupt_manifest", new { journal = new { name = "journal.jsonl", length = 1, sha256 = Hash([0]) }, journal_records = Frames.Count, report_files = Array.Empty<object>(), gaps = Array.Empty<string>() }));
        Put("first/termination.json", Envelope("termination", new
        {
            termination = "intended_self_kill", outcome = (string?)null, detail = (string?)null, signal_observation = (object?)null,
            successor_permitted = true, child = new { pid = 1, start_time_ticks = 1, boot_id = "synthetic-boot", parent_observed_start_time_ticks = 1 },
            exit_observed = true, exit_code_local_observation = 137, runtime_version = "synthetic", wait_bound_milliseconds = 720000,
            cgroup_before = new { boot_id = "synthetic-boot", cgroup_path = "/synthetic", directory_identity = "1:2", oom_kill = 0 },
            cgroup_after = new { boot_id = "synthetic-boot", cgroup_path = "/synthetic", directory_identity = "1:2", oom_kill = 0 },
            control_unconfirmed_attempt_ids = ControlSet, kill_unconfirmed_attempt_ids = KillSet, capture_gaps = Array.Empty<string>(),
            native_error_count = 0, native_fatal_count = 0, native_tail = "unknown_due_to_intended_kill", manifest = new { name = "abrupt-manifest.json", length = 1, sha256 = Hash([0]) }
        }));
        Put("successor/registration.json", Envelope("registration", new { registered_utc = Utc }, true));
        Files["successor/ledger.jsonl"] = Encode(new { attempt_id = "r4", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 }, newline: true);
        Put("successor/manifest.json", Envelope("cohort_inventory", new
        {
            instance_id = Second, cohort = "cross_restart", inventory_count = 1,
            inventory = new[] { new { attempt_id = "r4", invocation_ordinal = 0, predecessor_attempt_id = "c4", key = Key("cross_restart"), sequence = 4, payload_length = Payload.Length, payload_sha256 = Hash(Payload) } },
            bookkeeping_complete = true
        }, true));
        Files["successor/statistics.jsonl"] = Encoding.UTF8.GetBytes("{}\n");
        Files["successor/request-lifecycle.jsonl"] = [];
        Files["successor/capabilities.json"] = Encoding.UTF8.GetBytes("{}");
        Put("successor/completion.json", Envelope("successor_receipt", new
        {
            first_termination = new { name = "termination.json", length = 1, sha256 = Hash([0]) }, journal = new { name = "journal.jsonl", length = 1, sha256 = Hash([0]) },
            kill_unconfirmed_attempt_ids = KillSet, registration = new { name = "registration.json", length = 1, sha256 = Hash([0]) }, artifacts = Array.Empty<object>(),
            ready_utc = Utc, release_utc = Utc, invocations = new[] { new { attempt_id = "r4", predecessor_attempt_id = "c4", key = Key("cross_restart"), sequence = 4, payload_sha256 = Hash(Payload), invocation_utc = Utc, monotonic_ticks = 1 } },
            cleanup_completed = true, cross_host_timing = "not_verified"
        }, true));
        Rehash();
    }

    internal static JsonObject Envelope(string kind, object data, bool successor = false) => new()
    {
        ["schema_version"] = 3, ["dialect"] = "b10_abrupt", ["purpose"] = "diagnostic_trial", ["synthetic"] = true,
        ["scientific_admissible"] = false, ["kind"] = kind,
        ["identity"] = JsonSerializer.SerializeToNode(new
        {
            run_id = Run, scenario_id = "B10", configuration_id = "C1", phase = "C1", repeat = 1, source_commit = Commit, image_reference = Image,
            job_name = "synthetic-c1-r1-loadgen-" + (successor ? "successor" : "restart"), pod_name = successor ? Second : First,
            pod_uid = successor ? "uid-second" : "uid-first", instance_id = successor ? Second : First, predecessor_instance_id = successor ? First : null
        }),
        ["data"] = JsonSerializer.SerializeToNode(data)
    };

    private void AddPrepared(string id, string arm, int ordinal, string? predecessor = null) => Add("prepared", new
    {
        attempt_id = id, arm, role = predecessor is null ? "original" : "resend", ordinal, probe_id = Key(arm), key = Key(arm), sequence = ordinal,
        payload_length = Payload.Length, payload_sha256 = Hash(Payload), predecessor_attempt_id = predecessor
    });
    private void AddTerminal(string id) => Add("terminal", new { attempt_id = id, completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 });
    private void AddReport(string arm, string? id, string? warmup)
    {
        string name = id is null ? "control-post-drain-report.json" : arm + "-pre-acceptance-report.json";
        byte[] bytes = Encoding.UTF8.GetBytes("{}"); Files["first/" + name] = bytes;
        var data = JsonSerializer.SerializeToNode(new
        {
            arm, attempt_id = id, name, length = bytes.Length, sha256 = Hash(bytes),
            report_identity = new { scenario_id = "B10", run_id = Run, role = "LoadGenProducer", client_id = First },
            report_sequence = 1, report_capture_utc = Utc
        })!.AsObject();
        if (id is not null) { data["warmup_attempt_id"] = warmup; data["partition"] = 0; }
        Add(id is null ? "post_drain_report" : "pre_acceptance_report", data);
    }
    internal void Add(string kind, object data)
    {
        JsonObject frame = Envelope(kind, data);
        frame["sequence"] = Frames.Count + 1L; frame["observed_utc"] = Utc;
        frame["monotonic_ticks"] = Frames.Count + 1L; frame["clock_frequency"] = 10000000L;
        Frames.Add(frame);
    }
    internal void WriteJournal() => Files["first/journal.jsonl"] = Encoding.UTF8.GetBytes(string.Concat(Frames.Select(frame => frame.ToJsonString() + "\n")));
    internal void Rehash(bool writeJournal = true)
    {
        if (writeJournal) WriteJournal();
        Change("first/abrupt-manifest.json", root =>
        {
            root["data"]!["journal"] = Descriptor("journal.jsonl", Files["first/journal.jsonl"]);
            root["data"]!["journal_records"] = Frames.Count;
            root["data"]!["report_files"] = new JsonArray(Files.Where(pair => pair.Key.StartsWith("first/", StringComparison.Ordinal) && pair.Key.EndsWith("-report.json", StringComparison.Ordinal))
                .Select(pair => (JsonNode)Descriptor(Path.GetFileName(pair.Key), pair.Value)).ToArray());
        });
        Change("first/termination.json", root => root["data"]!["manifest"] = Descriptor("abrupt-manifest.json", Files["first/abrupt-manifest.json"]));
        RehashSuccessor();
    }
    internal void RehashSuccessor()
    {
        if (!Files.ContainsKey("successor/completion.json")) return;
        Change("successor/completion.json", root =>
        {
            root["data"]!["first_termination"] = Descriptor("termination.json", Files["first/termination.json"]);
            root["data"]!["journal"] = Descriptor("journal.jsonl", Files["first/journal.jsonl"]);
            root["data"]!["registration"] = Descriptor("registration.json", Files["successor/registration.json"]);
            root["data"]!["artifacts"] = new JsonArray(SuccessorArtifacts
                .Where(name => Files.ContainsKey("successor/" + name)).Select(name => (JsonNode)Descriptor(name, Files["successor/" + name])).ToArray());
        });
    }
    internal void Change(string name, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(Files[name])!.AsObject(); mutate(root); Put(name, root);
    }
    internal void Put(string name, JsonObject root) => Files[name] = Encoding.UTF8.GetBytes(root.ToJsonString());
    internal static byte[] Encode(object value, bool newline = false) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + (newline ? "\n" : ""));
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static JsonObject Descriptor(string name, byte[] bytes) => new() { ["name"] = name, ["length"] = bytes.LongLength, ["sha256"] = Hash(bytes) };

    internal static async Task<Dictionary<string, byte[]>> ExportAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "b10-abrupt-verify-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string assembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Parchwell.LoadGen.Tests", "bin", configuration, "net10.0", "Parchwell.LoadGen.Tests.dll"));
            Assert.True(File.Exists(assembly), "loadgen_test_assembly_missing:" + assembly);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(assembly); start.ArgumentList.Add("-class"); start.ArgumentList.Add("Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests");
            start.Environment["PARCHWELL_B10_ABRUPT_WIRE_OUTPUT"] = directory;
            using Process child = Process.Start(start) ?? throw new InvalidOperationException("wire_export_start_failed");
            Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            try { await child.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken); }
            finally { if (!child.HasExited) { child.Kill(true); Assert.True(child.WaitForExit(10000)); } }
            Assert.True(child.ExitCode == 0, (await stdout) + (await stderr));
            using JsonDocument export = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "export.json"), TestContext.Current.CancellationToken));
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (JsonElement descriptor in export.RootElement.GetProperty("files").EnumerateArray())
            {
                string name = descriptor.GetProperty("name").GetString()!;
                byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(directory, name), TestContext.Current.CancellationToken);
                Assert.Equal(descriptor.GetProperty("length").GetInt64(), bytes.LongLength);
                Assert.Equal(descriptor.GetProperty("sha256").GetString(), Hash(bytes)); files.Add(name, bytes);
            }
            Assert.Equal(14, files.Count); return files;
        }
        finally { Directory.Delete(directory, true); }
    }
}
