using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;
using F = Parchwell.Verifier.Tests.Consumer.ConsumerJudgementFixture;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
// ADR-022 counts require complete producer evidence, durable stop evidence and one corroborated broker observation.
public sealed class ConsumerJudgementTests
{
    private static readonly string[] MemberRoles = ["subject", "witness"];
    private static readonly int[] RequestedPartitions = [0];
    [Fact]
    public void Given_ConflictingCallbackOrders_D1_When_Judged_Then_ObservedBrokerValueWinsInBothOrders()
    {
        foreach (bool reversed in new[] { false, true })
        {
            ConsumerJudgementRequest request = F.Create();
            request = F.SetMember(request, "subject", member => PlaceCommit(member, 115, reversed ? 2 : 90, reversed ? F.Time : F.Time.AddSeconds(9)));
            request = F.SetMember(request, "witness", member => PlaceCommit(member, 110, reversed ? 90 : 2, reversed ? F.Time.AddSeconds(9) : F.Time));
            request = Rename(request, reversed ? "a" : "z", reversed ? "z" : "a");
            if (reversed) request = request with { Artifacts = request.Artifacts with { Members = request.Artifacts.Members.Reverse().ToArray() } };
            ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
            Complete(result);
            Assert.Equal(110, result.Consumer.GetProperty("partitions")[0].GetProperty("observed").GetInt64());
        }
    }

