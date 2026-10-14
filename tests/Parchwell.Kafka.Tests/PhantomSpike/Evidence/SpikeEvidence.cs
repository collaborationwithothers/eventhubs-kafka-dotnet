using System.Text;

namespace Parchwell.Kafka.Tests.PhantomSpike.Evidence;

internal sealed record RecipeFinding(int Retries, SpikeFault Fault, int ObservedTrials, int CompletedTrials,
    int WitnessedPairs, double? ObservedRate, bool PositiveRegressionCandidate);

internal sealed record SpikeDecision(string ExistenceFinding, bool MatrixComplete,
    IReadOnlyList<string> IntegrityFailures, IReadOnlyList<RecipeFinding> Recipes,
    bool DiagnosticOnly = true, bool SyntheticWorkload = true,
    bool AsynchronousDeliveryFailuresInvalidateAuthoritativeRun = true)
{
    public bool IntegritySucceeded => MatrixComplete && IntegrityFailures.Count == 0;
}

internal static class SpikeEvidence
{
    private const int PlannedRepeats = 5;
    private static readonly int[] RetrySettings = [0, int.MaxValue];
    private static readonly SpikeFault[] FaultSettings = [SpikeFault.None, SpikeFault.Latency, SpikeFault.Timeout, SpikeFault.ResetPeer];

    public static SpikeDecision Decide(ExperimentEvidence experiment)
    {
        var failures = new List<string>(experiment.Failures);
        if (string.IsNullOrWhiteSpace(experiment.SourceCommit)) failures.Add("Missing source commit.");
        if (!experiment.CleanupSucceeded) failures.Add("Experiment cleanup failed.");
        if (experiment.Finished < experiment.Started) failures.Add("Experiment timestamps conflict.");
        if (experiment.Trials.Select(trial => trial.TrialId).Distinct(StringComparer.Ordinal).Count() != experiment.Trials.Count)
            failures.Add("Duplicate trial IDs.");
        if (experiment.Trials.Select(trial => trial.Target.AttemptId).Distinct(StringComparer.Ordinal).Count() != experiment.Trials.Count)
            failures.Add("Duplicate target attempt IDs.");
        if (experiment.Trials.Select(trial => trial.Target.Topic).Distinct(StringComparer.Ordinal).Count() != experiment.Trials.Count)
            failures.Add("Duplicate trial topics.");

        var outcomes = experiment.Trials.Select(trial => new
        {
            Trial = trial,
            Failures = Validate(trial, experiment.Timings),
            Witnessed = IsWitness(trial)
        }).ToArray();
        foreach (var outcome in outcomes)
            failures.AddRange(outcome.Failures.Select(reason => $"{outcome.Trial.TrialId}: {reason}"));

        var recipes = new List<RecipeFinding>();
        var matrixComplete = true;
        foreach (var retries in RetrySettings)
        foreach (var fault in FaultSettings)
        {
            var planned = outcomes.Where(item => item.Trial.Configuration.Retries == retries && item.Trial.Configuration.Fault == fault).ToArray();
            var observed = planned.Where(item => item.Trial.Executed).ToArray();
            var repeatsComplete = planned.Length == PlannedRepeats && observed.Length == PlannedRepeats &&
                Enumerable.Range(1, PlannedRepeats).All(repeat => planned.Count(item => item.Trial.Configuration.Repeat == repeat) == 1);
            if (!repeatsComplete)
            {
                failures.Add($"Incomplete or duplicate repeats for retries={retries}, fault={fault}.");
                matrixComplete = false;
            }
            var completed = observed.Count(item => item.Failures.Count == 0);
            var witnessed = observed.Count(item => item.Witnessed);
            matrixComplete &= completed == PlannedRepeats;
            recipes.Add(new(retries, fault, observed.Length, completed, witnessed,
                observed.Length == 0 ? null : (double)witnessed / observed.Length,
                fault != SpikeFault.None && repeatsComplete && completed == PlannedRepeats && witnessed == PlannedRepeats));
        }
        if (outcomes.Any(item => !RetrySettings.Contains(item.Trial.Configuration.Retries) || !FaultSettings.Contains(item.Trial.Configuration.Fault)))
        {
            failures.Add("Unexpected configuration outside the approved matrix.");
            matrixComplete = false;
        }

        var finding = outcomes.Any(item => item.Witnessed) ? "yes" : matrixComplete && failures.Count == 0 ? "no" : "inconclusive";
        return new(finding, matrixComplete, failures, recipes);
    }

