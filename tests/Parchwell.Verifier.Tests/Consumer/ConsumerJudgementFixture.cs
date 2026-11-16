using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.Tests.Consumer;

// Synthetic fixture B separates processing identity, commit corroboration and the broker observation.
internal static class ConsumerJudgementFixture
{
    internal static readonly DateTimeOffset Time = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    internal const string Group = "run1-c1-r1-consumers";
    internal static ConsumerJudgementRequest Create()
    {
        IReadOnlyDictionary<string, string> settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["enable.auto.commit"] = "true", ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000"
        };
        ConsumerMember[] members = [Member("subject", settings, [0], Enumerable.Range(100, 5)), Member("witness", settings, [], Enumerable.Range(105, 5))];
        var window = new RunWindow([new PartitionHigh(0, 100)], new Dictionary<int, long> { [0] = 110 }, [new PartitionWindow(0, 100, 110)], [], true);
        return new(VerifierResultStatus.CompleteValid, "run1", "C03", "C1", "C1", 1, Group,
            new ConsumerVerificationPlan(null, 600000, settings, settings), "broker:9092", new ConsumerArtifacts(members, 2, false, []),
            new ConsumerStartSnapshot(Group, Time, Time.AddSeconds(1), [new ConsumerSnapshotMember("subject", "subject", 0, 0), new ConsumerSnapshotMember("witness", "witness", 0, 0)]),
            window, Enumerable.Range(100, 10).Select(Ledger).ToArray(), Enumerable.Range(100, 10).Select(Consumed).ToArray(), Time.AddMinutes(10));
    }
    internal static ConsumerMember Member(string role, IReadOnlyDictionary<string,string> expected, IReadOnlyList<int> assigned, IEnumerable<int> offsets)
    {
        var effective = new SortedDictionary<string, string>(expected.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), StringComparer.Ordinal)
        {
            ["bootstrap.servers"] = "broker:9092", ["client.id"] = role, ["group.id"] = Group
        };
        var registration = new ConsumerRegistration("run1", "C03", "C1", "C1", 1, role, role, role + "-job", role, role + "-uid", "n1", "commit", Group, effective, Time);
        var rows = new List<ConsumerRow> { Assigned(role, 0, assigned) };
        rows.AddRange(offsets.Select(offset => Processed(role, offset)));
        rows.Add(Commit(role, 110));
        return WithRows(new ConsumerMember("run1/C1/r1/consumer/" + role + "/", registration, [], null, 1, false, false), rows);
    }
    internal static ConsumerRow Processed(string member, int offset) => new("processed", member, 0, Time.AddSeconds(2), 0, offset, "producer", "attempt-" + offset, "key-" + offset, offset, ProcessingStartedUtc: Time.AddSeconds(1));
    internal static ConsumerRow Assigned(string member, long sequence, IReadOnlyList<int> partitions, DateTimeOffset? time = null) => new("partition_event", member, sequence, time ?? Time, Event: "assigned", Partitions: partitions);
    internal static ConsumerRow Commit(string member, long offset, int partition = 0, int rowError = 0, int entryError = 0) => new("commit", member, 0, Time.AddSeconds(3), Source: "callback", Outcome: "callback", ErrorCode: rowError, ErrorReason: rowError == 0 ? null : "test", Offsets: [new ConsumerCommitOffset("document-events", partition, offset, entryError)]);
    internal static ConsumerRow EmptyCommit(string member) => new("commit", member, 0, Time.AddSeconds(3), Source: "callback", Outcome: "callback", Offsets: []);
    internal static ConsumerRow Kill(string member = "subject", DateTimeOffset? time = null) => new("kill_intent", member, 0, time ?? Time.AddSeconds(5), 0, 102);
    internal static ConsumerMember WithRows(ConsumerMember member, IEnumerable<ConsumerRow> rows, bool receipt = true)
    {
        ConsumerRow[] ordered = rows.Select((row, index) => row with { RowSequence = index, InstanceId = member.Registration!.InstanceId }).ToArray();
        ConsumerRegistration r = member.Registration!;
        ConsumerCompletion? completion = receipt ? new(r.RunId, r.ScenarioId, r.ConfigurationId, r.Phase, r.RepeatNumber, r.Role, r.InstanceId, r.JobName, r.PodUid, r.SourceCommit, ordered.Length, 1, ordered.LongCount(row => row.Kind == "processed"), Time.AddSeconds(6)) : null;
        return member with { Rows = ordered, Completion = completion };
    }
    internal static ConsumerJudgementRequest SetMember(ConsumerJudgementRequest request, string role, Func<ConsumerMember, ConsumerMember> update) => request with
    {
        Artifacts = request.Artifacts with { Members = request.Artifacts.Members.Select(member => member.Registration?.Role == role ? update(member) : member).ToArray() }
    };
    internal static ConsumerJudgementRequest SetReports(ConsumerJudgementRequest request, params long[] values)
    {
        request = SetMember(request, "subject", member => WithRows(member, member.Rows.Where(row => row.Kind != "commit").Concat(values.Select(value => Commit(member.Registration!.InstanceId, value)))));
        return SetMember(request, "witness", member => WithRows(member, member.Rows.Where(row => row.Kind != "commit")));
    }
    internal static ConsumerJudgementRequest Empty(long start = 100)
    {
        ConsumerJudgementRequest request = SetReports(Create());
        foreach (string role in new[] { "subject", "witness" }) request = SetMember(request, role, member => WithRows(member, member.Rows.Where(row => row.Kind != "processed")));
        return request with { Window = request.Window with { RecordedStartHighs = [new PartitionHigh(0, start)], EndHighs = new Dictionary<int,long> { [0] = start }, Partitions = [new PartitionWindow(0,start,start)] }, LedgerRows = [], ConsumedRecords = [] };
    }
    internal static ConsumerOffsetRead Read(long offset = 110, DateTimeOffset? end = null) => new("success", [0], Time.AddSeconds(7), end ?? Time.AddSeconds(8), 0, [new ConsumerObservedOffset(0, offset, 0)]);
    internal static ConsumerOffsetRead NotAttempted() => new("not_attempted", [0], null, null, null, []);
    internal static LedgerRow Ledger(int offset)
    {
        var identity = new RunIdentity("C03", "C1", "run1", "producer", "producer", "producer");
        var attempt = new AttemptEntry("producer", "attempt-" + offset, offset, "tenant", "document", "key-" + offset, offset, "event", 0, 0);
        return new(1, identity, attempt, "callback", true, 0, ParsedField<string>.Null(), ParsedField<int>.Missing(), "callback", 0, ParsedField<double>.Missing(), ParsedField<double>.Missing());
    }
    internal static ConsumedRecord Consumed(int offset) => new("document-events", 0, offset, "key-" + offset, "producer", "attempt-" + offset, offset, []);
}
