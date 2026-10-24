using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Telemetry;
using Parchwell.Kafka.Tests.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Telemetry;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Collection("Parchwell telemetry")]
[Trait("Category", "Unit")]
public sealed class ParchwellTelemetryExportTests
{
    private static readonly string[] HistogramTagNames = ["broker", "client_id", "role", "run", "scenario"];
    private static readonly Action<ILogger, string, Exception?> LogOperation = LoggerMessage.Define<string>(
        LogLevel.Information, new EventId(1, "SyntheticOperation"), "Synthetic operation {Operation}");

    [Fact]
    public void Given_WorkerAndKafkaSignals_When_Flushed_Then_RegisteredPipelineExportsWithCredentialAndPreservedWindows()
    {
        using var pipeline = new TelemetryTestPipeline();
        pipeline.AssertResources();
        using var source = new ActivitySource(ParchwellTelemetrySources.WorkerActivitySourceName);
        using (Activity? activity = source.StartActivity("synthetic-worker-operation"))
        {
            Assert.NotNull(activity);
        }

        ILogger logger = pipeline.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Parchwell.Workers");
        using (logger.BeginScope(new Dictionary<string, object?> { ["repeat"] = 1 }))
        {
            LogOperation(logger, "publish", null);
        }

        using var statistics = new KafkaStatisticsMetrics();
        var identity = StatisticsTestSupport.ProducerIdentity();
        statistics.Record(identity, Broker(1, 100), 1, 0);
        statistics.Record(identity, Broker(20000, 300), 2, 1);
        statistics.Record(identity, Broker(20001, null), 3, 0);
        using var authentication = new KafkaAuthenticationMetrics();
        authentication.Record(new KafkaAuthenticationFailure(
            new KafkaAuthenticationIdentity("A01", "run-test", KafkaClientRole.LoadGenProducer, "test-client"),
            "refresh", "synthetic-failure"));
        using var worker = new Meter(ParchwellTelemetrySources.WorkerMeterName);
        worker.CreateCounter<long>("parchwell.worker.operations").Add(1);
        pipeline.Flush();

        Assert.Contains("synthetic-worker-operation", pipeline.Activities.Names);
        RecordedLog log = Assert.Single(pipeline.Logs.Records, record => record.Message == "Synthetic operation publish");
        Assert.Equal("publish", log.Attributes["Operation"]);
        Assert.Equal(1, log.Scopes["repeat"]);
        AssertHistogram(pipeline, "kafka.broker.throttle.max_ms", "ms", 3, 1, 20001);
        AssertHistogram(pipeline, "kafka.broker.rtt.p99_us", "us", 2, 100, 300);
        Assert.Contains(pipeline.Metrics.Records, record => record.Name == "kafka.broker.req_timeouts" && record.Sum == 6);
        Assert.Contains(pipeline.Metrics.Records, record => record.Name == "kafka.broker.disconnects" && record.Sum == 1);
        Assert.Contains(pipeline.Metrics.Records, record => record.Name == "parchwell.kafka.oauthbearer.refresh_handler.errors" && record.Sum == 1);
        Assert.Contains(pipeline.Metrics.Records, record => record.Name == "parchwell.worker.operations" && record.Sum == 1);
        Assert.NotEmpty(pipeline.Handler.Requests);
        Assert.All(pipeline.Handler.Requests, request => Assert.Equal("Bearer synthetic-token", request.Authorization));
        Assert.Contains("https://monitor.azure.com//.default", pipeline.Credential.Scopes);
        Assert.Contains(pipeline.Handler.Requests, request => request.Body.Contains("synthetic-worker-operation", StringComparison.Ordinal));
        Assert.Contains(pipeline.Handler.Requests, request => request.Body.Contains("Synthetic operation publish", StringComparison.Ordinal));
        Assert.Contains(pipeline.Handler.Requests, request => request.Body.Contains("kafka.broker.throttle.max_ms", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_RetainedStatisticsCompletion_When_ExporterFails_Then_EvidenceAndCompletionRemainUnchanged()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var statistics = new KafkaStatisticsMetrics();
        await using var session = new KafkaStatisticsSession(StatisticsTestSupport.ProducerIdentity(),
            StatisticsTestSupport.Options(), clock, sink, statistics, nativeClockCompatible: true);
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        string raw = StatisticsTestSupport.Report("test-client", 10_500_000, 500_000);
        session.Capture(raw);
        KafkaStatisticsCompletion completion = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(completion.IsComplete);
        KafkaStatisticsReport report = Assert.Single(sink.Reports);
        var original = completion with { };

        using (var pipeline = new TelemetryTestPipeline())
        {
            pipeline.AssertResources();
            pipeline.Handler.FailExports();
            using var source = new ActivitySource(ParchwellTelemetrySources.WorkerActivitySourceName);
            using (Activity? activity = source.StartActivity("synthetic-failed-export"))
            {
                Assert.NotNull(activity);
            }

            pipeline.Flush();
            Assert.Contains(pipeline.Handler.Requests, request => request.Body.Contains("synthetic-failed-export", StringComparison.Ordinal));
        }

        Assert.Equal(original, completion);
        Assert.Same(report, Assert.Single(sink.Reports));
        Assert.Equal(raw, report.RawJson);
    }

    private static KafkaBrokerStatistics Broker(long throttle, long? rtt) => new(
        "broker/1", 1, "UP", 100, 0, 0, 0, 0, 0,
        new KafkaWindowStatistics(1, throttle), new KafkaWindowStatistics(rtt.HasValue ? 1 : 0, rtt));

    private static void AssertHistogram(TelemetryTestPipeline pipeline, string name, string unit, long count, double min, double max)
    {
        RecordedMetric metric = Assert.Single(pipeline.Metrics.Records, record => record.Name == name);
        Assert.Equal(unit, metric.Unit);
        Assert.Equal(count, metric.Count);
        Assert.Equal(min, metric.Min);
        Assert.Equal(max, metric.Max);
        Assert.Equal(HistogramTagNames, metric.Tags.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("A01", metric.Tags["scenario"]);
        Assert.Equal("run-test", metric.Tags["run"]);
        Assert.Equal("LoadGenProducer", metric.Tags["role"]);
        Assert.Equal("test-client", metric.Tags["client_id"]);
        Assert.Equal("broker/1", metric.Tags["broker"]);
    }
}