    private static List<string> Validate(TrialEvidence trial, TimingSettings timings)
    {
        var failures = new List<string>(trial.Failures);
        if (!trial.Executed) failures.Add("Trial was not executed.");
        if (trial.Timings != timings) failures.Add("Trial timings differ from the frozen matrix timings.");
        if (!trial.CleanupSucceeded) failures.Add("Trial cleanup failed.");
        if (string.IsNullOrWhiteSpace(trial.TrialId) || string.IsNullOrWhiteSpace(trial.Target.Topic) ||
            string.IsNullOrWhiteSpace(trial.Target.InstanceId) || string.IsNullOrWhiteSpace(trial.Target.AttemptId))
            failures.Add("Missing target or trial identity.");
        if (!trial.Accepted || trial.SynchronousError is not null) failures.Add("Target Produce was not accepted cleanly.");
        if (!RoutingVerified(trial)) failures.Add("Producer metadata routing through the proxy is unverified.");
        if (!HealthyControlVerified(trial)) failures.Add("Healthy control is unverified.");
        if (!trial.IndependentObserver) failures.Add("Independent direct observation is unverified.");
        if (!FaultVerified(trial)) failures.Add("Fault activation before target submission is unverified.");
        if (trial.Submitted is not { } submitted || submitted < trial.Started || submitted > trial.Sealed)
            failures.Add("Target submission timestamp is missing or outside the trial.");
        if (trial.Callbacks.Count != 1) failures.Add("Missing or duplicate terminal callback.");
        else if (!CallbackMatches(trial, trial.Callbacks[0])) failures.Add("Callback identity, bytes or timestamp conflict.");
        if (trial.Configuration.Fault == SpikeFault.None && trial.Callbacks.Any(callback => callback.IsError))
            failures.Add("No-fault control has a delivery failure.");

        if (trial.Scan is not { } scan) failures.Add("Missing bounded observation scan.");
        else
        {
            if (!scan.Complete || scan.Failure is not null || scan.Start < 0 || scan.End < scan.Start || scan.Position < scan.End)
                failures.Add("Observation scan is incomplete or has conflicting boundaries.");
            if (scan.Records.Any(record => !ConsumedMatches(trial, record))) failures.Add("Consumed identity, bytes or bounded offset conflict.");
            if (trial.Configuration.Fault == SpikeFault.None && scan.Records.Count == 0) failures.Add("No-fault control was not independently consumed.");
        }
        if (trial.Producer is not { } producer ||
            !producer.ManagedVersion.StartsWith("2.16.0", StringComparison.Ordinal) ||
            !producer.NativeVersion.StartsWith("2.16.0", StringComparison.Ordinal) ||
            producer.Logs.Count == 0 || producer.Statistics.Count == 0)
            failures.Add("Missing pinned producer versions, debug logs or statistics.");
        if (trial.Producer is { } diagnostics && diagnostics.Errors.Any(IsCaptureFailure))
            failures.Add("Callback capture failed or conflicted with the submitted message.");
        if (trial.Infrastructure is not { } infrastructure || string.IsNullOrWhiteSpace(infrastructure.ProxyState) ||
            string.IsNullOrWhiteSpace(infrastructure.Metrics) || string.IsNullOrWhiteSpace(infrastructure.KafkaLogs) ||
            string.IsNullOrWhiteSpace(infrastructure.ProxyLogs))
            failures.Add("Missing infrastructure diagnostics.");
        return failures;
    }

    private static bool IsWitness(TrialEvidence trial) =>
        trial.Executed && trial.Configuration.Fault != SpikeFault.None && trial.Accepted && trial.SynchronousError is null &&
        RoutingVerified(trial) && FaultVerified(trial) && HealthyControlVerified(trial) && trial.IndependentObserver &&
        trial.Callbacks.Count == 1 && trial.Callbacks[0].IsError && CallbackMatches(trial, trial.Callbacks[0]) &&
        trial.Producer is { } producer && !producer.Errors.Any(IsCaptureFailure) &&
        trial.Scan is { } scan && scan.Start >= 0 && scan.End >= scan.Start &&
        scan.Records.Any(record => ConsumedMatches(trial, record)) && scan.Records.All(record => ConsumedMatches(trial, record));

