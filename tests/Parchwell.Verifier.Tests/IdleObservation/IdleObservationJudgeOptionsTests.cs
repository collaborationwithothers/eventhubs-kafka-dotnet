using System.Collections;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Parchwell.Verifier.VerifyRun.IdleObservation;
using Xunit;
using static Parchwell.Verifier.Tests.IdleObservation.IdleFixture;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.IdleObservation;

public sealed class IdleObservationJudgeOptionsTests
{
    private const string Section = "IdleObservationJudge:";

    public static TheoryData<string> RequiredLeaves => new(Values().Keys.Where(x => x.StartsWith(Section, StringComparison.Ordinal)));

    public static TheoryData<string, int, int> FixedBounds
    {
        get
        {
            var cases = new TheoryData<string, int, int>();
            foreach (var pair in Values().Where(x => x.Key.StartsWith(Section + "Limits:", StringComparison.Ordinal) || x.Key.StartsWith(Section + "ScenarioLoadOptions:", StringComparison.Ordinal)))
            {
                int value = int.Parse(pair.Value!, CultureInfo.InvariantCulture);
                foreach (int delta in new[] { -1, 0, 1 }) cases.Add(pair.Key, value, delta);
            }
            return cases;
        }
    }

    [Theory, MemberData(nameof(FixedBounds))]
    public void Given_FixedNumericOption_When_AtOrAdjacentToContractValue_Then_OnlyExactValueIsAccepted(string name, int value, int delta)
    {
        var values = Values();
        values[name] = (value + delta).ToString(CultureInfo.InvariantCulture);
        if (delta == 0) Assert.NotNull(Read(values));
        else Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("smoke", true)]
    [InlineData("batch", true)]
    [InlineData("", false)]
    [InlineData("Batch", false)]
    [InlineData("other", false)]
    public void Given_RunKind_When_Validated_Then_OnlyExactModesAreAccepted(string mode, bool valid)
    {
        var values = Values();
        values[Section + "RunKind"] = mode;
        if (valid) Assert.Equal(mode, Read(values).RunKind);
        else Assert.Equal("idle_judge_run_kind", Assert.Throws<InvalidOperationException>(() => Read(values)).Message);
    }

    [Fact]
    public void Given_ExactInputs_When_Validated_Then_AllSevenCapturePodsAreParsed()
    {
        var options = Read(Values());
        Assert.Equal(7, options.Pods().Count);
        Assert.Equal(("run1-a03-c2-idle-abc", "uid-A03C2"), options.Pods()["A03C2"]);
        Assert.Equal(2, options.ImageDigests().Count);
    }

