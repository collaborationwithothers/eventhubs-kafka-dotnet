using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Restart;
using Xunit;
using static Parchwell.LoadGen.Tests.Restart.RestartFixture;

namespace Parchwell.LoadGen.Tests.Restart;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RestartPlanTests
{
    [Fact]
    public void Given_PredecessorWithMixedOutcomes_When_Selecting_Then_ResendsLatestPermittedTimeoutsInSlotOrder()
    {
        var (manifest, rows) = Predecessor();

        RestartManifest plan = RestartPlan.Select(manifest, rows);

        Assert.Equal("pod-a", plan.PredecessorInstanceId);
        Assert.Equal(Plan().Planned, plan.Planned);
    }

    [Fact]
    public void Given_IneligiblePredecessor_When_Selecting_Then_RejectsWithReason()
    {
        var (manifest, rows) = Predecessor();

        Assert.Equal("restart_predecessor_is_restart", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Select(manifest with { Restart = Plan() }, rows)).Message);
        Assert.Equal("restart_manifest_requires_b10", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Select(manifest with { Identity = First with { ScenarioId = "A01" } }, rows)).Message);
        Assert.Equal("restart_predecessor_incomplete", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Select(manifest with { WorkerStatus = "incomplete", BookkeepingComplete = false }, rows)).Message);
        Assert.Equal("ledger_inventory_missing_rows", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Select(manifest, rows.RemoveAt(7))).Message);
    }

    [Fact]
    public void Given_RestartIdentity_When_Creating_Then_RequiresB10SameRepeatAndNewInstance()
    {
        var (manifest, rows) = Predecessor();

        RestartInstance instance = RestartInstance.Create(Second, manifest, rows);

        Assert.Equal(Second, instance.Identity);
        Assert.Equal(Plan().Planned, instance.Plan.Planned);
        Assert.Equal("restart_manifest_requires_b10", Assert.Throws<InvalidDataException>(() =>
            RestartInstance.Create(Second with { ScenarioId = "A01" }, manifest, rows)).Message);
        Assert.Equal("restart_identity_mismatch", Assert.Throws<InvalidDataException>(() =>
            RestartInstance.Create(Second with { RunId = "run-2" }, manifest, rows)).Message);
        Assert.Equal("restart_predecessor_invalid", Assert.Throws<InvalidDataException>(() =>
            RestartInstance.Create(First, manifest, rows)).Message);
    }

    [Fact]
    public void Given_RestartManifest_When_ValidatingAgainstPredecessor_Then_RequiresRecomputedPlanAndSameRepeat()
    {
        var (manifest, rows) = Predecessor();
        RestartManifest changed = Plan() with { Planned = [Plan().Planned[0], Plan().Planned[1] with { PredecessorAttemptId = "pod-a-7" }] };

        RestartPlan.ValidateAgainstPredecessor(Restarted(), manifest, rows);
        Assert.Equal("restart_plan_mismatch", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.ValidateAgainstPredecessor(Restarted() with { Restart = changed }, manifest, rows)).Message);
        Assert.Equal("restart_identity_mismatch", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.ValidateAgainstPredecessor(Restarted() with { Identity = Second with { RunId = "run-2" } }, manifest, rows)).Message);
    }

    [Fact]
    public void Given_MatchingProfile_When_StreamingResendEvents_Then_YieldsOriginalEventsInPlanOrder()
    {
        var events = RestartPlan.Events(Profile(1000), Plan(), 1000, 4, TestContext.Current.CancellationToken).ToArray();

        Assert.Equal([2L, 3L], events.Select(planned => planned.Ordinal));
        Assert.Equal([2000L, 3000L], events.Select(planned => planned.DueMicroseconds));
        Assert.Equal(["tenant-d0", "tenant-d0"], events.Select(planned => planned.Key));
        Assert.Empty(RestartPlan.Events(Profile(1000), new RestartManifest("pod-a", []), 1000, 4,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Given_ProfileThatDoesNotMatchPlan_When_StreamingResendEvents_Then_Rejects()
    {
        RestartManifest wrongTime = Plan() with { Planned = [Plan().Planned[0] with { PlannedMicroseconds = 2001 }] };
        RestartManifest beyondWorkload = new("pod-a",
            [new RestartPlannedEntry(0, "pod-a-9", "tenant", "d999", "tenant-d999", 0, "uploaded", 4_995_000)]);

        Assert.Equal("restart_workload_mismatch", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Events(Profile(1000), wrongTime, 1000, 4, TestContext.Current.CancellationToken).ToArray()).Message);
        Assert.Equal("restart_workload_mismatch", Assert.Throws<InvalidDataException>(() =>
            RestartPlan.Events(Profile(1000), beyondWorkload, 1000, 4, TestContext.Current.CancellationToken).ToArray()).Message);
    }
}
