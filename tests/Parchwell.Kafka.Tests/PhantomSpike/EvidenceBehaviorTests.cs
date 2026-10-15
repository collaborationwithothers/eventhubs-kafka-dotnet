using System.Text;
using Parchwell.Kafka.Tests.PhantomSpike.Evidence;
using Xunit;

namespace Parchwell.Kafka.Tests.PhantomSpike;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then test names.

// These synthetic snapshots test evidence validation; they are never measured spike findings.
public sealed class EvidenceBehaviorTests
{
    // A complete matrix without a witness is a scoped no, and no recipe becomes a test candidate.
    [Fact]
    public void Given_CompleteSyntheticMatrixWithoutWitness_When_Deciding_Then_ReturnsScopedNo()
    {
        var decision = SpikeEvidence.Decide(Experiment(Matrix()));

        Assert.Equal("no", decision.ExistenceFinding);
        Assert.True(decision.MatrixComplete);
        Assert.True(decision.IntegritySucceeded);
        Assert.True(decision.DiagnosticOnly);
        Assert.True(decision.AsynchronousDeliveryFailuresInvalidateAuthoritativeRun);
        Assert.All(decision.Recipes, recipe => Assert.False(recipe.PositiveRegressionCandidate));
    }

    // One witnessed pair establishes existence even when the matrix is incomplete (README, Findings).
    [Fact]
    public void Given_OneSyntheticWitnessAndIncompleteMatrix_When_Deciding_Then_SeparatesExistenceFromIntegrity()
    {
        var decision = SpikeEvidence.Decide(Experiment([Trial(0, SpikeFault.Timeout, 1, witnessed: true)]));

        Assert.Equal("yes", decision.ExistenceFinding);
        Assert.False(decision.MatrixComplete);
        Assert.False(decision.IntegritySucceeded);
        Assert.True(decision.DiagnosticOnly);
        Assert.All(decision.Recipes, recipe => Assert.False(recipe.PositiveRegressionCandidate));
    }

    // A recipe is a future positive-test candidate only when all five planned trials witness the pair.
    [Fact]
    public void Given_FiveIndependentSyntheticWitnesses_When_Deciding_Then_OnlyThatRecipeMeetsRepeatGate()
    {
        var trials = Matrix().Select(trial => trial.Configuration.Retries == 0 && trial.Configuration.Fault == SpikeFault.Timeout
            ? Trial(0, SpikeFault.Timeout, trial.Configuration.Repeat, witnessed: true) : trial).ToArray();
        var decision = SpikeEvidence.Decide(Experiment(trials));
        var recipe = Assert.Single(decision.Recipes, item => item.PositiveRegressionCandidate);

        Assert.True(decision.IntegritySucceeded);
        Assert.Equal(SpikeFault.Timeout, recipe.Fault);
        Assert.Equal(5, recipe.WitnessedPairs);
        Assert.Equal(1d, recipe.ObservedRate);
    }

    // A witness needs exactly one terminal callback; two cannot both describe the attempt's outcome.
    [Fact]
    public void Given_DuplicateSyntheticCallbacks_When_Deciding_Then_RejectsApparentWitness()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        var decision = SpikeEvidence.Decide(Experiment([trial with { Callbacks = [trial.Callbacks[0], trial.Callbacks[0]] }]));

