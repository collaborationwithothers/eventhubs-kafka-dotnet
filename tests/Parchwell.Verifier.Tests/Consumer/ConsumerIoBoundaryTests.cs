using System.Text.Json;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ConsumerIoBoundaryTests
{
    public static TheoryData<string,int,bool,bool> CompletedReadCases
    {
        get
        {
            var cases=new TheoryData<string,int,bool,bool>();
            foreach(string artifact in new[]{"record","snapshot"})
            {
                cases.Add(artifact,-1,false,true);
                cases.Add(artifact,0,false,false);
                cases.Add(artifact,1,false,false);
                cases.Add(artifact,-1,true,true);
                cases.Add(artifact,0,true,true);
                cases.Add(artifact,1,true,false);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CompletedReadCases))]
    public async Task Given_ReadElapsedBoundary_U20_When_CompletedReadReturns_Then_RequiresElapsedAndCancellationBudget(
        string artifact,int adjacentTicks,bool deferTimerCallbacks,bool valid)
    {
        var fixture=new IoFixture(deferTimerCallbacks);
        string name=IoFixture.ArtifactName(artifact);
        fixture.Store.ReadOverride=(readName,token)=>
        {
            if(readName==name)
            {
                fixture.ObservedReadToken=token;
                fixture.Clock.AdvanceRead(TimeSpan.FromMilliseconds(1000)+TimeSpan.FromTicks(adjacentTicks));
            }
            return Task.FromResult(fixture.Store.Blobs.GetValueOrDefault(readName));
        };

        RepeatResultExtension result=await fixture.Run();

        Assert.Equal(valid?VerifierResultStatus.CompleteValid:VerifierResultStatus.Incomplete,result.Status);
        Assert.Equal(!deferTimerCallbacks&&adjacentTicks>=0,fixture.ObservedReadToken.IsCancellationRequested);
        Assert.True(fixture.Clock.GetUtcNow()<fixture.Context.DeadlineInstant);
        Assert.Equal(valid?1:0,fixture.OffsetCalls);
        if(valid)
        {
            Assert.Empty(result.Consumer.GetProperty("reasons").EnumerateArray());
            Assert.Empty(result.Consumer.GetProperty("diagnostics").EnumerateArray());
            Assert.Equal("success",result.Consumer.GetProperty("offset_read").GetProperty("outcome").GetString());
            JsonElement counts=result.Consumer.GetProperty("counts");
            Assert.Equal(0,counts.GetProperty("processed_once").GetInt64());
            Assert.Equal(0,counts.GetProperty("processed_again").GetInt64());
            Assert.Equal(0,counts.GetProperty("processed_again_extra_rows").GetInt64());
            Assert.Equal(0,counts.GetProperty("committed_never_processed").GetInt64());
        }
        else
        {
            AssertUnavailable(result,name);
            string expectedDetail=artifact=="snapshot"&&adjacentTicks>0?nameof(TimeoutException):nameof(OperationCanceledException);
            Assert.Equal(expectedDetail,Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray()).GetProperty("detail").GetString());
        }
    }

    [Theory]
    [InlineData("record")]
    [InlineData("snapshot")]
    public async Task Given_PendingUncooperativeRead_U20_When_BudgetExpires_Then_WaitTimeoutStopsReadAndPreventsOffsetObservation(string artifact)
    {
        var fixture=new IoFixture(deferTimerCallbacks:false);
        string name=IoFixture.ArtifactName(artifact);
        var pending=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.ReadOverride=(readName,token)=>
        {
            if(readName!=name)return Task.FromResult(fixture.Store.Blobs.GetValueOrDefault(readName));
            fixture.ObservedReadToken=token;
            return pending.Task;
        };
        Task<RepeatResultExtension> running=fixture.Run();
        Assert.False(running.IsCompleted);
        Assert.False(fixture.ObservedReadToken.IsCancellationRequested);

        fixture.ManualClock.Advance(1000);
        RepeatResultExtension result=await running;

        Assert.True(fixture.ObservedReadToken.IsCancellationRequested);
        Assert.False(pending.Task.IsCompleted);
        Assert.True(fixture.Clock.GetUtcNow()<fixture.Context.DeadlineInstant);
        Assert.Equal(0,fixture.OffsetCalls);
        AssertUnavailable(result,name);
        Assert.Equal(nameof(TimeoutException),Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray()).GetProperty("detail").GetString());
    }

    private static void AssertUnavailable(RepeatResultExtension result,string artifact)
    {
        Assert.Equal(VerifierResultStatus.Incomplete,result.Status);
        Assert.Equal(["consumer_record_unavailable"],result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
        JsonElement diagnostic=Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("consumer_record_unavailable",diagnostic.GetProperty("reason").GetString());
        Assert.Equal(artifact,diagnostic.GetProperty("artifact").GetString());
        Assert.Equal("not_attempted",result.Consumer.GetProperty("offset_read").GetProperty("outcome").GetString());
        Assert.False(result.Consumer.TryGetProperty("counts",out _));
    }

    private sealed class IoFixture
    {
        internal ConsumerWireFixture Store {get;}=new();
        internal ManualKafkaTimeProvider ManualClock {get;}=new();
        internal IoClock Clock {get;}
        internal CancellationToken ObservedReadToken {get;set;}
        internal RepeatResultContext Context {get;}
        internal int OffsetCalls {get;private set;}
        private readonly ConsumerRepeatExtension extension;
        internal IoFixture(bool deferTimerCallbacks)
        {
            Clock=new IoClock(ManualClock,deferTimerCallbacks);
            Store.Clock=Clock;Store.Timeout=TimeSpan.FromMilliseconds(1000);
            var options=new VerifierHostOptions
            {
                RunId="synthetic-consumer",ScenarioId="C03",ConfigurationId="C0",Phase="C0",Repeat=1,
                BlobIoTimeout=Store.Timeout,MaxArtifactBytes=Store.Limits.MaxArtifactBytes
            };
            var settings=new Dictionary<string,string>(StringComparer.Ordinal)
            { ["isolation.level"]="read_uncommitted",["max.poll.interval.ms"]="300000" };
            var plan=new ConsumerVerificationPlan(null,600000,settings,settings);
            var empty=ConsumerJudgementFixture.Empty(100);
            Context=new(VerifierResultStatus.CompleteValid,empty.Window,[],[],Clock.GetUtcNow().AddMinutes(10),new InputBudget(Store.Limits));
            extension=new ConsumerRepeatExtension(options,plan,Store.Reader(),Store,ReadOffsets,Clock,"synthetic:9092");
        }
        internal static string ArtifactName(string artifact)=>artifact=="record"?ConsumerWireFixture.Name("subject","record/000000.jsonl"):ConsumerWireFixture.Prefix+"consumer-start.json";
        internal Task<RepeatResultExtension> Run()=>extension.EvaluateAsync(Context,TestContext.Current.CancellationToken);
        private Task<ConsumerOffsetRead> ReadOffsets(IReadOnlyList<int> partitions,DateTimeOffset deadline,CancellationToken token)
        {
            OffsetCalls++;
            Assert.Equal([0],partitions);Assert.Equal(Context.DeadlineInstant,deadline);Assert.False(token.IsCancellationRequested);
            return Task.FromResult(new ConsumerOffsetRead("success",partitions,Clock.GetUtcNow(),Clock.GetUtcNow(),0,[new ConsumerObservedOffset(0,-1001,0)]));
        }
    }

    // A timer callback may await scheduling after its due time. Delegate the existing manual clock's
    // timer behavior and add submillisecond ticks for the adjacent-tick cases. Deferring only callbacks
    // isolates the elapsed guard: equality with an unexpired token permits the read; elapsed above it rejects.
    private sealed class IoClock(ManualKafkaTimeProvider inner,bool deferTimerCallbacks):TimeProvider
    {
        private long fractionalTicks;
        public override long TimestampFrequency=>inner.TimestampFrequency;
        public override long GetTimestamp()=>inner.GetTimestamp()+fractionalTicks;
        public override DateTimeOffset GetUtcNow()=>inner.GetUtcNow().AddTicks(fractionalTicks);
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)=>
            deferTimerCallbacks?inner.CreateTimer(static _=>{},null,dueTime,period):inner.CreateTimer(callback,state,dueTime,period);
        internal void AdvanceRead(TimeSpan elapsed)
        {
            fractionalTicks+=elapsed.Ticks%TimeSpan.TicksPerMillisecond;
            inner.Advance((int)(elapsed.Ticks/TimeSpan.TicksPerMillisecond));
        }
    }
}
#pragma warning restore CA1707
