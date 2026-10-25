using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.VerifyRun.Window;

internal sealed record PartitionWindow(int Partition, long Start, long End);

/// <summary>Fixes the half-open scan boundaries without contacting Kafka.</summary>
internal sealed record RunWindow(
    IReadOnlyList<PartitionHigh> RecordedStartHighs,
    IReadOnlyDictionary<int, long> EndHighs,
    IReadOnlyList<PartitionWindow> Partitions,
    IReadOnlyList<CheckAssessment> Assessments,
    bool CanAssign)
{
    internal const string Topic = "document-events";

    internal static RunWindow Create(ParseResult<StartLineNote> parsedStartNote,
        RunIdentity expectedIdentity, IReadOnlyDictionary<int, long>? endHighs = null) =>
        Create(parsedStartNote, expectedIdentity, expectedIdentity, endHighs);

    internal static RunWindow Create(ParseResult<StartLineNote> parsedStartNote,
        RunIdentity expectedProducerIdentity, RunIdentity expectedStartIdentity,
        IReadOnlyDictionary<int, long>? endHighs = null)
    {
        IReadOnlyDictionary<int, long> recordedEnds = endHighs is null
            ? new Dictionary<int, long>() : new Dictionary<int, long>(endHighs);
        if (parsedStartNote.IsExecutionFailure)
            throw new InvalidOperationException("The start-line input has an execution failure.");

        StartLineNote? note = parsedStartNote.Value;
        if (!parsedStartNote.IsSuccess || note is null)
        {
            CheckAssessment[] failures = parsedStartNote.Problems.Select(p => new CheckAssessment(7,
                AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail, AssessmentClassification.Invalid,
                p.Artifact, p.Row, p.Field, p.Code, p.Snippet)).ToArray();
            return new([], recordedEnds, [], failures.Length == 0 ? [Failure(7)] : failures, false);
        }

        var assessments = new List<CheckAssessment>();
        if (note.Identity != expectedStartIdentity) assessments.Add(Failure(7, "identity"));
        if (!string.Equals(note.Topic, Topic, StringComparison.Ordinal)) assessments.Add(Failure(7, "topic"));
        if (note.PartitionHighs.Count == 0) assessments.Add(Failure(7, "partition_highs"));
        var seen = new HashSet<int>();
        foreach (PartitionHigh high in note.PartitionHighs)
        {
            if (!seen.Add(high.Partition)) assessments.Add(Failure(7, "partition_highs"));
            if (high.High < 0) assessments.Add(Failure(7, "partition_highs"));
        }
        if (assessments.Count != 0) return new(note.PartitionHighs, recordedEnds, [], assessments, false);
        assessments.Add(Pass(7));
        if (endHighs is null) return new(note.PartitionHighs, recordedEnds, [], assessments, false);
        foreach (PartitionHigh high in note.PartitionHighs)
            if (!endHighs.ContainsKey(high.Partition)) assessments.Add(Failure(8, "partition_highs"));
        if (assessments.Any(a => a.CheckNumber == 8)) return new(note.PartitionHighs, recordedEnds, [], assessments, false);
        assessments.Add(Pass(8));

        Dictionary<int, long> starts = note.PartitionHighs.ToDictionary(p => p.Partition, p => p.High);
        PartitionWindow[] partitions = endHighs.OrderBy(p => p.Key)
            .Select(p => new PartitionWindow(p.Key, starts.GetValueOrDefault(p.Key), p.Value)).ToArray();
        foreach (PartitionWindow partition in partitions)
            if (partition.End < partition.Start) assessments.Add(Failure(9, "partition_highs"));
        return new(note.PartitionHighs, recordedEnds, partitions, assessments,
            !assessments.Any(a => a.CheckNumber == 9));
    }

    internal static CheckAssessment Pass(int check) => new(check, AssessmentEvaluation.Evaluated,
        AssessmentDisposition.Pass, null, null, null, null, null, null);

    internal static CheckAssessment Failure(int check, string? field = null) => new(check,
        AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
        check is 10 or 11 ? AssessmentClassification.Incomplete : AssessmentClassification.Invalid,
        check == 7 ? "start-line" : Topic, null, field, $"check_{check}_failed", null);
}
