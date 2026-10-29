using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2EvidenceGapTests
{
    public static TheoryData<string,string,string> PartialIdentities
    {
        get
        {
            var data = new TheoryData<string,string,string>();
            string[] files = ["registration","capture","pod"];
            foreach (string missing in files)
                foreach (string present in files.Where(file => file != missing))
                    foreach (string mutation in new[] { "none","account","job","pod_name","pod_uid","schema" }) data.Add(missing,present,mutation);
            return data;
        }
    }

    [Theory,MemberData(nameof(PartialIdentities))]
    public void Given_MissingFileAndAvailableIdentity_When_Checked_Then_CompletionGapCannotHideMismatch(string missing,string present,string mutation)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        if (present == "registration")
        {
            var value = evidence.Registration!;
            value = mutation switch
            {
                "account" => value with { ServiceAccount = "verifier" },"job" => value with { JobName = "other-job" },
                "pod_name" => value with { PodName = value.JobName + "-different" },"pod_uid" => value with { PodUid = "other-uid" },
                "schema" => value with { SchemaVersion = 1 },_ => value
            };
            evidence = evidence with { Registration = value };
        }
        if (present == "capture")
        {
            var value = evidence.Capture!;
            value = mutation switch
            {
                "account" => value with { ServiceAccount = "verifier" },"job" => value with { JobName = "other-job" },
                "pod_name" => value with { PodName = value.JobName + "-different" },"pod_uid" => value with { PodUid = "other-uid" },
                "schema" => value with { SchemaVersion = 1 },_ => value
            };
            evidence = evidence with { Capture = value };
        }
        if (present == "pod")
        {
            var value = evidence.Pod!;
            value = mutation switch
            {
                "account" => value with { ServiceAccount = "verifier" },"job" => value with { JobName = "other-job" },
                "pod_name" => value with { PodName = value.JobName + "-different" },"pod_uid" => value with { PodUid = "other-uid" },
                "schema" => value with { SchemaVersion = 1 },_ => value
            };
            evidence = evidence with { Pod = value };
        }
        evidence = missing switch { "registration" => evidence with { Registration = null },"capture" => evidence with { Capture = null },_ => evidence with { Pod = null } };
        var problems = SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence);
        Assert.Contains("v2_completion",problems);
        Assert.Equal(mutation != "none",problems.Contains("v2_identity",StringComparer.Ordinal));
        Assert.DoesNotContain("v2_wire",problems);
    }

    [Theory]
    [InlineData("registration",0)][InlineData("registration",62)][InlineData("registration",63)][InlineData("registration",64)]
    [InlineData("capture",0)][InlineData("capture",62)][InlineData("capture",63)][InlineData("capture",64)]
    [InlineData("pod",0)][InlineData("pod",62)][InlineData("pod",63)][InlineData("pod",64)]
    public void Given_OnlyOneIdentityFile_When_PodNameAtLengthBoundary_Then_RuntimeBindingStillApplies(string present,int length)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        string prefix = evidence.Registration!.JobName + "-";
        string name = length == 0 ? "" : prefix + new string('a',length - prefix.Length);
        evidence = present switch
        {
            "registration" => evidence with { Registration = evidence.Registration with { PodName = name },Capture = null,Pod = null },
            "capture" => evidence with { Registration = null,Capture = evidence.Capture! with { PodName = name },Pod = null },
            _ => evidence with { Registration = null,Capture = null,Pod = evidence.Pod! with { PodName = name } }
        };
        var problems = SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence);
        Assert.Contains("v2_completion",problems);
        Assert.Equal(length is 0 or > 63,problems.Contains("v2_identity",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("registration")][InlineData("capture")][InlineData("pod")]
    public void Given_OnlyOneIdentityFile_When_ItContradictsRuntime_Then_MismatchStillSurvives(string present)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        evidence = present switch
        {
            "registration" => evidence with { Registration = evidence.Registration! with { SourceCommit = new string('2',40) },Capture = null,Pod = null },
            "capture" => evidence with { Registration = null,Capture = evidence.Capture! with { SourceCommit = new string('2',40) },Pod = null },
            _ => evidence with { Registration = null,Capture = null,Pod = evidence.Pod! with { ImageDigest = "sha256:" + new string('c',64) } }
        };
        Assert.Equal(["v2_completion","v2_identity"],SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence));
    }

    [Theory]
    [InlineData("registration")][InlineData("pod")]
    public void Given_MissingIdentityFileAndMalformedCapture_When_Checked_Then_WireFailureIsNotHidden(string missing)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        evidence = evidence with { Capture = evidence.Capture! with { Repeats = [] } };
        evidence = missing == "registration" ? evidence with { Registration = null } : evidence with { Pod = null };
        var problems = SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence);
        Assert.Contains("v2_wire",problems); Assert.Contains("v2_completion",problems);
    }

    [Theory]
    [InlineData(false,false,"incomplete")][InlineData(false,true,"incomplete")]
    [InlineData(true,false,"invalid")][InlineData(true,true,"complete_valid")]
    public void Given_AcknowledgedLocation_When_ReadAdmissionChecked_Then_MissingSourceTerminalIsDependencyGap(bool terminal,bool readRequired,string status)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options);
        var source = evidence[("C0","action")];
        if (!terminal) evidence[("C0","action")] = source with { Capture = source.Capture! with
        { Repeats = source.Capture.Repeats.Select(row => row with { Terminal = false,TerminalUnixMs = null,Result = null,Statistics = [] }).ToArray() } };
        if (!readRequired)
        {
            var reader = evidence[("C0","read")];
            evidence[("C0","read")] = reader with { Capture = reader.Capture! with { Repeats = reader.Capture.Repeats.Select(Skipped).ToArray() } };
        }
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(status,result.Status);
        Assert.All(result.Repeats,row => Assert.Equal(status,row.Status));
        if (!terminal) Assert.All(result.Repeats,row => Assert.Contains("v2_terminal",row.Reasons));
        if (!readRequired) Assert.All(result.Repeats,row => Assert.Contains("v2_location",row.Reasons));
    }

    [Theory]
    [InlineData(false,"invalid")][InlineData(true,"complete_valid")]
    public void Given_EarlierTerminalErrorAndLaterAcknowledgement_When_ReadAdmissionChecked_Then_ReadbackRemainsRequired(bool readRequired,string status)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options); var source = evidence[("C0","action")];
        evidence[("C0","action")] = source with { Capture = source.Capture! with
        { Repeats = source.Capture.Repeats.Select(row => row with { Result = "error",ElapsedMs = 5,Error = new(-150,true,"local","synthetic",5,0) }).ToArray() } };
        if (!readRequired)
        {
            var reader = evidence[("C0","read")];
            evidence[("C0","read")] = reader with { Capture = reader.Capture! with { Repeats = reader.Capture.Repeats.Select(Skipped).ToArray() } };
        }
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(status,result.Status); Assert.All(result.Repeats,row => Assert.Equal("error",row.OutcomeClass!.Result));
    }

    private static ProbeV2Repeat Skipped(ProbeV2Repeat row) => new(row.Repeat,"not_attempted_dependency",null,row.Action,false,0,null,
        new Dictionary<string,string>(StringComparer.Ordinal),[],null,null,null,new(0,0,null),null,null,[],[],["not_attempted_dependency"]);
}
