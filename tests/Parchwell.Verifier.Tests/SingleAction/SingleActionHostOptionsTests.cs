using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionHostOptionsTests
{
    [Fact]
    public void Given_ClosedRenderedCoordinates_When_Bound_Then_ValidWithoutLegacyInputs()
    {
        var options=Bind(Values());Assert.True(new VerifierHostOptionsValidator().Validate(null,options).Succeeded);Assert.Equal(VerifierMode.SingleActionJudge,Mode(options.Mode));Assert.Equal(TimeSpan.FromSeconds(90),options.ProcessLifetime);Assert.Null(options.LedgerContainer);Assert.Null(options.Phase);Assert.Null(options.Repeat);
    }
    [Fact]
    public void Given_EachRequiredFieldRemoved_When_Bound_Then_RejectedDespiteParchwellFallbacks()
    {
        var values=Values();
        foreach(string field in values.Keys.Where(k=>k.StartsWith("Verifier:",StringComparison.Ordinal)))
        {
            var changed=new Dictionary<string,string?>(values,StringComparer.Ordinal){[field]=null};Assert.False(Validated(changed),field);
        }
    }
    [Theory]
    [InlineData("Verifier:Unknown")][InlineData("Verifier:Repeat")][InlineData("Verifier:Phase")][InlineData("Verifier:LedgerContainer")][InlineData("Verifier:SourceCommit")][InlineData("Verifier:SingleActionLimits:Unknown")][InlineData("Verifier:ScenarioLoadOptions:Unknown")][InlineData("Verifier:SeedReceiptPath")]
    public void Given_UnknownOrInactiveInput_When_Bound_Then_Rejected(string key)
    {
        var values=Values();values[key]="1";Assert.Throws<OptionsValidationException>(()=>Bind(values));
    }
    [Fact]
    public void Given_EveryFixedLimitAtAdjacentValues_When_Validated_Then_OnlyLiteralAccepted()
    {
        foreach(var field in typeof(SingleActionLimits).GetProperties())
        {
            var options=Bind(Values());long expected=Convert.ToInt64(field.GetValue(options.SingleActionLimits),CultureInfo.InvariantCulture);
            foreach(long delta in new long[]{-1,0,1})
            {
                object value=Nullable.GetUnderlyingType(field.PropertyType)==typeof(long)?expected+delta:(object)checked((int)(expected+delta));
                field.SetValue(options.SingleActionLimits,value);Assert.Equal(delta==0,new VerifierHostOptionsValidator().Validate(null,options).Succeeded);
            }
        }
    }
    [Theory]
    [InlineData("ScenarioId","A01")][InlineData("ConfigurationId","C4")][InlineData("RunId","UPPER")][InlineData("VerifierCommit","other")][InlineData("JobName","other")][InlineData("PodName","../other")][InlineData("PodUid","")][InlineData("BlobServiceUri","relative")][InlineData("ScenarioRoot","/tmp")][InlineData("CaptureContainer","single-action")][InlineData("CapturePrefix","other/")][InlineData("LoadGenImage","tag-only")][InlineData("VerifierImage","tag-only")][InlineData("EnrichmentImage","tag-only")][InlineData("VerifierContainer","ledger")]
    public void Given_CoordinateMismatch_When_Validated_Then_Rejected(string field,string value)
    {
        var values=Values();values["Verifier:"+field]=value;var options=Bind(values);Assert.False(new VerifierHostOptionsValidator().Validate(null,options).Succeeded);
    }
    private static bool Validated(Dictionary<string,string?> values)
    {
        try{return new VerifierHostOptionsValidator().Validate(null,Bind(values)).Succeeded;}
        catch(OptionsValidationException){return false;}
    }
    private static VerifierMode Mode(string? mode){Assert.True(VerifierModeParser.TryParse(mode,out var parsed));return parsed;}
    private static VerifierHostOptions Bind(Dictionary<string,string?> values)
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();var options=new VerifierHostOptions();new VerifierHostOptionsSetup(configuration).Configure(options);return options;
    }
    internal static Dictionary<string,string?> Values()
    {
        var values=new Dictionary<string,string?>(StringComparer.Ordinal)
        {
            ["Verifier:Mode"]="single-action-judge",["Verifier:ScenarioId"]="B01",["Verifier:ConfigurationId"]="C0",["Verifier:RunId"]="run1",["Verifier:VerifierCommit"]=SingleActionFixture.Source,["Verifier:JobName"]="run1-b01-c0-single-judge",["Verifier:PodName"]="judge-pod",["Verifier:PodUid"]="judge-uid",["Verifier:BlobServiceUri"]="https://storage.example/",["Verifier:VerifierContainer"]="verifier",["Verifier:NamespaceName"]="synthetic-namespace",["Verifier:ScenarioRoot"]="/app",["Verifier:LoadGenImage"]="x@sha256:"+new string('1',64),["Verifier:VerifierImage"]="x@sha256:"+new string('2',64),["Verifier:EnrichmentImage"]="x@sha256:"+new string('3',64),["Verifier:CaptureContainer"]="ledger",["Verifier:CapturePrefix"]="run1/single-action/B01/C0/",["PARCHWELL_SOURCE_COMMIT"]=SingleActionFixture.Source,["PARCHWELL_JOB_NAME"]="run1-b01-c0-single-judge",["PARCHWELL_POD_NAME"]="judge-pod",["PARCHWELL_POD_UID"]="judge-uid"
        };
        // These expected bounds are transcribed independently from the production options type.
        string[] fields=["MaxCaptureBytes","MaxRegistrationBytes","MaxCompletionBytes","MaxSeedReceiptBytes","MaxSeedPodBytes","MaxCandidateBytes","MaxTotalInputBytes","MaxInputDepth","MaxSettingsKeys","MaxSettingsBytes","MaxErrorBytes","MaxDiagnosticBytes","MaxDiagnostics","MaxClientErrors","MaxStatisticsReports","MaxStatisticsReportBytes","CallbackQueueCapacity","MaxBrokerEntries","MaxMatchedValueBytes","MaxNonmatchingRecords","ProcessLifetimeSeconds","IoSeconds","CleanupSeconds"];
        long[] bounds=[134217728,65536,65536,65536,65536,16777216,268435456,32,128,16384,4096,2048,100,64,64,262144,128,64,1100000,4096,90,10,10];
        for(int i=0;i<fields.Length;i++)values["Verifier:SingleActionLimits:"+fields[i]]=bounds[i].ToString(CultureInfo.InvariantCulture);
        values["Verifier:ScenarioLoadOptions:MaxDocumentCharacters"]="65536";values["Verifier:ScenarioLoadOptions:MaxNestingDepth"]="32";values["Verifier:ScenarioLoadOptions:MaxScalarCharacters"]="65536";values["Verifier:ScenarioLoadOptions:MaxParserEvents"]="20000";
        return values;
    }
}
