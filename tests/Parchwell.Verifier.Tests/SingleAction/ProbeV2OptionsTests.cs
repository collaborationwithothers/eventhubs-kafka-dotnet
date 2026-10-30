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
    [Theory]
    [InlineData("smoke",true)][InlineData("batch",true)][InlineData("",false)][InlineData("Smoke",false)][InlineData("BATCH",false)][InlineData("other",false)]
    public void Given_RunKind_When_Validated_Then_OnlyExactExplicitModesAreAccepted(string mode,bool valid)
    {
        var values = Values(); values["ProbeV2Judge:RunKind"] = mode;
        if (valid) Assert.Equal(mode,Read(values).RunKind);
        else Assert.Equal("v2_run_kind",Assert.Throws<InvalidOperationException>(() => Read(values)).Message);
    }
    [Fact]
    public void Given_MissingRunKind_When_Validated_Then_NoDefaultModeIsSelected()
    {
        var values = Values(); values.Remove("ProbeV2Judge:RunKind");
        Assert.Equal("v2_run_kind",Assert.Throws<InvalidOperationException>(() => Read(values)).Message);
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
    [Theory]
    [InlineData("run",0,false)][InlineData("run",1,true)][InlineData("run",2,true)][InlineData("run",28,true)][InlineData("run",29,true)][InlineData("run",30,false)]
    [InlineData("namespace",0,false)][InlineData("namespace",1,true)][InlineData("namespace",2,true)][InlineData("namespace",62,true)][InlineData("namespace",63,true)][InlineData("namespace",64,false)]
    [InlineData("pod",0,false)][InlineData("pod",62,true)][InlineData("pod",63,true)][InlineData("pod",64,false)]
    [InlineData("commit",0,false)][InlineData("commit",39,false)][InlineData("commit",40,true)][InlineData("commit",41,false)]
    [InlineData("loadgen_digest",0,false)][InlineData("loadgen_digest",63,false)][InlineData("loadgen_digest",64,true)][InlineData("loadgen_digest",65,false)]
    [InlineData("verifier_digest",0,false)][InlineData("verifier_digest",63,false)][InlineData("verifier_digest",64,true)][InlineData("verifier_digest",65,false)]
    public void Given_IdentityStringLengthBoundary_When_Validated_Then_ExactWireAndDnsBoundsApply(string field,int length,bool valid)
    {
        var options = ProbeV2Fixture.Options();
        switch (field)
        {
            case "run":
                options.RunId = new string('a',length); options.JobName = ProbeV2Matrix.Job(options.RunId,"B08","C0","judge"); options.PodName = options.JobName + "-abc"; break;
            case "namespace": options.NamespaceName = new string('a',length); break;
            case "pod": options.PodName = length == 0 ? "" : options.JobName + "-" + new string('a',length - options.JobName.Length - 1); break;
            case "commit": options.SourceCommit = new string('a',length); break;
            case "loadgen_digest": options.LoadGenImage = "registry.test/loadgen@sha256:" + new string('a',length); break;
            default: options.VerifierImage = "registry.test/verifier@sha256:" + new string('a',length); break;
        }
        if (valid) options.Validate();
        else Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData("ProbeV2Judge:")][InlineData("ProbeV2Judge:Limits:")][InlineData("ProbeV2Judge:JudgeLimits:")][InlineData("ProbeV2Judge:ScenarioLoadOptions:")]
    public void Given_EmptyOptionGroup_When_Validated_Then_CompleteShapeIsStillRequired(string prefix)
    {
        var values = Values();
        foreach (string key in values.Keys.Where(key => key.StartsWith(prefix,StringComparison.Ordinal)).ToArray()) values.Remove(key);
        Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    public static TheoryData<string> StringLeaves => new(Values().Where(pair => pair.Key.StartsWith("ProbeV2Judge:",StringComparison.Ordinal) &&
        !pair.Key.StartsWith("ProbeV2Judge:Limits:",StringComparison.Ordinal) && !pair.Key.StartsWith("ProbeV2Judge:JudgeLimits:",StringComparison.Ordinal) &&
        !pair.Key.StartsWith("ProbeV2Judge:ScenarioLoadOptions:",StringComparison.Ordinal)).Select(pair => pair.Key));
    [Theory,MemberData(nameof(StringLeaves))]
    public void Given_EmptyIdentityOption_When_Validated_Then_RequiredTextCannotBeOmitted(string name)
    {
        var values = Values(); values[name] = ""; Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("user",0)][InlineData("user",1)][InlineData("user",2)]
    [InlineData("query",0)][InlineData("query",1)][InlineData("query",2)]
    [InlineData("fragment",0)][InlineData("fragment",1)][InlineData("fragment",2)]
    public void Given_BlobUriOptionalComponentLength_When_Validated_Then_EachComponentMustBeEmpty(string component,int length)
    {
        var options = ProbeV2Fixture.Options(); string text = new('x',length);
        options.BlobServiceUri = component switch
        {
            "user" => "https://" + (length == 0 ? "" : text + "@") + "syntheticstore.blob.core.windows.net/",
            "query" => "https://syntheticstore.blob.core.windows.net/" + (length == 0 ? "" : "?" + text[..^1]),
            _ => "https://syntheticstore.blob.core.windows.net/" + (length == 0 ? "" : "#" + text[..^1])
        };
        if (length == 0) options.Validate(); else Assert.Throws<InvalidOperationException>(options.Validate);
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
