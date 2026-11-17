using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class StartSnapshotTests
{
    [Fact]
    public async Task Given_TwoConsumerMembers_U17_When_Starting_Then_SnapshotsBothReadsAndKeepsTwoArtifactReceipt()
    {
        using var fixture=new HostEvidenceBuilder(scenarioId:"C03");
        var order=new List<string>();var store=new SnapshotStore(fixture,order);var completion=new VerifierCompletionState();
        fixture.Kafka.Query=_=>{order.Add("watermarks");fixture.Kafka.Clock.Advance(20);return new WatermarkOffsets(0,0);};
        StartLineHandler handler=Handler(fixture,store,completion);
        DateTimeOffset first=fixture.Kafka.Clock.GetUtcNow();
        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["first-read","watermarks","second-read","note","inventory","snapshot"],order);
        ConsumerStartSnapshot snapshot=ConsumerArtifactCodec.ReadSnapshot(store.Snapshot!);
        Assert.Equal(ConsumerPlanValidation.GroupId(fixture.Options),snapshot.GroupId);
        Assert.Equal(first,snapshot.FirstReadStartedUtc);
        Assert.Equal(first.AddMilliseconds(40),snapshot.SecondReadEndedUtc);
        Assert.Equal([new ConsumerSnapshotMember("subject","subject",0,2),new ConsumerSnapshotMember("witness","witness",1,3)],snapshot.Members);
        completion.MarkReturned(fixture.Options,0,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _,out string path,out byte[] bytes));
        Assert.Equal(PhasePaths.StartLineCompletion(fixture.Options.RunId!,fixture.Options.Phase!,1),path);
        WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal("start-line",receipt.Mode);
        Assert.Equal([fixture.Options.StartLineOutputName,fixture.Options.BrokerInventoryOutputName],receipt.Artifacts.Select(artifact=>artifact.Name));
        Assert.Equal(bytes,PhaseWireCodec.WriteReceipt(receipt));
    }

    [Fact]
    public async Task Given_MembersWithoutRows_U17_When_Starting_Then_RecordsMinusOneForBothReads()
    {
        using var fixture=new HostEvidenceBuilder(scenarioId:"C03");
        var store=new SnapshotStore(fixture,[]){EmptyRecords=true};var completion=new VerifierCompletionState();
        Assert.Equal(0,await Handler(fixture,store,completion).ExecuteAsync(TestContext.Current.CancellationToken));
        ConsumerStartSnapshot snapshot=ConsumerArtifactCodec.ReadSnapshot(store.Snapshot!);
        Assert.Equal([new ConsumerSnapshotMember("subject","subject",-1,-1),new ConsumerSnapshotMember("witness","witness",-1,-1)],snapshot.Members);
    }

    [Fact]
    public async Task Given_AbsentOptionalInputBudgets_U17_When_RecordExceedsOutputBudget_Then_ReadsFullRecordAndWritesBoundedSnapshot()
    {
        using var fixture=new HostEvidenceBuilder(scenarioId:"C03");
        fixture.Options.MaxArtifactBytes=null;fixture.Options.MaxTotalInputBytes=null;
        fixture.Options.MaxRowBytes=null;fixture.Options.MaxRowsPerArtifact=null;fixture.Options.MaxInputEntries=null;
        fixture.Options.MaxOutputBytes=2048;
        ConfigureStartLine(fixture);
        var store=new SnapshotStore(fixture,[]){ExtraRows=40};var completion=new VerifierCompletionState();
        var limits=new InputLimits(fixture.Options.MaxInputDepth!.Value,ConsumerRecordLimits.MaximumBlobBytes,long.MaxValue,
            ConsumerRecordLimits.MaximumRowBytes,ConsumerRecordLimits.MaximumRowsPerBlob,long.MaxValue,fixture.Options.MaxDiagnosticBytes!.Value);
        var reader=new ConsumerArtifactReader(store,limits,fixture.Options.BlobIoTimeout!.Value,fixture.Kafka.Clock);
        var handler=new StartLineHandler(Options.Create(fixture.Options),fixture.Kafka.Clock,new VerifierKafkaSessionFactory(_=>fixture.Kafka.Session),store,completion,reader);
        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.True(store.LargestRecordBytes>fixture.Options.MaxOutputBytes);
        Assert.True(store.Snapshot!.LongLength<=fixture.Options.MaxOutputBytes);
        Assert.Equal([new ConsumerSnapshotMember("subject","subject",40,42),new ConsumerSnapshotMember("witness","witness",41,43)],ConsumerArtifactCodec.ReadSnapshot(store.Snapshot).Members);
    }

    [Fact]
    public async Task Given_B10_U17_When_StartingWithConsumerReaderAvailable_Then_PerformsNoConsumerReadAndKeepsBytes()
    {
        using var baseline=new HostEvidenceBuilder(scenarioId:"B10",scenarioSourceId:"A01");
        using var current=new HostEvidenceBuilder(scenarioId:"B10",scenarioSourceId:"A01");
        var order=new List<string>();var store=new SnapshotStore(current,order);var oldCompletion=new VerifierCompletionState();var completion=new VerifierCompletionState();
        ConfigureStartLine(baseline);
        var old=new StartLineHandler(Options.Create(baseline.Options),baseline.Kafka.Clock,new VerifierKafkaSessionFactory(_=>baseline.Kafka.Session),baseline,oldCompletion);
        Assert.Equal(0,await old.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0,await Handler(current,store,completion).ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["note","inventory"],order);
        Assert.Null(store.Snapshot);
        Assert.Equal(baseline.CreatedObjects[baseline.Options.StartLineOutputName!],current.CreatedObjects[current.Options.StartLineOutputName!]);
        Assert.Equal(baseline.CreatedObjects[baseline.Options.BrokerInventoryOutputName!],current.CreatedObjects[current.Options.BrokerInventoryOutputName!]);
        oldCompletion.MarkReturned(baseline.Options,0,CancellationToken.None,CancellationToken.None);
        completion.MarkReturned(current.Options,0,CancellationToken.None,CancellationToken.None);
        Assert.True(oldCompletion.TryGetBlobReceipt(out _,out _,out byte[] oldBytes));
        Assert.True(completion.TryGetBlobReceipt(out _,out _,out byte[] bytes));
        Assert.Equal(oldBytes,bytes);
    }

    [Theory]
    [InlineData("first-read")]
    [InlineData("second-read")]
    [InlineData("snapshot")]
    public async Task Given_ConsumerReadOrSnapshotFailure_U17_When_WorkerRuns_Then_ExitsOneAndPublishesNoCompletion(string failure)
    {
        using var fixture=new HostEvidenceBuilder(scenarioId:"C03");
        var order=new List<string>();var store=new SnapshotStore(fixture,order){Failure=failure};var completion=new VerifierCompletionState();
        var state=new VerifierExecutionState();
        using IHost host=new HostBuilder().ConfigureServices(services=>
        {
            services.AddSingleton<IOptions<VerifierHostOptions>>(Options.Create(fixture.Options));
            services.AddSingleton<IVerifierModeHandler>(Handler(fixture,store,completion));services.AddSingleton(state);
            services.AddSingleton(completion);services.AddSingleton<TimeProvider>(fixture.Kafka.Clock);
            services.AddHostedService<VerifierWorker>();
        }).Build();
        await host.RunAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1,state.ExitCode);
        Assert.False(completion.TryGetBlobReceipt(out _,out _,out _));
        if(failure=="snapshot")Assert.Equal(["first-read","second-read","note","inventory","snapshot"],order);
        else Assert.Empty(fixture.CreateNames);
    }

    private static void ConfigureStartLine(HostEvidenceBuilder fixture)
    {
        fixture.Options.Mode="start-line";
        fixture.Options.JobName=PhaseIdentity.StartLineJobName(fixture.Options.RunId!,fixture.Options.Phase!,fixture.Options.Repeat!.Value);
    }

    private static StartLineHandler Handler(HostEvidenceBuilder fixture,SnapshotStore store,VerifierCompletionState completion)
    {
        ConfigureStartLine(fixture);
        var limits=new InputLimits(64,1000000,10000000,65536,40000,100000,256);
        var reader=new ConsumerArtifactReader(store,limits,TimeSpan.FromSeconds(1),fixture.Kafka.Clock);
        return new(Options.Create(fixture.Options),fixture.Kafka.Clock,new VerifierKafkaSessionFactory(_=>fixture.Kafka.Session),store,completion,reader);
    }

    private sealed class SnapshotStore(HostEvidenceBuilder fixture,List<string> order):IProducerArtifactStore,IVerifierArtifactStore
    {
        private int reads;
        internal string? Failure {get;init;}
        internal bool EmptyRecords {get;init;}
        internal int ExtraRows {get;init;}
        internal long LargestRecordBytes {get;private set;}
        internal byte[]? Snapshot {get;private set;}
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)
        {
            reads++;string step=reads==1?"first-read":"second-read";order.Add(step);
            if(Failure==step)throw new IOException("synthetic read failure");
            fixture.Kafka.Clock.Advance(10);
            if(EmptyRecords)return Task.FromResult(new BlobListing([prefix+"subject/registration.json",prefix+"witness/registration.json"],1,true));
            return Task.FromResult(new BlobListing([prefix+"subject/registration.json",prefix+"subject/record/000000.jsonl",
                prefix+"witness/registration.json",prefix+"witness/record/000000.jsonl"],1,true));
        }
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)
        {
            string role=name.Contains("/subject/",StringComparison.Ordinal)?"subject":"witness";
            if(name.EndsWith("registration.json",StringComparison.Ordinal))
            {
                var marker=new {schema_version=1,kind="consumer_registration",run_id=fixture.Options.RunId,scenario_id="C03",configuration_id="C0",phase="C0",repeat_number=1,
                    role,instance_id=role,job_name=role+"-job",pod_name=role,pod_uid=role+"-uid",node_name="n1",source_commit=fixture.Options.SourceCommit,
                    group_id=ConsumerPlanValidation.GroupId(fixture.Options),effective_settings=new Dictionary<string,string>(),registered_utc="2026-11-15T00:00:00.0000000Z"};
                return Task.FromResult<byte[]?>(JsonSerializer.SerializeToUtf8Bytes(marker));
            }
            int rows=(role=="subject"?1:2)+(reads==1?0:2)+ExtraRows;
            string value=string.Concat(Enumerable.Range(0,rows).Select(index=>JsonSerializer.Serialize(new {schema_version=1,kind="partition_event",instance_id=role,row_sequence=index,
                @event="assigned",partitions=Array.Empty<int>(),event_utc="2026-11-15T00:00:00.0000000Z"})+"\n"));
            byte[] record=Encoding.UTF8.GetBytes(value);LargestRecordBytes=Math.Max(LargestRecordBytes,record.LongLength);
            return Task.FromResult<byte[]?>(record);
        }
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken)
        {
            string kind=name.EndsWith("consumer-start.json",StringComparison.Ordinal)?"snapshot":name.EndsWith("broker-inventory.json",StringComparison.Ordinal)?"inventory":"note";
            order.Add(kind);if(Failure==kind)throw new IOException("synthetic snapshot write failure");
            if(kind=="snapshot")Snapshot=content;
            return fixture.CreateAsync(name,content,cancellationToken);
        }
    }
}
#pragma warning restore CA1707
