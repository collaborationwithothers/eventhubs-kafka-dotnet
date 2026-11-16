using Confluent.Kafka;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class OffsetSetupDeadlineTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ConstructionEndsAroundGlobalDeadline_When_Reading_Then_OnlyPositiveTimeStartsCommitted(int adjacentTicks)
    {
        var clock=new SetupClock();var calls=new List<string>();TimeSpan? nativeTimeout=null;
        DateTimeOffset deadline=clock.GetUtcNow().AddMilliseconds(1);
        var reader=new ConsumerCommittedOffsetReader(_=>
        {
            calls.Add("create");clock.Advance(TimeSpan.FromMilliseconds(1)+TimeSpan.FromTicks(adjacentTicks));
            return NativeProxy.Create<IConsumer<byte[],byte[]>>((method,args)=>
            {
                calls.Add(method.Name);if(method.Name=="Dispose")return null;
                Assert.Equal("Committed",method.Name);
                Assert.Equal([new TopicPartition("document-events",0)],Assert.IsAssignableFrom<IEnumerable<TopicPartition>>(args![0]));
                nativeTimeout=Assert.IsType<TimeSpan>(args[1]);
                return new List<TopicPartitionOffset>{new("document-events",0,Offset.Unset)};
            });
        },clock);
        ConsumerOffsetRead read=await reader.ReadAsync([0],deadline,TestContext.Current.CancellationToken);
        Assert.Equal([0],read.RequestedPartitions);
        Assert.Equal(1,calls.Count(call=>call=="create"));Assert.Equal(1,calls.Count(call=>call=="Dispose"));
        Assert.Equal(adjacentTicks<0?1:0,calls.Count(call=>call=="Committed"));
        if(adjacentTicks<0)
        {
            Assert.Equal("success",read.Outcome);Assert.Equal(TimeSpan.FromTicks(1),nativeTimeout);
            Assert.Equal(DateTimeOffset.UnixEpoch,read.StartedUtc);Assert.Equal(deadline.AddTicks(-1),read.EndedUtc);
            Assert.Equal(0,read.ErrorCode);Assert.Equal([new ConsumerObservedOffset(0,-1001,0)],read.Offsets);
        }
        else AssertNotAttempted(read);
        ConsumerJudgementResult result=ConsumerJudgement.Evaluate(ConsumerJudgementFixture.Empty(100) with {DeadlineInstant=deadline},read);
        Assert.Equal(adjacentTicks<0?VerifierResultStatus.CompleteValid:VerifierResultStatus.Incomplete,result.Status);
        if(adjacentTicks>=0)
        {
            Assert.Equal(["consumer_completion_deadline"],result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
            Assert.False(result.Consumer.TryGetProperty("counts",out _));
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("kafka_exception")]
    [InlineData("partition_exception")]
    [InlineData("invalid_response")]
    public async Task Given_NativeCallStartsBeforeDeadline_When_ReturningAfterDeadline_Then_PreservesItsOutcome(string outcome)
    {
        var clock=new SetupClock();var calls=new List<string>();
        DateTimeOffset deadline=clock.GetUtcNow().AddMilliseconds(1);
        var reader=new ConsumerCommittedOffsetReader(_=>NativeProxy.Create<IConsumer<byte[],byte[]>>((method,arguments)=>
        {
            calls.Add(method.Name);if(method.Name=="Dispose")return null;
            Assert.Equal("Committed",method.Name);
            Assert.Equal(TimeSpan.FromMilliseconds(1),Assert.IsType<TimeSpan>(arguments![1]));
            clock.Advance(TimeSpan.FromMilliseconds(2));
            if(outcome=="kafka_exception")throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
            if(outcome=="partition_exception")throw new TopicPartitionOffsetException([new TopicPartitionOffsetError("document-events",0,7,new Error(ErrorCode.TopicAuthorizationFailed))]);
            return outcome=="invalid_response"?new List<TopicPartitionOffset>():new List<TopicPartitionOffset>{new("document-events",0,Offset.Unset)};
        }),clock);
        ConsumerOffsetRead read=await reader.ReadAsync([0],deadline,TestContext.Current.CancellationToken);
        Assert.Equal(outcome,read.Outcome);Assert.Equal(["Committed","Dispose"],calls);
        Assert.Equal(DateTimeOffset.UnixEpoch,read.StartedUtc);Assert.Equal(deadline.AddMilliseconds(1),read.EndedUtc);
        if(outcome=="kafka_exception")Assert.Equal((int)ErrorCode.Local_TimedOut,read.ErrorCode);
        ConsumerJudgementResult result=ConsumerJudgement.Evaluate(ConsumerJudgementFixture.Empty(100) with {DeadlineInstant=deadline},read);
        Assert.Equal(VerifierResultStatus.Incomplete,result.Status);
        Assert.Equal([outcome=="success"?"consumer_completion_deadline":"consumer_commit_evidence_unavailable"],result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
        Assert.False(result.Consumer.TryGetProperty("counts",out _));
    }

    private static void AssertNotAttempted(ConsumerOffsetRead read)
    {
        Assert.Equal("not_attempted",read.Outcome);Assert.Null(read.StartedUtc);Assert.Null(read.EndedUtc);Assert.Null(read.ErrorCode);Assert.Empty(read.Offsets);
    }

    private sealed class SetupClock:TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>ticks;
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UnixEpoch.AddTicks(ticks);
        internal void Advance(TimeSpan elapsed)=>ticks+=elapsed.Ticks;
    }
}
#pragma warning restore CA1707