    [Theory]
    [InlineData(109, "incomplete")]
    [InlineData(110, "complete_valid")]
    [InlineData(111, "complete_valid")]
    public void Given_FrontierBoundary_D2_When_Judged_Then_EndOffsetDeterminesCompleteness(long offset, string status)
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(F.SetReports(F.Create(), offset), F.Read(offset));
        Assert.Equal(status, result.Consumer.GetProperty("status").GetString());
        if (offset == 109) Reasons(result, "consumer_commit_frontier_short"); else Complete(result);
    }

    [Fact]
    public void Given_EqualCommitEntries_D3_When_Judged_Then_NoMemberTieBreakIsNeeded() => Complete(ConsumerJudgement.Evaluate(F.Create(), F.Read()));

    [Theory]
    [InlineData(-1, -1, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, true)]
    public void Given_FailedOrSuccessfulCallbackEntry_D4_When_Judged_Then_OnlyCompleteSuccessCorroborates(int rowError, int entryError, bool valid)
    {
        ConsumerJudgementRequest request = F.SetReports(F.Create(), 109);
        if (rowError >= 0) request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(F.Commit("subject", 110, rowError: rowError, entryError: entryError))));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (valid) Complete(result); else Invalid(result, "consumer_commit_record_disagreement");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Given_NegativeCallbackError_D4_When_Judged_Then_NonzeroErrorsNeverCorroborate(int rowError, int entryError)
    {
        ConsumerJudgementRequest request = F.SetReports(F.Create(), 109);
        request = F.SetMember(request, "subject", member => F.WithRows(member,
            member.Rows.Append(F.Commit("subject", 110, rowError: rowError, entryError: entryError))));

        Invalid(ConsumerJudgement.Evaluate(request, F.Read()), "consumer_commit_record_disagreement");
    }

    [Theory]
    [InlineData(-1, -1001)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Given_SuccessfulEntryOffsetBoundary_D7_When_Judged_Then_OnlyNonnegativeEntriesCorroborate(long reported, long observed)
    {
        ConsumerJudgementRequest request = F.SetReports(F.Empty(), reported);

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read(observed));

        Complete(result);
        Assert.Equal(observed, result.Consumer.GetProperty("partitions")[0].GetProperty("observed").GetInt64());
    }

    [Fact]
    public void Given_UnsetCommitForNonemptyWindow_D5_When_Judged_Then_FrontierIsIncomplete() => Incomplete(ConsumerJudgement.Evaluate(F.SetReports(F.Create()), F.Read(-1001)), "consumer_commit_frontier_short");

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Given_EmptyWindowWithoutCommit_D6_When_Judged_Then_UnsetIsComplete(long start) => Complete(ConsumerJudgement.Evaluate(F.Empty(start), F.Read(-1001)));

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    [InlineData(101)]
    public void Given_EmptyWindowWithCorroboratedCommit_D7_When_Judged_Then_AnyConcreteFrontierIsComplete(long offset) => Complete(ConsumerJudgement.Evaluate(F.SetReports(F.Empty(), offset), F.Read(offset)));

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, -1001)]
    [InlineData(true, 101)]
    public void Given_EmptyWindowWithUncorroboratedCommit_D8_When_Judged_Then_RecordDisagreementIsInvalid(bool report, long observed) => Invalid(ConsumerJudgement.Evaluate(F.SetReports(F.Empty(), report ? [100] : []), F.Read(observed)), "consumer_commit_record_disagreement");

    [Theory]
    [InlineData("kafka_exception", "Local_TimedOut", false)]
    [InlineData("kafka_exception", "authorization", false)]
    [InlineData("invalid_response", "missing", false)]
    [InlineData("invalid_response", "duplicate", false)]
    [InlineData("invalid_response", "other_topic", false)]
    [InlineData("partition_exception", "partition", false)]
    [InlineData("kafka_exception", "Local_TimedOut", true)]
    [InlineData("kafka_exception", "authorization", true)]
    [InlineData("invalid_response", "missing", true)]
    [InlineData("invalid_response", "duplicate", true)]
    [InlineData("invalid_response", "other_topic", true)]
    [InlineData("partition_exception", "partition", true)]
    public void Given_UnavailableOffsetEvidence_D9_When_Judged_Then_NoCountsAreAuthorized(string outcome, string failure, bool empty)
    {
        ConsumerJudgementRequest request = empty ? F.Empty() : F.Create();
        IReadOnlyList<ConsumerObservedOffset> offsets = failure == "duplicate" ? [new(0, 110, 0), new(0, 110, 0)] : [];
        var read = new ConsumerOffsetRead(outcome, [0], F.Time, F.Time.AddSeconds(1), failure == "authorization" ? 30 : -185, offsets);
        Incomplete(ConsumerJudgement.Evaluate(request, read), "consumer_commit_evidence_unavailable");
    }

    [Fact]
    public void Given_OtherNegativeOffset_D10_When_Judged_Then_DisagreementIsInvalid() => Invalid(ConsumerJudgement.Evaluate(F.SetReports(F.Create()), F.Read(-2)), "consumer_commit_record_disagreement");

    [Theory]
    [InlineData(-1001, true)]
    [InlineData(0, false)]
    public void Given_NonemptyAndEmptyPartitions_D11_When_Judged_Then_EachRequiresIndependentAgreement(long second, bool valid)
    {
        ConsumerJudgementRequest request = F.Create();
        request = request with { Window = request.Window with { RecordedStartHighs = [new PartitionHigh(0, 100), new PartitionHigh(1, 50)], EndHighs = new Dictionary<int,long> { [0] = 110, [1] = 50 }, Partitions = [new PartitionWindow(0, 100, 110), new PartitionWindow(1, 50, 50)] } };
        request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Select(row => row.Kind == "partition_event" ? row with { Partitions = [0, 1] } : row)));
        ConsumerOffsetRead read = F.Read() with { RequestedPartitions = [0, 1], Offsets = [new(0, 110, 0), new(1, second, 0)] };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, read);
        if (valid) Complete(result); else Invalid(result, "consumer_commit_record_disagreement");
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Given_ReadEndsAtDeadlineBoundary_D12_When_Judged_Then_EqualityMeetsDeadline(int ticks, bool valid)
    {
        ConsumerJudgementRequest request = F.Create();
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read(end: request.DeadlineInstant.AddTicks(ticks)));
        if (valid) Complete(result); else Incomplete(result, "consumer_completion_deadline");
    }

    [Fact]
    public void Given_SequenceGapOrShortFrontier_D13_When_Judged_Then_InvalidityPrecedesReadFailure()
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "subject", member => member with { RecordInvalid = true, Rows = member.Rows.Select(row => row.RowSequence == 2 ? row with { RowSequence = 3 } : row).ToArray() });
        ConsumerOffsetRead read = F.Read() with { Outcome = "kafka_exception", Offsets = [], ErrorCode = -185 };
        Invalid(ConsumerJudgement.Evaluate(request, read), "consumer_record_invalid");
        Incomplete(ConsumerJudgement.Evaluate(F.SetReports(F.Create(), 109), F.Read(109)), "consumer_commit_frontier_short");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExplicitSuccessfulCommit_D15_When_Judged_Then_ReturnedEntriesJoinCallbacksAndEmptyAddsNothing(bool empty)
    {
        ConsumerJudgementRequest request = F.SetReports(F.Create(), empty ? [109, 110] : [109]);
        var submitted = empty ? Array.Empty<ConsumerCommitOffset>() : [new ConsumerCommitOffset("document-events", 0, 110)];
        var explicitRow = new ConsumerRow("commit", "subject", 0, F.Time, Source: "explicit", Outcome: "returned", Submitted: submitted, Offsets: submitted);
        request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(explicitRow)));
        Complete(ConsumerJudgement.Evaluate(request, F.Read()));
    }

    [Fact]
    public void Given_OnlyEarlierSuccessfulCommit_D16_When_Judged_Then_LossIsNotInvented()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(F.SetReports(F.Create(), 109), F.Read());
        Invalid(result, "consumer_commit_record_disagreement");
        Assert.DoesNotContain("committed_never_processed", result.Consumer.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Given_RegistrationCount_U3_When_Judged_Then_ExactlyTwoMembersAuthorizeRead(int count)
    {
        ConsumerJudgementRequest request = F.Create();
        ConsumerMember[] members = Enumerable.Range(0, count).Select(index => request.Artifacts.Members[index % 2]).ToArray();
        request = request with { Artifacts = request.Artifacts with { Members = members, RegistrationCount = count } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, count == 2 ? F.Read() : F.NotAttempted());
        Assert.Equal(count == 2, result.ShouldRead);
        if (count == 2) Complete(result); else Invalid(result, "consumer_registration_invalid");
    }

    [Theory]
    [InlineData("two_subjects")]
    [InlineData("repeat")]
    [InlineData("group")]
    [InlineData("observer")]
    [InlineData("prefix")]
    [InlineData("instance")]
    [InlineData("run")]
    public void Given_RegistrationIdentityMismatch_U3_When_Judged_Then_ReadIsSkipped(string fault)
    {
        ConsumerJudgementRequest request = F.Create();
        request = F.SetMember(request, fault == "two_subjects" ? "witness" : "subject", member =>
        {
            ConsumerRegistration r = member.Registration!;
            r = fault switch { "two_subjects" => r with { Role = "subject" }, "repeat" => r with { RepeatNumber = 2 }, "group" => r with { GroupId = r.GroupId + "x" }, "observer" => r with { Role = "observer" }, "instance" => r with { InstanceId = "other" }, "run" => r with { RunId = "other" }, _ => r };
            return member with { Registration = r, RegistrationInvalid = fault is "prefix" or "observer" or "instance" };
        });
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.NotAttempted());
        Invalid(result, "consumer_registration_invalid");
        Assert.False(result.ShouldRead);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void Given_PartitionEventAtSnapshotBoundary_U5_When_Judged_Then_OnlyOpenClosedReadIntervalInvalidates(int sequence, bool valid)
    {
        ConsumerJudgementRequest request = F.Create();
        request = F.SetMember(request, "witness", member => F.WithRows(member, member.Rows.Select(row => row.RowSequence == sequence ? F.Assigned("witness", sequence, []) : row)));
        request = request with { Snapshot = request.Snapshot! with { Members = [new("subject", "subject", 0, 0), new("witness", "witness", 3, 5)] } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (valid) Complete(result); else Invalid(result, "consumer_start_assignment_invalid");
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("neither", false)]
    [InlineData("both", false)]
    [InlineData("minus_one", false)]
    [InlineData("empty_assignment", true)]
    [InlineData("unregistered", false)]
    public void Given_InvalidOrEmptyStartAssignment_U5_When_Judged_Then_ExactCoverageAndPriorAssignmentAreRequired(string fault, bool valid)
    {
        ConsumerJudgementRequest request = F.Create();
        if (fault == "missing") request = request with { Snapshot = null };
        if (fault is "neither" or "both")
        {
            request = request with { Window = request.Window with { Partitions = [new(0, 100, 110), new(1, 50, 50)] } };
            if (fault == "both")
            {
                request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Select(row => row.Kind == "partition_event" ? row with { Partitions = [0,1] } : row)));
                request = F.SetMember(request, "witness", member => F.WithRows(member, member.Rows.Select(row => row.Kind == "partition_event" ? row with { Partitions = [1] } : row)));
            }
        }
        if (fault == "minus_one") request = request with { Snapshot = request.Snapshot! with { Members = [new("subject", "subject", 0, 0), new("witness", "witness", -1, -1)] } };
        if (fault == "empty_assignment") request = F.SetMember(request, "witness", member => F.WithRows(member, [F.Assigned("witness", 0, []), F.Commit("witness", 110)]));
        if (fault == "unregistered") request = request with { Snapshot = request.Snapshot! with { Members = [new("subject", "subject", 0, 0), new("other", "witness", 0, 0)] } };
        ConsumerOffsetRead read = F.Read();
        if (fault is "neither" or "both") read = read with { RequestedPartitions = [0,1], Offsets = [new(0,110,0), new(1,-1001,0)] };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, read);
        if (valid) Complete(result); else Invalid(result, "consumer_start_assignment_invalid");
    }

    [Theory]
    [InlineData("max.poll.interval.ms", "different", "subject")]
    [InlineData("extra", "added", "witness")]
    [InlineData("isolation.level", null, "subject")]
    [InlineData("client.id", "other", "subject")]
    [InlineData("group.id", "other", "subject")]
    [InlineData("bootstrap.servers", "other", "subject")]
    public void Given_OneEffectiveSettingDifference_U6_When_Judged_Then_OneDiagnosticNamesThatKey(string key, string? value, string role)
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), role, member =>
        {
            var settings = new Dictionary<string,string>(member.Registration!.EffectiveSettings, StringComparer.Ordinal);
            if (value is null) settings.Remove(key); else settings[key] = value;
            return member with { Registration = member.Registration with { EffectiveSettings = settings } };
        });
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Invalid(result, "consumer_configuration_mismatch");
        JsonElement diagnostic = Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(key, diagnostic.GetProperty("field").GetString());
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(109, true)]
    [InlineData(110, false)]
    public void Given_ProcessedOffsetAtWindowBoundary_U7_When_Judged_Then_WindowIsHalfOpen(int offset, bool valid)
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "subject", member => F.WithRows(member, member.Rows.Select(row => row.Kind == "processed" && row.Offset == 100 ? F.Processed("subject", offset) : row)));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (valid) Complete(result); else Invalid(result, "consumer_processed_outside_window");
    }

    [Theory]
    [InlineData("attempt_null")]
    [InlineData("no_ledger")]
    [InlineData("sequence")]
    [InlineData("consumed_attempt")]
    [InlineData("key")]
    public void Given_ProcessedIdentityMismatch_U7_When_Judged_Then_UnjoinedProcessingInvalidates(string fault)
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "subject", member => F.WithRows(member, member.Rows.Select(row => row.Kind == "processed" && row.Offset == 100 ? fault switch { "attempt_null" => row with { AttemptId = null }, "sequence" => row with { Sequence = row.Sequence + 1 }, "key" => row with { Key = "other" }, _ => row } : row)));
        if (fault == "no_ledger") request = request with { LedgerRows = request.LedgerRows.Where(row => row.Attempt.AttemptId != "attempt-100").ToArray() };
        if (fault == "consumed_attempt") request = request with { ConsumedRecords = request.ConsumedRecords.Select(row => row.Offset == 100 ? row with { AttemptId = "other" } : row).ToArray() };
        Invalid(ConsumerJudgement.Evaluate(request, F.Read()), "consumer_processed_unjoined");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Given_KillCountThree_U8_When_SubjectProcessedRowsVary_Then_ExactlyTwoMustPrecedeKill(int count, bool valid)
    {
        ConsumerJudgementRequest request = KillFixture(count);
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (valid) Complete(result); else Invalid(result, "consumer_stop_invalid");
    }

    [Theory]
    [InlineData("not_last")]
    [InlineData("two_kills")]
    [InlineData("kill_receipt")]
    [InlineData("no_count_kill")]
    [InlineData("subject_no_receipt")]
    [InlineData("witness_no_receipt")]
    [InlineData("witness_kill")]
    public void Given_InconsistentStopEvidence_U8_When_Judged_Then_StopIsInvalid(string fault)
    {
        ConsumerJudgementRequest request = fault is "not_last" or "two_kills" or "kill_receipt" ? KillFixture(2) : F.Create();
        if (fault == "not_last") request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(F.EmptyCommit("subject")), false));
        if (fault == "two_kills") request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(F.Kill()), false));
        if (fault == "kill_receipt") request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows));
        if (fault == "no_count_kill") request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(F.Kill())));
        if (fault == "subject_no_receipt") request = F.SetMember(request, "subject", member => member with { Completion = null });
        if (fault == "witness_no_receipt") request = F.SetMember(request, "witness", member => member with { Completion = null });
        if (fault == "witness_kill") request = F.SetMember(request, "witness", member => F.WithRows(member, member.Rows.Append(F.Kill("witness"))));
        Invalid(ConsumerJudgement.Evaluate(request, F.Read()), "consumer_stop_invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExplicitCommitExceptions_U11_When_Judged_Then_OnlySuccessfulPartitionEntriesCorroborate(bool partitionException)
    {
        ConsumerJudgementRequest request = F.SetReports(F.Create(), 109);
        var explicitRow = new ConsumerRow("commit", "subject", 0, F.Time, Source: "explicit", Outcome: partitionException ? "partition_exception" : "request_exception", ErrorCode: partitionException ? 0 : 1, ErrorReason: partitionException ? null : "test", Submitted: [new("document-events", 0, 110)], Offsets: partitionException ? [new("document-events", 0, 110)] : []);
        request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Append(explicitRow)));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (partitionException) Complete(result); else Invalid(result, "consumer_commit_record_disagreement");
    }

    [Fact]
    public void Given_MultipleInvalidReasons_U12_When_Judged_Then_OnlyInvalidNamesAppearInOrdinalOrder()
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "subject", member => member with { RecordInvalid = true, Completion = null });
        Invalid(ConsumerJudgement.Evaluate(request, F.Read()), "consumer_record_invalid", "consumer_stop_invalid");
    }

    [Fact]
    public void Given_LateReadAndShortFrontier_U12_When_Judged_Then_OnlyDeadlineReasonApplies()
    {
        ConsumerJudgementRequest request = F.SetReports(F.Create(), 109);
        Incomplete(ConsumerJudgement.Evaluate(request, F.Read(109, request.DeadlineInstant.AddTicks(1))), "consumer_completion_deadline");
    }

    [Fact]
    public void Given_DuplicatesLossAndFailedProduce_U13_When_Classified_Then_OnlyDeliveredMessagesContribute()
    {
        ConsumerJudgementRequest request = F.Create();
        request = F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Where(row => row.Kind != "processed" || row.Offset != 104).Append(F.Processed("subject", 105)).Append(F.Processed("subject", 105))));
        request = F.SetMember(request, "witness", member => F.WithRows(member, member.Rows.Append(F.Processed("witness", 103))));
        request = request with { LedgerRows = request.LedgerRows.Select(row => row.Attempt.AttemptId == "attempt-108" ? row with { Delivered = false, ErrorCode = 1 } : row).ToArray() };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        AssertCounts(result, 6, 2, 3, 1);
    }

    [Fact]
    public void Given_NoDeliveredMessages_U13b_When_Classified_Then_AllCountsAreZero()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(F.Empty(), F.Read(-1001));
        Complete(result);
        AssertCounts(result, 0, 0, 0, 0);
    }

    [Theory]
    [InlineData(-1, "n1", false, null, "no_reassignment")]
    [InlineData(0, "n1", false, 0L, null)]
    [InlineData(10000, "n1", false, 1000L, null)]
    [InlineData(10000, "n1", true, null, "no_reassignment")]
    [InlineData(10000, "n2", false, null, "different_nodes")]
    public void Given_ReassignmentBoundary_U14_When_Judged_Then_TimingNeedsSameNodeAndFullHeldSet(long ticks, string witnessNode, bool missingPartition, long? microseconds, string? unavailable)
    {
        ConsumerJudgementRequest request = KillFixture(2);
        DateTimeOffset stopped = F.Time.AddSeconds(5);
        request = F.SetMember(request, "witness", member => F.WithRows(member with { Registration = member.Registration! with { NodeName = witnessNode } }, member.Rows.Append(F.Assigned("witness", 0, missingPartition ? [] : [0], stopped.AddTicks(ticks)))));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        JsonElement reassignment = result.Consumer.GetProperty("reassignment");
        Assert.Equal(microseconds, reassignment.GetProperty("microseconds").ValueKind == JsonValueKind.Null ? null : reassignment.GetProperty("microseconds").GetInt64());
        Assert.Equal(unavailable, reassignment.GetProperty("unavailable_reason").GetString());
    }

    [Theory]
    [InlineData("complete_valid")]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public void Given_EachConsumerStatus_U18_When_Projected_Then_BlockHasExactlyContractProperties(string status)
    {
        ConsumerJudgementRequest request = F.Create();
        ConsumerOffsetRead read = F.Read();
        if (status == "invalid") request = F.SetMember(request, "subject", member => member with { RecordInvalid = true });
        if (status == "incomplete") read = read with { Outcome = "kafka_exception", Offsets = [], ErrorCode = -185 };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, read);
        Assert.Equal(status, result.Consumer.GetProperty("status").GetString());
        string[] fields = ["schema_version", "status", "reasons", "group_id", "kill_count", "members", "offset_read", "partitions", "diagnostics"];
        if (status == "complete_valid") fields = [..fields, "counts"];
        Assert.Equal(fields.Order(StringComparer.Ordinal), result.Consumer.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, result.Consumer.GetProperty("schema_version").GetInt32());
        Assert.Equal(F.Group, result.Consumer.GetProperty("group_id").GetString());
        Assert.Equal(JsonValueKind.Null, result.Consumer.GetProperty("kill_count").ValueKind);
        JsonElement[] members = result.Consumer.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(MemberRoles, members.Select(m => m.GetProperty("role").GetString()));
        foreach (JsonElement member in members)
        {
            AssertFields(member, "role", "instance_id", "node_name", "rows", "processed_rows", "ending");
            Assert.Equal(member.GetProperty("role").GetString(), member.GetProperty("instance_id").GetString());
            Assert.Equal("n1", member.GetProperty("node_name").GetString());
            Assert.Equal(7, member.GetProperty("rows").GetInt32());
            Assert.Equal(5, member.GetProperty("processed_rows").GetInt32());
            Assert.Equal("completion_receipt", member.GetProperty("ending").GetString());
        }
        JsonElement offsetRead = result.Consumer.GetProperty("offset_read");
        AssertFields(offsetRead, "outcome", "requested_partitions", "started_utc", "ended_utc", "error_code", "offsets");
        Assert.Equal(RequestedPartitions, offsetRead.GetProperty("requested_partitions").EnumerateArray().Select(p => p.GetInt32()));
        Assert.Equal(status == "incomplete" ? "kafka_exception" : "success", offsetRead.GetProperty("outcome").GetString());
        Assert.Equal("2026-11-15T12:00:07.0000000Z", offsetRead.GetProperty("started_utc").GetString());
        Assert.Equal("2026-11-15T12:00:08.0000000Z", offsetRead.GetProperty("ended_utc").GetString());
        Assert.Equal(status == "incomplete" ? -185 : 0, offsetRead.GetProperty("error_code").GetInt32());
        Assert.Equal(status == "incomplete" ? 0 : 1, offsetRead.GetProperty("offsets").GetArrayLength());
        foreach (JsonElement offset in offsetRead.GetProperty("offsets").EnumerateArray()) AssertFields(offset, "partition", "offset", "error_code");
        JsonElement partition = Assert.Single(result.Consumer.GetProperty("partitions").EnumerateArray());
        AssertFields(partition, "partition", "start", "end", "observed");
        Assert.Equal(0, partition.GetProperty("partition").GetInt32());
        Assert.Equal(100, partition.GetProperty("start").GetInt64()); Assert.Equal(110, partition.GetProperty("end").GetInt64());
        if (status == "incomplete") Assert.Equal(JsonValueKind.Null, partition.GetProperty("observed").ValueKind); else Assert.Equal(110, partition.GetProperty("observed").GetInt64());
        Reasons(result, status == "invalid" ? ["consumer_record_invalid"] : status == "incomplete" ? ["consumer_commit_evidence_unavailable"] : []);
        foreach (JsonElement diagnostic in result.Consumer.GetProperty("diagnostics").EnumerateArray()) AssertFields(diagnostic, "reason", "member", "artifact", "row_sequence", "field", "detail");
        if (status != "complete_valid") Assert.False(result.Consumer.TryGetProperty("counts", out _));
    }

    [Fact]
    public void Given_BoundedDiagnostics_U18_When_Projected_Then_EntryAndUtf8LimitsAndRedactionApply()
    {
        ConsumerJudgementRequest request = F.Create();
        var diagnostics = new List<ConsumerDiagnostic>
        {
            new("consumer_configuration_mismatch", Field: "sasl.password", Detail: "sasl-secret"),
            new("consumer_configuration_mismatch", Field: "access.token", Detail: "token-secret"),
            new("consumer_record_invalid", Detail: new string('é', 129))
        };
        diagnostics.AddRange(Enumerable.Range(0, 64).Select(index => new ConsumerDiagnostic("consumer_record_invalid", RowSequence: index, Detail: "test")));
        request = request with { Artifacts = request.Artifacts with { Diagnostics = diagnostics } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        JsonElement[] projected = result.Consumer.GetProperty("diagnostics").EnumerateArray().ToArray();
        Assert.Equal(64, projected.Length);
        Assert.Equal("redacted", projected[0].GetProperty("detail").GetString());
        Assert.Equal("redacted", projected[1].GetProperty("detail").GetString());
        Assert.Equal(256, Encoding.UTF8.GetByteCount(projected[2].GetProperty("detail").GetString()!));
        Assert.All(projected, entry => Assert.InRange(Encoding.UTF8.GetByteCount(entry.GetProperty("detail").GetString() ?? ""), 0, 256));
        Assert.DoesNotContain("sasl-secret", result.Consumer.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", result.Consumer.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(63, 63)]
    [InlineData(64, 64)]
    [InlineData(65, 64)]
    public void Given_DiagnosticCountBoundary_U18_When_Projected_Then_AtMostSixtyFourEntriesRemain(int count, int expected)
    {
        ConsumerJudgementRequest request = F.Create();
        request = request with { Artifacts = request.Artifacts with { Diagnostics = Enumerable.Range(0, count).Select(index => new ConsumerDiagnostic("consumer_record_invalid", RowSequence: index)).ToArray() } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Assert.Equal(expected, result.Consumer.GetProperty("diagnostics").GetArrayLength());
    }

    [Theory]
    [InlineData(255, 255)]
    [InlineData(256, 256)]
    [InlineData(257, 256)]
    public void Given_DiagnosticByteBoundary_U18_When_Projected_Then_DetailIsAtMostTwoHundredFiftySixBytes(int bytes, int expected)
    {
        ConsumerJudgementRequest request = F.Create();
        request = request with { Artifacts = request.Artifacts with { Diagnostics = [new ConsumerDiagnostic("consumer_record_invalid", Detail: new string('x', bytes))] } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        string detail = Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray()).GetProperty("detail").GetString()!;
        Assert.Equal(expected, Encoding.UTF8.GetByteCount(detail));
    }

    private const string FullCoverage = """{"qualifying_partitions":[0],"uncovered_partitions":[]}""";
    private const string NoCoverage = """{"qualifying_partitions":[0],"uncovered_partitions":[0]}""";

    private static ConsumerJudgementRequest Relabel(ConsumerJudgementRequest request, string scenario)
    {
        request = request with { ScenarioId = scenario };
        foreach (string role in MemberRoles)
            request = F.SetMember(request, role, member => F.WithRows(member with { Registration = member.Registration! with { ScenarioId = scenario } }, member.Rows, member.Completion is not null));
        return request;
    }
    private static string Coverage(ConsumerJudgementResult result) => result.Consumer.GetProperty("subject_commit_coverage").GetRawText();
    private static ConsumerJudgementRequest SetSubjectCommit(ConsumerJudgementRequest request, Func<ConsumerRow, ConsumerRow>? change)
        => F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.SelectMany(row => row.Kind != "commit" ? [row] : change is null ? [] : new[] { change(row) }), false));

    [Fact]
    public void Given_C10KillBlockWithSubjectCommit_CV1_When_Judged_Then_PartitionIsQualifyingAndCovered()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(Relabel(KillFixture(2), "C10"), F.Read());
        Complete(result);
        Assert.Equal(FullCoverage, Coverage(result));
        Assert.Equal(["schema_version", "status", "reasons", "group_id", "kill_count", "members", "offset_read", "partitions", "diagnostics", "counts", "reassignment", "subject_commit_coverage"],
            result.Consumer.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Given_C10KillBlockWithoutSubjectCommit_CV2_When_Judged_Then_PartitionIsUncovered()
    {
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(SetSubjectCommit(Relabel(KillFixture(2), "C10"), null), F.Read());
        Complete(result);
        Assert.Equal(NoCoverage, Coverage(result));
    }

    [Theory]
    [InlineData("row_error")]
    [InlineData("entry_error")]
    [InlineData("negative_offset")]
    [InlineData("other_topic")]
    public void Given_SubjectCommitThatDoesNotCount_CV3_When_Judged_Then_PartitionIsUncovered(string change)
    {
        ConsumerJudgementRequest request = Relabel(KillFixture(2), "C10");
        request = SetSubjectCommit(request, row => change switch
        {
            "row_error" => F.Commit("subject", 110, rowError: 1),
            "entry_error" => F.Commit("subject", 110, entryError: 1),
            "negative_offset" => F.Commit("subject", -1001),
            _ => row with { Offsets = [new ConsumerCommitOffset("other", 0, 110, 0)] }
        });
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        Assert.Equal(NoCoverage, Coverage(result));
    }

    [Theory]
    [InlineData("offset_0", true)]
    [InlineData("offset_minus_1", false)]
    [InlineData("row_error_negative", false)]
    [InlineData("entry_error_negative", false)]
    [InlineData("empty_offsets", false)]
    public void Given_SubjectCommitAtTheBoundary_CV7_When_Judged_Then_OnlyASuccessfulEntryOfAtLeastZeroCovers(string change, bool covered)
    {
        ConsumerJudgementRequest request = SetSubjectCommit(Relabel(KillFixture(2), "C10"), row => change switch
        {
            "offset_0" => F.Commit("subject", 0),
            "offset_minus_1" => F.Commit("subject", -1),
            "row_error_negative" => F.Commit("subject", 110, rowError: -195),
            "entry_error_negative" => F.Commit("subject", 110, entryError: -195),
            _ => row with { Offsets = [] }
        });
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        Assert.Equal(covered ? FullCoverage : NoCoverage, Coverage(result));
    }

    [Fact]
    public void Given_SubjectCommitOnAnotherPartition_CV8_When_Judged_Then_PartitionZeroIsUncovered()
    {
        ConsumerJudgementRequest request = SetSubjectCommit(Relabel(KillFixture(2), "C10"), _ => F.Commit("subject", 110, partition: 1));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        Assert.Equal(NoCoverage, Coverage(result));
    }

    [Fact]
    public void Given_C10EmptyWindow_CV4_When_Judged_Then_NoPartitionQualifies()
    {
        ConsumerJudgementRequest request = Relabel(F.Empty(), "C10");
        request = request with { Plan = request.Plan with { KillCount = 1 } };
        request = F.SetMember(request, "subject", member => F.WithRows(member, [F.Assigned("subject", 0, [0]), F.Kill()], false));
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read(-1001));
        Complete(result);
        Assert.Equal("""{"qualifying_partitions":[],"uncovered_partitions":[]}""", Coverage(result));
    }

    [Fact]
    public void Given_SubjectRevokedItsPartitionBeforeTheStartLine_CV5_When_Judged_Then_TheReplayNotTheLastAssignedRowDecides()
    {
        ConsumerJudgementRequest request = Relabel(KillFixture(2), "C10");
        request = F.SetMember(request, "subject", member => F.WithRows(member,
            member.Rows.Where(row => row.Kind != "partition_event").Prepend(new ConsumerRow("partition_event", "subject", 1, F.Time, Event: "revoked", Partitions: [0])).Prepend(F.Assigned("subject", 0, [0])), false));
        request = F.SetMember(request, "witness", member => F.WithRows(member, member.Rows.Where(row => row.Kind != "partition_event").Prepend(F.Assigned("witness", 0, [0]))));
        request = request with { Snapshot = request.Snapshot! with { Members = [new("subject", "subject", 1, 1), new("witness", "witness", 0, 0)] } };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        Complete(result);
        Assert.Equal("""{"qualifying_partitions":[],"uncovered_partitions":[]}""", Coverage(result));
    }

    [Theory]
    [InlineData("C03")]
    [InlineData("C01")]
    [InlineData("C10_without_kill_count")]
    [InlineData("C10_invalid_subject_record")]
    public void Given_BlockThatIsNotACompleteC10KillBlock_CV6_When_Judged_Then_NoCoverageIsReported(string shape)
    {
        ConsumerJudgementRequest request = shape switch
        {
            "C03" => KillFixture(2),
            "C01" => Relabel(KillFixture(2), "C01"),
            "C10_without_kill_count" => Relabel(F.Create(), "C10"),
            _ => F.SetMember(Relabel(KillFixture(2), "C10"), "subject", member => member with { RecordInvalid = true })
        };
        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());
        if (shape == "C10_invalid_subject_record") Assert.Equal(VerifierResultStatus.Invalid, result.Status); else Complete(result);
        Assert.False(result.Consumer.TryGetProperty("subject_commit_coverage", out _));
    }

    private static ConsumerJudgementRequest KillFixture(int processed)
    {
        ConsumerJudgementRequest request = F.Create();
        request = request with { Plan = request.Plan with { KillCount = 3 } };
        return F.SetMember(request, "subject", member => F.WithRows(member, member.Rows.Where(row => row.Kind != "processed").Concat(Enumerable.Range(100, processed).Select(offset => F.Processed("subject", offset))).Append(F.Kill()), false));
    }
    private static ConsumerMember PlaceCommit(ConsumerMember member, long offset, int sequence, DateTimeOffset time)
    {
        var rows = member.Rows.Where(row => row.Kind != "commit").ToList();
        while (rows.Count < sequence) rows.Add(F.EmptyCommit(member.Registration!.InstanceId));
        rows.Insert(sequence, F.Commit(member.Registration!.InstanceId, offset) with { Time = time });
        return F.WithRows(member, rows);
    }
    private static ConsumerJudgementRequest Rename(ConsumerJudgementRequest request, string subject, string witness)
    {
        foreach ((string role, string name) in new[] { ("subject", subject), ("witness", witness) })
        {
            request = F.SetMember(request, role, member =>
            {
                var settings = new Dictionary<string,string>(member.Registration!.EffectiveSettings, StringComparer.Ordinal) { ["client.id"] = name };
                ConsumerRegistration registration = member.Registration with { InstanceId = name, PodName = name, EffectiveSettings = settings };
                return F.WithRows(member with { Prefix = "run1/C1/r1/consumer/" + name + "/", Registration = registration }, member.Rows);
            });
        }
        return request with { Snapshot = request.Snapshot! with { Members = [new(subject,"subject",0,0),new(witness,"witness",0,0)] } };
    }
    private static void Complete(ConsumerJudgementResult result)
    {
        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Reasons(result);
        Assert.True(result.Consumer.TryGetProperty("counts", out _));
    }
    private static void Invalid(ConsumerJudgementResult result, params string[] reasons)
    {
        Assert.Equal(VerifierResultStatus.Invalid, result.Status); Reasons(result, reasons);
        Assert.False(result.Consumer.TryGetProperty("counts", out _)); Assert.False(result.Consumer.TryGetProperty("reassignment", out _));
    }
    private static void Incomplete(ConsumerJudgementResult result, string reason)
    {
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status); Reasons(result, reason);
        Assert.False(result.Consumer.TryGetProperty("counts", out _)); Assert.False(result.Consumer.TryGetProperty("reassignment", out _));
    }
    private static void Reasons(ConsumerJudgementResult result, params string[] reasons) => Assert.Equal(reasons, result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
    private static void AssertFields(JsonElement value, params string[] fields) => Assert.Equal(fields.Order(StringComparer.Ordinal), value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    private static void AssertCounts(ConsumerJudgementResult result, long once, long again, long extra, long never)
    {
        JsonElement counts = result.Consumer.GetProperty("counts"); AssertFields(counts, "processed_once", "processed_again", "processed_again_extra_rows", "committed_never_processed");
        Assert.Equal(once, counts.GetProperty("processed_once").GetInt64()); Assert.Equal(again, counts.GetProperty("processed_again").GetInt64());
        Assert.Equal(extra, counts.GetProperty("processed_again_extra_rows").GetInt64()); Assert.Equal(never, counts.GetProperty("committed_never_processed").GetInt64());
    }
}
#pragma warning restore CA1707
