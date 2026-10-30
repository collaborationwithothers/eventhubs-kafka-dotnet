using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

/// <summary>Owns p1-p3, p8 and configuration/item acceptance. Transport never decides these checks.</summary>
internal static class SingleActionConfigurationJudgement
{
    private static readonly int[] Ordinals=[1,2,3,4,5];
    private static readonly string[] RegistrationReasons=["p1_registration"], CaptureReasons=["p3_capture_file"], CompletionReasons=["p2_completion"], SeedReasons=["p8_seed_prerequisite"], UnsupportedCodecs=["C2","C3","C4"];

    internal static bool Registration(SingleActionRegistration? value,string run,string item,string configuration,string source) => value is not null&&
        value.SchemaVersion==1&&value.Mode=="single-action"&&Identity(value.Identity,run,item,configuration,value.PodName)&&
        value.JobName==SingleActionPaths.CaptureJob(run,item,configuration)&&Pod(value.PodName,value.JobName)&&SingleActionWire.Text(value.PodUid)&&value.SourceCommit==source;

    internal static bool Completion(SingleActionCompletion? value,SingleActionRegistration? registration) => value is not null&&registration is not null&&
        value.SchemaVersion==1&&value.Mode=="single-action"&&value.Identity==registration.Identity&&value.JobName==registration.JobName&&value.PodName==registration.PodName&&value.PodUid==registration.PodUid&&value.SourceCommit==registration.SourceCommit&&
        value.RepeatsAttempted==5&&value.HandlerExitCode==0&&value.CleanupCompleted;

    internal static bool Capture(SingleActionCapture? value,SingleActionRegistration? registration)
    {
        if(value is null||registration is null||value.SchemaVersion!=1||value.Mode!="single-action"||!value.Synthetic||value.Identity!=registration.Identity||value.JobName!=registration.JobName||value.PodName!=registration.PodName||value.PodUid!=registration.PodUid||value.SourceCommit!=registration.SourceCommit||!SingleActionWire.Text(value.ManagedVersion)||!SingleActionWire.Text(value.NativeVersion)||value.Repeats.Length!=5||!value.Repeats.Select(r=>r.Repeat).Order().SequenceEqual(Ordinals))return false;
        int diagnostics=0;
        foreach(var repeat in value.Repeats)
        {
            diagnostics+=repeat.Diagnostics.Length;
            if(repeat.ClientId!=$"{value.PodName}-r{repeat.Repeat}"||repeat.Settings.Count>128||repeat.Settings.Sum(p=>SingleActionWire.Bytes(p.Key)+SingleActionWire.Bytes(p.Value))>16384||repeat.OmittedKeys.Distinct(StringComparer.Ordinal).Count()!=repeat.OmittedKeys.Length||repeat.ClientErrors.Length>64||repeat.Statistics.Length>64||repeat.Diagnostics.Any(d=>SingleActionWire.Bytes(d)>2048)||repeat.ClientErrors.Any(e=>e.ObservedElapsedMs<0||e.Origin is not ("construction" or "local" or "broker")||SingleActionWire.Bytes(e.Reason)>4096))return false;
        }
        return diagnostics<=100;
    }

