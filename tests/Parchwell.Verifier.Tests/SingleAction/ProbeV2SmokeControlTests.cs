using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2SmokeControlTests
{
    [Theory]
    [InlineData("C0",null,"null")][InlineData("C1","","empty")]
    [InlineData("C0","Ynl0ZXM=","bytes")][InlineData("C1","Ynl0ZXM=","bytes")]
    public void Given_ContraryE01TargetBytes_When_SmokeJudged_Then_ObservedTransportIsAcceptedWithoutScientificAssessment(string configuration,string? observed,string kind)
    {
        var options = ProbeV2Fixture.Options("E01",configuration); var evidence = ProbeV2Fixture.Evidence(options);
        Change(evidence,(configuration,"read"),row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = observed } } });
        var candidate = Candidate(options,evidence);
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic);
        Assert.True(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate));
        Assert.Equal(["smoke_only"],candidate.Diagnostics); Assert.Null(candidate.ItemAssessment);
        Assert.All(candidate.Repeats,row =>
        {
            Assert.Equal(kind,row.SemanticOutcome.ValueKind); Assert.False(row.SemanticOutcome.ValueMatch);
            Assert.Equal(observed,row.ReadObservation!.Record!.ValueBase64); Assert.Empty(row.Reasons);
        });
    }

    [Theory]
    [InlineData("C0")][InlineData("C1")]
    public void Given_MixedE01ObservedValueKinds_When_SmokeJudged_Then_ObservationFreedomStillRequiresDeterminism(string configuration)
    {
        var options = ProbeV2Fixture.Options("E01",configuration); var evidence = ProbeV2Fixture.Evidence(options);
        Change(evidence,(configuration,"read"),row => row.Repeat == 1 ? row with { ReadObservation = row.ReadObservation with
        { Record = row.ReadObservation.Record! with { ValueBase64 = configuration == "C0" ? null : "" } } } : row);
        var candidate = Candidate(options,evidence);
        Assert.Equal("complete_valid",candidate.Status); Assert.False(candidate.Deterministic);
        Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate));
        Assert.DoesNotContain("smoke_controls_failed",candidate.Diagnostics); Assert.Null(candidate.ItemAssessment);
    }

    [Fact]
    public void Given_ContraryPriorE01TargetBytes_When_FinalConfigurationJudged_Then_PriorObservationRemainsEligibleSmokeEvidence()
    {
        var options = ProbeV2Fixture.Options("E01"); var evidence = ProbeV2Fixture.Evidence(options);
        Change(evidence,("C0","read"),row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = null } } });
        var prior = Candidate(options,evidence); var finalOptions = ProbeV2Fixture.Options("E01","C1"); var finalEvidence = ProbeV2Fixture.Evidence(finalOptions);
        var result = SingleActionConfigurationJudgement.V2Candidate(finalOptions,finalEvidence,ProbeV2Fixture.Check(finalOptions,finalEvidence),ProbeV2Fixture.Scope(finalOptions),
            new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal) { ["C0"] = prior });
        Assert.True(SingleActionConfigurationJudgement.V2SmokeAccepted(prior)); Assert.True(SingleActionConfigurationJudgement.V2SmokeAccepted(result));
        Assert.Null(result.ItemAssessment); Assert.DoesNotContain("v2_controls",result.Diagnostics);
    }

    [Theory]
    [InlineData("C0","seed_error")][InlineData("C1","seed_error")]
    [InlineData("C0","seedread_error")][InlineData("C1","seedread_error")]
    [InlineData("C0","missing_record")][InlineData("C1","missing_record")]
    [InlineData("C0","null_value")][InlineData("C1","null_value")]
    [InlineData("C0","empty_value")][InlineData("C1","empty_value")]
    [InlineData("C0","wrong_value")][InlineData("C1","wrong_value")]
    public void Given_FailedE01SeedControl_When_SmokeJudged_Then_TargetByteFreedomCannotExcuseSeedFailure(string configuration,string mutation)
    {
        var options = ProbeV2Fixture.Options("E01",configuration); var evidence = ProbeV2Fixture.Evidence(options);
        string stage = mutation == "seed_error" ? "seed" : "seedread";
        Change(evidence,(configuration,stage),row => mutation switch
        {
            "seed_error" => row with { Result = "error",Error = new(-1,false,"local","synthetic",10,1),Delivery = null },
            "seedread_error" => row with { Result = "error",Error = new(-1,false,"local","synthetic",10,1),ReadObservation = new(0,0,null) },
            "missing_record" => row with { ReadObservation = new(0,0,null) },
            _ => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with
            { ValueBase64 = mutation == "null_value" ? null : mutation == "empty_value" ? "" : "d3Jvbmc=" } } }
        });
        var candidate = Candidate(options,evidence);
        Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.NotEqual("complete_valid",candidate.Status);
        Assert.Contains("smoke_controls_failed",candidate.Diagnostics); Assert.Null(candidate.ItemAssessment);
        Assert.All(candidate.Repeats,row => Assert.Contains("v2_seed",row.Reasons));
    }

    [Theory]
    [InlineData("C0","target_error")][InlineData("C1","target_error")]
    [InlineData("C0","missing_read")][InlineData("C1","missing_read")]
    [InlineData("C0","read_error")][InlineData("C1","read_error")]
    public void Given_FailedE01TargetControl_When_SmokeJudged_Then_DeliveryAndIndependentReadRemainRequired(string configuration,string mutation)
    {
        var options = ProbeV2Fixture.Options("E01",configuration); var evidence = ProbeV2Fixture.Evidence(options);
        Change(evidence,(configuration,mutation == "target_error" ? "action" : "read"),row => mutation switch
        {
            "target_error" => row with { Result = "error",Error = new(-1,false,"local","synthetic",10,1),Delivery = null },
            "read_error" => row with { Result = "error",Error = new(-1,false,"local","synthetic",10,1),ReadObservation = new(0,0,null) },
            _ => row with { ReadObservation = new(0,0,null) }
        });
        var candidate = Candidate(options,evidence);
        Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.Contains("smoke_controls_failed",candidate.Diagnostics);
        Assert.Null(candidate.ItemAssessment);
    }

    [Theory]
    [InlineData("B08","C0","read")][InlineData("B08","C1","read")]
    [InlineData("F06","C0","read")][InlineData("F06","C1","read")]
    [InlineData("F06","C3","read")][InlineData("F06","C5","read")]
    [InlineData("F06","C6","action")][InlineData("F06","C7","action")]
    [InlineData("F06","C8","action")][InlineData("F06","C9","action")]
    public void Given_WrongPositiveControlMarkerOutsideE01_When_SmokeJudged_Then_ExactMarkerComparisonRemainsRequired(string item,string configuration,string stage)
    {
        var options = ProbeV2Fixture.Options(item,configuration); var evidence = ProbeV2Fixture.Evidence(options);
        Change(evidence,(configuration,stage),row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = "d3Jvbmc=" } } });
        var candidate = Candidate(options,evidence);
        Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.Contains("smoke_controls_failed",candidate.Diagnostics);
        Assert.All(candidate.Repeats,row => Assert.False(row.SemanticOutcome.ValueMatch)); Assert.Null(candidate.ItemAssessment);
    }

    private static void Change(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,(string Configuration,string Stage) key,Func<ProbeV2Repeat,ProbeV2Repeat> change)
    {
        var value = evidence[key]; evidence[key] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(change).ToArray() } };
    }
    private static ProbeV2Candidate Candidate(ProbeV2Options options,Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence)
    {
        var prior = new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        if (ProbeV2Matrix.Configurations(options.ScenarioId).Last() == options.ConfigurationId)
            foreach (string configuration in ProbeV2Matrix.Configurations(options.ScenarioId).TakeWhile(x => x != options.ConfigurationId))
                prior.Add(configuration,ProbeV2Fixture.Candidate(options.ScenarioId,configuration));
        return SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),prior);
    }
}
