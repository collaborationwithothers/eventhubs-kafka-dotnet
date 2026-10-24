using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.Judge;

/// <summary>Reads only bounded receipts and verifier results to build one phase candidate.</summary>
internal sealed class JudgeHandler(IOptions<VerifierHostOptions> configured, IVerifierArtifactStore verifier,
    IProducerArtifactStore producer, TimeProvider clock, VerifierCompletionState? completion=null) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.Judge;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options=configured.Value;
        DateTimeOffset started=clock.GetUtcNow();
        long startedTimestamp=clock.GetTimestamp();
        string prefix=PhaseIdentity.PhasePrefix(options.RunId!,options.Phase!);
        BlobListing listing=await verifier.ListAsync(prefix,cancellationToken).ConfigureAwait(false);
        var expected=new HashSet<string>(Enumerable.Range(1,5).Select(i=>$"r{i}"),StringComparer.Ordinal);
        bool extra=listing.Names.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Select(n=>n[prefix.Length..])
            .Where(n=>n.Contains('/',StringComparison.Ordinal)).Select(n=>n.Split('/')[0]).Any(n=>!expected.Contains(n));
        InputLimits limits=new(options.MaxInputDepth!.Value,options.MaxArtifactBytes!.Value,options.MaxTotalInputBytes!.Value,
            options.MaxRowBytes!.Value,options.MaxRowsPerArtifact!.Value,options.MaxInputEntries!.Value,options.MaxDiagnosticBytes!.Value);
        var slots=new List<PhaseRepeatEvidence>(5);
        for(int i=1;i<=5;i++)
        {
            string repeat=PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,i);
            bool present=listing.Names.Any(n=>n.StartsWith(repeat,StringComparison.Ordinal));
            bool startAccepted=false,publicationAccepted=false,receiptValid=false,knownInvalid=false,trustedIncomplete=false;
            bool? jobBinding=null,registrationCount=null,inventoryCoverage=null;
            OutcomeCounts? counts=null;ProbeRepeatObservation? probe=null;string? identity=null;
            string producersPrefix=PhasePaths.ProducersPrefix(options.RunId!,options.Phase!,i);
            try
            {
                BlobListing producerListing=await producer.ListAsync(producersPrefix,cancellationToken).ConfigureAwait(false);
                string[] registrationNames=producerListing.Names.Where(n=>n.EndsWith("/registration.json",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
                registrationCount=producerListing.IsComplete?false:null;
                ProducerRegistration? registration=null;
                if(registrationNames.Length==1)
                {
                    byte[]? registrationBytes=await producer.ReadAsync(registrationNames[0],cancellationToken).ConfigureAwait(false);
                    if(registrationBytes is not null)
                    {
                        registration=PhaseWireCodec.ReadRegistration(registrationBytes);
                        registrationCount=producerListing.IsComplete&&registrationNames.Length==1?true:null;
                        string instance=registrationNames[0][producersPrefix.Length..^"/registration.json".Length];
                        jobBinding=registration.Identity.RunId==options.RunId&&registration.Phase==options.Phase&&registration.RepeatNumber==i&&
                            registration.JobName==PhaseIdentity.JobName(options.RunId!,options.Phase!,i)&&registration.Identity.InstanceId==instance&&
                            registration.Identity.ExpectedInstanceId==instance&&registration.SourceCommit==options.SourceCommit;
                        identity=registration.Identity.InstanceId;
                    }
                }
                string startName=PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,i);
                byte[]? startBytes=await verifier.ReadAsync(startName,cancellationToken).ConfigureAwait(false);
                if(startBytes is not null)
                {
                    WorkerReceipt r=PhaseWireCodec.ReadReceipt(startBytes);
                    string noteName=PhasePaths.StartLine(options.RunId!,options.Phase!,i);
                    string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,i);
                    byte[]? note=await verifier.ReadAsync(noteName,cancellationToken).ConfigureAwait(false);
                    byte[]? inventory=await verifier.ReadAsync(inventoryName,cancellationToken).ConfigureAwait(false);
                    startAccepted=r.Mode=="start-line"&&r.RunId==options.RunId&&r.Phase==options.Phase&&r.Repeat==i&&r.SourceCommit==options.SourceCommit&&r.HandlerExitCode==0&&
                        r.Artifacts[0].Name==noteName&&r.Artifacts[1].Name==inventoryName&&Matches(r.Artifacts[0],"verifier",noteName,note)&&
                        Matches(r.Artifacts[1],"verifier",inventoryName,inventory);
                }
                string completionName=PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,i);
                byte[]? completionBytes=await verifier.ReadAsync(completionName,cancellationToken).ConfigureAwait(false);
                if(completionBytes is not null)
                {
                    WorkerReceipt r=PhaseWireCodec.ReadReceipt(completionBytes);
                    string resultName=PhasePaths.Result(options.RunId!,options.Phase!,i);
                    ArtifactReference resultRef=r.Artifacts.SingleOrDefault()??throw new FormatException("Verify receipt must name one result.");
                    byte[]? resultBytes=await verifier.ReadAsync(resultName,cancellationToken).ConfigureAwait(false);
                    ProducerCompletionReceipt? producerReceipt=null;
                    byte[]? producerReceiptBytes=null;
                    if(registration is not null)
                    {
                        string producerReceiptName=PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,i,registration.Identity.InstanceId);
                        producerReceiptBytes=await producer.ReadAsync(producerReceiptName,cancellationToken).ConfigureAwait(false);
                        if(producerReceiptBytes is not null)producerReceipt=PhaseWireCodec.ReadProducerCompletion(producerReceiptBytes);
                    }
                    receiptValid=r.Mode=="verify"&&r.RunId==options.RunId&&r.Phase==options.Phase&&r.Repeat==i&&r.SourceCommit==options.SourceCommit&&
                        r.HandlerExitCode is 0 or 1&&r.JobName==PhaseIdentity.JobName(options.RunId!,options.Phase!,i)&&
                        resultRef.Container=="verifier"&&resultRef.Name==resultName&&resultBytes is not null&&resultRef.Length==resultBytes.LongLength&&
                        resultRef.Sha256==Convert.ToHexStringLower(SHA256.HashData(resultBytes));
                    PublicationAcceptanceSnapshot? snapshot=r.PublicationAcceptance;
                    bool producerCompletionValid=registration is not null&&producerReceipt is not null&&snapshot is not null&&
                        producerReceipt.Identity==registration.Identity&&producerReceipt.JobName==registration.JobName&&producerReceipt.PodName==registration.PodName&&
                        producerReceipt.PodUid==registration.PodUid&&producerReceipt.SourceCommit==registration.SourceCommit&&
                        Matches(snapshot.ProducerReceipt,"ledger",PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,i,registration.Identity.InstanceId),producerReceiptBytes);
                    publicationAccepted=receiptValid&&snapshot is not null&&r.RepeatChecks is not null&&startAccepted&&producerCompletionValid&&
                        snapshot.ProducerIdentity==registration?.Identity;
                    jobBinding=jobBinding==true&&r.RepeatChecks?.ProducerJobBinding=="pass";
                    registrationCount=registrationCount==true&&r.RepeatChecks?.RegisteredProducerCount=="pass";
                    inventoryCoverage=r.RepeatChecks?.BrokerInventoryCoverage switch{"pass"=>true,"fail"=>false,_=>null};
                    if(receiptValid&&resultBytes is not null)
                    {
                        var parsed=VerifierResultCodec.Read(resultBytes,resultName,limits,options.MaxDiagnosticsPerCheck!.Value);
                        if(parsed.IsSuccess&&parsed.Value is VerifierResult result)
                        {
                            bool same=registration is not null&&result.Identity==registration.Identity;
                            receiptValid&=same;
                            publicationAccepted&=same;
                            if(result.Status==VerifierResultStatus.Invalid)knownInvalid=true;
                            if(result.Status==VerifierResultStatus.Incomplete)trustedIncomplete=true;
                            if(result.Status==VerifierResultStatus.CompleteValid){counts=result.Counts;probe=result.ProbeObservation;}
                            identity=result.Identity.InstanceId;
                        }
                        else trustedIncomplete=true;
                    }
                    else trustedIncomplete=true;
                }
                if(!producerListing.IsComplete)trustedIncomplete=true;
            }
            catch(Exception ex) when(ex is FormatException or InvalidOperationException or ArgumentException or InputLimitExceededException or OverflowException)
            {
                trustedIncomplete=true;
            }
            slots.Add(new(i,present&&!extra,startAccepted,publicationAccepted,receiptValid,jobBinding,registrationCount,inventoryCoverage,knownInvalid,trustedIncomplete,counts,probe,identity));
        }
        if(!listing.IsComplete)slots=slots.Select(x=>x with{SlotPresent=false,TrustedIncomplete=true}).ToList();
        PhaseJudgement judgement=PhaseJudge.Evaluate(options.Phase!,slots);
        var candidate=new PhaseCandidate(1,true,"A01",options.ConfigurationId!,options.RunId!,options.Phase!,options.SourceCommit!,judgement.Status,judgement.Gate,
            new Dictionary<string,string?>(StringComparer.Ordinal){["verifier"]=typeof(JudgeHandler).Assembly.GetName().Version?.ToString(),["workload_generator"]=null,["confluent_kafka"]=null,["librdkafka"]=null,["dotnet"]=Environment.Version.ToString()},
            new Dictionary<string,string?>(StringComparer.Ordinal){["loadgen"]=Digest(options.LoadGenImage),["verifier"]=Digest(options.VerifierImage)},
            new Dictionary<string,object?>(StringComparer.Ordinal){["worker_started_utc"]=started.ToString("O",System.Globalization.CultureInfo.InvariantCulture),["judge_completed_utc"]=clock.GetUtcNow().ToString("O",System.Globalization.CultureInfo.InvariantCulture),["duration_seconds"]=(decimal?)clock.GetElapsedTime(startedTimestamp).TotalSeconds},
            new Dictionary<string,object?>(StringComparer.Ordinal){["error_codes"]=listing.IsComplete?Array.Empty<string>():new[]{listing.FailureCode??"listing_incomplete"},["max_throttle_ms"]=null,["request_timeout_increment"]=null,["disconnect_increment"]=null,["rtt_p99_ms"]=null},
            judgement.Repeats,judgement.Counts,judgement.Reasons);
        byte[] bytes=PhaseCandidateCodec.Write(candidate);
        string name=PhasePaths.Candidate(options.RunId!,options.Phase!);
        if(options.CandidateOutputName!=name)throw new InvalidOperationException("Candidate path is not the derived phase path.");
        await verifier.CreateAsync(name,bytes,cancellationToken).ConfigureAwait(false);
        int exit=judgement.Status=="complete_valid"?0:1;
        var candidateReference=new ArtifactReference("verifier",name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var receipt=new WorkerReceipt(1,"judge","A01",options.ConfigurationId!,options.RunId!,options.Phase!,null,
            PhaseIdentity.JudgeJobName(options.RunId!,options.Phase!),options.PodName!,null,options.SourceCommit!,exit,true,
            started.ToString("O",System.Globalization.CultureInfo.InvariantCulture),clock.GetUtcNow().ToString("O",System.Globalization.CultureInfo.InvariantCulture),[candidateReference],null,null);
        completion?.SetPendingBlobReceipt(options.VerifierContainer!,PhasePaths.JudgeCompletion(options.RunId!,options.Phase!),PhaseWireCodec.WriteReceipt(receipt));
        return exit;
    }

    private static string? Digest(string? image)
    {
        if(image is null)return null;
        int at=image.LastIndexOf('@');
        return at<0?null:image[(at+1)..];
    }

    private static bool Matches(ArtifactReference reference,string container,string name,byte[]? bytes)=>
        bytes is not null&&reference.Container==container&&reference.Name==name&&reference.Length==bytes.LongLength&&
        reference.Sha256==Convert.ToHexStringLower(SHA256.HashData(bytes));
}
