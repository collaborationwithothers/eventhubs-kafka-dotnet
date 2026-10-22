using Azure.Identity;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen;

internal static class Program
{
    private static readonly Action<ILogger, string, string, Exception?> ProcessWatchdogExpired =
        LoggerMessage.Define<string, string>(LogLevel.Critical, new EventId(3, nameof(ProcessWatchdogExpired)),
            "loadgen_process_lifetime_watchdog_expired for scenario {ScenarioId} run {RunId}");
    private static readonly Action<ILogger, string, string, string, Exception?> HostFailure =
        LoggerMessage.Define<string, string, string>(LogLevel.Critical, new EventId(4, nameof(HostFailure)),
            "loadgen_startup_or_host_failure:{FailureType} for scenario {ScenarioId} run {RunId}");

    private static Task<int> Main(string[] args) => RunAsync(args);

    internal static async Task<int> RunAsync(string[] args, Action<IServiceCollection>? configureTestServices = null,
        TextWriter? completionOutput = null, Func<IHost>? hostFactory = null)
    {
        if (args.Length == 1 && args[0] == "--image-smoke") return ImageSmoke();
        completionOutput ??= Console.Out;
        string? completion = null;
        ITimer? processWatchdog = null;
        IHost? host = null;
        LoadGenHostOptions? runOptions = null;
        ILogger? logger = null;
        int exitCode = 1;
        try
        {
            IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
            IReadOnlyDictionary<string, string?>? admitted = await DeploymentAdmission.WaitAsync(configuration, TimeProvider.System).ConfigureAwait(false);
            host = hostFactory?.Invoke() ?? BuildHost(args, configureTestServices, admitted);
            LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
            ILogger processLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Parchwell.LoadGen.Process");
            if (IsValid(options))
            {
                runOptions = options;
                logger = processLogger;
            }
            processWatchdog = host.Services.GetRequiredService<TimeProvider>().CreateTimer(_ =>
            {
                try
                {
                    ProcessWatchdogExpired(processLogger, options.ScenarioId, options.RunId, null);
                }
                catch
                {
                    Console.Error.WriteLine($"loadgen_process_lifetime_watchdog_expired scenario_id={options.ScenarioId} run_id={options.RunId}");
                }
                finally
                {
                    Environment.Exit(124);
                }
            }, null, options.ProcessLifetimeTimeout, Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            LoadGenWorker worker = host.Services.GetRequiredService<LoadGenWorker>();
            await host.WaitForShutdownAsync().ConfigureAwait(false);
            await host.StopAsync().ConfigureAwait(false);
            exitCode = worker.ExitCode;
            if (exitCode == 0)
            {
                string sourceCommit = host.Services.GetRequiredService<IConfiguration>()["PARCHWELL_SOURCE_COMMIT"] ??
                    typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+').Last() ?? string.Empty;
                completion = CreateCompletionReceipt(new RunIdentity(options.ScenarioId, options.ConfigurationId, options.RunId,
                    options.ActualInstanceId, options.ExpectedInstanceId, options.ClientId), sourceCommit);
            }
        }
        catch (Exception exception)
        {
            ReportHostFailure(exception, runOptions, logger);
            exitCode = 1;
        }
        finally
        {
            try
            {
                host?.Dispose();
            }
            catch (Exception exception)
            {
                ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                exitCode = 1;
            }
            if (exitCode == 0 && completion is not null)
            {
                try
                {
                    await WriteCompletionAsync(completionOutput, completion).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                    exitCode = 1;
                }
            }
            try
            {
                processWatchdog?.Dispose();
            }
            catch (Exception exception)
            {
                ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                exitCode = 1;
            }
        }
        return exitCode;
    }

    internal static string CreateCompletionReceipt(RunIdentity identity, string sourceCommit) => JsonSerializer.Serialize(new
    {
        schema_version = 1, mode = "loadgen",
        identity = new
        {
            scenario_id = identity.ScenarioId, configuration_id = identity.ConfigurationId, run_id = identity.RunId,
            instance_id = identity.InstanceId, expected_instance_id = identity.ExpectedInstanceId, client_id = identity.ClientId
        },
        source_commit = sourceCommit, handler_exit_code = 0, cleanup_completed = true
    });

    internal static async Task WriteCompletionAsync(TextWriter output, string completion)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(completion) > 65536)
            throw new InvalidDataException("loadgen_completion_receipt_too_large");
        await output.WriteLineAsync("PARCHWELL_COMPLETION " + completion).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static void ReportHostFailure(Exception exception, LoadGenHostOptions? options, ILogger? logger,
        bool alwaysWriteIdentityFallback = false)
    {
        if (options is not null && logger is not null)
        {
            try
            {
                HostFailure(logger, exception.GetType().Name, options.ScenarioId, options.RunId, exception);
                if (!alwaysWriteIdentityFallback) return;
            }
            catch
            {
                alwaysWriteIdentityFallback = true;
            }
        }

        if (options is not null && alwaysWriteIdentityFallback)
        {
            Console.Error.WriteLine($"loadgen_startup_or_host_failure:{exception.GetType().Name} scenario_id={options.ScenarioId} run_id={options.RunId}");
            return;
        }

        if (options is null || logger is null)
            Console.Error.WriteLine("loadgen_startup_or_host_failure:" + exception.GetType().Name);
    }

