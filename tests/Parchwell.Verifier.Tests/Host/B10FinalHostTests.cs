using System.Globalization;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.B10Final;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10FinalHostTests
{
    private static readonly string[] RequiredPositiveOptions =
    [
        "ProcessLifetime", "CleanupTimeout", "IoTimeout", "TokenTimeout", "MetadataTimeout", "WatermarkTimeout",
        "PollSlice", "BlobIoTimeout", "SafetyStop", "MaxConsumedRecords", "MaxConsumedBytes", "MaxArtifactBytes",
        "MaxTotalInputBytes", "MaxRowBytes", "MaxRowsPerArtifact", "MaxInputEntries", "MaxPartitions", "MaxInputDepth",
        "MaxOutputBytes", "MaxDiagnosticsPerCheck", "MaxDiagnosticBytes"
    ];

    public static IEnumerable<object?[]> PositiveOptions() => RequiredPositiveOptions.SelectMany(property =>
        new long?[] { null, -1, 0, 1, 2 }.Select(value => new object?[] { property, value }));

    [Theory]
    [InlineData("b10-final-sweep", true)]
    [InlineData("B10-final-sweep", false)]
    [InlineData("b10-final-Sweep", false)]
    [InlineData("b10-final-sweep ", false)]
    [InlineData(" b10-final-sweep", false)]
    [InlineData("b10_final_sweep", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Given_ModeSpelling_When_Parsed_Then_OnlyExactFinalSweepIsAccepted(string? value, bool accepted)
    {
        Assert.Equal(accepted, VerifierModeParser.TryParse(value, out VerifierMode mode));
        if (accepted) Assert.Equal(VerifierMode.B10FinalSweep, mode);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_ScientificPhase_When_HostBuilt_Then_ResolvesFinalSweepWithoutScenarioSettings(string phase)
    {
        VerifierHostOptions options = Options(phase);
        Assert.Null(options.ScenarioRoot);
        Assert.Null(options.ScenarioLoadOptions);
        using IHost host = Program.BuildHost(Arguments(options));
        var handler = Assert.IsType<B10FinalSweepHandler>(host.Services.GetRequiredService<IVerifierModeHandler>());
        Assert.Equal(VerifierMode.B10FinalSweep, handler.Mode);
        Assert.NotNull(host.Services.GetRequiredService<ScenarioLoader>());
        ScenarioLoadOptions limits = host.Services.GetRequiredService<IOptions<ScenarioLoadOptions>>().Value;
        Assert.Equal(options.MaxArtifactBytes, limits.MaxDocumentCharacters);
        Assert.Equal(options.MaxInputDepth, limits.MaxNestingDepth);
        Assert.Equal(options.MaxRowBytes, limits.MaxScalarCharacters);
        Assert.Equal(options.MaxInputEntries, limits.MaxParserEvents);
    }

    [Fact]
    public void Given_LargeExistingInputBudgets_When_HostBuilt_Then_ScenarioIntegerLimitsAreCapped()
    {
        VerifierHostOptions options = Options();
        options.MaxArtifactBytes = (long)int.MaxValue + 1;
        options.MaxInputEntries = long.MaxValue;
        using IHost host = Program.BuildHost(Arguments(options));
        ScenarioLoadOptions limits = host.Services.GetRequiredService<IOptions<ScenarioLoadOptions>>().Value;
        Assert.Equal(int.MaxValue, limits.MaxDocumentCharacters);
        Assert.Equal(int.MaxValue, limits.MaxParserEvents);
    }

    [Theory]
    [MemberData(nameof(PositiveOptions))]
    public void Given_RequiredBudgetOrLimit_When_Validated_Then_RequiresExplicitPositiveValue(string property, long? value)
    {
        VerifierHostOptions options = Options();
        var member = typeof(VerifierHostOptions).GetProperty(property)!;
        Type type = Nullable.GetUnderlyingType(member.PropertyType)!;
        object? configured = value is null ? null : type == typeof(TimeSpan)
            ? property == "ProcessLifetime" && value > 0 ? TimeSpan.FromMinutes(1) : TimeSpan.FromTicks(value.Value)
            : Convert.ChangeType(value.Value, type, CultureInfo.InvariantCulture);
        member.SetValue(options, configured);
        Assert.Equal(value > 0, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(59999L)]
    [InlineData(60000L)]
    [InlineData(60001L)]
    public void Given_SettleInterval_When_Validated_Then_RequiresExactlySixtySeconds(long? interval)
    {
        VerifierHostOptions options = Options();
        options.B10SettleIntervalMilliseconds = interval;
        Assert.Equal(interval == 60000, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("b10-0-0", true)]
    [InlineData("b10-1-1", true)]
    [InlineData("b10-01-02", true)]
    [InlineData("b10d-1-1", false)]
    [InlineData("run-1", false)]
    [InlineData("b10-1", false)]
    [InlineData("b10-1-1-x", false)]
    [InlineData("b10-1-1\n", false)]
    [InlineData("B10-1-1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Given_RunIdentity_When_Validated_Then_RequiresScientificPattern(string? run, bool accepted)
    {
        VerifierHostOptions options = Options();
        options.RunId = run;
        options.JobName = run is null ? null : run + "-c0-b10-final-sweep";
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("A01", "C0", "C0")]
    [InlineData("B04", "C0", "C0")]
    [InlineData("b10", "C0", "C0")]
    [InlineData(null, "C0", "C0")]
    [InlineData("B10", "probe", "C0")]
    [InlineData("B10", "C3", "C3")]
    [InlineData("B10", "C0", "C1")]
    [InlineData("B10", "C1", "C0")]
    [InlineData("B10", null, "C0")]
    public void Given_UnsupportedScenarioOrPhase_When_Validated_Then_Rejects(string? scenario, string? phase, string configuration)
    {
        VerifierHostOptions options = Options();
        options.ScenarioId = scenario;
        options.Phase = phase;
        options.ConfigurationId = configuration;
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("Repeat")]
    [InlineData("RepeatPrefix")]
    [InlineData("ExpectedInstanceId")]
    [InlineData("InstanceId")]
    [InlineData("ArtifactPrefix")]
    [InlineData("StartLineInputName")]
    [InlineData("ResultOutputName")]
    public void Given_RepeatBinding_When_Validated_Then_Rejects(string property)
    {
        VerifierHostOptions options = Options();
        typeof(VerifierHostOptions).GetProperty(property)!.SetValue(options, property == "Repeat" ? (object)1 : "synthetic");
        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("b10-1-1-c0-b10-final-sweep", true)]
    [InlineData("b10-1-1-c1-b10-final-sweep", false)]
    [InlineData("b10-1-1-c0-b10-sweep", false)]
    [InlineData("b10-1-1-c0-r1-verify", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Given_JobName_When_Validated_Then_RequiresDerivedFinalSweepJob(string? job, bool accepted)
    {
        VerifierHostOptions options = Options();
        options.JobName = job;
        Assert.Equal(accepted, Validate(options).Succeeded);
    }

    [Fact]
    public void Given_FinalSweepConfiguration_When_Bound_Then_PreservesEveryActiveBudget()
    {
        VerifierHostOptions expected = Options();
        var actual = new VerifierHostOptions();
        new VerifierHostOptionsSetup(new ConfigurationBuilder().AddCommandLine(Arguments(expected)).Build()).Configure(actual);
        foreach (string property in RequiredPositiveOptions.Append("B10SettleIntervalMilliseconds"))
        {
            var member = typeof(VerifierHostOptions).GetProperty(property)!;
            Assert.Equal(member.GetValue(expected), member.GetValue(actual));
        }
        Assert.True(Validate(actual).Succeeded);
    }

    [Fact]
    public void Given_FinalSweepHost_When_TokenProviderResolved_Then_SharedProviderKeepsPinnedRetryAndIdentity()
    {
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(Arguments(Options()), services => registrations.AddRange(services));
        Assert.Single(registrations, service => service.ServiceType == typeof(IKafkaTokenProvider));
        var provider = Assert.IsType<VerifierKafkaTokenProvider>(host.Services.GetRequiredService<IKafkaTokenProvider>());
        Assert.Equal("Parchwell.Verifier.Common.Kafka", provider.GetType().Namespace);
        var options = provider.CredentialOptions;
        Assert.Equal(TimeSpan.FromSeconds(2), options.Retry.NetworkTimeout);
        Assert.Equal(1, options.Retry.MaxRetries);
        Assert.Equal(RetryMode.Fixed, options.Retry.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.Retry.Delay);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Retry.MaxDelay);
        Assert.Null(options.RetryPolicy);
#pragma warning disable AZID0002 // Mirrors the existing sign-in provider contract test.
        Assert.False(options.IsAzureProxyEnabled);
#pragma warning restore AZID0002
        Assert.Equal("tenant-id", options.TenantId);
        Assert.Equal("workload-client-id", options.ClientId);
        Assert.Equal("/tmp/token", options.TokenFilePath);
        Assert.Equal(TimeSpan.FromSeconds(10), host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value.TokenTimeout);
    }

    [Fact]
    public async Task Given_TrialRun_When_RealHostRuns_Then_ExitsOneBeforeAnyEvidenceRead()
    {
        VerifierHostOptions options = Options();
        options.RunId = "b10d-1-1";
        options.JobName = "b10d-1-1-c0-b10-final-sweep";
        var store = new CountingStore();
        int exit = await Program.RunAsync(Arguments(options), services =>
        {
            services.RemoveAll<IVerifierArtifactStore>();
            services.RemoveAll<IProducerArtifactStore>();
            services.AddSingleton<IVerifierArtifactStore>(store);
            services.AddSingleton<IProducerArtifactStore>(store);
        });
        Assert.Equal(1, exit);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task Given_FinalSweepMode_When_RealHostRuns_Then_DispatchesSelectedHandler()
    {
        int calls = 0;
        int exit = await Program.RunAsync(Arguments(Options()), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new CountingHandler(() => calls++));
        });
        Assert.Equal(0, exit);
        Assert.Equal(1, calls);
    }

    private static ValidateOptionsResult Validate(VerifierHostOptions options) => new VerifierHostOptionsValidator().Validate(null, options);

    private static VerifierHostOptions Options(string phase = "C0")
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("b10-final-sweep");
        options.ScenarioId = "B10";
        options.RunId = "b10-1-1";
        options.Phase = phase;
        options.ConfigurationId = phase;
        options.PhasePrefix = PhaseIdentity.PhasePrefix(options.RunId, phase);
        options.JobName = PhaseIdentity.B10FinalSweepJobName(options.RunId, phase);
        options.Repeat = null;
        options.RepeatPrefix = null;
        options.ExpectedInstanceId = null;
        options.InstanceId = null;
        options.ArtifactPrefix = null;
        options.StartLineInputName = null;
        options.ResultOutputName = null;
        options.ScenarioRoot = null;
        options.ScenarioLoadOptions = null;
        options.TokenTimeout = TimeSpan.FromSeconds(10);
        options.ProcessLifetime = TimeSpan.FromMinutes(1);
        options.B10SettleIntervalMilliseconds = 60000;
        return options;
    }

    private static string[] Arguments(VerifierHostOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var member in typeof(VerifierHostOptions).GetProperties())
        {
            object? value = member.GetValue(options);
            if (value is string or int or long or TimeSpan)
                values["Verifier:" + member.Name] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        values["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit;
        values["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        values["Telemetry:ServiceName"] = "Parchwell.Verifier";
        values["Telemetry:ServiceInstanceId"] = options.PodName;
        values["Telemetry:ScenarioId"] = options.ScenarioId;
        values["Telemetry:RunId"] = options.RunId;
        return values.Select(pair => $"--{pair.Key}={pair.Value}").ToArray();
    }

    private sealed class CountingHandler(Action called) : IVerifierModeHandler
    {
        public VerifierMode Mode => VerifierMode.B10FinalSweep;
        public Task<int> ExecuteAsync(CancellationToken cancellationToken) { called(); return Task.FromResult(0); }
    }

    private sealed class CountingStore : IVerifierArtifactStore, IProducerArtifactStore
    {
        internal int Reads { get; private set; }
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) { Reads++; return Task.FromResult<byte[]?>(null); }
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) { Reads++; return Task.FromResult(new BlobListing([], true)); }
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected write.");
    }
}
#pragma warning restore CA1707
