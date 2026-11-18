using System.Globalization;
using System.Text.Json;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;
using F = Parchwell.Verifier.Tests.Consumer.ConsumerJudgementFixture;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
// The membership observation of a scenario whose subject processes one slow message (items/508/plan-c03-scenario.md, sections 4.2, 4.3 and 7.3).
// Fixture F: the subject registers at 00:00:00 and the witness at 00:00:05 on 2026-01-01, so boundary B is 00:06:00.
// The subject holds partitions 0 to 15 and the witness 16 to 31. The subject's slow row is partition 3, offset 10, from 00:02:00 to 00:03:30.
public sealed class ConsumerMembershipTests
{
    private static readonly DateTimeOffset Day = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly int[] SubjectPartitions = Enumerable.Range(0, 16).ToArray();
    private static readonly int[] WitnessPartitions = Enumerable.Range(16, 16).ToArray();
    private static readonly int[] AllPartitions = Enumerable.Range(0, 32).ToArray();
    private static readonly int[] UpperPartitions = Enumerable.Range(8, 24).ToArray();
    private static readonly int[] LowerPartitions = Enumerable.Range(0, 8).ToArray();
    private static readonly string[] OnlySlowReason = ["consumer_slow_message_invalid"];

    private static DateTimeOffset T(string clock) => Day + TimeSpan.Parse(clock, CultureInfo.InvariantCulture);

    private static ConsumerRow Processed(string member, int partition, int offset, string started, string ended, bool slow = false) =>
        new("processed", member, 0, T(ended), partition, offset, "producer", $"attempt-{partition}-{offset}", $"key-{partition}-{offset}",
            Sequence(partition, offset), Slow: slow, ProcessingStartedUtc: T(started));

    private static ConsumerRow Event(string member, string kind, string time, int[] partitions) =>
        new("partition_event", member, 0, T(time), Event: kind, Partitions: partitions);

    private static ConsumerRow Commit(string member, string time, int rowError, params (int Partition, long Offset, int Error)[] entries) =>
        new("commit", member, 0, T(time), Source: "callback", Outcome: "callback", ErrorCode: rowError, ErrorReason: rowError == 0 ? null : "test",
            Offsets: entries.Select(e => new ConsumerCommitOffset("document-events", e.Partition, e.Offset, e.Error)).ToArray());

    private static long Sequence(int partition, int offset) => partition * 1000L + offset;

    private static ConsumerRow[] SubjectHead() => new[] { Event("subject", "assigned", "00:00:30", SubjectPartitions) }
        .Concat(Enumerable.Range(0, 10).Select(offset => Processed("subject", 3, offset, "00:00:40", "00:00:41")))
        .Append(Processed("subject", 3, 10, "00:02:00", "00:03:30", slow: true)).ToArray();

    private static ConsumerRow[] WitnessHead() => new[] { Event("witness", "assigned", "00:00:30", WitnessPartitions) }
        .Concat(Enumerable.Range(0, 8).Select(offset => Processed("witness", 20, offset, "00:00:40", "00:00:41"))).ToArray();

    // The witness's rows of case O1, with the time of the takeover row given.
    private static ConsumerRow[] WitnessAfterTakeover(string takeover) =>
    [
        Processed("witness", 3, 10, "00:03:10", "00:03:11"),
        Event("witness", "assigned", takeover, AllPartitions),
        Event("witness", "revoked", "00:03:39", AllPartitions),
        Event("witness", "assigned", "00:03:40", UpperPartitions)
    ];

    private static ConsumerRow[] SubjectLostThenAssigned() =>
        [Event("subject", "lost", "00:03:31", SubjectPartitions), Event("subject", "assigned", "00:03:40", LowerPartitions)];

