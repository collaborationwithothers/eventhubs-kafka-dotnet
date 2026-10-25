using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Preserves a native projection's association with its captured envelope.</summary>
internal sealed record ReportAssociation(StatisticsEnvelope Envelope, ParseResult<NativeClientReport> Native);

/// <summary>Judges recorded statistics and capture completion without consulting typed projections.</summary>
internal static class StatisticsChecks
{
    /// <summary>Evaluates checks 17 through 26 independently over the capture-order stream.</summary>
    public static IReadOnlyList<CheckResult> Evaluate(RunJudgementRequest request)
    {
        var checks = Enumerable.Range(17, 10).ToDictionary(number => number, number => new Observation(number));
        AddProblems(checks[17], request.Statistics.Problems);
        AddProblems(checks[17], request.Lifecycle.Problems);
        AddProblems(checks[17], request.Capabilities.Problems);
        if (request.Capabilities.Value is { IsFaulted: true })
            checks[17].Fail("capabilities.json", null, "is_faulted");

        bool statisticsAvailable = request.Statistics.Value is not null;
        if (statisticsAvailable)
        {
            if (request.Statistics.Value!.Count == 0) checks[18].Fail("statistics.jsonl", null, null);
        }
        else checks[18].Missing = true;

        CapturedCapabilities? capabilities = request.Capabilities.Value;
        Completion? completion = capabilities?.StatisticsCompletion.Value;
        if (capabilities is null) checks[19].Missing = true;
        else if (capabilities.StatisticsCompletion.Presence != FieldPresence.Value)
            checks[19].Fail("capabilities.json", null, "statistics_completion");
        else
        {
            if (!completion!.IsComplete) checks[19].Fail("capabilities.json", null, "statistics_completion.is_complete");
            if (completion.DrainBoundaryMicroseconds.Presence != FieldPresence.Value)
                checks[19].Fail("capabilities.json", null, "statistics_completion.drain_boundary_microseconds");
        }

        for (int number = 20; number <= 26; number++) checks[number].Missing = !statisticsAvailable;
        bool associationsComplete = statisticsAvailable && request.Reports.Count == request.Statistics.Value!.Count;
        if (!associationsComplete)
            foreach (int number in new[] { 20, 21, 22, 23, 25, 26 }) checks[number].Missing = true;
        long? previousTimestamp = null;
        Int128? creation = null;
        var counters = new Dictionary<(string Broker, string Field), long>();
        var observed = new HashSet<string>(StringComparer.Ordinal);
        var qualifying = new HashSet<string>(StringComparer.Ordinal);
        var reportSets = new List<(HashSet<string> Included, HashSet<string> Unknown)?>();
        int? selected = null;
        bool selectionUnknown = !associationsComplete;
        int? firstUnknownTimestamp = associationsComplete ? null : 0;
        bool boundaryAvailable = completion is not null && completion.DrainBoundaryMicroseconds.Presence == FieldPresence.Value;

        for (int index = 0; index < request.Reports.Count; index++)
        {
            ReportAssociation association = request.Reports[index];
            long row = index + 1L;
            foreach (CapturedDiagnostic diagnostic in association.Envelope.Diagnostics)
                checks[20].Fail("statistics.jsonl", row, diagnostic.Field.Value);
            AddProblems(checks[20], association.Native.Problems.Where(problem => problem.CheckNumber == 20), row);
            AddProblems(checks[21], association.Native.Problems.Where(problem => problem.CheckNumber == 21), row);
            NativeClientReport? report = association.Native.Value;
            if (report is null)
            {
                foreach (int number in new[] { 21, 22, 23, 25 }) checks[number].Missing = true;
                reportSets.Add(null);
                if (selected is null)
                {
                    selectionUnknown = true;
                    firstUnknownTimestamp ??= index;
                }
                continue;
            }

            if (report.TimestampMicroseconds.Presence == FieldPresence.Value)
            {
                long timestamp = report.TimestampMicroseconds.Value;
                if (previousTimestamp is long previous && timestamp <= previous)
                    checks[22].Fail("statistics.jsonl", row, "ts");
                previousTimestamp = timestamp;
                if (report.AgeMicroseconds.Presence == FieldPresence.Value)
                {
                    Int128 currentCreation = (Int128)timestamp - report.AgeMicroseconds.Value;
                    if (creation is Int128 origin && currentCreation != origin)
                        checks[22].Fail("statistics.jsonl", row, "age");
                    creation ??= currentCreation;
                }
                else checks[22].Missing = true;
                if (boundaryAvailable && selected is null && timestamp >= completion!.DrainBoundaryMicroseconds.Value)
                    selected = index;
            }
            else
            {
                checks[22].Missing = true;
                if (selected is null)
                {
                    selectionUnknown = true;
                    firstUnknownTimestamp ??= index;
                }
            }

            if (report.Brokers.Presence != FieldPresence.Value)
            {
                foreach (int number in new[] { 21, 23, 25 }) checks[number].Missing = true;
                reportSets.Add(null);
                continue;
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var unknown = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeBrokerReport broker in report.Brokers.Value!.Values)
            {
                string key = broker.DictionaryKey;
                observed.Add(key);
                Require(checks[21], broker.Name.Presence, row, key, "name");
                Require(checks[21], broker.NodeId.Presence, row, key, "nodeid");
                Require(checks[21], broker.Source.Presence, row, key, "source");
                Require(checks[21], broker.State.Presence, row, key, "state");
                foreach ((string field, ParsedField<long> value) in CounterFields(broker))
                {
                    Require(checks[21], value.Presence, row, key, field);
                    if (value.Presence != FieldPresence.Value) { checks[23].Missing = true; continue; }
                    if (counters.TryGetValue((key, field), out long previous) && value.Value < previous)
                        checks[23].Fail("statistics.jsonl", row, $"brokers.{key}.{field}");
                    counters[(key, field)] = value.Value;
                }
                RequireWindow(checks[21], broker.Throttle, row, key, "throttle", maximum: true);
                RequireWindow(checks[21], broker.RoundTripTime, row, key, "rtt", maximum: false);
                if (broker.Source.Presence != FieldPresence.Value) unknown.Add(key);
                else if (IsQualifyingSource(broker.Source.Value)) { keys.Add(key); qualifying.Add(key); }
            }
            reportSets.Add((keys, unknown));
            if (unknown.Count != 0) checks[25].Missing = true;
        }

        if (!request.ExpectedBrokerInventoryAvailable) checks[24].Missing = true;
        if (request.ExpectedBrokerInventoryAvailable && completion is null) checks[24].Missing = true;
        foreach (string expected in request.ExpectedBrokerInventoryAvailable ? request.ExpectedBrokerNames : Array.Empty<string>())
        {
            if (completion?.MissingBrokers.Contains(expected, StringComparer.Ordinal) == true)
                checks[24].Fail("capabilities.json", null, "statistics_completion.missing_brokers");
            if (statisticsAvailable && !observed.Contains(expected))
            {
                // An unreadable native report might contain the expected key.
                if (!associationsComplete || request.Reports.Any(report => report.Native.Value is null || report.Native.Value.Brokers.Presence != FieldPresence.Value))
                    checks[24].Missing = true;
                else checks[24].Fail("statistics.jsonl", null, "brokers." + expected);
            }
        }

        if (!boundaryAvailable || selectionUnknown) { checks[25].Missing = true; checks[26].Missing = true; }
        else if (statisticsAvailable && selected is null)
        {
            checks[25].Fail("statistics.jsonl", null, "ts");
            checks[26].Missing = true;
        }
        else if (selected is int first)
        {
            NativeClientReport? report = request.Reports[first].Native.Value;
            if (report?.Brokers.Presence == FieldPresence.Value)
            {
                foreach (string key in qualifying)
                    if (!report.Brokers.Value!.ContainsKey(key)) checks[25].Fail("statistics.jsonl", first + 1L, "brokers." + key);
            }
            else checks[25].Missing = true;
        }

        if (boundaryAvailable)
        {
            int prefixEnd = selected ?? reportSets.Count - 1;
            if (firstUnknownTimestamp is int uncertain) prefixEnd = Math.Min(prefixEnd, uncertain);
            if (selected is null || selectionUnknown) checks[26].Missing = true;
            var included = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string>? allowed = null;
            for (int index = 0; index <= prefixEnd; index++)
            {
                var observedSet = reportSets[index];
                if (!observedSet.HasValue)
                {
                    checks[26].Missing = true;
                    continue;
                }
                var current = observedSet.Value;
                if (current.Unknown.Count != 0) checks[26].Missing = true;
                var currentAllowed = new HashSet<string>(current.Included, StringComparer.Ordinal);
                currentAllowed.UnionWith(current.Unknown);
                // Present versus definitely absent witnesses a change even with partial reports.
                if ((allowed is not null && !current.Included.IsSubsetOf(allowed)) || !included.IsSubsetOf(currentAllowed))
                    checks[26].Fail("statistics.jsonl", index + 1L, "brokers");
                included.UnionWith(current.Included);
                if (allowed is null) allowed = currentAllowed;
                else allowed.IntersectWith(currentAllowed);
            }
        }
        return checks.Values.Select(check => check.Result()).ToArray();
    }

