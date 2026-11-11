using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptParentProcessTests
{
    private static readonly B10AbruptProcess.ProcessIdentity Child = new(123, "synthetic-boot", 456);
    private static readonly B10AbruptCgroup.Observation Cgroup = new("synthetic-boot", "/synthetic", "synthetic-inode", 0);

    [Fact]
    public void Given_MatchingSelfKill_When_ParentClassifies_Then_SuccessorIsPermitted()
    {
        B10AbruptParent.Classification result = Classify(Snapshot());
        Assert.Equal("intended_self_kill", result.Termination);
        Assert.Null(result.Outcome);
        Assert.Null(result.Detail);
        Assert.True(result.SuccessorPermitted);
    }

    [Theory]
    [InlineData(134, false)]
    [InlineData(0, false)]
    [InlineData(137, true)]
    public void Given_UnintendedExitOrProducingAfterIntent_When_ParentClassifies_Then_ExitIsUnexpected(int exitCode, bool producingAfterIntent)
    {
        var snapshot = Snapshot();
        if (producingAfterIntent)
            snapshot = snapshot with { Frames = [.. snapshot.Frames, Frame(5, "prepared", Prepared("after-intent"))] };
        B10AbruptParent.Classification result = Classify(snapshot, exitCode);
        Assert.Equal("unexpected_exit", result.Termination);
        Assert.Equal("b10_abrupt_unexpected_exit", result.Outcome);
        Assert.False(result.SuccessorPermitted);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void Given_DurableNoKill_When_ParentClassifies_Then_ExitCodeDoesNotGateNoKill(int exitCode)
    {
        var snapshot = Snapshot() with { Frames = [Frame(1, "child_started", B10AbruptWire.Element(new { pid = Child.Pid, start_time_ticks = Child.StartIdentity, boot_id = Child.BootId })),
            Frame(2, "no_kill", B10AbruptWire.Element(new { reason = "b10_abrupt_control_incomplete", cross_restart_admitted = false }))] };
        B10AbruptParent.Classification result = Classify(snapshot, exitCode);
        Assert.Equal("no_kill", result.Termination);
        Assert.Equal("b10_abrupt_control_incomplete", result.Outcome);
        Assert.False(result.SuccessorPermitted);
    }

    [Fact]
    public void Given_IntendedKillWithNoUnconfirmedAttempt_When_ParentClassifies_Then_KillTooLateBlocksSuccessor()
    {
        B10AbruptParent.Classification result = Classify(Snapshot() with { KillUnconfirmedAttemptIds = [] });
        Assert.Equal("intended_self_kill", result.Termination);
        Assert.Equal("kill_too_late", result.Outcome);
        Assert.Equal("no_unconfirmed_attempt", result.Detail);
        Assert.False(result.SuccessorPermitted);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("binding")]
    [InlineData("identity")]
    [InlineData("oom")]
    public void Given_InvalidCaptureOrIdentity_When_ParentClassifies_Then_SuccessorIsBlocked(string fault)
    {
        var snapshot = Snapshot();
        if (fault == "gap") snapshot = snapshot with { Gaps = ["malformed_frame"] };
        if (fault == "binding") snapshot = snapshot with { Failures = new Dictionary<string, string> { ["same_instance"] = "b10_abrupt_sample_missing" } };
        var child = fault == "identity" ? Child with { StartIdentity = Child.StartIdentity + 1 } : Child;
        var after = fault == "oom" ? Cgroup with { OomKill = 1 } : Cgroup;
        B10AbruptParent.Classification result = B10AbruptParent.Classify(snapshot, true, 137, child, Cgroup, after);
        Assert.False(result.SuccessorPermitted);
    }

    private static B10AbruptParent.Classification Classify(B10AbruptJournal.Snapshot snapshot, int code = 137) =>
        B10AbruptParent.Classify(snapshot, true, code, Child, Cgroup, Cgroup);
    private static B10AbruptJournal.Snapshot Snapshot() => new(
        [Frame(1, "child_started", B10AbruptWire.Element(new { pid = Child.Pid, start_time_ticks = Child.StartIdentity, boot_id = Child.BootId })),
         Frame(2, "prepared", Prepared("synthetic-original")),
         Frame(3, "accepted", B10AbruptWire.Element(new { attempt_id = "synthetic-original", prepared_sequence = 2L })),
         Frame(4, "kill_intent", B10AbruptWire.Element(new { arm = "cross_restart", cut_attempt_id = "synthetic-original", release_utc = "2026-11-10T00:02:00.0000000+00:00" }))],
        [], new Dictionary<string, string>(), ["synthetic-control"], ["synthetic-original"]);
    private static JsonElement Prepared(string attempt) => B10AbruptWire.Element(new
    {
        attempt_id = attempt, arm = "cross_restart", role = "original", ordinal = 4,
        probe_id = "B10/test/C1/1/cross_restart", key = "B10/test/C1/1/cross_restart", sequence = 4L,
        payload_length = 0, payload_sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        predecessor_attempt_id = (string?)null
    });
    private static B10AbruptWire.Frame Frame(long sequence, string kind, JsonElement data) => new(B10AbruptProcessTests.Options().Identity(), sequence,
        "2026-11-10T00:00:00.0000000+00:00", sequence, TimeSpan.TicksPerSecond, kind, data);
}
