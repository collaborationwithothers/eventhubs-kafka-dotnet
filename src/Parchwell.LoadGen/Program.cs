using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Reflection;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ITimer? processWatchdog = null;
        try
        {
            using IHost host = BuildHost(args);
            LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
            processWatchdog = host.Services.GetRequiredService<TimeProvider>().CreateTimer(_ =>
            {
                Console.Error.WriteLine("loadgen_process_lifetime_watchdog_expired");
                Environment.Exit(124);
            }, null, options.ProcessLifetimeTimeout, Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            LoadGenWorker worker = host.Services.GetRequiredService<LoadGenWorker>();
            await host.WaitForShutdownAsync().ConfigureAwait(false);
            await host.StopAsync().ConfigureAwait(false);
            return worker.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("loadgen_startup_or_host_failure:" + exception.GetType().Name);
            return 1;
        }
        finally
        {
            processWatchdog?.Dispose();
        }
    }

    internal static IHost BuildHost(string[] args, Action<IServiceCollection>? configureTestServices = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
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

        builder.Services.AddParchwellKafkaStatistics(statisticsOptions =>
        {
            LoadGenHostOptions hostOptions = ReadOptions(builder.Configuration);
            statisticsOptions.MaxSnapshotCharacters = hostOptions.MaxSnapshotCharacters;
            statisticsOptions.QueueCapacity = hostOptions.EvidenceQueueCapacity;
            statisticsOptions.MaxBrokerSeries = hostOptions.MaxBrokerSeries;
            statisticsOptions.CompletionTimeout = hostOptions.StatisticsCompletionTimeout;
        });

        builder.Services.AddSingleton<IWorkerAdmission, UnavailableWorkerAdmission>();
        builder.Services.AddSingleton<IRunArtifactPublisher, UnavailableRunArtifactPublisher>();
        builder.Services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, IProducer<byte[], byte[]>>>(
            _ => (configuration, statistics, evidence) => new ProducerBuilder<byte[], byte[]>(configuration)
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
