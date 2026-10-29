using System.Collections;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2OptionsTests
{
    public static TheoryData<string> RequiredLeaves => new(Values().Keys.Where(x => x.StartsWith("ProbeV2Judge:",StringComparison.Ordinal)));
    public static TheoryData<string,int,int> FixedBounds
    {
        get
        {
            var cases = new TheoryData<string,int,int>();
            foreach (var pair in Values().Where(x => x.Key.StartsWith("ProbeV2Judge:Limits:",StringComparison.Ordinal) ||
                x.Key.StartsWith("ProbeV2Judge:JudgeLimits:",StringComparison.Ordinal) || x.Key.StartsWith("ProbeV2Judge:ScenarioLoadOptions:",StringComparison.Ordinal)))
            {
                int value = int.Parse(pair.Value!,CultureInfo.InvariantCulture);
                foreach (int delta in new[] { -1,0,1 }) cases.Add(pair.Key,value,delta);
            }
            return cases;
        }
    }
    [Theory,MemberData(nameof(FixedBounds))]
    public void Given_FixedNumericOption_When_AtOrAdjacentToContractValue_Then_OnlyExactValueIsAccepted(string name,int value,int delta)
    {
        var values = Values(); values[name] = (value + delta).ToString(CultureInfo.InvariantCulture);
        if (delta == 0) Assert.NotNull(Read(values));
        else Assert.Throws<InvalidOperationException>(() => Read(values));
    }
    [Fact]
    public void Given_ExactInputs_When_ValidatedOffline_Then_NoEvidenceOrTokenFileIsOpened()
    {
        var values = Values(); values["AZURE_FEDERATED_TOKEN_FILE"] = "/definitely-missing/token";
        var options = Read(values); Assert.Equal("B08",options.ScenarioId); Assert.Equal("verifier",options.ServiceAccount);
        Assert.Equal(2,options.ImageDigests().Count);
    }
    [Theory,MemberData(nameof(RequiredLeaves))]
    public void Given_MissingLeaf_When_Validated_Then_DefaultsCannotFillIt(string name)
    {
        var values = Values(); values.Remove(name); Assert.Throws<InvalidOperationException>(() => Read(values));
    }
    [Theory]
    [InlineData("ProbeV2Judge:Unexpected")][InlineData("ProbeV2Judge:Limits:Unexpected")][InlineData("ProbeV2Judge:JudgeLimits:Unexpected")][InlineData("ProbeV2Judge:ScenarioLoadOptions:Unexpected")]
    public void Given_UnknownOption_When_Validated_Then_ClosedInputRejectsIt(string name)
    {
        var values = Values(); values[name] = "1"; Assert.Throws<InvalidOperationException>(() => Read(values));
    }
    [Theory]
    [InlineData("ProbeV2Judge:RunId","")][InlineData("ProbeV2Judge:RunId","UPPER")]
    [InlineData("ProbeV2Judge:SourceCommit","abc")][InlineData("ProbeV2Judge:ServiceAccount","probe")]
    [InlineData("ProbeV2Judge:JobName","other-job")][InlineData("ProbeV2Judge:PodName","other-pod")]
    [InlineData("ProbeV2Judge:Stage","read")][InlineData("ProbeV2Judge:InputRoot","/tmp/inputs")]
    [InlineData("ProbeV2Judge:BlobServiceUri","http://example.test/")][InlineData("ProbeV2Judge:BlobServiceUri","https://example.test/path")]
    [InlineData("ProbeV2Judge:VerifierContainer","ledger")][InlineData("ProbeV2Judge:LoadGenImage","registry.test/loadgen:latest")]
    [InlineData("ProbeV2Judge:Limits:MaxCaptureBytes","524287")][InlineData("ProbeV2Judge:Limits:MaxCaptureBytes","524289")]
    [InlineData("ProbeV2Judge:JudgeLimits:ProcessLifetimeSeconds","89")][InlineData("ProbeV2Judge:JudgeLimits:ProcessLifetimeSeconds","91")]
    [InlineData("ProbeV2Judge:ScenarioLoadOptions:MaxParserEvents","99999")][InlineData("ProbeV2Judge:ScenarioLoadOptions:MaxParserEvents","100001")]
    [InlineData("AZURE_CLIENT_ID","11111111-1111-1111-1111-111111111111")][InlineData("AZURE_TENANT_ID","bad")][InlineData("AZURE_FEDERATED_TOKEN_FILE","relative")]
    public void Given_InvalidIdentityOrFixedBound_When_Validated_Then_StartupFails(string name,string value)
    {
        var values = Values(); values[name] = value; Assert.Throws<InvalidOperationException>(() => Read(values));
    }
    [Theory]
    [InlineData("ProbeV2__RunId")][InlineData("SingleAction__RunId")][InlineData("Verifier__Mode")][InlineData("probev2judge__RunId")]
    public void Given_MixedOrWrongCasePrefix_When_Validated_Then_EnvironmentIsRejected(string name)
    {
        var environment = new Hashtable(StringComparer.Ordinal) { [name] = "synthetic" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();
        Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(configuration,environment));
    }
    [Fact]
    public void Given_CaseCollidingEnvironmentLeaves_When_Validated_Then_NoBindingOrderCanChooseWinner()
    {
        var environment = new Hashtable(StringComparer.Ordinal) { ["ProbeV2Judge__RunId"] = "run1",["ProbeV2Judge__runid"] = "run2" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();
        Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(configuration,environment));
    }
    [Fact]
    public void Given_DuplicateDeploymentIdentity_When_ItDisagrees_Then_StartupFails()
    {
        var values = Values(); values["PARCHWELL_POD_UID"] = "other"; Assert.Throws<InvalidOperationException>(() => Read(values));
    }
    private static ProbeV2Options Read(Dictionary<string,string?> values) => ProbeV2Options.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    private static Dictionary<string,string?> Values()
    {
        var values = new Dictionary<string,string?>(StringComparer.Ordinal); var options = ProbeV2Fixture.Options();
        void Flatten(string prefix,object value)
        {
            foreach (var property in value.GetType().GetProperties())
            {
                object child = property.GetValue(value)!; string key = prefix + ":" + property.Name;
                if (child is string text) values.Add(key,text);
                else if (child is int number) values.Add(key,number.ToString(CultureInfo.InvariantCulture));
                else Flatten(key,child);
            }
        }
        Flatten("ProbeV2Judge",options);
        values.Add("AZURE_CLIENT_ID",options.WorkloadIdentityClientId); values.Add("AZURE_TENANT_ID","11111111-1111-1111-1111-111111111111");
        values.Add("AZURE_FEDERATED_TOKEN_FILE","/tmp/synthetic-token"); return values;
    }
}
