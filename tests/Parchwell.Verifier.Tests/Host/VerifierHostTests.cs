using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class VerifierHostTests
{
    private static readonly string[] AllModes = ["start-line", "verify", "probe-gate"];
    private static readonly string[] SharedRequiredOptions =
    [
        "ScenarioId", "VerifierCommit", "ProcessLifetime", "CleanupTimeout", "MaxOutputBytes",
        "MaxDiagnosticsPerCheck", "MaxDiagnosticBytes"
    ];
    private static readonly string[] DuplicateRepeatPaths = ["same.json", "same.json"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("VERIFY")]
    [InlineData("other")]
    public async Task Given_MissingOrUnknownMode_When_HostStarts_Then_ExitsNonzeroBeforeIo(string? mode)
    {
        string outputPath = TempPath();
        var handlerCalls = 0;
        int exitCode = await Program.RunAsync(
            mode is null ? [] : [$"--Verifier:Mode={mode}"],
            services =>
            {
                services.RemoveAll<IVerifierModeHandler>();
                services.AddSingleton<IVerifierModeHandler>(new CountingHandler(() => handlerCalls++));
            });

        Assert.NotEqual(0, exitCode);
        Assert.Equal(0, handlerCalls);
        Assert.False(File.Exists(outputPath));
    }

    public static IEnumerable<object[]> RequiredOptions =>
        RequiredOptionsFor("probe-gate",
            "GateOutputPath", "ScenarioRoot", "ScenarioLoadOptions", "ExpectedRepeats", "RepeatResultPaths", "IoTimeout",
            "ScenarioLoadOptions.MaxDocumentCharacters", "ScenarioLoadOptions.MaxNestingDepth",
            "ScenarioLoadOptions.MaxScalarCharacters", "ScenarioLoadOptions.MaxParserEvents",
            "MaxArtifactBytes", "MaxTotalInputBytes", "MaxInputDepth")
        .Concat(RequiredOptionsFor("probe-gate",
            "ExpectedRepeats.0.ScenarioId", "ExpectedRepeats.0.ConfigurationId", "ExpectedRepeats.0.RunId",
            "ExpectedRepeats.0.InstanceId", "ExpectedRepeats.0.ExpectedInstanceId", "ExpectedRepeats.0.ClientId"))
        .Concat(RequiredOptionsFor("start-line",
            "ConfigurationId", "RunId", "InstanceId", "ExpectedInstanceId", "ClientId", "VerifierClientId",
            "NamespaceName", "BlobServiceUri", "VerifierContainer", "TokenTimeout", "MetadataTimeout",
            "WatermarkTimeout", "PollSlice", "BlobIoTimeout", "TenantId", "WorkloadIdentityClientId",
            "FederatedTokenFilePath", "StartLineOutputName", "MaxPartitions", "MaxInputDepth"))
        .Concat(RequiredOptionsFor("verify",
            "ConfigurationId", "RunId", "InstanceId", "ExpectedInstanceId", "ClientId", "VerifierClientId",
            "NamespaceName", "BlobServiceUri", "VerifierContainer", "TokenTimeout", "MetadataTimeout",
            "WatermarkTimeout", "PollSlice", "IoTimeout", "BlobIoTimeout", "TenantId",
            "WorkloadIdentityClientId", "FederatedTokenFilePath", "LedgerContainer", "ArtifactPrefix",
            "StartLineInputName", "ResultOutputName", "ScenarioRoot", "ExpectedBrokerNames", "SafetyStop",
            "MaxArtifactBytes", "MaxTotalInputBytes", "MaxRowBytes", "MaxRowsPerArtifact",
            "MaxInventoryEntries", "MaxPartitions", "MaxInputDepth", "MaxNativeReportBytes",
            "MaxBrokerEntries", "MaxConsumedRecords", "MaxConsumedBytes", "ScenarioLoadOptions"))
        .Concat(RequiredOptionsFor("verify",
            "ScenarioLoadOptions.MaxDocumentCharacters", "ScenarioLoadOptions.MaxNestingDepth",
            "ScenarioLoadOptions.MaxScalarCharacters", "ScenarioLoadOptions.MaxParserEvents"))
        .Concat(SharedRequiredOptions.SelectMany(property =>
            AllModes.Select(mode => new object[] { mode, property })));

    [Fact]
    public void Given_LiteralScenarioLoadOptionsConfigurationKeys_When_Bound_Then_UsesAllParserLimits()
    {
        string[] args = ArgumentsFor("probe-gate", TempPath())
            .Concat(
            [
                "--Verifier:ScenarioLoadOptions:MaxDocumentCharacters=101",
                "--Verifier:ScenarioLoadOptions:MaxNestingDepth=102",
                "--Verifier:ScenarioLoadOptions:MaxScalarCharacters=103",
                "--Verifier:ScenarioLoadOptions:MaxParserEvents=104"
            ])
            .ToArray();

        using IHost host = Program.BuildHost(args);
        VerifierHostOptions options = host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;

        Assert.Equal(101, options.ScenarioLoadOptions!.MaxDocumentCharacters);
        Assert.Equal(102, options.ScenarioLoadOptions.MaxNestingDepth);
        Assert.Equal(103, options.ScenarioLoadOptions.MaxScalarCharacters);
        Assert.Equal(104, options.ScenarioLoadOptions.MaxParserEvents);
    }

    [Theory]
    [MemberData(nameof(RequiredOptions))]
    public void Given_MissingRequiredOption_When_Validated_Then_ReportsStartupFailure(string mode, string propertyName)
    {
        VerifierHostOptions options = ValidOptions(mode);
        SetOptionNull(options, propertyName);

        var result = new VerifierHostOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Failures!);
    }

    [Theory]
    [MemberData(nameof(RequiredTextOptions))]
    public void Given_BlankRequiredTextOption_When_Validated_Then_ReportsStartupFailure(string mode, string propertyName)
    {
        VerifierHostOptions options = ValidOptions(mode);
        SetOptionValue(options, propertyName, " \t ");

        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    public void Given_RelativeBlobServiceUri_When_Validated_Then_ReportsStartupFailure(string mode)
    {
        VerifierHostOptions options = ValidOptions(mode);
        options.BlobServiceUri = "storage.example/container";

        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    public static IEnumerable<object[]> ValidContainerNames =>
        new[] { "abc", "a0z", "0-ab-9", new string('a', 63), "$root", "$logs", "$web" }
            .SelectMany(containerName => new[]
            {
                new object[] { "start-line", "VerifierContainer", containerName },
                new object[] { "verify", "VerifierContainer", containerName },
                new object[] { "verify", "LedgerContainer", containerName }
            });

    [Theory]
    [MemberData(nameof(ValidContainerNames))]
    public void Given_ValidBlobContainerName_When_Validated_Then_AllowsIt(
        string mode,
        string propertyName,
        string containerName)
    {
        VerifierHostOptions options = ValidOptions(mode);
        SetOptionValue(options, propertyName, containerName);

        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    public static IEnumerable<object[]> InvalidContainerNames =>
        new[] { "ab", "Aaa", "-abc", "abc-", "a--b", "$container", new string('a', 64), "ébc" }
            .SelectMany(containerName => new[]
            {
                new object[] { "start-line", "VerifierContainer", containerName },
                new object[] { "verify", "VerifierContainer", containerName },
                new object[] { "verify", "LedgerContainer", containerName }
            });

    [Theory]
    [MemberData(nameof(InvalidContainerNames))]
    public void Given_InvalidBlobContainerName_When_Validated_Then_ReportsStartupFailure(
        string mode,
        string propertyName,
        string containerName)
    {
        VerifierHostOptions options = ValidOptions(mode);
        SetOptionValue(options, propertyName, containerName);

        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    public static IEnumerable<object[]> RequiredBudgets =>
        new[]
        {
            ("probe-gate", "CleanupTimeout"), ("probe-gate", "ProcessLifetime"), ("probe-gate", "IoTimeout"),
            ("start-line", "CleanupTimeout"), ("start-line", "ProcessLifetime"), ("start-line", "TokenTimeout"),
            ("start-line", "MetadataTimeout"), ("start-line", "WatermarkTimeout"), ("start-line", "PollSlice"),
            ("start-line", "BlobIoTimeout"), ("verify", "CleanupTimeout"), ("verify", "ProcessLifetime"),
            ("verify", "TokenTimeout"), ("verify", "MetadataTimeout"), ("verify", "WatermarkTimeout"),
            ("verify", "PollSlice"), ("verify", "IoTimeout"), ("verify", "BlobIoTimeout"), ("verify", "SafetyStop")
        }.SelectMany(item => new[] { 0, -1 }.Select(value => new object[] { item.Item1, item.Item2, value }));

    public static IEnumerable<object[]> RequiredTextOptions =>
        RequiredOptionsFor("probe-gate", "ScenarioId", "VerifierCommit", "GateOutputPath", "ScenarioRoot",
            "ExpectedRepeats.0.ScenarioId", "ExpectedRepeats.0.ConfigurationId", "ExpectedRepeats.0.RunId",
            "ExpectedRepeats.0.InstanceId", "ExpectedRepeats.0.ExpectedInstanceId", "ExpectedRepeats.0.ClientId")
        .Concat(RequiredOptionsFor("start-line", "ScenarioId", "VerifierCommit", "ConfigurationId", "RunId",
            "InstanceId", "ExpectedInstanceId", "ClientId", "VerifierClientId", "NamespaceName",
            "VerifierContainer", "TenantId", "WorkloadIdentityClientId", "FederatedTokenFilePath",
            "StartLineOutputName"))
        .Concat(RequiredOptionsFor("verify", "ScenarioId", "VerifierCommit", "ConfigurationId", "RunId",
            "InstanceId", "ExpectedInstanceId", "ClientId", "VerifierClientId", "NamespaceName",
            "VerifierContainer", "TenantId", "WorkloadIdentityClientId", "FederatedTokenFilePath",
            "LedgerContainer", "ArtifactPrefix", "StartLineInputName", "ResultOutputName", "ScenarioRoot"));

    public static IEnumerable<object[]> RequiredLimits =>
        new[]
        {
            ("probe-gate", "MaxOutputBytes"), ("probe-gate", "MaxDiagnosticsPerCheck"),
            ("probe-gate", "MaxDiagnosticBytes"), ("probe-gate", "MaxArtifactBytes"),
            ("probe-gate", "MaxTotalInputBytes"), ("probe-gate", "MaxInputDepth"),
            ("probe-gate", "ScenarioLoadOptions.MaxDocumentCharacters"),
            ("probe-gate", "ScenarioLoadOptions.MaxNestingDepth"),
            ("probe-gate", "ScenarioLoadOptions.MaxScalarCharacters"),
            ("probe-gate", "ScenarioLoadOptions.MaxParserEvents"),
            ("start-line", "MaxOutputBytes"), ("start-line", "MaxDiagnosticsPerCheck"),
            ("start-line", "MaxDiagnosticBytes"), ("start-line", "MaxPartitions"),
            ("start-line", "MaxInputDepth"),
            ("verify", "MaxOutputBytes"), ("verify", "MaxDiagnosticsPerCheck"),
            ("verify", "MaxDiagnosticBytes"), ("verify", "MaxArtifactBytes"),
            ("verify", "MaxTotalInputBytes"), ("verify", "MaxRowBytes"),
            ("verify", "MaxRowsPerArtifact"), ("verify", "MaxInventoryEntries"),
            ("verify", "MaxPartitions"), ("verify", "MaxInputDepth"), ("verify", "MaxNativeReportBytes"),
            ("verify", "MaxBrokerEntries"), ("verify", "MaxConsumedRecords"), ("verify", "MaxConsumedBytes"),
            ("verify", "ScenarioLoadOptions.MaxDocumentCharacters"),
            ("verify", "ScenarioLoadOptions.MaxNestingDepth"),
            ("verify", "ScenarioLoadOptions.MaxScalarCharacters"),
            ("verify", "ScenarioLoadOptions.MaxParserEvents")
        }.SelectMany(item => new[] { 0, -1 }.Select(value => new object[] { item.Item1, item.Item2, value }));

    [Theory]
    [MemberData(nameof(RequiredBudgets))]
    public void Given_NonpositiveRequiredBudget_When_Validated_Then_ReportsStartupFailure(
        string mode,
        string propertyName,
        int milliseconds)
    {
        VerifierHostOptions options = ValidOptions(mode);
        typeof(VerifierHostOptions).GetProperty(propertyName)!.SetValue(options, TimeSpan.FromMilliseconds(milliseconds));

        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [MemberData(nameof(RequiredLimits))]
    public void Given_NonpositiveRequiredLimit_When_Validated_Then_ReportsStartupFailure(
        string mode,
        string propertyName,
        int value)
    {
        VerifierHostOptions options = ValidOptions(mode);
        SetLimit(options, propertyName, value);

        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public async Task Given_MalformedDuration_When_Bound_Then_ReportsStartupFailure()
    {
        int exitCode = await Program.RunAsync(
        [
            "--Verifier:Mode=probe-gate",
            "--Verifier:IoTimeout=not-a-duration"
        ]);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Given_ProcessLifetimeAtBudgetSum_When_Validated_Then_RejectsEqualityAndAcceptsGreaterValue()
    {
        VerifierHostOptions equal = ValidOptions("probe-gate");
        equal.IoTimeout = TimeSpan.FromMilliseconds(3_000);
        equal.CleanupTimeout = TimeSpan.FromMilliseconds(4_000);
        equal.ProcessLifetime = TimeSpan.FromMilliseconds(7_000);
        VerifierHostOptions greater = ValidOptions("probe-gate");
        greater.IoTimeout = TimeSpan.FromMilliseconds(3_000);
        greater.CleanupTimeout = TimeSpan.FromMilliseconds(4_000);
        greater.ProcessLifetime = TimeSpan.FromMilliseconds(7_001);
        VerifierHostOptions oversized = ValidOptions("probe-gate");
        oversized.IoTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        oversized.CleanupTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        oversized.ProcessLifetime = TimeSpan.MaxValue;

        var validator = new VerifierHostOptionsValidator();

        Assert.False(validator.Validate(null, equal).Succeeded);
        Assert.True(validator.Validate(null, greater).Succeeded);
        Assert.False(validator.Validate(null, oversized).Succeeded);
        Assert.False(VerifierHostOptionsValidator.TrySumBudgetTicks(
            [TimeSpan.MaxValue, TimeSpan.FromTicks(1)],
            out _));
    }

    [Theory]
    [InlineData("probe-gate", "TokenTimeout")]
    [InlineData("probe-gate", "MetadataTimeout")]
    [InlineData("probe-gate", "WatermarkTimeout")]
    [InlineData("probe-gate", "PollSlice")]
    [InlineData("probe-gate", "BlobIoTimeout")]
    [InlineData("probe-gate", "SafetyStop")]
    [InlineData("start-line", "IoTimeout")]
    [InlineData("start-line", "SafetyStop")]
    public async Task Given_MalformedInactiveBudget_When_HostBindsOptions_Then_IgnoresTheValue(string mode, string propertyName)
    {
        string outputPath = TempPath();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Verifier:Mode"] = mode,
                [$"Verifier:{propertyName}"] = "not-a-duration"
            })
            .Build();
        VerifierHostOptions boundOptions = ValidOptions(mode);
        new VerifierHostOptionsSetup(configuration).Configure(boundOptions);
        Assert.True(new VerifierHostOptionsValidator().Validate(null, boundOptions).Succeeded);

        string[] args = ArgumentsFor(mode, outputPath)
            .Append($"--Verifier:{propertyName}=not-a-duration")
            .ToArray();
        var handler = new SuccessHandler(mode == "start-line" ? VerifierMode.StartLine : VerifierMode.ProbeGate);

        int exitCode = await Program.RunAsync(args, services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(handler);
        });

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("start-line", false)]
    [InlineData("start-line", true)]
    [InlineData("probe-gate", false)]
    [InlineData("probe-gate", true)]
    public async Task Given_InactiveConsumedLimits_When_Bound_Then_OmissionAndMalformedValuesAreIgnored(string mode, bool malformed)
    {
        string[] args = ArgumentsFor(mode, TempPath());
        if (malformed)
        {
            args = args.Concat(["--Verifier:MaxConsumedRecords=not-a-number", "--Verifier:MaxConsumedBytes=not-a-number"]).ToArray();
        }
        int exitCode = await Program.RunAsync(args, services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new SuccessHandler(
                mode == "start-line" ? VerifierMode.StartLine : VerifierMode.ProbeGate));
        });
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("MaxConsumedRecords", null)]
    [InlineData("MaxConsumedRecords", "0")]
    [InlineData("MaxConsumedRecords", "-1")]
    [InlineData("MaxConsumedBytes", null)]
    [InlineData("MaxConsumedBytes", "0")]
    [InlineData("MaxConsumedBytes", "-1")]
    public async Task Given_MissingOrNonpositiveConsumedLimit_When_VerifyStarts_Then_FailsBeforeWork(string property, string? value)
    {
        int calls = 0;
        string[] args = ArgumentsFor("verify", TempPath())
            .Where(argument => !argument.StartsWith($"--Verifier:{property}=", StringComparison.Ordinal)).ToArray();
        if (value is not null) args = args.Append($"--Verifier:{property}={value}").ToArray();
        int exitCode = await Program.RunAsync(args, services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new ReturningHandler(VerifierMode.Verify, () =>
            {
                calls++;
                return 0;
            }));
        });
        Assert.Equal(1, exitCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Given_ConsumedLimitsOfOne_When_VerifyStarts_Then_AcceptsWithoutChangingDurationBudgets()
    {
        string[] args = ArgumentsFor("verify", TempPath())
            .Concat(["--Verifier:MaxConsumedRecords=1", "--Verifier:MaxConsumedBytes=1"]).ToArray();
        int exitCode = await Program.RunAsync(args, services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new SuccessHandler(VerifierMode.Verify));
        });
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_HandlerReturnCode_When_WorkerCompletes_Then_OnlyZeroSucceeds(int handlerCode)
    {
        int exitCode = await Program.RunAsync(ArgumentsFor("probe-gate", TempPath()), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new ReturningHandler(VerifierMode.ProbeGate, () => handlerCode));
        });
        Assert.Equal(handlerCode == 0 ? 0 : 1, exitCode);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("dispose")]
    public async Task Given_PublicationThenHostCleanupFailure_When_RunEnds_Then_ExitsOneAndPreservesOutput(string failure)
    {
        string outputPath = TempPath();
        try
        {
            int exitCode = await Program.RunAsync(ArgumentsFor("probe-gate", outputPath), services =>
            {
                services.RemoveAll<IVerifierModeHandler>();
                services.AddSingleton<IVerifierModeHandler>(new ReturningHandler(VerifierMode.ProbeGate, () =>
                {
                    File.WriteAllText(outputPath, "committed");
                    return 0;
                }));
                services.AddSingleton<IHostedService>(_ => new FailingCleanupService(failure));
            });
            Assert.Equal(1, exitCode);
            Assert.Equal("committed", await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Given_CreateCommitsThenThrows_When_WorkerEnds_Then_ExitsOneWithoutAnotherCreate()
    {
        int creates = 0;
        string outputPath = TempPath();
        try
        {
            int exitCode = await Program.RunAsync(ArgumentsFor("probe-gate", outputPath), services =>
            {
                services.RemoveAll<IVerifierModeHandler>();
                services.AddSingleton<IVerifierModeHandler>(new ReturningHandler(VerifierMode.ProbeGate, () =>
                {
                    creates++;
                    File.WriteAllText(outputPath, "committed");
                    throw new IOException("synthetic uncertain commit");
                }));
            });
            Assert.Equal(1, exitCode);
            Assert.Equal(1, creates);
            Assert.Equal("committed", await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Given_ProbeGateMode_When_HostBuilds_Then_RequiresNoKafkaOrAzureServices()
    {
        var registeredServices = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(
            ArgumentsFor("probe-gate", TempPath()),
            services =>
            {
                registeredServices.AddRange(services);
                services.RemoveAll<IVerifierModeHandler>();
            });
        VerifierHostOptions boundOptions = host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;

        ServiceDescriptor[] externalServices = registeredServices
            .Where(descriptor => descriptor.ServiceType.Namespace?.StartsWith("Confluent", StringComparison.Ordinal) == true
                || descriptor.ServiceType.Namespace?.StartsWith("Azure", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(externalServices);
        Assert.Equal("probe-gate", boundOptions.Mode);
        Assert.DoesNotContain(host.Services.GetServices<IVerifierModeHandler>(), handler => handler.Mode == VerifierMode.ProbeGate);
    }

    [Fact]
    public void Given_ProbeGateMode_When_Composed_Then_RegistersOneLocalHandler()
    {
        using IHost host = Program.BuildHost(ArgumentsFor("probe-gate", TempPath())
            .Append($"--Verifier:ScenarioRoot={Path.GetTempPath()}").ToArray());
        Assert.IsType<ProbeGateHandler>(Assert.Single(host.Services.GetServices<IVerifierModeHandler>()));
        Assert.IsType<LocalProbeGateFileStore>(host.Services.GetRequiredService<IProbeGateFileStore>());
        Assert.NotNull(host.Services.GetRequiredService<ScenarioLoader>());
    }

    [Theory]
    [InlineData("start-line", false)]
    [InlineData("verify", true)]
    public void Given_KafkaMode_When_Composed_Then_RegistersSelectedDependenciesBeforeTestOverrides(string mode, bool hasProducer)
    {
        var registrations = new List<ServiceDescriptor>();
        using IHost host = Program.BuildHost(ArgumentsFor(mode, TempPath())
            .Append($"--Verifier:ScenarioRoot={Path.GetTempPath()}").ToArray(), services =>
            {
                registrations.AddRange(services);
                services.RemoveAll<IVerifierModeHandler>();
            });
        Assert.Contains(registrations, service => service.ServiceType == typeof(VerifierKafkaSessionFactory));
        Assert.Contains(registrations, service => service.ServiceType == typeof(KafkaAuthenticationFactory));
        Assert.Single(registrations, service => service.ServiceType == typeof(IVerifierModeHandler));
        var factory = host.Services.GetRequiredService<KafkaConfigurationFactory>();
        Assert.Equal("verifier-client", factory.CreateVerifierConsumer().Configuration.ClientId);
        if (hasProducer)
        {
            Assert.Equal("loadgen-producer", factory.CreateLoadGenProducer().Configuration.ClientId);
            Assert.NotNull(host.Services.GetRequiredService<ScenarioLoader>());
            Assert.NotNull(host.Services.GetRequiredService<ScenarioResolver>());
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => factory.CreateLoadGenProducer());
            Assert.Null(host.Services.GetService<ScenarioLoader>());
        }
        Assert.DoesNotContain(registrations, service =>
            service.ServiceType.Namespace?.StartsWith("Confluent", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    [InlineData("probe-gate")]
    public async Task Given_AnyConfiguredModeWithoutHandler_When_WorkerRuns_Then_ExitsNonzeroWithoutOutput(string mode)
    {
        string outputPath = TempPath();
        int exitCode = await Program.RunAsync(ArgumentsFor(mode, outputPath),
            services => services.RemoveAll<IVerifierModeHandler>());

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("exception")]
    [InlineData("cancellation")]
    [InlineData("host-stop")]
    [InlineData("host-stop-return")]
    public async Task Given_WorkerExceptionCancellationOrHostStop_When_ExecutionEnds_Then_DoesNotReportSuccess(string failure)
    {
        string outputPath = TempPath();
        Action<IServiceCollection> configure = services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(provider => new ControlledHandler(
                VerifierMode.ProbeGate,
                failure,
                provider.GetRequiredService<IHostApplicationLifetime>()));
        };

        int exitCode = await Program.RunAsync(ArgumentsFor("probe-gate", outputPath), configure);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_HostStopCompetesWithCompletion_When_OutcomeTransitions_Then_OnlyCompletionBeforeStopSucceeds(bool stopFirst)
    {
        var state = new VerifierExecutionState();
        Assert.Equal(1, state.ExitCode);

        if (stopFirst) state.MarkStopped();
        state.MarkSucceeded(CancellationToken.None, CancellationToken.None);
        if (!stopFirst) state.MarkStopped();

        Assert.Equal(stopFirst ? 1 : 0, state.ExitCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_HostFailureCompetesWithCompletion_When_OutcomeTransitions_Then_FailureCannotBeOverwritten(bool failureFirst)
    {
        var state = new VerifierExecutionState();

        if (failureFirst) state.MarkFailed();
        state.MarkSucceeded(CancellationToken.None, CancellationToken.None);
        if (!failureFirst) state.MarkFailed();

        Assert.Equal(1, state.ExitCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_StopCallbackDelayedAfterCancellation_When_HandlerCompletes_Then_RequestedStopStillPreventsSuccess(bool hostStop)
    {
        var state = new VerifierExecutionState();
        using var cancellation = new CancellationTokenSource();
        using var resumeCallback = new ManualResetEventSlim();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken testCancellation = TestContext.Current.CancellationToken;
        using CancellationTokenRegistration registration = cancellation.Token.Register(() =>
        {
            callbackEntered.SetResult();
            resumeCallback.Wait(testCancellation);
            state.MarkStopped();
        });
        Task requestStop = Task.Run(() => cancellation.Cancel(), testCancellation);
        try
        {
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), testCancellation);

            state.MarkSucceeded(
                hostStop ? CancellationToken.None : cancellation.Token,
                hostStop ? cancellation.Token : CancellationToken.None);

            Assert.Equal(1, state.ExitCode);
        }
        finally
        {
            resumeCallback.Set();
            await requestStop;
        }

        Assert.Equal(1, state.ExitCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Given_UnexpectedRepeatInputCardinality_When_OptionsAreValidated_Then_PreservesItForFutureGateJudgement(int pathCount)
    {
        VerifierHostOptions options = ValidOptions("probe-gate");
        options.RepeatResultPaths = Enumerable.Range(0, pathCount).Select(index => $"repeat-{index}.json").ToList();

        var result = new VerifierHostOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(pathCount, options.RepeatResultPaths!.Count);
    }

    [Fact]
    public void Given_DuplicateRepeatResultPaths_When_OptionsAreValidated_Then_PreservesThemForCheck32()
    {
        VerifierHostOptions options = ValidOptions("probe-gate");
        options.RepeatResultPaths = ["same.json", "same.json"];

        var result = new VerifierHostOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(DuplicateRepeatPaths, options.RepeatResultPaths);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Given_ExpectedIdentityCardinalityOtherThanFive_When_OptionsAreValidated_Then_PreservesItForCheck32(int count)
    {
        VerifierHostOptions options = ValidOptions("probe-gate");
        options.ExpectedRepeats = Enumerable.Range(0, count).Select(CreateExpectedRepeat).ToList();

        var result = new VerifierHostOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(count, options.ExpectedRepeats!.Count);
    }

    [Theory]
    [InlineData("blocked-work")]
    [InlineData("blocked-cleanup")]
    public async Task Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124(string scenario)
    {
        string outputPath = TempPath();
        var startInfo = CreateChildStartInfo(scenario, outputPath);
        using Process child = Process.Start(startInfo)!;
        CancellationToken testCancellation = TestContext.Current.CancellationToken;
        Task<string> stdoutTask = child.StandardOutput.ReadToEndAsync(testCancellation);
        Task<string> stderrTask = child.StandardError.ReadToEndAsync(testCancellation);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(testCancellation, timeout.Token);
        bool timedOut = false;
        try
        {
            await child.WaitForExitAsync(waitCancellation.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !testCancellation.IsCancellationRequested)
        {
            timedOut = true;
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
            }
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        Assert.False(timedOut, $"Child did not exit within the watchdog test deadline. stdout={stdout} stderr={stderr}");
        TestContext.Current.SendDiagnosticMessage(
            "Watchdog child exited with {0}. stdout={1} stderr={2}",
            child.ExitCode,
            stdout,
            stderr);
        Assert.Equal(124, child.ExitCode);
        Assert.False(File.Exists(outputPath), $"Child wrote output. stdout={stdout} stderr={stderr}");
    }

    [Theory]
    [InlineData("successful-note", 0, true)]
    [InlineData("valid-result", 0, true)]
    [InlineData("invalid-result", 1, true)]
    [InlineData("incomplete-result", 1, true)]
    [InlineData("write-failure", 1, false)]
    [InlineData("blocked-prepublication-cleanup", 124, false)]
    [InlineData("blocked-disposal-after-publication", 124, true)]
    public async Task Given_ChildFinalizationOutcome_When_ProcessEnds_Then_ReturnsRequiredCodeAndPreservesPublication(
        string scenario, int expectedExit, bool published)
    {
        string outputPath = TempPath();
        using Process child = Process.Start(CreateChildStartInfo(scenario, outputPath))!;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await child.WaitForExitAsync(timeout.Token);
            string output = await stdout;
            string errors = await stderr;
            Assert.Equal(expectedExit, child.ExitCode);
            Assert.Equal(published, File.Exists(outputPath));
            if (published)
            {
                using JsonDocument json = JsonDocument.Parse(await File.ReadAllBytesAsync(outputPath, cancellationToken));
                if (scenario == "successful-note")
                {
                    Assert.Equal("document-events", json.RootElement.GetProperty("topic").GetString());
                    Assert.Single(json.RootElement.GetProperty("partition_highs").EnumerateArray());
                }
                else
                {
                    string status = scenario switch
                    {
                        "invalid-result" => "invalid",
                        "incomplete-result" => "incomplete",
                        _ => "complete_valid"
                    };
                    Assert.Equal(status, json.RootElement.GetProperty("status").GetString());
                    Assert.Equal(status == "complete_valid", json.RootElement.TryGetProperty("ok", out _));
                }
            }
            TestContext.Current.SendDiagnosticMessage("Child {0}: stdout={1} stderr={2}", scenario, output, errors);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
            }
            File.Delete(outputPath);
        }
    }

    internal static VerifierHostOptions ValidOptions(string mode) => new()
    {
        Mode = mode,
        ScenarioId = "A01",
        VerifierCommit = "test-commit",
        ProcessLifetime = TimeSpan.FromSeconds(10),
        CleanupTimeout = TimeSpan.FromSeconds(1),
        IoTimeout = TimeSpan.FromSeconds(1),
        MaxOutputBytes = 16_384,
        MaxDiagnosticsPerCheck = 4,
        MaxDiagnosticBytes = 512,
        ConfigurationId = "C0",
        RunId = "run-1",
        InstanceId = "instance-1",
        ExpectedInstanceId = "instance-1",
        ClientId = "loadgen-producer",
        VerifierClientId = "verifier-client",
        NamespaceName = "namespace-example",
        BlobServiceUri = "https://storage.example/",
        VerifierContainer = "verifier-output",
        TokenTimeout = TimeSpan.FromSeconds(1),
        MetadataTimeout = TimeSpan.FromSeconds(1),
        WatermarkTimeout = TimeSpan.FromSeconds(1),
        PollSlice = TimeSpan.FromMilliseconds(100),
        BlobIoTimeout = TimeSpan.FromSeconds(1),
        TenantId = "tenant-id",
        WorkloadIdentityClientId = "workload-client-id",
        FederatedTokenFilePath = "/tmp/token",
        StartLineOutputName = "start-line.json",
        MaxPartitions = 8,
        MaxInputDepth = 16,
        LedgerContainer = "ledger",
        ArtifactPrefix = "run-1/",
        StartLineInputName = "start-line.json",
        ResultOutputName = "result.json",
        ScenarioRoot = "/tmp/scenarios",
        ExpectedBrokerNames = ["broker-0"],
        SafetyStop = TimeSpan.FromSeconds(2),
        MaxArtifactBytes = 1_048_576,
        MaxTotalInputBytes = 4_194_304,
        MaxRowBytes = 65_536,
        MaxRowsPerArtifact = 1_000,
        MaxInventoryEntries = 1_000,
        MaxNativeReportBytes = 65_536,
        MaxBrokerEntries = 16,
        MaxConsumedRecords = 1000,
        MaxConsumedBytes = 1048576,
        ScenarioLoadOptions = new VerifierScenarioLoadOptions
        {
            MaxDocumentCharacters = 16_384,
            MaxNestingDepth = 32,
            MaxScalarCharacters = 2_048,
            MaxParserEvents = 2_000
        },
        GateOutputPath = TempPath(),
        ExpectedRepeats = Enumerable.Range(0, 5).Select(index => new ExpectedProbeRepeat
        {
            ScenarioId = "A01",
            ConfigurationId = "C0",
            RunId = $"probe-{index}",
            InstanceId = $"instance-{index}",
            ExpectedInstanceId = $"instance-{index}",
            ClientId = "loadgen-producer"
        }).ToList(),
        RepeatResultPaths = Enumerable.Range(0, 5).Select(index => $"probe-{index}.json").ToList()
    };

    private static IEnumerable<object[]> RequiredOptionsFor(string mode, params string[] names) =>
        names.Select(name => new object[] { mode, name });

    private static ExpectedProbeRepeat CreateExpectedRepeat(int index) => new()
    {
        ScenarioId = "A01",
        ConfigurationId = "C0",
        RunId = $"probe-{index}",
        InstanceId = $"instance-{index}",
        ExpectedInstanceId = $"instance-{index}",
        ClientId = "loadgen-producer"
    };

    private static void SetLimit(VerifierHostOptions options, string propertyName, int value)
    {
        string[] propertyPath = propertyName.Split('.');
        object owner = options;
        for (var index = 0; index < propertyPath.Length - 1; index++)
        {
            PropertyInfo nestedProperty = owner.GetType().GetProperty(propertyPath[index])!;
            owner = nestedProperty.GetValue(owner)!;
        }

        PropertyInfo property = owner.GetType().GetProperty(propertyPath[^1])!;
        object? valueToSet = Nullable.GetUnderlyingType(property.PropertyType) == typeof(long)
            ? (object)(long)value
            : value;
        property.SetValue(owner, valueToSet);
    }

    private static void SetOptionNull(VerifierHostOptions options, string propertyName)
    {
        (object owner, string property) = ResolveOwner(options, propertyName);
        owner.GetType().GetProperty(property)!.SetValue(owner, null);
    }

    private static void SetOptionValue(VerifierHostOptions options, string propertyName, object value)
    {
        (object owner, string property) = ResolveOwner(options, propertyName);
        owner.GetType().GetProperty(property)!.SetValue(owner, value);
    }

    private static (object Owner, string Property) ResolveOwner(VerifierHostOptions options, string propertyName)
    {
        string[] propertyPath = propertyName.Split('.');
        object owner = options;
        for (var index = 0; index < propertyPath.Length - 1; index++)
        {
            if (int.TryParse(propertyPath[index], out int listIndex))
            {
                owner = ((System.Collections.IList)owner)[listIndex]!;
            }
            else
            {
                PropertyInfo nestedProperty = owner.GetType().GetProperty(propertyPath[index])!;
                owner = nestedProperty.GetValue(owner)!;
            }
        }

        return (owner, propertyPath[^1]);
    }

    internal static string[] ArgumentsFor(string mode, string outputPath)
    {
        var args = new List<string>
        {
            $"--Verifier:Mode={mode}",
            "--Verifier:ScenarioId=A01",
            "--Verifier:VerifierCommit=test-commit",
            "--Verifier:ProcessLifetime=00:00:10",
            "--Verifier:CleanupTimeout=00:00:01",
            "--Verifier:IoTimeout=00:00:01",
            "--Verifier:MaxOutputBytes=16384",
            "--Verifier:MaxDiagnosticsPerCheck=4",
            "--Verifier:MaxDiagnosticBytes=512"
        };

        if (mode is "start-line" or "verify")
        {
            args.AddRange(
            [
                "--Verifier:ConfigurationId=C0",
                "--Verifier:RunId=run-1",
                "--Verifier:InstanceId=instance-1",
                "--Verifier:ExpectedInstanceId=instance-1",
                "--Verifier:ClientId=loadgen-producer",
                "--Verifier:VerifierClientId=verifier-client",
                "--Verifier:NamespaceName=namespace-example",
                "--Verifier:BlobServiceUri=https://storage.example/",
                "--Verifier:VerifierContainer=verifier-output",
                "--Verifier:TokenTimeout=00:00:01",
                "--Verifier:MetadataTimeout=00:00:01",
                "--Verifier:WatermarkTimeout=00:00:01",
                "--Verifier:PollSlice=00:00:00.100",
                "--Verifier:BlobIoTimeout=00:00:01",
                "--Verifier:TenantId=tenant-id",
                "--Verifier:WorkloadIdentityClientId=workload-client-id",
                "--Verifier:FederatedTokenFilePath=/tmp/token"
            ]);
        }

        if (mode == "start-line")
        {
            args.Add("--Verifier:StartLineOutputName=start-line.json");
            args.Add("--Verifier:MaxPartitions=8");
            args.Add("--Verifier:MaxInputDepth=16");
        }
        else if (mode == "verify")
        {
            args.AddRange(
            [
                "--Verifier:LedgerContainer=ledger",
                "--Verifier:ArtifactPrefix=run-1/",
                "--Verifier:StartLineInputName=start-line.json",
                "--Verifier:ResultOutputName=result.json",
                "--Verifier:ScenarioRoot=/tmp/scenarios",
                "--Verifier:ExpectedBrokerNames:0=broker-0",
                "--Verifier:SafetyStop=00:00:02",
                "--Verifier:MaxArtifactBytes=1048576",
                "--Verifier:MaxTotalInputBytes=4194304",
                "--Verifier:MaxRowBytes=65536",
                "--Verifier:MaxRowsPerArtifact=1000",
                "--Verifier:MaxInventoryEntries=1000",
                "--Verifier:MaxPartitions=8",
                "--Verifier:MaxInputDepth=16",
                "--Verifier:MaxNativeReportBytes=65536",
                "--Verifier:MaxBrokerEntries=16",
                "--Verifier:MaxConsumedRecords=1000",
                "--Verifier:MaxConsumedBytes=1048576"
            ]);
            AddScenarioLoadOptions(args);
        }
        else
        {
            args.AddRange(
            [
                $"--Verifier:GateOutputPath={outputPath}",
                "--Verifier:ScenarioRoot=/tmp/scenarios",
                "--Verifier:MaxArtifactBytes=1048576",
                "--Verifier:MaxTotalInputBytes=4194304",
                "--Verifier:MaxInputDepth=16"
            ]);
            AddScenarioLoadOptions(args);
            for (var index = 0; index < 5; index++)
            {
                args.Add($"--Verifier:ExpectedRepeats:{index}:ScenarioId=A01");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ConfigurationId=C0");
                args.Add($"--Verifier:ExpectedRepeats:{index}:RunId=probe-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:InstanceId=instance-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ExpectedInstanceId=instance-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ClientId=loadgen-producer");
                args.Add($"--Verifier:RepeatResultPaths:{index}=probe-{index}.json");
            }
        }

        return args.ToArray();
    }

    private static void AddScenarioLoadOptions(List<string> args)
    {
        args.Add("--Verifier:ScenarioLoadOptions:MaxDocumentCharacters=16384");
        args.Add("--Verifier:ScenarioLoadOptions:MaxNestingDepth=32");
        args.Add("--Verifier:ScenarioLoadOptions:MaxScalarCharacters=2048");
        args.Add("--Verifier:ScenarioLoadOptions:MaxParserEvents=2000");
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), "verifier-host-" + Guid.NewGuid().ToString("N") + ".json");

    private static ProcessStartInfo CreateChildStartInfo(string scenario, string outputPath)
    {
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Current test process path is unavailable.");
        var startInfo = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(VerifierWatchdogChildProcessTests).Assembly.Location);
        }

        startInfo.ArgumentList.Add("-class");
        startInfo.ArgumentList.Add(typeof(VerifierWatchdogChildProcessTests).FullName!);
        startInfo.Environment["PARCHWELL_VERIFIER_WATCHDOG_CHILD"] = scenario;
        startInfo.Environment["PARCHWELL_VERIFIER_WATCHDOG_OUTPUT"] = outputPath;
        return startInfo;
    }

    private sealed class ReturningHandler(VerifierMode mode, Func<int> execute) : IVerifierModeHandler
    {
        public VerifierMode Mode => mode;
        public Task<int> ExecuteAsync(CancellationToken cancellationToken) => Task.FromResult(execute());
    }

    private sealed class FailingCleanupService(string failure) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => failure == "stop"
            ? Task.FromException(new IOException("synthetic stop failure")) : Task.CompletedTask;
        public void Dispose()
        {
            if (failure == "dispose") throw new IOException("synthetic disposal failure");
        }
    }

    internal sealed class ControlledHandler : IVerifierModeHandler
    {
        private readonly string _failure;
        private readonly IHostApplicationLifetime _lifetime;

        public ControlledHandler(VerifierMode mode, string failure, IHostApplicationLifetime lifetime)
        {
            Mode = mode;
            _failure = failure;
            _lifetime = lifetime;
        }

        public VerifierMode Mode { get; }

        public Task<int> ExecuteAsync(CancellationToken cancellationToken) => _failure switch
        {
            "exception" => Task.FromException<int>(new InvalidOperationException("synthetic")),
            "cancellation" => Task.FromCanceled<int>(new CancellationToken(canceled: true)),
            "host-stop" => StopHostAndWaitAsync(cancellationToken),
            "host-stop-return" => StopHostAndReturnSuccess(),
            _ => Task.FromResult(0)
        };

        private Task<int> StopHostAndReturnSuccess()
        {
            _lifetime.StopApplication();
            return Task.FromResult(0);
        }

        private async Task<int> StopHostAndWaitAsync(CancellationToken cancellationToken)
        {
            _lifetime.StopApplication();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    internal sealed class SuccessHandler : IVerifierModeHandler
    {
        public SuccessHandler(VerifierMode mode) => Mode = mode;

        public VerifierMode Mode { get; }

        public Task<int> ExecuteAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class CountingHandler(Action onExecute) : IVerifierModeHandler
    {
        public VerifierMode Mode => VerifierMode.ProbeGate;

        public Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            onExecute();
            return Task.FromResult(0);
        }
    }

}

/// <summary>Isolated entry point selected when the host test executable launches its watchdog child.</summary>
public sealed class VerifierWatchdogChildProcessTests
{
    [Fact]
    public async Task Given_ChildWatchdogMarker_When_Run_Then_UsesProductionHardExit()
    {
        string? scenario = Environment.GetEnvironmentVariable("PARCHWELL_VERIFIER_WATCHDOG_CHILD");
        if (scenario is null)
        {
            return;
        }

        string outputPath = Environment.GetEnvironmentVariable("PARCHWELL_VERIFIER_WATCHDOG_OUTPUT")!;
        var clock = new ManualTimeProvider();
        Action<IServiceCollection> configure = services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new ChildBlockingHandler(clock, scenario, outputPath));
            if (scenario == "blocked-disposal-after-publication")
            {
                services.AddSingleton<IHostedService>(_ => new ChildBlockingDisposal(clock));
            }
            if (scenario == "blocked-cleanup")
            {
                services.AddSingleton<IHostedService>(new ChildBlockingCleanup(clock));
            }
        };

        int exitCode = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", outputPath), configure, clock);
        Environment.Exit(exitCode);
    }

    private sealed class ChildBlockingHandler : IVerifierModeHandler
    {
        private readonly ManualTimeProvider _clock;
        private readonly string _scenario;
        private readonly string _outputPath;

        public ChildBlockingHandler(ManualTimeProvider clock, string scenario, string outputPath)
        {
            _clock = clock;
            _scenario = scenario;
            _outputPath = outputPath;
        }

        public VerifierMode Mode => VerifierMode.ProbeGate;

        public Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_scenario == "blocked-prepublication-cleanup")
            {
                _clock.Advance(TimeSpan.FromSeconds(10));
                Thread.Sleep(Timeout.Infinite);
            }
            if (_scenario == "blocked-work")
            {
                _clock.Advance(TimeSpan.FromSeconds(10));
                return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }

            if (_scenario == "write-failure") throw new IOException("synthetic write failure");
            if (_scenario is "successful-note" or "valid-result" or "invalid-result" or "incomplete-result" or "blocked-disposal-after-publication")
            {
                File.WriteAllBytes(_outputPath, PublicationBytes());
            }
            return Task.FromResult(_scenario is "invalid-result" or "incomplete-result" ? 1 : 0);
        }

        private byte[] PublicationBytes()
        {
            var identity = new RunIdentity("A01", "C0", "run-1", "instance-1", "instance-1", "loadgen-producer");
            var limits = new OutputLimits(16_384, 4, 512);
            if (_scenario == "successful-note")
            {
                return StartLineNoteCodec.Write(new StartLineNote(1, identity, "document-events",
                    [new PartitionHigh(0, 0)], "1970-01-01T00:00:00Z", "synthetic", "synthetic", "synthetic"), limits);
            }
            VerifierResultStatus status = _scenario switch
            {
                "invalid-result" => VerifierResultStatus.Invalid,
                "incomplete-result" => VerifierResultStatus.Incomplete,
                _ => VerifierResultStatus.CompleteValid
            };
            return VerifierResultCodec.Write(new VerifierResult(1, identity, "synthetic", null,
                "synthetic", "synthetic", "synthetic", status, [], null, null, null, null,
                status == VerifierResultStatus.CompleteValid ? new OutcomeCounts(1, 0, 0, 0, 0, 0) : null,
                null, null, null, null, [], []), limits);
        }
    }

    private sealed class ChildBlockingDisposal(ManualTimeProvider clock) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose()
        {
            clock.Advance(TimeSpan.FromSeconds(10));
            Thread.Sleep(Timeout.Infinite);
        }
    }

    private sealed class ChildBlockingCleanup : IHostedService
    {
        private readonly ManualTimeProvider _clock;

        public ChildBlockingCleanup(ManualTimeProvider clock) => _clock = clock;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _clock.Advance(TimeSpan.FromSeconds(10));
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override long GetTimestamp()
        {
            lock (_gate)
            {
                return _timestamp;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan delta)
        {
            List<ManualTimer> ready;
            lock (_gate)
            {
                _utcNow += delta;
                _timestamp += delta.Ticks;
                ready = _timers.Where(timer => timer.IsDue).ToList();
                foreach (ManualTimer timer in ready)
                {
                    timer.MarkFired();
                }
            }

            foreach (ManualTimer timer in ready)
            {
                timer.Fire();
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private bool _disposed;
            private bool _fired;
            private TimeSpan _dueTime;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                _dueTime = dueTime;
                _ = period;
            }

            public bool IsDue => !_disposed && !_fired && _owner._timestamp >= _dueTime.Ticks;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_owner._gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _dueTime = dueTime;
                    _fired = false;
                    _ = period;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (_owner._gate)
                {
                    _disposed = true;
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void MarkFired() => _fired = true;

            public void Fire() => _callback(_state);
        }
    }
}
#pragma warning restore CA1707