    private static ConsumerJudgementRequest Build(IEnumerable<ConsumerRow> subjectTail, IEnumerable<ConsumerRow> witnessTail,
        string subjectRegistered = "00:00:00", string witnessRegistered = "00:00:05", string mode = "inline", string subjectFinalCommit = "00:04:00")
    {
        ConsumerJudgementRequest request = F.Create();
        IReadOnlyDictionary<string, string> settings = request.Plan.SubjectSettings;
        ConsumerMember Make(string role, string registered, IEnumerable<ConsumerRow> rows)
        {
            ConsumerMember member = F.Member(role, settings, [], []);
            return F.WithRows(member with { Registration = member.Registration! with { RegisteredUtc = T(registered) } }, rows);
        }
        ConsumerMember subject = Make("subject", subjectRegistered,
            SubjectHead().Concat(subjectTail).Append(Commit("subject", subjectFinalCommit, 0, (3, 11, 0))));
        ConsumerMember witness = Make("witness", witnessRegistered,
            WitnessHead().Concat(witnessTail).Append(Commit("witness", "00:04:00", 0, (20, 8, 0))));
        var window = new RunWindow([new PartitionHigh(3, 0), new PartitionHigh(20, 0)], new Dictionary<int, long> { [3] = 11, [20] = 8 },
            [new PartitionWindow(3, 0, 11), new PartitionWindow(20, 0, 8)], [], true);
        var records = Enumerable.Range(0, 11).Select(offset => (Partition: 3, Offset: offset))
            .Concat(Enumerable.Range(0, 8).Select(offset => (Partition: 20, Offset: offset))).ToArray();
        return request with
        {
            Plan = request.Plan with { WindowMs = 360000, SlowMessageMode = mode },
            Artifacts = new ConsumerArtifacts([subject, witness], 2, false, []),
            Window = window,
            LedgerRows = records.Select(r => Ledger(r.Partition, r.Offset)).ToArray(),
            ConsumedRecords = records.Select(r => new ConsumedRecord("document-events", r.Partition, r.Offset, $"key-{r.Partition}-{r.Offset}",
                "producer", $"attempt-{r.Partition}-{r.Offset}", Sequence(r.Partition, r.Offset), [])).ToArray(),
            DeadlineInstant = T("01:00:00")
        };
    }

    private static LedgerRow Ledger(int partition, int offset)
    {
        var identity = new RunIdentity("C03", "C1", "run1", "producer", "producer", "producer");
        long sequence = Sequence(partition, offset);
        var attempt = new AttemptEntry("producer", $"attempt-{partition}-{offset}", sequence, "tenant", "document", $"key-{partition}-{offset}", sequence, "event", 0, 0);
        return new(1, identity, attempt, "callback", true, 0, ParsedField<string>.Null(), ParsedField<int>.Missing(), "callback", 0, ParsedField<double>.Missing(), ParsedField<double>.Missing());
    }

    private static ConsumerOffsetRead Read() => new("success", [3, 20], T("00:10:00"), T("00:10:01"), 0,
        [new ConsumerObservedOffset(3, 11, 0), new ConsumerObservedOffset(20, 8, 0)]);

    private static string Expect(int slowRows, int lost, int revoked, int witnessAssigned, bool during, long? takeover, int afterLost) => FormattableString.Invariant(
        $"{{\"slow_partition\":3,\"slow_offset\":10,\"slow_processed_rows\":{slowRows},\"subject_lost_rows\":{lost},\"subject_revoked_rows\":{revoked},\"witness_assigned_rows\":{witnessAssigned},\"witness_assigned_slow_partition_during_slow\":{(during ? "true" : "false")},\"witness_takeover_microseconds\":{(takeover is null ? "null" : takeover.Value.ToString(CultureInfo.InvariantCulture))},\"subject_commit_entries_after_lost\":{afterLost}}}");

