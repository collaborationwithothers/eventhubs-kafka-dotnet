using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Kafka.Telemetry;

/// <summary>Registers host-owned telemetry providers for worker and Kafka signals.</summary>
public static class ParchwellTelemetryServiceCollectionExtensions
{
    private const string ValidationFailure = "Telemetry requires a connection string and nonempty identity values without whitespace or control characters.";

    /// <summary>Registers Azure Monitor telemetry using the supplied Entra credential and validates its identity.</summary>
    /// <param name="services">The application's service collection, which owns provider lifetime.</param>
    /// <param name="credential">The workload identity credential supplied by the composition root.</param>
    /// <param name="configure">Configures the destination and identity once; registration retains a snapshot.</param>
    /// <returns>The same service collection for registration chaining.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="OptionsValidationException">The destination or identity is missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">Telemetry is already registered on this collection.</exception>
    /// <remarks>
    /// Keep the host alive through worker shutdown. Registration adds no automatic Kafka spans
    /// and does not guarantee backend delivery or change evidence-capture results.
    /// </remarks>
    public static IServiceCollection AddParchwellTelemetry(
        this IServiceCollection services,
        TokenCredential credential,
        Action<ParchwellTelemetryOptions> configure) =>
        AddParchwellTelemetryCore(services, credential, configure, null, null);

    internal static IServiceCollection AddParchwellTelemetryCore(
        IServiceCollection services,
        TokenCredential credential,
        Action<ParchwellTelemetryOptions> configure,
        HttpPipelineTransport? transport,
        Action<ResourceBuilder>? prepareResourceForTest)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RegistrationMarker)))
        {
            throw new InvalidOperationException("Parchwell telemetry is already registered.");
        }

        ParchwellTelemetryOptions configured = new();
        configure(configured);
        ParchwellTelemetryOptions snapshot = new();
        CopyOptions(configured, snapshot);
        if (!IsValid(snapshot))
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(ParchwellTelemetryOptions), [ValidationFailure]);
        }

        services.AddSingleton(new RegistrationMarker());
        services.AddOptions<ParchwellTelemetryOptions>()
            .Configure(options => CopyOptions(snapshot, options))
            .Validate(IsValid, ValidationFailure)
            .ValidateOnStart();

        var telemetry = services.AddOpenTelemetry().UseMicrosoftOpenTelemetry(
            options => ConfigureDistro(options, snapshot, credential, transport));
        telemetry.ConfigureResource(resource =>
        {
            // Tests remove cloud detectors before resource construction. Identity always comes
            // from this production callback, after the optional reset.
            prepareResourceForTest?.Invoke(resource);
            resource.AddService(serviceName: snapshot.ServiceName, serviceInstanceId: snapshot.ServiceInstanceId);
            resource.AddAttributes(new KeyValuePair<string, object>[]
            {
                new("scenario", snapshot.ScenarioId),
                new("run", snapshot.RunId)
            });
        });
        telemetry.WithTracing(tracing => tracing.AddSource(ParchwellTelemetrySources.WorkerActivitySourceName));
        telemetry.WithMetrics(metrics => metrics
            .AddMeter(KafkaStatisticsMetrics.MeterName, KafkaAuthenticationMetrics.MeterName, ParchwellTelemetrySources.WorkerMeterName)
            .AddView("kafka.broker.throttle.max_ms", HistogramView())
            .AddView("kafka.broker.rtt.p99_us", HistogramView()));
        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });
        return services;
    }

    internal static void ConfigureDistro(
        MicrosoftOpenTelemetryOptions options,
        ParchwellTelemetryOptions snapshot,
        TokenCredential credential,
        HttpPipelineTransport? transport)
    {
        options.Exporters = ExportTarget.AzureMonitor;
        options.AzureMonitor.ConnectionString = snapshot.ConnectionString;
        options.AzureMonitor.Credential = credential;
        options.AzureMonitor.TracesPerSecond = null;
        options.AzureMonitor.SamplingRatio = 1.0F;
        options.AzureMonitor.EnableTraceBasedLogsSampler = false;
        options.AzureMonitor.EnableLiveMetrics = false;
        options.AzureMonitor.EnableStandardMetrics = true;
        options.AzureMonitor.EnablePerfCounters = false;
        options.AzureMonitor.DisableOfflineStorage = true;
        if (transport is not null)
        {
            options.AzureMonitor.Transport = transport;
        }

        options.Instrumentation.EnableTracing = true;
        options.Instrumentation.EnableMetrics = true;
        options.Instrumentation.EnableLogging = true;
        options.Instrumentation.EnableHttpClientInstrumentation = true;
        options.Instrumentation.EnableAzureSdkInstrumentation = true;
        options.Instrumentation.EnableAspNetCoreInstrumentation = false;
        options.Instrumentation.EnableSqlClientInstrumentation = false;
        options.Instrumentation.EnableOpenAIInstrumentation = false;
        options.Instrumentation.EnableSemanticKernelInstrumentation = false;
        options.Instrumentation.EnableAgentFrameworkInstrumentation = false;
        options.Instrumentation.EnableAgent365Instrumentation = false;
    }

    private static ExplicitBucketHistogramConfiguration HistogramView() => new()
    {
        RecordMinMax = true,
        TagKeys = ["scenario", "run", "role", "client_id", "broker"]
    };

    private static bool IsValid(ParchwellTelemetryOptions options) =>
        !string.IsNullOrWhiteSpace(options.ConnectionString)
        && IsIdentity(options.ServiceName)
        && IsIdentity(options.ServiceInstanceId)
        && IsIdentity(options.ScenarioId)
        && IsIdentity(options.RunId);

    private static bool IsIdentity(string value) =>
        !string.IsNullOrEmpty(value) && !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));

    private static void CopyOptions(ParchwellTelemetryOptions source, ParchwellTelemetryOptions target)
    {
        target.ConnectionString = source.ConnectionString;
        target.ServiceName = source.ServiceName;
        target.ServiceInstanceId = source.ServiceInstanceId;
        target.ScenarioId = source.ScenarioId;
        target.RunId = source.RunId;
    }

    private sealed class RegistrationMarker;
}