    [Theory, MemberData(nameof(RequiredLeaves))]
    public void Given_MissingLeaf_When_Validated_Then_DefaultsCannotFillIt(string name)
    {
        var values = Values();
        values.Remove(name);
        Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("Unexpected")]
    [InlineData("Limits:Unexpected")]
    [InlineData("ScenarioLoadOptions:Unexpected")]
    [InlineData("ScenarioId")]
    [InlineData("Stage")]
    public void Given_UnknownOption_When_Validated_Then_ClosedInputRejectsIt(string name)
    {
        var values = Values();
        values[Section + name] = "1";
        Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("RunId", "")]
    [InlineData("RunId", "UPPER")]
    [InlineData("SourceCommit", "abc")]
    [InlineData("ServiceAccount", "probe")]
    [InlineData("JobName", "other-job")]
    [InlineData("PodName", "other-pod")]
    [InlineData("LedgerContainer", "verifier")]
    [InlineData("VerifierContainer", "ledger")]
    [InlineData("BlobServiceUri", "http://example.test/")]
    [InlineData("BlobServiceUri", "https://example.test/path")]
    [InlineData("LoadGenImage", "registry.test/loadgen:latest")]
    [InlineData("NamespaceName", "azure-ns")]
    [InlineData("ScenarioRoot", "relative")]
    public void Given_InvalidIdentity_When_Validated_Then_StartupFails(string name, string value)
    {
        var values = Values();
        values[Section + name] = value;
        Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("AZURE_CLIENT_ID", "11111111-1111-1111-1111-111111111111")]
    [InlineData("AZURE_TENANT_ID", "bad")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "relative")]
    [InlineData("PARCHWELL_POD_UID", "other")]
    [InlineData("PARCHWELL_JOB_NAME", "other")]
    public void Given_AuthenticationOrDeploymentInput_When_ItDisagrees_Then_StartupFails(string name, string value)
    {
        var values = Values();
        values[name] = value;
        Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("A02C0:run1-a02-c0-idle-abc:uid,A02C0:run1-a02-c0-idle-def:uid2", false)]
    [InlineData("A02C2:run1-a02-c2-idle-abc:uid", false)]
    [InlineData("A02C0:run1-a02-c1-idle-abc:uid", false)]
    [InlineData("A02C0:other-a02-c0-idle-abc:uid", false)]
    [InlineData("A02C0:run1-a02-c0-idle-abc:", false)]
    [InlineData("A02C0:run1-a02-c0-idle-abc:u:id", false)]
    [InlineData("A02C0:run1-a02-c0-idle-abc", false)]
    [InlineData("A02C0:RUN1-a02-c0-idle-abc:uid", false)]
    [InlineData("A02C0:run1-a02-c0-idle-abc:uid,", false)]
    [InlineData("", false)]
    [InlineData("none", true)]
    [InlineData("A02C0:run1-a02-c0-idle-abc:uid", true)]
    [InlineData("A04C1:run1-a04-c1-idle-x:uid,A02C0:run1-a02-c0-idle-abc:uid", true)]
    public void Given_CapturePods_When_Validated_Then_OnlyDistinctKnownPodsOfTheRunAreAccepted(string text, bool valid)
    {
        var values = Values();
        values[Section + "CapturePods"] = text;
        if (valid) Assert.Equal(text == "none" ? 0 : text.Split(',').Length, Read(values).Pods().Count);
        else Assert.Throws<InvalidOperationException>(() => Read(values));
    }

    [Theory]
    [InlineData("ProbeV2__RunId")]
    [InlineData("ProbeV2Judge__RunId")]
    [InlineData("SingleAction__RunId")]
    [InlineData("Verifier__Mode")]
    [InlineData("IdleObservation__RunId")]
    [InlineData("idleobservation__RunId")]
    [InlineData("idleobservationjudge__RunId")]
    public void Given_MixedOrWrongCasePrefix_When_Validated_Then_EnvironmentIsRejected(string name)
    {
        var environment = new Hashtable(StringComparer.Ordinal) { [name] = "synthetic" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();
        Assert.Throws<InvalidOperationException>(() => IdleObservationJudgeOptions.Read(configuration, environment));
    }

    [Fact]
    public void Given_CaseCollidingEnvironmentLeaves_When_Validated_Then_NoBindingOrderCanChooseWinner()
    {
        var environment = new Hashtable(StringComparer.Ordinal) { ["IdleObservationJudge__RunId"] = "run1", ["IdleObservationJudge__runid"] = "run2" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();
        Assert.Throws<InvalidOperationException>(() => IdleObservationJudgeOptions.Read(configuration, environment));
    }

    [Fact]
    public void Given_OwnEnvironmentPrefix_When_Validated_Then_ItIsAccepted()
    {
        var environment = new Hashtable(StringComparer.Ordinal) { ["IdleObservationJudge__RunId"] = "run1", ["PATH"] = "/usr/bin" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Values()).Build();
        Assert.NotNull(IdleObservationJudgeOptions.Read(configuration, environment));
    }

    private static IdleObservationJudgeOptions Read(Dictionary<string, string?> values) =>
        IdleObservationJudgeOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    private static Dictionary<string, string?> Values()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var options = JudgeOptions();
        void Flatten(string prefix, object value)
        {
            foreach (var property in value.GetType().GetProperties())
            {
                object child = property.GetValue(value)!;
                string key = prefix + ":" + property.Name;
                if (child is string text) values.Add(key, text);
                else if (child is int number) values.Add(key, number.ToString(CultureInfo.InvariantCulture));
                else Flatten(key, child);
            }
        }
        Flatten("IdleObservationJudge", options);
        values.Add("AZURE_CLIENT_ID", options.WorkloadIdentityClientId);
        values.Add("AZURE_TENANT_ID", "11111111-1111-1111-1111-111111111111");
        values.Add("AZURE_FEDERATED_TOKEN_FILE", "/tmp/synthetic-token");
        values.Add("PARCHWELL_POD_UID", options.PodUid);
        values.Add("PARCHWELL_JOB_NAME", options.JobName);
        return values;
    }
}
