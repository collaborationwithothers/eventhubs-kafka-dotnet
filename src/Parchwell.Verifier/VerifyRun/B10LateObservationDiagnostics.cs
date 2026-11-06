using System.Globalization;
using Microsoft.Extensions.Logging;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

/// <summary>Identifies a recorded repeat's half-open original window for diagnostic landing labels.</summary>
internal sealed record B10LateObservationWindow(int Repeat, IReadOnlyList<WatermarkOffset> Starts, IReadOnlyList<WatermarkOffset> Ends);

internal sealed record B10LateLocation(string InstanceId, string AttemptId, int Partition, long Offset, string LandingRepeat);
internal sealed record B10LateGroup(string InstanceId, int Partition, string LandingRepeat, long Count);

/// <summary>Retains exact selected late-record frequencies and a bounded set of public location samples.</summary>
internal sealed record B10LateObservation(long Total, IReadOnlyList<B10LateGroup> Groups, IReadOnlyList<B10LateLocation> Samples)
{
    internal long OmittedSamples => Total - Samples.Count;
}

internal delegate B10LateObservation? B10LateObservationProjector(int sourceRepeat, IReadOnlyList<string> owners,
    RunWindow original, RunWindow late, IReadOnlyList<ConsumedRecord> records,
    IReadOnlyList<B10LateObservationWindow> windows);

/// <summary>Projects already reconciled late records without authorizing or changing scientific outcome counts.</summary>
internal static partial class B10LateObservationDiagnostics
{
    internal const int SampleLimit = 100;
    internal const int GroupLimit = 384;

    // Diagnostics must not enter the handler's evidence-failure path, even when a provider throws.
    internal static B10LateObservation? TryProject(int sourceRepeat, IReadOnlyList<string> owners, RunWindow original,
        RunWindow late, IReadOnlyList<ConsumedRecord> records, IReadOnlyList<B10LateObservationWindow> windows)
    {
        try { return Project(sourceRepeat, owners, original, late, records, windows); }
        catch (Exception) { return null; }
    }

    internal static B10LateObservation? Project(int sourceRepeat, IReadOnlyList<string> owners, RunWindow original,
        RunWindow late, IReadOnlyList<ConsumedRecord> records, IReadOnlyList<B10LateObservationWindow> windows)
    {
        if (owners.Count != 2 || owners.Distinct(StringComparer.Ordinal).Count() != 2 || late.Partitions.Count > 32 ||
            windows.Count != 5 || !windows.Select(w => w.Repeat).Order().SequenceEqual(Enumerable.Range(1, 5))) return null;
        var seen = new HashSet<(string Topic, int Partition, long Offset)>();
        var groups = new Dictionary<(string Instance, int Partition, string Landing), long>();
        var samples = new List<B10LateLocation>(SampleLimit);
        long total = 0;
        foreach (ConsumedRecord record in records.Where(IsSelected).OrderBy(r => r.Partition).ThenBy(r => r.Offset))
        {
            if (!seen.Add((record.Topic, record.Partition, record.Offset))) continue;
            string landing = Landing(record);
            var key = (record.InstanceId!, record.Partition, landing);
            groups[key] = groups.GetValueOrDefault(key) + 1;
            total++;
            if (samples.Count < SampleLimit)
                samples.Add(new(record.InstanceId!, record.AttemptId!, record.Partition, record.Offset, landing));
        }
        // The fixed owner, partition and landing-label bounds permit at most 384 groups.
        return new(total, groups.OrderBy(g => g.Key.Instance, StringComparer.Ordinal).ThenBy(g => g.Key.Partition)
            .ThenBy(g => g.Key.Landing, StringComparer.Ordinal).Select(g => new B10LateGroup(g.Key.Instance, g.Key.Partition, g.Key.Landing, g.Value)).ToArray(), samples);

        bool IsSelected(ConsumedRecord record) => record.Topic == RunWindow.Topic && record.Problems.Count == 0 &&
            record.InstanceId is not null && record.AttemptId is not null && record.Key is not null && record.Sequence is not null &&
            owners.Contains(record.InstanceId, StringComparer.Ordinal) && Contains(late, record) && !Contains(original, record);

        string Landing(ConsumedRecord record)
        {
            B10LateObservationWindow[] matches = windows.Where(window => window.Repeat > sourceRepeat &&
                window.Ends.Any(end => end.Partition == record.Partition && record.Offset < end.Offset &&
                    record.Offset >= (window.Starts.SingleOrDefault(start => start.Partition == record.Partition)?.Offset ?? 0))).ToArray();
            return matches.Length == 1 ? matches[0].Repeat.ToString(CultureInfo.InvariantCulture) : "gap";
        }
    }

    internal static void Write(ILogger logger, string runId, string phase, int sourceRepeat, bool scanAndReconciliationComplete,
        B10LateObservation? observation)
    {
        try
        {
            bool complete = scanAndReconciliationComplete && observation is not null;
            Summary(logger, "B10", runId, phase, sourceRepeat, complete, observation is not null,
                observation?.Total, observation?.Samples.Count, observation?.OmittedSamples);
            if (observation is null) return;
            foreach (B10LateGroup group in observation.Groups)
                Group(logger, "B10", runId, phase, sourceRepeat, complete, group.InstanceId, group.Partition, group.LandingRepeat, group.Count);
            foreach (B10LateLocation sample in observation.Samples)
                Location(logger, "B10", runId, phase, sourceRepeat, complete, sample.InstanceId, sample.AttemptId,
                    sample.Partition, sample.Offset, sample.LandingRepeat);
        }
        catch (Exception) { }
    }

    private static bool Contains(RunWindow window, ConsumedRecord record) => window.Partitions.Any(partition =>
        partition.Partition == record.Partition && record.Offset >= partition.Start && record.Offset < partition.End);

    [LoggerMessage(EventId = 35501, EventName = "B10LateObservationSummary", Level = LogLevel.Information,
        Message = "Late record observation scenario {scenario_id} run {run_id} phase {phase} source repeat {source_repeat}: observation_complete {observation_complete}, observation_available {observation_available}, total_selected_late_records {total_selected_late_records}, retained_samples {retained_samples}, omitted_samples {omitted_samples}.")]
    private static partial void Summary(ILogger logger, string scenario_id, string run_id, string phase, int source_repeat,
        bool observation_complete, bool observation_available, long? total_selected_late_records, int? retained_samples, long? omitted_samples);

    [LoggerMessage(EventId = 35502, EventName = "B10LateObservationGroup", Level = LogLevel.Information,
        Message = "Late record group scenario {scenario_id} run {run_id} phase {phase} source repeat {source_repeat}: observation_complete {observation_complete}, instance {instance_id}, partition {partition}, landing_repeat {landing_repeat}, selected_late_records {selected_late_records}.")]
    private static partial void Group(ILogger logger, string scenario_id, string run_id, string phase, int source_repeat,
        bool observation_complete, string instance_id, int partition, string landing_repeat, long selected_late_records);

    [LoggerMessage(EventId = 35503, EventName = "B10LateObservationLocation", Level = LogLevel.Information,
        Message = "Late record location scenario {scenario_id} run {run_id} phase {phase} source repeat {source_repeat}: observation_complete {observation_complete}, instance {instance_id}, attempt {attempt_id}, partition {partition}, offset {offset}, landing_repeat {landing_repeat}.")]
    private static partial void Location(ILogger logger, string scenario_id, string run_id, string phase, int source_repeat,
        bool observation_complete, string instance_id, string attempt_id, int partition, long offset, string landing_repeat);
}
