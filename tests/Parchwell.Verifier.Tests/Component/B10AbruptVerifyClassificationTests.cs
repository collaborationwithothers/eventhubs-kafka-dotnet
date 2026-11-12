using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyClassificationTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private static readonly string[] TimeoutFields = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly string[] UnreportedFields = ["unreported_attempts", "unreported_seen", "unreported_unseen"];
    private static readonly string[] ProblemNames = ["b10_abrupt_verify_start_line_invalid", "b10_abrupt_verify_first_evidence_invalid", "b10_abrupt_verify_successor_absent", "b10_abrupt_verify_successor_evidence_invalid", "b10_abrupt_verify_window_incomplete", "b10_abrupt_verify_probe_record_malformed", "b10_abrupt_verify_timeout_evidence_invalid", "b10_abrupt_verify_record_unjoined", "b10_abrupt_verify_end_line_advanced"];
    private static readonly string[] ReasonNames = ["b10_restart_configuration_invalid", "b10_restart_first_evidence_unavailable", "b10_restart_identity_unauthenticated", "b10_restart_termination_unverified", "b10_restart_native_error_captured", "b10_restart_fatal_captured", "b10_restart_timeout_bookkeeping_unavailable", "b10_abrupt_control_incomplete", "b10_restart_successor_evidence_unavailable", "b10_restart_resend_count_invalid", "b10_abrupt_successor_late", "b10_restart_window_incomplete", "b10_restart_guard_failed"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_TimeoutRetries_When_HostRuns_Then_CountsAndRetryEligibilityAreIndependent_T2_T3(bool deliveredPrior)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.SetTimeoutRows(B10AbruptVerifyEvidence.TimeoutRow("t1", "k1", 0), B10AbruptVerifyEvidence.TimeoutRow("t2", "k2", 1, deliveredPrior), B10AbruptVerifyEvidence.TimeoutRow("t3", "k2", 2));
        using var run = NewRun(evidence);
        await run.ProduceAsync(0, 0, instance: B10AbruptVerifyEvidence.Timeout, attempt: "t1", key: "k1", sequence: 0);
        await run.ProduceAsync(1, 0, instance: B10AbruptVerifyEvidence.Timeout, attempt: "t2", key: "k2", sequence: 0);
        await run.ProduceAsync(1, 1, instance: B10AbruptVerifyEvidence.Timeout, attempt: "t3", key: "k2", sequence: 0);
        await AssertResult(run, deliveredPrior ? [7] : [], deliveredPrior ? [7] : []);
        if (deliveredPrior) Assert.Null(run.Data["timeout"]);
        else Assert.Equal(new long[] { 2, 0, 1, 0, 1, 0 }, TimeoutFields.Select(field => Number(run.Data["timeout"]!, field)));
    }

    [Theory]
    [InlineData("no-registration")]
    [InlineData("second-registration")]
    [InlineData("restart-job")]
    public async Task Given_InvalidTimeoutRegistration_When_HostRuns_Then_OnlyTimeoutBookkeepingIsUnavailable_T4(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); using var run = NewRun(evidence);
        string path = "synthetic/C1/r1/producer/synthetic-timeout/registration.json";
        if (defect == "no-registration") run.Ledger.Remove(path);
        else if (defect == "second-registration") run.Ledger["synthetic/C1/r1/producer/other/registration.json"] = run.Ledger[path];
        else { JsonObject registration = JsonNode.Parse(run.Ledger[path])!.AsObject(); registration["job_name"] = "synthetic-c1-r1-loadgen-restart"; run.Ledger[path] = Encoding.UTF8.GetBytes(registration.ToJsonString()); }
        await AssertResult(run, [7], [7]); Assert.Null(run.Data["timeout_instance_id"]); Assert.Null(run.Data["timeout"]); Assert.NotNull(run.Data["arms"]);
    }

    [Theory]
    [InlineData("arm-key")]
    [InlineData("unknown-attempt")]
    [InlineData("foreign")]
    public async Task Given_UnjoinedRecord_When_HostRuns_Then_ReconciliationAddsOnlyIdentityReason_T5_T6(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); using var run = NewRun(evidence);
        await run.ProduceAsync(0, 0, instance: defect == "foreign" ? "foreign" : B10AbruptVerifyEvidence.Timeout, attempt: "x", key: defect == "arm-key" ? B10AbruptVerifyEvidence.Key("cross_restart") : "ordinary", sequence: 0);
        await AssertResult(run, [8], [3]); Assert.NotNull(run.Data["timeout"]);
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray()) Assert.Equal(0, Number(arm!, "pair_count"));
    }

    [Fact]
    public async Task Given_TwoWarmups_When_HostRuns_Then_NoProbeOrTimeoutCountChanges_T7()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(0, 0, "same_instance", attempt: B10AbruptVerifyEvidence.First + "-same_instance-warmup", sequence: -1);
        await run.ProduceAsync(1, 0, attempt: B10AbruptVerifyEvidence.First + "-cross_restart-warmup", sequence: -1);
        await AssertResult(run, [], []);
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray()) { Assert.Equal(0, Number(arm!, "originals_seen")); Assert.Equal(0, Number(arm!, "pair_count")); }
    }

    [Fact]
    public async Task Given_UnreportedOriginalAtOffsetZero_When_HostRuns_Then_UnreportedPartitionIsComplete_T8()
    {
        await kafka.ResetAsync(); using var run = NewRun(); await run.ProduceAsync(0, 0); await AssertResult(run, [], []);
        Assert.Equal(new long[] { 1, 1, 0 }, UnreportedFields.Select(field => Number(Arm(run, "cross_restart"), field)));
        Assert.Equal(new long[] { 0, 0, 0 }, UnreportedFields.Select(field => Number(Arm(run, "same_instance"), field)));
    }

    [Theory]
    [InlineData("stable")]
    [InlineData("advanced")]
    [InlineData("timeout")]
    [InlineData("consumed")]
    public async Task Given_ResendLossCandidate_When_WatermarksAreChecked_Then_LossRequiresStableEndLine_T9_T24_T25_T26(string state)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        // The control resend is consumed so the only possible missing resend belongs to cross_restart.
        await run.ProduceAsync(1, 0, "same_instance", attempt: "t4");
        if (state == "consumed") await run.ProduceAsync(0, 0, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        run.BeforeReRead = () => { if (state == "advanced") run.ProduceAsync(0, 0).GetAwaiter().GetResult(); };
        run.FailReRead = state == "timeout";
        await AssertResult(run, state is "advanced" or "timeout" ? [9] : [], []);
        Assert.Equal(state == "consumed" ? 0 : 1, run.ReReads);
        JsonNode member = Arm(run, "cross_restart")["members"]![0]!;
        if (state is "advanced" or "timeout") { Assert.Null(member["resend_lost"]); Assert.Null(Arm(run, "cross_restart")["lost_resends"]); }
        else Assert.Equal(state != "consumed", member["resend_lost"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Given_IncompleteWindow_When_HostRuns_Then_LossAndTimeoutAreUnknown_T10()
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Configuration["Verifier:MaxConsumedRecords"] = "1";
        await run.ProduceAsync(0, 0); await run.ProduceAsync(0, 1); await AssertResult(run, [5], [12]);
        Assert.Null(run.Data["timeout"]); foreach (JsonNode? arm in run.Data["arms"]!.AsArray()) { Assert.Null(arm!["lost_resends"]); Assert.Null(arm["members"]![0]!["resend_lost"]); }
    }

    [Theory]
    [InlineData(false, false, "neither")]
    [InlineData(true, false, "same_instance_only")]
    [InlineData(false, true, "cross_restart_only")]
    [InlineData(true, true, "both")]
    public async Task Given_IndependentArmPairs_When_HostRuns_Then_ComparisonMatchesBothArms_T11(bool same, bool cross, string expected)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(0, 0, "same_instance", attempt: "t4"); await run.ProduceAsync(1, 0, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        if (same) await run.ProduceAsync(0, 1, "same_instance", attempt: "s4"); if (cross) await run.ProduceAsync(1, 1);
        await AssertResult(run, [], []); Assert.Equal(expected, run.Data["comparison"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_NoSuccessor_When_HostRuns_Then_NoUnavailableEvidenceAddsSpeculativeReasons_T12()
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); foreach (string path in evidence.Files.Keys.Where(path => path.StartsWith("successor/", StringComparison.Ordinal)).ToArray()) evidence.Files.Remove(path);
        using var run = NewRun(evidence); await AssertResult(run, [3], [9]); Assert.Equal("unavailable", run.Data["comparison"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("nonfatal", 0, 5)]
    [InlineData("fatal", 0, 6)]
    [InlineData("count", 2, 3)]
    [InlineData("tail", 2, 3)]
    [InlineData("pid", 2, 4)]
    [InlineData("oom", 2, 4)]
    [InlineData("cgroup", 2, 4)]
    [InlineData("partition", 2, 3)]
    [InlineData("capture-equal", 2, 3)]
    [InlineData("leader", 2, 3)]
    [InlineData("post-before", 2, 3)]
    [InlineData("flush", 2, 3)]
    [InlineData("queue", 2, 3)]
    [InlineData("terminal", 2, 3)]
    public async Task Given_FirstReconciliationDefect_When_HostRuns_Then_OnlyItsEnforcedPredicateFails_T13_T14_T15_T16(string defect, int problem, int reason)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject Frame(string kind) => evidence.Frames.First(frame => frame["kind"]!.GetValue<string>() == kind);
        switch (defect)
        {
            case "nonfatal": case "fatal": case "count":
                JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
                evidence.Add("native_error", new { capture_sequence = 1, error_code = -1, error_name = "synthetic", is_error = defect != "count", is_fatal = defect == "fatal", callback_monotonic_ticks = 1 });
                kill["sequence"] = evidence.Frames.Count + 1; evidence.Frames.Add(kill);
                evidence.Change("first/termination.json", root => { root["data"]!["native_error_count"] = defect == "count" ? 0 : 1; root["data"]!["native_fatal_count"] = defect == "fatal" ? 1 : 0; }); break;
            case "tail": evidence.Change("first/termination.json", root => root["data"]!["native_tail"] = null); break;
            case "pid": evidence.Change("first/termination.json", root => root["data"]!["child"]!["pid"] = 2); break;
            case "oom": evidence.Change("first/termination.json", root => root["data"]!["cgroup_after"]!["oom_kill"] = 1); break;
            case "cgroup": evidence.Change("first/termination.json", root => root["data"]!["cgroup_after"] = null); break;
            case "partition": Frame("pre_acceptance_report")["data"]!["partition"] = 1; break;
            case "capture-equal": Frame("pre_acceptance_report")["data"]!["report_capture_utc"] = B10AbruptVerifyEvidence.Utc; break;
            case "leader": evidence.Change("first/same_instance-pre-acceptance-report.json", root => root["topics"]!["document-events"]!["partitions"]!["0"]!["leader"] = -1); RefreshReport(evidence, Frame("pre_acceptance_report")); break;
            case "post-before": int post = evidence.Frames.IndexOf(Frame("post_drain_report")), drain = evidence.Frames.IndexOf(Frame("control_drained")); (evidence.Frames[post], evidence.Frames[drain]) = (evidence.Frames[drain], evidence.Frames[post]); Resequence(evidence); break;
            case "flush": Frame("control_drained")["data"]!["flush_returned"] = false; break;
            case "queue": Frame("control_drained")["data"]!["out_queue_length"] = 1; break;
            case "terminal": evidence.Frames.Remove(evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "terminal" && frame["data"]!["attempt_id"]!.GetValue<string>() == "t4")); Resequence(evidence); break;
        }
        evidence.Rehash(); using var run = NewRun(evidence); await AssertResult(run, problem == 0 ? [] : [problem], defect == "terminal" ? [3, 8] : [reason]);
        Assert.Equal(problem != 0, run.Data["comparison"]!.GetValue<string>() == "unavailable");
    }

    [Theory]
    [InlineData("key", 4, 9)]
    [InlineData("count", 4, 9)]
    [InlineData("late", 0, 11)]
    [InlineData("twice", 4, 10)]
    public async Task Given_SuccessorReconciliationDefect_When_HostRuns_Then_ReasonKeepsItsDistinctMeaning_T17(string defect, int problem, int reason)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect == "key") evidence.Change("successor/manifest.json", root => root["data"]!["inventory"]![0]!["key"] = "wrong");
        if (defect == "count") evidence.Change("successor/manifest.json", root => root["data"]!["inventory_count"] = 2);
        if (defect == "late") evidence.Change("successor/completion.json", root => root["data"]!["ready_utc"] = "2026-11-12T00:00:01.0000000+00:00");
        if (defect == "twice") evidence.Change("successor/completion.json", root => root["data"]!["invocations"]!.AsArray().Add(root["data"]!["invocations"]![0]!.DeepClone()));
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertResult(run, problem == 0 ? [] : [problem], [reason]);
    }

    [Theory]
    [InlineData("txerrs")]
    [InlineData("foreign-broker")]
    [InlineData("rxerrs")]
    [InlineData("timeout")]
    [InlineData("capture")]
    public async Task Given_GuardFailure_When_HostRuns_Then_CountsRemainAvailable_T18(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect is "txerrs" or "foreign-broker")
        {
            evidence.Change("first/same_instance-pre-acceptance-report.json", root => { JsonObject brokers = root["brokers"]!.AsObject(); if (defect == "txerrs") brokers[B10AbruptVerifyEvidence.Broker]!["txerrs"] = 1; else { JsonNode broker = brokers[B10AbruptVerifyEvidence.Broker]!.DeepClone(); brokers.Clear(); brokers["foreign"] = broker; } });
            RefreshReport(evidence, evidence.Frames.First(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report")); evidence.Rehash();
        }
        else if (defect == "rxerrs")
        {
            string[] lines = Encoding.UTF8.GetString(evidence.Files["successor/statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < lines.Length; index++) { JsonObject report = JsonNode.Parse(lines[index])!.AsObject(); JsonObject native = JsonNode.Parse(report["raw_json"]!.GetValue<string>())!.AsObject(); native["brokers"]![B10AbruptVerifyEvidence.Broker]!["rxerrs"] = 1; report["raw_json"] = native.ToJsonString(); lines[index] = report.ToJsonString(); }
            evidence.Files["successor/statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
        }
        else evidence.Change("successor/capabilities.json", root => { if (defect == "timeout") root["resolved_configuration"]!["settings"]!["message.timeout.ms"]!["value"] = "100"; else root["guard_observations_complete"] = false; });
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertResult(run, [], [13]); Assert.NotNull(run.Data["arms"]); Assert.NotNull(run.Data["timeout"]); Assert.NotEqual("unavailable", run.Data["comparison"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("hash", 3)]
    [InlineData("registration", 2)]
    public async Task Given_UnavailableFirstEvidence_When_HostRuns_Then_TimeoutStillClassifiesAndNoAbsentCheckAddsReasons_T23(string defect, int reason)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect == "hash") evidence.Change("first/termination.json", root => root["data"]!["manifest"]!["sha256"] = new string('f', 64)); else evidence.Files.Remove("first/registration.json");
        using var run = NewRun(evidence); await AssertResult(run, [2], [reason]); Assert.Null(run.Data["arms"]); Assert.NotNull(run.Data["timeout"]); Assert.NotNull(run.Data["end_line"]); Assert.True(run.Data["window_complete"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Given_TrialStartLineWithLegacyNames_When_HostRuns_Then_ConfigurationAloneMakesRepeatIneligible_T22()
    {
        await kafka.ResetAsync(); using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files, "b10d-1-1");
        string path = "b10d-1-1/C1/r1/start-line.json";
        JsonObject note = JsonNode.Parse(run.Verifier[path])!.AsObject();
        note["expected_job_names"] = new JsonArray("b10d-1-1-c1-r1-loadgen", "b10d-1-1-c1-r1-loadgen2");
        run.Verifier[path] = Encoding.UTF8.GetBytes(note.ToJsonString()); run.RehashStartReceipt();
        await AssertResult(run, [1], [1]); Assert.Null(run.Data["timeout"]);
    }

    [Theory]
    [InlineData("ScenarioRoot")]
    [InlineData("ArtifactPrefix")]
    [InlineData("MaxRowBytes")]
    [InlineData("MaxRowsPerArtifact")]
    [InlineData("MaxInventoryEntries")]
    [InlineData("MaxInputEntries")]
    [InlineData("MaxNativeReportBytes")]
    [InlineData("MaxBrokerEntries")]
    public async Task Given_MissingNewRequiredModeOption_When_HostStarts_Then_RejectsBeforeStorage(string field)
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Configuration.Remove("Verifier:" + field);
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    [Theory]
    [InlineData("synthetic/C1/r1/producer")]
    [InlineData("synthetic/C1/r1/producer/other/")]
    [InlineData("synthetic/C0/r1/producer/")]
    public async Task Given_ArtifactPrefixOtherThanExactRepeatProducerRoot_When_HostStarts_Then_RejectsBeforeStorage(string prefix)
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Configuration["Verifier:ArtifactPrefix"] = prefix;
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence? evidence = null) => new(kafka.Bootstrap, (evidence ?? new()).Files);
    private static async Task AssertResult(B10AbruptVerifyHarness run, int[] problems, int[] reasons)
    {
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(problems.Select(number => ProblemNames[number - 1]), run.Data["problems"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(reasons.Select(number => ReasonNames[number - 1]), run.Data["eligibility_reasons"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(reasons.Length == 0 ? "pending_sweep" : "ineligible", run.Data["eligibility"]!.GetValue<string>());
        Assert.Equal("nonfinal", run.Data["finality"]!.GetValue<string>());
    }
    private static JsonObject Arm(B10AbruptVerifyHarness run, string arm) => run.Data["arms"]!.AsArray().Single(node => node!["arm"]!.GetValue<string>() == arm)!.AsObject();
    private static long Number(JsonNode node, string field) => node[field]!.GetValue<long>();
    private static void Resequence(B10AbruptVerifyEvidence evidence) { for (int index = 0; index < evidence.Frames.Count; index++) evidence.Frames[index]["sequence"] = index + 1; }
    private static void RefreshReport(B10AbruptVerifyEvidence evidence, JsonObject frame)
    {
        byte[] bytes = evidence.Files["first/" + frame["data"]!["name"]!.GetValue<string>()]; frame["data"]!["length"] = bytes.Length; frame["data"]!["sha256"] = B10AbruptVerifyEvidence.Hash(bytes);
    }
}
#pragma warning restore CA1707
