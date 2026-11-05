using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Storage;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10SweepOptionsTests
{
    private static readonly string[] RequiredPositiveOptions =
    [
        "ProcessLifetime", "CleanupTimeout", "IoTimeout", "TokenTimeout", "MetadataTimeout", "WatermarkTimeout",
        "PollSlice", "BlobIoTimeout", "SafetyStop", "MaxConsumedRecords", "MaxConsumedBytes", "MaxArtifactBytes",
        "MaxTotalInputBytes", "MaxRowBytes", "MaxRowsPerArtifact", "MaxInputEntries", "MaxPartitions", "MaxInputDepth",
        "MaxOutputBytes", "MaxDiagnosticsPerCheck", "MaxDiagnosticBytes"
    ];

    private static readonly string[] PositiveOptionValues = ["absent", "negative", "zero", "valid"];
    private static readonly string[] MissingIdentityValues = ["absent", "empty"];
    private static readonly string[] RequiredIdentityProperties =
    [
        "ScenarioId", "ConfigurationId", "RunId", "Phase", "JobName", "VerifierCommit", "SourceCommit",
        "PodName", "VerifierClientId", "NamespaceName", "BlobServiceUri", "VerifierContainer", "LedgerContainer",
        "TenantId", "WorkloadIdentityClientId", "FederatedTokenFilePath"
    ];

    public static IEnumerable<object[]> PositiveOptions() => RequiredPositiveOptions.SelectMany(property =>
        PositiveOptionValues.Select(value => new object[] { property, value }));

    public static IEnumerable<object[]> RequiredIdentityOptions() => RequiredIdentityProperties.SelectMany(property =>
        MissingIdentityValues.Select(value => new object[] { property, value }));

    [Theory]
    [InlineData("b10-sweep", true)]
    [InlineData("B10-sweep", false)]
    [InlineData("b10-Sweep", false)]
    [InlineData("b10-sweep ", false)]
    [InlineData(" b10-sweep", false)]
    [InlineData("b10_sweep", false)]
    public void Given_SweepModeSpelling_When_Parsed_Then_OnlyExactNameIsAccepted(string value, bool accepted)
    {
        Assert.Equal(accepted, VerifierModeParser.TryParse(value, out VerifierMode mode));
        if (accepted) Assert.Equal(VerifierMode.B10Sweep, mode);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_B10PhaseSweep_When_Validated_Then_ScenarioLoadingAndRepeatFieldsAreUnnecessary(string phase)
    {
        VerifierHostOptions options = SweepOptions(phase);
        Assert.Null(options.ScenarioRoot);
        Assert.Null(options.ScenarioLoadOptions);
        Assert.True(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("C0", "C1")]
    [InlineData("C1", "C0")]
    [InlineData("probe", "C0")]
    [InlineData("C3", "C3")]
    [InlineData("c0", "C0")]
    public void Given_MismatchedPhaseConfiguration_When_SweepValidated_Then_RejectsBeforeIo(string phase, string configuration)
    {
        VerifierHostOptions options = SweepOptions();
        options.Phase = phase;
        options.ConfigurationId = configuration;
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("A01")]
    [InlineData("B04")]
    [InlineData("B01")]
    [InlineData("b10")]
    [InlineData(null)]
    public void Given_NonB10Scenario_When_SweepValidated_Then_RejectsBeforeIo(string? scenario)
    {
        VerifierHostOptions options = SweepOptions();
        options.ScenarioId = scenario;
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [MemberData(nameof(PositiveOptions))]
    public void Given_SweepBudgetOrLimit_When_Validated_Then_RequiresExplicitPositiveValue(string property, string value)
    {
        VerifierHostOptions options = SweepOptions();
        var member = typeof(VerifierHostOptions).GetProperty(property)!;
        Type type = Nullable.GetUnderlyingType(member.PropertyType)!;
        long number = value == "negative" ? -1 : value == "zero" ? 0 : 1;
        object? configured = value == "absent" ? null : type == typeof(TimeSpan)
            ? property == "ProcessLifetime" && value == "valid" ? TimeSpan.FromSeconds(10) : TimeSpan.FromTicks(number)
            : Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
        member.SetValue(options, configured);
        Assert.Equal(value == "valid", Validate(options).Succeeded);
    }

    [Theory]
    [MemberData(nameof(RequiredIdentityOptions))]
    public void Given_MissingSweepIdentityOrStoreInput_When_Validated_Then_RejectsBeforeIo(string property, string value)
    {
        VerifierHostOptions options = SweepOptions();
        typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, value == "absent" ? null : "");
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2147483647L)]
    [InlineData(2147483648L)]
    public void Given_SettleInterval_When_SweepAndB10JudgeValidated_Then_BoundsAreIdentical(long? interval)
    {
        bool accepted = interval is > 0 and <= int.MaxValue;
        VerifierHostOptions sweep = SweepOptions();
        sweep.B10SettleIntervalMilliseconds = interval;
        Assert.Equal(accepted, Validate(sweep).Succeeded);
        VerifierHostOptions judge = JudgeOptions();
        judge.B10SettleIntervalMilliseconds = interval;
        Assert.Equal(accepted, Validate(judge).Succeeded);
    }

    [Theory]
    [InlineData("Repeat")]
    [InlineData("RepeatPrefix")]
    [InlineData("ExpectedInstanceId")]
    [InlineData("InstanceId")]
    [InlineData("ArtifactPrefix")]
    [InlineData("StartLineInputName")]
    [InlineData("ResultOutputName")]
    public void Given_RepeatBinding_When_SweepValidated_Then_RejectsIt(string property)
    {
        VerifierHostOptions options = SweepOptions();
        typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, property == "Repeat" ? (object)1 : "synthetic");
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("run-1-c0-b10-sweep", true)]
    [InlineData("run-1-c0-judge", false)]
    [InlineData("run-1-c1-b10-sweep", false)]
    [InlineData("run-1-c0-r1-verify", false)]
    [InlineData(null, false)]
    public void Given_JobName_When_SweepValidated_Then_RequiresDerivedPhaseJob(string? job, bool accepted)
    {
        VerifierHostOptions options = SweepOptions();
        options.JobName = job;
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    public void Given_ProcessLifetimeAroundRequiredBudgetSum_When_SweepValidated_Then_RequiresStrictlyGreater(long ticks, bool accepted)
    {
        VerifierHostOptions options = SweepOptions();
        options.ProcessLifetime = TimeSpan.FromSeconds(8) + TimeSpan.FromTicks(ticks);
        options.B10SettleIntervalMilliseconds = int.MaxValue;
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(-1L, true)]
    [InlineData(0L, true)]
    [InlineData(1L, false)]
    public void Given_TimeoutRepresentationBoundary_When_SweepValidated_Then_RejectsOverflow(long ticks, bool accepted)
    {
        VerifierHostOptions options = SweepOptions();
        options.PollSlice = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromTicks(ticks);
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Fact]
    public void Given_SweepConfiguration_When_Bound_Then_PreservesAllActiveBudgetsAndRecordLimits()
    {
        VerifierHostOptions expected = SweepOptions();
        var actual = new VerifierHostOptions();
        new VerifierHostOptionsSetup(new ConfigurationBuilder().AddInMemoryCollection(Configuration(expected)).Build()).Configure(actual);
        foreach (string property in RequiredPositiveOptions.Append("B10SettleIntervalMilliseconds"))
        {
            var member = typeof(VerifierHostOptions).GetProperty(property)!;
            Assert.Equal(member.GetValue(expected), member.GetValue(actual));
        }
        Assert.True(Validate(actual).Succeeded);
    }

    [Theory]
    [InlineData("b10-sweep")]
    [InlineData("judge")]
    public void Given_B10PhaseMode_When_Composed_Then_RegistersItsOwnDependencies(string mode)
    {
        VerifierHostOptions options = mode == "judge" ? JudgeOptions() : SweepOptions();
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(Configuration(options).Select(pair => $"--{pair.Key}={pair.Value}").ToArray(),
            services => registrations.AddRange(services));
        Assert.Contains(registrations, service => service.ServiceType == typeof(IVerifierArtifactStore));
        Assert.Contains(registrations, service => service.ServiceType == typeof(IProducerArtifactStore));
        ServiceDescriptor handler = Assert.Single(registrations, service => service.ServiceType == typeof(IVerifierModeHandler));
        Assert.Equal(mode == "judge" ? typeof(JudgeHandler) : typeof(B10SweepHandler), handler.ImplementationType);
        Assert.DoesNotContain(registrations, service => service.ServiceType == typeof(ILedgerArtifactSource) ||
            service.ServiceType == typeof(RegisteredProducerConfigurationResolver));
        if (mode == "b10-sweep")
        {
            Assert.Contains(registrations, service => service.ServiceType == typeof(VerifierKafkaSessionFactory));
            Assert.Contains(registrations, service => service.ServiceType == typeof(KafkaAuthenticationFactory));
            Assert.DoesNotContain(registrations, service => service.ServiceType == typeof(IScenarioSource) ||
                service.ServiceType == typeof(ScenarioLoader) || service.ServiceType == typeof(ScenarioResolver));
        }
        else
        {
            Assert.DoesNotContain(registrations, service => service.ServiceType == typeof(VerifierKafkaSessionFactory) ||
                service.ServiceType == typeof(KafkaAuthenticationFactory));
        }
        Assert.Equal(1000L, host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value.B10SettleIntervalMilliseconds);
    }

    private static ValidateOptionsResult Validate(VerifierHostOptions options) => new VerifierHostOptionsValidator().Validate(null, options);

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

    private static VerifierHostOptions JudgeOptions()
    {
        VerifierHostOptions options = SweepOptions();
        options.Mode = "judge";
        options.JobName = PhaseIdentity.JudgeJobName(options.RunId!, options.Phase!);
        options.CandidateOutputName = PhasePaths.Candidate(options.RunId!, options.Phase!);
        options.LoadGenImage = "synthetic/loadgen@sha256:" + new string('a', 64);
        options.VerifierImage = "synthetic/verifier@sha256:" + new string('b', 64);
        options.ScenarioLoadOptions = VerifierHostTestOptions.ValidOptions("verify").ScenarioLoadOptions;
        options.VerifierClientId = null;
        options.NamespaceName = null;
        options.TokenTimeout = null;
        options.MetadataTimeout = null;
        options.WatermarkTimeout = null;
        options.PollSlice = null;
        options.BlobIoTimeout = null;
        options.SafetyStop = null;
        options.MaxConsumedRecords = null;
        options.MaxConsumedBytes = null;
        return options;
    }

    private static Dictionary<string, string?> Configuration(VerifierHostOptions options)
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
