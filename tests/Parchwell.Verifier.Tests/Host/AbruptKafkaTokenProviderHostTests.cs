using System.Globalization;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Authentication;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Component;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
/// <summary>Shows which Kafka token provider each verifier mode composes and how the abrupt mode's credential is configured.</summary>
public sealed class AbruptKafkaTokenProviderHostTests
{
    [Fact]
    public void Given_AbruptVerifyMode_When_HostBuilt_Then_OneAbruptTokenProviderIsRegistered()
    {
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(AbruptArguments(), services => registrations.AddRange(services));
        Assert.Single(registrations, service => service.ServiceType == typeof(IKafkaTokenProvider));
        Assert.IsType<AbruptKafkaTokenProvider>(host.Services.GetRequiredService<IKafkaTokenProvider>());
    }

    [Fact]
    public void Given_AbruptVerifyMode_When_ProviderResolved_Then_CredentialCarriesPinnedRetryAndIdentity()
    {
        using IHost host = Program.BuildHost(AbruptArguments());
        var provider = Assert.IsType<AbruptKafkaTokenProvider>(host.Services.GetRequiredService<IKafkaTokenProvider>());
        var options = provider.CredentialOptions;
        Assert.Equal(TimeSpan.FromSeconds(2), options.Retry.NetworkTimeout);
        Assert.Equal(1, options.Retry.MaxRetries);
        Assert.Equal(RetryMode.Fixed, options.Retry.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.Retry.Delay);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Retry.MaxDelay);
        Assert.Null(options.RetryPolicy);
#pragma warning disable AZID0002 // IsAzureProxyEnabled is experimental in Azure.Core 1.63.0.
        Assert.False(options.IsAzureProxyEnabled);
#pragma warning restore AZID0002
        Assert.Equal("synthetic-tenant", options.TenantId);
        Assert.Equal("synthetic-client", options.ClientId);
        Assert.Equal("/tmp/synthetic-token", options.TokenFilePath);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    [InlineData("b10-sweep-line")]
    [InlineData("b10-sweep")]
    public void Given_OtherKafkaModes_When_HostBuilt_Then_SharedTokenProviderIsKept(string mode)
    {
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(ArgumentsFor(mode), services => registrations.AddRange(services));
        Assert.Single(registrations, service => service.ServiceType == typeof(IKafkaTokenProvider));
        Assert.Equal("Parchwell.Kafka.Authentication.WorkloadIdentityTokenProvider",
            host.Services.GetRequiredService<IKafkaTokenProvider>().GetType().FullName);
    }

