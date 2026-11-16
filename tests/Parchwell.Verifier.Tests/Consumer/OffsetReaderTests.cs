using Confluent.Kafka;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class OffsetReaderTests
{
    private static readonly string[] NativeReadCalls=["Committed","Dispose"];
    [Theory]
    [InlineData("success")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("other-topic")]
    [InlineData("extra")]
    [InlineData("timeout")]
    [InlineData("authorization")]
    [InlineData("partition")]
    public async Task Given_ExplicitPartitionRead_When_Reading_Then_PreservesOutcomeWithOneCallAndDispose(string input)
    {
        var clock=new ManualKafkaTimeProvider();var calls=new List<string>();TimeSpan? timeout=null;
        IConsumer<byte[],byte[]> consumer=NativeProxy.Create<IConsumer<byte[],byte[]>>((method,args)=>
        {
            calls.Add(method.Name);
            if(method.Name=="Dispose")return null;
            Assert.Equal("Committed",method.Name);
            Assert.Equal([new TopicPartition("document-events",0),new TopicPartition("document-events",1)],Assert.IsAssignableFrom<IEnumerable<TopicPartition>>(args![0]));
            timeout=Assert.IsType<TimeSpan>(args[1]);clock.Advance(3);
            if(input=="timeout")throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
            if(input=="authorization")throw new KafkaException(new Error(ErrorCode.GroupAuthorizationFailed));
            if(input=="partition")throw new TopicPartitionOffsetException([new TopicPartitionOffsetError("document-events",0,7,new Error(ErrorCode.TopicAuthorizationFailed))]);
            return input switch
            {
                "missing"=>new List<TopicPartitionOffset>{new("document-events",0,7)},
                "duplicate"=>new List<TopicPartitionOffset>{new("document-events",0,7),new("document-events",0,8)},
                "other-topic"=>new List<TopicPartitionOffset>{new("document-events",0,7),new("other",1,8)},
                "extra"=>new List<TopicPartitionOffset>{new("document-events",0,7),new("document-events",1,8),new("document-events",2,9)},
                _=>new List<TopicPartitionOffset>{new("document-events",1,Offset.Unset),new("document-events",0,7)}
            };
        });
        var reader=new ConsumerCommittedOffsetReader(_=>{clock.Advance(2);return consumer;},clock);
        ConsumerOffsetRead result=await reader.ReadAsync([1,0],DateTimeOffset.UnixEpoch.AddMilliseconds(100),TestContext.Current.CancellationToken);
        Assert.Equal(["Committed","Dispose"],calls);
        Assert.Equal(TimeSpan.FromMilliseconds(98),timeout);
        Assert.Equal([0,1],result.RequestedPartitions);
        Assert.Equal(DateTimeOffset.UnixEpoch,result.StartedUtc);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMilliseconds(5),result.EndedUtc);
        Assert.Equal(input switch {"success"=>"success","timeout" or "authorization"=>"kafka_exception","partition"=>"partition_exception",_=>"invalid_response"},result.Outcome);
        if(input is "success" or "missing" or "duplicate" or "other-topic" or "extra")Assert.Equal(0,result.ErrorCode);
        if(input=="success")Assert.Equal([new ConsumerObservedOffset(1,-1001,0),new ConsumerObservedOffset(0,7,0)],result.Offsets);
        if(input=="partition")Assert.Equal([new ConsumerObservedOffset(0,7,(int)ErrorCode.TopicAuthorizationFailed)],result.Offsets);
        if(input=="timeout")Assert.Equal((int)ErrorCode.Local_TimedOut,result.ErrorCode);
        if(input=="authorization")Assert.Equal((int)ErrorCode.GroupAuthorizationFailed,result.ErrorCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_DeadlineBoundary_When_Reading_Then_OnlyPositiveTimeConstructsClient(int ticks)
    {
        var clock=new ManualKafkaTimeProvider();var calls=new List<string>();
        var reader=new ConsumerCommittedOffsetReader(_=>NativeProxy.Create<IConsumer<byte[],byte[]>>((method,args)=>
        {
            calls.Add(method.Name);
            return method.Name=="Committed"?new List<TopicPartitionOffset>():null;
        }),clock);
        ConsumerOffsetRead result=await reader.ReadAsync([],DateTimeOffset.UnixEpoch.AddTicks(ticks),TestContext.Current.CancellationToken);
        Assert.Equal(ticks>0?"success":"not_attempted",result.Outcome);
        Assert.Equal(ticks>0?NativeReadCalls:[],calls);
        Assert.Empty(result.Offsets);
        if(ticks<=0){Assert.Null(result.StartedUtc);Assert.Null(result.EndedUtc);Assert.Null(result.ErrorCode);}
    }

    [Fact]
    public async Task Given_ClientConstructionFails_When_Reading_Then_ReturnsKafkaExceptionWithoutRetry()
    {
        var attempts=0;var clock=new ManualKafkaTimeProvider();
        var reader=new ConsumerCommittedOffsetReader(_=>{attempts++;throw new KafkaException(new Error(ErrorCode.Local_Authentication));},clock);
        ConsumerOffsetRead result=await reader.ReadAsync([0],clock.GetUtcNow().AddSeconds(1),TestContext.Current.CancellationToken);
        Assert.Equal(1,attempts);Assert.Equal("kafka_exception",result.Outcome);Assert.Equal((int)ErrorCode.Local_Authentication,result.ErrorCode);
    }

    [Theory]
    [InlineData("read_committed")]
    [InlineData("read_uncommitted")]
    public void Given_GroupIsolation_U6_When_BuildingReaderConfiguration_Then_UsesRepeatGroupAndDisablesAutoCommit(string isolation)
    {
        var baseline=new ConsumerConfig{BootstrapServers="synthetic:9093",GroupId="parchwell-verifier",EnableAutoCommit=true};
        var options=new VerifierHostOptions{RunId="run1",Phase="C1",Repeat=1};
        var settings=new Dictionary<string,string>{{"isolation.level",isolation}};
        ConsumerConfig config=ConsumerCommittedOffsetReader.ConfigurationForRead(baseline,options,new ConsumerVerificationPlan(null,1000,settings,settings));
        Assert.Equal("run1-c1-r1-consumers",config.GroupId);Assert.False(config.EnableAutoCommit);Assert.Equal(isolation,config.Get("isolation.level"));
        Assert.Equal("parchwell-verifier",baseline.GroupId);Assert.True(baseline.EnableAutoCommit);Assert.Equal(baseline.BootstrapServers,config.BootstrapServers);
    }
}
#pragma warning restore CA1707
