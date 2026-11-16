using Azure.Identity;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.SeedF04;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Telemetry;
using Parchwell.Kafka.Statistics;
using Parchwell.Enrichment.NativeMonitoring;

namespace Parchwell.Enrichment;

internal static class Program
{
    private static Task<int> Main(string[] args) => RunAsync(args);

    internal static async Task<int> RunAsync(string[] args, Action<HostApplicationBuilder>? configure = null,
        TextWriter? output = null, Func<IHost>? hostFactory = null)
    {
        output ??= Console.Out;
        if (args is ["--image-smoke"])
        {
            try
            {
                _ = Library.VersionString;
                await output.WriteLineAsync("{\"mode\":\"single-action-seed-smoke\",\"native_loaded\":true,\"uid_expected\":1654}").ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                return 0;
            }
            catch (Exception) { return 1; }
        }
        if (args is ["--consumer-processing"])
            return await RunConsumerProcessingAsync(configure, hostFactory).ConfigureAwait(false);
        bool validation = args is ["--validate-seed-configuration"];
        if (!validation && args is not ["--single-action-seed"]) return 2;
        IHost? host = null;
        ITimer? watchdog = null;
        SeedF04Options? options = null;
        SeedF04Result? result = null;
        TimeProvider clock = TimeProvider.System;
        bool hostCleaned = true;
        int exitCode = 1;
        try
        {
            host = hostFactory?.Invoke() ?? BuildHost(configure, validation);
            options = host.Services.GetRequiredService<IOptions<SeedF04Options>>().Value;
            clock = host.Services.GetRequiredService<TimeProvider>();
            if (!validation)
                watchdog = clock.CreateTimer(_ => Environment.Exit(124), null,
                    TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            if (validation) exitCode = 0;
            else
            {
                CancellationToken stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                result = await host.Services.GetRequiredService<SeedF04Worker>().ExecuteAsync(stopping).ConfigureAwait(false);
                exitCode = result.ExitCode;
            }
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync($"seed_startup_or_execution_failed scenario_id=F04 run_id={options?.RunId ?? "unknown"}").ConfigureAwait(false);
        }
        finally
        {
            if (host is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options?.HostCleanupSeconds ?? 10), clock);
                try { await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { hostCleaned = false; }
                try
                {
                    KafkaNativeMonitoringCoordinator? monitoring = host.Services.GetService<KafkaNativeMonitoringCoordinator>();
                    if (monitoring is not null) await monitoring.CompleteAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception) { }
                try { await Task.Run(host.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { hostCleaned = false; }
            }
        }
        try { watchdog?.Dispose(); }
        catch (Exception) { hostCleaned = false; }
        try
        {
            if (!hostCleaned) exitCode = 1;
            if (!validation && hostCleaned && result is { CleanupCompleted: true } && options is not null)
            {
                using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.HostCleanupSeconds), clock);
                await output.WriteLineAsync(SeedF04Receipt.CreateLine(options, result))
                    .WaitAsync(receiptDeadline.Token).ConfigureAwait(false);
                await output.FlushAsync().WaitAsync(receiptDeadline.Token).ConfigureAwait(false);
            }
        }
        catch (Exception) { exitCode = 1; }
        return exitCode;
    }

