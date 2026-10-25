using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.Judge;

/// <summary>Reads only bounded receipts and verifier results to build one phase candidate.</summary>
internal sealed class JudgeHandler(IOptions<VerifierHostOptions> configured, IVerifierArtifactStore verifier,
    IProducerArtifactStore producer, TimeProvider clock, VerifierCompletionState? completion=null) : IVerifierModeHandler
{
    // These are the fixed judge-side parser bounds. The chart supplies
    // ScenarioLoadOptions to verifier Jobs, but judge parsing uses this fixed profile.
    private static readonly InputLimits JudgeLimits = new(32, 16_777_216, 83_886_080,
        1_048_576, 100_000, 1_000_000, 65_536);

    public VerifierMode Mode => VerifierMode.Judge;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options=configured.Value;
        DateTimeOffset judgeStarted=clock.GetUtcNow();
        DateTimeOffset? phaseStarted=null;
        string prefix=PhaseIdentity.PhasePrefix(options.RunId!,options.Phase!);
        BlobListing listing;
        try { listing=await Bounded(ct=>verifier.ListAsync(prefix,ct),options,clock,cancellationToken).ConfigureAwait(false); }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) { listing=new([],0,false,"judge_io_timeout"); }
        var expected=new HashSet<string>(Enumerable.Range(1,5).Select(i=>$"r{i}"),StringComparer.Ordinal);
        bool extra=listing.Names.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Select(n=>n[prefix.Length..])
            .Where(n=>n.Contains('/',StringComparison.Ordinal)).Select(n=>n.Split('/')[0]).Any(n=>!expected.Contains(n));
        InputLimits limits=JudgeLimits;
        var inputBudget=new InputBudget(limits);
        inputBudget.ConsumeEntries(listing.Names.Count);
        var slots=new List<PhaseRepeatEvidence>(5);
        var acceptedVersions=new List<(string? Generator,string? Kafka,string? Native,string? Dotnet)>();
        var acceptedA01=new List<Parchwell.Verifier.Common.Results.A01RecordedEvidence>();
        for(int i=1;i<=5;i++)
        {
            string repeat=PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,i);
            bool present=listing.Names.Any(n=>n.StartsWith(repeat,StringComparison.Ordinal));
            bool startAccepted=false,publicationAccepted=false,receiptValid=false,knownInvalid=false,trustedIncomplete=false;
            bool? jobBinding=null,registrationCount=null,inventoryCoverage=null;
            OutcomeCounts? counts=null;ProbeRepeatObservation? probe=null;string? identity=null;WorkloadObservation? workload=null;
            byte[]? startBytes=null,note=null,inventory=null;
            string producersPrefix=PhasePaths.ProducersPrefix(options.RunId!,options.Phase!,i);
            try
            {
                BlobListing producerListing=await Bounded(ct=>producer.ListAsync(producersPrefix,ct),options,clock,cancellationToken).ConfigureAwait(false);
                inputBudget.ConsumeEntries(producerListing.Names.Count);
                string[] registrationNames=producerListing.Names.Where(n=>n.EndsWith("/registration.json",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
                registrationCount=producerListing.IsComplete && registrationNames.Length>1 ? false : null;
                ProducerRegistration? registration=null;
                if(registrationNames.Length==1)
                {
                    byte[]? registrationBytes=await Bounded(ct=>producer.ReadAsync(registrationNames[0],ct),options,clock,cancellationToken).ConfigureAwait(false);
                    if(registrationBytes is not null)
                    {
                        inputBudget.ConsumeBytes(registrationBytes.LongLength);
                        try { registration=PhaseWireCodec.ReadRegistration(registrationBytes); }
                        catch(FormatException) { registrationCount=false; throw; }
                        catch(System.Text.Json.JsonException) { registrationCount=false; throw; }
                        catch(ArgumentException) { registrationCount=false; }
                        if(registration is not null)
                        {
                            registrationCount=producerListing.IsComplete&&registrationNames.Length==1?true:null;
                            string instance=registrationNames[0][producersPrefix.Length..^"/registration.json".Length];
                            jobBinding=registration.Identity.RunId==options.RunId&&registration.Phase==options.Phase&&registration.RepeatNumber==i&&
                                registration.JobName==PhaseIdentity.JobName(options.RunId!,options.Phase!,i)&&registration.Identity.InstanceId==instance&&
                                registration.Identity.ExpectedInstanceId==instance&&registration.SourceCommit==options.SourceCommit;
                            identity=registration.Identity.InstanceId;
                        }
                    }
                }
                string startName=PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,i);
                startBytes=await Bounded(ct=>verifier.ReadAsync(startName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                if(startBytes is not null)
                {
                    inputBudget.ConsumeBytes(startBytes.LongLength);
                    WorkerReceipt r=PhaseWireCodec.ReadReceipt(startBytes);
                    string noteName=PhasePaths.StartLine(options.RunId!,options.Phase!,i);
                    string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,i);
                    note=await Bounded(ct=>verifier.ReadAsync(noteName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                    inventory=await Bounded(ct=>verifier.ReadAsync(inventoryName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                    if(note is not null)inputBudget.ConsumeBytes(note.LongLength);
                    if(inventory is not null)inputBudget.ConsumeBytes(inventory.LongLength);
                    bool startContentsValid=false;
                    if(note is not null&&inventory is not null&&r.Identity is not null)
                    {
                        var parsedNote=StartLineNoteCodec.Read(note,noteName,limits,100_000);
                        var parsedInventory=BrokerInventoryReader.Read(inventory,inventoryName,r.Identity,options.SourceCommit!,limits);
                        startContentsValid=parsedNote.IsSuccess&&parsedInventory.IsSuccess&&parsedNote.Value!.Identity==r.Identity&&
                            parsedNote.Value.VerifierCommit==options.SourceCommit;
                    }
                    startAccepted=r.Mode=="start-line"&&r.RunId==options.RunId&&r.Phase==options.Phase&&r.Repeat==i&&r.SourceCommit==options.SourceCommit&&r.HandlerExitCode==0&&startContentsValid&&
                        r.Artifacts[0].Name==noteName&&r.Artifacts[1].Name==inventoryName&&Matches(r.Artifacts[0],"verifier",noteName,note)&&
                        Matches(r.Artifacts[1],"verifier",inventoryName,inventory);
                    if(startAccepted && DateTimeOffset.TryParse(r.StartedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out var repeatStarted))
                        phaseStarted=phaseStarted is null||repeatStarted<phaseStarted?repeatStarted:phaseStarted;
                }
                string completionName=PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,i);
                byte[]? completionBytes=await Bounded(ct=>verifier.ReadAsync(completionName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                if(completionBytes is not null)
                {
                    inputBudget.ConsumeBytes(completionBytes.LongLength);
                    WorkerReceipt r=PhaseWireCodec.ReadReceipt(completionBytes);
                    string resultName=PhasePaths.Result(options.RunId!,options.Phase!,i);
                    ArtifactReference resultRef=r.Artifacts.SingleOrDefault()??throw new FormatException("Verify receipt must name one result.");
                    byte[]? resultBytes=await Bounded(ct=>verifier.ReadAsync(resultName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                    if(resultBytes is not null)inputBudget.ConsumeBytes(resultBytes.LongLength);
                    ProducerCompletionReceipt? producerReceipt=null;
                    byte[]? producerReceiptBytes=null;
                    if(registration is not null)
                    {
                        string producerReceiptName=PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,i,registration.Identity.InstanceId);
                        producerReceiptBytes=await Bounded(ct=>producer.ReadAsync(producerReceiptName,ct),options,clock,cancellationToken).ConfigureAwait(false);
                        if(producerReceiptBytes is not null)inputBudget.ConsumeBytes(producerReceiptBytes.LongLength);
                        if(producerReceiptBytes is not null)producerReceipt=PhaseWireCodec.ReadProducerCompletion(producerReceiptBytes);
                    }
                    receiptValid=r.Mode=="verify"&&r.RunId==options.RunId&&r.Phase==options.Phase&&r.Repeat==i&&r.SourceCommit==options.SourceCommit&&
                        r.HandlerExitCode is 0 or 1&&r.JobName==PhaseIdentity.VerifyJobName(options.RunId!,options.Phase!,i)&&
                        resultRef.Container=="verifier"&&resultRef.Name==resultName&&resultBytes is not null&&resultRef.Length==resultBytes.LongLength&&
                        resultRef.Sha256==Convert.ToHexStringLower(SHA256.HashData(resultBytes));
                    PublicationAcceptanceSnapshot? snapshot=r.PublicationAcceptance;
                    bool snapshotReferencesMatch=snapshot is not null &&
                        Matches(snapshot.StartLine,"verifier",PhasePaths.StartLine(options.RunId!,options.Phase!,i),note) &&
                        Matches(snapshot.BrokerInventory,"verifier",PhasePaths.BrokerInventory(options.RunId!,options.Phase!,i),inventory);
                    bool producerCompletionValid=registration is not null&&producerReceipt is not null&&snapshot is not null&&
                        producerReceipt.Identity==registration.Identity&&producerReceipt.JobName==registration.JobName&&producerReceipt.PodName==registration.PodName&&
                        producerReceipt.PodUid==registration.PodUid&&producerReceipt.SourceCommit==registration.SourceCommit&&
                        Matches(snapshot.ProducerReceipt,"ledger",PhasePaths.ProducerCompletion(options.RunId!,options.Phase!,i,registration.Identity.InstanceId),producerReceiptBytes);
                    publicationAccepted=receiptValid&&snapshotReferencesMatch&&snapshot is not null&&r.RepeatChecks is not null&&startAccepted&&producerCompletionValid&&
                        snapshot.ProducerIdentity==registration?.Identity;
                    jobBinding=Combine(jobBinding,r.RepeatChecks?.ProducerJobBinding);
                    registrationCount=Combine(registrationCount,r.RepeatChecks?.RegisteredProducerCount);
                    inventoryCoverage=r.RepeatChecks?.BrokerInventoryCoverage switch{"pass"=>true,"fail"=>false,_=>null};
                    if(receiptValid&&resultBytes is not null)
                    {
                        var parsed=VerifierResultCodec.Read(resultBytes,resultName,limits,options.MaxDiagnosticsPerCheck!.Value);
                        if(parsed.IsSuccess&&parsed.Value is VerifierResult result)
                        {
                            bool semanticReceipt = result.VerifierCommit==r.SourceCommit &&
                                (r.HandlerExitCode==0 ? result.Status==VerifierResultStatus.CompleteValid : result.Status!=VerifierResultStatus.CompleteValid);
                            receiptValid &= semanticReceipt;
                            bool same=registration is not null&&result.Identity==registration.Identity;
                            receiptValid&=same;
                            publicationAccepted&=same;
                            // J3 binds the result to the verifier receipt and registered producer.
                            // Once that binding fails, its status and check projections are untrusted.
                            if(receiptValid)
                            {
                                if(result.Status==VerifierResultStatus.Invalid)knownInvalid=true;
                                if(result.Status==VerifierResultStatus.Incomplete)trustedIncomplete=true;
                            }
                            // Probe evidence has an independent authorization path: a
                            // message-only invalid result can still carry a usable,
                            // accepted throttle observation. Counts remain status-gated.
                            if(receiptValid&&publicationAccepted)probe=result.ProbeObservation;
                            if(receiptValid&&publicationAccepted)
                            {
                                workload=result.WorkloadObservation;
                                if(result.A01RecordedEvidence is not null)acceptedA01.Add(result.A01RecordedEvidence);
                                string? producerKafkaVersion=null,producerNativeVersion=null,producerDotnetVersion=null;
                                result.RuntimeVersions?.TryGetValue("Confluent.Kafka",out producerKafkaVersion);
                                result.RuntimeVersions?.TryGetValue("librdkafka",out producerNativeVersion);
                                result.RuntimeVersions?.TryGetValue("dotnet",out producerDotnetVersion);
                                acceptedVersions.Add((result.GeneratorVersion,producerKafkaVersion,producerNativeVersion,producerDotnetVersion));
                            }
                            if(result.Status==VerifierResultStatus.CompleteValid)counts=result.Counts;
                            bool inventoryFailed=result.FailedChecks.Contains(24);
                            if(receiptValid && ((inventoryCoverage==true&&inventoryFailed)||(inventoryCoverage==false&&!inventoryFailed)))
                            {
                                knownInvalid=true;
                                inventoryCoverage=false;
                            }
                            identity=result.Identity.InstanceId;
                        }
                        else { receiptValid=false; trustedIncomplete=true; }
                    }
                    else trustedIncomplete=true;
                }
                if(!producerListing.IsComplete)trustedIncomplete=true;
            }
            // Exhausting the aggregate phase input budget is an execution failure.
            // It must stop the judge rather than publish a candidate that looks like
            // ordinary unavailable evidence for one slot.
            catch(Exception ex) when(ex is FormatException or System.Text.Json.JsonException or InvalidOperationException or OverflowException or TimeoutException || ex is OperationCanceledException&&!cancellationToken.IsCancellationRequested)
            {
                trustedIncomplete=true;
            }
            slots.Add(new(i,present&&!extra,startAccepted,publicationAccepted,receiptValid,jobBinding,registrationCount,inventoryCoverage,knownInvalid,trustedIncomplete,counts,probe,identity,workload));
        }
        if(!listing.IsComplete)slots=slots.Select(x=>x with{SlotPresent=false,TrustedIncomplete=true}).ToList();
        PhaseJudgement judgement=PhaseJudge.Evaluate(options.Phase!,slots);
        string? CommonVersion(Func<(string? Generator,string? Kafka,string? Native,string? Dotnet),string?> select)
        {
            if(acceptedVersions.Count==0)return null;
            string? first=select(acceptedVersions[0]);
            return first is not null&&acceptedVersions.All(v=>StringComparer.Ordinal.Equals(first,select(v)))?first:null;
        }
        long? Sum(Func<Parchwell.Verifier.Common.Results.A01WindowEvidence,long?> select)
        {
            try { long total=0;bool seen=false;foreach(var window in acceptedA01.SelectMany(e=>e.Windows??Array.Empty<Parchwell.Verifier.Common.Results.A01WindowEvidence>())){long? value=select(window);if(value is long v){seen=true;total=checked(total+v);}}return seen?total:null; }
            catch(OverflowException){return null;}
        }
        Parchwell.Verifier.Common.Results.A01WindowEvidence[] a01Windows=acceptedA01.SelectMany(e=>e.Windows??Array.Empty<Parchwell.Verifier.Common.Results.A01WindowEvidence>()).ToArray();
        string[] versionReasons=new Func<(string? Generator,string? Kafka,string? Native,string? Dotnet),string?>[]{v=>v.Generator,v=>v.Kafka,v=>v.Native,v=>v.Dotnet}.Any(selector=>CommonVersion(selector) is null)?["version_unavailable"]:[];
        string[] timingReasons=phaseStarted is null?["timing_unavailable"]:[];
        var candidate=new PhaseCandidate(1,true,"A01",options.ConfigurationId!,options.RunId!,options.Phase!,options.SourceCommit!,judgement.Status,judgement.Gate,
            new Dictionary<string,string?>(StringComparer.Ordinal){["verifier"]=typeof(JudgeHandler).Assembly.GetName().Version?.ToString(),["workload_generator"]=CommonVersion(v=>v.Generator),["confluent_kafka"]=CommonVersion(v=>v.Kafka),["librdkafka"]=CommonVersion(v=>v.Native),["dotnet"]=CommonVersion(v=>v.Dotnet)},
            new Dictionary<string,string?>(StringComparer.Ordinal){["loadgen"]=Digest(options.LoadGenImage),["verifier"]=Digest(options.VerifierImage)},
            new Dictionary<string,object?>(StringComparer.Ordinal){["worker_started_utc"]=phaseStarted?.ToString("O",System.Globalization.CultureInfo.InvariantCulture),["judge_completed_utc"]=phaseStarted is null?null:clock.GetUtcNow().ToString("O",System.Globalization.CultureInfo.InvariantCulture),["duration_seconds"]=phaseStarted is null?null:(decimal?)(clock.GetUtcNow()-phaseStarted.Value).TotalSeconds},
            new Dictionary<string,object?>(StringComparer.Ordinal){["error_codes"]=acceptedA01.SelectMany(e=>e.DeliveryErrorCodes??Array.Empty<int>()).Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Concat(listing.IsComplete?Array.Empty<string>():new[]{listing.FailureCode??"listing_incomplete"}).ToArray(),["max_throttle_ms"]=a01Windows.Where(w=>w.ThrottleMaximumMilliseconds.HasValue).Select(w=>(long?)w.ThrottleMaximumMilliseconds!.Value).Max(),["request_timeout_increment"]=Sum(w=>w.RequestTimeoutsIncrement),["disconnect_increment"]=Sum(w=>w.DisconnectsIncrement),["rtt_p99_ms"]=a01Windows.Where(w=>w.RoundTripP99Microseconds.HasValue).Select(w=>(decimal?)w.RoundTripP99Microseconds!.Value/1000m).Max()},
            judgement.Repeats,judgement.Counts,judgement.Reasons.Concat(versionReasons).Concat(timingReasons).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(100).ToArray());
        byte[] bytes=PhaseCandidateCodec.Write(candidate);
        string name=PhasePaths.Candidate(options.RunId!,options.Phase!);
        if(options.CandidateOutputName!=name)throw new InvalidOperationException("Candidate path is not the derived phase path.");
        await Bounded(ct=>verifier.CreateAsync(name,bytes,ct),options,clock,cancellationToken).ConfigureAwait(false);
        int exit=judgement.Status=="complete_valid"?0:1;
        var candidateReference=new ArtifactReference("verifier",name,bytes.LongLength,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var receipt=new WorkerReceipt(1,"judge","A01",options.ConfigurationId!,options.RunId!,options.Phase!,null,
            PhaseIdentity.JudgeJobName(options.RunId!,options.Phase!),options.PodName!,null,options.SourceCommit!,exit,true,
            judgeStarted.ToString("O",System.Globalization.CultureInfo.InvariantCulture),clock.GetUtcNow().ToString("O",System.Globalization.CultureInfo.InvariantCulture),[candidateReference],null,null);
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

    private static bool? Combine(bool? observed,string? receipt) => observed switch
    {
        false => false,
        null => null,
        true => receipt switch { "pass" => true, "fail" => false, _ => null }
    };

    private static async Task<T> Bounded<T>(Func<CancellationToken,Task<T>> operation,VerifierHostOptions options,TimeProvider clock,CancellationToken cancellationToken)
    {
        long origin=clock.GetTimestamp();
        using var deadline=new CancellationTokenSource(options.IoTimeout!.Value,clock);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token);
        T result=await operation(linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
        if(clock.GetElapsedTime(origin)>=options.IoTimeout.Value)throw new TimeoutException("The judge I/O operation budget expired.");
        return result;
    }
    private static async Task Bounded(Func<CancellationToken,Task> operation,VerifierHostOptions options,TimeProvider clock,CancellationToken cancellationToken)
    {
        long origin=clock.GetTimestamp();
        using var deadline=new CancellationTokenSource(options.IoTimeout!.Value,clock);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token);
        await operation(linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
        if(clock.GetElapsedTime(origin)>=options.IoTimeout.Value)throw new TimeoutException("The judge I/O operation budget expired.");
    }
}