    private static string MembershipOf(ConsumerJudgementRequest request)
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, Read());
        Assert.Equal("complete_valid", result.Consumer.GetProperty("status").GetString());
        string[] names = result.Consumer.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal("membership", names[Array.IndexOf(names, "counts") + 1]);
        return result.Consumer.GetProperty("membership").GetRawText();
    }

    [Fact]
    public void Given_InlineRepeatWhereTheSubjectLosesItsPartitions_O1_When_Judged_Then_MembershipCountsEachObservation() =>
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 0), MembershipOf(Build(SubjectLostThenAssigned(), WitnessAfterTakeover("00:03:01"))));

    [Fact]
    public void Given_AnEntryOnTheLostPartitionsBetweenLostAndAssigned_O2_When_Judged_Then_OnlyThatEntryIsCounted()
    {
        ConsumerRow[] subject = [SubjectLostThenAssigned()[0], Commit("subject", "00:03:35", 0, (3, 5, 0), (20, 7, 0)), SubjectLostThenAssigned()[1]];
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 1), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"))));
    }

    [Fact]
    public void Given_AFailedEntryBetweenLostAndAssigned_O3_When_Judged_Then_ItIsNotCounted()
    {
        ConsumerRow[] subject = [SubjectLostThenAssigned()[0], Commit("subject", "00:03:35", 0, (3, 5, 25), (20, 7, 0)), SubjectLostThenAssigned()[1]];
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 0), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"))));
    }

    [Fact]
    public void Given_AFailedCommitResultBetweenLostAndAssigned_When_Judged_Then_NoEntryOfItIsCounted()
    {
        ConsumerRow[] subject = [SubjectLostThenAssigned()[0], Commit("subject", "00:03:35", 7, (3, 5, 0)), SubjectLostThenAssigned()[1]];
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 0), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"))));
    }

    [Fact]
    public void Given_NoAssignedRowAfterLost_When_Judged_Then_CommitsAreCountedUpToTheLastRowBeforeB()
    {
        ConsumerRow[] subject = [SubjectLostThenAssigned()[0], Commit("subject", "00:03:35", 0, (3, 8, 0))];
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 1), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"), subjectFinalCommit: "00:06:30")));
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, 2), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"), subjectFinalCommit: "00:05:00")));
    }

    [Fact]
    public void Given_PauseRepeatWhoseSubjectRevokesExactlyAtB_O4_When_Judged_Then_NothingIsCountedAndThereIsNoTakeover() =>
        Assert.Equal(Expect(1, 0, 0, 0, false, null, 0), MembershipOf(Build(
            [Event("subject", "revoked", "00:06:00", SubjectPartitions)], [Event("witness", "assigned", "00:06:01", AllPartitions)], mode: "pause")));

    [Fact]
    public void Given_ARevokedRowOneTickBeforeB_O5_When_Judged_Then_ItIsCounted() =>
        Assert.Equal(Expect(1, 0, 1, 0, false, null, 0), MembershipOf(Build(
            [Event("subject", "revoked", "00:05:59.9999999", SubjectPartitions)], [Event("witness", "assigned", "00:06:01", AllPartitions)], mode: "pause")));

    [Theory]
    [InlineData("00:02:00", true, 0)]
    [InlineData("00:03:30", true, 90000000)]
    [InlineData("00:03:30.0000001", false, 90000000)]
    public void Given_TheWitnessTakeoverAtTheEdgesOfTheSlowMessage_O6_When_Judged_Then_DuringSlowAndTakeoverFollowTheirOwnRules(string takeover, bool during, long microseconds) =>
        Assert.Equal(Expect(2, 1, 0, 2, during, microseconds, 0), MembershipOf(Build(SubjectLostThenAssigned(), WitnessAfterTakeover(takeover))));

    [Theory]
    [InlineData("00:06:00", false, null)]
    [InlineData("00:05:59.9999999", true, 239999999)]
    public void Given_AWitnessRowAtTheEdgeOfB_When_Judged_Then_OnlyARowBeforeBCountsAsAssignedAndTakeover(string time, bool counted, int? microseconds)
    {
        ConsumerRow[] witness = [Event("witness", "assigned", time, AllPartitions)];
        Assert.Equal(Expect(1, 0, 0, counted ? 1 : 0, false, microseconds, 0), MembershipOf(Build([], witness, mode: "pause")));
    }

    [Theory]
    [InlineData("00:06:00", 1)]
    [InlineData("00:05:59.9999999", 2)]
    public void Given_NoAssignedRowAfterLostAndACommitAtTheEdgeOfB_When_Judged_Then_OnlyARowBeforeBIsTheLastCounted(string finalCommit, int entries)
    {
        ConsumerRow[] subject = [SubjectLostThenAssigned()[0], Commit("subject", "00:03:35", 0, (3, 8, 0))];
        Assert.Equal(Expect(2, 1, 0, 2, true, 61000000, entries), MembershipOf(Build(subject, WitnessAfterTakeover("00:03:01"), subjectFinalCommit: finalCommit)));
    }

    [Theory]
    [InlineData("00:00:00", "00:00:05")]
    [InlineData("00:00:05", "00:00:00")]
    public void Given_EitherMemberRegisteringFirst_O10_When_Judged_Then_BIsTheEarlierRegistrationPlusTheWindow(string subjectRegistered, string witnessRegistered)
    {
        ConsumerRow[] witness = [Event("witness", "assigned", "00:06:01", AllPartitions)];
        Assert.Equal(Expect(1, 0, 0, 0, false, null, 0), MembershipOf(Build(
            [Event("subject", "revoked", "00:06:00", SubjectPartitions)], witness, subjectRegistered, witnessRegistered, "pause")));
        Assert.Equal(Expect(1, 0, 1, 0, false, null, 0), MembershipOf(Build(
            [Event("subject", "revoked", "00:05:59.9999999", SubjectPartitions)], witness, subjectRegistered, witnessRegistered, "pause")));
    }

    [Theory]
    [InlineData("00:02:44.9999999", 44999999)]
    [InlineData("00:02:45", 45000000)]
    public void Given_ATakeoverWithAFractionOfAMicrosecond_O11_When_Judged_Then_TheTimeIsRoundedDown(string takeover, long microseconds) =>
        Assert.Equal(Expect(2, 1, 0, 2, true, microseconds, 0), MembershipOf(Build(SubjectLostThenAssigned(), WitnessAfterTakeover(takeover))));

    [Theory]
    [InlineData("subject-none")]
    [InlineData("subject-two")]
    [InlineData("witness-one")]
    public void Given_AWrongNumberOfSlowRows_O7_When_Judged_Then_TheBlockIsInvalidWithoutMembership(string change)
    {
        ConsumerJudgementRequest request = Build(SubjectLostThenAssigned(), WitnessAfterTakeover("00:03:01"));
        request = change switch
        {
            "subject-none" => F.SetMember(request, "subject", m => F.WithRows(m, m.Rows.Select(r => r with { Slow = false }))),
            "subject-two" => F.SetMember(request, "subject", m => F.WithRows(m, m.Rows.Select(r => r.Kind == "processed" && r.Offset == 9 ? r with { Slow = true } : r))),
            _ => F.SetMember(request, "witness", m => F.WithRows(m, m.Rows.Select(r => r.Kind == "processed" && r.Partition == 3 ? r with { Slow = true } : r)))
        };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, Read());
        Assert.Equal("invalid", result.Consumer.GetProperty("status").GetString());
        Assert.Equal(OnlySlowReason, result.Consumer.GetProperty("reasons").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(result.Consumer.TryGetProperty("membership", out _));
        Assert.False(result.Consumer.TryGetProperty("counts", out _));
    }

    [Fact]
    public void Given_APlanWithoutASlowMessageMode_O8_When_Judged_Then_TheBlockHasNoMembershipAndNoSlowCheck()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(F.Create(), F.Read());
        Assert.Equal("complete_valid", result.Consumer.GetProperty("status").GetString());
        Assert.False(result.Consumer.TryGetProperty("membership", out _));
    }

    [Fact]
    public void Given_ACommittedOffsetReadThatFails_O9_When_Judged_Then_TheBlockIsIncompleteWithoutCountsOrMembership()
    {
        var read = new ConsumerOffsetRead("kafka_exception", [3, 20], T("00:10:00"), T("00:10:01"), 1, []);
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(Build(SubjectLostThenAssigned(), WitnessAfterTakeover("00:03:01")), read);
        Assert.Equal("incomplete", result.Consumer.GetProperty("status").GetString());
        Assert.False(result.Consumer.TryGetProperty("counts", out _));
        Assert.False(result.Consumer.TryGetProperty("membership", out _));
    }

    [Fact]
    public void Given_ThePreflightPass_When_Judged_Then_NoMembershipIsWritten()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(Build(SubjectLostThenAssigned(), WitnessAfterTakeover("00:03:01")),
            new ConsumerOffsetRead("not_attempted", [3, 20], null, null, null, []), final: false);
        Assert.False(result.Consumer.TryGetProperty("membership", out JsonElement _));
    }
}
