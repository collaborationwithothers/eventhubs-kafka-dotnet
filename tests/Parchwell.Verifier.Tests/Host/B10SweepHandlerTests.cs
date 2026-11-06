using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707

public sealed class B10SweepHandlerTests
{
    [Theory]
    [InlineData("C0")] [InlineData("C1")]
    public async Task Given_FiveEligibleRepeats_When_Swept_Then_ImmutableResultsReceiveFinalCountsAfterCleanup(string phase)
    {
        using PhaseFixture fixture=await PhaseFixture.Create(phase);
        Dictionary<string,byte[]> originals=fixture.Verifier.Where(p=>p.Key.EndsWith("/result.json",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        int exit=await fixture.Run(TestContext.Current.CancellationToken);
        Assert.Equal(0,exit);B10Finalization finalization=fixture.Finalization();
        Assert.True(finalization.ScanComplete);Assert.Equal(B10SweepEvidence.ResidualRisk,finalization.ResidualRisk);
        Assert.Equal(Enumerable.Range(1,5),finalization.Repeats.Select(r=>r.Repeat));
        Assert.All(finalization.Repeats,r=>{Assert.True(r.ClassificationFinal);Assert.Equal(VerifierResultStatus.CompleteValid,r.Status);Assert.Equal(new OutcomeCounts(1,0,0,0,0,0),r.Counts);});
        foreach((string path,byte[] bytes) in originals)Assert.Equal(bytes,fixture.Verifier[path]);
        Assert.Equal(["admin-dispose","consumer-dispose","create"],fixture.Events);
        Assert.Single(fixture.Created);Assert.Equal(PhasePaths.B10Finalization(fixture.Options.RunId!,phase),fixture.Created[0]);
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
        fixture.Completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(fixture.Completion.TryGetBlobReceipt(out string container,out string name,out byte[] receiptBytes));
        Assert.Equal("verifier",container);Assert.Equal(PhasePaths.B10SweepCompletion(fixture.Options.RunId!,phase),name);
        WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(receiptBytes);
        Assert.Equal("b10-sweep",receipt.Mode);Assert.True(receipt.CleanupCompleted);Assert.Equal(0,receipt.HandlerExitCode);
        Assert.Equal(Reference(fixture.Created[0],fixture.Verifier[fixture.Created[0]]),Assert.Single(receipt.Artifacts));
        Assert.Equal(15,fixture.RecordsRead);Assert.Equal(9,fixture.Assignments);
    }

    [Fact]
    public async Task Given_EarlierRepeatLateCopyInsideLaterOriginalWindow_When_Swept_Then_OnlyItsOwnerClassifiesTheCopy()
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        fixture.ChangeRecordInstance(1,"pod-first");
        Assert.Equal(0,await fixture.Run(TestContext.Current.CancellationToken));B10Finalization finalization=fixture.Finalization();
        Assert.Equal(new OutcomeCounts(1,0,0,0,1,0),finalization.Repeats[0].Counts);
        Assert.Equal(new OutcomeCounts(0,1,0,0,0,0),finalization.Repeats[1].Counts);
        Assert.All(finalization.Repeats.Skip(2),r=>Assert.Equal(new OutcomeCounts(1,0,0,0,0,0),r.Counts));
    }

    [Fact]
    public async Task Given_OwnAppendAtSweepOffset_When_Swept_Then_CapturedHalfOpenBoundExcludesIt()
    {
        using PhaseFixture fixture=await PhaseFixture.Create();fixture.AppendPostSweepRecord();
        Assert.Equal(0,await fixture.Run(TestContext.Current.CancellationToken));
        Assert.All(fixture.Finalization().Repeats,r=>Assert.Equal(new OutcomeCounts(1,0,0,0,0,0),r.Counts));
        Assert.Equal(15,fixture.RecordsRead);
    }

    [Theory]
    [InlineData("path")] [InlineData("container")] [InlineData("length")] [InlineData("hash")]
    [InlineData("source")] [InlineData("identity")] [InlineData("exit")] [InlineData("intake")]
    [InlineData("result-source")] [InlineData("result-identity")] [InlineData("result-bytes")]
    [InlineData("start-source")] [InlineData("start-reference")] [InlineData("inventory-reference")]
    public async Task Given_SubstitutedRepeatEvidence_When_Swept_Then_UntrustedBoundariesFailWholePhase(string change)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        string resultName=PhasePaths.Result(fixture.Options.RunId!,"C0",2);
        string receiptName=PhasePaths.VerifyCompletion(fixture.Options.RunId!,"C0",2);
        if(change.StartsWith("result-",StringComparison.Ordinal))
        {
            if(change=="result-bytes")fixture.Verifier[resultName]=[..fixture.Verifier[resultName],(byte)' '];
            else
            {
                fixture.Change(resultName,result=>
                {
                    if(change=="result-source")result["verifier_commit"]=new string('b',40);
                    else result["identity"]!["client_id"]="substituted";
                });
                fixture.BindResult(2);
            }
        }
        else if(change.StartsWith("start-",StringComparison.Ordinal)||change=="inventory-reference")
            fixture.Change(PhasePaths.StartLineCompletion(fixture.Options.RunId!,"C0",2),r=>
            {
                if(change=="start-source")r["source_commit"]=new string('b',40);
                else r["artifacts"]![change=="inventory-reference"?1:0]!["sha256"]=new string('b',64);
            });
        else fixture.Change(receiptName,r=>
        {
            switch(change)
            {
                case "path":r["artifacts"]![0]!["name"]=PhasePaths.Result(fixture.Options.RunId!,"C0",3);break;
                case "container":r["artifacts"]![0]!["container"]="ledger";break;
                case "length":r["artifacts"]![0]!["length"]=r["artifacts"]![0]!["length"]!.GetValue<long>()+1;break;
                case "hash":r["artifacts"]![0]!["sha256"]=new string('b',64);break;
                case "source":r["source_commit"]=new string('b',40);break;
                case "identity":r["identity"]!["client_id"]="substituted";break;
                case "exit":r["handler_exit_code"]=0;break;
                case "intake":r["b10_intake"]!["producers"]![0]!["publication_acceptance"]!["objects"]![0]!["sha256"]=new string('b',64);break;
            }
        });
        Assert.Equal(1,await fixture.Run(TestContext.Current.CancellationToken));AssertFailedScan(fixture.Finalization());
    }