        Assert.Equal("inconclusive", decision.ExistenceFinding);
        Assert.Contains(decision.IntegrityFailures, failure => failure.Contains("duplicate terminal callback", StringComparison.Ordinal));
    }

    // Callback and consumed record must match the exact attempt's IDs, key, payload and headers.
    [Fact]
    public void Given_ConflictingSyntheticIdentityBytes_When_Deciding_Then_RejectsApparentWitness()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        var callback = trial.Callbacks[0];
        var record = trial.Scan!.Records[0];
        TrialEvidence[] conflicts =
        [
            trial with { Callbacks = [callback with { Payload = [255] }] },
            trial with { Callbacks = [callback with { Headers = [.. callback.Headers!, callback.Headers![0]] }] },
            trial with { Scan = trial.Scan with { Records = [record with { AttemptId = "another-attempt" }] } },
            trial with { Scan = trial.Scan with { Records = [record with { Key = [255] }] } },
            trial with { Scan = trial.Scan with { Records = [record with { Headers = [new("instance-id", [255]), record.Headers![1]] }] } }
        ];

        foreach (var conflict in conflicts)
            Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([conflict])).ExistenceFinding);
    }

    // Anything seen after the trial was sealed may come from cleanup, so it is not evidence for the attempt.
    [Fact]
    public void Given_SyntheticObservationsAfterSafetySeal_When_Deciding_Then_RejectsApparentWitness()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        TrialEvidence[] lateEvidence =
        [
            trial with { Callbacks = [trial.Callbacks[0] with { Timestamp = trial.Sealed.AddTicks(1) }] },
            trial with { Scan = trial.Scan! with { Records = [trial.Scan.Records[0] with { Timestamp = trial.Sealed.AddTicks(1) }] } }
        ];

        foreach (var late in lateEvidence)
            Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([late])).ExistenceFinding);
    }

    // A failed callback is attributable to the fault only with a verified proxy route, a fault active
    // before submission, a healthy warmup, an independent observer and a cleanly accepted Produce.
    [Fact]
    public void Given_UnverifiedSyntheticCausation_When_Deciding_Then_RejectsApparentWitness()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        TrialEvidence[] unverified =
        [
            trial with { RouteVerified = false },
            trial with { AdvertisedBrokers = ["direct:9092"] },
            trial with { FaultVerified = false },
            trial with { FaultActivated = trial.Submitted!.Value.AddTicks(1) },
            trial with { HealthyControlVerified = false },
            trial with { IndependentObserver = false },
            trial with { Accepted = false, SynchronousError = "queue-full" }
        ];

        foreach (var item in unverified)
            Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([item])).ExistenceFinding);
    }

    // Persistence status alone or a client-wide error cannot establish the pair (README, Findings).
    [Fact]
    public void Given_PersistenceStatusOrGlobalErrorWithoutFailedSyntheticCallback_When_Deciding_Then_DoesNotProveExistence()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        var successfulCallback = trial.Callbacks[0] with { IsError = false, ErrorCode = "NoError", PersistenceStatus = "PossiblyPersisted" };
        trial = trial with { Callbacks = [successfulCallback], Producer = trial.Producer! with { Errors = ["transport-error"] } };

        Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([trial])).ExistenceFinding);
    }

    // A callback handler that failed or saw different bytes makes that callback unusable as evidence.
    [Fact]
    public void Given_StampedSyntheticCallbackCaptureError_When_Deciding_Then_RejectsApparentWitness()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        foreach (var marker in new[] { "callback-key-or-payload-conflict", "callback-capture-failed" })
        {
            var stamped = $"2026-10-14T00:00:03.0000000+00:00 monotonic=12345 {marker} synthetic callback fault";
            var conflict = trial with { Producer = trial.Producer! with { Errors = [stamped] } };
            var decision = SpikeEvidence.Decide(Experiment([conflict]));

            Assert.Equal("inconclusive", decision.ExistenceFinding);
            Assert.Contains(decision.IntegrityFailures, failure => failure.Contains("Callback capture failed", StringComparison.Ordinal));
        }
    }

    // A cleanup failure fails integrity without erasing a witness already seen; a passing status
    // cannot replace cleanup evidence (README, Findings).
    [Fact]
    public void Given_CleanupFailureAfterSyntheticWitness_When_Deciding_Then_RetainsExistenceButFailsIntegrity()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true) with { CleanupSucceeded = false };
        var decision = SpikeEvidence.Decide(Experiment([trial]) with { CleanupSucceeded = false });

        Assert.Equal("yes", decision.ExistenceFinding);
        Assert.False(decision.IntegritySucceeded);
        Assert.Contains(decision.IntegrityFailures, failure => failure.Contains("cleanup failed", StringComparison.Ordinal));
    }

    // A missing record means something only after a complete scan, so an incomplete one cannot give no.
    [Fact]
    public void Given_IncompleteSyntheticBoundedScan_When_Deciding_Then_AbsenceCannotProduceNo()
    {
        var trials = Matrix();
        trials[0] = trials[0] with { Scan = trials[0].Scan! with { Complete = false, Position = 0 } };

        Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment(trials)).ExistenceFinding);
    }

    [Fact]
    public void Given_WitnessInIncompleteSyntheticScan_When_Deciding_Then_RetainsExistenceButFailsIntegrity()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        trial = trial with { Scan = trial.Scan! with { Complete = false, Failure = "synthetic safety stop" } };
        var decision = SpikeEvidence.Decide(Experiment([trial]));

        Assert.Equal("yes", decision.ExistenceFinding);
        Assert.False(decision.IntegritySucceeded);
    }

    // The healthy-control flag must be backed by the raw warmup callback and scan, not trusted alone.
    [Fact]
    public void Given_MissingRawSyntheticHealthyControl_When_Deciding_Then_FlagAloneDoesNotProveCausation()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true) with { WarmupCallbacks = null };

        Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([trial])).ExistenceFinding);
    }

    // The trial topic is fresh, so any other identity in its window is a conflict, not background noise.
    [Fact]
    public void Given_AnotherIdentityInSyntheticScan_When_Deciding_Then_RejectsConflictingWitnessEvidence()
    {
        var trial = Trial(0, SpikeFault.Timeout, 1, witnessed: true);
        var matching = trial.Scan!.Records[0];
        trial = trial with { Scan = trial.Scan with { Records = [matching, matching with { InstanceId = "unexpected-instance" }] } };

        Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment([trial])).ExistenceFinding);
    }

    // Calibration trials and trials with other timings are outside the frozen matrix and never the finding.
    [Fact]
    public void Given_CalibrationWitnessOutsideFrozenSyntheticMatrix_When_Deciding_Then_DoesNotPromoteItToFinding()
    {
        var evidence = Experiment(Matrix()) with { CalibrationTrials = [Trial(0, SpikeFault.Timeout, 1, witnessed: true)] };
        Assert.Equal("no", SpikeEvidence.Decide(evidence).ExistenceFinding);

        var trials = Matrix();
        trials[0] = trials[0] with { Timings = new(6000, 2000, 10000) };
        Assert.Equal("inconclusive", SpikeEvidence.Decide(Experiment(trials)).ExistenceFinding);
    }

    // Each repeat number must appear once; a duplicate cannot stand in for a missing repeat.
    [Fact]
    public void Given_DuplicateSyntheticRepeatReplacingMissingRepeat_When_Deciding_Then_RejectsCompleteMatrixClaim()
    {
        var trials = Matrix();
        trials[1] = trials[1] with { Configuration = trials[0].Configuration };

        var decision = SpikeEvidence.Decide(Experiment(trials));
        Assert.Equal("inconclusive", decision.ExistenceFinding);
        Assert.False(decision.MatrixComplete);
    }

    // Placeholders for trials that never ran are not observations, so they give no rate (README,
    // Measured finding, earlier inconclusive runs).
    [Fact]
    public void Given_UnexecutedSyntheticPlaceholders_When_Deciding_Then_ReportsNoObservedRate()
    {
        var trials = Matrix().Select(trial => trial with { Executed = false }).ToArray();
        var decision = SpikeEvidence.Decide(Experiment(trials));

        Assert.Equal("inconclusive", decision.ExistenceFinding);
        Assert.False(decision.MatrixComplete);
        Assert.All(decision.Recipes, recipe =>
        {
            Assert.Equal(0, recipe.ObservedTrials);
            Assert.Equal(0, recipe.CompletedTrials);
            Assert.Null(recipe.ObservedRate);
        });
    }

    [Fact]
    public void Given_OneExecutedSyntheticTrialAndPlaceholders_When_Deciding_Then_OnlyExecutedTrialCountsAsObserved()
    {
        var trials = Matrix().Select(trial => trial with { Executed = false }).ToArray();
        trials[0] = trials[0] with { Executed = true };
        var decision = SpikeEvidence.Decide(Experiment(trials));
        var observed = Assert.Single(decision.Recipes, recipe => recipe.ObservedTrials != 0);

        Assert.Equal("inconclusive", decision.ExistenceFinding);
        Assert.Equal(1, observed.ObservedTrials);
        Assert.Equal(0d, observed.ObservedRate);
        Assert.All(decision.Recipes.Where(recipe => recipe != observed), recipe => Assert.Null(recipe.ObservedRate));
    }

    private static ExperimentEvidence Experiment(IReadOnlyList<TrialEvidence> trials) =>
        new("synthetic-source-commit", false, Origin, Origin.AddMinutes(5), new(), trials, [], [], true);

    private static readonly DateTimeOffset Origin = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    private static TrialEvidence[] Matrix() =>
        (from retries in new[] { 0, int.MaxValue }
         from fault in new[] { SpikeFault.None, SpikeFault.Latency, SpikeFault.Timeout, SpikeFault.ResetPeer }
         from repeat in Enumerable.Range(1, 5)
         select Trial(retries, fault, repeat)).ToArray();

    private static TrialEvidence Trial(int retries, SpikeFault fault, int repeat, bool witnessed = false)
    {
        var id = Guid.NewGuid().ToString("N");
        var target = new AttemptMessage($"synthetic-topic-{id}", $"synthetic-instance-{id}", $"synthetic-attempt-{id}", [1], [2]);
        HeaderObservation[] headers = [new("instance-id", Encoding.UTF8.GetBytes(target.InstanceId)), new("attempt-id", Encoding.UTF8.GetBytes(target.AttemptId))];
        var error = fault != SpikeFault.None;
        var callback = new DeliveryObservation(Origin.AddSeconds(3), target.Topic, 0, error ? -1 : 0,
            error, error ? "Local_MsgTimedOut" : "NoError", error ? "synthetic timeout" : "", "PossiblyPersisted",
            target.InstanceId, target.AttemptId, target.Key, target.Payload, headers);
        ConsumedObservation[] records = witnessed || fault == SpikeFault.None
            ? [new(Origin.AddSeconds(4), target.Topic, 0, 0, target.InstanceId, target.AttemptId, target.Key, target.Payload, headers)] : [];
        var end = records.Length;
        var warmupTarget = target with { AttemptId = $"synthetic-warmup-{id}" };
        HeaderObservation[] warmupHeaders = [headers[0], new("attempt-id", Encoding.UTF8.GetBytes(warmupTarget.AttemptId))];
        var warmupCallback = callback with { Timestamp = Origin.AddMilliseconds(500), IsError = false,
            ErrorCode = "NoError", AttemptId = warmupTarget.AttemptId, Headers = warmupHeaders };
        var warmupRecord = new ConsumedObservation(Origin.AddMilliseconds(750), target.Topic, 0, 0,
            target.InstanceId, warmupTarget.AttemptId, target.Key, target.Payload, warmupHeaders);
        return new(id, new(retries, fault, repeat), new(), target, true, null, [callback],
            new(0, end, end, true, records, null), ["proxy:8666"], "synthetic verified toxic",
            new("2.16.0", "2.16.0", ["synthetic debug log"], ["synthetic statistics"], []),
            new("synthetic proxy state", "synthetic proxy metrics", "synthetic Kafka log", "synthetic proxy log"),
            [], true, Origin, Origin.AddSeconds(5), "proxy:8666", true, true, true, true,
            Origin.AddSeconds(1), Origin.AddSeconds(2), warmupTarget, [warmupCallback], new(0, 1, 1, true, [warmupRecord], null));
    }
}
