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
using Parchwell.Verifier.VerifyRun.B04;
using Parchwell.Verifier.VerifyRun.LedgerBatch;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.Publication;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

/// <summary>Discovers the permitted producers and independently accepts their publications before judging one Kafka window.</summary>
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
        bool b10=options.ScenarioId=="B10";
        if(b10&&registrationNames.Length!=2)throw new InvalidOperationException("b10_registration_count_invalid");
        if(!b10&&registrationNames.Length!=1)throw new InvalidOperationException(registrationNames.Length==0?"producer_registration_missing":"producer_registration_not_singleton");
        var discovered=new List<(ProducerRegistration Registration,string PathInstance)>();
        foreach(string name in registrationNames)
        {
            string pathInstance=RegistrationInstance(producerRoot,name);
            byte[] bytes=await ReadLedgerRequired(name,cancellationToken).ConfigureAwait(false);
            budget.ConsumeBytes(bytes.Length);
            discovered.Add((PhaseWireCodec.ReadRegistration(bytes),pathInstance));
        }
        IReadOnlyList<ProducerRegistration> producers;
        if(b10)
            producers=B10IntakeChecks.BindRegistrations(discovered,options.ScenarioId!,options.ConfigurationId!,options.RunId!,
                options.Phase!,options.Repeat.Value,options.SourceCommit!,options.ExpectedProducerJobNames);
        else
        {
            (ProducerRegistration single,string pathInstance)=discovered[0];
            if(single.Identity.InstanceId!=pathInstance||single.Identity.ExpectedInstanceId!=pathInstance||single.JobName!=options.ExpectedInstanceId||
                single.Identity.RunId!=options.RunId||single.Phase!=options.Phase||single.RepeatNumber!=options.Repeat||single.SourceCommit!=options.SourceCommit)
                throw new InvalidOperationException("producer_registration_binding_invalid");
            producers=[single];
        }
        ProducerRegistration registration=producers[0];

        byte[]? startReceiptBytes=await ReadVerifier(PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,options.Repeat.Value),cancellationToken).ConfigureAwait(false);
        WorkerReceipt? startReceipt=null;
        StructuralProblem? startReceiptProblem=null;
        if(startReceiptBytes is not null)
        {
            budget.ConsumeBytes(startReceiptBytes.Length);
            try { startReceipt=PhaseWireCodec.ReadReceipt(startReceiptBytes); }
            catch(PhaseWireIdentityException) { startReceiptProblem=new StructuralProblem(7,"start_line_receipt_binding_invalid","start-line",null,"identity",null); }
            catch(FormatException) { /* retain the missing-evidence path below */ }
            catch(System.Text.Json.JsonException) { /* malformed completion remains missing evidence */ }
        }
        string startJob=PhaseIdentity.StartLineJobName(options.RunId!,options.Phase!,options.Repeat.Value);
        if(startReceipt is not null&&(startReceipt.Mode!="start-line"||startReceipt.JobName!=startJob||startReceipt.RunId!=options.RunId||startReceipt.Phase!=options.Phase||startReceipt.Repeat!=options.Repeat||startReceipt.SourceCommit!=options.SourceCommit))
        {
            string field=startReceipt.SourceCommit!=options.SourceCommit?"source_commit":"identity";
            startReceiptProblem=new StructuralProblem(7,"start_line_receipt_binding_invalid","start-line",null,field,null);
            startReceipt=null;
        }
        string startName=PhasePaths.StartLine(options.RunId!,options.Phase!,options.Repeat.Value);
        string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,options.Repeat.Value);
        if(startReceipt is { Artifacts.Count: 2 })
        {
            try
            {
                ValidateReference(startReceipt.Artifacts[0],"verifier",startName);
                ValidateReference(startReceipt.Artifacts[1],"verifier",inventoryName);
            }
            catch(InvalidOperationException)
            {
                startReceiptProblem=new StructuralProblem(7,"artifact_reference_invalid","start-line",null,"reference",null);
                startReceipt=null;
            }
        }
        else if(startReceipt is not null)
        {
            startReceiptProblem=new StructuralProblem(7,"artifact_reference_invalid","start-line",null,"reference",null);
            startReceipt=null;
        }
        byte[]? noteBytes=await ReadVerifier(startName,cancellationToken).ConfigureAwait(false);
        byte[]? inventoryBytes=await ReadVerifier(inventoryName,cancellationToken).ConfigureAwait(false);
        if(noteBytes is not null)budget.ConsumeBytes(noteBytes.Length);if(inventoryBytes is not null)budget.ConsumeBytes(inventoryBytes.Length);
        StructuralProblem? noteReferenceProblem=null;
        StructuralProblem? inventoryReferenceProblem=null;
        if(noteBytes is not null&&startReceipt is not null)
        {
            try { VerifyHashAndLength(startReceipt.Artifacts[0],noteBytes); }
            catch(InvalidOperationException) { noteReferenceProblem=new StructuralProblem(7,"artifact_reference_hash_mismatch","start-line",null,"reference",null); }
        }
        if(inventoryBytes is not null&&startReceipt is not null)
        {
            try { VerifyHashAndLength(startReceipt.Artifacts[1],inventoryBytes); }
            catch(InvalidOperationException) { inventoryReferenceProblem=new StructuralProblem(6,"artifact_reference_hash_mismatch","broker-inventory",null,"reference",null); }
        }
        RunIdentity expectedStartIdentity=ExpectedStartIdentity(options);
        ParseResult<StartLineNote> start=noteBytes is null
            ?ParseResult<StartLineNote>.Failure(new StructuralProblem(7,"missing_input",startName,null,null,null))
            :StartLineNoteCodec.Read(noteBytes,startName,inputLimits,options.MaxPartitions!.Value);
        if(startReceiptProblem is not null||noteReferenceProblem is not null)
            start=ParseResult<StartLineNote>.Failure(startReceiptProblem??noteReferenceProblem!);
        ParseResult<BrokerInventory> inventory=inventoryBytes is null
            ?ParseResult<BrokerInventory>.Failure(new StructuralProblem(6,"missing_input",inventoryName,null,null,null))
            :BrokerInventoryReader.Read(inventoryBytes,inventoryName,expectedStartIdentity,options.SourceCommit!,inputLimits);
        if(inventoryReferenceProblem is not null)
            inventory=ParseResult<BrokerInventory>.Failure(inventoryReferenceProblem);
        if(inventory.IsExecutionFailure)
            throw new InvalidOperationException("Broker inventory parsing ended with an execution failure.");
        List<StructuralProblem> sharedCheck6Problems=inventory.Problems
            .Where(problem=>problem.Code!="missing_input").ToList();

        long retainedPublicationBytes=0;
        var inputs=new List<ProducerInputs>();
        for(int producerIndex=0;producerIndex<producers.Count;producerIndex++)
            inputs.Add(await ReadProducer(producers[producerIndex],producerIndex==0).ConfigureAwait(false));
        ProducerInputs firstInput=inputs[0];
        B10IntakeEvidence? b10Intake=b10?new(options.Phase!,options.Repeat.Value,inputs.Select(input=>
            new B10ProducerEvidence(input.Registration.Identity,input.Registration.JobName,input.Registration.PredecessorInstanceId,input.Acceptance.AcceptedSnapshot)).ToArray()):null;
        long scenarioOrigin=clock.GetTimestamp();
        using(var deadline=new CancellationTokenSource(options.IoTimeout!.Value,clock))
        using(var local=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token))
        {
            LoadedScenario scenario=await scenarioLoader.LoadAsync(options.ScenarioId!,local.Token).ConfigureAwait(false);
            CheckDeadline(scenarioOrigin,options.IoTimeout.Value,local.Token);
            ResolvedRoleConfiguration[] configurations=producers.Select(producer=>configurationResolver.Resolve(scenario,producer)
                .Configurations[options.ConfigurationId!].Roles[KafkaClientRole.LoadGenProducer]).ToArray();
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
                RunWindow window=b10?RunWindow.Create(start,producers.Select(producer=>producer.Identity).ToArray(),expectedStartIdentity,options.ExpectedProducerJobNames,ends)
                    :RunWindow.Create(start,registration.Identity,expectedStartIdentity,ends);
                assessments.AddRange(window.Assessments);
                WindowDrainResult? drain=null;if(window.CanAssign){drain=WindowConsumer.Drain(session!.Consumer,window,clock,origin,options.SafetyStop!.Value,options.PollSlice!.Value,
                    new ConsumedRecordBudget(options.MaxConsumedRecords!.Value,options.MaxConsumedBytes!.Value),cancellationToken,kafkaWork.Token);assessments.AddRange(drain.Assessments);}
                var requests=new List<RunJudgementRequest>();
                var judgements=new List<RunJudgementResult>();
                for(int producerIndex=0;producerIndex<inputs.Count;producerIndex++)
                {
                    ProducerInputs input=inputs[producerIndex];
                    IReadOnlyList<ConsumedRecord> records=drain?.Records??[];
                    if(b10)
                        records=records.Where(record=>producerIndex==1
                            ?record.InstanceId==producers[1].Identity.InstanceId
                            :record.InstanceId!=producers[1].Identity.InstanceId).ToArray();
                    var perInstance=new RunJudgementRequest(input.Registration.Identity,input.Index,input.Ledger,input.Manifest,input.Statistics,
                        input.Lifecycle,input.Capabilities,input.Reports,expectedBrokers,configurations[producerIndex],records,assessments,
                        outputLimits,inventory.Value is not null,input.Check6Problems);
                    requests.Add(perInstance);
                    RunJudgementResult evaluated=input.DiagnosticsSelected||input.Index.IsExecutionFailure
                        ?RunJudgement.EvaluateDiagnostic(perInstance):RunJudgement.Evaluate(perInstance);
                    judgements.Add(ApplyPublicationOutcome(evaluated,input.Acceptance));
                }
                RunJudgementRequest request=requests[0];
                RunJudgementResult judgement=b10?B10RepeatChecks.Evaluate(requests[0],requests[1],judgements[0],judgements[1],
                    producers.Select(producer=>producer.Identity.InstanceId).ToArray(),outputLimits).Judgement:judgements[0];
                A01EvidenceRecording a01=b10?new(null,null,[]):options.ConfigurationId=="C3"?A01EvidenceRecorder.RecordC3(request,judgement):A01EvidenceRecorder.Record(request,judgement);
                B04EvidenceRecording b04=b10?new(null,[],null):B04EvidenceRecorder.Record(request);
                BatchingEvidenceRecording batching=b10?new(null,[],null):BatchingEvidenceRecorder.Record(request,judgement);
                if(!b10&&!firstInput.Acceptance.Accepted)
                    a01=a01 with{ProbeObservation=new ProbeRepeatObservation(false,false,null,null,null,null)};
                status=judgement.Status??VerifierResultStatus.Incomplete;
                var hashes=firstInput.Acceptance.AcceptedSnapshot is null?[]:PublicationAcceptance.DescriptorNames
                    .Select(n=>{byte[] bytes=firstInput.Raw[n]!;return new JudgedFileHash(n,Convert.ToHexStringLower(SHA256.HashData(bytes)));}).ToArray();
                var result=new VerifierResult(1,registration.Identity,firstInput.Manifest.Value?.GeneratorVersion,firstInput.Manifest.Value?.RuntimeVersions,options.VerifierCommit!,typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(),Library.VersionString,
                    status,judgement.FailedChecks,start.Value?.PartitionHighs.Select(p=>new WatermarkOffset(p.Partition,p.High)).ToArray(),ends?.OrderBy(p=>p.Key).Select(p=>new WatermarkOffset(p.Key,p.Value)).ToArray(),
                    Milliseconds(drain?.DrainDuration),Milliseconds(drain?.StopDuration),judgement.Counts,judgement.BrokerCount,judgement.ConfigurationFidelity,a01.ProbeObservation,a01.RecordedEvidence,hashes,
                    judgement.Diagnostics.Concat(a01.Diagnostics).Concat(b04.Diagnostics).Concat(batching.Diagnostics).ToArray(),
                    a01.WorkloadObservation??b04.WorkloadObservation??batching.WorkloadObservation,
                    b04.Evidence,batching.Evidence,b10Intake,b10?false:null);
                resultBytes=VerifierResultCodec.Write(result,outputLimits);
                coverage=b10?B10IntakeChecks.BrokerInventoryCoverage(judgements[0],judgements[1]):judgement.Assessments.FirstOrDefault(a=>a.CheckNumber==24) is { Evaluation:AssessmentEvaluation.Evaluated,Disposition:AssessmentDisposition.Pass }?"pass":
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
                [resultReference],b10?null:firstInput.Acceptance.AcceptedSnapshot,new RepeatChecks("pass","pass",inventory.Value is null?"unavailable":coverage),b10Intake);
            completion?.SetPendingBlobReceipt(options.VerifierContainer!,PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,options.Repeat.Value),PhaseWireCodec.WriteReceipt(verifyReceipt));
            return status==VerifierResultStatus.CompleteValid?0:1;
        }

        async Task<ProducerInputs> ReadProducer(ProducerRegistration producerRegistration,bool first)
        {
            string instance=producerRegistration.Identity.InstanceId;
            var knownCheck6Problems=first?new List<StructuralProblem>(sharedCheck6Problems):[];
            string producerCompletionName=PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,options.Repeat.Value,instance);
            byte[]? completionBytes=await ReadLedgerOptional(producerCompletionName,cancellationToken).ConfigureAwait(false);
            ProducerCompletionReceipt? producerReceipt=null;
            bool producerReceiptIdentityProblem=false;
            if(completionBytes is not null)
            {
                budget.ConsumeBytes(completionBytes.Length);
                try { producerReceipt=b10
                        ?PhaseWireCodec.ReadProducerCompletion(completionBytes,options.Phase!,options.Repeat.Value,producerRegistration)
                        :PhaseWireCodec.ReadProducerCompletion(completionBytes); }
                catch(PhaseWireIdentityException)
                {
                    producerReceiptIdentityProblem=true;
                    knownCheck6Problems.Add(new StructuralProblem(6,"producer_completion_invalid","producer-completion",null,"identity",null));
                }
                catch(FormatException) { /* malformed producer receipt rejects publication; diagnostics remain readable */ }
                catch(System.Text.Json.JsonException) { /* malformed producer receipt rejects publication; diagnostics remain readable */ }
            }
            string producerPrefix=PhasePaths.ProducerPrefix(options.RunId!,options.Phase!,options.Repeat.Value,instance);
            BlobListing publicationListing=await ReadLedgerListing(producerPrefix+"publication/",cancellationToken).ConfigureAwait(false);
            string[] readNames=["index.json","ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"];
            var raw=new Dictionary<string,byte[]?>(StringComparer.Ordinal);
            foreach(string name in readNames)
            {
                byte[]? downloaded=await ReadLedgerOptional(PhasePaths.Publication(options.RunId!,options.Phase!,options.Repeat.Value,instance,name),cancellationToken).ConfigureAwait(false);
                if(downloaded is not null)Retain(downloaded);
                raw[name]=downloaded;
            }

            if(b10&&first&&(raw["manifest.json"] is null||raw["ledger.jsonl"] is null))
                throw new InvalidOperationException("b10_predecessor_evidence_missing");
            ParseResult<ArtifactIndex> index=ArtifactIndexReader.Read(raw["index.json"],inputLimits,budget);
            ParseResult<IReadOnlyList<LedgerRow>> ledger=LedgerReader.ReadRows(Memory("ledger.jsonl"),inputLimits,budget);
            ParseResult<SealedRunManifest> manifest=ManifestReader.Read(Memory("manifest.json"),inputLimits,budget,options.MaxInventoryEntries!.Value);
            ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics=CapturedEvidenceReader.ReadStatistics(Memory("statistics.jsonl"),"statistics.jsonl",inputLimits,budget);
            ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle=CapturedEvidenceReader.ReadLifecycle(Memory("request-lifecycle.jsonl"),"request-lifecycle.jsonl",inputLimits,budget);
            ParseResult<CapturedCapabilities> capabilities=CapturedEvidenceReader.ReadCapabilities(Memory("capabilities.json"),"capabilities.json",inputLimits,budget,captureOnly:true);
            ReportAssociation[] reports=statistics.Value?.Select(envelope=>new ReportAssociation(envelope,
                NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson),"statistics.jsonl",inputLimits,budget,options.MaxNativeReportBytes!.Value,options.MaxBrokerEntries!.Value))).ToArray()??[];
            ArtifactReference Ref(string container,string name,byte[] bytes)=>new(container,name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));
            PublicationReadOutcome accepted=PublicationAcceptance.Evaluate(publicationListing,producerPrefix,raw,producerRegistration,producerReceipt,
                index.Value,manifest.Value,start.Value,inventory.Value,options.SourceCommit!,
                noteBytes is null||startReceipt is null?null:Ref("verifier",startName,noteBytes),
                inventoryBytes is null||startReceipt is null?null:Ref("verifier",inventoryName,inventoryBytes),
                completionBytes is null||producerReceipt is null?null:Ref("ledger",producerCompletionName,completionBytes),
                options.MaxArtifactBytes.Value,Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes));
            if(producerReceiptIdentityProblem)
                accepted=accepted with{RejectionCodes=accepted.RejectionCodes.Append("producer_completion_invalid").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),Incomplete=false};
            if(knownCheck6Problems.Count>0)
                accepted=accepted with{Incomplete=false};
            bool diagnosticsSelected=false;
            if(!accepted.Accepted)
            {
                string diagnosticPrefix=producerPrefix+"diagnostics/";
                BlobListing diagnosticListing=await ReadLedgerListing(diagnosticPrefix,cancellationToken).ConfigureAwait(false);
                DiagnosticPublicationReadOutcome diagnostic=await DiagnosticPublicationReader.ReadAsync(diagnosticListing,diagnosticPrefix,
                    ReadDiagnostic,options.MaxArtifactBytes.Value,Math.Max(0,Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes)-retainedPublicationBytes),cancellationToken).ConfigureAwait(false);
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
                    capabilities=CapturedEvidenceReader.ReadCapabilities(Memory("capabilities.json"),"capabilities.json",inputLimits,budget,captureOnly:true);
                    reports=statistics.Value?.Select(envelope=>new ReportAssociation(envelope,
                        NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson),"statistics.jsonl",inputLimits,budget,options.MaxNativeReportBytes!.Value,options.MaxBrokerEntries!.Value))).ToArray()??[];
                }
                else if(index.IsExecutionFailure||index.Value is null)
                {
                    throw new InvalidOperationException("publication_index_unavailable_without_diagnostics");
                }
            }

            return new(producerRegistration,raw,index,ledger,manifest,statistics,lifecycle,capabilities,reports,accepted,diagnosticsSelected,knownCheck6Problems);
            ReadOnlyMemory<byte>? Memory(string name)=>raw[name] is byte[] bytes?(ReadOnlyMemory<byte>?)new ReadOnlyMemory<byte>(bytes):null;
            async Task<byte[]?> ReadDiagnostic(string name,CancellationToken ct)
            {
                byte[]? bytes=await ReadLedgerOptional(name,ct).ConfigureAwait(false);
                if(bytes is not null)Retain(bytes);
                return bytes;
            }
        }
        void Retain(byte[] bytes)
        {
            retainedPublicationBytes=checked(retainedPublicationBytes+bytes.LongLength);
            if(retainedPublicationBytes>Math.Min(options.MaxTotalInputBytes.Value,PublicationAcceptance.MaximumAggregateBytes))
                throw new InputLimitExceededException("Combined publication buffers exceed the configured retained-input limit.");
        }
        async Task<BlobListing> ReadLedgerListing(string prefix,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);var value=await producerStore.ListAsync(prefix,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
        async Task<byte[]?> ReadLedgerOptional(string name,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);byte[]? value=await producerStore.ReadAsync(name,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
        async Task<byte[]> ReadLedgerRequired(string name,CancellationToken ct)=>await ReadLedgerOptional(name,ct).ConfigureAwait(false)??throw new InvalidOperationException("required_ledger_object_missing:"+name);
        async Task<byte[]?> ReadVerifier(string name,CancellationToken ct){long origin=clock.GetTimestamp();using var deadline=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,deadline.Token);byte[]? value=await verifierStore.ReadAsync(name,linked.Token).ConfigureAwait(false);CheckDeadline(origin,options.BlobIoTimeout.Value,linked.Token);return value;}
    }

    private sealed record ProducerInputs(
        ProducerRegistration Registration,
        IReadOnlyDictionary<string,byte[]?> Raw,
        ParseResult<ArtifactIndex> Index,
        ParseResult<IReadOnlyList<LedgerRow>> Ledger,
        ParseResult<SealedRunManifest> Manifest,
        ParseResult<IReadOnlyList<StatisticsEnvelope>> Statistics,
        ParseResult<IReadOnlyList<LifecycleEvent>> Lifecycle,
        ParseResult<CapturedCapabilities> Capabilities,
        IReadOnlyList<ReportAssociation> Reports,
        PublicationReadOutcome Acceptance,
        bool DiagnosticsSelected,
        IReadOnlyList<StructuralProblem> Check6Problems);

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