    internal static bool IsQualifyingSource(string? source) => source is "learned" or "configured" or "logical";

    private static (string Field, ParsedField<long> Value)[] CounterFields(NativeBrokerReport broker) =>
    [
        ("req_timeouts", broker.RequestTimeouts), ("txretries", broker.TransmissionRetries),
        ("txerrs", broker.TransmissionErrors), ("rxerrs", broker.ReceiveErrors), ("disconnects", broker.Disconnects)
    ];

    private static void Require(Observation check, FieldPresence presence, long row, string broker, string field)
    {
        if (presence != FieldPresence.Value && !check.Diagnostics.Any(diagnostic => diagnostic.Row == row && diagnostic.Field == $"brokers.{broker}.{field}"))
            check.Fail("statistics.jsonl", row, $"brokers.{broker}.{field}");
    }

    private static void RequireWindow(Observation check, ParsedField<NativeWindow> field, long row, string broker, string name, bool maximum)
    {
        Require(check, field.Value?.Count.Presence ?? FieldPresence.Missing, row, broker, name + ".cnt");
        Require(check, maximum ? field.Value?.Maximum.Presence ?? FieldPresence.Missing : field.Value?.P99.Presence ?? FieldPresence.Missing,
            row, broker, name + (maximum ? ".max" : ".p99"));
    }

    private static void AddProblems(Observation check, IEnumerable<StructuralProblem> problems, long? row = null)
    {
        foreach (StructuralProblem problem in problems)
            check.Diagnostics.Add(new ResultDiagnostic(check.Number, problem.Code, problem.Artifact, problem.Row ?? row, problem.Field, problem.Snippet));
    }

    private sealed class Observation(int number)
    {
        public int Number { get; } = number;
        public bool Missing { get; set; }
        public List<ResultDiagnostic> Diagnostics { get; } = [];
        public void Fail(string artifact, long? row, string? field) =>
            Diagnostics.Add(new ResultDiagnostic(Number, $"check_{Number}_failed", artifact, row, field, null));
        public CheckResult Result() => Diagnostics.Count != 0 ? CheckResult.Fail(Number, Diagnostics)
            : Missing ? CheckResult.NotEvaluated(Number) : CheckResult.Pass(Number);
    }
}
