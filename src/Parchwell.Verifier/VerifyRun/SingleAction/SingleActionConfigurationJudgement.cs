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
}