    private static string[] ArgumentsFor(string mode) => mode switch
    {
        "start-line" or "verify" => VerifierHostTestOptions.ArgumentsFor(mode, VerifierHostTestOptions.TempPath()),
        "b10-sweep-line" => Arguments(B10SweepLineOptionsTests.Configuration(B10SweepLineOptionsTests.CaptureOptions("C1"))),
        "b10-sweep" => Arguments(SweepConfiguration(SweepOptions())),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static string[] Arguments(Dictionary<string, string?> values) =>
        values.Select(pair => $"--{pair.Key}={pair.Value}").ToArray();

    // The key-value pairs B10AbruptVerifyHarness sets for this mode, with the run name "synthetic".
    private static string[] AbruptArguments()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string name, string value) in new Dictionary<string, string>
        {
            ["Mode"] = "b10-abrupt-verify", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["RunId"] = "synthetic", ["VerifierCommit"] = B10AbruptVerifyEvidence.Commit, ["SourceCommit"] = B10AbruptVerifyEvidence.Commit,
            ["RepeatPrefix"] = "synthetic/C1/r1/", ["ExpectedInstanceId"] = "synthetic-c1-r1-loadgen",
            ["JobName"] = "synthetic-c1-r1-b10-abrupt-verify", ["PodName"] = "synthetic-verifier", ["PodUid"] = "synthetic-verifier-uid",
            ["VerifierClientId"] = "synthetic-verifier", ["NamespaceName"] = "synthetic-namespace", ["BlobServiceUri"] = "https://synthetic.blob.core.windows.net/",
            ["VerifierContainer"] = "verifier", ["LedgerContainer"] = "ledger", ["TenantId"] = "synthetic-tenant", ["WorkloadIdentityClientId"] = "synthetic-client",
            ["FederatedTokenFilePath"] = "/tmp/synthetic-token", ["LoadGenImage"] = B10AbruptVerifyEvidence.Image,
            ["VerifierImage"] = "registry.invalid/verifier@sha256:" + new string('c', 64),
            ["StartLineInputName"] = PhasePaths.StartLine("synthetic", "C1", 1), ["ProcessLifetime"] = "00:01:00", ["CleanupTimeout"] = "00:00:10",
            ["IoTimeout"] = "00:00:10", ["BlobIoTimeout"] = "00:00:10", ["TokenTimeout"] = "00:00:10", ["MetadataTimeout"] = "00:00:10",
            ["WatermarkTimeout"] = "00:00:10", ["PollSlice"] = "00:00:00.050", ["SafetyStop"] = "00:00:20",
            ["MaxOutputBytes"] = "16777216", ["MaxDiagnosticsPerCheck"] = "100", ["MaxDiagnosticBytes"] = "4096", ["MaxPartitions"] = "32",
            ["ScenarioRoot"] = B10AbruptVerifyEvidence.RepositoryRoot(), ["ArtifactPrefix"] = "synthetic/C1/r1/producer/",
            ["MaxRowBytes"] = "1048576", ["MaxRowsPerArtifact"] = "10000", ["MaxInventoryEntries"] = "10000",
            ["MaxInputEntries"] = "10000", ["MaxNativeReportBytes"] = "1048576", ["MaxBrokerEntries"] = "100",
            ["ScenarioLoadOptions:MaxDocumentCharacters"] = "131072", ["ScenarioLoadOptions:MaxNestingDepth"] = "32",
            ["ScenarioLoadOptions:MaxScalarCharacters"] = "32768", ["ScenarioLoadOptions:MaxParserEvents"] = "16384",
            ["MaxInputDepth"] = "32", ["MaxArtifactBytes"] = "268435456", ["MaxTotalInputBytes"] = "2147483648", ["MaxConsumedRecords"] = "1000", ["MaxConsumedBytes"] = "1048576"
        }) values["Verifier:" + name] = value;
        values["PARCHWELL_SOURCE_COMMIT"] = B10AbruptVerifyEvidence.Commit;
        values["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        values["Telemetry:ServiceName"] = "Parchwell.Verifier";
        values["Telemetry:ServiceInstanceId"] = "synthetic-verifier";
        values["Telemetry:ScenarioId"] = "B10";
        values["Telemetry:RunId"] = "synthetic";
        return Arguments(values);
    }

    // Copied unchanged from B10SweepOptionsTests (`SweepOptions` and `Configuration`), where both are private.
    private static VerifierHostOptions SweepOptions(string phase = "C0")
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("b10-sweep");
        options.ScenarioId = "B10";
        options.Phase = phase;
        options.ConfigurationId = phase;
        options.PhasePrefix = PhaseIdentity.PhasePrefix(options.RunId!, phase);
        options.JobName = PhaseIdentity.B10SweepJobName(options.RunId!, phase);
        options.Repeat = null;
        options.RepeatPrefix = null;
        options.ExpectedInstanceId = null;
        options.InstanceId = null;
        options.ArtifactPrefix = null;
        options.StartLineInputName = null;
        options.ResultOutputName = null;
        options.ScenarioRoot = null;
        options.ScenarioLoadOptions = null;
        options.B10SettleIntervalMilliseconds = 1000;
        return options;
    }

    private static Dictionary<string, string?> SweepConfiguration(VerifierHostOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var member in typeof(VerifierHostOptions).GetProperties())
        {
            object? value = member.GetValue(options);
            if (value is string or int or long or TimeSpan)
                values["Verifier:" + member.Name] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        if (options.ScenarioLoadOptions is { } limits)
            foreach (var member in typeof(VerifierScenarioLoadOptions).GetProperties())
                values["Verifier:ScenarioLoadOptions:" + member.Name] = Convert.ToString(member.GetValue(limits), CultureInfo.InvariantCulture);
        values["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit;
        values["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        values["Telemetry:ServiceName"] = "Parchwell.Verifier";
        values["Telemetry:ServiceInstanceId"] = options.PodName;
        values["Telemetry:ScenarioId"] = options.ScenarioId;
        values["Telemetry:RunId"] = options.RunId;
        return values;
    }
}
#pragma warning restore CA1707
