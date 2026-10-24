using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO.Compression;
using System.Net;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Parchwell.Kafka.Telemetry;
using Xunit;

namespace Parchwell.Kafka.Tests.Telemetry;

internal sealed class TelemetryTestPipeline : IDisposable
{
    internal const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://telemetry.invalid/";
    private readonly HttpRequestGuard _guard = new();
    private readonly ResourceReset _resourceReset = new();
    private readonly HttpClient _client;
    private readonly HttpClientTransport _transport;

    internal TelemetryTestPipeline(Action<ParchwellTelemetryOptions>? configure = null, Action<IServiceCollection>? afterRegistration = null)
    {
        _client = new HttpClient(Handler);
        _transport = new HttpClientTransport(_client);
        try
        {
            var services = new ServiceCollection();
            ParchwellTelemetryServiceCollectionExtensions.AddParchwellTelemetryCore(
                services, Credential, configure ?? Configure, _transport, _resourceReset.Prepare);
            afterRegistration?.Invoke(services);
            services.AddOpenTelemetry()
                .WithTracing(builder => builder.AddProcessor(Activities))
                .WithMetrics(builder => builder.AddReader(new BaseExportingMetricReader(Metrics)))
                .WithLogging(builder => builder.AddProcessor(Logs));
            Services = services.BuildServiceProvider();
            Traces = Services.GetRequiredService<TracerProvider>();
            Meter = Services.GetRequiredService<MeterProvider>();
            Logger = Services.GetRequiredService<LoggerProvider>();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal RecordingTelemetryHandler Handler { get; } = new();
    internal SyntheticCredential Credential { get; } = new();
    internal ActivityRecorder Activities { get; } = new();
    internal LogRecorder Logs { get; } = new();
    internal MetricRecorder Metrics { get; } = new();
    internal ServiceProvider Services { get; } = null!;
    internal TracerProvider Traces { get; } = null!;
    internal MeterProvider Meter { get; } = null!;
    internal LoggerProvider Logger { get; } = null!;

    internal static void Configure(ParchwellTelemetryOptions options)
    {
        options.ConnectionString = ConnectionString;
        options.ServiceName = "Parchwell.LoadGen";
        options.ServiceInstanceId = "pod-test";
        options.ScenarioId = "A01";
        options.RunId = "run-test";
    }

    internal void AssertResources()
    {
        foreach (BaseProvider provider in new BaseProvider[] { Traces, Meter, Logger })
        {
            var attributes = provider.GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
            Assert.Equal(true, attributes["parchwell.test.resource_reset"]);
            Assert.Equal("Parchwell.LoadGen", attributes["service.name"]);
            Assert.Equal("pod-test", attributes["service.instance.id"]);
            Assert.Equal("A01", attributes["scenario"]);
            Assert.Equal("run-test", attributes["run"]);
        }

        Assert.Equal(0, _resourceReset.DetectCalls);
    }

    internal void Flush()
    {
        Assert.True(Traces.ForceFlush(5000));
        Assert.True(Meter.ForceFlush(5000));
        Assert.True(Logger.ForceFlush(5000));
    }

    public void Dispose()
    {
        try
        {
            Services?.Dispose();
            _transport.Dispose();
            _client.Dispose();
        }
        finally
        {
            _guard.Dispose();
            Assert.False(_guard.UnexpectedRequest, "The offline telemetry pipeline attempted an unexpected HTTP request.");
            Assert.Equal(0, _resourceReset.DetectCalls);
        }
    }
}

internal sealed class ResourceReset : IResourceDetector
{
    private int _detectCalls;
    internal int DetectCalls => Volatile.Read(ref _detectCalls);

    internal void Prepare(ResourceBuilder resource)
    {
        resource.AddDetector(this);
        resource.Clear();
        resource.AddAttributes([new KeyValuePair<string, object>("parchwell.test.resource_reset", true)]);
    }

    public Resource Detect()
    {
        Interlocked.Increment(ref _detectCalls);
        return Resource.Empty;
    }
}

internal sealed class HttpRequestGuard : EventListener
{
    private int _unexpectedRequest;
    internal bool UnexpectedRequest => Volatile.Read(ref _unexpectedRequest) != 0;

    internal static bool IsUnexpectedHost(string? host) => !string.Equals(host, "telemetry.invalid", StringComparison.Ordinal);

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.Http")
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != "System.Net.Http" || eventData.EventId != 1)
        {
            return;
        }

        int index = eventData.PayloadNames?.IndexOf("host") ?? -1;
        string? host = index >= 0 ? eventData.Payload?[index] as string : null;
        if (IsUnexpectedHost(host))
        {
            Interlocked.Exchange(ref _unexpectedRequest, 1);
        }
    }
}