    [Theory]
    [InlineData(0,"missing")] [InlineData(1,"missing")]
    [InlineData(0,"malformed")] [InlineData(1,"malformed")]
    [InlineData(0,"changed")] [InlineData(1,"changed")]
    [InlineData(0,"completion-missing")] [InlineData(1,"completion-missing")]
    [InlineData(0,"completion-malformed")] [InlineData(1,"completion-malformed")]
    [InlineData(0,"ledger-identity")] [InlineData(1,"ledger-identity")]
    [InlineData(0,"transport-azure")] [InlineData(1,"transport-azure")]
    [InlineData(0,"transport-io")] [InlineData(1,"transport-io")]
    public async Task Given_OneProducerPublicationUnavailable_When_Swept_Then_OnlyItsRepeatRemainsNonfinal(int role,string failure)
    {
        using PhaseFixture fixture=await PhaseFixture.Create(secondHasAttempt:role==1);
        string instance=role==0?"pod-first-r2":"pod-second-r2";
        string path=PhasePaths.Publication(fixture.Options.RunId!,"C0",2,instance,"ledger.jsonl");
        if(failure.StartsWith("completion-",StringComparison.Ordinal))
        {
            path=PhasePaths.ProducerCompletion(fixture.Options.RunId!,"C0",2,instance);
            if(failure=="completion-missing")fixture.Ledger.Remove(path);else fixture.Ledger[path]="{"u8.ToArray();
        }
        else if(failure.StartsWith("transport-",StringComparison.Ordinal))fixture.ReadFailures[path]=failure=="transport-azure"
            ?new Azure.RequestFailedException(503,"synthetic unavailable"):new IOException("synthetic unavailable");
        else if(failure=="missing")fixture.Ledger.Remove(path);
        else if(failure=="changed")fixture.Ledger[path]=[..fixture.Ledger[path],(byte)' '];
        else
        {
            if(failure=="malformed")fixture.Ledger[path]="{\n"u8.ToArray();
            else
            {
                JsonObject row=JsonNode.Parse(Encoding.UTF8.GetString(fixture.Ledger[path]).Trim())!.AsObject();
                row["identity"]!["client_id"]="substituted";
                fixture.Ledger[path]=Encoding.UTF8.GetBytes(row.ToJsonString()+"\n");
            }
            fixture.BindLedger(2,role,path);
        }
        Assert.Equal(1,await fixture.Run(TestContext.Current.CancellationToken));B10Finalization finalization=fixture.Finalization();
        Assert.True(finalization.ScanComplete);
        Assert.False(finalization.Repeats[1].ClassificationFinal);Assert.Null(finalization.Repeats[1].Counts);
        Assert.All(finalization.Repeats.Where(r=>r.Repeat!=2),r=>Assert.True(r.ClassificationFinal));
    }

