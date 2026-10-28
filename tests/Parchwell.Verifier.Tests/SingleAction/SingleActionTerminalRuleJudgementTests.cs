using System.Text;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionTerminalRuleJudgementTests
{
    private const string P4="p4_no_terminal_outcome", P7="p7_outcome_record", P6="p6_broker_count";
    private static readonly SingleActionOutcome NoOutcome=new("no_outcome",null,null,null), Success=new("success",null,null,null);
    private static readonly SingleActionConfiguration First=SingleActionFixture.Declaration with{TerminalRule="first_error_callback_or_delivery"};
    private static SingleActionClientError[] Errors(int count,int code=29)=>Enumerable.Range(0,count).Select(_=>new SingleActionClientError(code,false,"broker","synthetic",0)).ToArray();
    private static SingleActionRepeatProjection Judge(SingleActionRepeat repeat,SingleActionConfiguration? declaration=null)=>SingleActionFixture.Judge(repeat,declaration??SingleActionFixture.NoOutcomeDeclaration);
    private static void Check(SingleActionRepeatProjection result,string status,SingleActionOutcome? outcome,bool statistics,params string[] reasons)
    {
        Assert.Equal(status,result.Status);Assert.Equal(reasons,result.Reasons);Assert.Equal(outcome,result.OutcomeClass);
        if(statistics){Assert.Equal(0,result.BrokerCount);Assert.NotNull(result.StatisticsReference);}
        else{Assert.Null(result.BrokerCount);Assert.Null(result.StatisticsReference);}
    }
    [Theory]
    [InlineData("subscribe")][InlineData("assign_consume")]
    public void Given_WindowEndsWithoutOutcome_When_Judged_Then_ExplicitNullClassAndNativeStatistics(string action)
    {
        var result=Judge(SingleActionFixture.NoOutcomeRepeat(action:action),SingleActionFixture.NoOutcomeDeclaration with{Action=action});
        Check(result,"complete_valid",NoOutcome,true);Assert.Equal(120,result.StatisticsReference!.NativeTime);Assert.Equal(1,result.StatisticsReference.NativeTs);Assert.Equal(1,result.StatisticsReference.Sequence);
    }
    [Theory]
    [InlineData(119999,false)][InlineData(120000,true)][InlineData(120001,false)]
    public void Given_NoOutcomeElapsedWindowBoundary_When_Judged_Then_ExactWindowRequired(long elapsed,bool accepted)=>Check(Judge(SingleActionFixture.NoOutcomeRepeat() with{ElapsedMs=elapsed}),accepted?"complete_valid":"incomplete",NoOutcome,accepted,accepted?[]:[P4,P6]);
    [Theory]
    [InlineData("standard",true)][InlineData("standard",false)][InlineData("first_error_callback_or_delivery",true)][InlineData("first_error_callback_or_delivery",false)]
    public void Given_NoOutcomeUnderOtherRule_When_Judged_Then_P4AndP7IndependentlyReject(string rule,bool terminal)=>Check(Judge(SingleActionFixture.NoOutcomeRepeat() with{Terminal=terminal},SingleActionFixture.NoOutcomeDeclaration with{TerminalRule=rule}),"invalid",null,false,P4,P7,P6);
    [Theory]
    [InlineData("nonterminal")][InlineData("timestamp")][InlineData("capture_limit")]
    public void Given_NoOutcomeTerminalGap_When_Judged_Then_ClassRetainedWithoutStatistics(string gap)
    {
        var repeat=SingleActionFixture.NoOutcomeRepeat();repeat=gap switch{"nonterminal"=>repeat with{Terminal=false},"timestamp"=>repeat with{TerminalUnixMs=null},_=>repeat with{Diagnostics=["capture_limit"]}};
        Check(Judge(repeat),"incomplete",NoOutcome,false,P4,P6);
    }
    [Theory]
    [InlineData("error")][InlineData("matched")][InlineData("callback")][InlineData("dropped")]
    public void Given_NoOutcomeHasErrorOrMatchEvidence_When_Judged_Then_RecordInvalidButStatisticsRetained(string evidence)
    {
        var repeat=SingleActionFixture.NoOutcomeRepeat();repeat=evidence switch{"error"=>repeat with{Error=SingleActionFixture.Json("{\"code\":29,\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"synthetic\"}")},"matched"=>repeat with{Matched=SingleActionFixture.Json("{}")},"callback"=>repeat with{ClientErrors=Errors(1)},_=>repeat with{ClientErrorsDropped=1}};
        Check(Judge(repeat),"invalid",null,true,P7);
    }
    [Theory]
    [InlineData(119,false)][InlineData(120,true)][InlineData(121,true)]
    public void Given_NoOutcomeNativeTimeBoundary_When_Judged_Then_OnlyPostTerminalReportAccepted(long time,bool accepted)
    {
        var repeat=SingleActionFixture.NoOutcomeRepeat();repeat=repeat with{Statistics=[ConsumerStatistic(repeat,time)]};
        Check(Judge(repeat),accepted?"complete_valid":"incomplete",NoOutcome,accepted,accepted?[]:[P6]);
    }
    [Theory]
    [InlineData(119999,true)][InlineData(120000,true)][InlineData(120001,false)]
    public void Given_NoOutcomeTerminalTimestampBoundary_When_Judged_Then_NativeTimeCompared(long unix,bool accepted)=>Check(Judge(SingleActionFixture.NoOutcomeRepeat() with{TerminalUnixMs=unix}),accepted?"complete_valid":"incomplete",NoOutcome,accepted,accepted?[]:[P6]);
    [Theory]
    [InlineData("absent")][InlineData("malformed")][InlineData("wrong_client")][InlineData("wrong_type")]
    public void Given_NoOutcomeStatisticsUnavailable_When_Judged_Then_ZeroIsNeverInvented(string mutation)
    {
        var repeat=SingleActionFixture.NoOutcomeRepeat();string raw=Encoding.UTF8.GetString(Convert.FromBase64String(repeat.Statistics[0].RawBase64));
        repeat=mutation switch{"absent"=>repeat with{Statistics=[]},"malformed"=>repeat with{Statistics=[repeat.Statistics[0] with{RawBase64="?"}]},"wrong_client"=>repeat with{Statistics=[repeat.Statistics[0] with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw.Replace(repeat.ClientId,"other",StringComparison.Ordinal)))}]},_=>repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,120,1,1)]}};
        Check(Judge(repeat),"incomplete",NoOutcome,false,P6);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,false)]
    public void Given_StandardDroppedCountBoundary_When_Judged_Then_OnlyZeroAccepted(long dropped,bool accepted)=>Check(Judge(SingleActionFixture.Repeat() with{ClientErrorsDropped=dropped},SingleActionFixture.Declaration),accepted?"complete_valid":"invalid",accepted?Success:null,true,accepted?[]:[P7]);
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,false)]
    public void Given_NonterminalNullOutcomeDroppedCount_When_Judged_Then_CountStillChecked(long dropped,bool accepted)=>Check(Judge(SingleActionFixture.Repeat() with{Terminal=false,Result=SingleActionFixture.Json("null"),ClientErrorsDropped=dropped},SingleActionFixture.Declaration),accepted?"incomplete":"invalid",null,false,accepted?[P4,P6]:[P4,P7,P6]);
    [Theory]
    [InlineData(0,false)][InlineData(63,false)][InlineData(64,true)][InlineData(65,false)]
    public void Given_FirstRulePositiveDroppedCount_When_Judged_Then_Exactly64RecordedErrorsRequired(int count,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat() with{ClientErrors=Errors(count),ClientErrorsDropped=1};
        Check(Judge(repeat,First),accepted?"complete_valid":"invalid",accepted?Success:null,true,accepted?[]:[P7]);
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=repeat;Assert.Equal(count<=64,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,true)][InlineData(2,true)][InlineData(2147483648,true)][InlineData(long.MaxValue,true)]
    public void Given_FirstRule64ErrorsAndInt64DroppedCount_When_Judged_Then_AllNonnegativeValuesAccepted(long dropped,bool accepted)=>Check(Judge(SingleActionFixture.Repeat() with{ClientErrors=Errors(64),ClientErrorsDropped=dropped},First),accepted?"complete_valid":"invalid",accepted?Success:null,true,accepted?[]:[P7]);
    [Theory]
    [InlineData("standard")][InlineData("window_end_no_outcome")]
    public void Given_PositiveDroppedCountOutsideFirstRule_When_Judged_Then_RecordInvalid(string rule)=>Check(Judge(SingleActionFixture.Repeat() with{ClientErrors=Errors(64),ClientErrorsDropped=1},SingleActionFixture.Declaration with{TerminalRule=rule}),"invalid",null,true,P7);
    [Theory]
    [InlineData("standard",0)][InlineData("standard",63)][InlineData("standard",64)]
    [InlineData("first_error_callback_or_delivery",0)][InlineData("first_error_callback_or_delivery",63)][InlineData("first_error_callback_or_delivery",64)]
    public void Given_ZeroDroppedCountAndBoundedRecordedErrors_When_Judged_Then_OutcomeAccepted(string rule,int count)=>Check(Judge(SingleActionFixture.Repeat() with{ClientErrors=Errors(count)},SingleActionFixture.Declaration with{TerminalRule=rule}),"complete_valid",Success,true);
    [Theory]
    [InlineData(-169,29)][InlineData(29,-169)]
    public void Given_FirstRuleCallbackCodesDifferFromTerminal_When_Judged_Then_TerminalCodeDefinesClass(int callback,int terminal)
    {
        var repeat=SingleActionFixture.Repeat() with{ClientErrors=Errors(64,callback),ClientErrorsDropped=2,Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json($$"""{"code":{{terminal}},"is_fatal":false,"origin":"broker","reason":"synthetic"}""")};
        Check(Judge(repeat,First),"complete_valid",new("error",terminal,false,"broker"),true);
    }
    [Theory]
    [InlineData("subscribe",false)][InlineData("subscribe",true)][InlineData("assign_consume",false)][InlineData("assign_consume",true)]
    public void Given_WindowRuleEarlyOrdinaryOutcome_When_Judged_Then_SuccessOrErrorAccepted(string action,bool error)
    {
        var repeat=SingleActionFixture.NoOutcomeRepeat(action:action) with{ElapsedMs=119999,Result=SingleActionFixture.Json(error?"\"error\"":"\"success\""),Error=SingleActionFixture.Json(error?"{\"code\":29,\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"synthetic\"}":"null"),Matched=!error&&action=="assign_consume"?SingleActionFixture.Matched():SingleActionFixture.Json("null")};
        Check(Judge(repeat,SingleActionFixture.NoOutcomeDeclaration with{Action=action,Topic=action=="assign_consume"?"document-state":"document-events"}),"complete_valid",error?new("error",29,false,"broker"):Success,true);
    }
    private static SingleActionStatistic ConsumerStatistic(SingleActionRepeat repeat,long time)
    {
        var row=SingleActionFixture.Statistic(repeat.ClientId,time,1,1,observed:120000);
        return row with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Convert.FromBase64String(row.RawBase64)).Replace("producer","consumer",StringComparison.Ordinal)))};
    }
}
