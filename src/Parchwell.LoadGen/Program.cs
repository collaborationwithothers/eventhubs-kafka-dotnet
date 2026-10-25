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
using Parchwell.Kafka.Telemetry;
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
        ITimer? processWatchdog = null;
        IHost? host = null;
        LoadGenHostOptions? runOptions = null;
        ILogger? logger = null;
        IProducerRunLifecycle? lifecycle = null;
        ProducerLifecycleOptions? lifecycleOptions = null;
        ProducerRunContext? runContext = null;
        TimeProvider? clock = null;
        bool readyForCompletion = false;
        int exitCode = 1;
        try
        {
            host = hostFactory?.Invoke() ?? BuildHost(args, configureTestServices);
            LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
            runOptions = options;
            clock = host.Services.GetRequiredService<TimeProvider>();
            lifecycle = host.Services.GetRequiredService<IProducerRunLifecycle>();
            IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();
            string sourceCommit = configuration["PARCHWELL_SOURCE_COMMIT"] ?? string.Empty;
            runContext = CreateRunContext(options, sourceCommit);
            lifecycleOptions = CreateLifecycleOptions(options);
            ILogger processLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Parchwell.LoadGen.Process");
            logger = processLogger;
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
            CancellationToken applicationStopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, applicationStopping).ConfigureAwait(false); }
            catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested) { }
            exitCode = worker.ExitCode;
            readyForCompletion = exitCode == 0;
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
                if (host is not null)
                {
                    TimeSpan cleanupTimeout = runOptions?.CleanupTimeout ?? TimeSpan.FromSeconds(10);
                    using var hostCleanupDeadline = new CancellationTokenSource(cleanupTimeout, clock ?? TimeProvider.System);
                    try
                    {
                        await host.StopAsync(hostCleanupDeadline.Token).WaitAsync(hostCleanupDeadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        ReportHostFailure(new TimeoutException("loadgen_host_stop_timeout"), runOptions, logger, alwaysWriteIdentityFallback: true);
                        exitCode = 1;
                    }
                    catch (Exception exception)
                    {
                        ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                        exitCode = 1;
                    }
                    try
                    {
                        await Task.Run(host.Dispose).WaitAsync(hostCleanupDeadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        ReportHostFailure(new TimeoutException("loadgen_host_dispose_timeout"), runOptions, logger, alwaysWriteIdentityFallback: true);
                        exitCode = 1;
                    }
                    catch (Exception exception)
                    {
                        ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                        exitCode = 1;
                    }
                }
            }
            catch (Exception exception)
            {
                ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                exitCode = 1;
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
        if (readyForCompletion && exitCode == 0 && lifecycle is not null && lifecycleOptions is not null && runContext is not null && clock is not null)
        {
            try
            {
                using var receiptDeadline = new CancellationTokenSource(lifecycleOptions.CleanupTimeout, clock);
                await lifecycle.WriteCompletionAsync(runContext, lifecycleOptions, receiptDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                ReportHostFailure(exception, runOptions, logger, alwaysWriteIdentityFallback: true);
                exitCode = 1;
            }
        }
        return exitCode;
    }

    // Retained for the Runner's wire-contract fixtures; deployed completion uses BlobProducerRunLifecycle.
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

    // This helper is not part of RunAsync; it preserves the existing Runner fixture format only.
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

        if (options is not null && (alwaysWriteIdentityFallback || logger is null))
        {
            Console.Error.WriteLine($"loadgen_startup_or_host_failure:{exception.GetType().Name} for scenario {options.ScenarioId} run {options.RunId}");
            if (exception is OptionsValidationException validation && validation.OptionsType == typeof(KafkaWorkloadIdentityOptions))
                foreach (string failure in validation.Failures) Console.Error.WriteLine(failure);
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

    internal static IHost BuildHost(string[] args, Action<IServiceCollection>? configureTestServices = null) =>
        BuildHostCore(args, configureTestServices, registerTelemetry: null);

    internal static IHost BuildHostCore(string[] args, Action<IServiceCollection>? configureTestServices,
        Action<IServiceCollection, Action<ParchwellTelemetryOptions>>? registerTelemetry)
    {
        LoadGenHostOptions? validatedBootstrapOptions = null;
        try
        {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        LoadGenHostOptions bootstrapOptions = ReadOptions(builder.Configuration);
        if (!IsValid(bootstrapOptions))
            throw new OptionsValidationException(Options.DefaultName, typeof(LoadGenHostOptions), ["LoadGen host inputs or finite budgets are invalid."]);
        validatedBootstrapOptions = bootstrapOptions;
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = bootstrapOptions.CleanupTimeout);
        string sourceCommit = builder.Configuration["PARCHWELL_SOURCE_COMMIT"] ?? string.Empty;
        if (sourceCommit.Length != 40 || sourceCommit.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidOperationException("source_commit_must_be_40_lowercase_hex");
        var authentication = new KafkaWorkloadIdentityOptions
        {
            TenantId = builder.Configuration["AZURE_TENANT_ID"],
            ClientId = builder.Configuration["AZURE_CLIENT_ID"],
            TokenFilePath = builder.Configuration["AZURE_FEDERATED_TOKEN_FILE"]
        };
        List<string> authenticationFailures = [];
        if (string.IsNullOrWhiteSpace(authentication.TenantId)) authenticationFailures.Add("TenantId is required.");
        if (string.IsNullOrWhiteSpace(authentication.ClientId)) authenticationFailures.Add("ClientId is required.");
        if (string.IsNullOrWhiteSpace(authentication.TokenFilePath)) authenticationFailures.Add("TokenFilePath is required.");
        if (authenticationFailures.Count != 0)
            throw new OptionsValidationException(Options.DefaultName, typeof(KafkaWorkloadIdentityOptions), authenticationFailures);

        Action<ParchwellTelemetryOptions> configureTelemetry = options =>
        {
            options.ConnectionString = bootstrapOptions.ApplicationInsightsConnectionString;
            options.ServiceName = "Parchwell.LoadGen";
            options.ServiceInstanceId = bootstrapOptions.ActualInstanceId;
            options.ScenarioId = bootstrapOptions.ScenarioId;
            options.RunId = bootstrapOptions.RunId;
        };
        if (registerTelemetry is null)
        {
            var credential = new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
            {
                TenantId = authentication.TenantId,
                ClientId = authentication.ClientId,
                TokenFilePath = authentication.TokenFilePath
            });
            builder.Services.AddParchwellTelemetry(credential, configureTelemetry);
        }
        else registerTelemetry(builder.Services, configureTelemetry);

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
            options.TenantId = authentication.TenantId;
            options.ClientId = authentication.ClientId;
            options.TokenFilePath = authentication.TokenFilePath;
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
        builder.Services.AddSingleton<IProducerRunLifecycle>(provider =>
        {
            LoadGenHostOptions hostOptions = provider.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
            var client = new BlobServiceClient(new Uri(hostOptions.BlobEndpoint, UriKind.Absolute),
                new WorkloadIdentityCredential(), new BlobClientOptions());
            return new BlobProducerRunLifecycle(client, provider.GetRequiredService<TimeProvider>());
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
        catch (Exception exception)
        {
            ReportHostFailure(exception, validatedBootstrapOptions, logger: null, alwaysWriteIdentityFallback: true);
            throw;
        }
    }

    private static ProducerRunContext CreateRunContext(LoadGenHostOptions options, string sourceCommit) => new(
        new RunIdentity(options.ScenarioId, options.ConfigurationId, options.RunId, options.ActualInstanceId,
            options.ExpectedInstanceId, options.ClientId), options.Phase, options.RepeatNumber, options.JobName,
        options.PodName, options.PodUid, sourceCommit);

    private static ProducerLifecycleOptions CreateLifecycleOptions(LoadGenHostOptions options) => new(
        options.BlobEndpoint, options.BlobContainer, options.CleanupTimeout, options.UploadTimeout,
        options.MaxArtifactBytes, options.MaxAggregateArtifactBytes, options.OutputRoot);

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
