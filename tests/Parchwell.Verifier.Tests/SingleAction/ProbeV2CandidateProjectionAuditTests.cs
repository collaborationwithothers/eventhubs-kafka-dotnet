using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2CandidateProjectionAuditTests
{
    [Theory]
    [InlineData("complete","complete_valid","")]
    [InlineData("missing","incomplete","v2_access_evidence")]
    [InlineData("wire","invalid","v2_wire")]
    [InlineData("identity","invalid","v2_identity")]
    public void Given_PermissionAuditOutcome_When_Projected_Then_EveryRepeatCarriesItsProblem(string audit,string status,string reasons)
    {
        var options = ProbeV2Fixture.Options("F06"); options.RunKind = "batch"; var evidence = ProbeV2Fixture.Evidence(options);
        var access = ProbeV2AccessEvidenceTests.Access(options);
        ProbeV2AccessEnvelope? used = audit switch { "complete" => access,"missing" => null,"wire" => access with { SchemaVersion = 2 },_ => access with { RunId = "other" } };
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),
            new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal),used);
        Assert.Equal(status,result.Status);
        Assert.All(result.Repeats,row => { Assert.Equal(status,row.Status); Assert.Equal(reasons,string.Join(',',row.Reasons)); });
        Assert.Equal(audit == "complete",result.Diagnostics.Contains("permission_audit_sha256:" + access.ProjectionSha256,StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0,"complete_valid","")]
    [InlineData(1,"invalid","v2_wire")]
    [InlineData(2,"invalid","v2_wire")]
    public void Given_InvalidHelperSlots_When_Projected_Then_AnyInvalidHelperInvalidatesTheRepeat(int helpers,string status,string reasons)
    {
        var options = ProbeV2Fixture.Options("E01"); var evidence = ProbeV2Fixture.Evidence(options); var slots = ProbeV2Fixture.Check(options,evidence);
        foreach (var key in new[] { ("C0","seed"),("C0","seedread") }.Take(helpers))
            slots[key] = slots[key].Select(row => row with { Status = "invalid",Reasons = ["v2_wire"] }).ToArray();
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(status,result.Status);
        Assert.All(result.Repeats,row => { Assert.Equal(status,row.Status); Assert.Equal(reasons,string.Join(',',row.Reasons)); });
    }

    [Theory]
    [InlineData("B08","action,read",null)]
    [InlineData("E01","action,seed,seedread,read","seed")]
    public void Given_CompleteMatrix_When_Projected_Then_HelperClientsAndOnlyTheSeedReferenceAreKept(string item,string stages,string? seedStage)
    {
        var candidate = ProbeV2Fixture.Candidate(item);
        Assert.All(candidate.Repeats,row =>
        {
            Assert.Equal(stages,string.Join(',',row.Clients.Select(x => x.Stage)));
            Assert.Equal(seedStage,row.SeedReference?.Stage);
        });
    }

    [Theory]
    [InlineData("B08","C0","read_row_missing","incomplete","v2_completion,v2_location")]
    [InlineData("B08","C0","source_exit","incomplete","v2_location")]
    [InlineData("B08","C0","source_missing","incomplete","v2_completion,v2_location")]
    [InlineData("B08","C0","cleanup_admission","incomplete","v2_completion")]
    [InlineData("B08","C0","cleanup_no_record","incomplete","v2_completion,v2_location")]
    [InlineData("B08","C0","wrong_admission","invalid","v2_location")]
    [InlineData("B08","C0","record_partition","invalid","v2_location")]
    [InlineData("B08","C0","record_offset","invalid","v2_location")]
    [InlineData("B08","C0","read_no_outcome","incomplete","v2_location")]
    [InlineData("B08","C0","helper_pod","invalid","v2_identity")]
    [InlineData("B08","C0","missing_slot","incomplete","v2_completion")]
    [InlineData("F06","C0","scope_invalid","incomplete","v2_controls")]
    [InlineData("E01","C0","target_partition","invalid","v2_seed,v2_location")]
    [InlineData("E01","C0","seedread_no_outcome","incomplete","v2_location,v2_seed")]
    [InlineData("F06","C6","action_no_record","incomplete","v2_location")]
    public void Given_SourceOrReaderDefect_When_Projected_Then_FirstRepeatHasExactStatusAndReasons(string item,string configuration,string defect,string status,string reasons)
    {
        var options = ProbeV2Fixture.Options(item,configuration); var evidence = ProbeV2Fixture.Evidence(options); var slots = ProbeV2Fixture.Check(options,evidence);
        var scope = ProbeV2Fixture.Scope(options); var action = (configuration,"action"); var read = (configuration,"read");
        switch (defect)
        {
            case "read_row_missing": Rows(evidence,read,rows => rows.Where(x => x.Repeat != 1)); break;
            case "source_exit": evidence[action] = evidence[action] with { Capture = evidence[action].Capture! with { HandlerExitCode = 1 } }; break;
            case "source_missing": evidence.Remove(action); break;
            case "cleanup_admission": Change(evidence,read,row => row with { Admission = "not_attempted_cleanup" }); break;
            case "cleanup_no_record": Change(evidence,read,row => row with { Admission = "not_attempted_cleanup",ReadObservation = row.ReadObservation with { Record = null } }); break;
            case "wrong_admission": Change(evidence,read,row => row with { Admission = "not_applicable_no_delivery" }); break;
            case "record_partition": Change(evidence,read,row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { Partition = row.ReadObservation.Record.Partition + 1 } } }); break;
            case "record_offset": Change(evidence,read,row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { Offset = row.ReadObservation.Record.Offset + 1 } } }); break;
            case "read_no_outcome":
                Change(evidence,action,row => row with { Result = "error" });
                Change(evidence,read,row => row with { Result = "no_outcome",ReadObservation = row.ReadObservation with { Record = null } }); break;
            case "helper_pod": evidence[read] = evidence[read] with { Capture = evidence[read].Capture! with { PodUid = evidence[action].Capture!.PodUid } }; break;
            case "missing_slot": slots.Remove(read); break;
            case "scope_invalid": scope = scope with { EffectiveScope = null,EffectiveScopeSha256 = null }; break;
            case "target_partition": Change(evidence,action,row => row with { Delivery = row.Delivery! with { Partition = 1 } }); break;
            case "seedread_no_outcome": Change(evidence,(configuration,"seedread"),row => row with { Result = "no_outcome",ReadObservation = row.ReadObservation with { Record = null } }); break;
            case "action_no_record": Change(evidence,action,row => row with { ReadObservation = row.ReadObservation with { Record = null } }); break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,scope,new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(status,result.Status); Assert.Equal(status,result.Repeats[0].Status); Assert.Equal(reasons,string.Join(',',result.Repeats[0].Reasons));
    }

    [Theory]
    [InlineData("C9","C6")][InlineData("C10","C7")][InlineData("C11","C8")]
    public void Given_NegativeReaderSourceActionFailed_When_Projected_Then_ControlsGapIsRecorded(string configuration,string source)
    {
        var options = ProbeV2Fixture.Options("F06",configuration); var evidence = ProbeV2Fixture.Evidence(options); var slots = ProbeV2Fixture.Check(options,evidence);
        Change(evidence,(source,"action"),row => row with { Result = "error" });
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.All(result.Repeats,row => { Assert.Equal("incomplete",row.Status); Assert.Equal("v2_controls",string.Join(',',row.Reasons)); });
    }

    [Theory]
    [InlineData("",5,true)]
    [InlineData("",2,true)]
    [InlineData("v2_identity",5,false)]
    [InlineData("v2_configuration",5,false)]
    [InlineData("v2_wire",5,false)]
    public void Given_NegativeSendDelivered_When_Projected_Then_SecurityFindingFollowsActionFidelity(string defect,int delivered,bool security)
    {
        var options = ProbeV2Fixture.Options("F06","C2"); var evidence = ProbeV2Fixture.Evidence(options); var slots = ProbeV2Fixture.Check(options,evidence);
        var action = ("C2","action");
        if (defect.Length != 0) slots[action] = slots[action].Select(row => row with { Reasons = [defect] }).ToArray();
        Change(evidence,action,row => row.Repeat > delivered ? row : row with { Result = "success",Error = null,Delivery = new(row.Requested!.Topic,0,100 * row.Repeat,"Persisted",null,10,1) });
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(security,result.SecurityFinding);
    }

    [Theory]
    [InlineData("B08","prior_rejected","incomplete","smoke_only,v2_controls",false)]
    [InlineData("B08","invalid_without_prior","invalid","smoke_only,v2_controls",false)]
    [InlineData("F06","one_prior_missing","incomplete","smoke_only,v2_controls",false)]
    [InlineData("F06","prior_security","incomplete","smoke_only,v2_controls",true)]
    public void Given_FinalConfiguration_When_PriorsChecked_Then_StatusDiagnosticsAndSecurityAreExact(string item,string defect,string status,string diagnostics,bool security)
    {
        string configuration = ProbeV2Matrix.Configurations(item).Last();
        var options = ProbeV2Fixture.Options(item,configuration); var evidence = ProbeV2Fixture.Evidence(options); var slots = ProbeV2Fixture.Check(options,evidence);
        var previous = ProbeV2Matrix.Configurations(item).SkipLast(1).ToDictionary(x => x,x => ProbeV2Fixture.Candidate(item,x),StringComparer.Ordinal);
        var action = (configuration,"action");
        switch (defect)
        {
            case "prior_rejected": previous["C0"] = previous["C0"] with { Status = "incomplete" }; break;
            case "invalid_without_prior": previous.Clear(); slots[action] = slots[action].Select(row => row with { Status = "invalid",Reasons = ["v2_wire"] }).ToArray(); break;
            case "one_prior_missing": previous.Remove("C3"); break;
            case "prior_security":
                previous["C0"] = previous["C0"] with { SecurityFinding = true };
                Change(evidence,action,row => row with { ReadObservation = row.ReadObservation with { Record = new(row.Requested!.Topic,row.Requested.Partition!.Value,row.Requested.Offset!.Value,row.Requested.KeyBase64,row.Requested.ValueBase64,10,1) } });
                break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,ProbeV2Fixture.Scope(options),previous);
        Assert.Equal(status,result.Status); Assert.Equal(diagnostics,string.Join(',',result.Diagnostics)); Assert.Equal(security,result.SecurityFinding);
    }

    private static void Change(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,(string Configuration,string Stage) key,Func<ProbeV2Repeat,ProbeV2Repeat> change) =>
        Rows(evidence,key,rows => rows.Select(change));
    private static void Rows(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,(string Configuration,string Stage) key,Func<IEnumerable<ProbeV2Repeat>,IEnumerable<ProbeV2Repeat>> change) =>
        evidence[key] = evidence[key] with { Capture = evidence[key].Capture! with { Repeats = change(evidence[key].Capture!.Repeats).ToArray() } };
}