    internal static int ImageSmoke()
    {
        try
        {
            _ = Library.VersionString;
            string[] paths = ["scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"];
            foreach (string path in paths)
            {
                string current = AppContext.BaseDirectory;
                foreach (string segment in path.Split('/'))
                {
                    current = Path.Combine(current, segment);
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("image_scenario_file_not_regular");
                }
                if ((File.GetAttributes(current) & FileAttributes.Directory) != 0)
                    throw new InvalidDataException("image_scenario_file_not_regular");
            }
            Console.Out.WriteLine("{\"native_loaded\":true,\"scenario_files\":2,\"uid_expected\":1654}");
            return 0;
        }
        catch
        {
            Console.Error.WriteLine("loadgen_image_smoke_failed");
            return 1;
        }
    }

    internal static IHost BuildHost(string[] args, Action<IServiceCollection>? configureTestServices = null,
        IReadOnlyDictionary<string, string?>? deploymentInputs = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        if (deploymentInputs is not null) builder.Configuration.AddInMemoryCollection(deploymentInputs);
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        builder.Services.AddOptions<LoadGenHostOptions>()
            .Configure(options => BindOptions(options, builder.Configuration))
            .Validate(options => IsValid(options), "LoadGen host inputs or finite budgets are invalid.")
            .ValidateOnStart();

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<RunEvidenceSinkRouter>();
        builder.Services.AddSingleton<IKafkaStatisticsEvidenceSink>(provider => provider.GetRequiredService<RunEvidenceSinkRouter>());
        builder.Services.AddParchwellScenarios(options =>
        {
            LoadGenHostOptions hostOptions = ReadOptions(builder.Configuration);
            options.MaxDocumentCharacters = hostOptions.MaxDocumentCharacters;
            options.MaxNestingDepth = hostOptions.MaxNestingDepth;
            options.MaxScalarCharacters = hostOptions.MaxScalarCharacters;
            options.MaxParserEvents = hostOptions.MaxParserEvents;
        });
        builder.Services.AddSingleton<IScenarioSource>(provider => new FileSystemScenarioSource(
            provider.GetRequiredService<IOptions<LoadGenHostOptions>>().Value.ScenarioSourceRoot));

        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
        {
            builder.Services.AddParchwellKafka(role, kafkaOptions =>
            {
                LoadGenHostOptions hostOptions = ReadOptions(builder.Configuration);
                kafkaOptions.NamespaceName = hostOptions.NamespaceName;
                kafkaOptions.ClientId = hostOptions.ClientId;
                kafkaOptions.EnrichmentGroupId = hostOptions.EnrichmentGroupId;
            });
        }

        builder.Services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = builder.Configuration["AZURE_TENANT_ID"];
            options.ClientId = builder.Configuration["AZURE_CLIENT_ID"];
            options.TokenFilePath = builder.Configuration["AZURE_FEDERATED_TOKEN_FILE"];
        });

        builder.Services.AddParchwellKafkaStatistics(statisticsOptions =>
        {
            LoadGenHostOptions hostOptions = ReadOptions(builder.Configuration);
            statisticsOptions.MaxSnapshotCharacters = hostOptions.MaxSnapshotCharacters;
            statisticsOptions.QueueCapacity = hostOptions.EvidenceQueueCapacity;
            statisticsOptions.MaxBrokerSeries = hostOptions.MaxBrokerSeries;
            statisticsOptions.CompletionTimeout = hostOptions.StatisticsCompletionTimeout;
        });

        builder.Services.AddSingleton<IWorkerAdmission, OAuthBearerWorkerAdmission>();
        builder.Services.AddSingleton<IRunArtifactPublisher>(provider =>
        {
            LoadGenHostOptions hostOptions = provider.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
            new RunIdentity(hostOptions.ScenarioId, hostOptions.ConfigurationId, hostOptions.RunId,
                hostOptions.ActualInstanceId, hostOptions.ExpectedInstanceId, hostOptions.ClientId).Validate();
            if (!StringComparer.Ordinal.Equals(hostOptions.BlobContainer, "ledger"))
                throw new InvalidOperationException("publication_container_must_be_ledger");
            if (hostOptions.MaxArtifactBytes > int.MaxValue)
                throw new InvalidOperationException("publication_artifact_limit_exceeds_supported_reader_bound");
            var client = new BlobServiceClient(new Uri(hostOptions.BlobEndpoint, UriKind.Absolute),
                new WorkloadIdentityCredential(), new BlobClientOptions());
            return new BlobRunArtifactPublisher(client, provider.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(
            provider => (configuration, statistics, evidence, authentication) => provider
                .GetRequiredService<KafkaAuthenticationFactory>()
                .CreateProducerBuilder<byte[], byte[]>(configuration, authentication)
                .SetStatisticsHandler((_, report) => statistics.Capture(report))
                .SetLogHandler((_, message) => evidence.CaptureLog(message))
                .SetErrorHandler((_, error) => evidence.CaptureError(error))
                .Build());
        builder.Services.AddSingleton<RunSessionFactory>();
        builder.Services.AddSingleton<LoadGenWorker>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LoadGenWorker>());
        configureTestServices?.Invoke(builder.Services);
        return builder.Build();
    }

    private static bool IsValid(LoadGenHostOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static LoadGenHostOptions ReadOptions(IConfiguration configuration)
    {
        var options = new LoadGenHostOptions();
        BindOptions(options, configuration);
        return options;
    }

    private static void BindOptions(LoadGenHostOptions options, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(LoadGenHostOptions.SectionName);
        foreach (PropertyInfo property in typeof(LoadGenHostOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite) continue;
            string? raw = section[property.Name];
            if (raw is null) throw new InvalidOperationException("required_loadgen_setting_missing:" + property.Name);
            Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object value = targetType == typeof(string) ? raw
                : targetType == typeof(TimeSpan) ? TimeSpan.Parse(raw, CultureInfo.InvariantCulture)
                : Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            property.SetValue(options, value);
        }
    }
}
