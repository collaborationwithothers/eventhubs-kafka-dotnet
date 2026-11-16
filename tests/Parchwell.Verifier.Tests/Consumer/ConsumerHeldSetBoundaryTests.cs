using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;
using F = Parchwell.Verifier.Tests.Consumer.ConsumerJudgementFixture;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
// ADR-022 applies partition removal both at the start line and at the subject's last durable row.
public sealed class ConsumerHeldSetBoundaryTests
{
    [Theory]
    [InlineData("revoked")]
    [InlineData("lost")]
    public void Given_PartitionTransferredBeforeStart_U5_When_HeldSetsAreJudged_Then_RemovalPreventsDoubleAssignment(string removedEvent)
    {
        ConsumerJudgementRequest request = TwoEmptyPartitions();
        request = F.SetMember(request, "subject", member => F.WithRows(member,
            [F.Assigned("subject", 0, [0, 1]), Removal(removedEvent)]));
        request = F.SetMember(request, "witness", member => F.WithRows(member,
            [F.Assigned("witness", 0, [1])]));
        request = request with
        {
            Snapshot = request.Snapshot! with
            {
                Members = [new("subject", "subject", 1, 1), new("witness", "witness", 0, 0)]
            }
        };

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, EmptyRead());

        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Assert.Empty(result.Consumer.GetProperty("reasons").EnumerateArray());
        Assert.Equal([0], ConsumerJudgement.Held(request.Artifacts.Members[0], 1).Order());
        Assert.Equal([1], ConsumerJudgement.Held(request.Artifacts.Members[1], 0).Order());
        Assert.True(result.Consumer.TryGetProperty("counts", out _));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("lost")]
    public void Given_SubjectHeldSetShrinksBeforeKill_U14_When_WitnessGetsRemainingPartition_Then_ReassignmentUsesNarrowedHeldSet(string removedEvent)
    {
        ConsumerJudgementRequest request = TwoEmptyPartitions();
        DateTimeOffset stopped = F.Time.AddSeconds(5);
        request = request with { Plan = request.Plan with { KillCount = 1 } };
        request = F.SetMember(request, "subject", member => F.WithRows(member,
            [F.Assigned("subject", 0, [0, 1]), Removal(removedEvent), F.Kill(time: stopped)], false));
        request = F.SetMember(request, "witness", member => F.WithRows(member,
            [F.Assigned("witness", 0, []), F.Assigned("witness", 1, [0], stopped.AddMilliseconds(1))]));

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, EmptyRead());

        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Assert.Empty(result.Consumer.GetProperty("reasons").EnumerateArray());
        Assert.Equal([0], ConsumerJudgement.Held(request.Artifacts.Members[0], 2).Order());
        Assert.Equal(1000, result.Consumer.GetProperty("reassignment").GetProperty("microseconds").GetInt64());
        Assert.Null(result.Consumer.GetProperty("reassignment").GetProperty("unavailable_reason").GetString());
    }

    private static ConsumerRow Removal(string kind) => new("partition_event", "subject", 1,
        F.Time.AddSeconds(1), Event: kind, Partitions: [1]);

    private static ConsumerJudgementRequest TwoEmptyPartitions()
    {
        ConsumerJudgementRequest request = F.Empty();
        return request with
        {
            Window = request.Window with
            {
                RecordedStartHighs = [new PartitionHigh(0, 100), new PartitionHigh(1, 100)],
                EndHighs = new Dictionary<int, long> { [0] = 100, [1] = 100 },
                Partitions = [new PartitionWindow(0, 100, 100), new PartitionWindow(1, 100, 100)]
            }
        };
    }

    private static ConsumerOffsetRead EmptyRead() => F.Read(-1001) with
    {
        RequestedPartitions = [0, 1], Offsets = [new(0, -1001, 0), new(1, -1001, 0)]
    };
}
#pragma warning restore CA1707
