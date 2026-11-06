using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Storage;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class B10SweepLineOptionsTests
{
    private static readonly string[] Budgets = ["ProcessLifetime", "CleanupTimeout", "IoTimeout", "TokenTimeout", "MetadataTimeout",
        "WatermarkTimeout", "PollSlice", "BlobIoTimeout", "MaxPartitions", "MaxInputDepth", "MaxOutputBytes", "MaxDiagnosticsPerCheck", "MaxDiagnosticBytes"];
    private static readonly string[] Identities = ["ScenarioId", "ConfigurationId", "RunId", "Phase", "JobName", "SourceCommit", "VerifierCommit",
        "PodName", "VerifierClientId", "NamespaceName", "BlobServiceUri", "VerifierContainer", "LedgerContainer", "TenantId", "WorkloadIdentityClientId", "FederatedTokenFilePath"];
    private static readonly string[] PositiveValues = ["absent", "negative", "zero", "positive"];
    private static readonly string[] MissingValues = ["absent", "empty"];
    public static IEnumerable<object[]> PositiveCases() => Budgets.SelectMany(property => PositiveValues.Select(value => new object[] { property, value }));
    public static IEnumerable<object[]> MissingIdentities() => Identities.SelectMany(property => MissingValues.Select(value => new object[] { property, value }));

    [Theory]
    [InlineData("b10-sweep-line", true)]
    [InlineData("B10-sweep-line", false)]
    [InlineData("b10-sweep-Line", false)]
    [InlineData("b10-sweep-line ", false)]
    [InlineData(" b10-sweep-line", false)]
    [InlineData("b10_sweep_line", false)]
    public void Given_ModeSpelling_When_Parsed_Then_OnlyExactCaptureModeIsAccepted(string text, bool accepted)
    {
        Assert.Equal(accepted, VerifierModeParser.TryParse(text, out VerifierMode mode));
        if (accepted) Assert.Equal(VerifierMode.B10SweepLine, mode);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_B10Phase_When_Validated_Then_CaptureUsesOnlyMetadataDependencies(string phase)
    {
        VerifierHostOptions options = CaptureOptions(phase);
        Assert.True(Validate(options).Succeeded);
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(Configuration(options).Select(pair => $"--{pair.Key}={pair.Value}").ToArray(), services => registrations.AddRange(services));
        Assert.Equal(typeof(B10SweepLineHandler), Assert.Single(registrations, service => service.ServiceType == typeof(IVerifierModeHandler)).ImplementationType);
        Assert.Contains(registrations, service => service.ServiceType == typeof(VerifierKafkaSessionFactory));
        Assert.Contains(registrations, service => service.ServiceType == typeof(IVerifierArtifactStore));
        Assert.DoesNotContain(registrations, service => service.ServiceType == typeof(IProducerArtifactStore) || service.ServiceType == typeof(ILedgerArtifactSource) ||
            service.ServiceType == typeof(ScenarioLoader) || service.ServiceType == typeof(ScenarioResolver) || service.ServiceType == typeof(IScenarioSource) ||
            service.ServiceType == typeof(RegisteredProducerConfigurationResolver));
        VerifierHostOptions actual = host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
        foreach (string property in Budgets.Append("B10SettleIntervalMilliseconds").Append("B10LastVerifierCompletedUtc").Append("B10LastLoadgenTerminatedUtc"))
            Assert.Equal(typeof(VerifierHostOptions).GetProperty(property)!.GetValue(options), typeof(VerifierHostOptions).GetProperty(property)!.GetValue(actual));
        Assert.Equal(TimeSpan.FromSeconds(10), Program.FinalReceiptBudget(actual));
    }

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public void Given_BudgetOrLimit_When_Validated_Then_RequiresExplicitPositiveValue(string property, string value)
    {
        VerifierHostOptions options = CaptureOptions();
        var member = typeof(VerifierHostOptions).GetProperty(property)!;
        Type type = Nullable.GetUnderlyingType(member.PropertyType)!;
        long number = value == "negative" ? -1 : value == "zero" ? 0 : 1;
        member.SetValue(options, value == "absent" ? null : type == typeof(TimeSpan) ? property == "ProcessLifetime" && value == "positive" ? TimeSpan.FromMinutes(2) : TimeSpan.FromTicks(number) : Convert.ChangeType(number, type, CultureInfo.InvariantCulture));
        Assert.Equal(value == "positive", Validate(options).Succeeded);
    }

    [Theory]
    [MemberData(nameof(MissingIdentities))]
    public void Given_MissingIdentity_When_Validated_Then_RejectsBeforeIo(string property, string value)
    {
        VerifierHostOptions options = CaptureOptions();
        typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, value == "absent" ? null : "");
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(59999L, false)]
    [InlineData(60000L, true)]
    [InlineData(60001L, false)]
    [InlineData(2147483647L, false)]
    [InlineData(2147483648L, false)]
    public void Given_SettleInterval_When_Validated_Then_OnlyDeploymentIntervalIsAccepted(long? settle, bool accepted)
    {
        VerifierHostOptions options = CaptureOptions(); options.B10SettleIntervalMilliseconds = settle;
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("Repeat")]
    [InlineData("RepeatPrefix")]
    [InlineData("InstanceId")]
    [InlineData("ExpectedInstanceId")]
    [InlineData("ClientId")]
    [InlineData("ArtifactPrefix")]
    [InlineData("StartLineInputName")]
    [InlineData("StartLineOutputName")]
    [InlineData("BrokerInventoryOutputName")]
    [InlineData("ResultOutputName")]
    [InlineData("CandidateOutputName")]
    public void Given_RepeatOrProducerBinding_When_Validated_Then_RejectsBeforeIo(string property)
    {
        VerifierHostOptions options = CaptureOptions();
        typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, property == "Repeat" ? (object)1 : "synthetic");
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("ScenarioId", "B04")]
    [InlineData("ConfigurationId", "C1")]
    [InlineData("Phase", "C3")]
    [InlineData("Phase", "probe")]
    [InlineData("Phase", "c0")]
    [InlineData("RunId", "../foreign")]
    [InlineData("JobName", "run-1-c0-b10-sweep")]
    [InlineData("PhasePrefix", "run-2/C0/")]
    [InlineData("SourceCommit", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [InlineData("VerifierContainer", "ledger")]
    [InlineData("LedgerContainer", "Bad")]
    public void Given_ForeignCoordinate_When_Validated_Then_RejectsBeforeIo(string property, string value)
    {
        VerifierHostOptions options = CaptureOptions(); typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, value);
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-time")]
    [InlineData("2026-01-01T00:00:00Z")]
    [InlineData("2026-01-01T00:00:00.0000000+01:00")]
    [InlineData("10000-01-01T00:00:00.0000000+00:00")]
    public void Given_MalformedLifecycleTime_When_Validated_Then_RejectsBothFields(string? value)
    {
        VerifierHostOptions options = CaptureOptions(); options.B10LastVerifierCompletedUtc = value; Assert.False(Validate(options).Succeeded);
        options = CaptureOptions(); options.B10LastLoadgenTerminatedUtc = value; Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    [InlineData("judge")]
    [InlineData("b10-sweep")]
    [InlineData("probe-gate")]
    [InlineData("single-action-judge")]
    public void Given_LifecycleSettingOnOtherMode_When_Validated_Then_RejectsBeforeModeSpecificEarlyReturn(string mode)
    {
        foreach (string property in new[] { "B10LastVerifierCompletedUtc", "B10LastLoadgenTerminatedUtc" })
        {
            VerifierHostOptions options = VerifierHostTestOptions.ValidOptions(mode);
            typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, DateTimeOffset.UnixEpoch.ToString("O", CultureInfo.InvariantCulture));
            Assert.Contains(Validate(options).Failures!, failure => failure.Contains("only accepted", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    public void Given_ProcessBudgetSum_When_Validated_Then_RequiresStrictlyGreaterLifetime(long ticks, bool accepted)
    {
        VerifierHostOptions options = CaptureOptions(); options.ProcessLifetime = TimeSpan.FromSeconds(15) + TimeSpan.FromTicks(ticks);
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(-1L, true)]
    [InlineData(0L, true)]
    [InlineData(1L, false)]
    public void Given_TimeoutRepresentationBoundary_When_Validated_Then_RejectsOverflow(long ticks, bool accepted)
    {
        VerifierHostOptions options = CaptureOptions(); options.PollSlice = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromTicks(ticks);
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    internal static VerifierHostOptions CaptureOptions(string phase = "C0")
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("b10-sweep-line");
        options.ScenarioId = "B10"; options.Phase = phase; options.ConfigurationId = phase;
        options.JobName = PhaseIdentity.B10SweepLineJobName(options.RunId!, phase);
        options.PhasePrefix = PhaseIdentity.PhasePrefix(options.RunId!, phase);
        options.Repeat = null; options.RepeatPrefix = null; options.InstanceId = null; options.ExpectedInstanceId = null; options.ClientId = null;
        options.ArtifactPrefix = null; options.StartLineInputName = null; options.StartLineOutputName = null; options.BrokerInventoryOutputName = null; options.ResultOutputName = null;
        options.ScenarioRoot = null; options.ScenarioLoadOptions = null; options.MaxArtifactBytes = null;
        options.ProcessLifetime = TimeSpan.FromMinutes(2); options.IoTimeout = TimeSpan.FromSeconds(10);
        options.B10SettleIntervalMilliseconds = 60000;
        options.B10LastVerifierCompletedUtc = DateTimeOffset.UnixEpoch.ToString("O", CultureInfo.InvariantCulture);
        options.B10LastLoadgenTerminatedUtc = DateTimeOffset.UnixEpoch.AddSeconds(-60).ToString("O", CultureInfo.InvariantCulture);
        return options;
    }

    internal static Dictionary<string, string?> Configuration(VerifierHostOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var member in typeof(VerifierHostOptions).GetProperties())
            if (member.GetValue(options) is string or int or long or TimeSpan)
                values["Verifier:" + member.Name] = Convert.ToString(member.GetValue(options), CultureInfo.InvariantCulture);
        values["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit;
        values["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        values["Telemetry:ServiceName"] = "Parchwell.Verifier"; values["Telemetry:ServiceInstanceId"] = options.PodName;
        values["Telemetry:ScenarioId"] = options.ScenarioId; values["Telemetry:RunId"] = options.RunId;
        return values;
    }

    private static ValidateOptionsResult Validate(VerifierHostOptions options) => new VerifierHostOptionsValidator().Validate(null, options);
}
#pragma warning restore CA1707
