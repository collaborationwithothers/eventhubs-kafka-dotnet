using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2OptionsTests
{
    [Fact]
    public void Given_AllRequiredLeaves_When_Validating_Then_PathsNeedNotExist()
    {
        var env = Environment(Options());
        Assert.Equal("B08", ProbeV2Options.Read(env).ScenarioId);
    }
    [Fact]
    public void Given_ActorHost_When_Composed_Then_ItRegistersNoBlobClient()
    {
        using var host = ProbeV2Host.BuildHost(telemetry: false, environment: Environment(Options()));
        Assert.Null(host.Services.GetService<Azure.Storage.Blobs.BlobServiceClient>());
        Assert.NotNull(host.Services.GetService<ProbeV2SessionFactory>());
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("case")]
    [InlineData("legacy")]
    [InlineData("identity")]
    public void Given_InvalidEnvironment_When_Validating_Then_Rejects(string kind)
    {
        var env = Environment(Options());
        switch (kind)
        {
            case "missing": env.Remove("ProbeV2__Limits__MaxCaptureBytes"); break;
            case "unknown": env.Add("ProbeV2__Extra", "x"); break;
            case "case": env.Add("ProbeV2__runid", "run-1"); break;
            case "legacy": env.Add("SingleAction__RunId", "run-1"); break;
            case "identity": env["AZURE_CLIENT_ID"] = "00000000-0000-0000-0000-000000000002"; break;
        }
        Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(env));
    }
    [Fact]
    public void Given_EachFixedLimitChanged_When_Validating_Then_Rejects()
    {
        foreach (System.Reflection.PropertyInfo property in typeof(ProbeV2Limits).GetProperties())
        {
            ProbeV2Options options = Options();
            int value = (int)property.GetValue(options.Limits)!;
            property.SetValue(options.Limits, value - 1);
            Assert.Throws<InvalidOperationException>(options.Validate);
        }
    }
    [Fact]
    public void Given_AllDeclaredActorStages_When_DerivingMatrix_Then_ThereAreThirtyThreeJobsWithCorrectActors()
    {
        var stages = new List<ProbeV2Stage>();
        foreach (string item in new[] { "B08", "E01", "F06" })
            for (int config = 0; config < (item == "F06" ? 12 : 2); config++)
                foreach (string stage in new[] { "seed", "seedread", "action", "read" })
                    try { stages.Add(ProbeV2Stage.For(item, "C" + config.ToString(CultureInfo.InvariantCulture), stage)); }
                    catch (InvalidOperationException) { }
        Assert.Equal(33, stages.Count);
        Assert.Equal("loadgen", ProbeV2Stage.For("B08", "C1", "action").Account);
        Assert.Equal("probe", ProbeV2Stage.For("B08", "C1", "read").Account);
        Assert.Equal("explicit", ProbeV2Stage.For("B08", "C1", "read").PartitionMode);
        Assert.Equal("enrichment-consumer", ProbeV2Stage.For("E01", "C0", "seed").Account);
        Assert.Equal("C7", ProbeV2Stage.For("F06", "C10", "action").SourceConfiguration);
        Assert.Throws<InvalidOperationException>(() => ProbeV2Stage.For("F04", "C0", "action"));
    }
    internal static ProbeV2Options Options(string item = "B08", string config = "C0", string stage = "action")
    {
        ProbeV2Stage row = ProbeV2Stage.For(item, config, stage);
        string job = ProbeV2Options.Job("run-1", item, config, stage);
        return new()
        {
            ScenarioId = item, ConfigurationId = config, Stage = stage, RunId = "run-1", SourceCommit = new string('a', 40),
            JobName = job, PodName = job + "-pod", PodUid = "00000000-0000-0000-0000-000000000003",
            NamespaceName = "synthetic-namespace", ServiceAccount = row.Account,
            WorkloadIdentityClientId = "00000000-0000-0000-0000-000000000001", ScenarioRoot = "/app", InputRoot = "/run/probe-inputs",
            Limits = ProbeV2Limits.Fixed(), ProcessLifetimeSeconds = row.Action == "produce_one" ? 1920 : 840,
            ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }
        };
    }
    internal static Dictionary<string, string> Environment(ProbeV2Options options)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        Flatten(options, "ProbeV2__");
        env["AZURE_TENANT_ID"] = "00000000-0000-0000-0000-000000000004";
        env["AZURE_CLIENT_ID"] = options.WorkloadIdentityClientId;
        env["AZURE_FEDERATED_TOKEN_FILE"] = "/unopened/token";
        env["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit;
        env["PARCHWELL_JOB_NAME"] = options.JobName;
        env["PARCHWELL_POD_NAME"] = options.PodName;
        env["PARCHWELL_POD_UID"] = options.PodUid;
        return env;
        void Flatten(object value, string prefix)
        {
            foreach (System.Reflection.PropertyInfo property in value.GetType().GetProperties())
            {
                object nested = property.GetValue(value)!;
                if (nested is string or int) env[prefix + property.Name] = Convert.ToString(nested, CultureInfo.InvariantCulture)!;
                else Flatten(nested, prefix + property.Name + "__");
            }
        }
    }
}
