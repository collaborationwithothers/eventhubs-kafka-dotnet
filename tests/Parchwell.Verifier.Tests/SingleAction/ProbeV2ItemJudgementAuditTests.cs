using System.Security.Cryptography;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2ItemJudgementAuditTests
{
    private static readonly SingleActionOutcome Success = new("success",null,null,null);
    private static readonly string Audit = "permission_audit_sha256:" + new string('a',64);

    [Theory]
    [InlineData("B08","C0","none","none",0,0,false)]
    [InlineData("B08","C0","success","error",0,0,false)]
    [InlineData("F06","C0","success","success",0,0,true)]
    [InlineData("F06","C1","success","success",0,0,true)]
    [InlineData("F06","C2","success","success",0,0,true)]
    [InlineData("F06","C3","success","success",0,0,true)]
    [InlineData("F06","C4","success","success",0,0,true)]
    [InlineData("F06","C5","success","success",0,0,true)]
    [InlineData("F06","C6","success","success",0,0,false)]
    [InlineData("F06","C6","success","success",null,null,true)]
    [InlineData("F06","C6","success","success",null,0,false)]
    [InlineData("F06","C11","success","success",0,null,false)]
    [InlineData("X01","C0","success","success",0,0,false)]
    public void Given_OutcomeClassesAndDeliveredPartitions_When_Compared_Then_OnlyTheItemRuleDecidesEquality(string item,string configuration,string first,string other,int? firstDelivered,int? otherDelivered,bool expected)
    {
        static SingleActionOutcome? Outcome(string name) => name switch { "success" => Success,"error" => new("error",29,false,"broker"),_ => null };
        static ProbeV2RepeatProjection Row(SingleActionOutcome? outcome,int? delivered) => new(1,"complete_valid",[],outcome,null,null,new(delivered,0,null,"bytes",true),null,null,null,[],[]);
        Assert.Equal(expected,SingleActionConfigurationJudgement.V2Equal(item,configuration,Row(Outcome(first),firstDelivered),Row(Outcome(other),otherDelivered)));
    }

    [Theory]
    [InlineData("","confirmed",null)]
    [InlineData("v2_controls",null,"incomplete_configuration")]
    [InlineData("v2_capture_limit",null,"incomplete_configuration")]
    [InlineData("v2_access_evidence",null,"incomplete_configuration")]
    [InlineData("v2_access_configuration",null,"incomplete_configuration")]
    [InlineData("smoke_only","confirmed",null)]
    public void Given_ConfigurationDiagnostic_When_ItemJudged_Then_OnlyCompletionBlockersWithholdTheVerdict(string diagnostic,string? verdict,string? reason)
    {
        var candidates = Judged("B08");
        if (diagnostic.Length != 0) candidates["C0"] = candidates["C0"] with { Diagnostics = ["full_experiment",diagnostic] };
        var result = SingleActionConfigurationJudgement.V2JudgeItem("B08",candidates);
        Assert.Equal(verdict,result.Verdict); Assert.Equal(reason,result.Reason);
    }

    [Theory]
    [InlineData(true,true,"confirmed",null)]
    [InlineData(false,true,null,"unequal_classes_separate_full_plan")]
    [InlineData(true,false,null,"unequal_classes_separate_full_plan")]
    public void Given_DeterminismAndOutcomeClass_When_ItemJudged_Then_BothAreRequiredForAVerdict(bool deterministic,bool outcome,string? verdict,string? reason)
    {
        var candidates = Judged("B08");
        candidates["C1"] = candidates["C1"] with { Deterministic = deterministic,OutcomeClass = outcome ? Success : null };
        var result = SingleActionConfigurationJudgement.V2JudgeItem("B08",candidates);
        Assert.Equal(verdict,result.Verdict); Assert.Equal(reason,result.Reason);
    }

    [Theory]
    [InlineData("B08","none","confirmed",null)]
    [InlineData("B08","delivered","contradicted",null)]
    [InlineData("B08","consumed","contradicted",null)]
    [InlineData("E01","none","confirmed",null)]
    [InlineData("E01","control","untestable","failed_control")]
    [InlineData("E01","transport","contradicted",null)]
    [InlineData("F06","none","undocumented",null)]
    [InlineData("F06","positive","untestable","failed_control")]
    public void Given_OneContraryRepeat_When_ItemJudged_Then_EveryRepeatMustSupportTheHypothesis(string item,string change,string? verdict,string? reason)
    {
        var candidates = Judged(item);
        (string configuration,Func<ProbeV2Semantic,ProbeV2Semantic> edit) = change switch
        {
            "delivered" => ("C1",s => s with { DeliveredPartition = 1 }),
            "consumed" => ("C1",s => s with { ConsumedPartition = 1 }),
            "control" => ("C0",s => s with { ValueKind = "bytes" }),
            "transport" => ("C1",s => s with { ValueKind = "empty" }),
            "positive" => ("C3",s => s with { ValueMatch = false }),
            _ => ("C0",(Func<ProbeV2Semantic,ProbeV2Semantic>)(s => s))
        };
        var target = candidates[configuration];
        candidates[configuration] = target with { Repeats = target.Repeats.Select(r => r.Repeat == 5 ? r with { SemanticOutcome = edit(r.SemanticOutcome) } : r).ToArray() };
        var result = SingleActionConfigurationJudgement.V2JudgeItem(item,candidates);
        Assert.Equal(verdict,result.Verdict); Assert.Equal(reason,result.Reason);
    }

    [Fact]
    public void Given_UnknownItem_When_ItemJudged_Then_NoVerdictAndTheUnknownItemReason()
    {
        var result = SingleActionConfigurationJudgement.V2JudgeItem("X01",new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Null(result.Verdict); Assert.Equal("unknown_item",result.Reason); Assert.Empty(result.Hypotheses);
    }

    [Theory]
    [InlineData("shared","undocumented",null)]
    [InlineData("missing",null,"incomplete_configuration")]
    [InlineData("duplicate",null,"incomplete_configuration")]
    [InlineData("different",null,"incomplete_configuration")]
    public void Given_PermissionAuditDiagnostics_When_F06Judged_Then_ExactlyOneSharedAuditAdmitsTheVerdict(string change,string? verdict,string? reason)
    {
        var candidates = Judged("F06");
        string[] diagnostics = change switch
        {
            "missing" => ["full_experiment"],
            "duplicate" => ["full_experiment",Audit,Audit],
            "different" => ["full_experiment","permission_audit_sha256:" + new string('b',64)],
            _ => ["full_experiment",Audit]
        };
        candidates["C3"] = candidates["C3"] with { Diagnostics = diagnostics };
        var result = SingleActionConfigurationJudgement.V2JudgeItem("F06",candidates);
        Assert.Equal(verdict,result.Verdict); Assert.Equal(reason,result.Reason);
    }

    [Theory]
    [InlineData("batch","full_experiment",true)]
    [InlineData("batch","",false)]
    [InlineData("batch","smoke_only",false)]
    [InlineData("batch","smoke_only,full_experiment",false)]
    [InlineData("batch","full_experiment,smoke_controls_failed",false)]
    [InlineData("smoke","smoke_only",true)]
    [InlineData("smoke","smoke_only,smoke_controls_failed",true)]
    [InlineData("smoke","full_experiment",false)]
    public void Given_RunKindMarkers_When_PublishedChecked_Then_OnlyTheMatchingRunShapeIsAdmitted(string runKind,string markers,bool expected)
    {
        var (options,candidate) = Batch("B08","C0"); options.RunKind = runKind;
        candidate = candidate with { Diagnostics = markers.Split(',',StringSplitOptions.RemoveEmptyEntries) };
        Assert.Equal(expected,Published(options,candidate));
    }

    [Theory]
    [InlineData("F06","none",true)]
    [InlineData("F06","/",false)][InlineData("F06","0",true)][InlineData("F06","5",true)][InlineData("F06","9",true)][InlineData("F06",":",false)]
    [InlineData("F06","`",false)][InlineData("F06","a",true)][InlineData("F06","b",true)][InlineData("F06","f",true)][InlineData("F06","g",false)]
    [InlineData("F06","mixed",false)]
    [InlineData("F06","short",false)]
    [InlineData("F06","long",false)]
    [InlineData("F06","two",false)]
    [InlineData("B08","a",false)]
    public void Given_PermissionAuditDiagnosticShape_When_PublishedChecked_Then_OnlyOneLowercaseHexAuditOnF06IsAdmitted(string item,string audit,bool expected)
    {
        var (options,candidate) = Batch(item,"C0");
        string[] hashes = audit switch
        {
            "none" => [],
            "mixed" => [new string('a',63) + "g"],
            "short" => [new string('a',63)],
            "long" => [new string('a',65)],
            "two" => [new string('a',64),new string('a',64)],
            _ => [new string(audit[0],64)]
        };
        candidate = candidate with { Diagnostics = ["full_experiment",..hashes.Select(h => "permission_audit_sha256:" + h)] };
        Assert.Equal(expected,Published(options,candidate));
    }

    [Theory]
    [InlineData("B08","absent","","",true)]
    [InlineData("B08","null","H_partition=undecided","null",true)]
    [InlineData("B08","confirmed","H_partition=holds","null",true)]
    [InlineData("B08","contradicted","H_partition=contradicted","null",true)]
    [InlineData("B08","undocumented","H_partition=recorded_only","null",true)]
    [InlineData("B08","untestable","H_partition=undecided","null",true)]
    [InlineData("B08","other","H_partition=holds","null",false)]
    [InlineData("B08","confirmed","H_transport=holds","null",false)]
    [InlineData("B08","confirmed","H_partition=other","null",false)]
    [InlineData("B08","null","H_partition=undecided","security_finding_requires_hari",true)]
    [InlineData("B08","null","H_partition=undecided","missing_configuration",true)]
    [InlineData("B08","null","H_partition=undecided","incomplete_configuration",true)]
    [InlineData("B08","null","H_partition=undecided","unequal_classes_separate_full_plan",true)]
    [InlineData("B08","untestable","H_partition=undecided","construction_origin",true)]
    [InlineData("B08","untestable","H_partition=undecided","action_error",true)]
    [InlineData("B08","untestable","H_partition=undecided","failed_control",true)]
    [InlineData("B08","null","H_partition=undecided","other",false)]
    [InlineData("E01","confirmed","H_transport=holds","null",true)]
    [InlineData("F06","undocumented","H_negative_surface=recorded_only;H_positive_scope=holds","null",true)]
    [InlineData("F06","undocumented","H_positive_scope=holds;H_negative_surface=other","null",false)]
    public void Given_FinalItemAssessmentShape_When_PublishedChecked_Then_OnlyDeclaredVerdictsLabelsValuesAndReasonsAreAdmitted(string item,string verdict,string hypotheses,string reason,bool expected)
    {
        string final = ProbeV2Matrix.Configurations(item).Last();
        var (options,candidate) = Batch(item,final);
        var labels = hypotheses.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=')).ToDictionary(x => x[0],x => x[1],StringComparer.Ordinal);
        candidate = candidate with { ItemAssessment = verdict == "absent" ? null : new(verdict == "null" ? null : verdict,labels,reason == "null" ? null : reason) };
        Assert.Equal(expected,Published(options,candidate,item == "F06" ? AuditHash(candidate) : null));
    }

    [Theory]
    [InlineData("C11","match",true)]
    [InlineData("C11","other",false)]
    [InlineData("C11","none",false)]
    [InlineData("C0","match",true)]
    [InlineData("C0","none",true)]
    public void Given_F06BatchAuditBinding_When_PublishedChecked_Then_TheCandidateCarriesTheAuditOrNoVerdict(string configuration,string binding,bool expected)
    {
        var (options,candidate) = Batch("F06",configuration);
        string? hash = binding switch { "match" => AuditHash(candidate),"other" => new string('b',64),_ => null };
        Assert.Equal(expected,Published(options,candidate,hash));
    }

    [Theory]
    [InlineData("none",true)]
    [InlineData("invalid",true)]
    [InlineData("incomplete",true)]
    [InlineData("unknown",false)]
    [InlineData("unequal",true)]
    public void Given_RepeatStatusesAndClasses_When_PublishedChecked_Then_AggregateStatusDeterminismAndClassMustFollowTheRepeats(string change,bool expected)
    {
        var (options,candidate) = Batch("B08","C0");
        var last = candidate.Repeats[4];
        candidate = change switch
        {
            "invalid" => With(candidate,last with { Status = "invalid" },"invalid",null),
            "incomplete" => With(candidate,last with { Status = "incomplete" },"incomplete",null),
            "unknown" => With(candidate,last with { Status = "unknown" },"incomplete",null),
            "unequal" => With(candidate,last with { SemanticOutcome = last.SemanticOutcome with { DeliveredPartition = 29 } },"complete_valid",false),
            _ => candidate
        };
        Assert.Equal(expected,Published(options,candidate));

        static ProbeV2Candidate With(ProbeV2Candidate candidate,ProbeV2RepeatProjection last,string status,bool? deterministic) =>
            candidate with { Repeats = [..candidate.Repeats.Take(4),last],Status = status,Deterministic = deterministic,OutcomeClass = null };
    }

    private static Dictionary<string,ProbeV2Candidate> Judged(string item) => ProbeV2Matrix.Configurations(item).ToDictionary(configuration => configuration,configuration =>
    {
        ProbeV2Semantic semantic = item switch
        {
            "B08" => new(configuration == "C0" ? 30 : 2,configuration == "C0" ? 30 : 2,null,"bytes",true),
            "E01" => new(null,0,null,configuration == "C0" ? "empty" : "null",true),
            _ => new(0,0,null,"bytes",true)
        };
        var repeats = Enumerable.Range(1,5).Select(i => new ProbeV2RepeatProjection(i,"complete_valid",[],Success,null,null,semantic,null,null,null,[],[])).ToArray();
        string[] diagnostics = item == "F06" ? ["full_experiment",Audit] : ["full_experiment"];
        return new ProbeV2Candidate(2,"single-action-v2",true,"run1",item,configuration,ProbeV2Fixture.Source,null,null,new Dictionary<string,string>(StringComparer.Ordinal),
            "complete_valid",repeats,true,Success,"not_applicable",diagnostics,false,null);
    },StringComparer.Ordinal);

    private static (ProbeV2Options Options,ProbeV2Candidate Candidate) Batch(string item,string configuration)
    {
        var options = ProbeV2Fixture.Options(item,configuration); options.RunKind = "batch"; var evidence = ProbeV2Fixture.Evidence(options);
        var previous = ProbeV2Matrix.Configurations(item).Last() == configuration
            ? ProbeV2Matrix.Configurations(item).TakeWhile(c => c != configuration).ToDictionary(c => c,c => Batch(item,c).Candidate,StringComparer.Ordinal)
            : new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        return (options,SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),previous,
            item == "F06" ? ProbeV2AccessEvidenceTests.Access(options) : null));
    }

    private static string AuditHash(ProbeV2Candidate candidate) =>
        candidate.Diagnostics.Single(x => x.StartsWith("permission_audit_sha256:",StringComparison.Ordinal))["permission_audit_sha256:".Length..];

    private static bool Published(ProbeV2Options options,ProbeV2Candidate candidate,string? auditHash = null)
    {
        byte[] raw = ProbeV2Wire.Write(candidate,16777216);
        var receipt = new ProbeV2JudgeReceipt(2,"single-action-v2-judge",options.RunId,options.ScenarioId,options.ConfigurationId,options.SourceCommit,options.JobName,options.PodName,options.PodUid,
            ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,options.ConfigurationId) + "candidate.json",Convert.ToHexStringLower(SHA256.HashData(raw)),0,true);
        return SingleActionConfigurationJudgement.V2Published(options,options.ConfigurationId,candidate,receipt,raw,auditHash);
    }
}
