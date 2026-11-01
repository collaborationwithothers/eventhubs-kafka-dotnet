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

    [Theory]
    [InlineData("C6", "seed")]
    [InlineData("C9", "action")]
    public void GivenEnabledRefreshPathFlag_When_UsingOrdinaryActorTuple_Then_AcceptsStrictBoolean(string configuration, string stage)
    {
        ProbeV2Options options = Options("F06", configuration, stage);
        options.CaptureAuthenticationMetadata = true;
        options.TestHandlerRefreshPath = true;
        var environment = Environment(options);

        ProbeV2Options parsed = ProbeV2Options.Read(environment);

        Assert.True(parsed.TestHandlerRefreshPath);
        Assert.True(parsed.CaptureAuthenticationMetadata);
    }

    [Theory]
    [InlineData("B08", "C0", "action", true)]
    [InlineData("F06", "C0", "action", true)]
    [InlineData("F06", "C6", "action", true)]
    [InlineData("F06", "C6", "seed", false)]
    public void GivenEnabledRefreshPathFlag_When_TupleOrCaptureIsInvalid_Then_Rejects(string item, string configuration, string stage, bool capture)
    {
        ProbeV2Options options = OptionsForTuple(item, configuration, stage);
        options.CaptureAuthenticationMetadata = capture;
        options.TestHandlerRefreshPath = true;
        Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(options)));
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("yes")]
    public void GivenMalformedRefreshPathFlag_When_ReadingOptions_Then_Rejects(string value)
    {
        ProbeV2Options options = Options("F06", "C6", "seed");
        options.CaptureAuthenticationMetadata = true;
        Dictionary<string, string> environment = Environment(options);
        environment["ProbeV2__TestHandlerRefreshPath"] = value;
        Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(environment));
    }
    [Theory]
    [InlineData("nativejwt", "jwt-bearer")]
    [InlineData("nativeimds", "azure-imds-workload")]
    [InlineData("nativeimdsnode", "azure-imds-default")]
    public void GivenNativePair_When_ReadingOptions_Then_BindsNativeFieldsAndAuthenticationInputs(string stage, string mode)
    {
        ProbeV2Options parsed = ProbeV2Options.Read(Environment(NativeOptions(stage, mode)));

        Assert.Equal(stage, parsed.Stage);
        Assert.Equal(mode, parsed.NativeAuthentication);
        Assert.Equal("33333333-3333-3333-3333-333333333333", parsed.NativePrincipalId);
        Assert.Equal("00000000-0000-0000-0000-000000000004", parsed.TenantId);
        Assert.Equal("/unopened/token", parsed.FederatedTokenFile);
    }

    [Fact]
    public void GivenNativeStageWithRefreshPath_When_ReadingOptions_Then_RejectsAsRefreshSelection()
    {
        ProbeV2Options options = NativeOptions("nativejwt", "jwt-bearer");
        options.TestHandlerRefreshPath = true;
        Assert.Equal("v2_handler_refresh_selection", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(options))).Message);
    }

    [Theory]
    [InlineData("nativejwt", "azure-imds-workload", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativejwt", "azure-imds-default", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativeimds", "jwt-bearer", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativeimds", "azure-imds-default", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativeimdsnode", "jwt-bearer", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativeimdsnode", "azure-imds-workload", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativejwt", "jwt-bearer", false, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativejwt", "jwt-bearer", true, "")]
    [InlineData("nativejwt", "jwt-bearer", true, "x")]
    [InlineData("nativejwt", "jwt-bearer", true, "33333333333333333333333333333333")]
    [InlineData("nativejwt", "jwt-bearer", true, "{33333333-3333-3333-3333-333333333333}")]
    [InlineData("seed", "jwt-bearer", true, "33333333-3333-3333-3333-333333333333")]
    [InlineData("nativejwt", "", true, "33333333-3333-3333-3333-333333333333")]
    public void GivenInvalidNativeSelection_When_ReadingOptions_Then_Rejects(string stage, string mode, bool capture, string principal)
    {
        ProbeV2Options options = NativeOptions(stage, mode);
        options.CaptureAuthenticationMetadata = capture;
        options.NativePrincipalId = principal;
        Assert.Equal("v2_native_authentication_selection", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(options))).Message);
    }

    [Fact]
    public void GivenNativeStageOnAnotherConfiguration_When_ReadingOptions_Then_RejectsAsStage()
    {
        ProbeV2Options options = NativeOptions("nativejwt", "jwt-bearer");
        options.ConfigurationId = "C9";
        Assert.Equal("v2_stage", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(options))).Message);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("jwt-bearer\n")]
    public void GivenMalformedNativeValue_When_ReadingOptions_Then_RejectsAsNativeOption(string value)
    {
        ProbeV2Options options = NativeOptions("nativejwt", "jwt-bearer");
        options.NativeAuthentication = value;
        Assert.Equal("v2_native_authentication_option", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(options))).Message);
    }

    [Fact]
    public void GivenProcessLifetimes_When_ReadingOptions_Then_NativeStageNeedsEightHundredFortyAndProduceStageStillNeedsNineteenTwenty()
    {
        ProbeV2Options native = NativeOptions("nativejwt", "jwt-bearer");
        native.ProcessLifetimeSeconds = 1920;
        Assert.Equal("v2_identity", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(native))).Message);

        ProbeV2Options ordinary = Options("F06", "C6", "seed");
        ordinary.ProcessLifetimeSeconds = 840;
        Assert.Equal("v2_identity", Assert.Throws<InvalidOperationException>(() => ProbeV2Options.Read(Environment(ordinary))).Message);
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
            Limits = ProbeV2Limits.Fixed(), ProcessLifetimeSeconds = row.Action == "produce_one" && row.WindowMs == 330000 ? 1920 : 840,
            ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }
        };
    }
    internal static ProbeV2Options NativeOptions(string stage, string mode)
    {
        ProbeV2Options options = Options("F06", "C6", stage);
        options.CaptureAuthenticationMetadata = true;
        options.NativeAuthentication = mode;
        options.NativePrincipalId = "33333333-3333-3333-3333-333333333333";
        options.WorkloadIdentityClientId = "22222222-2222-2222-2222-222222222222";
        options.NamespaceName = "pwtest";
        options.TenantId = "11111111-1111-1111-1111-111111111111";
        options.FederatedTokenFile = "/var/run/secrets/azure/tokens/azure-identity-token";
        return options;
    }
    private static ProbeV2Options OptionsForTuple(string item, string config, string stage)
    {
        ProbeV2Stage selected = ProbeV2Stage.For(item, config, stage);
        ProbeV2Options options = Options();
        options.ScenarioId = item;
        options.ConfigurationId = config;
        options.Stage = stage;
        options.ServiceAccount = selected.Account;
        options.JobName = ProbeV2Options.Job(options.RunId, item, config, stage);
        options.PodName = options.JobName + "-pod";
        options.ProcessLifetimeSeconds = selected.Action == "produce_one" && selected.WindowMs == 330000 ? 1920 : 840;
        return options;
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
                if (nested is string or int or bool)
                    env[prefix + property.Name] = nested is bool boolean
                        ? boolean ? "true" : "false"
                        : Convert.ToString(nested, CultureInfo.InvariantCulture)!;
                else Flatten(nested, prefix + property.Name + "__");
            }
        }
    }
}
