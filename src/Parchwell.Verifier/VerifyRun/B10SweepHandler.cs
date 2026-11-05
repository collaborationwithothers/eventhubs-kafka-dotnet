using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.Publication;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

/// <summary>Authenticates immutable B10 repeat evidence and classifies one bounded phase-wide sweep.</summary>
internal sealed class B10SweepHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore verifierStore, IProducerArtifactStore producerStore,
    VerifierCompletionState? completion=null, ILogger<B10SweepHandler>? logger=null) : IVerifierModeHandler
{
    public VerifierMode Mode=>VerifierMode.B10Sweep;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options=configured.Value;
        DateTimeOffset started=clock.GetUtcNow();
        var limits=new InputLimits(options.MaxInputDepth!.Value,options.MaxArtifactBytes!.Value,options.MaxTotalInputBytes!.Value,
            options.MaxRowBytes!.Value,options.MaxRowsPerArtifact!.Value,options.MaxInputEntries!.Value,options.MaxDiagnosticBytes!.Value);
        var inputBudget=new InputBudget(limits);
        var outputLimits=new OutputLimits(options.MaxOutputBytes!.Value,options.MaxDiagnosticsPerCheck!.Value,options.MaxDiagnosticBytes!.Value);
        var repeats=Enumerable.Range(1,5).Select(i=>new RepeatInput(i)).ToArray();
        ArtifactReference? sweepReference=null;
        bool scanComplete=false;
        VerifierKafkaSession? session=null;
        ITimer? safetyTimer=null;
        using var kafkaWork=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            try
            {
                string sweepName=PhasePaths.SweepLine(options.RunId!,options.Phase!);
                byte[] sweepBytes=await ReadRequired(sweepName,false,true).ConfigureAwait(false);
                sweepReference=Reference("verifier",sweepName,sweepBytes);
                B10SweepLine sweep=B10SweepEvidenceCodec.ReadSweepLine(sweepBytes,limits,options.MaxPartitions!.Value);
                IReadOnlyList<IReadOnlyList<ProducerRegistration>> registrations=await B10PhaseRegistrationReader.ReadAsync(
                    options,5,producerStore,inputBudget,(name,ct)=>ReadUncharged(name,true,ct),clock,cancellationToken).ConfigureAwait(false);
                for(int i=0;i<5;i++)await ReadRepeat(repeats[i],registrations[i]).ConfigureAwait(false);
                if(!B10SweepEvidenceCodec.MatchesSweepLine(sweep,options,started,repeats.Select(r=>r.CompletedUtc)))
                    throw new InvalidOperationException("b10_sweep_note_binding_invalid");

                session=sessions.Create(kafkaWork.Token);
                session.InstallToken(cancellationToken);
                long origin=0;
                IReadOnlyDictionary<int,long> current=session.ReadHighWatermarks(()=>
                {
                    origin=clock.GetTimestamp();
                    safetyTimer=clock.CreateTimer(_=>kafkaWork.Cancel(),null,options.SafetyStop!.Value,Timeout.InfiniteTimeSpan);
                },()=>options.SafetyStop!.Value-clock.GetElapsedTime(origin),kafkaWork.Token);
                var recordBudget=new ConsumedRecordBudget(options.MaxConsumedRecords!.Value,options.MaxConsumedBytes!.Value);
                for(int i=0;i<5;i++)
                {
                    RepeatInput repeat=repeats[i];
                    B10SweepWindows windows=B10LateWriteChecks.BuildWindows(repeat.Result!.StartLine,repeat.Result.EndLine,
                        repeat.Start!.PartitionHighs,sweep.PartitionHighs,current,options.MaxPartitions.Value);
                    IReadOnlyList<ConsumedRecord> original=Drain(windows.Original);
                    IReadOnlyList<ConsumedRecord> late=Drain(windows.Late);
                    HashSet<string> earlier=registrations.Take(i).SelectMany(pair=>pair).Select(r=>r.Identity.InstanceId).ToHashSet(StringComparer.Ordinal);
                    HashSet<string> own=repeat.Producers.Select(p=>p.Identity.InstanceId).ToHashSet(StringComparer.Ordinal);
                    if(original.Any(record=>record.InstanceId is null||record.Problems.Any(p=>p.Field=="instance-id")||
                        !own.Contains(record.InstanceId)&&!earlier.Contains(record.InstanceId)))
                    {
                        repeat.Status=VerifierResultStatus.Invalid;repeat.Reasons=["b10_original_owner_invalid"];
                        continue;
                    }
                    if(!repeat.Eligible)continue;
                    B10LateWriteJudgement judgement=B10LateWriteChecks.Classify(repeat.Producers.Select(p=>p.Identity).ToArray(),
                        repeat.Ledgers,original,late,earlier);
                    repeat.Final=judgement.Valid;
                    repeat.Counts=judgement.Counts;
                    repeat.Status=judgement.Valid?VerifierResultStatus.CompleteValid:VerifierResultStatus.Invalid;
                    repeat.Reasons=judgement.Reasons;
                    if(judgement.Valid&&logger is not null)
                        repeat.Observation=B10LateObservationDiagnostics.TryProject(repeat.Number,
                            repeat.Producers.Select(p=>p.Identity.InstanceId).ToArray(),windows.Original,windows.Late,late,
                            repeats.Select(r=>new B10LateObservationWindow(r.Number,r.Result!.StartLine!,r.Result.EndLine!)).ToArray());
                }
                scanComplete=true;

                IReadOnlyList<ConsumedRecord> Drain(RunWindow window)
                {
                    // Empty windows must not inherit the preceding window's assignment.
                    session.Consumer.Unassign();
                    WindowDrainResult result=WindowConsumer.Drain(session.Consumer,window,clock,origin,options.SafetyStop!.Value,
                        options.PollSlice!.Value,recordBudget,cancellationToken,kafkaWork.Token);
                    if(result.Assessments.Any(a=>a.Disposition!=AssessmentDisposition.Pass)||result.DrainDuration is null)
                        throw new InvalidOperationException("b10_sweep_drain_incomplete");
                    session.Consumer.Unassign();
                    return result.Records;
                }
            }
            catch(Exception ex) when(IsEvidenceFailure(ex,cancellationToken))
            {
                foreach(RepeatInput repeat in repeats)
                {
                    repeat.Final=false;repeat.Status=VerifierResultStatus.Invalid;repeat.Counts=null;repeat.Reasons=["b10_sweep_failed"];
                }
            }
        }
        finally
        {
            safetyTimer?.Dispose();
            long origin=clock.GetTimestamp();
            using var cleanupDeadline=new CancellationTokenSource(options.CleanupTimeout!.Value,clock);
            session?.Cleanup(cleanupDeadline.Token);
            CheckDeadline(origin,options.CleanupTimeout.Value,cleanupDeadline.Token);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var finalization=new B10Finalization(1,"B10",options.ConfigurationId!,options.RunId!,options.Phase!,options.SourceCommit!,
            sweepReference,scanComplete,B10SweepEvidence.ResidualRisk,repeats.Select(r=>new B10RepeatFinalization(r.Number,r.ResultReference,
                r.ReceiptReference,r.Final,r.Status,r.Counts,r.Reasons)).ToArray());
        byte[] finalBytes=B10SweepEvidenceCodec.WriteFinalization(finalization,outputLimits);
        string finalName=PhasePaths.B10Finalization(options.RunId!,options.Phase!);
        await Bounded(ct=>verifierStore.CreateAsync(finalName,finalBytes,ct),options.BlobIoTimeout!.Value,cancellationToken).ConfigureAwait(false);
        int exit=scanComplete&&repeats.All(r=>r.Final&&r.Status==VerifierResultStatus.CompleteValid)?0:1;
        var receipt=new WorkerReceipt(1,"b10-sweep","B10",options.ConfigurationId!,options.RunId!,options.Phase!,null,
            PhaseIdentity.B10SweepJobName(options.RunId!,options.Phase!),options.PodName!,null,options.SourceCommit!,exit,true,
            started.ToString("O",CultureInfo.InvariantCulture),clock.GetUtcNow().ToString("O",CultureInfo.InvariantCulture),
            [Reference("verifier",finalName,finalBytes)],null,null);
        completion?.SetPendingBlobReceipt(options.VerifierContainer!,PhasePaths.B10SweepCompletion(options.RunId!,options.Phase!),PhaseWireCodec.WriteReceipt(receipt));
        if(logger is not null)
            foreach(RepeatInput repeat in repeats)
                B10LateObservationDiagnostics.Write(logger,options.RunId!,options.Phase!,repeat.Number,
                    scanComplete&&repeat.Final,repeat.Observation);
        return exit;

        async Task ReadRepeat(RepeatInput input,IReadOnlyList<ProducerRegistration> producers)
        {
            int repeat=input.Number;
            input.Producers=producers;
            string resultName=PhasePaths.Result(options.RunId!,options.Phase!,repeat);
            string receiptName=PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,repeat);
            byte[]? resultBytes=await ReadOptional(resultName,false,true).ConfigureAwait(false);
            byte[]? receiptBytes=await ReadOptional(receiptName,false,true).ConfigureAwait(false);
            if(resultBytes is not null)input.ResultReference=Reference("verifier",resultName,resultBytes);
            if(receiptBytes is not null)input.ReceiptReference=Reference("verifier",receiptName,receiptBytes);
            if(resultBytes is null||receiptBytes is null)throw new InvalidOperationException("b10_repeat_boundary_missing");
            WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(receiptBytes);
            ParseResult<VerifierResult> parsedResult=VerifierResultCodec.Read(resultBytes,resultName,limits,options.MaxDiagnosticsPerCheck!.Value);
            VerifierResult result=parsedResult.IsSuccess&&parsedResult.Value is not null?parsedResult.Value:
                throw new InvalidOperationException("b10_repeat_result_invalid");
            input.Result=result;
            input.Status=result.Status==VerifierResultStatus.Invalid?VerifierResultStatus.Invalid:VerifierResultStatus.Incomplete;
            if(!ReceiptBinds(receipt,"verify",repeat,PhaseIdentity.VerifyJobName(options.RunId!,options.Phase!,repeat))||
                receipt.Identity!=producers[0].Identity||result.Identity!=producers[0].Identity||result.VerifierCommit!=receipt.SourceCommit||
                (receipt.HandlerExitCode==0?result.Status!=VerifierResultStatus.CompleteValid:result.Status==VerifierResultStatus.CompleteValid)||
                receipt.Artifacts.Count!=1||!Matches(receipt.Artifacts[0],"verifier",resultName,resultBytes)||
                receipt.B10Intake is null||result.B10Intake is null||!BindsIntake(receipt.B10Intake,producers)||
                !BindsIntake(result.B10Intake,producers)||!SameIntake(receipt.B10Intake,result.B10Intake)||
                !DateTimeOffset.TryParse(receipt.CompletedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out DateTimeOffset completed))
                throw new InvalidOperationException("b10_repeat_result_untrusted");
            input.CompletedUtc=completed;
            string startName=PhasePaths.StartLine(options.RunId!,options.Phase!,repeat);
            string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,repeat);
            byte[] startBytes=await ReadRequired(startName,false,true).ConfigureAwait(false);
            byte[] inventoryBytes=await ReadRequired(inventoryName,false,true).ConfigureAwait(false);
            byte[] startReceiptBytes=await ReadRequired(PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,repeat),false,true).ConfigureAwait(false);
            WorkerReceipt startReceipt=PhaseWireCodec.ReadReceipt(startReceiptBytes);
            string firstJob=PhaseIdentity.JobName(options.RunId!,options.Phase!,repeat);
            var startIdentity=new RunIdentity("B10",options.ConfigurationId!,options.RunId!,firstJob,firstJob,firstJob);
            ParseResult<StartLineNote> start=StartLineNoteCodec.Read(startBytes,startName,limits,options.MaxPartitions!.Value);
            ParseResult<BrokerInventory> inventory=BrokerInventoryReader.Read(inventoryBytes,inventoryName,startIdentity,options.SourceCommit!,limits);
            if(!ReceiptBinds(startReceipt,"start-line",repeat,PhaseIdentity.StartLineJobName(options.RunId!,options.Phase!,repeat))||
                startReceipt.HandlerExitCode!=0||startReceipt.Identity!=startIdentity||startReceipt.Artifacts.Count!=2||
                !Matches(startReceipt.Artifacts[0],"verifier",startName,startBytes)||
                !Matches(startReceipt.Artifacts[1],"verifier",inventoryName,inventoryBytes)||
                !start.IsSuccess||start.Value is null||start.Value.Identity!=startIdentity||start.Value.VerifierCommit!=options.SourceCommit||
                start.Value.Topic!=RunWindow.Topic||start.Value.ExpectedJobNames is null||
                !start.Value.ExpectedJobNames.SequenceEqual(producers.Select(p=>p.JobName),StringComparer.Ordinal)||!inventory.IsSuccess)
                throw new InvalidOperationException("b10_repeat_start_untrusted");
            input.Start=start.Value;
            bool accepted=true;
            for(int role=0;role<2;role++)
            {
                ProducerRegistration producer=producers[role];
                string producerCompletionName=PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,repeat,producer.Identity.InstanceId);
                PublicationAcceptanceSnapshot? snapshot=receipt.B10Intake.Producers[role].PublicationAcceptance;
                try
                {
                    byte[]? producerBytes=await ReadOptional(producerCompletionName,true,true).ConfigureAwait(false);
                    if(producerBytes is null||snapshot is null)throw new InvalidOperationException("b10_publication_missing");
                    _=PhaseWireCodec.ReadProducerCompletion(producerBytes,options.Phase!,repeat,producer);
                    string prefix=PhasePaths.ProducerPrefix(options.RunId!,options.Phase!,repeat,producer.Identity.InstanceId);
                    if(snapshot.ProducerIdentity!=producer.Identity||snapshot.ProducerPrefix!=prefix||snapshot.SourceCommit!=options.SourceCommit||
                        !Matches(snapshot.StartLine,"verifier",startName,startBytes)||!Matches(snapshot.BrokerInventory,"verifier",inventoryName,inventoryBytes)||
                        !Matches(snapshot.ProducerReceipt,"ledger",producerCompletionName,producerBytes)||
                        snapshot.Objects.Count!=6||!snapshot.Objects.Select(r=>r.Name).SequenceEqual(PublicationAcceptance.PublicationNames.Select(n=>prefix+"publication/"+n),StringComparer.Ordinal)||
                        snapshot.Objects.Any(r=>r.Container!="ledger"))throw new InvalidOperationException("b10_publication_reference_invalid");
                    if(result.B10WindowChecksComplete==true&&result.Status!=VerifierResultStatus.Invalid)
                    {
                        string ledgerName=PhasePaths.Publication(options.RunId!,options.Phase!,repeat,producer.Identity.InstanceId,"ledger.jsonl");
                        ArtifactReference ledgerReference=snapshot.Objects.Single(r=>r.Name==ledgerName);
                        byte[] ledgerBytes=await ReadRequired(ledgerName,true,false).ConfigureAwait(false);
                        if(!Matches(ledgerReference,"ledger",ledgerName,ledgerBytes))throw new InvalidOperationException("b10_ledger_reference_invalid");
                        ParseResult<IReadOnlyList<LedgerRow>> ledger=LedgerReader.ReadRows(ledgerBytes,limits,inputBudget);
                        if(!ledger.IsSuccess||ledger.Value is null)throw new InvalidOperationException("b10_ledger_invalid");
                        input.Ledgers.Add(ledger.Value);
                    }
                }
                catch(Exception ex) when(ex is FormatException or JsonException or InvalidOperationException or ArgumentException or IOException or Azure.RequestFailedException)
                {
                    accepted=false;
                    input.Reasons=["b10_publication_unaccepted"];
                }
            }
            input.Eligible=accepted&&result.B10WindowChecksComplete==true&&result.Status!=VerifierResultStatus.Invalid&&
                receipt.RepeatChecks is { ProducerJobBinding:"pass",RegisteredProducerCount:"pass",BrokerInventoryCoverage:"pass" }&&input.Ledgers.Count==2;
            if(!input.Eligible&&input.Reasons.Count==0)input.Reasons=[result.Status==VerifierResultStatus.Invalid?"trusted_result_invalid":"b10_window_checks_incomplete"];
        }

        bool ReceiptBinds(WorkerReceipt receipt,string mode,int repeat,string job)=>
            receipt.Mode==mode&&receipt.ScenarioId=="B10"&&receipt.ConfigurationId==options.ConfigurationId&&receipt.RunId==options.RunId&&
            receipt.Phase==options.Phase&&receipt.Repeat==repeat&&receipt.SourceCommit==options.SourceCommit&&receipt.JobName==job&&
            receipt.HandlerExitCode is 0 or 1&&receipt.CleanupCompleted;

        async Task<byte[]?> ReadOptional(string name,bool producer,bool account)
        {
            byte[]? bytes=await Bounded(ct=>producer?producerStore.ReadBoundedAsync(name,limits.MaxArtifactBytes,ct):
                verifierStore.ReadBoundedAsync(name,limits.MaxArtifactBytes,ct),options.BlobIoTimeout!.Value,cancellationToken).ConfigureAwait(false);
            if(bytes is null)return null;
            if(bytes.LongLength>limits.MaxArtifactBytes)throw new InputLimitExceededException("The sweep object exceeds MaxArtifactBytes.");
            if(account)
            {
                inputBudget.ConsumeBytes(bytes.LongLength);
                ParseResult<JsonDocument> document=StrictJson.ParseDocument(bytes,name,limits,null);
                if(document.IsSuccess)
                {
                    using JsonDocument parsed=document.Value!;
                    inputBudget.ConsumeEntries(Entries(parsed.RootElement));
                }
            }
            return bytes;
        }
        async Task<byte[]> ReadRequired(string name,bool producer,bool account)=>
            await ReadOptional(name,producer,account).ConfigureAwait(false)??throw new InvalidOperationException("b10_sweep_input_missing");
        async Task<byte[]> ReadUncharged(string name,bool producer,CancellationToken ct)
        {
            byte[]? bytes=await Bounded(token=>producer?producerStore.ReadBoundedAsync(name,limits.MaxArtifactBytes,token):
                verifierStore.ReadBoundedAsync(name,limits.MaxArtifactBytes,token),options.BlobIoTimeout!.Value,ct).ConfigureAwait(false);
            if(bytes is null)throw new InvalidOperationException("b10_sweep_input_missing");
            if(bytes.LongLength>limits.MaxArtifactBytes)throw new InputLimitExceededException("The sweep object exceeds MaxArtifactBytes.");
            return bytes;
        }
    }

    private static bool BindsIntake(B10IntakeEvidence intake,IReadOnlyList<ProducerRegistration> producers)=>
        intake.Producers.Count==2&&intake.Phase==producers[0].Phase&&intake.RepeatNumber==producers[0].RepeatNumber&&
        intake.Producers.Zip(producers).All(p=>p.First.Identity==p.Second.Identity&&p.First.JobName==p.Second.JobName&&p.First.PredecessorInstanceId==p.Second.PredecessorInstanceId);

    private static bool SameIntake(B10IntakeEvidence left,B10IntakeEvidence right)=>
        left.Phase==right.Phase&&left.RepeatNumber==right.RepeatNumber&&left.Producers.Count==right.Producers.Count&&
        left.Producers.Zip(right.Producers).All(p=>p.First.Identity==p.Second.Identity&&p.First.JobName==p.Second.JobName&&
            p.First.PredecessorInstanceId==p.Second.PredecessorInstanceId&&SameSnapshot(p.First.PublicationAcceptance,p.Second.PublicationAcceptance));

    private static bool SameSnapshot(PublicationAcceptanceSnapshot? left,PublicationAcceptanceSnapshot? right)=>
        left is null?right is null:right is not null&&left.ProducerPrefix==right.ProducerPrefix&&left.ProducerIdentity==right.ProducerIdentity&&
        left.SourceCommit==right.SourceCommit&&left.StartLine==right.StartLine&&left.BrokerInventory==right.BrokerInventory&&
        left.ProducerReceipt==right.ProducerReceipt&&left.Objects.SequenceEqual(right.Objects);

    private static ArtifactReference Reference(string container,string name,byte[] bytes)=>
        new(container,name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static bool Matches(ArtifactReference reference,string container,string name,byte[] bytes)=>reference==Reference(container,name,bytes);
    private static long Entries(JsonElement element)=>element.ValueKind switch
    {
        JsonValueKind.Object=>element.EnumerateObject().Sum(p=>checked(1+Entries(p.Value))),
        JsonValueKind.Array=>element.EnumerateArray().Sum(p=>checked(1+Entries(p))),
        _=>0
    };
    private static bool IsEvidenceFailure(Exception exception,CancellationToken caller)=>
        exception is FormatException or JsonException or InvalidOperationException or ArgumentException or OverflowException or
            TimeoutException or InputLimitExceededException or VerifierKafkaOperationException or KafkaException or IOException or Azure.RequestFailedException||
        exception is OperationCanceledException&&!caller.IsCancellationRequested;

    private async Task<T> Bounded<T>(Func<CancellationToken,Task<T>> operation,TimeSpan timeout,CancellationToken cancellationToken)
    {
        long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(timeout,clock);
        using var local=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token);
        T value=await operation(local.Token).ConfigureAwait(false);CheckDeadline(origin,timeout,local.Token);return value;
    }
    private async Task Bounded(Func<CancellationToken,Task> operation,TimeSpan timeout,CancellationToken cancellationToken)
    {
        long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(timeout,clock);
        using var local=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token);
        await operation(local.Token).ConfigureAwait(false);CheckDeadline(origin,timeout,local.Token);
    }
    private void CheckDeadline(long origin,TimeSpan timeout,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();if(clock.GetElapsedTime(origin)>=timeout)throw new TimeoutException("The B10 sweep I/O budget expired.");
    }

    private sealed class RepeatInput(int number)
    {
        internal int Number { get; }=number;
        internal ArtifactReference? ResultReference { get; set; }
        internal ArtifactReference? ReceiptReference { get; set; }
        internal VerifierResult? Result { get; set; }
        internal StartLineNote? Start { get; set; }
        internal IReadOnlyList<ProducerRegistration> Producers { get; set; }=[];
        internal List<IReadOnlyList<LedgerRow>> Ledgers { get; }=[];
        internal DateTimeOffset CompletedUtc { get; set; }
        internal bool Eligible { get; set; }
        internal bool Final { get; set; }
        internal VerifierResultStatus Status { get; set; }=VerifierResultStatus.Incomplete;
        internal OutcomeCounts? Counts { get; set; }
        internal B10LateObservation? Observation { get; set; }
        internal IReadOnlyList<string> Reasons { get; set; }=[];
    }
}
