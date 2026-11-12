using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyPredicateBoundaryTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    [Theory]
    [InlineData("capture", 0)]
    [InlineData("capture", 1)]
    [InlineData("capture", 2)]
    [InlineData("count", 0)]
    [InlineData("count", 1)]
    [InlineData("count", 2)]
    [InlineData("fatal-count", -1)]
    [InlineData("fatal-count", 0)]
    [InlineData("fatal-count", 1)]
    public async Task Given_NativeCaptureAtEitherSideOfExactBinding_When_HostRuns_Then_OnlyExactCountAndSequencePass(string field, int value)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
        evidence.Add("native_error", new { capture_sequence = field == "capture" ? value : 1, error_code = 0, error_name = "synthetic", is_error = false, is_fatal = false, callback_monotonic_ticks = 1 });
        kill["sequence"] = evidence.Frames.Count + 1; evidence.Frames.Add(kill);
        evidence.Change("first/termination.json", root => { root["data"]!["native_error_count"] = field == "count" ? value : 1; root["data"]!["native_fatal_count"] = field == "fatal-count" ? value : 0; });
        evidence.Rehash(); using var run = NewRun(evidence);
        bool valid = value == (field == "fatal-count" ? 0 : 1); await AssertOnly(run, valid, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_identity_unauthenticated");
    }

    [Theory]
    [InlineData("report", -1)]
    [InlineData("report", 0)]
    [InlineData("report", 1)]
    [InlineData("invocation", -1)]
    [InlineData("invocation", 0)]
    [InlineData("invocation", 1)]
    [InlineData("ready", -1)]
    [InlineData("ready", 0)]
    [InlineData("ready", 1)]
    public async Task Given_TimingOnEitherSideOfRequiredOrdering_When_HostRuns_Then_StrictAndInclusiveBoundsRemainDistinct(string field, int ticks)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        string utc = DateTimeOffset.Parse(B10AbruptVerifyEvidence.Utc, CultureInfo.InvariantCulture).AddTicks(ticks).ToString("O", CultureInfo.InvariantCulture);
        if (field == "report") evidence.Frames.First(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report")["data"]!["report_capture_utc"] = utc;
        else evidence.Change("successor/completion.json", root => { if (field == "ready") root["data"]!["ready_utc"] = utc; else root["data"]!["invocations"]![0]!["invocation_utc"] = utc; });
        evidence.Rehash(); using var run = NewRun(evidence);
        bool valid = field == "report" ? ticks > 0 : field == "ready" ? ticks <= 0 : ticks >= 0;
        await AssertOnly(run, valid, field == "report" ? "b10_abrupt_verify_first_evidence_invalid" : null,
            field == "report" ? "b10_restart_identity_unauthenticated" : "b10_abrupt_successor_late");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_InventoryCountAroundInvocationCount_When_HostRuns_Then_OnlyExactCountAuthenticates(int count)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); evidence.Change("successor/manifest.json", root => root["data"]!["inventory_count"] = count); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, count == 1, "b10_abrupt_verify_successor_evidence_invalid", "b10_restart_successor_evidence_unavailable");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_FirstGuardCounterAroundZero_When_HostRuns_Then_OnlyZeroPasses(int count)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        const string name = "same_instance-pre-acceptance-report.json";
        evidence.Change("first/" + name, root => root["brokers"]![B10AbruptVerifyEvidence.Broker]!["txerrs"] = count);
        JsonObject descriptor = evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report" && frame["data"]!["name"]!.GetValue<string>() == name)["data"]!.AsObject();
        descriptor["length"] = evidence.Files["first/" + name].Length; descriptor["sha256"] = B10AbruptVerifyEvidence.Hash(evidence.Files["first/" + name]); evidence.Rehash();
        using var run = NewRun(evidence); await AssertOnly(run, count == 0, null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("broker_lifecycle_inventory_unproven")]
    [InlineData("request_lifecycle_capture_unsupported")]
    [InlineData("unexpected")]
    public async Task Given_SuccessorGuardMarker_When_HostRuns_Then_OnlyEnumeratedMarkersArePermitted(string marker)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/capabilities.json", root => root["guard_observations"] = marker.Length == 0 ? new JsonArray() : new JsonArray(marker)); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, marker != "unexpected", null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    public async Task Given_PermittedSuccessorCounters_When_HostRuns_Then_GuardStillPasses(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        string[] reports = Encoding.UTF8.GetString(evidence.Files["successor/statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < reports.Length; index++)
        {
            JsonObject envelope = JsonNode.Parse(reports[index])!.AsObject(); JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            native["brokers"]![B10AbruptVerifyEvidence.Broker]![field] = index + 1; envelope["raw_json"] = native.ToJsonString(); reports[index] = envelope.ToJsonString();
        }
        evidence.Files["successor/statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', reports) + "\n"); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, true, null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("pid", 0)]
    [InlineData("pid", 1)]
    [InlineData("pid", 2)]
    [InlineData("start_time_ticks", 0)]
    [InlineData("start_time_ticks", 1)]
    [InlineData("start_time_ticks", 2)]
    [InlineData("parent_observed_start_time_ticks", 0)]
    [InlineData("parent_observed_start_time_ticks", 1)]
    [InlineData("parent_observed_start_time_ticks", 2)]
    [InlineData("oom_kill", 0)]
    [InlineData("oom_kill", 1)]
    [InlineData("oom_kill", 2)]
    public async Task Given_ChildOrCgroupValueBelowAtOrAboveBinding_When_HostRuns_Then_OnlyEqualityPasses(string field, int value)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("first/termination.json", root =>
        {
            if (field == "oom_kill") { root["data"]!["cgroup_before"]![field] = 1; root["data"]!["cgroup_after"]![field] = value; }
            else root["data"]!["child"]![field] = value;
        });
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertOnly(run, value == 1, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_termination_unverified");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ReportLeaderBelowAtOrAboveZero_When_MatchingBrokerIsUp_Then_NonnegativeLeaderPasses(int leader)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); const string name = "same_instance-pre-acceptance-report.json";
        evidence.Change("first/" + name, root => { root["topics"]!["document-events"]!["partitions"]!["0"]!["leader"] = leader; root["brokers"]![B10AbruptVerifyEvidence.Broker]!["nodeid"] = leader; });
        JsonNode descriptor = evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report" && frame["data"]!["name"]!.GetValue<string>() == name)["data"]!;
        descriptor["length"] = evidence.Files["first/" + name].Length; descriptor["sha256"] = B10AbruptVerifyEvidence.Hash(evidence.Files["first/" + name]); evidence.Rehash();
        using var run = NewRun(evidence); await AssertOnly(run, leader >= 0, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_identity_unauthenticated");
    }

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence evidence) => new(kafka.Bootstrap, evidence.Files);
    private static async Task AssertOnly(B10AbruptVerifyHarness run, bool valid, string? problem, string reason)
    {
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(valid || problem is null ? [] : new[] { problem }, run.Data["problems"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(valid ? [] : new[] { reason }, run.Data["eligibility_reasons"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(valid ? "pending_sweep" : "ineligible", run.Data["eligibility"]!.GetValue<string>());
    }
}
#pragma warning restore CA1707
