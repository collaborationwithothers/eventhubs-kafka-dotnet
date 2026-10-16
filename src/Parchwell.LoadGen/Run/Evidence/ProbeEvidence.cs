using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Evidence for one explicitly attributed probe repeat.</summary>
public sealed record ProbeRepeatEvidence(
    RunIdentity Identity,
    string ScenarioId,
    string ConfigurationId,
    KafkaClientRole Role,
    ImmutableArray<KafkaStatisticsReport> Reports,
    bool SelectedMeasurementCoverageComplete,
    bool OracleValid);

/// <summary>Outcome of a pure A01 probe evaluation.</summary>
public enum ProbeEvaluationStatus
{
    Pass,
    UntestableAtBuildScale,
    Inconclusive
}

/// <summary>Pure evaluation result; no verifier counts or oracle outcome are included.</summary>
public sealed record ProbeEvaluationResult(ProbeEvaluationStatus Status, ImmutableArray<string> Diagnostics);

/// <summary>Evaluates the configured throttle probe from immutable, attributed statistics reports.</summary>
public static class PureProbeEvaluator
{
    /// <summary>Evaluates every configured repeat and ignores oracle eligibility when judging probe usability.</summary>
    public static ProbeEvaluationResult Evaluate(
        ProbeGate expectedProbeGate,
        string expectedScenarioId,
        ImmutableArray<ProbeRepeatEvidence> repeats)
    {
        ArgumentNullException.ThrowIfNull(expectedProbeGate);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedScenarioId);
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        if (expectedProbeGate.Repeats != 5 || expectedProbeGate.Configuration != "C0" ||
            expectedProbeGate.Measurement.Id != "stats.broker.throttle.max" ||
            expectedProbeGate.Measurement.Role != KafkaClientRole.LoadGenProducer ||
            expectedProbeGate.Operator != ProbeOperator.GreaterThan || expectedProbeGate.ThresholdMs <= 0)
        {
            return new(ProbeEvaluationStatus.Inconclusive, ["probe_gate_does_not_match_a01_contract"]);
        }

        if (repeats.Length != expectedProbeGate.Repeats)
        {
            diagnostics.Add("required_repeat_count_mismatch");
            return new(ProbeEvaluationStatus.Inconclusive, diagnostics.ToImmutable());
        }

        var runIds = new HashSet<string>(StringComparer.Ordinal);
        bool anyQualifyingObservation = false;
        bool anyUnusableRepeat = false;
        foreach (ProbeRepeatEvidence repeat in repeats)
        {
            if (repeat is null || repeat.Identity is null)
            {
                diagnostics.Add("repeat_identity_missing");
                anyUnusableRepeat = true;
                continue;
            }

            try
            {
                repeat.Identity.Validate();
            }
            catch (ArgumentException)
            {
                diagnostics.Add("repeat_identity_invalid");
                anyUnusableRepeat = true;
                continue;
            }
            catch (InvalidOperationException)
            {
                diagnostics.Add("repeat_identity_invalid");
                anyUnusableRepeat = true;
                continue;
            }
            if (!runIds.Add(repeat.Identity.RunId))
            {
                diagnostics.Add("repeat_run_identity_duplicate");
                anyUnusableRepeat = true;
            }

            if (!StringComparer.Ordinal.Equals(repeat.ScenarioId, expectedScenarioId) ||
                !StringComparer.Ordinal.Equals(repeat.Identity.ScenarioId, expectedScenarioId) ||
                !StringComparer.Ordinal.Equals(repeat.Identity.ConfigurationId, expectedProbeGate.Configuration) ||
                !StringComparer.Ordinal.Equals(repeat.ConfigurationId, expectedProbeGate.Configuration) ||
                repeat.Role != expectedProbeGate.Measurement.Role)
            {
                diagnostics.Add("repeat_attribution_mismatch");
                anyUnusableRepeat = true;
                continue;
            }

            if (!repeat.SelectedMeasurementCoverageComplete || repeat.Reports.IsDefaultOrEmpty)
            {
                diagnostics.Add("selected_measurement_coverage_incomplete");
                anyUnusableRepeat = true;
                continue;
            }

            bool hasUsableWindow = false;
            foreach (KafkaStatisticsReport report in repeat.Reports)
            {
                if (report is null || report.Identity is null || report.Snapshot is null ||
                    !StringComparer.Ordinal.Equals(report.Snapshot.ClientId, repeat.Identity.ClientId) ||
                    !StringComparer.Ordinal.Equals(report.Identity.ScenarioId, repeat.Identity.ScenarioId) ||
                    !StringComparer.Ordinal.Equals(report.Identity.RunId, repeat.Identity.RunId) ||
                    report.Identity.Role != repeat.Role ||
                    !StringComparer.Ordinal.Equals(report.Identity.ClientId, repeat.Identity.ClientId) ||
                    report.Diagnostics.IsDefault || !report.Diagnostics.IsEmpty)
                {
                    anyUnusableRepeat = true;
                    diagnostics.Add("statistics_report_attribution_or_snapshot_invalid");
                    continue;
                }

                foreach (KafkaBrokerStatistics broker in report.Snapshot.Brokers)
                {
                    if (broker.Throttle.Count <= 0 || broker.Throttle.Value is null)
                    {
                        continue;
                    }
                    hasUsableWindow = true;
                    if (broker.Throttle.Value.Value > expectedProbeGate.ThresholdMs)
                    {
                        anyQualifyingObservation = true;
                    }
                }
            }

            if (!hasUsableWindow)
            {
                diagnostics.Add("repeat_has_no_nonempty_throttle_window");
                anyUnusableRepeat = true;
            }
        }

        if (anyUnusableRepeat || runIds.Count != expectedProbeGate.Repeats)
        {
            return new(ProbeEvaluationStatus.Inconclusive, diagnostics.Distinct().ToImmutableArray());
        }
        return anyQualifyingObservation
            ? new(ProbeEvaluationStatus.Pass, diagnostics.ToImmutable())
            : new(ProbeEvaluationStatus.UntestableAtBuildScale, diagnostics.ToImmutable());
    }
}
