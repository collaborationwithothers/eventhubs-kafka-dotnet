using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.A01;

/// <summary>Contains A01 projections and record-only diagnostics independently of run status.</summary>
internal sealed record A01EvidenceRecording(
    ProbeRepeatObservation? ProbeObservation,
    A01RecordedEvidence? RecordedEvidence,
    IReadOnlyList<ResultDiagnostic> Diagnostics,
    WorkloadObservation? WorkloadObservation = null);

/// <summary>Records the approved A01 measurements without adding run-validity checks.</summary>
internal static class A01EvidenceRecorder
{
    internal static A01EvidenceRecording Record(RunJudgementRequest request, RunJudgementResult judgement)
    {
        if (request.Identity.ScenarioId != "A01" || request.Identity.ConfigurationId != "C0")
            return new(null, null, []);

        return Project(request, judgement, includeProbe: true);
    }

    internal static A01EvidenceRecording RecordC3(RunJudgementRequest request, RunJudgementResult judgement)
    {
        if (request.Identity.ScenarioId != "A01" || request.Identity.ConfigurationId != "C3")
            return new(null, null, []);
        return Project(request, judgement, includeProbe: false);
    }

    private static A01EvidenceRecording Project(RunJudgementRequest request, RunJudgementResult judgement, bool includeProbe)
    {
        int[]? codes = request.Ledger.IsSuccess && request.Ledger.Value is not null
            ? request.Ledger.Value.Select(row => row.ErrorCode).Where(code => code != 0).Distinct().Order().ToArray()
            : null;
        var diagnostics = new List<ResultDiagnostic>();
        (A01WindowEvidence Window, long Count)? selected = null;
        bool complete = Enumerable.Range(17, 10).All(Passed);
        IReadOnlyList<A01WindowEvidence>? windows = complete ? RecordWindows() : null;
        ProbeRepeatObservation observation = new(false, false, null, null, null, null);
        bool usable = complete && Passed(3) && Passed(27);
        if (usable && selected is { } chosen)
            observation = new(true, chosen.Window.ThrottleMaximumMilliseconds!.Value > 20_000,
                chosen.Window.Broker, chosen.Window.ReportIndex, chosen.Count, chosen.Window.ThrottleMaximumMilliseconds);
        else if (usable)
            observation = new(true, false, null, null, null, null);
        WorkloadRecording workload=WorkloadRecorder.Record(request.Manifest,request.Ledger);
        diagnostics.AddRange(workload.Diagnostics);
        return new(includeProbe ? observation : null, new(codes, windows), diagnostics,workload.Observation);

        bool Passed(int number) => judgement.Assessments.Any(assessment => assessment.CheckNumber == number &&
            assessment.Evaluation == AssessmentEvaluation.Evaluated && assessment.Disposition == AssessmentDisposition.Pass);

        IReadOnlyList<A01WindowEvidence> RecordWindows()
        {
            var result = new List<A01WindowEvidence>();
            var previous = new Dictionary<string, (long Timeouts, long Disconnects)>(StringComparer.Ordinal);
            for (int index = 0; index < request.Reports.Count; index++)
            {
                NativeClientReport report = request.Reports[index].Native.Value!;
                foreach ((string key, NativeBrokerReport broker) in report.Brokers.Value!.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    (long timeouts, long disconnects) = previous.GetValueOrDefault(key);
                    long? timeoutIncrement = Increment(broker.RequestTimeouts.Value, timeouts, index, key, "req_timeouts");
                    long? disconnectIncrement = Increment(broker.Disconnects.Value, disconnects, index, key, "disconnects");
                    previous[key] = (broker.RequestTimeouts.Value, broker.Disconnects.Value);
                    NativeWindow throttle = broker.Throttle.Value!;
                    NativeWindow rtt = broker.RoundTripTime.Value!;
                    var window = new A01WindowEvidence(key, index, report.TimestampMicroseconds.Value, timeoutIncrement, disconnectIncrement,
                        throttle.Count.Value > 0 ? throttle.Maximum.Value : null,
                        rtt.Count.Value > 0 ? rtt.P99.Value : null);
                    result.Add(window);
                    // Equal maxima retain the earlier report, then the ordinal dictionary key.
                    if (throttle.Count.Value > 0 && (selected is null ||
                        throttle.Maximum.Value > selected.Value.Window.ThrottleMaximumMilliseconds!.Value))
                        selected = (window, throttle.Count.Value);
                }
            }
            return result;
        }

        long? Increment(long current, long previous, int index, string key, string field)
        {
            try { return checked(current - previous); }
            catch (OverflowException)
            {
                if (diagnostics.Count < request.Limits.MaxDiagnosticsPerCheck)
                    diagnostics.Add(new(29, "increment_overflow", "statistics.jsonl", index + 1L, $"brokers.{key}.{field}", null));
                return null;
            }
        }
    }
}