    private static bool Pod(string pod,string job)=>pod.StartsWith(job+"-",StringComparison.Ordinal)&&pod.Length<=253&&pod.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '.');

    private static bool Identity(SingleActionIdentity identity,string run,string item,string configuration,string pod) => identity.RunId==run&&identity.ScenarioId==item&&identity.ConfigurationId==configuration&&identity.InstanceId==pod;

    internal static SingleActionRepeatProjection[] ApplyGlobal(bool registration,bool capture,bool completion,bool seed,IEnumerable<SingleActionRepeatProjection> repeats)
    {
        var projections=repeats.OrderBy(r=>r.Repeat).ToArray();
        if(projections.Length!=5)projections=Enumerable.Range(1,5).Select(i=>new SingleActionRepeatProjection(i,"incomplete",[],null,null,null)).ToArray();
        return projections.Select(r=>r with
        {
            Status=!registration||!capture?"invalid":r.Status=="invalid"?"invalid":!completion||!seed?"incomplete":r.Status,
            Reasons=r.Reasons.Concat(!registration?RegistrationReasons:Array.Empty<string>()).Concat(!capture?CaptureReasons:Array.Empty<string>()).Concat(!completion?CompletionReasons:Array.Empty<string>()).Concat(!seed?SeedReasons:Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray()
        }).ToArray();
    }

    internal static (string Status,bool? Deterministic,SingleActionOutcome? Outcome) Aggregate(SingleActionRepeatProjection[] repeats)
    {
        string status=repeats.Any(r=>r.Status=="invalid")?"invalid":repeats.Length==5&&repeats.All(r=>r.Status=="complete_valid")?"complete_valid":"incomplete";
        bool? deterministic=status=="complete_valid"?repeats.All(r=>r.OutcomeClass is not null&&r.OutcomeClass==repeats[0].OutcomeClass):null;
        return(status,deterministic,deterministic==true?repeats[0].OutcomeClass:null);
    }

    internal static bool SecurityFinding(string item,string configuration,bool registration,bool capture,SingleActionRepeatProjection[] repeats) => item=="F04"&&configuration is "C1" or "C2" or "C3"&&registration&&capture&&repeats.Any(r=>!r.Reasons.Any(reason=>reason is "p5_configuration_fidelity" or "p7_outcome_record")&&r.OutcomeClass?.Result=="success");

    internal static bool Seed(SingleActionSeedReceipt? receipt,SingleActionSeedPod? pod,string run,string source)
    {
        if(receipt is null||pod is null)return false;
        string job=$"{run}-f04-seed";
        try
        {
            return receipt.SchemaVersion==1&&receipt.Mode=="single-action-seed"&&receipt.RunId==run&&receipt.SourceCommit==source&&receipt.JobName==job&&
                receipt.ServiceAccount=="enrichment-consumer"&&receipt.Topic=="document-state"&&receipt.Partition==0&&
                Convert.FromBase64String(receipt.KeyBase64).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes($"f04-seed:{run}"))&&
                receipt.ValueSha256=="68fe9c9f499dd1a6bf07c6f737c2b6a8118cca37877336ee0f2674209554d3c6"&&receipt.Delivered&&receipt.Error.ValueKind==JsonValueKind.Null&&receipt.HandlerExitCode==0&&receipt.CleanupCompleted&&
                pod.SchemaVersion==1&&pod.JobName==job&&pod.OwnerKind=="Job"&&pod.OwnerName==job&&pod.ServiceAccount==receipt.ServiceAccount&&pod.PodName==receipt.PodName&&pod.PodUid==receipt.PodUid&&SingleActionWire.Text(pod.PodName)&&SingleActionWire.Text(pod.PodUid);
        }
        catch(FormatException){return false;}
    }

    internal static bool Published(SingleActionCandidate? candidate,SingleActionJudgeReceipt? receipt,string run,string item,string configuration,string source)
    {
        if(candidate is null||receipt is null)return false;
        var aggregate=Aggregate(candidate.Repeats);
        return candidate.SchemaVersion==1&&candidate.Mode=="single-action"&&candidate.Synthetic&&candidate.RunId==run&&candidate.ScenarioId==item&&candidate.ConfigurationId==configuration&&candidate.SourceCommit==source&&candidate.Adr012Counts=="not_applicable"&&candidate.Repeats.Select(r=>r.Repeat).Order().SequenceEqual(Ordinals)&&
            candidate.Status==aggregate.Status&&candidate.Deterministic==aggregate.Deterministic&&candidate.OutcomeClass==aggregate.Outcome&&
            receipt.SchemaVersion==1&&receipt.Mode=="single-action-judge"&&receipt.RunId==run&&receipt.ScenarioId==item&&receipt.ConfigurationId==configuration&&receipt.SourceCommit==source&&receipt.JobName==SingleActionPaths.JudgeJob(run,item,configuration)&&SingleActionWire.Text(receipt.PodName)&&SingleActionWire.Text(receipt.PodUid)&&receipt.CandidateName==SingleActionPaths.Candidate(run,item,configuration)&&receipt.HandlerExitCode==0&&receipt.CleanupCompleted;
    }

    internal static bool Control(SingleActionCandidate? candidate,SingleActionJudgeReceipt? receipt,string run,string source) => Published(candidate,receipt,run,"F04","C0",source)&&candidate!.Status=="complete_valid"&&candidate.Deterministic==true&&candidate.OutcomeClass?.Result=="success";

    internal static string[] Configurations(string item) => item switch
    {
        "B01" or "C08"=>["C0","C1"],"B03"=>["C1","C2","C3","C4"],"B05"=>["C1"],"C12"=>["C1","C2"],"F04"=>["C0","C1","C2","C3","C4"],_=>[]
    };

    internal static SingleActionItemAssessment JudgeItem(string item,IReadOnlyDictionary<string,SingleActionCandidate> candidates)
    {
        string[] labels=item switch{"B01"=>["H0","H1"],"B03"=>["H0","H1_snappy","H1_lz4","H1_zstd"],"B05"=>["H1","H2"],"C08"=>["H0","H1"],"C12"=>["H1","H2"],"F04"=>["H0","H1","H2","H3","H4"],_=>[]};
        var hypotheses=labels.ToDictionary(h=>h,_=>"undecided",StringComparer.Ordinal);
        SingleActionItemAssessment Result(string? verdict,string? reason=null)=>new(verdict,hypotheses,reason);
        if(candidates.Values.Any(c=>c.SecurityFinding))return Result(null,"security_finding_requires_hari");
        string[] configurations=Configurations(item);
        if(configurations.Length==0||configurations.Any(c=>!candidates.ContainsKey(c)))return Result(null,"missing_configuration");
        if(configurations.Any(c=>candidates[c].Status!="complete_valid"))return Result(null,"incomplete_configuration");
        if(configurations.Any(c=>candidates[c].Deterministic!=true||candidates[c].OutcomeClass is null))return Result(null,"unequal_classes_separate_full_plan");
        SingleActionOutcome O(string c)=>candidates[c].OutcomeClass!;
        bool Success(string c)=>O(c).Result=="success";
        bool BrokerError(string c)=>O(c).Result=="error"&&O(c).Origin=="broker";
        if(configurations.Any(c=>O(c).Origin=="construction"))return Result("untestable","construction_origin");
        if(item is "B01" or "C08" or "F04"&&!Success("C0")||item=="B03"&&!Success("C1"))return Result("untestable","failed_control");
        switch(item)
        {
            case "B01":hypotheses["H0"]="holds";hypotheses["H1"]=Success("C1")?"contradicted":"holds";return Result(Success("C1")?"contradicted":"confirmed");
            case "B03":
                hypotheses["H0"]="holds";
                foreach(var (label,configuration) in new[]{("H1_snappy","C2"),("H1_lz4","C3"),("H1_zstd","C4")})hypotheses[label]=Success(configuration)?"contradicted":"holds";
                return Result(UnsupportedCodecs.Any(Success)?"contradicted":"confirmed");
            case "B05":hypotheses["H1"]=Success("C1")?"holds":"contradicted";hypotheses["H2"]=Success("C1")?"contradicted":"holds";return Result("confirmed");
            case "C08":hypotheses["H0"]="holds";hypotheses["H1"]="recorded_only";return Result("undocumented");
            case "C12":
                hypotheses["H1"]=Success("C1")?"holds":BrokerError("C1")?"contradicted":"undecided";
                hypotheses["H2"]=BrokerError("C2")?"holds":Success("C2")?"contradicted":"undecided";
                return Result(Success("C1")&&BrokerError("C2")?"confirmed":BrokerError("C1")||Success("C2")?"contradicted":"untestable");
            case "F04":hypotheses["H0"]="holds";hypotheses["H4"]=Success("C4")?"holds":"contradicted";foreach(string h in new[]{"H1","H2","H3"})hypotheses[h]="recorded_only";return Result(Success("C4")?"undocumented":"untestable",Success("C4")?null:"failed_control");
            default:return Result(null,"unknown_item");
        }
    }
    private static readonly string[] V2PositiveConfigurations = ["C0","C1","C3","C5","C6","C7","C8","C9"];
    private static readonly string[] V2ImageNames = ["loadgen", "verifier"], V2ControlNames = ["loadgen", "enrichment-consumer"];

    internal static bool V2Images(IReadOnlyDictionary<string,string> actual, IReadOnlyDictionary<string,string> expected) =>
        actual.Count == 2 && actual.Keys.Order(StringComparer.Ordinal).SequenceEqual(V2ImageNames,StringComparer.Ordinal) &&
        actual.All(x => x.Value.Length == 71 && x.Value.StartsWith("sha256:",StringComparison.Ordinal) && x.Value[7..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && expected.TryGetValue(x.Key,out string? value) && value == x.Value);

    internal static string[] V2EvidenceProblems(ProbeV2Options options, ProbeV2Evidence evidence)
    {
        var reasons = new List<string>(evidence.Problems);
        var registration = evidence.Registration; var capture = evidence.Capture; var pod = evidence.Pod;
        var matrix = ProbeV2Matrix.For(options.ScenarioId,evidence.Configuration,evidence.Stage);
        string job = ProbeV2Matrix.Job(options.RunId,options.ScenarioId,evidence.Configuration,evidence.Stage);
        if (registration is null || capture is null || pod is null) reasons.Add("v2_completion");
        if (registration is not null && (registration.SchemaVersion != 2 || registration.Mode != "single-action-v2" || !registration.Synthetic ||
            registration.RunId != options.RunId || registration.ScenarioId != options.ScenarioId || registration.ConfigurationId != evidence.Configuration ||
            registration.Stage != evidence.Stage || registration.SourceCommit != options.SourceCommit || registration.JobName != job ||
            registration.ServiceAccount != matrix.Account || registration.PodName.Length > 63 || !Pod(registration.PodName,job) || !SingleActionWire.Text(registration.PodUid)))
            reasons.Add("v2_identity");
        if (capture is not null && (capture.SchemaVersion != 2 || capture.Mode != "single-action-v2" || !capture.Synthetic || capture.RunId != options.RunId ||
            capture.ScenarioId != options.ScenarioId || capture.ConfigurationId != evidence.Configuration || capture.Stage != evidence.Stage ||
            capture.SourceCommit != options.SourceCommit || capture.JobName != job || capture.ServiceAccount != matrix.Account ||
            capture.PodName.Length > 63 || !Pod(capture.PodName,job) || !SingleActionWire.Text(capture.PodUid))) reasons.Add("v2_identity");
        if (pod is not null && (pod.SchemaVersion != 2 || pod.JobName != job || pod.OwnerKind != "Job" || pod.OwnerName != job ||
            pod.PodName.Length > 63 || !Pod(pod.PodName,job) || !SingleActionWire.Text(pod.PodUid) ||
            pod.ServiceAccount != matrix.Account || pod.ImageDigest != options.ImageDigests()["loadgen"])) reasons.Add("v2_identity");
        if (registration is not null && capture is not null && (registration.PodName != capture.PodName || registration.PodUid != capture.PodUid) ||
            registration is not null && pod is not null && (registration.PodName != pod.PodName || registration.PodUid != pod.PodUid) ||
            capture is not null && pod is not null && (capture.PodName != pod.PodName || capture.PodUid != pod.PodUid)) reasons.Add("v2_identity");
        if (capture is null) return reasons.Distinct(StringComparer.Ordinal).ToArray();
        if (capture.Repeats.Count != 5 || !capture.Repeats.Select(x => x.Repeat).SequenceEqual(Enumerable.Range(1,5)) ||
            !SingleActionWire.Text(capture.ManagedVersion) || !SingleActionWire.Text(capture.NativeVersion)) reasons.Add("v2_wire");
        if (capture.Repeats.Sum(x => x.Diagnostics.Count) > options.Limits.MaxDiagnostics) reasons.Add("v2_capture_limit");
        bool cleanupStopped = false;
        foreach (var repeat in capture.Repeats)
        {
            if (cleanupStopped && repeat.Admission != "not_attempted_cleanup") reasons.Add("v2_completion");
            cleanupStopped |= repeat.Admission == "not_attempted_cleanup" || repeat.Diagnostics.Any(x => x.StartsWith("client_cleanup_failed",StringComparison.Ordinal));
        }
        if (capture.HandlerExitCode != 0 || !capture.CleanupCompleted || !capture.HostCleanupCompleted) reasons.Add("v2_completion");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static bool V2Scope(ProbeV2Options options, ProbeV2Scope? scope)
    {
        if (scope is null || scope.SchemaVersion != 2 || scope.RunId != options.RunId || scope.SourceCommit != options.SourceCommit || scope.NamespaceName != options.NamespaceName) return false;
        var deployed = scope.Deployed;
        string[] aliases = ["enrichment-consumer","loadgen","probe","verifier"];
        string[] topics = ["document-events","document-events-dlq","document-state"];
        if (!deployed.Accounts.Keys.Order(StringComparer.Ordinal).SequenceEqual(aliases,StringComparer.Ordinal) ||
            !deployed.Topics.Keys.Order(StringComparer.Ordinal).SequenceEqual(topics,StringComparer.Ordinal) ||
            !deployed.NamespaceId.EndsWith("/providers/Microsoft.EventHub/namespaces/" + options.NamespaceName,StringComparison.OrdinalIgnoreCase) ||
            deployed.Accounts.Values.Any(x => !Guid.TryParse(x.ClientId,out _) || !Guid.TryParse(x.PrincipalId,out _)) ||
            deployed.Accounts.Values.Select(x => x.ClientId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 ||
            deployed.Accounts.Values.Select(x => x.PrincipalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 ||
            deployed.Accounts["verifier"].ClientId != options.WorkloadIdentityClientId ||
            deployed.Topics.Any(x => !string.Equals(x.Value,deployed.NamespaceId + "/eventhubs/" + x.Key,StringComparison.OrdinalIgnoreCase)) ||
            options.ScenarioId is "E01" or "F06" && !deployed.ProbeNamespaceDataRoles) return false;
        int resourceGroupEnd = deployed.NamespaceId.IndexOf("/providers/",StringComparison.OrdinalIgnoreCase);
        if (resourceGroupEnd < 0) return false;
        string storage = deployed.NamespaceId[..resourceGroupEnd] + "/providers/Microsoft.Storage/storageAccounts/" + new Uri(options.BlobServiceUri).Host.Split('.')[0] + "/blobServices/default/containers/";
        var expected = new Dictionary<string,(string Account,string Role,string Scope)>(StringComparer.Ordinal)
        {
            ["probe_events_receiver"] = ("probe","Azure Event Hubs Data Receiver",deployed.Topics["document-events"]),
            ["probe_single_action_contributor"] = ("probe","Storage Blob Data Contributor",storage + "single-action"),
            ["verifier_single_action_reader"] = ("verifier","Storage Blob Data Reader",storage + "single-action"),
            ["loadgen_events_sender"] = ("loadgen","Azure Event Hubs Data Sender",deployed.Topics["document-events"]),
            ["loadgen_ledger_contributor"] = ("loadgen","Storage Blob Data Contributor",storage + "ledger"),
            ["enrichment_events_receiver"] = ("enrichment-consumer","Azure Event Hubs Data Receiver",deployed.Topics["document-events"]),
            ["enrichment_state_sender"] = ("enrichment-consumer","Azure Event Hubs Data Sender",deployed.Topics["document-state"]),
            ["enrichment_dlq_sender"] = ("enrichment-consumer","Azure Event Hubs Data Sender",deployed.Topics["document-events-dlq"]),
            ["enrichment_claim_check_contributor"] = ("enrichment-consumer","Storage Blob Data Contributor",storage + "claim-check"),
            ["verifier_events_receiver"] = ("verifier","Azure Event Hubs Data Receiver",deployed.Topics["document-events"]),
            ["verifier_ledger_reader"] = ("verifier","Storage Blob Data Reader",storage + "ledger"),
            ["verifier_results_contributor"] = ("verifier","Storage Blob Data Contributor",storage + "verifier")
        };
        if (deployed.ProbeNamespaceDataRoles)
        {
            expected["probe_namespace_sender"] = ("probe","Azure Event Hubs Data Sender",deployed.NamespaceId);
            expected["probe_namespace_receiver"] = ("probe","Azure Event Hubs Data Receiver",deployed.NamespaceId);
        }
        if (deployed.Assignments.Count != expected.Count || expected.Any(x => !deployed.Assignments.TryGetValue(x.Key,out var actual) ||
            actual.ServiceAccount != x.Value.Account || actual.RoleDefinitionName != x.Value.Role ||
            !string.Equals(actual.Scope,x.Value.Scope,StringComparison.OrdinalIgnoreCase) || actual.PrincipalId != deployed.Accounts[x.Value.Account].PrincipalId)) return false;
        if (options.ScenarioId != "F06" || options.RunKind == "batch") return true;
        var confirmation = scope.EffectiveScope;
        return confirmation is not null && confirmation.SchemaVersion == 2 &&
            confirmation.NamespaceId == deployed.NamespaceId && confirmation.Accounts.Count == 2 &&
            V2ControlNames.All(alias => confirmation.Accounts.TryGetValue(alias,out var account) && account.NoAdditionalDataAccess && account.PrincipalId == deployed.Accounts[alias].PrincipalId) &&
            scope.EffectiveScopeSha256 is { Length:64 } hash && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    internal static ProbeV2Candidate V2Candidate(ProbeV2Options options, IReadOnlyDictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,
        IReadOnlyDictionary<(string Configuration,string Stage),ProbeV2RepeatProjection[]> checkedSlots, ProbeV2Scope? scope,
        IReadOnlyDictionary<string,ProbeV2Candidate> previous, ProbeV2AccessEnvelope? access = null)
    {
        var result = new List<ProbeV2RepeatProjection>();
        string item = options.ScenarioId, configuration = options.ConfigurationId;
        var actionKey = (configuration,"action");
        evidence.TryGetValue(actionKey,out var actionEvidence);
        bool scopeValid = V2Scope(options,scope);
        var audit = item == "F06" && options.RunKind == "batch" ? V2AccessEvidence(options,scope,access) : new ProbeV2AccessResult([],null);
        var keys = ProbeV2Matrix.Dependencies(item,configuration);
        bool security = false, controls = true;
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            ProbeV2RepeatProjection action = Slot(actionKey,ordinal);
            ProbeV2Repeat? raw = Raw(actionKey,ordinal);
            var reasons = new List<string>(action.Reasons);
            reasons.AddRange(audit.Problems);
            bool invalid = action.Status == "invalid" || audit.Problems.Any(x => x is "v2_wire" or "v2_identity");
            var clients = new List<ProbeV2Client>(action.Clients);
            var references = new List<ProbeV2Reference>();
            ProbeV2Reference? seedReference = null;
            var semantic = action.SemanticOutcome;
            ProbeV2ReadObservation? read = action.ReadObservation;
            foreach (var key in keys.Where(key => key != actionKey))
            {
                var helper = Slot(key,ordinal);
                reasons.AddRange(helper.Reasons);
                invalid |= helper.Status == "invalid";
                clients.AddRange(helper.Clients);
                if (evidence.TryGetValue(key,out var helperEvidence) && helperEvidence.Capture is { } capture && helperEvidence.CaptureSha256 is string hash)
                {
                    var reference = new ProbeV2Reference(options.RunId,item,key.Configuration,key.Stage,options.SourceCommit,capture.JobName,capture.PodUid,hash);
                    references.Add(reference); if (key.Stage == "seed") seedReference = reference;
                    if (actionEvidence?.Capture is { } own && (capture.PodUid == own.PodUid || capture.JobName == own.JobName)) { invalid = true; reasons.Add("v2_identity"); }
                }
            }
            foreach (var key in keys)
            {
                var row = Raw(key,ordinal);
                if (row is null) { reasons.Add("v2_completion"); continue; }
                var matrix = ProbeV2Matrix.For(item,key.Configuration,key.Stage);
                if (matrix.SourceStage is null) continue;
                var sourceKey = (matrix.SourceConfiguration!,matrix.SourceStage);
                var source = Raw(sourceKey,ordinal);
                bool sourceReceipt = evidence.TryGetValue(sourceKey,out var sourceEvidence) && sourceEvidence.Capture is { HandlerExitCode:0, CleanupCompleted:true, HostCleanupCompleted:true } &&
                    V2EvidenceProblems(options,sourceEvidence).Length == 0;
                string admission = sourceReceipt && source is { Terminal:true,Requested:not null } && SingleActionRepeatJudgement.V2Delivered(source) ? "required" :
                    sourceReceipt && source is { Terminal:true,Result:"error",Error:not null } ? "not_applicable_no_delivery" : "not_attempted_dependency";
                if (matrix.Action == "assign_consume")
                {
                    if (row.Admission == "not_attempted_cleanup") { reasons.Add("v2_completion"); continue; }
                    if (admission == "not_attempted_dependency") reasons.Add("v2_location");
                    else if (row.Admission != admission) { invalid = true; reasons.Add("v2_location"); }
                    if (admission == "required" && source?.Delivery is { } delivery)
                    {
                        if (row.Requested?.Topic != delivery.Topic || row.Requested.Partition != delivery.Partition || row.Requested.Offset != delivery.Offset)
                        { invalid = true; reasons.Add("v2_location"); }
                        if (row.ReadObservation.Record is { } record)
                        {
                            bool keysBounded = SingleActionRepeatJudgement.V2Base64(record.KeyBase64,options.Limits.MaxKeyBytes,out bool recordKeyLimited) && !recordKeyLimited &&
                                SingleActionRepeatJudgement.V2Base64(source.Requested?.KeyBase64,options.Limits.MaxKeyBytes,out bool sourceKeyLimited) && !sourceKeyLimited;
                            if (record.Topic != delivery.Topic || record.Partition != delivery.Partition || record.Offset != delivery.Offset || keysBounded && record.KeyBase64 != source.Requested?.KeyBase64)
                            { invalid = true; reasons.Add("v2_location"); }
                        }
                        else if (row.Result == "success" || key.Stage is "read" or "seedread") reasons.Add("v2_location");
                    }
                }
                if (key.Stage == "action" && item == "E01" && source?.Delivery is { } seed && raw?.Delivery is { } target &&
                    (target.Topic != seed.Topic || target.Partition != seed.Partition || target.Offset <= seed.Offset)) { invalid = true; reasons.Add("v2_seed"); }
            }
            if (raw is not null && ProbeV2Matrix.For(item,configuration,"action").Action == "produce_one")
            {
                var reader = Raw((configuration,"read"),ordinal);
                read = reader?.ReadObservation;
                var record = read?.Record;
                semantic = new(raw.Delivery?.Partition,record?.Partition,record?.KeyBase64,
                    record is null ? null : record.ValueBase64 is null ? "null" : record.ValueBase64.Length == 0 ? "empty" : "bytes",
                    record is null ? null : record.ValueBase64 == raw.Requested?.ValueBase64);
                if (raw.Result == "success" && record is null) reasons.Add("v2_location");
            }
            if (options.RunKind == "batch" && item == "B08" && raw?.Result == "success" && read?.Record is not null && semantic.ValueMatch == false) reasons.Add("v2_location");
            if (item == "E01")
            {
                var seed = Raw((configuration,"seed"),ordinal); var seedread = Raw((configuration,"seedread"),ordinal);
                if (seed?.Result != "success" || seedread?.Result != "success" || seedread.ReadObservation.Record?.ValueBase64 != seed.Requested?.ValueBase64)
                { reasons.Add("v2_seed"); controls = false; }
            }
            if (item == "F06" && configuration is "C9" or "C10" or "C11")
            {
                var sourceConfig = ProbeV2Matrix.For(item,configuration,"action").SourceConfiguration!;
                if (Raw((sourceConfig,"action"),ordinal)?.Result != "success") reasons.Add("v2_controls");
            }
            if (!scopeValid) reasons.Add("v2_controls");
            bool successEvidence = raw is not null && (SingleActionRepeatJudgement.V2Delivered(raw) || raw.ReadObservation.Record is not null &&
                raw.ReadObservation.Record.Topic == raw.Requested?.Topic && raw.ReadObservation.Record.Partition == raw.Requested?.Partition &&
                raw.ReadObservation.Record.KeyBase64 == raw.Requested?.KeyBase64 && raw.ReadObservation.Record.Offset == raw.Requested?.Offset && raw.ReadObservation.Record.ValueBase64 == raw.Requested?.ValueBase64);
            bool fidelity = !action.Reasons.Any(x => x is "v2_identity" or "v2_configuration" or "v2_wire");
            security |= item == "F06" && configuration is "C2" or "C4" or "C10" or "C11" && fidelity && successEvidence;
            bool positive = item != "F06" || configuration is "C0" or "C1" or "C3" or "C5" or "C6" or "C7" or "C8" or "C9";
            if (positive && (raw?.Result != "success" || read?.Record is null || item != "E01" && semantic.ValueMatch != true)) controls = false;
            var projected = action with { Status = invalid ? "invalid" : reasons.Count == 0 ? "complete_valid" : "incomplete",
                Reasons = reasons.Distinct(StringComparer.Ordinal).ToArray(), SemanticOutcome = semantic, ReadObservation = read,
                SeedReference = seedReference,ControlReferences = references.ToArray(),Clients = clients.ToArray() };
            result.Add(projected);
        }
        string status = result.Any(x => x.Status == "invalid") ? "invalid" : result.All(x => x.Status == "complete_valid") ? "complete_valid" : "incomplete";
        bool? deterministic = status == "complete_valid" ? result.All(x => V2Equal(item,configuration,result[0],x)) : null;
        var diagnostics = new List<string> { options.RunKind == "smoke" ? "smoke_only" : "full_experiment" };
        if (options.RunKind == "smoke" && !controls) diagnostics.Add("smoke_controls_failed");
        if (audit.AuditHash is string auditHash) diagnostics.Add("permission_audit_sha256:" + auditHash);
        bool final = ProbeV2Matrix.Configurations(item).Last() == configuration;
        if (final)
        {
            string[] prior = ProbeV2Matrix.Configurations(item).TakeWhile(x => x != configuration).ToArray();
            security |= previous.Values.Any(x => x.SecurityFinding);
            if (prior.Any(x => !previous.TryGetValue(x,out var candidate) || options.RunKind == "smoke" && !V2SmokeAccepted(candidate)))
            {
                if (options.RunKind == "smoke") { status = status == "invalid" ? status : "incomplete"; deterministic = null; }
                diagnostics.Add("v2_controls");
            }
        }
        var candidateResult = new ProbeV2Candidate(2,"single-action-v2",true,options.RunId,item,configuration,options.SourceCommit,actionEvidence?.Capture?.ManagedVersion,actionEvidence?.Capture?.NativeVersion,
            options.ImageDigests(),status,result.ToArray(),deterministic,deterministic == true ? result[0].OutcomeClass : null,"not_applicable",diagnostics.ToArray(),security,null);
        if (final && options.RunKind == "batch")
        {
            var all = previous.ToDictionary(x => x.Key,x => x.Value,StringComparer.Ordinal);
            all[configuration] = candidateResult;
            candidateResult = candidateResult with { ItemAssessment = V2JudgeItem(item,all) };
        }
        return candidateResult;

        ProbeV2Repeat? Raw((string Configuration,string Stage) key,int ordinal) => evidence.TryGetValue(key,out var value) ? value.Capture?.Repeats.FirstOrDefault(x => x.Repeat == ordinal) : null;
        ProbeV2RepeatProjection Slot((string Configuration,string Stage) key,int ordinal) => checkedSlots.TryGetValue(key,out var value) && value.Length == 5 ? value[ordinal - 1] :
            new(ordinal,"incomplete",["v2_completion"],null,null,null,new(null,null,null,null,null),null,null,null,[],[]);
    }

    internal static bool V2Equal(string item,string configuration,ProbeV2RepeatProjection first,ProbeV2RepeatProjection other)
    {
        if (first.OutcomeClass is null || first.OutcomeClass != other.OutcomeClass) return false;
        if (first.OutcomeClass.Result != "success") return true;
        var a = first.SemanticOutcome; var b = other.SemanticOutcome;
        return item switch
        {
            "B08" => a.DeliveredPartition == b.DeliveredPartition && a.ConsumedPartition == b.ConsumedPartition && a.KeyBase64 == b.KeyBase64 && a.ValueMatch == b.ValueMatch,
            "E01" => a.ValueKind == b.ValueKind,
            "F06" => a.ConsumedPartition == b.ConsumedPartition && a.ValueKind == b.ValueKind && a.ValueMatch == b.ValueMatch &&
                (configuration is "C0" or "C1" or "C2" or "C3" or "C4" or "C5" ? a.DeliveredPartition == b.DeliveredPartition : a.DeliveredPartition is null && b.DeliveredPartition is null),
            _ => false
        };
    }
    internal static bool V2SmokeAccepted(ProbeV2Candidate candidate) => candidate.Status == "complete_valid" && candidate.Deterministic == true &&
        !candidate.SecurityFinding && candidate.ItemAssessment is null && candidate.Diagnostics.Contains("smoke_only",StringComparer.Ordinal) && !candidate.Diagnostics.Contains("smoke_controls_failed",StringComparer.Ordinal);
    internal static SingleActionItemAssessment V2JudgeItem(string item,IReadOnlyDictionary<string,ProbeV2Candidate> candidates)
    {
        string[] labels = item switch { "B08" => ["H_partition"], "E01" => ["H_transport"], "F06" => ["H_positive_scope","H_negative_surface"], _ => [] };
        var hypotheses = labels.ToDictionary(x => x,_ => "undecided",StringComparer.Ordinal);
        SingleActionItemAssessment Result(string? verdict,string? reason = null) => new(verdict,hypotheses,reason);
        string[] configurations = ProbeV2Matrix.Configurations(item);
        if (candidates.Values.Any(x => x.SecurityFinding)) return Result(null,"security_finding_requires_hari");
        if (configurations.Any(x => !candidates.ContainsKey(x))) return Result(null,"missing_configuration");
        if (configurations.Any(x => candidates[x].Status != "complete_valid" || candidates[x].Diagnostics.Any(d => d is "v2_controls" or "v2_capture_limit" or "v2_access_evidence" or "v2_access_configuration"))) return Result(null,"incomplete_configuration");
        if (configurations.Any(x => candidates[x].Deterministic != true || candidates[x].OutcomeClass is null)) return Result(null,"unequal_classes_separate_full_plan");
        if (configurations.Any(x => candidates[x].OutcomeClass!.Origin == "construction")) return Result("untestable","construction_origin");
        bool Success(string configuration) => candidates[configuration].OutcomeClass!.Result == "success";
        if (item == "B08")
        {
            if (configurations.Any(x => !Success(x))) return Result("untestable","action_error");
            bool holds = configurations.All(c => candidates[c].Repeats.All(r => r.SemanticOutcome.DeliveredPartition == (c == "C0" ? 30 : 2) && r.SemanticOutcome.ConsumedPartition == (c == "C0" ? 30 : 2) && r.SemanticOutcome.ValueMatch == true));
            hypotheses["H_partition"] = holds ? "holds" : "contradicted";
            return Result(holds ? "confirmed" : "contradicted");
        }
        if (item == "E01")
        {
            if (!Success("C0") || candidates["C0"].Repeats.Any(x => x.SemanticOutcome.ValueKind != "empty")) return Result("untestable","failed_control");
            if (!Success("C1")) return Result("untestable","action_error");
            bool holds = candidates["C1"].Repeats.All(x => x.SemanticOutcome.ValueKind == "null");
            hypotheses["H_transport"] = holds ? "holds" : "contradicted";
            return Result(holds ? "confirmed" : "contradicted");
        }
        if (item == "F06")
        {
            if (candidates.Values.Any(c => c.Diagnostics.Count(d => d.StartsWith("permission_audit_sha256:",StringComparison.Ordinal)) != 1) || candidates.Values.SelectMany(c => c.Diagnostics.Where(d => d.StartsWith("permission_audit_sha256:",StringComparison.Ordinal))).Distinct(StringComparer.Ordinal).Count() != 1) return Result(null,"incomplete_configuration");
            if (V2PositiveConfigurations.Any(c => !Success(c))) return Result("untestable","failed_control");
            hypotheses["H_positive_scope"] = "holds"; hypotheses["H_negative_surface"] = "recorded_only";
            return Result("undocumented");
        }
        return Result(null,"unknown_item");
    }
    private static bool V2AssessmentShape(ProbeV2Options options,string configuration,ProbeV2Candidate candidate)
    {
        bool smoke = candidate.Diagnostics.Contains("smoke_only",StringComparer.Ordinal), batch = candidate.Diagnostics.Contains("full_experiment",StringComparer.Ordinal);
        if (smoke == batch || smoke != (options.RunKind == "smoke")) return false;
        if (options.RunKind == "smoke") return candidate.ItemAssessment is null;
        if (candidate.Diagnostics.Contains("smoke_controls_failed",StringComparer.Ordinal)) return false;
        var audit = candidate.Diagnostics.Where(x => x.StartsWith("permission_audit_sha256:",StringComparison.Ordinal)).ToArray();
        if (audit.Length > 1 || audit.Any(x => options.ScenarioId != "F06" || x.Length != 88 || !x[24..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))) return false;
        if (configuration != ProbeV2Matrix.Configurations(options.ScenarioId).Last()) return candidate.ItemAssessment is null;
        var assessment = candidate.ItemAssessment;
        if (assessment is null) return true;
        string[] labels = options.ScenarioId switch { "B08" => ["H_partition"],"E01" => ["H_transport"],_ => ["H_negative_surface","H_positive_scope"] };
        return assessment.Verdict is null or "confirmed" or "contradicted" or "undocumented" or "untestable" &&
            assessment.Hypotheses.Keys.Order(StringComparer.Ordinal).SequenceEqual(labels,StringComparer.Ordinal) &&
            assessment.Hypotheses.Values.All(x => x is "holds" or "contradicted" or "recorded_only" or "undecided") &&
            assessment.Reason is null or "security_finding_requires_hari" or "missing_configuration" or "incomplete_configuration" or "unequal_classes_separate_full_plan" or "construction_origin" or "action_error" or "failed_control";
    }
    internal static bool V2Published(ProbeV2Options options,string configuration,ProbeV2Candidate? candidate,ProbeV2JudgeReceipt? receipt,byte[]? raw,string? auditHash = null)
    {
        if (candidate is null || receipt is null || raw is null) return false;
        string prefix = ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,configuration);
        string job = ProbeV2Matrix.Job(options.RunId,options.ScenarioId,configuration,"judge");
        return candidate.SchemaVersion == 2 && candidate.Mode == "single-action-v2" && candidate.Synthetic && candidate.RunId == options.RunId &&
            candidate.ScenarioId == options.ScenarioId && candidate.ConfigurationId == configuration && candidate.SourceCommit == options.SourceCommit &&
            candidate.Adr012Counts == "not_applicable" && V2AssessmentShape(options,configuration,candidate) &&
            (options.RunKind != "batch" || options.ScenarioId != "F06" || (auditHash is null ? candidate.ItemAssessment?.Verdict is null : candidate.Diagnostics.Contains("permission_audit_sha256:" + auditHash,StringComparer.Ordinal))) && candidate.Repeats.Length == 5 &&
            candidate.Repeats.All(x => x.Status is "invalid" or "incomplete" or "complete_valid") &&
            candidate.Status == (candidate.Repeats.Any(x => x.Status == "invalid") ? "invalid" : candidate.Repeats.All(x => x.Status == "complete_valid") ? "complete_valid" : "incomplete") &&
            candidate.Deterministic == (candidate.Status == "complete_valid" ? candidate.Repeats.All(x => V2Equal(options.ScenarioId,configuration,candidate.Repeats[0],x)) : (bool?)null) &&
            candidate.OutcomeClass == (candidate.Deterministic == true ? candidate.Repeats[0].OutcomeClass : null) && candidate.Repeats.Select(x => x.Repeat).SequenceEqual(Enumerable.Range(1,5)) &&
            V2Images(candidate.ImageDigests,options.ImageDigests()) && receipt.SchemaVersion == 2 && receipt.Mode == "single-action-v2-judge" &&
            receipt.RunId == options.RunId && receipt.ScenarioId == options.ScenarioId && receipt.ConfigurationId == configuration && receipt.SourceCommit == options.SourceCommit &&
            receipt.JobName == job && Pod(receipt.PodName,job) && SingleActionWire.Text(receipt.PodUid) && receipt.CandidateName == prefix + "candidate.json" &&
            receipt.CandidateSha256 == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(raw)) && receipt.HandlerExitCode == 0 && receipt.CleanupCompleted;
    }
    internal static ProbeV2AccessResult V2AccessEvidence(ProbeV2Options options, ProbeV2Scope? scope, ProbeV2AccessEnvelope? access)
    {
        static ProbeV2AccessResult Fail(string reason) => new([reason],null);
        if (access is null) return Fail("v2_access_evidence");
        if (scope?.Deployed is null) return Fail("v2_identity");
        string[] accounts = ["enrichment-consumer","loadgen"];
        string[] topics = ["document-events","document-events-dlq","document-state"];
        if (access.SchemaVersion != 1 || access.Mode != "azure_rbac_configuration_check") return Fail("v2_wire");
        if (access.RunId != options.RunId || access.SourceCommit != options.SourceCommit || access.NamespaceId != scope.Deployed.NamespaceId ||
            !access.Principals.Keys.Order(StringComparer.Ordinal).SequenceEqual(accounts) || !access.Targets.Keys.Order(StringComparer.Ordinal).SequenceEqual(topics) ||
            accounts.Any(x => !scope.Deployed.Accounts.TryGetValue(x,out var a) || access.Principals[x] != a.PrincipalId) ||
            topics.Any(x => !scope.Deployed.Topics.TryGetValue(x,out string? target) || access.Targets[x] != target)) return Fail("v2_identity");
        if (access.StartedUnixMs < 0 || access.CompletedUnixMs < access.StartedUnixMs || access.CompletedUnixMs - access.StartedUnixMs > 600000) return Fail("v2_wire");
        if (access.ProjectionBase64.Length > ((options.JudgeLimits.MaxEffectiveScopeBytes + 2) / 3) * 4) return Fail("v2_capture_limit");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(access.ProjectionBase64); }
        catch (FormatException) { return Fail("v2_wire"); }
        if (bytes.Length > options.JudgeLimits.MaxEffectiveScopeBytes) return Fail("v2_capture_limit");
        if (!V2AccessHash(access.ProjectionSha256) || access.ProjectionSha256 != Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))) return Fail("v2_identity");
        ProbeV2AccessProjection projection;
        try
        {
            using var document = JsonDocument.Parse(bytes,new JsonDocumentOptions { MaxDepth = 32 });
            if (!V2AccessBounds(document.RootElement)) return Fail("v2_capture_limit");
            projection = ProbeV2Wire.Read<ProbeV2AccessProjection>(bytes,options.JudgeLimits.MaxEffectiveScopeBytes);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return Fail("v2_wire"); }
        if (projection.SchemaVersion != 1 || projection.Mode != "azure_rbac_configuration_check" ||
            projection.StartedUnixMs != access.StartedUnixMs || projection.CompletedUnixMs != access.CompletedUnixMs) return Fail("v2_wire");
        string binding = "probe-v2-access-v1\n" + options.RunId + "\n" + options.SourceCommit + "\n" + scope.Deployed.NamespaceId.ToLowerInvariant() + "\n" +
            "loadgen=" + access.Principals["loadgen"].ToLowerInvariant() + "\n" + "enrichment-consumer=" + access.Principals["enrichment-consumer"].ToLowerInvariant() + "\n" +
            string.Concat(topics.Select(x => x + "=" + access.Targets[x].ToLowerInvariant() + "\n"));
        if (projection.RunId != options.RunId || projection.SourceCommit != options.SourceCommit || !V2AccessHash(projection.BindingSha256) ||
            projection.BindingSha256 != Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(binding)))) return Fail("v2_identity");
        string[] reasons = ["input_invalid","platform_unsupported","membership_unavailable","assignment_unavailable","definition_unavailable","membership_shape","assignment_shape","definition_shape","unsupported_grant","configuration_mismatch","pagination_invalid","request_limit","page_limit","group_limit","definition_limit","assignment_limit","private_file_limit","response_limit","raw_bytes_limit","projection_limit","time_limit","transport_failure","safety_stop"];
        if (!projection.Reasons.SequenceEqual(projection.Reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) || projection.Reasons.Any(x => !reasons.Contains(x,StringComparer.Ordinal)) ||
            projection.Status is not ("complete" or "incomplete" or "mismatch") ||
            (projection.Status == "complete" && projection.Reasons.Length != 0) ||
            (projection.Status == "incomplete" && projection.Reasons.Length == 0) ||
            (projection.Status == "mismatch" && !projection.Reasons.SequenceEqual(new[] { "configuration_mismatch" }))) return Fail("v2_wire");
        Dictionary<string,long> limits = new(StringComparer.Ordinal)
        {
            ["audit_seconds"] = 600,["requests"] = 256,["pages_per_collection"] = 8,["groups"] = 64,["definitions"] = 64,["assignments"] = 4096,
            ["private_files"] = 1024,["response_bytes"] = 1048576,["total_raw_bytes"] = 16777216,["projection_bytes"] = 32768,["automatic_retries"] = 0,["first_admission_age_ms"] = 60000
        };
        if (projection.SourceLimits.Count != limits.Count || limits.Any(x => !projection.SourceLimits.TryGetValue(x.Key,out long n) || x.Value != n) ||
            projection.Observed.Requests < 0 || projection.Observed.Requests > 256 || projection.Observed.ApiVersions.Count != 2 ||
            !projection.Observed.ApiVersions.TryGetValue("arm",out string? arm) || arm != "2022-04-01" ||
            !projection.Observed.ApiVersions.TryGetValue("graph",out string? graph) || graph != "v1.0") return Fail("v2_wire");
        var coverage = projection.Coverage;
        if (!coverage.Accounts.Keys.Order(StringComparer.Ordinal).SequenceEqual(accounts) || !coverage.Targets.SequenceEqual(topics) ||
            coverage.Definitions.Any(x => !V2AccessAlias(x.Alias,"role:") || !V2AccessHash(x.RawSha256)) ||
            coverage.Definitions.Select(x => x.Alias).Distinct(StringComparer.Ordinal).Count() != coverage.Definitions.Length) return Fail("v2_wire");
        bool completed = true;
        int queryHashes = coverage.Definitions.Length;
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (string account in accounts)
        {
            var value = coverage.Accounts[account];
            if (value.Groups.Distinct(StringComparer.Ordinal).Count() != value.Groups.Length || value.DirectoryRoles.Distinct(StringComparer.Ordinal).Count() != value.DirectoryRoles.Length ||
                value.Groups.Any(x => !V2AccessAlias(x,"group:")) || value.DirectoryRoles.Any(x => !V2AccessAlias(x,"directory_role:")) ||
                value.MembershipPages.Any(x => !V2AccessHash(x))) return Fail("v2_wire");
            groups.UnionWith(value.Groups);
            queryHashes += value.MembershipPages.Length;
            string[] subjects = [account,..value.Groups];
            if (value.AssignmentCollections.Any(x => !subjects.Contains(x.Subject,StringComparer.Ordinal) || x.Pages.Any(y => !V2AccessHash(y))) ||
                value.AssignmentCollections.Select(x => x.Subject).Distinct(StringComparer.Ordinal).Count() != value.AssignmentCollections.Length) return Fail("v2_wire");
            bool accountComplete = value.MembershipComplete && value.MembershipPages.Length > 0 &&
                value.AssignmentCollections.Length == subjects.Length && value.AssignmentCollections.All(x => x.Complete && x.Pages.Length > 0);
            completed &= accountComplete;
            queryHashes += value.AssignmentCollections.Sum(x => x.Pages.Length);
        }
        if (groups.Count > 64) return Fail("v2_capture_limit");
        if (projection.Observed.Requests < queryHashes || (projection.Status != "incomplete" && projection.Observed.Requests == 0)) return Fail("v2_wire");
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var assignmentBindings = new Dictionary<string,string>(StringComparer.Ordinal);
        var table = new HashSet<string>(StringComparer.Ordinal);
        string[] definitions = coverage.Definitions.Select(x => x.Alias).ToArray();
        foreach (var entry in projection.Entries)
        {
            if (!accounts.Contains(entry.Account,StringComparer.Ordinal) || !topics.Contains(entry.Target,StringComparer.Ordinal) ||
                !coverage.Accounts.TryGetValue(entry.Account,out var owner) || (entry.Subject != entry.Account && !owner.Groups.Contains(entry.Subject,StringComparer.Ordinal)) ||
                entry.Operation is not ("send" or "receive") || entry.Role != (entry.Operation == "send" ? "sender" : "receiver") ||
                entry.ScopeClass != "event_hub" || !definitions.Contains(entry.DefinitionAlias,StringComparer.Ordinal) ||
                !V2AccessAlias(entry.AssignmentAlias,"assignment:")) return Fail("v2_wire");
            string tuple = string.Join('|',entry.Account,entry.Subject,entry.Target,entry.Operation,entry.Role,entry.AssignmentAlias,entry.DefinitionAlias,entry.ScopeClass);
            if (!entryIds.Add(tuple)) return Fail("v2_wire");
            string assignment = entry.Account + "|" + entry.AssignmentAlias;
            string interpretation = entry.Subject + "|" + entry.DefinitionAlias + "|" + entry.Target + "|" + entry.Operation;
            if (assignmentBindings.TryGetValue(assignment,out string? old) && old != interpretation) return Fail("v2_wire");
            assignmentBindings[assignment] = interpretation;
            table.Add(entry.Account + "|" + entry.Target + "|" + entry.Operation);
        }
        string[] expected = ["loadgen|document-events|send","enrichment-consumer|document-events|receive","enrichment-consumer|document-state|send","enrichment-consumer|document-events-dlq|send"];
        string[] unsupportedReasons = ["custom_role","condition","delegation","unfamiliar_operation","unmatched_scope","definition_shape","unexpected_grant"];
        string[] scopeClasses = ["event_hub","consumer_group","namespace","resource_group","subscription","management_group","other"];
        foreach (var entry in projection.Unsupported)
            if (!accounts.Contains(entry.Account,StringComparer.Ordinal) ||
                !coverage.Accounts.TryGetValue(entry.Account,out var owner) || (entry.Subject != entry.Account && !owner.Groups.Contains(entry.Subject,StringComparer.Ordinal)) ||
                !scopeClasses.Contains(entry.ScopeClass,StringComparer.Ordinal) || !unsupportedReasons.Contains(entry.Reason,StringComparer.Ordinal) || !V2AccessHash(entry.RawSha256)) return Fail("v2_wire");
        if (projection.Entries.Select(x => x.AssignmentAlias).Distinct(StringComparer.Ordinal).Count() > 4096) return Fail("v2_capture_limit");
        bool tableMatches = table.SetEquals(expected);
        if (projection.Status == "complete" && (!completed || projection.Unsupported.Length != 0 || !tableMatches)) return Fail("v2_wire");
        if (projection.Status == "mismatch" && (!completed || (tableMatches && !projection.Unsupported.Any(x => x.Reason == "unexpected_grant")) ||
            projection.Unsupported.Any(x => x.Reason != "unexpected_grant"))) return Fail("v2_wire");
        if (projection.Status == "mismatch") return Fail("v2_access_configuration");
        if (projection.Status == "incomplete")
        {
            string[] caps = ["request_limit","page_limit","group_limit","definition_limit","assignment_limit","private_file_limit","response_limit","raw_bytes_limit","projection_limit"];
            return new(projection.Reasons.Select(x => caps.Contains(x,StringComparer.Ordinal) ? "v2_capture_limit" : "v2_access_evidence").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),null);
        }
        return new([],access.ProjectionSha256);
    }

    private static bool V2AccessHash(string value) => value.Length == 64 && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool V2AccessAlias(string value,string prefix) => value.StartsWith(prefix,StringComparison.Ordinal) && V2AccessHash(value[prefix.Length..]);
    private static bool V2AccessBounds(JsonElement value,string name = "",bool entry = false)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            int maximum = name switch { "reasons" => 23,"entries" => 8192,"unsupported" => 4096,"groups" or "directory_roles" or "definitions" => 64,"assignment_collections" => 65,"membership_pages" or "pages" => 8,_ => int.MaxValue };
            if (value.GetArrayLength() > maximum) return false;
            foreach (var child in value.EnumerateArray())
            {
                bool isEntry = name == "entries";
                if (isEntry && JsonSerializer.SerializeToUtf8Bytes(child).Length > 1024) return false;
                if (!V2AccessBounds(child,name,isEntry)) return false;
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
                if (!V2AccessBounds(property.Value,property.Name,entry)) return false;
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (Encoding.UTF8.GetByteCount(text) > (entry ? 96 : 128) || (entry && (text.Length == 0 || text.Any(x => x > 127)))) return false;
        }
        return true;
    }
}