    [Theory]
    [InlineData("invalid")] [InlineData("legacy")] [InlineData("genuine-incomplete")] [InlineData("genuine-incomplete-foreign")]
    [InlineData("job-check")] [InlineData("count-check")] [InlineData("inventory-check")]
    public async Task Given_IneligibleOriginalResult_When_ScanCompletes_Then_ItCannotAcquireFinality(string state)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        string resultName=PhasePaths.Result(fixture.Options.RunId!,"C0",2);
        string receiptName=PhasePaths.VerifyCompletion(fixture.Options.RunId!,"C0",2);
        if(state.EndsWith("-check",StringComparison.Ordinal))fixture.Change(receiptName,r=>
            r["repeat_checks"]![state switch {"job-check"=>"producer_job_binding","count-check"=>"registered_producer_count",_=>"broker_inventory_coverage"}]="fail");
        else
        {
            fixture.Change(resultName,r=>
            {
                if(state=="legacy")r.Remove("b10_window_checks_complete");
                else r["b10_window_checks_complete"]=false;
                if(state=="invalid")r["status"]="invalid";
            });
            fixture.BindResult(2);
        }
        if(state=="genuine-incomplete-foreign")fixture.ChangeRecordInstance(1,"synthetic-unknown");
        Assert.Equal(1,await fixture.Run(TestContext.Current.CancellationToken));B10Finalization finalization=fixture.Finalization();
        Assert.True(finalization.ScanComplete);B10RepeatFinalization repeat=finalization.Repeats[1];
        Assert.False(repeat.ClassificationFinal);Assert.Null(repeat.Counts);
        Assert.Equal(state is "invalid" or "genuine-incomplete-foreign"?VerifierResultStatus.Invalid:VerifierResultStatus.Incomplete,repeat.Status);
        Assert.All(finalization.Repeats.Where(r=>r.Repeat!=2),r=>Assert.True(r.ClassificationFinal));
    }

    [Theory]
    [InlineData("missing-note")] [InlineData("malformed-note")] [InlineData("missing-end")]
    [InlineData("offset-error")] [InlineData("deadline")] [InlineData("duplicate-owner")]
    [InlineData("partition-loss")] [InlineData("regressed-current")]
    [InlineData("transport-azure")] [InlineData("transport-io")]
    public async Task Given_GlobalScanFailure_When_Swept_Then_AllFiveRepeatsAreInvalidWithoutCounts(string failure)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        switch(failure)
        {
            case "missing-note":fixture.Verifier.Remove(PhasePaths.SweepLine(fixture.Options.RunId!,"C0"));break;
            case "malformed-note":fixture.Verifier[PhasePaths.SweepLine(fixture.Options.RunId!,"C0")]="{"u8.ToArray();break;
            case "missing-end":fixture.Change(PhasePaths.Result(fixture.Options.RunId!,"C0",2),r=>{r.Remove("end_line");r["b10_window_checks_complete"]=false;});fixture.BindResult(2);break;
            case "offset-error":fixture.Kafka.ReadRecord=()=>throw new KafkaException(new Error(ErrorCode.OffsetOutOfRange));break;
            case "deadline":fixture.AfterRecord=()=>fixture.Kafka.Clock.Advance(1001);break;
            case "duplicate-owner":
                string registration=PhasePaths.Registration(fixture.Options.RunId!,"C0",2,"pod-first-r2");
                JsonObject value=JsonNode.Parse(fixture.Ledger[registration])!.AsObject();value["identity"]!["instance_id"]="pod-first";
                fixture.Ledger[registration]=Encoding.UTF8.GetBytes(value.ToJsonString());break;
            case "partition-loss":fixture.Kafka.ReadMetadata=()=>KafkaSessionTestSupport.Metadata(1);break;
            case "regressed-current":fixture.Kafka.Query=_=>new WatermarkOffsets(0,4);break;
            case "transport-azure":fixture.ReadFailures[PhasePaths.StartLine(fixture.Options.RunId!,"C0",2)]=new Azure.RequestFailedException(503,"synthetic unavailable");break;
            case "transport-io":fixture.ReadFailures[PhasePaths.StartLine(fixture.Options.RunId!,"C0",2)]=new IOException("synthetic unavailable");break;
        }
        Assert.Equal(1,await fixture.Run(TestContext.Current.CancellationToken));AssertFailedScan(fixture.Finalization());
    }

    [Theory]
    [InlineData("records",-1,false)] [InlineData("records",0,true)] [InlineData("records",1,true)]
    [InlineData("bytes",-1,false)] [InlineData("bytes",0,true)] [InlineData("bytes",1,true)]
    [InlineData("inputs",-1,false)] [InlineData("inputs",0,true)] [InlineData("inputs",1,true)]
    public async Task Given_SharedPhaseBudgets_When_SecondWindowAndRepeatRereadEvidence_Then_ExactLimitPassesAndExcessFails(
        string budget,int delta,bool valid)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        if(budget=="records")fixture.Options.MaxConsumedRecords=15+delta;
        if(budget=="bytes")fixture.Options.MaxConsumedBytes=fixture.ExpectedConsumedBytes()+delta;
        if(budget=="inputs")fixture.Options.MaxTotalInputBytes=fixture.ExpectedInputBytes()+delta;
        int exit=await fixture.Run(TestContext.Current.CancellationToken);
        Assert.True(exit==(valid?0:1),$"Expected exit {(valid?0:1)}, actual {exit}; budget {fixture.Options.MaxTotalInputBytes}, read bytes {fixture.ReadBytes}, listed bytes {fixture.ListedBytes}; last {fixture.LastRead}.");
        if(valid)Assert.True(fixture.Finalization().ScanComplete);else AssertFailedScan(fixture.Finalization());
        if(budget!="inputs")Assert.True(fixture.RecordsRead>1);
    }

    [Theory]
    [InlineData(999,true)] [InlineData(1000,true)] [InlineData(1001,false)]
    public async Task Given_FinalWindowCompletionAroundDeadline_When_Swept_Then_ExactDeadlineIsComplete(int milliseconds,bool complete)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();
        fixture.AfterRecord=()=>{if(fixture.Assignments==9)fixture.Kafka.Clock.Advance(milliseconds);};
        Assert.Equal(complete?0:1,await fixture.Run(TestContext.Current.CancellationToken));Assert.Equal(complete,fixture.Finalization().ScanComplete);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Given_CleanupFailureOrCallerCancellation_When_Swept_Then_NoFinalizationOrCompletionIsPublished(bool cancel)
    {
        using PhaseFixture fixture=await PhaseFixture.Create();using var source=new CancellationTokenSource();
        fixture.Kafka.AdminDispose=()=>{if(cancel)source.Cancel();else throw new InvalidOperationException("synthetic cleanup failure");};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>fixture.Run(source.Token));
        else
        {
            AggregateException error=await Assert.ThrowsAsync<AggregateException>(()=>fixture.Run(source.Token));
            Assert.IsType<InvalidOperationException>(Assert.Single(error.InnerExceptions));
        }
        Assert.Empty(fixture.Created);Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
    }

    [Theory]
    [InlineData("C0","complete")] [InlineData("C1","complete")]
    [InlineData("C0","record-count-minus-one")] [InlineData("C1","record-count-minus-one")]
    [InlineData("C0","actual-byte-count-minus-one")] [InlineData("C1","actual-byte-count-minus-one")]
    public async Task Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(
        string phase,string boundary)
    {
        using PhaseFixture fixture=await PhaseFixture.Create(phase,fullPhaseVolume:true);
        Dictionary<string,byte[]> originals=fixture.Verifier.Where(p=>p.Key.EndsWith("/result.json",StringComparison.Ordinal))
            .ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        long expectedBytes=fixture.ExpectedCapacityConsumedBytes();
        Assert.Equal(212505000,expectedBytes);
        Assert.Equal(9300000,fixture.Options.MaxConsumedRecords);
        Assert.Equal(429496729600,fixture.Options.MaxConsumedBytes);
        Assert.True(expectedBytes<fixture.Options.MaxConsumedBytes!.Value);
        if(boundary=="record-count-minus-one")fixture.Options.MaxConsumedRecords=2324999;
        if(boundary=="actual-byte-count-minus-one")fixture.Options.MaxConsumedBytes=expectedBytes-1;

        int exit=await fixture.Run(TestContext.Current.CancellationToken);

        Assert.Equal(2325000,fixture.RecordsRead);
        Assert.Equal(expectedBytes,fixture.ConsumedBytes);
        Assert.Equal(5,fixture.Assignments);
        Assert.Equal(["admin-dispose","consumer-dispose","create"],fixture.Events);
        Assert.Equal(PhasePaths.B10Finalization(fixture.Options.RunId!,phase),Assert.Single(fixture.Created));
        foreach((string path,byte[] bytes) in originals)Assert.Equal(bytes,fixture.Verifier[path]);
        B10Finalization finalization=fixture.Finalization();
        if(boundary=="complete")
        {
            Assert.Equal(0,exit);
            Assert.True(finalization.ScanComplete);
            Assert.Equal(B10SweepEvidence.ResidualRisk,finalization.ResidualRisk);
            Assert.Equal(Enumerable.Range(1,5),finalization.Repeats.Select(r=>r.Repeat));
            Assert.All(finalization.Repeats,r=>
            {
                Assert.True(r.ClassificationFinal);
                Assert.Equal(VerifierResultStatus.CompleteValid,r.Status);
                Assert.Equal(new OutcomeCounts(0,0,1,1,92999,0),r.Counts);
            });
        }
        else
        {
            Assert.Equal(1,exit);
            AssertFailedScan(finalization);
        }
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
        fixture.Completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(fixture.Completion.TryGetBlobReceipt(out string container,out string name,out byte[] receiptBytes));
        Assert.Equal("verifier",container);
        Assert.Equal(PhasePaths.B10SweepCompletion(fixture.Options.RunId!,phase),name);
        WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(receiptBytes);
        Assert.Equal("b10-sweep",receipt.Mode);
        Assert.True(receipt.CleanupCompleted);
        Assert.Equal(exit,receipt.HandlerExitCode);
        Assert.Equal(Reference(fixture.Created[0],fixture.Verifier[fixture.Created[0]]),Assert.Single(receipt.Artifacts));
        TestContext.Current.TestOutputHelper?.WriteLine($"Synthetic full-phase volume: unique_tail_records=465000; reads={fixture.RecordsRead}; bytes={fixture.ConsumedBytes}; boundary={boundary}");
    }

    private static (long Records,long Bytes) PhaseCapacityLimits()
    {
        DirectoryInfo? root=new(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"docs/operations/a01-runtime-limits.json")))root=root.Parent;
        if(root is null)throw new DirectoryNotFoundException("repository_root_not_found");
        using JsonDocument limits=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root.FullName,"docs/operations/a01-runtime-limits.json")));
        JsonElement phase=limits.RootElement.GetProperty("b10_sweep");
        return(phase.GetProperty("MaxConsumedRecords").GetInt64(),phase.GetProperty("MaxConsumedBytes").GetInt64());
    }

    private static void AssertFailedScan(B10Finalization finalization)
    {
        Assert.False(finalization.ScanComplete);Assert.All(finalization.Repeats,r=>
        {Assert.Equal(VerifierResultStatus.Invalid,r.Status);Assert.False(r.ClassificationFinal);Assert.Null(r.Counts);Assert.Contains("b10_sweep_failed",r.Reasons);});
    }
    private static ArtifactReference Reference(string name,byte[] bytes)=>new("verifier",name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private sealed class PhaseFixture : IDisposable,IVerifierArtifactStore,IProducerArtifactStore
    {
        private const int CapacityCopiesPerOwner=93000;
        private long position;
        private bool assigned;
        private readonly List<ConsumeResult<byte[],byte[]>> records=[];
        private readonly List<ConsumeResult<byte[],byte[]>> capacityRecords=[];
        private bool capacityMode;
        private int TotalRecords=>capacityMode?capacityRecords.Count*CapacityCopiesPerOwner:records.Count;
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
        internal long ConsumedBytes { get; private set; }
        internal Action? AfterRecord { get; set; }
        internal long ReadBytes { get; private set; }
        internal long ListedBytes { get; private set; }
        internal string? LastRead { get; private set; }

        internal static async Task<PhaseFixture> Create(string phase="C0",bool secondHasAttempt=false,bool fullPhaseVolume=false)
        {
            var result=new PhaseFixture { capacityMode=fullPhaseVolume };
            try
            {
                for(int repeat=1;repeat<=5;repeat++)
                {
                    using HostEvidenceBuilder fixture=B10SweepFixture.CreateRepeat(repeat,phase,secondHasAttempt||fullPhaseVolume);
                    if(fullPhaseVolume)
                    {
                        string secondInstance=repeat==1?"pod-second":$"pod-second-r{repeat}";
                        B10SweepFixture.ChangeRows(fixture,secondInstance,"ledger.jsonl",row=>
                        {
                            row["delivered"]=false;row["error_code"]=-192;
                            row["error_name"]="Local_MsgTimedOut";row["persistence_status"]=1;
                        });
                        B10SweepFixture.ChangePayload(fixture,secondInstance,"manifest.json",manifest=>
                        {
                            manifest["guard_observations"]!.AsArray().Add("async_delivery_failure_observed");
                            manifest["guard_observations"]!.AsArray().Add("possibly_persisted_observed");
                        });
                        ConsumeResult<byte[],byte[]> template=HostEvidenceBuilder.Record();
                        template.Message.Value=new byte[16];
                        template.Message.Headers.Remove("instance-id");
                        template.Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes(fixture.Options.InstanceId!));
                        result.capacityRecords.Add(template);
                    }
                    foreach((string name,byte[] bytes) in result.Ledger.Where(p=>p.Key.EndsWith("/registration.json",StringComparison.Ordinal)))fixture.AddLedgerObject(name,bytes);
                    int count=fullPhaseVolume?0:secondHasAttempt?2:1;long start=(repeat-1)*count;long end=repeat*count;
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
                        string instance=current==start?fixture.Options.InstanceId!:repeat==1?"pod-second":$"pod-second-r{repeat}";
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
                        string instance=offset==start?fixture.Options.InstanceId!:repeat==1?"pod-second":$"pod-second-r{repeat}";
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
                if(fullPhaseVolume)
                {
                    (long recordLimit,long byteLimit)=PhaseCapacityLimits();
                    result.Options.MaxConsumedRecords=recordLimit;result.Options.MaxConsumedBytes=byteLimit;
                }
                DateTimeOffset capture=result.Kafka.Clock.GetUtcNow();
                var sweep=new B10SweepLine(1,"B10",result.Options.ConfigurationId!,result.Options.RunId!,phase,result.Options.SourceCommit!,
                    "document-events",[new PartitionHigh(0,result.TotalRecords)],capture,capture,capture.AddSeconds(-1),1000);
                result.Verifier[PhasePaths.SweepLine(result.Options.RunId!,phase)]=B10SweepEvidenceCodec.WriteSweepLine(sweep);
                result.Kafka.Poll=result.Kafka.Refresh;
                result.Kafka.Query=_=>new WatermarkOffsets(0,result.TotalRecords);
                result.Kafka.Position=_=>new Offset(result.position);
                result.Kafka.Assign=partitions=>{result.position=partitions.Single().Offset.Value;result.assigned=true;result.Assignments++;};
                result.Kafka.ReadRecord=()=>
                {
                    if(!result.assigned||result.position>=result.TotalRecords)return null;
                    ConsumeResult<byte[],byte[]> record;
                    if(fullPhaseVolume)
                    {
                        record=result.capacityRecords[(int)(result.position/CapacityCopiesPerOwner)];
                        record.Offset=result.position++;
                    }
                    else record=result.records[(int)result.position++];
                    result.RecordsRead++;
                    if(fullPhaseVolume)result.ConsumedBytes+=RawBytes(record);
                    result.AfterRecord?.Invoke();return record;
                };
                result.Kafka.AdminDispose=()=>result.Events.Add("admin-dispose");result.Kafka.ConsumerDispose=()=>result.Events.Add("consumer-dispose");
                return result;
            }
            catch {result.Dispose();throw;}
        }

        internal Task<int> Run(CancellationToken token=default)=>new B10SweepHandler(Microsoft.Extensions.Options.Options.Create(Options),Kafka.Clock,
            new VerifierKafkaSessionFactory(_=>Kafka.Session),this,this,Completion,
            capacityMode?NullLogger<B10SweepHandler>.Instance:null).ExecuteAsync(token);
        internal B10Finalization Finalization()=>B10SweepEvidenceCodec.ReadFinalization(Verifier[PhasePaths.B10Finalization(Options.RunId!,Options.Phase!)]);
        internal void Change(string path,Action<JsonObject> change)
        {JsonObject json=JsonNode.Parse(Verifier[path])!.AsObject();change(json);Verifier[path]=Encoding.UTF8.GetBytes(json.ToJsonString());}
        internal void BindResult(int repeat)=>Change(PhasePaths.VerifyCompletion(Options.RunId!,Options.Phase!,repeat),r=>
        {
            byte[] bytes=Verifier[PhasePaths.Result(Options.RunId!,Options.Phase!,repeat)];
            r["artifacts"]![0]!["length"]=bytes.LongLength;r["artifacts"]![0]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes));
        });
        internal void BindLedger(int repeat,int role,string ledgerPath)
        {
            foreach(string path in new[]{PhasePaths.Result(Options.RunId!,Options.Phase!,repeat),PhasePaths.VerifyCompletion(Options.RunId!,Options.Phase!,repeat)})
                Change(path,r=>
                {
                    JsonObject reference=r["b10_intake"]!["producers"]![role]!["publication_acceptance"]!["objects"]!.AsArray()
                        .Single(p=>p!["name"]!.GetValue<string>()==ledgerPath)!.AsObject();
                    reference["length"]=Ledger[ledgerPath].LongLength;reference["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Ledger[ledgerPath]));
                });
            BindResult(repeat);
        }
        internal void ChangeRecordInstance(int index,string instance)
        {records[index].Message.Headers.Remove("instance-id");records[index].Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes(instance));}
        internal void AppendPostSweepRecord()
        {
            ConsumeResult<byte[],byte[]> record=HostEvidenceBuilder.Record();record.Offset=records.Count;
            record.Message.Headers.Remove("instance-id");record.Message.Headers.Add("instance-id",Encoding.UTF8.GetBytes("pod-first"));records.Add(record);
        }
        internal long ExpectedInputBytes()=>Verifier.Values.Sum(b=>b.LongLength)+Ledger.Keys.Sum(name=>(long)Encoding.UTF8.GetByteCount(name))+Ledger.Where(p=>p.Key.EndsWith("/registration.json",StringComparison.Ordinal)||
            p.Key.EndsWith("/completion.json",StringComparison.Ordinal)||p.Key.EndsWith("/ledger.jsonl",StringComparison.Ordinal)).Sum(p=>p.Value.LongLength);
        internal long ExpectedConsumedBytes()=>records.Select((r,i)=>(i+1)*(r.Message.Key.LongLength+r.Message.Value.LongLength+
            r.Message.Headers.Sum(h=>(long)Encoding.UTF8.GetByteCount(h.Key)+h.GetValueBytes().LongLength))).Sum();
        internal long ExpectedCapacityConsumedBytes()=>capacityRecords.Sum(RawBytes)*CapacityCopiesPerOwner*5;
        private static long RawBytes(ConsumeResult<byte[],byte[]> record)=>record.Message.Key.LongLength+record.Message.Value.LongLength+
            record.Message.Headers.Sum(header=>(long)Encoding.UTF8.GetByteCount(header.Key)+header.GetValueBytes().LongLength);
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
