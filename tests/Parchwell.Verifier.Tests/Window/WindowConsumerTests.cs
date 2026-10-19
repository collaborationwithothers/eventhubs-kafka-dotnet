using System.Reflection;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

public sealed class WindowConsumerTests
{
    private static readonly int[] CompletedChecks = [9, 10];
    private static readonly int[] PendingPartitions = [0, 2];
    private static readonly long[] IncludedOffsets = [5, 6];
    [Fact]
    public void Given_HalfOpenWindow_When_Draining_Then_OnlyIncludedOffsetsAreDecodedAndBudgeted()
    {
        var (consumer, fake) = Create();
        foreach (long offset in new long[] { 4, 5, 6, 7 })
        {
            var record = MessageIdentityReaderTests.Record(offset: offset);
            record.Partition = new Partition(0);
            if (offset is 4 or 7) { record.Message.Key = [0xff]; record.Message.Headers = null; record.Message.Value = new byte[10_000]; }
            fake.Polls.Enqueue(new(record, offset == 7 ? 7 : 5, 1));
        }
        var budget = new ConsumedRecordBudget(2, 1000);
        WindowDrainResult result = Drain(consumer, fake, budget: budget);
        Assert.Equal(IncludedOffsets, result.Records.Select(r => r.Offset));
        Assert.All(result.Records, r => Assert.Empty(r.Problems));
        Assert.Equal(2, budget.Records);
        Assert.Equal(4, fake.ConsumeCalls);
        Assert.Equal(new TopicPartitionOffset("document-events", new Partition(0), new Offset(5)), Assert.Single(fake.Assigned));
        AssertComplete(result);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public void Given_PositionAroundEnd_When_Observing_Then_PositionDeterminesCompletion(long position, bool complete)
    {
        var (consumer, fake) = Create();
        fake.Polls.Enqueue(new(null, position, 1000));
        var result = Drain(consumer, fake);
        if (complete) AssertComplete(result);
        else AssertStopped(result);
        Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Given_FinalPositionAroundSafetyDeadline_When_Observing_Then_LateCompletionIsRejected(long milliseconds, bool complete)
    {
        var (consumer, fake) = Create();
        fake.Polls.Enqueue(new(null, 7, milliseconds));
        WindowDrainResult result = Drain(consumer, fake);
        if (complete)
        {
            AssertComplete(result);
            Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), result.DrainDuration);
        }
        else AssertStopped(result);
        Assert.Single(fake.Timeouts);
    }

    [Theory]
    [InlineData(999, 1)]
    [InlineData(1000, 0)]
    [InlineData(1001, 0)]
    public void Given_PendingWindowAroundDeadline_When_Observing_Then_NoPollStartsAtOrAfterDeadline(long elapsed, int polls)
    {
        var (consumer, fake) = Create();
        fake.Clock.Milliseconds = elapsed;
        WindowDrainResult result = Drain(consumer, fake);
        AssertStopped(result);
        Assert.Equal(polls, fake.ConsumeCalls);
        if (polls != 0) Assert.Equal(TimeSpan.FromMilliseconds(1), Assert.Single(fake.Timeouts));
    }

