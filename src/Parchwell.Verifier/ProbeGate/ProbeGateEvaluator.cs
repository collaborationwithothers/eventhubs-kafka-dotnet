using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.ProbeGate;

/// <summary>Judges only the approved five-repeat A01/C0 probe gate.</summary>
internal static class ProbeGateEvaluator
{
    internal static bool IsSupportedGate(string scenarioId, ScenarioGate gate) =>
        scenarioId == "A01" && gate is Parchwell.Scenarios.ProbeGate probe &&
        probe.Configuration == "C0" && probe.Measurement.Id == "stats.broker.throttle.max" &&
        probe.Measurement.Role == KafkaClientRole.LoadGenProducer &&
        probe.Operator == ProbeOperator.GreaterThan && probe.ThresholdMs == 20_000 &&
        probe.Repeats == 5 && probe.Configurations.SequenceEqual(["C0", "C2", "C3"], StringComparer.Ordinal);

    internal static IReadOnlyList<string> GetRootReasons(IReadOnlyList<RunIdentity> expectedIdentities, int actualInputCount)
    {
        var reasons = new List<string>();
        if (expectedIdentities.Count != 5) reasons.Add("expected_repeat_count");
        if (actualInputCount != 5) reasons.Add("actual_repeat_count");
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        bool duplicate = false;
        bool foreign = false;
        foreach (RunIdentity identity in expectedIdentities)
        {
            if (!runIds.Add(identity.RunId)) duplicate = true;
            if (identity.ScenarioId != "A01" || identity.ConfigurationId != "C0") foreign = true;
        }
        if (duplicate) reasons.Add("duplicate_expected_run");
        if (foreign) reasons.Add("foreign_expected_run");
        return reasons;
    }

    internal static ProbeGateResult Evaluate(ProbeGateRequest request, string verifierCommit)
    {
        if (!IsSupportedGate("A01", request.Gate))
            throw new InvalidOperationException("The selected scenario has no supported probe gate.");
        IReadOnlyList<string> roots = GetRootReasons(request.ExpectedIdentities, request.Inputs.Count);
        if (roots.Count > 0) return new("inconclusive", roots, [], verifierCommit);

        var expected = request.ExpectedIdentities.ToHashSet();
        var runCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ProbeGateInput input in request.Inputs)
        {
            if (input.Status != ProbeGateInputStatus.Readable || input.Result is null) continue;
            string runId = input.Result.Identity.RunId;
            runCounts[runId] = runCounts.GetValueOrDefault(runId) + 1;
        }
        var repeats = new List<ProbeGateRepeat>(5);
        foreach (ProbeGateInput input in request.Inputs)
        {
            VerifierResult? result = input.Status == ProbeGateInputStatus.Readable ? input.Result : null;
            RunIdentity? identity = result?.Identity;
            ProbeRepeatObservation? observation = result?.ProbeObservation;
            string reason;
            if (input.Status == ProbeGateInputStatus.Missing) reason = "missing_result";
            else if (result is null) reason = "unreadable_result";
            else if (identity!.ScenarioId != "A01" || identity.ConfigurationId != "C0" || !expected.Contains(identity)) reason = "foreign_identity";
            else if (runCounts[identity.RunId] > 1) reason = "duplicate_identity";
            else if (observation is null || !observation.Usable) reason = "unusable_repeat";
            else reason = "usable";
            repeats.Add(new(input.Path, identity, observation?.Usable ?? false,
                observation?.Qualifying ?? false, reason, observation));
        }
        string outcome = repeats.Any(repeat => repeat.Reason != "usable") ? "inconclusive" :
            repeats.Any(repeat => repeat.Qualifying) ? "pass" : "untestable_at_build_scale";
        return new(outcome, [], repeats, verifierCommit);
    }
}
