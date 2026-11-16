using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class OffsetAuthenticationTests
{
    [Theory]
    [InlineData(999,999)]
    [InlineData(1000,1000)]
    [InlineData(1001,1000)]
    public async Task Given_CompletionTimeAroundTokenTimeout_When_Installing_Then_UsesSmallerBudgetAndReadsWithoutPolling(int remaining,int expectedBudget)
    {
        using var fixture=new AuthenticationFixture();
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],fixture.Clock.GetUtcNow().AddMilliseconds(remaining),TestContext.Current.CancellationToken);
        Assert.Equal("success",read.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedBudget),Assert.Single(fixture.Clock.DueTimes));
        Assert.Equal(1,fixture.Provider.Calls);
        Assert.Equal("https://synthetic-namespace.servicebus.windows.net/.default",fixture.Provider.Scope);
        Assert.True(fixture.Provider.ObservedCancellation.CanBeCanceled);
        Assert.Equal(TimeSpan.FromMilliseconds(remaining),fixture.CommittedTimeout);
        Assert.Equal("run1-c1-r1-consumers",fixture.Configuration!.GroupId);
        Assert.False(fixture.Configuration.EnableAutoCommit);
        Assert.Equal(IsolationLevel.ReadCommitted,fixture.Configuration.IsolationLevel);
        fixture.AssertCalls(committed:1);
    }

    [Theory]
    [InlineData(999,true)]
    [InlineData(1000,false)]
    [InlineData(1001,false)]
    public async Task Given_AcquisitionAroundInstallationDeadline_When_Reading_Then_TimedOutAcquisitionNeverFetchesOffsets(int acquisitionMs,bool valid)
    {
        using var fixture=new AuthenticationFixture();
        fixture.Provider.ElapsedMilliseconds=acquisitionMs;
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],fixture.Clock.GetUtcNow().AddMilliseconds(2000),TestContext.Current.CancellationToken);
        Assert.Equal(valid?"success":"kafka_exception",read.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(1000),Assert.Single(fixture.Clock.DueTimes));
        Assert.Equal(!valid,fixture.Provider.ObservedCancellation.IsCancellationRequested);
        Assert.Equal(1,fixture.Provider.Calls);
        fixture.AssertCalls(valid?1:0);
        if(!valid){Assert.Equal((int)ErrorCode.Local_TimedOut,read.ErrorCode);Assert.Empty(read.Offsets);}
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("expired")]
    [InlineData("empty")]
    public async Task Given_InitialTokenFailure_When_Reading_Then_ReturnsKafkaExceptionWithoutFetchingOffsets(string failure)
    {
        using var fixture=new AuthenticationFixture();fixture.Provider.Failure=failure;
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],fixture.Clock.GetUtcNow().AddSeconds(2),TestContext.Current.CancellationToken);
        Assert.Equal("kafka_exception",read.Outcome);
        Assert.Equal(1,fixture.Provider.Calls);
        Assert.Empty(read.Offsets);
        fixture.AssertCalls(committed:0);
    }

    [Theory]
    [InlineData(999,true)]
    [InlineData(1000,false)]
    [InlineData(1001,false)]
    public async Task Given_GlobalDeadlineShorterThanTokenBudget_When_AcquisitionFinishesAtBoundary_Then_ExhaustionIsNotAttempted(int acquisitionMs,bool valid)
    {
        using var fixture=new AuthenticationFixture(tokenTimeoutMs:2000);
        fixture.Provider.ElapsedMilliseconds=acquisitionMs;
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],fixture.Clock.GetUtcNow().AddMilliseconds(1000),TestContext.Current.CancellationToken);
        Assert.Equal(valid?"success":"not_attempted",read.Outcome);
        Assert.Equal([0],read.RequestedPartitions);
        Assert.Equal(TimeSpan.FromMilliseconds(1000),Assert.Single(fixture.Clock.DueTimes));
        Assert.Equal(!valid,fixture.Provider.ObservedCancellation.IsCancellationRequested);
        Assert.Equal(1,fixture.Provider.Calls);
        fixture.AssertCalls(valid?1:0);
        if(valid)Assert.Equal(TimeSpan.FromMilliseconds(1),fixture.CommittedTimeout);
        else {Assert.Null(read.StartedUtc);Assert.Null(read.EndedUtc);Assert.Null(read.ErrorCode);Assert.Empty(read.Offsets);}
    }

    [Fact]
    public async Task Given_GlobalDeadlineReachedBeforeProductionConstruction_When_Reading_Then_DoesNotConstructOrAcquire()
    {
        using var fixture=new AuthenticationFixture(tokenTimeoutMs:2000);
        DateTimeOffset deadline=fixture.Clock.GetUtcNow().AddMilliseconds(1000);
        int reads=0;
        fixture.Clock.BeforeUtcRead=()=>{if(++reads==3)fixture.Clock.Advance(1000);};
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],deadline,TestContext.Current.CancellationToken);
        Assert.Equal("not_attempted",read.Outcome);Assert.Equal([0],read.RequestedPartitions);
        Assert.Null(read.StartedUtc);Assert.Null(read.EndedUtc);Assert.Null(read.ErrorCode);Assert.Empty(read.Offsets);
        Assert.Null(fixture.Configuration);Assert.Equal(0,fixture.Provider.Calls);fixture.AssertNoClientCalls();
    }

    [Fact]
    public async Task Given_AuthenticationFailsWithTimeStillRemaining_When_Reading_Then_PreservesKafkaFailure()
    {
        using var fixture=new AuthenticationFixture(tokenTimeoutMs:2000);
        fixture.Provider.ElapsedMilliseconds=999;fixture.Provider.Failure="acquisition";
        DateTimeOffset deadline=fixture.Clock.GetUtcNow().AddMilliseconds(1000);
        ConsumerOffsetRead read=await fixture.Reader.ReadAsync([0],deadline,TestContext.Current.CancellationToken);
        Assert.Equal("kafka_exception",read.Outcome);
        Assert.NotNull(read.StartedUtc);Assert.NotNull(read.EndedUtc);Assert.NotNull(read.ErrorCode);
        Assert.True(read.EndedUtc<deadline);Assert.Empty(read.Offsets);
        fixture.AssertCalls(committed:0);
    }

    private sealed class AuthenticationFixture:IDisposable
    {
        private static readonly string[] ForbiddenCalls=["Consume","Subscribe","Assign","Commit","StoreOffset","StoreOffsets","Close"];
        private readonly ServiceProvider services;
        private readonly List<string> calls=[];
        internal RecordingClock Clock {get;}=new();
        internal SyntheticProvider Provider {get;}
        internal ConsumerCommittedOffsetReader Reader {get;}
        internal ConsumerConfig? Configuration {get;private set;}
        internal TimeSpan? CommittedTimeout {get;private set;}
        internal AuthenticationFixture(int tokenTimeoutMs=1000)
        {
            Provider=new SyntheticProvider(Clock);
            var registrations=new ServiceCollection();registrations.AddSingleton<TimeProvider>(Clock);
            registrations.AddSingleton<IKafkaTokenProvider>(Provider);
            registrations.AddParchwellKafka(KafkaClientRole.VerifierConsumer,options=>{options.NamespaceName="synthetic-namespace";options.ClientId="synthetic-reader";});
            registrations.AddParchwellKafkaAuthentication(options=>{options.TenantId="synthetic-tenant";options.ClientId="synthetic-identity";options.TokenFilePath="synthetic-file";});
            services=registrations.BuildServiceProvider();
            KafkaAuthenticationFactory authentication=services.GetRequiredService<KafkaAuthenticationFactory>();
            var options=new VerifierHostOptions{ScenarioId="C03",RunId="run1",Phase="C1",Repeat=1,VerifierClientId="synthetic-reader",TokenTimeout=TimeSpan.FromMilliseconds(tokenTimeoutMs)};
            var settings=new Dictionary<string,string>{{"isolation.level","read_committed"}};
            Reader=new ConsumerCommittedOffsetReader(services.GetRequiredService<KafkaConfigurationFactory>(),authentication,Options.Create(options),
                new ConsumerVerificationPlan(null,1000,settings,settings),Clock,(config,session)=>
                {
                    Configuration=new ConsumerConfig(config);
                    config.BootstrapServers="127.0.0.1:1";
                    IConsumer<byte[],byte[]> native=authentication.CreateConsumerBuilder<byte[],byte[]>(config,session).Build();
                    return NativeProxy.Create<IConsumer<byte[],byte[]>>((method,args)=>
                    {
                        calls.Add(method.Name);
                        if(method.Name=="get_Handle")return native.Handle;
                        if(method.Name=="Dispose"){native.Dispose();return null;}
                        Assert.Equal("Committed",method.Name);
                        Assert.Equal([new TopicPartition("document-events",0)],Assert.IsAssignableFrom<IEnumerable<TopicPartition>>(args![0]));
                        CommittedTimeout=Assert.IsType<TimeSpan>(args[1]);
                        return new List<TopicPartitionOffset>{new("document-events",0,7)};
                    });
                });
        }
        internal void AssertCalls(int committed)
        {
            Assert.Equal(committed,calls.Count(call=>call=="Committed"));
            Assert.Equal(1,calls.Count(call=>call=="Dispose"));
            Assert.Contains("get_Handle",calls);
            Assert.DoesNotContain(calls,call=>ForbiddenCalls.Contains(call,StringComparer.Ordinal));
        }
        internal void AssertNoClientCalls()=>Assert.Empty(calls);
        public void Dispose()=>services.Dispose();
    }

    private sealed class SyntheticProvider(RecordingClock clock):IKafkaTokenProvider
    {
        internal int Calls {get;private set;}
        internal int ElapsedMilliseconds {get;set;}
        internal string? Failure {get;set;}
        internal string? Scope {get;private set;}
        internal CancellationToken ObservedCancellation {get;private set;}
        public AccessToken GetToken(string scope,CancellationToken cancellationToken)
        {
            Calls++;Scope=scope;ObservedCancellation=cancellationToken;
            clock.Advance(ElapsedMilliseconds);cancellationToken.ThrowIfCancellationRequested();
            if(Failure=="acquisition")throw new InvalidOperationException("synthetic-secret");
            return new AccessToken(Failure=="empty"?"":"synthetic-token",Failure=="expired"?DateTimeOffset.UnixEpoch:TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }

    private sealed class RecordingClock:TimeProvider
    {
        private readonly ManualKafkaTimeProvider inner=new();
        internal List<TimeSpan> DueTimes {get;}=[];
        internal Action? BeforeUtcRead {get;set;}
        public override long TimestampFrequency=>inner.TimestampFrequency;
        public override long GetTimestamp()=>inner.GetTimestamp();
        public override DateTimeOffset GetUtcNow(){BeforeUtcRead?.Invoke();return inner.GetUtcNow();}
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {DueTimes.Add(dueTime);return inner.CreateTimer(callback,state,dueTime,period);}
        internal void Advance(int milliseconds)=>inner.Advance(milliseconds);
    }
}
#pragma warning restore CA1707
