using System.Text;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionRepeatJudgementTests
{
    [Fact]
    public void Given_LiteralFiveSlots_When_Judged_Then_AllCompleteWithObservedZero()
    {
        var fixture=SingleActionFixture.Capture();
        foreach(var repeat in fixture.Repeats){var result=SingleActionFixture.Judge(repeat);Assert.Equal("complete_valid",result.Status);Assert.Equal(0,result.BrokerCount);Assert.Empty(result.Reasons);}
    }
    [Theory]
    [InlineData(-1,"incomplete")][InlineData(0,"complete_valid")][InlineData(1,"complete_valid")][InlineData(329999,"complete_valid")][InlineData(330000,"complete_valid")][InlineData(330001,"incomplete")]
    public void Given_MonotonicWindow_When_Judged_Then_InclusiveBoundary(long elapsed,string status)=>Assert.Equal(status,SingleActionFixture.Judge(SingleActionFixture.Repeat() with{ElapsedMs=elapsed}).Status);

    [Theory]
    [InlineData(999,1,"complete_valid")][InlineData(1000,1,"complete_valid")][InlineData(1001,1,"incomplete")][InlineData(1001,2,"complete_valid")]
    public void Given_NativeEmission_When_Judged_Then_UnixMillisecondBoundary(long terminal,long native,string expected)
    {
        var repeat=SingleActionFixture.Repeat();repeat=repeat with{TerminalUnixMs=terminal,Statistics=[SingleActionFixture.Statistic(repeat.ClientId,native,1,1,observed:999999)]};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal(expected,result.Status);if(expected=="incomplete"){Assert.Contains("p6_broker_count",result.Reasons);Assert.Null(result.BrokerCount);}
    }
    [Theory]
    [InlineData("terminal")][InlineData("time")][InlineData("capture_limit")]
    public void Given_MissingTerminal_When_Judged_Then_P4IncompleteWithoutP7(string mutation)
    {
        var repeat=SingleActionFixture.Repeat();repeat=mutation switch{"terminal"=>repeat with{Terminal=false,Result=SingleActionFixture.Json("null")},"time"=>repeat with{TerminalUnixMs=null},_=>repeat with{Diagnostics=["capture_limit"]}};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal("incomplete",result.Status);Assert.Contains("p4_no_terminal_outcome",result.Reasons);Assert.DoesNotContain("p7_outcome_record",result.Reasons);
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("value")][InlineData("omission")][InlineData("action")]
    public void Given_ConfigurationMismatch_When_Judged_Then_P5Invalid(string mutation)
    {
        var repeat=SingleActionFixture.Repeat();var settings=new Dictionary<string,string>(repeat.Settings,StringComparer.Ordinal);
        if(mutation=="missing")settings.Remove("a");if(mutation=="extra")settings["extra"]="x";if(mutation=="value")settings["a"]="x";
        repeat=repeat with{Settings=settings,OmittedKeys=mutation=="omission"?["omitted"]:[],Action=mutation=="action"?"subscribe":repeat.Action};
        Assert.Contains("p5_configuration_fidelity",SingleActionFixture.Judge(repeat).Reasons);Assert.Equal("invalid",SingleActionFixture.Judge(repeat).Status);
    }
    [Fact]
    public void Given_RestoredOmittedKey_When_Judged_Then_P5Invalid()
    {
        var repeat=SingleActionFixture.Repeat();repeat.Settings["session.timeout.ms"]="1";
        var result=SingleActionRepeatJudgement.Judge(repeat,SingleActionFixture.Declaration,repeat.Settings,["session.timeout.ms"],SingleActionFixture.Run);
        Assert.Equal("invalid",result.Status);Assert.Contains("p5_configuration_fidelity",result.Reasons);
    }
    [Theory]
    [InlineData("null","null")][InlineData("true","null")][InlineData("\"success\"","{}")][InlineData("\"error\"","null")]
    [InlineData("\"error\"","{\"code\":\"29\",\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"x\"}")]
    [InlineData("\"error\"","{\"code\":29,\"is_fatal\":1,\"origin\":\"broker\",\"reason\":\"x\"}")]
    [InlineData("\"error\"","{\"code\":29,\"is_fatal\":false,\"origin\":\"other\",\"reason\":\"x\"}")]
    public void Given_InvalidOutcomeTypes_When_Judged_Then_P7Invalid(string result,string error)
    {
        var judged=SingleActionFixture.Judge(SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json(result),Error=SingleActionFixture.Json(error)});
        Assert.Equal("invalid",judged.Status);Assert.Contains("p7_outcome_record",judged.Reasons);
    }
    [Theory]
    [InlineData("construction")][InlineData("local")][InlineData("broker")]
    public void Given_MeasuredError_When_Judged_Then_TupleRetained(string origin)
    {
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json($$"""{"code":-1,"is_fatal":true,"origin":"{{origin}}","reason":"synthetic"}""")};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal("complete_valid",result.Status);Assert.Equal(new SingleActionOutcome("error",-1,true,origin),result.OutcomeClass);
    }
    [Fact]
    public void Given_NativeSourcesAndStates_When_Judged_Then_OnlyThreeUPSourcesCount()
    {
        const string brokers="""{"a":{"source":"learned","state":"UP"},"b":{"source":"configured","state":"UP"},"c":{"source":"logical","state":"UP"},"d":{"source":"internal","state":"UP"},"e":{"source":"learned","state":"DOWN"},"f":{"source":"unknown","state":"UP"}}""";
        var repeat=SingleActionFixture.Repeat();var result=SingleActionFixture.Judge(repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,1,1,1,brokers)]});Assert.Equal(3,result.BrokerCount);
    }
    [Fact]
    public void Given_MultipleQualifyingReports_When_Judged_Then_TimeThenTsThenSequenceChoosesReference()
    {
        var repeat=SingleActionFixture.Repeat();var result=SingleActionFixture.Judge(repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,2,0,1),SingleActionFixture.Statistic(repeat.ClientId,1,2,2),SingleActionFixture.Statistic(repeat.ClientId,1,1,4),SingleActionFixture.Statistic(repeat.ClientId,1,1,3)]});
        Assert.Equal(3,result.StatisticsReference!.Sequence);Assert.Equal(1,result.StatisticsReference.NativeTime);Assert.Equal(1,result.StatisticsReference.NativeTs);Assert.Equal(64,result.StatisticsReference.RawSha256.Length);
    }
    [Theory]
    [InlineData("absent")][InlineData("malformed")][InlineData("base64")][InlineData("wrong_client")][InlineData("negative")][InlineData("overflow")][InlineData("oversized")][InlineData("duplicate")]
    public void Given_UnusableStatistics_When_Judged_Then_P6NoFabricatedCount(string mutation)
    {
        var repeat=SingleActionFixture.Repeat();var row=repeat.Statistics[0];
        row=mutation switch{"malformed"=>row with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))},"base64"=>row with{RawBase64="?"},"wrong_client"=>SingleActionFixture.Statistic("other",1,1,1),"negative"=>SingleActionFixture.Statistic(repeat.ClientId,-1,1,1),"overflow"=>SingleActionFixture.Statistic(repeat.ClientId,long.MaxValue,1,1),"oversized"=>row with{RawBase64=new string('a',349529)},_=>row};
        var result=SingleActionFixture.Judge(repeat with{Statistics=mutation=="absent"?[]:mutation=="duplicate"?[row,row]:[row]});Assert.Equal("incomplete",result.Status);Assert.Contains("p6_broker_count",result.Reasons);Assert.Null(result.BrokerCount);Assert.Null(result.StatisticsReference);
    }
    [Theory]
    [InlineData("subscribe",119999,"complete_valid")][InlineData("subscribe",120000,"complete_valid")][InlineData("subscribe",120001,"incomplete")]
    [InlineData("init_transactions",149999,"complete_valid")][InlineData("init_transactions",150000,"complete_valid")][InlineData("init_transactions",150001,"incomplete")]
    public void Given_ActionSpecificWindow_When_Judged_Then_LiteralBoundary(string action,long elapsed,string expected)
    {
        var declaration=SingleActionFixture.Declaration with{Action=action,WindowMs=action=="subscribe"?120000:150000};
        var repeat=SingleActionFixture.Repeat() with{Action=action,ElapsedMs=elapsed};
        if(action=="subscribe")
        {
            string native=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(repeat.Statistics[0].RawBase64)).Replace("producer","consumer",StringComparison.Ordinal);
            repeat=repeat with{Statistics=[repeat.Statistics[0] with{RawBase64=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(native))}]};
        }
        Assert.Equal(expected,SingleActionFixture.Judge(repeat,declaration).Status);
    }
    [Theory]
    [InlineData("exact",true)][InlineData("eof",false)][InlineData("topic",false)][InlineData("partition",false)][InlineData("key",false)][InlineData("value",false)][InlineData("missing",false)][InlineData("oversized",false)]
    public void Given_AssignedConsumerSuccess_When_MarkerChecked_Then_OnlyExactSeedBytesAccepted(string mutation,bool accepted)
    {
        var declaration=SingleActionFixture.Declaration with{Action="assign_consume",Topic="document-state",WindowMs=120000};
        var repeat=SingleActionFixture.Repeat() with{Action="assign_consume"};
        var matched=new System.Text.Json.Nodes.JsonObject{["topic"]="document-state",["partition"]=0,["key_base64"]=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("f04-seed:run1")),["value_base64"]=Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(new string('s',1000)))};
        if(mutation=="topic")matched["topic"]="document-events";
        if(mutation=="partition")matched["partition"]=1;
        if(mutation=="key")matched["key_base64"]=Convert.ToBase64String("other"u8.ToArray());
        if(mutation=="value")matched["value_base64"]=Convert.ToBase64String(new byte[1000]);
        if(mutation=="oversized")matched["value_base64"]=new string('a',1466669);
        repeat=repeat with{Matched=mutation is "missing" or "eof"?SingleActionFixture.Json("null"):SingleActionFixture.Json(matched.ToJsonString())};
        Assert.Equal(accepted,SingleActionRepeatJudgement.TryOutcome(repeat,declaration,"run1",out _));
    }
    [Theory]
    [InlineData(63,true)][InlineData(64,true)][InlineData(65,false)]
    public void Given_NativeBrokerCount_When_Judged_Then_ExactLimitBoundary(int count,bool accepted)
    {
        var brokers=new System.Text.Json.Nodes.JsonObject();
        for(int i=0;i<count;i++)brokers["b"+i]=new System.Text.Json.Nodes.JsonObject{["source"]="learned",["state"]="UP"};
        var repeat=SingleActionFixture.Repeat();var result=SingleActionFixture.Judge(repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,1,1,1,brokers.ToJsonString())]});
        Assert.Equal(accepted?"complete_valid":"incomplete",result.Status);Assert.Equal(accepted?count:null,result.BrokerCount);
    }
    [Theory]
    [InlineData(262143,true)][InlineData(262144,true)][InlineData(262145,false)]
    public void Given_NativeReportBytes_When_Judged_Then_ExactLimitBoundary(int bytes,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();
        string raw=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(repeat.Statistics[0].RawBase64));
        string padded=raw+new string(' ',bytes-System.Text.Encoding.UTF8.GetByteCount(raw));
        var result=SingleActionFixture.Judge(repeat with{Statistics=[repeat.Statistics[0] with{RawBase64=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(padded))}]});
        Assert.Equal(accepted?"complete_valid":"incomplete",result.Status);if(!accepted)Assert.Null(result.BrokerCount);
    }

    public static TheoryData<string,string> MalformedMatchedFields
    {
        get
        {
            var cases=new TheoryData<string,string>();
            foreach(string propertyName in new[]{"topic","key_base64","value_base64"})
                foreach(string raw in new[]{"null","0","true","[]","{}"})cases.Add(propertyName,raw);
            foreach(string raw in new[]{"null","\"0\"","true","[]","{}","0.5","-2147483649","2147483648"})cases.Add("partition",raw);
            cases.Add("key_base64","\"?\"");cases.Add("value_base64","\"?\"");
            return cases;
        }
    }
    [Theory]
    [MemberData(nameof(MalformedMatchedFields))]
    public void Given_NullOrWrongKindMatchedField_When_Judged_Then_P7InvalidWithoutThrowing(string field,string raw)
    {
        var repeat=SingleActionFixture.AssignedRepeat();
        var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!.AsObject();matched[field]=System.Text.Json.Nodes.JsonNode.Parse(raw);
        var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);
        Assert.Equal("invalid",result.Status);Assert.Contains("p7_outcome_record",result.Reasons);Assert.Null(result.OutcomeClass);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,false)][InlineData(int.MinValue,false)][InlineData(int.MaxValue,false)]
    public void Given_MatchedPartitionBoundary_When_Judged_Then_OnlyDeclaredPartitionPasses(int partition,bool accepted)
    {
        var repeat=SingleActionFixture.AssignedRepeat();var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!.AsObject();matched["partition"]=partition;
        var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);
        Assert.Equal(accepted?"complete_valid":"invalid",result.Status);Assert.Equal(!accepted,result.Reasons.Contains("p7_outcome_record",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData("key_base64",65535)][InlineData("key_base64",65536)][InlineData("key_base64",65537)]
    [InlineData("value_base64",1466667)][InlineData("value_base64",1466668)][InlineData("value_base64",1466669)]
    public void Given_MatchedBase64LengthBoundary_When_BytesDoNotMatch_Then_P7InvalidWithoutThrowing(string field,int length)
    {
        var repeat=SingleActionFixture.AssignedRepeat();var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!.AsObject();matched[field]=new string('a',length);
        var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);
        Assert.Equal("invalid",result.Status);Assert.Contains("p7_outcome_record",result.Reasons);
    }

}
