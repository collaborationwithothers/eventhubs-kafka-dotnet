using System.Security.Cryptography;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.Publication;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

/// <summary>Discovers one producer and independently accepts its fixed publication before judging a Kafka window.</summary>
internal sealed class VerifyRunHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore verifierStore, IProducerArtifactStore producerStore,
    ScenarioLoader scenarioLoader, RegisteredProducerConfigurationResolver configurationResolver,
    VerifierCompletionState? completion = null) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.Verify;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options=configured.Value;
        DateTimeOffset started=clock.GetUtcNow();
        string repeatPrefix=PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,options.Repeat!.Value);
        string producerRoot=PhasePaths.ProducersPrefix(options.RunId!,options.Phase!,options.Repeat.Value);
        var inputLimits=new InputLimits(options.MaxInputDepth!.Value,options.MaxArtifactBytes!.Value,options.MaxTotalInputBytes!.Value,
            options.MaxRowBytes!.Value,options.MaxRowsPerArtifact!.Value,options.MaxInputEntries!.Value,options.MaxDiagnosticBytes!.Value);
        var budget=new InputBudget(inputLimits);
        var outputLimits=new OutputLimits(options.MaxOutputBytes!.Value,options.MaxDiagnosticsPerCheck!.Value,options.MaxDiagnosticBytes!.Value);

        BlobListing registrations=await ReadLedgerListing(producerRoot,cancellationToken).ConfigureAwait(false);
        if(!registrations.IsComplete)throw new InvalidOperationException(registrations.FailureCode??"listing_incomplete");
        string[] registrationNames=registrations.Names.Where(n=>n.EndsWith("/registration.json",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        if(registrationNames.Length!=1)throw new InvalidOperationException(registrationNames.Length==0?"producer_registration_missing":"producer_registration_not_singleton");
        string registrationName=registrationNames[0];
        string instance=RegistrationInstance(producerRoot,registrationName);
        byte[] registrationBytes=await ReadLedgerRequired(registrationName,cancellationToken).ConfigureAwait(false);
        budget.ConsumeBytes(registrationBytes.Length);
        ProducerRegistration registration=PhaseWireCodec.ReadRegistration(registrationBytes);
        if(registration.Identity.InstanceId!=instance||registration.Identity.ExpectedInstanceId!=instance||registration.JobName!=options.ExpectedInstanceId||
            registration.Identity.RunId!=options.RunId||registration.Phase!=options.Phase||registration.RepeatNumber!=options.Repeat||registration.SourceCommit!=options.SourceCommit)
            throw new InvalidOperationException("producer_registration_binding_invalid");

        byte[]? startReceiptBytes=await ReadVerifier(PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,options.Repeat.Value),cancellationToken).ConfigureAwait(false);
        WorkerReceipt? startReceipt=null;
        if(startReceiptBytes is not null)
        {
            budget.ConsumeBytes(startReceiptBytes.Length);
            try { startReceipt=PhaseWireCodec.ReadReceipt(startReceiptBytes); }
            catch(FormatException) { /* retain the missing-evidence path below */ }
            catch(System.Text.Json.JsonException) { /* malformed completion remains missing evidence */ }
        }
        string startJob=PhaseIdentity.StartLineJobName(options.RunId!,options.Phase!,options.Repeat.Value);
        if(startReceipt is not null&&(startReceipt.Mode!="start-line"||startReceipt.JobName!=startJob||startReceipt.RunId!=options.RunId||startReceipt.Phase!=options.Phase||startReceipt.Repeat!=options.Repeat||startReceipt.SourceCommit!=options.SourceCommit))
            startReceipt=null;
        string startName=PhasePaths.StartLine(options.RunId!,options.Phase!,options.Repeat.Value);
        string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,options.Repeat.Value);
        if(startReceipt is { Artifacts.Count: 2 })
        {
            try
            {
                ValidateReference(startReceipt.Artifacts[0],"verifier",startName);
                ValidateReference(startReceipt.Artifacts[1],"verifier",inventoryName);
            }
            catch(InvalidOperationException) { startReceipt=null; }
        }
        else startReceipt=null;
        byte[]? noteBytes=await ReadVerifier(startName,cancellationToken).ConfigureAwait(false);
        byte[]? inventoryBytes=await ReadVerifier(inventoryName,cancellationToken).ConfigureAwait(false);
        if(noteBytes is not null)budget.ConsumeBytes(noteBytes.Length);if(inventoryBytes is not null)budget.ConsumeBytes(inventoryBytes.Length);
        if(noteBytes is not null&&startReceipt is not null)VerifyHashAndLength(startReceipt.Artifacts[0],noteBytes);
        if(inventoryBytes is not null&&startReceipt is not null)VerifyHashAndLength(startReceipt.Artifacts[1],inventoryBytes);
        RunIdentity expectedStartIdentity=ExpectedStartIdentity(options);
        ParseResult<StartLineNote> start=noteBytes is null
            ?ParseResult<StartLineNote>.Failure(new StructuralProblem(7,"missing_input",startName,null,null,null))
            :StartLineNoteCodec.Read(noteBytes,startName,inputLimits,options.MaxPartitions!.Value);
        ParseResult<BrokerInventory> inventory=inventoryBytes is null
            ?ParseResult<BrokerInventory>.Failure(new StructuralProblem(6,"missing_input",inventoryName,null,null,null))
            :BrokerInventoryReader.Read(inventoryBytes,inventoryName,expectedStartIdentity,options.SourceCommit!,inputLimits);

        string producerCompletionName=PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,options.Repeat.Value,instance);
        byte[]? completionBytes=await ReadLedgerOptional(producerCompletionName,cancellationToken).ConfigureAwait(false);
        ProducerCompletionReceipt? producerReceipt=null;
        if(completionBytes is not null)
        {
            budget.ConsumeBytes(completionBytes.Length);
            try { producerReceipt=PhaseWireCodec.ReadProducerCompletion(completionBytes); }
            catch(FormatException) { /* malformed producer receipt rejects publication; diagnostics remain readable */ }
            catch(System.Text.Json.JsonException) { /* malformed producer receipt rejects publication; diagnostics remain readable */ }
        }
        string producerPrefix=PhasePaths.ProducerPrefix(options.RunId!,options.Phase!,options.Repeat.Value,instance);
        BlobListing publicationListing=await ReadLedgerListing(producerPrefix+"publication/",cancellationToken).ConfigureAwait(false);
        string[] readNames=["index.json","ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"];
        var raw=new Dictionary<string,byte[]?>(StringComparer.Ordinal);
        long retainedPublicationBytes=0;
        foreach(string name in readNames)
        {
            byte[]? downloaded=await ReadLedgerOptional(PhasePaths.Publication(options.RunId!,options.Phase!,options.Repeat.Value,instance,name),cancellationToken).ConfigureAwait(false);
            if(downloaded is not null)
            {
                retainedPublicationBytes=checked(retainedPublicationBytes+downloaded.LongLength);
                if(retainedPublicationBytes>Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes))
                    throw new InputLimitExceededException("Combined publication buffers exceed the configured retained-input limit.");
            }
            raw[name]=downloaded;
        }

        ParseResult<ArtifactIndex> index=ArtifactIndexReader.Read(raw["index.json"],inputLimits,budget);
        ParseResult<IReadOnlyList<LedgerRow>> ledger=LedgerReader.ReadRows(Memory("ledger.jsonl"),inputLimits,budget);
        ParseResult<SealedRunManifest> manifest=ManifestReader.Read(Memory("manifest.json"),inputLimits,budget,options.MaxInventoryEntries!.Value);
        ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics=CapturedEvidenceReader.ReadStatistics(Memory("statistics.jsonl"),"statistics.jsonl",inputLimits,budget);
        ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle=CapturedEvidenceReader.ReadLifecycle(Memory("request-lifecycle.jsonl"),"request-lifecycle.jsonl",inputLimits,budget);
        ParseResult<CapturedCapabilities> capabilities=CapturedEvidenceReader.ReadCapabilities(Memory("capabilities.json"),"capabilities.json",inputLimits,budget);
        ReportAssociation[] reports=statistics.Value?.Select(envelope=>new ReportAssociation(envelope,
            NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson),"statistics.jsonl",inputLimits,budget,options.MaxNativeReportBytes!.Value,options.MaxBrokerEntries!.Value))).ToArray()??[];
        ArtifactReference Ref(string container,string name,byte[] bytes)=>new(container,name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        PublicationReadOutcome accepted=completionBytes is null||producerReceipt is null||startReceipt is null||inventoryBytes is null
            ?new(null,raw,["producer_completion_unavailable"],true)
            :start.Value is null
            ?new(null,raw,["start_line_invalid"],start.IsExecutionFailure)
            :PublicationAcceptance.Evaluate(publicationListing,producerPrefix,raw,registration,producerReceipt,
                index.Value,manifest.Value,start.Value,inventory.Value,options.SourceCommit!,Ref("verifier",startName,noteBytes??[]),Ref("verifier",inventoryName,inventoryBytes!),Ref("ledger",producerCompletionName,completionBytes!),
                options.MaxArtifactBytes.Value,Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes));
        bool diagnosticsSelected=false;
        if(!accepted.Accepted)
        {
            string diagnosticPrefix=producerPrefix+"diagnostics/";
            BlobListing diagnosticListing=await ReadLedgerListing(diagnosticPrefix,cancellationToken).ConfigureAwait(false);
            DiagnosticPublicationReadOutcome diagnostic=await DiagnosticPublicationReader.ReadAsync(diagnosticListing,diagnosticPrefix,
                ReadLedgerOptional,options.MaxArtifactBytes.Value,Math.Max(0,Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes)-retainedPublicationBytes),cancellationToken).ConfigureAwait(false);
            if(diagnostic.HasPayload)
            {
                diagnosticsSelected=true;
                foreach(string name in PublicationAcceptance.PublicationNames)raw[name]=null;
                foreach((string name,byte[]? bytes) in diagnostic.Payloads)raw[name]=bytes;
                // The failed-evidence protocol intentionally has no index. Keep its normal
                // unowned execution failure so check 3 cannot infer identity from registration.
                index=ArtifactIndexReader.Read(null,inputLimits,budget);
                ledger=LedgerReader.ReadRows(Memory("ledger.jsonl"),inputLimits,budget);
                manifest=ManifestReader.Read(Memory("manifest.json"),inputLimits,budget,options.MaxInventoryEntries.Value);
                statistics=CapturedEvidenceReader.ReadStatistics(Memory("statistics.jsonl"),"statistics.jsonl",inputLimits,budget);
                lifecycle=CapturedEvidenceReader.ReadLifecycle(Memory("request-lifecycle.jsonl"),"request-lifecycle.jsonl",inputLimits,budget);
                capabilities=CapturedEvidenceReader.ReadCapabilities(Memory("capabilities.json"),"capabilities.json",inputLimits,budget);
                reports=statistics.Value?.Select(envelope=>new ReportAssociation(envelope,
                    NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson),"statistics.jsonl",inputLimits,budget,options.MaxNativeReportBytes!.Value,options.MaxBrokerEntries!.Value))).ToArray()??[];
            }
            else if(index.IsExecutionFailure||index.Value is null)
            {
                throw new InvalidOperationException("publication_index_unavailable_without_diagnostics");
            }
        }
        long scenarioOrigin=clock.GetTimestamp();
        using(var deadline=new CancellationTokenSource(options.IoTimeout!.Value,clock))
        using(var local=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token))
        {
            LoadedScenario scenario=await scenarioLoader.LoadAsync("A01",local.Token).ConfigureAwait(false);
            CheckDeadline(scenarioOrigin,options.IoTimeout.Value,local.Token);
            ResolvedScenario resolved=configurationResolver.Resolve(scenario,registration);
            ResolvedRoleConfiguration producer=resolved.Configurations[options.ConfigurationId!].Roles[KafkaClientRole.LoadGenProducer];
            IReadOnlyList<string> expectedBrokers=inventory.Value?.Brokers.Select(b=>b.Name).Order(StringComparer.Ordinal).ToArray()??[];

            VerifierKafkaSession? session=null;ITimer? safetyTimer=null;using var kafkaWork=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            byte[]? resultBytes=null;VerifierResultStatus status=VerifierResultStatus.Incomplete;string coverage="unavailable";
            try
            {
                var assessments=new List<CheckAssessment>();IReadOnlyDictionary<int,long>? ends=null;long origin=0;
                try
                {
                    session=sessions.Create(kafkaWork.Token);session.InstallToken(cancellationToken);
                    ends=session.ReadHighWatermarks(()=>{origin=clock.GetTimestamp();safetyTimer=clock.CreateTimer(_=>kafkaWork.Cancel(),null,options.SafetyStop!.Value,Timeout.InfiniteTimeSpan);},()=>options.SafetyStop!.Value-clock.GetElapsedTime(origin),kafkaWork.Token);
                    assessments.Add(RunWindow.Pass(11));
                }
                catch(VerifierKafkaOperationException){cancellationToken.ThrowIfCancellationRequested();assessments.Add(RunWindow.Failure(11));}
                RunWindow window=RunWindow.Create(start,registration.Identity,expectedStartIdentity,ends);assessments.AddRange(window.Assessments);
                WindowDrainResult? drain=null;if(window.CanAssign){drain=WindowConsumer.Drain(session!.Consumer,window,clock,origin,options.SafetyStop!.Value,options.PollSlice!.Value,
                    new ConsumedRecordBudget(options.MaxConsumedRecords!.Value,options.MaxConsumedBytes!.Value),cancellationToken,kafkaWork.Token);assessments.AddRange(drain.Assessments);}
                var request=new RunJudgementRequest(registration.Identity,index,ledger,manifest,statistics,lifecycle,capabilities,reports,expectedBrokers,producer,drain?.Records??[],assessments,outputLimits,inventory.Value is not null);
                // A readable but unaccepted publication remains diagnostic evidence. It may
                // produce an incomplete/invalid result, but only the accepted snapshot below
                // can authorize phase-level J2.
                RunJudgementResult judgement=diagnosticsSelected||index.IsExecutionFailure
                    ?RunJudgement.EvaluateDiagnostic(request)
                    :RunJudgement.Evaluate(request);
                judgement=ApplyPublicationOutcome(judgement,accepted);
                A01EvidenceRecording a01=options.ConfigurationId=="C3"?A01EvidenceRecorder.RecordC3(request,judgement):A01EvidenceRecorder.Record(request,judgement);
                if(!accepted.Accepted)
                    a01=a01 with{ProbeObservation=new ProbeRepeatObservation(false,false,null,null,null,null)};
                status=judgement.Status??VerifierResultStatus.Incomplete;
                var hashes=accepted.AcceptedSnapshot is null?[]:PublicationAcceptance.DescriptorNames
                    .Select(n=>{byte[] bytes=raw[n]!;return new JudgedFileHash(n,Convert.ToHexStringLower(SHA256.HashData(bytes)));}).ToArray();
                var result=new VerifierResult(1,registration.Identity,manifest.Value?.GeneratorVersion,manifest.Value?.RuntimeVersions,options.VerifierCommit!,typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(),Library.VersionString,
                    status,judgement.FailedChecks,start.Value?.PartitionHighs.Select(p=>new WatermarkOffset(p.Partition,p.High)).ToArray()??[],ends?.OrderBy(p=>p.Key).Select(p=>new WatermarkOffset(p.Key,p.Value)).ToArray(),
                    Milliseconds(drain?.DrainDuration),Milliseconds(drain?.StopDuration),judgement.Counts,judgement.BrokerCount,judgement.ConfigurationFidelity,a01.ProbeObservation,a01.RecordedEvidence,hashes,
                    judgement.Diagnostics.Concat(a01.Diagnostics).ToArray(),a01.WorkloadObservation);
                resultBytes=VerifierResultCodec.Write(result,outputLimits);
                coverage=judgement.Assessments.FirstOrDefault(a=>a.CheckNumber==24) is { Evaluation:AssessmentEvaluation.Evaluated,Disposition:AssessmentDisposition.Pass }?"pass":
                    judgement.Assessments.FirstOrDefault(a=>a.CheckNumber==24)?.Disposition==AssessmentDisposition.Fail?"fail":"unavailable";
            }
            finally
            {
                safetyTimer?.Dispose();long cleanup=clock.GetTimestamp();using var cleanupDeadline=new CancellationTokenSource(options.CleanupTimeout!.Value,clock);session?.Cleanup(cleanupDeadline.Token);CheckDeadline(cleanup,options.CleanupTimeout.Value,cleanupDeadline.Token);
            }
            if(resultBytes is null)throw new InvalidOperationException("verifier_result_not_built");
            // Publish only after native cleanup has completed successfully.
            cancellationToken.ThrowIfCancellationRequested();
            string resultName=PhasePaths.Result(options.RunId!,options.Phase!,options.Repeat.Value);
            long resultOrigin=clock.GetTimestamp();using var writeBudget=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var write=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,writeBudget.Token);
            await verifierStore.CreateAsync(resultName,resultBytes,write.Token).ConfigureAwait(false);CheckDeadline(resultOrigin,options.BlobIoTimeout.Value,write.Token);
            var resultReference=new ArtifactReference("verifier",resultName,resultBytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(resultBytes)));
            var verifyReceipt=new WorkerReceipt(1,"verify",options.ScenarioId!,options.ConfigurationId!,options.RunId!,options.Phase!,options.Repeat,
                options.JobName!,options.PodName!,registration.Identity,options.SourceCommit!,status==VerifierResultStatus.CompleteValid?0:1,true,
                started.ToString("O",System.Globalization.CultureInfo.InvariantCulture),clock.GetUtcNow().ToString("O",System.Globalization.CultureInfo.InvariantCulture),
                [resultReference],accepted.AcceptedSnapshot,new RepeatChecks("pass","pass",inventory.Value is null?"unavailable":coverage));
            completion?.SetPendingBlobReceipt(options.VerifierContainer!,PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,options.Repeat.Value),PhaseWireCodec.WriteReceipt(verifyReceipt));
            return status==VerifierResultStatus.CompleteValid?0:1;
        }

        ReadOnlyMemory<byte>? Memory(string name)=>raw[name] is byte[] bytes?(ReadOnlyMemory<byte>?)new ReadOnlyMemory<byte>(bytes):null;
        async Task<BlobListing> ReadLedgerListing(string prefix,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);var value=await producerStore.ListAsync(prefix,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
        async Task<byte[]?> ReadLedgerOptional(string name,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);byte[]? value=await producerStore.ReadAsync(name,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
        async Task<byte[]> ReadLedgerRequired(string name,CancellationToken ct)=>await ReadLedgerOptional(name,ct).ConfigureAwait(false)??throw new InvalidOperationException("required_ledger_object_missing:"+name);
        async Task<byte[]?> ReadVerifier(string name,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);byte[]? value=await verifierStore.ReadAsync(name,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
    }

    private static string RegistrationInstance(string root,string name)
    {
        if(!name.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("registration_prefix_escape");
        string tail=name[root.Length..];string[] parts=tail.Split('/');
        if(parts.Length!=2||parts[1]!="registration.json"||parts[0].Length is 0 or >128||parts[0].Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))throw new InvalidOperationException("registration_path_invalid");
        return parts[0];
    }
    private static RunIdentity ExpectedStartIdentity(VerifierHostOptions o){string loadgen=PhaseIdentity.JobName(o.RunId!,o.Phase!,o.Repeat!.Value);return new(o.ScenarioId!,o.ConfigurationId!,o.RunId!,loadgen,loadgen,loadgen);}
    private static RunJudgementResult ApplyPublicationOutcome(RunJudgementResult judgement,PublicationReadOutcome accepted)
    {
        if(accepted.Accepted)return judgement;
        // Publication acceptance is an independent phase-level authorization. It
        // must not replace the numbered check that owns the underlying observation.
        VerifierResultStatus status=judgement.Status==VerifierResultStatus.Invalid||!accepted.Incomplete
            ?VerifierResultStatus.Invalid:VerifierResultStatus.Incomplete;
        return judgement with
        {
            Status=status,
            Counts=null,
            Diagnostics=judgement.Diagnostics.Append(new ResultDiagnostic(0,"publication_unaccepted","index.json",null,"artifacts",null)).ToArray()
        };
    }
    private static void ValidateReference(ArtifactReference r,string container,string name){if(r.Container!=container||r.Name!=name||r.Length<0)throw new InvalidOperationException("artifact_reference_invalid");}
    private static void VerifyHashAndLength(ArtifactReference reference,byte[] bytes){if(reference.Length!=bytes.LongLength||reference.Sha256!=Convert.ToHexStringLower(SHA256.HashData(bytes)))throw new InvalidOperationException("artifact_reference_hash_mismatch");}
    private void CheckDeadline(long origin,TimeSpan budget,CancellationToken ct){ct.ThrowIfCancellationRequested();if(clock.GetElapsedTime(origin)>=budget)throw new TimeoutException("The verifier operation budget expired.");}
    private static decimal? Milliseconds(TimeSpan? duration)=>duration is null?null:duration.Value.Ticks/(decimal)TimeSpan.TicksPerMillisecond;
}
