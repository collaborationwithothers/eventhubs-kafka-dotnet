using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707

// Diagnostics observe authenticated reconciliation and must never become a scientific gate.
public sealed class B10LateObservationDiagnosticsTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(99)] [InlineData(100)] [InlineData(101)]
    public void Given_SampleCountAroundCap_When_Projected_Then_TotalsStayExactAndSamplesAreBounded(int count)
    {
        ConsumedRecord[] records=Enumerable.Range(3,count).Select(i=>Record(i)).Reverse().ToArray();
        B10LateObservation observation=Assert.IsType<B10LateObservation>(Project(records,1000));
        Assert.Equal(count,observation.Total);
        Assert.Equal(Math.Min(count,100),observation.Samples.Count);
        Assert.Equal(Math.Max(0,count-100),observation.OmittedSamples);
        Assert.Equal(count,observation.Groups.Sum(g=>g.Count));
        Assert.Equal(Enumerable.Range(3,Math.Min(count,100)).Select(i=>(long)i),observation.Samples.Select(s=>s.Offset));
    }

    [Fact]
    public void Given_PinnedTwoLateLocations_When_Projected_Then_OneLandsInRepeatTwoAndOneInGap()
    {
        B10LateObservation observation=Assert.IsType<B10LateObservation>(Project([Record(7),Record(3)]));
        Assert.Equal(2,observation.Total);
        Assert.Equal(["2","gap"],observation.Samples.Select(s=>s.LandingRepeat));
        Assert.Equal([new B10LateGroup("first",0,"2",1),new B10LateGroup("first",0,"gap",1)],observation.Groups);
    }

    [Theory]
    [InlineData(2,false,null)] [InlineData(3,true,"2")] [InlineData(4,true,"2")]
    [InlineData(5,true,"2")] [InlineData(6,true,"gap")] [InlineData(7,true,"gap")]
    [InlineData(9,true,"gap")] [InlineData(10,false,null)] [InlineData(11,false,null)]
    public void Given_StartEndAndSweepBoundaries_When_Projected_Then_HalfOpenWindowsDetermineSelectionAndLanding(long offset,bool selected,string? landing)
    {
        B10LateObservation observation=Assert.IsType<B10LateObservation>(Project([Record(offset)]));
        Assert.Equal(selected?1:0,observation.Total);
        if(selected)Assert.Equal(landing,Assert.Single(observation.Samples).LandingRepeat);
        else Assert.Empty(observation.Samples);
    }

    [Fact]
    public void Given_OverlappingAttemptStringsAndRepeatedCoordinates_When_Projected_Then_OwnersStayDistinctAndCoordinatesCountOnce()
    {
        B10LateObservation observation=Assert.IsType<B10LateObservation>(Project([
            Record(4) with {InstanceId="second"},Record(3),Record(3)]));
        Assert.Equal(2,observation.Total);Assert.Equal(2,observation.Groups.Count);
        Assert.Equal(["first","second"],observation.Samples.Select(s=>s.InstanceId));
        Assert.All(observation.Samples,s=>Assert.Equal("same-attempt",s.AttemptId));
    }

    [Theory]
    [InlineData("foreign")] [InlineData("missing-instance")] [InlineData("ambiguous-instance")]
    [InlineData("malformed-attempt")] [InlineData("missing-key")] [InlineData("missing-sequence")]
    [InlineData("wrong-topic")]
    public void Given_ForeignOrMalformedTail_When_Projected_Then_ItCannotBecomeOwnedObservation(string kind)
    {
        ConsumedRecord record=kind switch
        {
            "foreign"=>Record(3) with {InstanceId="foreign"},
            "missing-instance"=>Record(3) with {InstanceId=null},
            "ambiguous-instance"=>Record(3) with {Problems=[new(null,"duplicate_header","document-events",null,"instance-id",null)]},
            "malformed-attempt"=>Record(3) with {AttemptId=null},
            "missing-key"=>Record(3) with {Key=null},
            "missing-sequence"=>Record(3) with {Sequence=null},
            _=>Record(3) with {Topic="other"}
        };
        Assert.Equal(0,Assert.IsType<B10LateObservation>(Project([record])).Total);
    }

    [Fact]
    public void Given_MultiplePartitionsAndOwners_When_Projected_Then_SamplesSortByPartitionAndOffsetAndGroupsCoverAllRecords()
    {
        RunWindow original=Window(0,3),late=Window(3,10) with {Partitions=[new(0,3,10),new(1,0,10)]};
        B10LateObservation observation=Assert.IsType<B10LateObservation>(B10LateObservationDiagnostics.Project(1,["first","second"],original,late,
            [Record(5) with {Partition=1},Record(7),Record(1) with {Partition=1,InstanceId="second"},Record(3)],Windows()));
        Assert.Equal([(0,3L),(0,7L),(1,1L),(1,5L)],observation.Samples.Select(s=>(s.Partition,s.Offset)));
        Assert.Equal(4,observation.Groups.Sum(g=>g.Count));
        Assert.True(observation.Groups.Count<=B10LateObservationDiagnostics.GroupLimit);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void Given_ObservationAvailabilityAndScanCompletion_When_Logged_Then_OnlyAvailableCompleteObservationsClaimCompleteFrequency(bool available,bool scanComplete)
    {
        var logger=new CaptureLogger();
        B10LateObservation? observation=available?Project([Record(3)]):null;
        B10LateObservationDiagnostics.Write(logger,"synthetic-run","C0",1,scanComplete,observation);
        Captured summary=Assert.Single(logger.Events,e=>e.Id.Name=="B10LateObservationSummary");
        Assert.Equal(available&&scanComplete,summary.Fields["observation_complete"]);
        Assert.Equal(available,summary.Fields["observation_available"]);
        Assert.Equal(available?1L:null,summary.Fields["total_selected_late_records"]);
        Assert.All(logger.Events,e=>Assert.Equal(available&&scanComplete,e.Fields["observation_complete"]));
    }

    [Theory]
    [InlineData(31,true)] [InlineData(32,true)] [InlineData(33,false)]
    public void Given_PartitionCountAroundDeploymentBound_When_Projected_Then_GroupStorageRemainsBounded(int count,bool available)
    {
        RunWindow late=Window(3,10) with {Partitions=Enumerable.Range(0,count).Select(i=>new PartitionWindow(i,3,10)).ToArray()};
        ConsumedRecord[] records=Enumerable.Range(0,count).SelectMany(p=>new[]{Record(3) with {Partition=p},Record(7) with {Partition=p,InstanceId="second"}}).ToArray();
        B10LateObservation? observation=B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,3),late,records,Windows());
        Assert.Equal(available,observation is not null);
        if(available){Assert.Equal(count*2,observation!.Total);Assert.True(observation.Groups.Count<=384);}
    }

    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,true)] [InlineData(3,false)]
    public void Given_OwnerCountAroundTwo_When_Projected_Then_UnexpectedOwnershipShapeIsUnavailable(int count,bool available)
    {
        string[] owners=Enumerable.Range(0,count).Select(i=>i==0?"first":i==1?"second":"third").ToArray();
        Assert.Equal(available,B10LateObservationDiagnostics.Project(1,owners,Window(0,3),Window(3,10),[],Windows()) is not null);
        Assert.Null(B10LateObservationDiagnostics.Project(1,["first","first"],Window(0,3),Window(3,10),[],Windows()));
    }

    [Theory]
    [InlineData(0,false)] [InlineData(4,false)] [InlineData(5,true)] [InlineData(6,false)]
    public void Given_RepeatWindowCountAroundFive_When_Projected_Then_LandingLabelsUseOnlyTheFiveRecordedRepeats(int count,bool available)
    {
        B10LateObservationWindow[] windows=Enumerable.Range(1,count).Select(i=>new B10LateObservationWindow(i,[new(0,0)],[new(0,3)])).ToArray();
        Assert.Equal(available,B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,3),Window(3,10),[],windows) is not null);
        Assert.Null(B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,3),Window(3,10),[],Enumerable.Repeat(windows.FirstOrDefault()??Windows()[0],5).ToArray()));
    }

    [Fact]
    public void Given_OverlappingLandingWindowsOrOriginalBounds_When_Projected_Then_AmbiguityIsNotAttributedToALaterRepeat()
    {
        B10LateObservationWindow[] windows=Windows();windows[2]=new(3,[new(0,3)],[new(0,6)]);
        B10LateObservation observation=Assert.IsType<B10LateObservation>(B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,3),Window(3,10),[Record(3)],windows));
        Assert.Equal("gap",Assert.Single(observation.Samples).LandingRepeat);
        Assert.Equal(0,Assert.IsType<B10LateObservation>(B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,4),Window(3,10),[Record(3)],Windows())).Total);
        windows=Windows();windows[1]=new(2,[new(0,3),new(0,3)],[new(0,6)]);
        Assert.Null(B10LateObservationDiagnostics.TryProject(1,["first","second"],Window(0,3),Window(3,10),[Record(3)],windows));
    }

    [Fact]
    public void Given_SampleCapExhaustion_When_Logged_Then_ExactCountsOmissionsAndOnlyPublicLocationFieldsAreRetained()
    {
        var logger=new CaptureLogger();B10LateObservation observation=Assert.IsType<B10LateObservation>(Project(Enumerable.Range(3,101).Select(i=>Record(i)).ToArray(),1000));
        B10LateObservationDiagnostics.Write(logger,"synthetic-run","C0",1,true,observation);
        Captured summary=Assert.Single(logger.Events,e=>e.Id.Id==35501);
        Assert.Equal(101L,summary.Fields["total_selected_late_records"]);Assert.Equal(100,summary.Fields["retained_samples"]);Assert.Equal(1L,summary.Fields["omitted_samples"]);
        Assert.Equal(101L,logger.Events.Where(e=>e.Id.Id==35502).Sum(e=>Assert.IsType<long>(e.Fields["selected_late_records"])));
        Assert.Equal(100,logger.Events.Count(e=>e.Id.Id==35503));
        string[] fields=["scenario_id","run_id","phase","source_repeat","observation_complete","instance_id","attempt_id","partition","offset","landing_repeat","{OriginalFormat}"];
        Assert.All(logger.Events.Where(e=>e.Id.Id==35503),e=>Assert.Equal(fields.Order(StringComparer.Ordinal),e.Fields.Keys.Order(StringComparer.Ordinal)));
        Assert.All(logger.Events,e=>{Assert.Equal("B10",e.Fields["scenario_id"]);Assert.Equal("synthetic-run",e.Fields["run_id"]);Assert.Equal("C0",e.Fields["phase"]);});
    }

    [Theory]
    [InlineData("valid")] [InlineData("original-invalid")] [InlineData("scan-fails-later")]
    [InlineData("ineligible")] [InlineData("logger-throws")]
    public async Task Given_EnabledAndDisabledDiagnostics_When_Swept_Then_ResultsFinalizationAndExitAreIdentical(string condition)
    {
        using DiagnosticsFixture disabled=await DiagnosticsFixture.Create();
        using DiagnosticsFixture enabled=await DiagnosticsFixture.Create();
        foreach(DiagnosticsFixture fixture in new[]{disabled,enabled})
        {
            // A copy from repeat one lands in repeat two's original window.
            fixture.ChangeRecordInstance(1,"pod-first");
            if(condition=="original-invalid")fixture.ChangeRecordInstance(0,"foreign");
            if(condition=="scan-fails-later")fixture.AfterRecord=()=>{if(fixture.Assignments>=4)throw new InvalidOperationException("synthetic drain failure");};
            if(condition=="ineligible")
            {
                fixture.Change(PhasePaths.Result(fixture.Options.RunId!,"C0",2),r=>r["b10_window_checks_complete"]=false);
                fixture.BindResult(2);
            }
        }
        var logger=new CaptureLogger {Throw=condition=="logger-throws"};
        int expected=await disabled.Run(token:TestContext.Current.CancellationToken);
        Assert.Equal(condition is "original-invalid" or "scan-fails-later" or "ineligible"?1:0,expected);
        Assert.Equal(expected,await enabled.Run(logger,TestContext.Current.CancellationToken));
        string finalName=PhasePaths.B10Finalization(disabled.Options.RunId!,"C0");
        Assert.Equal(disabled.Verifier[finalName],enabled.Verifier[finalName]);
        foreach(string path in disabled.Verifier.Keys.Where(p=>p.EndsWith("/result.json",StringComparison.Ordinal)))
            Assert.Equal(disabled.Verifier[path],enabled.Verifier[path]);
        if(condition is "valid" or "ineligible" or "logger-throws")
            Assert.Equal(new OutcomeCounts(1,0,0,0,2,0),enabled.Finalization().Repeats[0].Counts);
        if(condition=="logger-throws")return;
        Captured[] summaries=logger.Events.Where(e=>e.Id.Name=="B10LateObservationSummary").ToArray();
        Assert.Equal(5,summaries.Length);
        Captured first=summaries.Single(e=>Equals(e.Fields["source_repeat"],1));
        Assert.Equal(condition!="original-invalid",first.Fields["observation_available"]);
        Assert.Equal(condition is "valid" or "ineligible",first.Fields["observation_complete"]);
        if(condition!="original-invalid")Assert.Equal(2L,first.Fields["total_selected_late_records"]);
        if(condition=="ineligible")Assert.Null(summaries[1].Fields["total_selected_late_records"]);
        if(condition=="scan-fails-later")Assert.All(summaries,e=>Assert.Equal(false,e.Fields["observation_complete"]));
    }

    private static ConsumedRecord Record(long offset)=>new("document-events",0,offset,"synthetic-key","first","same-attempt",1,[]);
    private static RunWindow Window(long start,long end)=>new([new(0,start)],new Dictionary<int,long>{{0,end}},[new(0,start,end)],[],true);
    private static B10LateObservationWindow[] Windows()=>[
        new(1,[new(0,0)],[new(0,3)]),new(2,[new(0,3)],[new(0,6)]),
        new(3,[new(0,10)],[new(0,10)]),new(4,[new(0,10)],[new(0,10)]),new(5,[new(0,10)],[new(0,10)])];
    private static B10LateObservation? Project(IReadOnlyList<ConsumedRecord> records,long sweep=10)=>
        B10LateObservationDiagnostics.Project(1,["first","second"],Window(0,3),Window(3,sweep),records,Windows());

    private sealed record Captured(EventId Id,IReadOnlyDictionary<string,object?> Fields);
    private sealed class CaptureLogger : ILogger<B10SweepHandler>
    {
        internal bool Throw {get;init;}
        internal List<Captured> Events {get;}=[];
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel logLevel)=>true;
        public void Log<TState>(LogLevel logLevel,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        {
            if(Throw)throw new InvalidOperationException("synthetic log failure");
            var fields=Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string,object?>>>(state).ToDictionary(p=>p.Key,p=>p.Value);
            Events.Add(new(eventId,fields));
        }
    }

    private sealed class DiagnosticsFixture : IDisposable,IVerifierArtifactStore,IProducerArtifactStore
    {
        private long position;
        private bool assigned;
        private readonly List<ConsumeResult<byte[],byte[]>> records=[];
        internal VerifierHostOptions Options { get; private set; }=null!;
        internal KafkaSessionTestSupport Kafka { get; }=new();
        internal VerifierCompletionState Completion { get; }=new();
        internal Dictionary<string,byte[]> Verifier { get; }=new(StringComparer.Ordinal);
        internal Dictionary<string,byte[]> Ledger { get; }=new(StringComparer.Ordinal);
        internal Dictionary<string,Exception> ReadFailures { get; }=new(StringComparer.Ordinal);
        internal List<string> Created { get; }=[];
        internal List<string> Events { get; }=[];
        internal int Assignments { get; private set; }
        internal int RecordsRead { get; private set; }
        internal Action? AfterRecord { get; set; }
        internal long ReadBytes { get; private set; }
        internal long ListedBytes { get; private set; }
        internal string? LastRead { get; private set; }

        internal static async Task<DiagnosticsFixture> Create(string phase="C0")
        {
            var result=new DiagnosticsFixture();
            try
            {
                for(int repeat=1;repeat<=5;repeat++)
                {
                    using HostEvidenceBuilder fixture=B10SweepFixture.CreateRepeat(repeat,phase);
                    foreach((string name,byte[] bytes) in result.Ledger.Where(p=>p.Key.EndsWith("/registration.json",StringComparison.Ordinal)))fixture.AddLedgerObject(name,bytes);
                    long start=repeat switch {1=>0,2=>3,3=>8,4=>9,_=>10};long end=repeat==2?6:start+1;
                    JsonObject note=JsonNode.Parse(fixture.Note!)!.AsObject();note["partition_highs"]![0]!["high"]=start;
                    fixture.Note=Encoding.UTF8.GetBytes(note.ToJsonString());
                    string noteName=PhasePaths.StartLine(fixture.Options.RunId!,phase,repeat);
                    await fixture.CreateAsync(noteName,fixture.Note,CancellationToken.None);
                    string startReceiptName=PhasePaths.StartLineCompletion(fixture.Options.RunId!,phase,repeat);
                    JsonObject startReceipt=JsonNode.Parse(fixture.VerifierObjects[startReceiptName])!.AsObject();
                    startReceipt["artifacts"]![0]!["length"]=fixture.Note.LongLength;
                    startReceipt["artifacts"]![0]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(fixture.Note));
                    await fixture.CreateAsync(startReceiptName,Encoding.UTF8.GetBytes(startReceipt.ToJsonString()),CancellationToken.None);
                    long current=start;
                    fixture.Kafka.Query=_=>new WatermarkOffsets(0,end);
                    fixture.Kafka.Position=_=>new Offset(current);
                    fixture.ConsumeRecord=()=>
                    {
                        if(current>=end)return null;
                        ConsumeResult<byte[],byte[]> record=HostEvidenceBuilder.Record();record.Offset=current;
                        string instance=fixture.Options.InstanceId!;
                        record.Message.Headers.Remove("instance-id");record.Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes(instance));
                        current++;return record;
                    };
                    var completion=new VerifierCompletionState();int exit=await fixture.Verify(completion).ExecuteAsync(CancellationToken.None);
                    Assert.Equal(1,exit);completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
                    Assert.True(completion.TryGetBlobReceipt(out _,out string completionName,out byte[] completionBytes));
                    foreach((string name,byte[] bytes) in fixture.VerifierObjects)result.Verifier[name]=bytes;
                    result.Verifier[completionName]=completionBytes;
                    foreach((string name,byte[] bytes) in fixture.LedgerObjects)result.Ledger[name]=bytes;
                    for(long offset=start;offset<end;offset++)
                    {
                        ConsumeResult<byte[],byte[]> record=HostEvidenceBuilder.Record();record.Offset=offset;
                        string instance=fixture.Options.InstanceId!;
                        record.Message.Headers.Remove("instance-id");record.Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes(instance));
                        result.records.Add(record);
                    }
                    result.Options=fixture.Options;
                }
                result.Options.Mode="b10-sweep";result.Options.Repeat=null;result.Options.RepeatPrefix=null;result.Options.InstanceId=null;
                result.Options.ExpectedInstanceId=null;result.Options.ArtifactPrefix=null;result.Options.StartLineInputName=null;result.Options.ResultOutputName=null;
                result.Options.JobName=PhaseIdentity.B10SweepJobName(result.Options.RunId!,phase);result.Options.B10SettleIntervalMilliseconds=1000;
                result.Options.MaxInputEntries=100_000;result.Options.MaxTotalInputBytes=4_194_304;
                result.Options.MaxConsumedRecords=1000;result.Options.MaxConsumedBytes=1_048_576;
                ConsumeResult<byte[],byte[]> gap=HostEvidenceBuilder.Record();gap.Offset=7;
                gap.Message.Headers.Remove("instance-id");gap.Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes("pod-first"));
                result.records.Add(gap);result.records.Sort((left,right)=>left.Offset.Value.CompareTo(right.Offset.Value));
                long high=result.records.Max(record=>record.Offset.Value)+1;
                DateTimeOffset capture=result.Kafka.Clock.GetUtcNow();
                var sweep=new B10SweepLine(1,"B10",result.Options.ConfigurationId!,result.Options.RunId!,phase,result.Options.SourceCommit!,
                    "document-events",[new PartitionHigh(0,high)],capture,capture,capture.AddSeconds(-1),1000);
                result.Verifier[PhasePaths.SweepLine(result.Options.RunId!,phase)]=B10SweepEvidenceCodec.WriteSweepLine(sweep);
                result.Kafka.Poll=result.Kafka.Refresh;
                result.Kafka.Query=_=>new WatermarkOffsets(0,high);
                result.Kafka.Position=_=>new Offset(result.position);
                result.Kafka.Assign=partitions=>{result.position=partitions.Single().Offset.Value;result.assigned=true;result.Assignments++;};
                result.Kafka.ReadRecord=()=>
                {
                    if(!result.assigned)return null;
                    ConsumeResult<byte[],byte[]>? record=result.records.FirstOrDefault(record=>record.Offset.Value>=result.position);
                    if(record is null){result.position=high;return null;}
                    result.position=record.Offset.Value+1;result.RecordsRead++;
                    result.AfterRecord?.Invoke();return record;
                };
                result.Kafka.AdminDispose=()=>result.Events.Add("admin-dispose");result.Kafka.ConsumerDispose=()=>result.Events.Add("consumer-dispose");
                return result;
            }
            catch {result.Dispose();throw;}
        }

        internal Task<int> Run(ILogger<B10SweepHandler>? logger=null,CancellationToken token=default)=>new B10SweepHandler(Microsoft.Extensions.Options.Options.Create(Options),Kafka.Clock,
            new VerifierKafkaSessionFactory(_=>Kafka.Session),this,this,Completion,logger).ExecuteAsync(token);
        internal B10Finalization Finalization()=>B10SweepEvidenceCodec.ReadFinalization(Verifier[PhasePaths.B10Finalization(Options.RunId!,Options.Phase!)]);
        internal void Change(string path,Action<JsonObject> change)
        {JsonObject json=JsonNode.Parse(Verifier[path])!.AsObject();change(json);Verifier[path]=Encoding.UTF8.GetBytes(json.ToJsonString());}
        internal void BindResult(int repeat)=>Change(PhasePaths.VerifyCompletion(Options.RunId!,Options.Phase!,repeat),r=>
        {
            byte[] bytes=Verifier[PhasePaths.Result(Options.RunId!,Options.Phase!,repeat)];
            r["artifacts"]![0]!["length"]=bytes.LongLength;r["artifacts"]![0]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes));
        });
        internal void ChangeRecordInstance(int index,string instance)
        {records[index].Message.Headers.Remove("instance-id");records[index].Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes(instance));}
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)
        {
            if(ReadFailures.TryGetValue(name,out Exception? failure))return Task.FromException<byte[]?>(failure);
            byte[]? bytes=Verifier.GetValueOrDefault(name)??Ledger.GetValueOrDefault(name);ReadBytes+=bytes?.LongLength??0;LastRead=name;
            return Task.FromResult(bytes);
        }
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)
        {
            string[] names=Ledger.Keys.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            ListedBytes+=names.Sum(name=>(long)Encoding.UTF8.GetByteCount(name));
            return Task.FromResult(new BlobListing(names,1,true));
        }
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken)
        {Verifier.Add(name,content);Created.Add(name);Events.Add("create");return Task.CompletedTask;}
        public void Dispose()=>Kafka.Dispose();
    }
}
