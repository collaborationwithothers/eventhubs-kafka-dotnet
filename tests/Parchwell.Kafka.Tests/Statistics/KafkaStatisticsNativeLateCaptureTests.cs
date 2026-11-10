using System.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Native")]
public sealed class KafkaStatisticsNativeLateCaptureTests
{
    // Real librdkafka statistics thread, session built by the production registration. Linux only:
    // TimeProvider.System and librdkafka's report timestamp share CLOCK_MONOTONIC there
    // (docs/operations/statistics-bridge.md, "Drain, completion and clock proof").
    [Fact]
    public async Task Given_RealProducerCallbackAfterCaptureClosed_When_CompletionSeals_Then_CompletionStaysComplete()
    {
        Assert.True(OperatingSystem.IsLinux(), "This test needs the Linux monotonic clock shared with librdkafka.");
        CancellationToken token = TestContext.Current.CancellationToken;
        long threshold = long.MaxValue;
        var sink = new HoldingStatisticsSink(report =>
            report.Snapshot is { } snapshot && snapshot.TimestampMicroseconds > Volatile.Read(ref threshold));
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaStatisticsEvidenceSink>(sink);
        services.AddParchwellKafkaStatistics(options =>
        {
            options.MaxSnapshotCharacters = 16_384;
            options.QueueCapacity = 8;
            options.MaxBrokerSeries = 4;
            options.CompletionTimeout = TimeSpan.FromSeconds(20);
        });
        await using ServiceProvider provider = services.BuildServiceProvider();
        KafkaStatisticsBridgeFactory factory = provider.GetRequiredService<KafkaStatisticsBridgeFactory>();
        await using KafkaStatisticsSession session = factory.Create(StatisticsTestSupport.ProducerIdentity("client"));

        int started = 0;
        int ended = 0;
        var config = new ProducerConfig
        {
            BootstrapServers = "127.0.0.1:1",
            ClientId = "client",
            StatisticsIntervalMs = 100
        };
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(config)
            .SetLogHandler((_, _) => { })
            .SetErrorHandler((_, _) => { })
            .SetStatisticsHandler((_, json) =>
            {
                Interlocked.Increment(ref started);
                session.Capture(json);
                Interlocked.Increment(ref ended);
            })
            .Build();

        KafkaStatisticsCompletion completion;
        try
        {
            await LateCaptureTestSupport.WaitUntilAsync(() => sink.WrittenTimestamps.Length >= 1, "one written report", token);
            Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(token);
            Volatile.Write(ref threshold, (long)((Int128)Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency));
            await sink.EnteredAsync(1, token);
            int endedAtHold = Volatile.Read(ref ended);
            await LateCaptureTestSupport.WaitUntilAsync(() => Volatile.Read(ref ended) > endedAtHold, "a callback queued before close", token);
            sink.Release(1);
            await sink.EnteredAsync(2, token);
            await SessionProbe.WaitUntilCaptureClosedAsync(session, token);
            int startedAtClose = Volatile.Read(ref started);
            await LateCaptureTestSupport.WaitUntilAsync(
                () => Volatile.Read(ref ended) > startedAtClose, "a callback started after capture closed", token);
            sink.Release(2);
            completion = await completionTask.WaitAsync(TimeSpan.FromSeconds(20), token);
        }
        finally
        {
            sink.ReleaseAll();
        }

        string codes = string.Join(",", completion.Diagnostics.Select(item => item.Code));
        if (completion.IsComplete && codes.Length == 0)
        {
            ReportOutcome("T2 outcome: complete");
            return;
        }

        // Decision 6755339632: librdkafka may emit a duplicate broker key against the unreachable
        // bootstrap address. That outcome is accepted and labelled; it is not a pass of the late-callback rule.
        string[] accepted = ["duplicate_property", "invalid_or_missing_field"];
        string[] actual = completion.Diagnostics.Select(item => item.Code).ToArray();
        Assert.True(
            !completion.IsComplete && actual.Length > 0 && actual.All(code => accepted.Contains(code, StringComparer.Ordinal)),
            $"Unexpected completion: IsComplete={completion.IsComplete}, diagnostics '{codes}'.");
        ReportOutcome(actual.Contains("duplicate_property", StringComparer.Ordinal)
            ? "T2 outcome: accepted, duplicate_property, not a pass"
            : "T2 outcome: accepted, invalid_or_missing_field (cause not identified), not a pass");
    }

    // The runner hides the output of passing tests, so a repeated run can set this variable to a
    // file path and count each outcome by script.
    private static void ReportOutcome(string outcome)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(outcome);
        string? path = Environment.GetEnvironmentVariable("PARCHWELL_T2_OUTCOME_FILE");
        if (!string.IsNullOrEmpty(path))
        {
            File.AppendAllText(path, outcome + Environment.NewLine);
        }
    }
}