    [Fact]
    public void Given_EmptyPollBelowEnd_When_Draining_Then_RemainsPendingUntilSafetyStop()
    {
        var (consumer, fake) = Create();
        fake.Polls.Enqueue(new(null, 6, 999));
        WindowDrainResult result = Drain(consumer, fake);
        AssertStopped(result);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), result.StopDuration);
        Assert.Equal(2, fake.ConsumeCalls);
    }

    [Fact]
    public void Given_NoncontiguousRecordAndEndPosition_When_Draining_Then_DeliveredCountDoesNotControlCompletion()
    {
        var (consumer, fake) = Create();
        var record = MessageIdentityReaderTests.Record(offset: 5);
        record.Partition = new Partition(0);
        fake.Polls.Enqueue(new(record, 7, 1));
        WindowDrainResult result = Drain(consumer, fake);
        Assert.Single(result.Records);
        AssertComplete(result);
    }

    [Fact]
    public void Given_ForeignTopicAndUnassignedPartition_When_Draining_Then_RecordsAreExcludedBeforeBudgetAndDecode()
    {
        var (consumer, fake) = Create();
        var foreignTopic = MessageIdentityReaderTests.Record();
        foreignTopic.Topic = "other-topic";
        var foreignPartition = MessageIdentityReaderTests.Record();
        foreignPartition.Partition = new Partition(42);
        fake.Polls.Enqueue(new(foreignTopic, 5, 1));
        fake.Polls.Enqueue(new(foreignPartition, 7, 1));
        var budget = new ConsumedRecordBudget(1, 1);
        var result = Drain(consumer, fake, budget: budget);
        AssertComplete(result);
        Assert.Empty(result.Records);
        Assert.Equal(0, budget.Records);
    }

    [Theory]
    [InlineData(99, 99)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    public void Given_PollSliceAroundRemainingSafety_When_Draining_Then_NativeTimeoutUsesSmallerDuration(long slice, long expected)
    {
        var (consumer, fake) = Create();
        fake.Clock.Milliseconds = 900;
        fake.Polls.Enqueue(new(null, 7, 1));
        var result = WindowConsumer.Drain(consumer, Window(), fake.Clock, 0, TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(slice), new(10, 1000), CancellationToken.None);
        AssertComplete(result);
        Assert.Equal(TimeSpan.FromMilliseconds(expected), Assert.Single(fake.Timeouts));
        Assert.Equal(TimeSpan.FromMilliseconds(901), result.DrainDuration);
    }

    [Fact]
    public void Given_SeveralPartitionsIncludingEmpty_When_Draining_Then_AssignsOnlyPendingAndRequiresEveryPosition()
    {
        var (consumer, fake) = Create();
        var window = RunWindowTests.Create([new(0, 5), new(1, 8), new(2, 0)],
            new Dictionary<int, long> { [0] = 7, [1] = 8, [2] = 7 });
        fake.Polls.Enqueue(new(null, 6, 1));
        fake.Polls.Enqueue(new(null, 7, 1));
        AssertComplete(Drain(consumer, fake, window));
        Assert.Equal(PendingPartitions, fake.Assigned.Select(a => a.Partition.Value));
        Assert.Equal(2, fake.ConsumeCalls);
    }

    [Fact]
    public void Given_EqualStartAndEnd_When_Draining_Then_CompletesWithoutAssignmentOrPoll()
    {
        var (consumer, fake) = Create();
        WindowDrainResult result = Drain(consumer, fake, RunWindowTests.Create([new(0, 5)], new Dictionary<int, long> { [0] = 5 }));
        AssertComplete(result);
        Assert.Empty(result.Records);
        Assert.Equal(0, fake.AssignCalls);
        Assert.Equal(0, fake.ConsumeCalls);
        Assert.Equal(TimeSpan.Zero, result.DrainDuration);
    }

    [Theory]
    [InlineData("regression")]
    [InlineData("invalid")]
    [InlineData("noend")]
    public void Given_UnassignableWindow_When_Draining_Then_NoPassOrDurationIsManufactured(string kind)
    {
        var (consumer, fake) = Create();
        RunWindow window = kind switch
        {
            "regression" => RunWindowTests.Create([new(0, 5)], new Dictionary<int, long> { [0] = 4 }),
            "invalid" => RunWindowTests.Create([], new Dictionary<int, long> { [0] = 6 }),
            _ => RunWindow.Create(ParseResult<Parchwell.Verifier.Common.Window.StartLineNote>.Success(RunWindowTests.Note()), RunWindowTests.Identity)
        };
        var result = Drain(consumer, fake, window);
        Assert.Empty(result.Assessments);
        Assert.Null(result.DrainDuration);
        Assert.Null(result.StopDuration);
        Assert.Equal(0, fake.AssignCalls);
        Assert.Equal(0, fake.ConsumeCalls);
    }

    [Theory]
    [InlineData(ErrorCode.Local_AutoOffsetReset)]
    [InlineData(ErrorCode.OffsetOutOfRange)]
    public void Given_NativeOffsetError_When_Draining_Then_OnlyCheckNineFailsAndDurationsAreAbsent(ErrorCode code)
    {
        var (consumer, fake) = Create();
        fake.PollError = new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(code));
        var result = Drain(consumer, fake);
        Assert.Equal(9, Assert.Single(result.Assessments).CheckNumber);
        Assert.Equal(AssessmentClassification.Invalid, result.Assessments[0].Classification);
        Assert.Null(result.DrainDuration);
        Assert.Null(result.StopDuration);
        Assert.Equal(1, fake.AssignCalls);
    }

    [Fact]
    public void Given_UnrelatedNativeFailure_When_Draining_Then_DoesNotBecomeOffsetFailure()
    {
        var (consumer, fake) = Create();
        fake.PollError = new KafkaException(new Error(ErrorCode.Local_Transport));
        Assert.Throws<KafkaException>(() => Drain(consumer, fake));
    }

    [Theory]
    [InlineData(false, 6, false)]
    [InlineData(false, 7, true)]
    [InlineData(true, 6, false)]
    [InlineData(true, 7, true)]
    public void Given_RefreshCancelledAtSafety_When_PollThrows_Then_FinalPositionStillDecidesEquality(bool native, long position, bool complete)
    {
        var (consumer, fake) = Create();
        using var safety = new CancellationTokenSource();
        fake.PollError = native ? new KafkaException(new Error(ErrorCode.Local_Authentication)) : new OperationCanceledException();
        fake.BeforePollError = () => { fake.Clock.Milliseconds = 1000; fake.Position = position; safety.Cancel(); };
        var result = WindowConsumer.Drain(consumer, Window(), fake.Clock, 0, TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000), new(10, 1000), TestContext.Current.CancellationToken, safety.Token);
        if (complete) AssertComplete(result);
        else AssertStopped(result);
        Assert.Equal(1, fake.ConsumeCalls);
    }

    [Fact]
    public void Given_UnrelatedCancellation_When_Draining_Then_ItDoesNotBecomeSafetyFailure()
    {
        var (consumer, fake) = Create();
        fake.PollError = new OperationCanceledException();
        Assert.Throws<OperationCanceledException>(() => Drain(consumer, fake));
    }

    [Fact]
    public void Given_OffsetErrorAtSafety_When_Draining_Then_InvalidNineTakesPrecedence()
    {
        var (consumer, fake) = Create();
        using var safety = new CancellationTokenSource();
        fake.PollError = new KafkaException(new Error(ErrorCode.Local_AutoOffsetReset));
        fake.BeforePollError = () => { fake.Clock.Milliseconds = 1000; safety.Cancel(); };
        var result = WindowConsumer.Drain(consumer, Window(), fake.Clock, 0, TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000), new(10, 1000), TestContext.Current.CancellationToken, safety.Token);
        Assert.Equal(9, Assert.Single(result.Assessments).CheckNumber);
        Assert.Null(result.DrainDuration);
        Assert.Null(result.StopDuration);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_IncludedMalformedRecordOverBudget_When_Draining_Then_ExecutionFailurePropagates(bool recordLimit)
    {
        var (consumer, fake) = Create();
        for (int i = 0; i < 2; i++)
        {
            var record = MessageIdentityReaderTests.Record(offset: 5 + i);
            record.Partition = new Partition(0);
            record.Message.Headers = null;
            fake.Polls.Enqueue(new(record, 5, 1));
        }
        var budget = new ConsumedRecordBudget(recordLimit ? 1 : 10, recordLimit ? 100 : 2);
        Assert.Throws<InputLimitExceededException>(() => Drain(consumer, fake, budget: budget));
        Assert.Equal(recordLimit ? 1 : 0, budget.Records);
        Assert.Equal(recordLimit ? 3 : 0, budget.Bytes);
    }

    [Fact]
    public void Given_CancelledWork_When_Draining_Then_CancellationPropagates()
    {
        var (consumer, fake) = Create();
        Assert.Throws<OperationCanceledException>(() => WindowConsumer.Drain(consumer, Window(), fake.Clock, 0,
            TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000), new(10, 1000), new CancellationToken(true)));
    }

    private static RunWindow Window() => RunWindowTests.Create([new(0, 5)], new Dictionary<int, long> { [0] = 7 });
    private static WindowDrainResult Drain(IConsumer<byte[], byte[]> consumer, ConsumerProxy fake,
        RunWindow? window = null, ConsumedRecordBudget? budget = null) => WindowConsumer.Drain(consumer, window ?? Window(),
            fake.Clock, 0, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000), budget ?? new(100, 10000), CancellationToken.None);

    private static void AssertComplete(WindowDrainResult result)
    {
        Assert.Equal(CompletedChecks, result.Assessments.Select(a => a.CheckNumber));
        Assert.All(result.Assessments, a => Assert.Equal(AssessmentDisposition.Pass, a.Disposition));
        Assert.NotNull(result.DrainDuration);
        Assert.Null(result.StopDuration);
    }

    private static void AssertStopped(WindowDrainResult result)
    {
        Assert.Contains(result.Assessments, a => a.CheckNumber == 10 && a.Disposition == AssessmentDisposition.Fail && a.Classification == AssessmentClassification.Incomplete);
        Assert.Null(result.DrainDuration);
        Assert.NotNull(result.StopDuration);
    }

    private static (IConsumer<byte[], byte[]> Consumer, ConsumerProxy Proxy) Create()
    {
        var consumer = DispatchProxy.Create<IConsumer<byte[], byte[]>, ConsumerProxy>();
        return (consumer, (ConsumerProxy)(object)consumer);
    }

    public class ConsumerProxy : DispatchProxy
    {
        internal SyntheticClock Clock { get; } = new();
        internal Queue<Poll> Polls { get; } = new();
        internal List<TopicPartitionOffset> Assigned { get; } = [];
        internal List<TimeSpan> Timeouts { get; } = [];
        internal Exception? PollError { get; set; }
        internal Action? BeforePollError { get; set; }
        internal int AssignCalls { get; private set; }
        internal int ConsumeCalls { get; private set; }
        internal long Position { get; set; } = 5;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Assign":
                    AssignCalls++;
                    Assigned.AddRange((IEnumerable<TopicPartitionOffset>)args![0]!);
                    return null;
                case "Position": return new Offset(Position);
                case "Consume":
                    ConsumeCalls++;
                    var timeout = (TimeSpan)args![0]!;
                    Timeouts.Add(timeout);
                    if (PollError is { } exception) { BeforePollError?.Invoke(); throw exception; }
                    if (Polls.TryDequeue(out Poll? poll))
                    {
                        Clock.Milliseconds += poll.ElapsedMilliseconds;
                        Position = poll.Position;
                        return poll.Record;
                    }
                    Clock.Milliseconds += (long)timeout.TotalMilliseconds;
                    return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }

    internal sealed record Poll(ConsumeResult<byte[], byte[]>? Record, long Position, long ElapsedMilliseconds);
    internal sealed class SyntheticClock : TimeProvider
    {
        internal long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