internal sealed class SyntheticCredential : TokenCredential
{
    internal ConcurrentQueue<string> Scopes { get; } = new();

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        foreach (string scope in requestContext.Scopes)
        {
            Scopes.Enqueue(scope);
        }

        return new AccessToken("synthetic-token", DateTimeOffset.MaxValue);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}

internal sealed record RecordedRequest(string? Authorization, string Body);

internal sealed class RecordingTelemetryHandler : HttpMessageHandler
{
    private int _fail;
    internal ConcurrentQueue<RecordedRequest> Requests { get; } = new();
    internal void FailExports() => Interlocked.Exchange(ref _fail, 1);

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        RecordAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        RecordAsync(request, cancellationToken);

    private async Task<HttpResponseMessage> RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("telemetry.invalid", request.RequestUri?.Host);
        byte[] bytes = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        if (request.Content?.Headers.ContentEncoding.Contains("gzip") == true)
        {
            using var compressed = new MemoryStream(bytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var decoded = new MemoryStream();
            await gzip.CopyToAsync(decoded, cancellationToken);
            bytes = decoded.ToArray();
        }

        Requests.Enqueue(new RecordedRequest(request.Headers.Authorization?.ToString(), Encoding.UTF8.GetString(bytes)));
        return new HttpResponseMessage(Volatile.Read(ref _fail) == 0 ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"itemsReceived\":1,\"itemsAccepted\":1,\"errors\":[]}")
        };
    }
}

internal sealed class ActivityRecorder : BaseProcessor<Activity>
{
    internal ConcurrentQueue<string> Names { get; } = new();
    public override void OnEnd(Activity data) => Names.Enqueue(data.OperationName);
}

internal sealed record RecordedLog(string? Message, IReadOnlyDictionary<string, object?> Attributes, IReadOnlyDictionary<string, object?> Scopes);

internal sealed class LogRecorder : BaseProcessor<LogRecord>
{
    internal ConcurrentQueue<RecordedLog> Records { get; } = new();

    public override void OnEnd(LogRecord data)
    {
        var scopes = new Dictionary<string, object?>(StringComparer.Ordinal);
        data.ForEachScope((scope, values) =>
        {
            foreach (var pair in scope)
            {
                values[pair.Key] = pair.Value;
            }
        }, scopes);
        Records.Enqueue(new RecordedLog(data.FormattedMessage,
            data.Attributes?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [], scopes));
    }
}

internal sealed record RecordedMetric(string Name, string? Unit, long Count, double Min, double Max, long Sum, IReadOnlyDictionary<string, object?> Tags);

internal sealed class MetricRecorder : BaseExporter<Metric>
{
    internal ConcurrentQueue<RecordedMetric> Records { get; } = new();

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (Metric metric in batch)
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                var tags = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in point.Tags)
                {
                    tags[tag.Key] = tag.Value;
                }

                bool histogram = metric.MetricType == MetricType.Histogram;
                double min = double.NaN;
                double max = double.NaN;
                if (histogram)
                {
                    point.TryGetHistogramMinMaxValues(out min, out max);
                }

                Records.Enqueue(new RecordedMetric(metric.Name, metric.Unit,
                    histogram ? point.GetHistogramCount() : 0, min, max,
                    metric.MetricType == MetricType.LongSum ? point.GetSumLong() : 0, tags));
            }
        }

        return ExportResult.Success;
    }
}
