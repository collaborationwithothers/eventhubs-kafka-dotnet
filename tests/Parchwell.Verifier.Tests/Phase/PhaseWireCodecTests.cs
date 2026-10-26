using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class PhaseWireCodecTests
{
    [Theory]
    [InlineData("probe","probe")]
    [InlineData("C0","C0")]
    [InlineData("C1","C1")]
    [InlineData("C3","C3")]
    public void Given_ValidPhase_When_DerivingRepeatNames_Then_PreservesBlobCaseAndLowercasesJob(string phase,string path)
    {
        Assert.Equal($"synthetic-c5-{phase.ToLowerInvariant()}-r1-loadgen",PhaseIdentity.JobName("synthetic-c5",phase,1));
        Assert.Equal($"synthetic-c5/{path}/r1/",PhaseIdentity.RepeatPrefix("synthetic-c5",phase,1));
        Assert.Equal($"synthetic-c5/{path}/r5/result.json",PhasePaths.Result("synthetic-c5",phase,5));
    }

    [Theory]
    [InlineData("",false)] [InlineData("synthetic-c5",true)] [InlineData("x",true)]
    [InlineData("-bad",false)] [InlineData("bad-",false)] [InlineData("UPPER",false)] [InlineData("123456789012345678901234567890",false)]
    public void Given_RunId_When_Validating_Then_EnforcesDnsSafeBound(string runId,bool valid)=>Assert.Equal(valid,PhaseIdentity.IsRunId(runId));

    [Fact]
    public void Given_RunIdWithTerminalNewline_When_Validating_Then_RejectsTheWholeInput()=>Assert.False(PhaseIdentity.IsRunId("x\n"));

    [Fact]
    public void Given_IdentityBoundaries_When_DerivingNames_Then_AcceptsMaximumAndRejectsRepeatBounds()
    {
        string maximum = "a" + new string('b', 27) + "z";
        Assert.True(PhaseIdentity.IsRunId(maximum));
        Assert.Equal(29, maximum.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseIdentity.JobName("synthetic-c5", "C0", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseIdentity.RepeatPrefix("synthetic-c5", "C0", 6));
    }

    [Theory]
    [InlineData(127, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Given_PathInstanceSegmentBoundary_When_BuildingRegistrationPath_Then_EnforcesMaximum(int length, bool accepted)
    {
        string instance = new('a', length);
        if (accepted) Assert.EndsWith($"producer/{instance}/registration.json", PhasePaths.Registration("synthetic-c5", "C0", 1, instance));
        else Assert.Throws<ArgumentException>(() => PhasePaths.Registration("synthetic-c5", "C0", 1, instance));
    }

    [Theory]
    [InlineData(252, true)]
    [InlineData(253, true)]
    [InlineData(254, false)]
    public void GivenReceiptPodNameLengthBoundary_When_Read_Then_EnforcesMaximum(int length, bool accepted)
    {
        byte[] valid = PhaseWireCodec.WriteReceipt(Receipt(new("verifier", PhasePaths.Candidate("synthetic-c5", "C0"), 1, new string('a', 64))));
        string json = Encoding.UTF8.GetString(valid).Replace("\"pod_name\":\"judge-pod\"", $"\"pod_name\":\"{new string('p', length)}\"", StringComparison.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (accepted) Assert.Equal(length, PhaseWireCodec.ReadReceipt(bytes).PodName.Length);
        else Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(bytes));
    }

    [Theory]
    [InlineData(127, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void GivenRegistrationIdentitySegmentBoundary_When_Read_Then_EnforcesMaximum(int length, bool accepted)
    {
        string instance = new('i', length);
        string json = $"{{\"schema_version\":1,\"identity\":{{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"synthetic-c5\",\"instance_id\":\"{instance}\",\"expected_instance_id\":\"{instance}\",\"client_id\":\"producer-c5\"}},\"phase\":\"C0\",\"repeat_number\":1,\"job_name\":\"synthetic-c5-c0-r1-loadgen\",\"pod_name\":\"pod-c5\",\"pod_uid\":\"uid-c5\",\"source_commit\":\"{new string('a', 40)}\"}}";
        if (accepted) Assert.Equal(instance, PhaseWireCodec.ReadRegistration(Encoding.UTF8.GetBytes(json)).Identity.InstanceId);
        else Assert.Throws<FormatException>(() => PhaseWireCodec.ReadRegistration(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("pod_name", 252, true)]
    [InlineData("pod_name", 253, true)]
    [InlineData("pod_name", 254, false)]
    [InlineData("pod_uid", 252, true)]
    [InlineData("pod_uid", 253, true)]
    [InlineData("pod_uid", 254, false)]
    [InlineData("client_id", 252, true)]
    [InlineData("client_id", 253, true)]
    [InlineData("client_id", 254, false)]
    public void GivenRegistrationTextBoundary_When_Read_Then_EnforcesMaximum(string field, int length, bool accepted)
    {
        var identity = new RunIdentity("A01", "C0", "synthetic-c5", "producer-c5", "producer-c5", "producer-c5");
        var registration = new ProducerRegistration(1, identity, "C0", 1, "synthetic-c5-c0-r1-loadgen", "pod-c5", "uid-c5", new string('a', 40));
        JsonObject json = JsonNode.Parse(PhaseWireCodec.WriteRegistration(registration))!.AsObject();
        if (field == "client_id") json["identity"]![field] = new string('x', length);
        else json[field] = new string('x', length);
        byte[] bytes = Encoding.UTF8.GetBytes(json.ToJsonString());
        if (accepted) Assert.Equal(length, field == "client_id" ? PhaseWireCodec.ReadRegistration(bytes).Identity.ClientId.Length : field == "pod_name" ? PhaseWireCodec.ReadRegistration(bytes).PodName.Length : PhaseWireCodec.ReadRegistration(bytes).PodUid.Length);
        else Assert.Throws<FormatException>(() => PhaseWireCodec.ReadRegistration(bytes));
    }

    [Fact]
    public void Given_ProducerRegistration_When_RoundTripped_Then_ClosedIdentityIsRetained()
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        var value=new ProducerRegistration(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40));
        byte[] bytes=PhaseWireCodec.WriteRegistration(value);
        Assert.Equal(value,PhaseWireCodec.ReadRegistration(bytes));
        byte[] malformed=[..bytes.AsSpan(0,bytes.Length-1).ToArray(),..Encoding.UTF8.GetBytes(",\"extra\":true}")];
        Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.ReadRegistration(malformed));
    }

    [Theory]
    [InlineData("B04","C0","C0",true)]
    [InlineData("B04","C1","C1",true)]
    [InlineData("B04","probe","C0",false)]
    [InlineData("B04","C3","C3",false)]
    [InlineData("A01","C1","C1",false)]
    [InlineData("B04","C1","C0",false)]
    public void Given_ScenarioPhaseConfiguration_When_ReceiptWritten_Then_EnforcesExecutablePairs(string scenario,string phase,string configuration,bool accepted)
    {
        var receipt=new WorkerReceipt(1,"judge",scenario,configuration,"synthetic-c5",phase,null,
            PhaseIdentity.JudgeJobName("synthetic-c5",phase),"judge-pod",null,new string('a',40),1,true,
            "2026-10-24T00:00:00.0000000+00:00","2026-10-24T00:00:01.0000000+00:00",
            [new("verifier",PhasePaths.Candidate("synthetic-c5",phase),1,new string('a',64))],null,null);
        var identity=new RunIdentity(scenario,configuration,"synthetic-c5","producer-c5","producer-c5","producer-c5");
        var registration=new ProducerRegistration(1,identity,phase,1,PhaseIdentity.JobName("synthetic-c5",phase,1),"pod-c5","uid-c5",new string('a',40));
        var completion=new ProducerCompletionReceipt(1,identity,phase,1,registration.JobName,"pod-c5","uid-c5",new string('a',40),"loadgen",0,true);
        var verifyReceipt=new WorkerReceipt(1,"verify",scenario,configuration,"synthetic-c5",phase,1,
            PhaseIdentity.VerifyJobName("synthetic-c5",phase,1),"verifier-pod",identity,new string('a',40),0,true,
            "2026-10-24T00:00:00.0000000+00:00","2026-10-24T00:00:01.0000000+00:00",
            [new("verifier",PhasePaths.Result("synthetic-c5",phase,1),1,new string('a',64))],null,new("pass","pass","pass"));
        if(accepted)
        {
            Assert.Equal(phase,PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(receipt)).Phase);
            Assert.Equal(registration,PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(registration)));
            Assert.Equal(completion,PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(completion)));
            WorkerReceipt actualVerify=PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(verifyReceipt));
            Assert.Equal(verifyReceipt with { Artifacts=actualVerify.Artifacts },actualVerify);
            Assert.Equal(verifyReceipt.Artifacts,actualVerify.Artifacts);
        }
        else
        {
            Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.WriteReceipt(receipt));
            Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.WriteRegistration( registration));
            Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.WriteProducerCompletion(completion));
            Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.WriteReceipt(verifyReceipt));
        }
    }

    [Fact]
    public void Given_ProducerCompletionWithoutSuccessfulCleanup_When_Read_Then_RejectsSuccessClaim()
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        var value=new ProducerCompletionReceipt(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40),"loadgen",1,false);
        Assert.Throws<FormatException>(()=>PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(value)));
    }

    [Fact]
    public void Given_ProducerCompletionWithUnsupportedPhase_When_Read_Then_RejectsAtWireBoundary()
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        var value=new ProducerCompletionReceipt(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40),"loadgen",0,true);
        JsonObject json=JsonNode.Parse(PhaseWireCodec.WriteProducerCompletion(value))!.AsObject();
        json["phase"]="foreign";

        Assert.Throws<PhaseWireIdentityException>(()=>PhaseWireCodec.ReadProducerCompletion(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("phase","foreign")]
    [InlineData("repeat_number","0")]
    [InlineData("repeat_number","6")]
    public void GivenRegistrationWithUnsupportedCoordinate_When_Read_Then_ReportsWireIdentityFailure(string field,string value)
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        byte[] valid=PhaseWireCodec.WriteRegistration(new(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40)));
        JsonObject json=JsonNode.Parse(valid)!.AsObject();
        json[field]=field=="repeat_number"?JsonValue.Create(int.Parse(value,System.Globalization.CultureInfo.InvariantCulture)):JsonValue.Create(value);
        Assert.Throws<PhaseWireIdentityException>(()=>PhaseWireCodec.ReadRegistration(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("phase","foreign")]
    [InlineData("repeat_number","0")]
    [InlineData("repeat_number","6")]
    public void GivenProducerCompletionWithUnsupportedCoordinate_When_Read_Then_ReportsWireIdentityFailure(string field,string value)
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        byte[] valid=PhaseWireCodec.WriteProducerCompletion(new(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40),"loadgen",0,true));
        JsonObject json=JsonNode.Parse(valid)!.AsObject();
        json[field]=field=="repeat_number"?JsonValue.Create(int.Parse(value,System.Globalization.CultureInfo.InvariantCulture)):JsonValue.Create(value);
        Assert.Throws<PhaseWireIdentityException>(()=>PhaseWireCodec.ReadProducerCompletion(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData(65_535,true)]
    [InlineData(65_536,true)]
    [InlineData(65_537,false)]
    public void Given_ReceiptAtByteBoundary_When_Read_Then_AcceptsEqualAndRejectsExcess(int length,bool accepted)
    {
        string commit=new('a',40);
        var receipt=new WorkerReceipt(1,"judge","A01","C0","synthetic-c5","C0",null,
            PhaseIdentity.JudgeJobName("synthetic-c5","C0"),"judge-pod",null,commit,1,true,
            "2026-10-24T00:00:00.0000000+00:00","2026-10-24T00:00:01.0000000+00:00",
            [new("verifier",PhasePaths.Candidate("synthetic-c5","C0"),1,new string('a',64))],null,null);
        byte[] compact=PhaseWireCodec.WriteReceipt(receipt);
        byte[] padded=[..compact,..Encoding.UTF8.GetBytes(new string(' ',length-compact.Length))];
        if(accepted)Assert.Equal(receipt.Mode,PhaseWireCodec.ReadReceipt(padded).Mode);
        else Assert.Throws<InputLimitExceededException>(()=>PhaseWireCodec.ReadReceipt(padded));
    }

    [Theory]
    [InlineData(0,true)]
    [InlineData(1,true)]
    [InlineData(long.MaxValue,true)]
    [InlineData(-1,false)]
    public void Given_ArtifactReferenceLengthBoundary_When_Read_Then_EnforcesNonnegativeInt64(long length,bool accepted)
    {
        byte[] bytes=PhaseWireCodec.WriteReceipt(Receipt(new("verifier",PhasePaths.Candidate("synthetic-c5","C0"),length,new string('a',64))));
        if(accepted) Assert.Equal(length,PhaseWireCodec.ReadReceipt(bytes).Artifacts[0].Length);
        else Assert.Throws<FormatException>(()=>PhaseWireCodec.ReadReceipt(bytes));
    }

    [Theory]
    [InlineData(63,false)]
    [InlineData(64,true)]
    [InlineData(65,false)]
    public void Given_ArtifactReferenceDigestLengthBoundary_When_Read_Then_RequiresSha256Width(int length,bool accepted)
    {
        byte[] bytes=PhaseWireCodec.WriteReceipt(Receipt(new("verifier",PhasePaths.Candidate("synthetic-c5","C0"),1,new string('a',length))));
        if(accepted) Assert.Equal(64,PhaseWireCodec.ReadReceipt(bytes).Artifacts[0].Sha256.Length);
        else Assert.Throws<FormatException>(()=>PhaseWireCodec.ReadReceipt(bytes));
    }

    [Fact]
    public void Given_ArtifactReferenceLengthBeyondInt64_When_Read_Then_Rejects()
    {
        byte[] valid=PhaseWireCodec.WriteReceipt(Receipt(new("verifier",PhasePaths.Candidate("synthetic-c5","C0"),1,new string('a',64))));
        string json=Encoding.UTF8.GetString(valid).Replace("\"length\":1", "\"length\":9223372036854775808", StringComparison.Ordinal);
        Assert.Throws<FormatException>(()=>PhaseWireCodec.ReadReceipt(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("phase", "foreign")]
    [InlineData("repeat", "0")]
    [InlineData("repeat", "6")]
    public void GivenUnsupportedReceiptCoordinate_When_Read_Then_ReportsMalformedWireData(string field, string value)
    {
        var identity = new RunIdentity("A01", "C0", "synthetic-c5", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen");
        var receipt = new WorkerReceipt(1, "start-line", "A01", "C0", "synthetic-c5", "C0", 1,
            PhaseIdentity.StartLineJobName("synthetic-c5", "C0", 1), "start-pod", identity, new string('a', 40), 0, true,
            "2026-10-24T00:00:00.0000000+00:00", "2026-10-24T00:00:01.0000000+00:00",
            [new("verifier", PhasePaths.StartLine("synthetic-c5", "C0", 1), 1, new string('a', 64)),
             new("verifier", PhasePaths.BrokerInventory("synthetic-c5", "C0", 1), 1, new string('b', 64))], null, null);
        byte[] valid = PhaseWireCodec.WriteReceipt(receipt);
        string json = Encoding.UTF8.GetString(valid);
        json = field == "phase"
            ? json.Replace("\"phase\":\"C0\"", $"\"phase\":\"{value}\"", StringComparison.Ordinal)
            : json.Replace("\"repeat\":1", $"\"repeat\":{value}", StringComparison.Ordinal);

        Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadReceipt(Encoding.UTF8.GetBytes(json)));
    }

    private static WorkerReceipt Receipt(ArtifactReference reference)=>new(1,"judge","A01","C0","synthetic-c5","C0",null,
        PhaseIdentity.JudgeJobName("synthetic-c5","C0"),"judge-pod",null,new string('a',40),1,true,
        "2026-10-24T00:00:00.0000000+00:00","2026-10-24T00:00:01.0000000+00:00",[reference],null,null);
}
#pragma warning restore CA1707