    internal static IHost BuildHost(Action<HostApplicationBuilder>? configure = null, bool validationOnly = false)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        configure?.Invoke(builder);
        ConfigurationManager configuration = builder.Configuration;
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddOptions<SeedF04Options>()
            .Bind(configuration.GetSection(SeedF04Options.SectionName), binding => binding.ErrorOnUnknownConfiguration = true)
            .Validate(options =>
            {
                options.Validate();
                if (!StringComparer.Ordinal.Equals(options.SourceCommit, configuration["PARCHWELL_SOURCE_COMMIT"]) ||
                    !StringComparer.Ordinal.Equals(options.JobName, configuration["PARCHWELL_JOB_NAME"]) ||
                    !StringComparer.Ordinal.Equals(options.PodName, configuration["PARCHWELL_POD_NAME"]) ||
                    !StringComparer.Ordinal.Equals(options.PodUid, configuration["PARCHWELL_POD_UID"]))
                    throw new InvalidOperationException("seed_deployment_identity_mismatch");
                return true;
            }).ValidateOnStart();
        builder.Services.AddParchwellKafka(KafkaClientRole.EnrichmentProducer, options =>
        {
            options.NamespaceName = configuration["SeedF04:NamespaceName"] ?? string.Empty;
            options.ClientId = configuration["SeedF04:PodName"] ?? string.Empty;
        });
        if (!validationOnly)
        {
            RegisterTelemetry(builder.Services, configuration, "F04", configuration["SeedF04:RunId"]);
            RegisterNativeMonitoring(builder.Services, configuration, "F04", configuration["SeedF04:RunId"]);
            builder.Services.AddParchwellKafkaAuthentication(options =>
            {
                options.TenantId = configuration["AZURE_TENANT_ID"];
                options.ClientId = configuration["AZURE_CLIENT_ID"];
                options.TokenFilePath = configuration["AZURE_FEDERATED_TOKEN_FILE"];
            });
            builder.Services.AddSingleton<Func<IProducer<byte[], byte[]>>>(services => () =>
            {
                SeedF04Options options = services.GetRequiredService<IOptions<SeedF04Options>>().Value;
                KafkaAuthenticationFactory authentication = services.GetRequiredService<KafkaAuthenticationFactory>();
                CancellationToken stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                KafkaOAuthBearerSession session = authentication.CreateSession(new KafkaAuthenticationIdentity(
                    "F04", options.RunId, KafkaClientRole.EnrichmentProducer, options.PodName), stopping);
                ProducerConfig producer = services.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentProducer().Configuration;
                KafkaNativeMonitoringLifetime lifetime = services.GetRequiredService<NativeClientCapture>().Begin(
                    new KafkaStatisticsIdentity("F04", options.RunId, KafkaClientRole.EnrichmentProducer, options.PodName));
                try
                {
                    IProducer<byte[], byte[]> result = authentication.CreateProducerBuilder<byte[], byte[]>(producer, session)
                        .SetStatisticsHandler(NativeClientCapture.ProducerHandler(lifetime)).Build();
                    lifetime.MarkBuilt();
                    return result;
                }
                catch { lifetime.MarkBuildFailed(); lifetime.Complete(false, "unknown"); throw; }
            });
            builder.Services.AddSingleton<SeedF04Worker>();
        }
        return builder.Build();
    }

    private static async Task<int> RunConsumerProcessingAsync(Action<HostApplicationBuilder>? configure, Func<IHost>? hostFactory)
    {
        IHost? host = null;
        ConsumerProcessingOptions? options = null;
        string reason = "consumer_identity_invalid";
        try
        {
            host = hostFactory?.Invoke() ?? BuildConsumerProcessingHost(configure);
            options = host.Services.GetRequiredService<IOptions<ConsumerProcessingOptions>>().Value;
            options.ValidateDeploymentIdentity(host.Services.GetRequiredService<IConfiguration>());
            reason = "consumer_plan_invalid";
            if (host.Services.GetService<IConsumerProcessingPlanSource>() is null)
                throw new InvalidOperationException(reason);
            await host.StartAsync().ConfigureAwait(false);
            return await host.Services.GetRequiredService<ConsumerProcessingWorker>()
                .ExecuteAsync(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync($"consumer_processing_failed reason={reason} scenario_id={ConsumerIdentity(options?.ScenarioId)} run_id={ConsumerIdentity(options?.RunId)} role={ConsumerIdentity(options?.Role)}").ConfigureAwait(false);
            return 1;
        }
        finally
        {
            if (host is not null)
            {
                TimeProvider clock = host.Services.GetService<TimeProvider>() ?? TimeProvider.System;
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
                try { await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { }
                try
                {
                    KafkaNativeMonitoringCoordinator? monitoring = host.Services.GetService<KafkaNativeMonitoringCoordinator>();
                    if (monitoring is not null) await monitoring.CompleteAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception) { }
                try { await Task.Run(host.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { }
            }
        }
    }

    internal static IHost BuildConsumerProcessingHost(Action<HostApplicationBuilder>? configure = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        configure?.Invoke(builder);
        ConfigurationManager configuration = builder.Configuration;
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddOptions<ConsumerProcessingOptions>()
            .Bind(configuration.GetSection(ConsumerProcessingOptions.SectionName), binding => binding.ErrorOnUnknownConfiguration = true)
            .Validate(options => { options.ValidateDeploymentIdentity(configuration); return true; }).ValidateOnStart();
        builder.Services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, options =>
        {
            options.NamespaceName = configuration["ConsumerProcessing:NamespaceName"] ?? string.Empty;
            options.ClientId = configuration["ConsumerProcessing:PodName"] ?? string.Empty;
            string run = configuration["ConsumerProcessing:RunId"] ?? string.Empty;
            string phase = configuration["ConsumerProcessing:Phase"] ?? string.Empty;
            string repeat = configuration["ConsumerProcessing:RepeatNumber"] ?? string.Empty;
            options.EnrichmentGroupId = $"{run}-{phase.ToLowerInvariant()}-r{repeat}-consumers";
        });
        RegisterTelemetry(builder.Services, configuration, configuration["ConsumerProcessing:ScenarioId"], configuration["ConsumerProcessing:RunId"]);
        RegisterNativeMonitoring(builder.Services, configuration, configuration["ConsumerProcessing:ScenarioId"], configuration["ConsumerProcessing:RunId"]);
        builder.Services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = configuration["AZURE_TENANT_ID"];
            options.ClientId = configuration["AZURE_CLIENT_ID"];
            options.TokenFilePath = configuration["AZURE_FEDERATED_TOKEN_FILE"];
        });
        builder.Services.TryAddSingleton<Func<ConsumerConfig>>(services =>
            () => services.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration);
        builder.Services.TryAddSingleton<Func<IConsumerRecordStore>>(services => () =>
        {
            ConsumerProcessingOptions options = services.GetRequiredService<IOptions<ConsumerProcessingOptions>>().Value;
            var client = new BlobServiceClient(new Uri(options.BlobEndpoint, UriKind.Absolute), new WorkloadIdentityCredential(), new BlobClientOptions());
            return new BlobConsumerRecordStore(client, options.Prefix);
        });
        builder.Services.TryAddSingleton<ConsumerProcessingConsumerFactory>(services => (consumerConfiguration, handlers) =>
        {
            ConsumerProcessingOptions options = services.GetRequiredService<IOptions<ConsumerProcessingOptions>>().Value;
            KafkaAuthenticationFactory authentication = services.GetRequiredService<KafkaAuthenticationFactory>();
            CancellationToken stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            KafkaOAuthBearerSession session = authentication.CreateSession(new KafkaAuthenticationIdentity(
                options.ScenarioId, options.RunId, KafkaClientRole.EnrichmentConsumer, options.PodName), stopping);
            KafkaNativeMonitoringLifetime lifetime = services.GetRequiredService<NativeClientCapture>().Begin(
                new KafkaStatisticsIdentity(options.ScenarioId, options.RunId, KafkaClientRole.EnrichmentConsumer, options.PodName));
            try
            {
            IConsumer<byte[], byte[]> result = authentication.CreateConsumerBuilder<byte[], byte[]>(consumerConfiguration, session)
                .SetStatisticsHandler(NativeClientCapture.ConsumerHandler(lifetime))
                .SetPartitionsAssignedHandler(handlers.Assigned)
                .SetPartitionsRevokedHandler((consumer, partitions) => handlers.Revoked(consumer, partitions.Select(partition => partition.TopicPartition).ToList()))
                .SetPartitionsLostHandler((consumer, partitions) => handlers.Lost(consumer, partitions.Select(partition => partition.TopicPartition).ToList()))
                .SetOffsetsCommittedHandler(handlers.OffsetsCommitted).Build();
            lifetime.MarkBuilt();
            return result;
            }
            catch { lifetime.MarkBuildFailed(); lifetime.Complete(false, "unknown"); throw; }
        });
        builder.Services.TryAddSingleton<Action<int>>(_ => Environment.Exit);
        builder.Services.TryAddSingleton<ConsumerSignalSender>(_ => ConsumerSelfSignal.Send);
        builder.Services.AddSingleton(services => new ConsumerProcessingWorker(
            services.GetRequiredService<IOptions<ConsumerProcessingOptions>>(), services.GetService<IConsumerProcessingPlanSource>(),
            services.GetRequiredService<Func<ConsumerConfig>>(), services.GetRequiredService<Func<IConsumerRecordStore>>(),
            services.GetRequiredService<ConsumerProcessingConsumerFactory>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<Action<int>>(), services.GetRequiredService<ConsumerSignalSender>(), monitoring: services.GetRequiredService<NativeClientCapture>()));
        return builder.Build();
    }

    private static void RegisterNativeMonitoring(IServiceCollection services, IConfiguration configuration, string? scenarioId, string? runId)
    {
        services.AddOptions<KafkaNativeMonitoringOptions>()
            .Bind(configuration.GetSection(KafkaNativeMonitoringOptions.SectionName), binding => binding.ErrorOnUnknownConfiguration = true)
            .Validate(options => options.IsValid(out _) && options.RunId == runId && options.ScenarioId == scenarioId &&
                options.JobName == configuration["PARCHWELL_JOB_NAME"] && options.PodUid == configuration["PARCHWELL_POD_UID"] &&
                options.SourceCommit == configuration["PARCHWELL_SOURCE_COMMIT"], "Native monitoring identity is invalid.").ValidateOnStart();
        services.AddParchwellNativeMonitoring();
        services.TryAddSingleton<IKafkaNativeMonitoringStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<KafkaNativeMonitoringOptions>>().Value;
            return new BlobNativeMonitoringStore(new BlobContainerClient(new Uri(new Uri(options.BlobEndpoint), options.BlobContainer),
                new WorkloadIdentityCredential(), new BlobClientOptions()));
        });
        services.AddSingleton(provider => provider.GetRequiredService<KafkaNativeMonitoringFactory>()
            .Create(provider.GetRequiredService<IOptions<KafkaNativeMonitoringOptions>>().Value.CreateIdentity()));
        services.AddSingleton<NativeClientCapture>();
    }

    private static void RegisterTelemetry(IServiceCollection services, IConfiguration configuration, string? scenarioId, string? runId)
    {
        string? instance = configuration["Telemetry:ServiceInstanceId"] ?? configuration["PARCHWELL_POD_NAME"];
        if (configuration["Telemetry:ServiceName"] != "Parchwell.Enrichment" ||
            configuration["Telemetry:ScenarioId"] != scenarioId || configuration["Telemetry:RunId"] != runId ||
            string.IsNullOrWhiteSpace(instance))
            throw new InvalidOperationException("enrichment_telemetry_identity_invalid");
        services.AddParchwellTelemetry(new WorkloadIdentityCredential(), options =>
        {
            options.ConnectionString = configuration["Telemetry:ConnectionString"] ?? string.Empty;
            options.ServiceName = configuration["Telemetry:ServiceName"] ?? string.Empty;
            options.ServiceInstanceId = instance;
            options.ScenarioId = scenarioId ?? string.Empty;
            options.RunId = runId ?? string.Empty;
        });
    }

    private static string ConsumerIdentity(string? value) => string.IsNullOrEmpty(value) ? "unknown" : value;
}
