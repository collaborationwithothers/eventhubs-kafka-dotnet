using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionWireAndBoundaryTests
{
    [Theory]
    [InlineData("unknown")][InlineData("missing")][InlineData("duplicate")][InlineData("trailing")][InlineData("utf8")][InlineData("type")][InlineData("null")][InlineData("depth")]
    public void Given_StrictWireViolation_When_Read_Then_Rejected(string mutation)
    {
        string json=Encoding.UTF8.GetString(SingleActionWire.Write(SingleActionFixture.Capture(),134217728));
        var node=JsonNode.Parse(json)!.AsObject();
        switch(mutation)
        {
            case "unknown":node["extra"]=1;json=node.ToJsonString();break;
            case "missing":node.Remove("repeats");json=node.ToJsonString();break;
            case "duplicate":json=json.Replace("\"schema_version\":1","\"schema_version\":1,\"schema_version\":1",StringComparison.Ordinal);break;
            case "trailing":json+="{}";break;
            case "type":node["schema_version"]="1";json=node.ToJsonString();break;
            case "null":node["repeats"]=null;json=node.ToJsonString();break;
            case "depth":node["extra"]=JsonNode.Parse(new string('[',33)+"0"+new string(']',33));json=node.ToJsonString();break;
        }
        byte[] bytes=mutation=="utf8"?[0xff]:Encoding.UTF8.GetBytes(json);
        Assert.ThrowsAny<Exception>(()=>SingleActionWire.Read<SingleActionCapture>(bytes,134217728));
    }
    [Theory]
    [InlineData("repeats")][InlineData("settings")][InlineData("omitted_keys")][InlineData("client_errors")][InlineData("statistics")][InlineData("diagnostics")]
    public void Given_NullCollectionElement_When_CaptureRead_Then_FormatRejected(string collection)
    {
        var node=JsonNode.Parse(SingleActionWire.Write(SingleActionFixture.Capture(),134217728))!.AsObject();
        if(collection=="repeats")node["repeats"]![0]=null;
        else if(collection=="settings")node["repeats"]![0]!["settings"]!["a"]=null;
        else node["repeats"]![0]![collection]=new JsonArray((JsonNode?)null);
        Assert.Throws<FormatException>(()=>SingleActionWire.Read<SingleActionCapture>(Encoding.UTF8.GetBytes(node.ToJsonString()),134217728));
    }
    [Theory]
    [InlineData("repeats")][InlineData("image_digests")][InlineData("diagnostics")][InlineData("reasons")][InlineData("hypotheses")]
    public void Given_NullCollectionElement_When_CandidateRead_Then_FormatRejected(string collection)
    {
        var candidate=SingleActionFixture.Candidate() with{ItemAssessment=new(null,new(StringComparer.Ordinal){["H0"]="undecided"},null)};
        var node=JsonNode.Parse(SingleActionCandidateCodec.Write(candidate))!.AsObject();
        if(collection=="repeats")node["repeats"]![0]=null;
        else if(collection=="image_digests")node["image_digests"]!["loadgen"]=null;
        else if(collection=="hypotheses")node["item_assessment"]!["hypotheses"]!["H0"]=null;
        else if(collection=="reasons")node["repeats"]![0]!["reasons"]=new JsonArray((JsonNode?)null);
        else node[collection]=new JsonArray((JsonNode?)null);
        Assert.Throws<FormatException>(()=>SingleActionCandidateCodec.Read(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Theory]
    [InlineData(-1,true)][InlineData(0,true)][InlineData(1,false)]
    public void Given_ArtifactByteLimit_When_Read_Then_ExactBoundary(int delta,bool valid)
    {
        byte[] bytes=SingleActionWire.Write(SingleActionFixture.Registration(),65536);long maximum=bytes.Length-delta;
        if(valid)Assert.NotNull(SingleActionWire.Read<SingleActionRegistration>(bytes,maximum));else Assert.ThrowsAny<Exception>(()=>SingleActionWire.Read<SingleActionRegistration>(bytes,maximum));
    }
    [Theory]
    [InlineData(63,true)][InlineData(64,true)][InlineData(65,false)]
    public void Given_CallbackCount_When_CaptureChecked_Then_ExactBoundary(int count,bool accepted)
    {
        var capture=SingleActionFixture.Capture();var repeat=capture.Repeats[0];
        var errors=Enumerable.Range(0,count).Select(_=>new SingleActionClientError(1,false,"local","synthetic",0)).ToArray();
        capture.Repeats[0]=repeat with{ClientErrors=errors};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
        capture.Repeats[0]=repeat with{Statistics=Enumerable.Range(1,count).Select(i=>SingleActionFixture.Statistic(repeat.ClientId,1,1,i)).ToArray()};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(127,true)][InlineData(128,true)][InlineData(129,false)]
    public void Given_SettingsCount_When_CaptureChecked_Then_ExactBoundary(int count,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Settings=Enumerable.Range(0,count).ToDictionary(i=>"k"+i,_=>"v",StringComparer.Ordinal)};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(16383,true)][InlineData(16384,true)][InlineData(16385,false)]
    public void Given_SettingsUtf8Bytes_When_CaptureChecked_Then_ExactBoundary(int bytes,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Settings=new(StringComparer.Ordinal){["k"]=new string('s',bytes-1)}};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(4095,true)][InlineData(4096,true)][InlineData(4097,false)]
    public void Given_ErrorBytes_When_CaptureChecked_Then_ExactBoundary(int bytes,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{ClientErrors=[new(1,false,"local",new string('s',bytes),0)]};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(2047,true)][InlineData(2048,true)][InlineData(2049,false)]
    public void Given_DiagnosticBytes_When_CaptureChecked_Then_ExactBoundary(int bytes,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Diagnostics=[new string('s',bytes)]};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(99,true)][InlineData(100,true)][InlineData(101,false)]
    public void Given_ConfigurationDiagnosticCount_When_CaptureChecked_Then_ExactBoundary(int count,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Diagnostics=Enumerable.Repeat("synthetic",count).ToArray()};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Fact]
    public void Given_Candidate_When_Written_Then_ExactClosedSchemaAndNoLedgerCounts()
    {
        byte[] bytes=SingleActionCandidateCodec.Write(SingleActionFixture.Candidate());
        var root=JsonNode.Parse(bytes)!.AsObject();
        string[] expected=["schema_version","mode","synthetic","run_id","scenario_id","configuration_id","source_commit","managed_version","native_version","image_digests","status","repeats","deterministic","outcome_class","adr012_counts","diagnostics","security_finding","item_assessment"];
        Assert.Equal(expected.Order(StringComparer.Ordinal),root.Select(p=>p.Key).Order(StringComparer.Ordinal));Assert.Equal("not_applicable",root["adr012_counts"]!.GetValue<string>());
        Assert.Equal("complete_valid",SingleActionCandidateCodec.Read(bytes).Status);
        foreach(string prohibited in new[]{"ok","loss","phantom","reported_failure","duplicate","reorder","counts"})Assert.False(root.ContainsKey(prohibited));
        string[] projection=["repeat","status","reasons","outcome_class","broker_count","statistics_reference"];Assert.Equal(projection.Order(StringComparer.Ordinal),root["repeats"]![0]!.AsObject().Select(p=>p.Key).Order(StringComparer.Ordinal));
    }
}