    private static bool IsCaptureFailure(string error) =>
        error.Contains("callback-key-or-payload-conflict", StringComparison.Ordinal) ||
        error.Contains("callback-capture-failed", StringComparison.Ordinal);

    private static bool RoutingVerified(TrialEvidence trial) => trial.RouteVerified &&
        !string.IsNullOrWhiteSpace(trial.ExpectedProducerEndpoint) && trial.AdvertisedBrokers.Count > 0 &&
        trial.AdvertisedBrokers.All(broker => string.Equals(broker, trial.ExpectedProducerEndpoint, StringComparison.OrdinalIgnoreCase));

    private static bool FaultVerified(TrialEvidence trial) => trial.FaultVerified &&
        (trial.Configuration.Fault == SpikeFault.None ||
         !string.IsNullOrWhiteSpace(trial.FaultState) && trial.FaultActivated is { } activated &&
         trial.Submitted is { } submitted && activated >= trial.Started && activated <= submitted);

    private static bool HealthyControlVerified(TrialEvidence trial)
    {
        if (!trial.HealthyControlVerified || trial.WarmupTarget is not { } target ||
            trial.WarmupCallbacks is not { Count: 1 } callbacks || callbacks[0].IsError ||
            trial.WarmupScan is not { Complete: true, Failure: null } scan || scan.Records.Count == 0 ||
            scan.Start < 0 || scan.End < scan.Start || scan.Position < scan.End ||
            target.Topic != trial.Target.Topic || target.InstanceId != trial.Target.InstanceId || target.AttemptId == trial.Target.AttemptId)
            return false;
        var warmup = trial with { Target = target, Scan = scan, Submitted = trial.Started,
            Sealed = trial.FaultActivated ?? trial.Submitted ?? trial.Sealed };
        return CallbackMatches(warmup, callbacks[0]) && scan.Records.All(record => ConsumedMatches(warmup, record));
    }

    private static bool CallbackMatches(TrialEvidence trial, DeliveryObservation callback) =>
        trial.Submitted is { } submitted && callback.Timestamp >= submitted && callback.Timestamp <= trial.Sealed &&
        callback.Topic == trial.Target.Topic && callback.InstanceId == trial.Target.InstanceId &&
        callback.AttemptId == trial.Target.AttemptId && callback.Key is not null && callback.Payload is not null &&
        callback.Key.AsSpan().SequenceEqual(trial.Target.Key) && callback.Payload.AsSpan().SequenceEqual(trial.Target.Payload) &&
        HeadersMatch(callback.Headers, trial.Target) && !string.IsNullOrWhiteSpace(callback.ErrorCode) &&
        !string.IsNullOrWhiteSpace(callback.PersistenceStatus) &&
        callback.IsError != string.Equals(callback.ErrorCode, "NoError", StringComparison.Ordinal);

    private static bool ConsumedMatches(TrialEvidence trial, ConsumedObservation record) =>
        trial.Scan is { } scan && trial.Submitted is { } submitted && record.Timestamp >= submitted && record.Timestamp <= trial.Sealed &&
        record.Topic == trial.Target.Topic && record.Partition == 0 && record.Offset >= scan.Start && record.Offset < scan.End &&
        record.InstanceId == trial.Target.InstanceId && record.AttemptId == trial.Target.AttemptId &&
        record.Key.AsSpan().SequenceEqual(trial.Target.Key) && record.Payload.AsSpan().SequenceEqual(trial.Target.Payload) &&
        HeadersMatch(record.Headers, trial.Target);

    private static bool HeadersMatch(IReadOnlyList<HeaderObservation>? headers, AttemptMessage target) =>
        headers is not null && HeaderMatches(headers, "instance-id", target.InstanceId) && HeaderMatches(headers, "attempt-id", target.AttemptId);

    private static bool HeaderMatches(IReadOnlyList<HeaderObservation> headers, string key, string value)
    {
        var matches = headers.Where(header => header.Key == key).ToArray();
        return matches.Length == 1 && matches[0].Value is { } bytes && bytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(value));
    }
}
