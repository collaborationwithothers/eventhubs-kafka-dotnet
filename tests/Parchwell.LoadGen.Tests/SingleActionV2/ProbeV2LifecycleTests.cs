using System.Reflection;
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.Scenarios;
using Parchwell.Testing;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2LifecycleTests
{
    private static readonly DateTimeOffset MetadataE0 = new(2030, 1, 1, 1, 0, 0, TimeSpan.Zero);
    private static readonly string[] ScientificCaptureFields = ["schema_version", "mode", "synthetic", "run_id", "scenario_id", "configuration_id", "stage", "source_commit", "job_name", "pod_name", "pod_uid", "service_account", "managed_version", "native_version", "repeats", "handler_exit_code", "cleanup_completed", "host_cleanup_completed"];
    private static readonly string[] ScientificRepeatFields = ["repeat", "admission", "client_id", "action", "terminal", "elapsed_ms", "terminal_unix_ms", "settings", "omitted_keys", "metadata_partitions", "requested", "delivery", "read_observation", "result", "error", "client_errors", "statistics", "diagnostics"];

    [Fact]
    public async Task Given_FiveMaximumSizedNativeStatistics_When_HostEnds_Then_FullCaptureFollowsCleanupAndIsExportable()
    {
        using var cluster = new ProbeV2MockCluster(64);
        cluster.CreateTopic("document-events", 32);
        string raw = await cluster.CaptureProducerStatisticsAsync("document-events", "synthetic-budget-producer", TestContext.Current.CancellationToken);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(raw) > 32768);
        raw += new string(' ', 262144 - System.Text.Encoding.UTF8.GetByteCount(raw));
        using var fixture = new Fixture(statistics: raw);
        Assert.Equal(0, await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs));
        string line = fixture.Output.ToString().Split('\n').Single(value => value.StartsWith("PARCHWELL_PROBE_V2_CAPTURE ", StringComparison.Ordinal));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line["PARCHWELL_PROBE_V2_CAPTURE ".Length..]);
        Assert.InRange(bytes.Length, 1048577, fixture.Options.Limits.MaxCaptureBytes);
        ProbeV2CaptureFile capture = ProbeV2Wire.Read<ProbeV2CaptureFile>(bytes, fixture.Options.Limits.MaxCaptureBytes);
        Assert.True(capture.CleanupCompleted && capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.All(capture.Repeats, row => Assert.Equal(raw, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Assert.Single(row.Statistics).RawBase64))));
        Assert.Equal("capture", fixture.Events[^1]);
        string? directory = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_LARGE_FIXTURE_DIRECTORY");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "actor.stdout"), fixture.Output.ToString(), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "capture.json"), bytes, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
                fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
                "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536), TestContext.Current.CancellationToken);
        }
    }
    [Fact]
    public async Task Given_FiveFreshClients_When_HostEnds_Then_CaptureFollowsStopDispose()
    {
        using var fixture = new Fixture();
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(0, exit);
        Assert.Equal(["registration", "client-1", "client-2", "client-3", "client-4", "client-5", "stop", "dispose", "capture"], fixture.Events);
        Assert.Equal(5, fixture.Clients.Count);
        Assert.All(fixture.Clients, client => { Assert.Equal(1, client.ProduceCalls); Assert.Equal(1, client.DisposeCalls); Assert.IsType<string>(client.Destination); });
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.True(capture.CleanupCompleted);
        Assert.True(capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.Equal([1, 2, 3, 4, 5], capture.Repeats.Select(value => value.Repeat));
        Assert.All(capture.Repeats, value =>
        {
            Assert.Equal("success", value.Result);
            Assert.Equal(31, value.Delivery!.Partition);
            Assert.Equal("Persisted", value.Delivery.PersistenceStatus);
            Assert.Equal(Enumerable.Range(0, 32), value.MetadataPartitions!);
            Assert.Single(value.Statistics);
        });
        Assert.Empty(MetadataLines(fixture));
        Assert.Equal(0, fixture.SamplerCalls);
        string? directory = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "actor.stdout"), fixture.Output.ToString(), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "capture.json"), ProbeV2Wire.Encode(capture, 524288), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
                fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
                "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Given_MetadataOptIn_When_FiveClientsComplete_Then_FiveCorrelatedRowsPreserveServiceCapture()
    {
        using var fixture = new Fixture(captureAuthenticationMetadata: true);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(0, exit);
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.Equal(["registration", "client-1", "client-2", "client-3", "client-4", "client-5", "stop", "dispose", "capture"], fixture.Events);
        Assert.Equal(5, fixture.Clients.Count);
        Assert.All(fixture.Clients, client => { Assert.Equal(1, client.ProduceCalls); Assert.Equal(1, client.DisposeCalls); });
        Assert.True(capture.CleanupCompleted && capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.Equal([1, 2, 3, 4, 5], capture.Repeats.Select(row => row.Repeat));
        Assert.All(capture.Repeats, row =>
        {
            Assert.Equal("success", row.Result);
            Assert.Equal(31, row.Delivery!.Partition);
            Assert.Equal("Persisted", row.Delivery.PersistenceStatus);
            Assert.Single(row.Statistics);
        });
        var lines = MetadataLines(fixture);
        Assert.Equal(5, lines.Count);
        Assert.Equal(10, fixture.SamplerCalls);
        for (int index = 0; index < lines.Count; index++)
        {
            ProbeV2AuthenticationMetadata row = ReadMetadata(lines[index]);
            Assert.Equal(index + 1, row.Repeat);
            Assert.Equal(fixture.Options.JobName, row.JobName);
            Assert.Equal(fixture.Options.PodName, row.PodName);
            Assert.Equal(fixture.Options.SourceCommit, row.SourceCommit);
            Assert.Equal(fixture.Options.Stage, row.Stage);
            Assert.Equal(fixture.Options.PodName + "-r" + (index + 1), row.ClientId);
            Assert.Equal(MetadataE0, row.InitialInstalledExpiresOn);
            Assert.Equal(["before_client", "after_client_cleanup"], row.ProjectedFileSamples.Select(sample => sample.Boundary));
            Assert.True(row.ClientCleanupCompleted);
            Assert.Null(row.CaptureError);
        }
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(capture, ProbeV2Wire.JsonOptions));
        Assert.DoesNotContain(document.RootElement.EnumerateObject(), property => property.Name.Contains("authentication", StringComparison.OrdinalIgnoreCase));
        string? directory = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_METADATA_FIXTURE_DIRECTORY");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "capture.json"), ProbeV2Wire.Encode(capture, 524288), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
                fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
                "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536), TestContext.Current.CancellationToken);
            List<string> stdoutLines =
            [
                "PARCHWELL_PROBE_V2_REGISTRATION " + System.Text.Encoding.UTF8.GetString(ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536))
            ];
            stdoutLines.AddRange(lines);
            stdoutLines.Add("PARCHWELL_PROBE_V2_CAPTURE " + System.Text.Encoding.UTF8.GetString(ProbeV2Wire.Encode(capture, 524288)));
            string stdout = string.Join('\n', stdoutLines) + "\n";
            await File.WriteAllTextAsync(Path.Combine(directory, "actor.stdout"), stdout, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("C6", "seed", "producer")]
    [InlineData("C9", "action", "consumer")]
    public async Task GivenHandlerRefreshPathOptIn_When_FiveSlotsRun_Then_ActualHostOutputCarriesBoundedV2Rows(
        string configuration, string stage, string actor)
    {
        using var fixture = new Fixture(item: "F06", configuration: configuration, stage: stage,
            captureAuthenticationMetadata: true, testHandlerRefreshPath: true);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output,
            logs: fixture.Logs, cancellationToken: fixture.Stopping.Token);
        Assert.Equal(0, exit);
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.Equal(5, fixture.Clients.Count + fixture.ConsumerClients.Count);
        Assert.True(capture.CleanupCompleted && capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.Equal(5, capture.Repeats.Count);
        List<string> lines = MetadataLines(fixture);
        Assert.Equal(5, lines.Count);
        foreach (string line in lines)
        {
            ProbeV2AuthenticationMetadata row = ReadMetadata(line);
            Assert.Equal(2, row.SchemaVersion);
            Assert.Equal("single-action-v2-handler-refresh-path", row.Mode);
            Assert.NotNull(row.HandlerRefresh);
            Assert.True(row.HandlerRefresh.OperationAttempted);
            Assert.Equal(3, row.HandlerRefresh.TriggerAttempt);
            Assert.NotNull(row.HandlerRefresh.BeforeService);
            Assert.NotNull(row.HandlerRefresh.AfterService);
            Assert.True(row.HandlerRefresh.ClientCleanupCompleted);
            Assert.Equal(20000, row.HandlerRefresh.ObservationLimitMs);
            Assert.Equal(5000, row.HandlerRefresh.TestExpiryCapMs);
            Assert.Equal(3, row.Lifecycle!.Events.Count(value => value.Stage == "installation_succeeded"));
            Assert.Contains(row.Lifecycle.Events, value => value.Stage == "installation_succeeded" &&
                value.AcquiredExpiresOn != value.EffectiveExpiresOn && value.TestExpiryCapMilliseconds == 5000);
            if (actor == "producer")
            {
                Assert.Equal(2, fixture.Clients[row.Repeat - 1].ProduceCalls);
                Assert.Equal(7, capture.Repeats[row.Repeat - 1].Delivery!.Offset);
                Assert.Equal("delivery", row.HandlerRefresh.BeforeService.Action);
                Assert.Equal("delivery", row.HandlerRefresh.AfterService.Action);
            }
            else
            {
                Assert.Equal(1, fixture.ConsumerClients[row.Repeat - 1].SeekCalls);
                Assert.Equal(1, fixture.ConsumerClients[row.Repeat - 1].PostSeekConsumeCalls);
                Assert.Equal("read", row.HandlerRefresh.BeforeService.Action);
                Assert.Equal("read", row.HandlerRefresh.AfterService.Action);
                Assert.NotNull(row.HandlerRefresh.SeekReturnedAt);
                Assert.NotNull(capture.Repeats[row.Repeat - 1].ReadObservation.Record);
            }
        }
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(capture, ProbeV2Wire.JsonOptions));
        Assert.DoesNotContain(document.RootElement.EnumerateObject(), property => property.Name.Contains("handler", StringComparison.OrdinalIgnoreCase));
        ExportHandlerRefreshFixture(actor, fixture, capture);
    }

    [Fact]
    public async Task GivenConsumerSeekReturnsAfterObservationCutoff_WhenWorkerRuns_ThenNoPostSeekConsumeIsAdmitted()
    {
        using var fixture = new Fixture(item: "F06", configuration: "C9", stage: "action",
            captureAuthenticationMetadata: true, testHandlerRefreshPath: true,
            advanceClockOnSeekMilliseconds: 20001);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output,
            logs: fixture.Logs, cancellationToken: fixture.Stopping.Token);

        Assert.Equal(0, exit);
        Assert.Equal(5, fixture.ConsumerClients.Count);
        Assert.All(fixture.ConsumerClients, client =>
        {
            Assert.Equal(1, client.SeekCalls);
            Assert.Equal(0, client.PostSeekConsumeCalls);
            Assert.True(client.SessionCancellationToken.IsCancellationRequested);
            Assert.True(client.SessionCancelledAtDispose);
        });
        foreach (string line in MetadataLines(fixture))
        {
            ProbeV2AuthenticationMetadata row = ReadMetadata(line);
            Assert.True(row.HandlerRefresh!.OperationAttempted);
            Assert.NotNull(row.HandlerRefresh.SeekReturnedTimestamp);
            Assert.NotNull(row.HandlerRefresh.StartedTimestamp);
            Assert.True(row.HandlerRefresh.SeekReturnedTimestamp.Value >= row.HandlerRefresh.StartedTimestamp.Value + 20001);
            Assert.Null(row.HandlerRefresh.AfterService);
        }
        TestClock.TimerStub[] timers = fixture.Clock.Timers.Where(value =>
            value.InitialDueTime > TimeSpan.FromSeconds(20) && value.ChangedDueTimes.Contains(TimeSpan.FromSeconds(20))).ToArray();
        Assert.Equal(5, timers.Length);
        Assert.All(timers, timer => Assert.True(timer.IsDisposed));
    }

    [Fact]
    public async Task GivenNonmatchingDiagnosticReadsReachWorkerCap_WhenWorkerRuns_ThenNoFurtherConsumeIsAdmitted()
    {
        using var fixture = new Fixture(item: "F06", configuration: "C9", stage: "action",
            captureAuthenticationMetadata: true, testHandlerRefreshPath: true,
            nonmatchingDiagnosticRead: true);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output,
            logs: fixture.Logs, cancellationToken: fixture.Stopping.Token);

        Assert.Equal(0, exit);
        Assert.All(fixture.ConsumerClients, client =>
        {
            Assert.Equal(4096, client.PostSeekConsumeCalls);
            Assert.Equal(1, client.SeekCalls);
        });
        foreach (string line in MetadataLines(fixture))
        {
            ProbeV2HandlerRefreshMetadata handler = ReadMetadata(line).HandlerRefresh!;
            Assert.Equal(4096, handler.DiagnosticNonmatches);
            Assert.True(handler.CaptureLimitReached);
            Assert.Null(handler.AfterService);
        }
    }

    [Fact]
    public async Task Given_MetadataOptInAndClientCleanupFailure_When_HostEnds_Then_OnlyAttemptedSlotHasIncompleteMetadata()
    {
        using var fixture = new Fixture("client_cleanup", captureAuthenticationMetadata: true);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(1, exit);
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.Equal(1, capture.HandlerExitCode);
        Assert.Single(MetadataLines(fixture));
        ProbeV2AuthenticationMetadata row = ReadMetadata(Assert.Single(MetadataLines(fixture)));
        Assert.False(row.ClientCleanupCompleted);
        Assert.Equal("client_cleanup_incomplete", row.CaptureError);
        Assert.Null(row.ProjectedFileSamples[1].Observation);
        Assert.Null(row.Lifecycle);
        Assert.Null(row.InitialInstalledExpiresOn);
        Assert.Equal(1, fixture.SamplerCalls);
        Assert.All(capture.Repeats.Skip(1), value =>
        {
            Assert.Equal("not_attempted_cleanup", value.Admission);
            Assert.Null(value.ClientId);
            Assert.Empty(value.Settings);
        });
    }

    [Fact]
    public async Task Given_MetadataWriterFailure_When_ServiceControlCompletes_Then_OriginalExitAndServiceEvidenceRemainSuccessful()
    {
        using var fixture = new Fixture(captureAuthenticationMetadata: true, logWriter: new MetadataFailingWriter());
        Assert.Equal(0, await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs));
        Assert.Empty(MetadataLines(fixture));
        Assert.Equal(5, fixture.Clients.Count);
        Assert.All(fixture.Clients, client => { Assert.Equal(1, client.ProduceCalls); Assert.Equal(1, client.DisposeCalls); });
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.True(capture.CleanupCompleted && capture.HostCleanupCompleted);
        Assert.All(capture.Repeats, row => Assert.Equal("success", row.Result));
    }

    [Fact]
    public async Task Given_CancellationAfterFirstDelivery_When_MetadataIsEnabled_Then_OriginalCancellationAndCleanupArePreserved()
    {
        using var disabled = new Fixture("cancellation");
        using var enabled = new Fixture("cancellation", captureAuthenticationMetadata: true);
        Assert.Equal(1, await ProbeV2Host.RunAsync(hostFactory: () => disabled.Host, output: disabled.Output, logs: disabled.Logs));
        Assert.Equal(1, await ProbeV2Host.RunAsync(hostFactory: () => enabled.Host, output: enabled.Output, logs: enabled.Logs));

        ProbeV2CaptureFile disabledCapture = Capture(disabled);
        ProbeV2CaptureFile enabledCapture = Capture(enabled);
        Assert.Single(disabled.Clients);
        Assert.Single(enabled.Clients);
        Assert.Equal(1, disabled.Clients[0].ProduceCalls);
        Assert.Equal(1, enabled.Clients[0].ProduceCalls);
        Assert.Equal(1, disabled.Clients[0].DisposeCalls);
        Assert.Equal(1, enabled.Clients[0].DisposeCalls);
        Assert.Equal(1, disabledCapture.HandlerExitCode);
        Assert.Equal(1, enabledCapture.HandlerExitCode);
        Assert.True(disabledCapture.CleanupCompleted && disabledCapture.HostCleanupCompleted);
        Assert.True(enabledCapture.CleanupCompleted && enabledCapture.HostCleanupCompleted);
        Assert.Equal(5, disabledCapture.Repeats.Count);
        Assert.Equal(5, enabledCapture.Repeats.Count);
        Assert.Equal("success", disabledCapture.Repeats[0].Result);
        Assert.Equal("success", enabledCapture.Repeats[0].Result);
        ProbeV2Delivery disabledDelivery = Assert.IsType<ProbeV2Delivery>(disabledCapture.Repeats[0].Delivery);
        ProbeV2Delivery enabledDelivery = Assert.IsType<ProbeV2Delivery>(enabledCapture.Repeats[0].Delivery);
        Assert.Equal(31, disabledDelivery.Partition);
        Assert.Equal(31, enabledDelivery.Partition);
        Assert.Equal("Persisted", disabledDelivery.PersistenceStatus);
        Assert.Equal("Persisted", enabledDelivery.PersistenceStatus);
        Assert.Equal(4, disabledCapture.Repeats.Skip(1).Count());
        Assert.Equal(4, enabledCapture.Repeats.Skip(1).Count());
        Assert.All(disabledCapture.Repeats.Skip(1).Concat(enabledCapture.Repeats.Skip(1)), row => Assert.Equal("not_attempted_cleanup", row.Admission));
        Assert.Equal(JsonSerializer.Serialize(disabledCapture, ProbeV2Wire.JsonOptions), JsonSerializer.Serialize(enabledCapture, ProbeV2Wire.JsonOptions));
        using (JsonDocument scientific = JsonDocument.Parse(JsonSerializer.Serialize(disabledCapture, ProbeV2Wire.JsonOptions)))
            Assert.Equal(ScientificCaptureFields,
                scientific.RootElement.EnumerateObject().Select(property => property.Name));
        using (JsonDocument repeat = JsonDocument.Parse(JsonSerializer.Serialize(disabledCapture.Repeats[0], ProbeV2Wire.JsonOptions)))
            Assert.Equal(ScientificRepeatFields,
                repeat.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Empty(MetadataLines(disabled));
        Assert.Equal(0, disabled.SamplerCalls);
        var metadataLines = MetadataLines(enabled);
        ProbeV2AuthenticationMetadata row = ReadMetadata(Assert.Single(metadataLines));
        Assert.Equal(1, row.Repeat);
        Assert.True(row.ClientCleanupCompleted);
        Assert.Null(row.CaptureError);
        Assert.Equal(MetadataE0, row.InitialInstalledExpiresOn);
        Assert.Equal("statistics:OperationCanceledException", enabledCapture.Repeats[0].Diagnostics[^1]);
        Assert.Equal(2, enabled.SamplerCalls);
        Assert.Equal(["before_client", "after_client_cleanup"], row.ProjectedFileSamples.Select(sample => sample.Boundary));
    }

    [Theory]
    [InlineData("registration", 0)]
    [InlineData("stop", 5)]
    [InlineData("dispose", 5)]
    [InlineData("client_cleanup", 1)]
    public async Task Given_RegistrationOrCleanupFailure_When_HostEnds_Then_NoSuccessfulCapture(string failure, int expectedClients)
    {
        using var fixture = new Fixture(failure);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(1, exit);
        Assert.Equal(expectedClients, fixture.Clients.Count);
        if (failure == "registration") Assert.DoesNotContain("PARCHWELL_PROBE_V2_CAPTURE ", fixture.Output.ToString(), StringComparison.Ordinal);
        else
        {
            ProbeV2CaptureFile capture = Capture(fixture);
            Assert.Equal(1, capture.HandlerExitCode);
            Assert.False(capture.CleanupCompleted && capture.HostCleanupCompleted);
            if (failure == "client_cleanup") Assert.All(capture.Repeats.Skip(1), value =>
            {
                Assert.Equal("not_attempted_cleanup", value.Admission);
                Assert.Null(value.ClientId);
                Assert.Empty(value.Settings);
            });
        }
    }

    [Fact]
    public async Task Given_BrokerRejection_When_ReceiptCleanupSucceed_Then_CaptureIsSuccessfulEvidenceOfError()
    {
        using var fixture = new Fixture("broker", "F06", "C2");
        Assert.Equal(0, await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs));
        Assert.All(Capture(fixture).Repeats, value =>
        {
            Assert.Equal("error", value.Result);
            Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, value.Error!.Code);
            Assert.Null(value.Delivery);
        });
    }

    private static ProbeV2CaptureFile Capture(Fixture fixture)
    {
        string line = fixture.Output.ToString().Split('\n').Single(value => value.StartsWith("PARCHWELL_PROBE_V2_CAPTURE ", StringComparison.Ordinal));
        return ProbeV2Wire.Read<ProbeV2CaptureFile>(System.Text.Encoding.UTF8.GetBytes(line["PARCHWELL_PROBE_V2_CAPTURE ".Length..]), 524288);
    }
    private static List<string> MetadataLines(Fixture fixture) => (fixture.Logs.ToString() ?? string.Empty).Split('\n')
        .Where(line => line.StartsWith(ProbeV2AuthenticationMetadataWriter.Marker, StringComparison.Ordinal)).ToList();
    private static ProbeV2AuthenticationMetadata ReadMetadata(string line) => ProbeV2Wire.Read<ProbeV2AuthenticationMetadata>(
        System.Text.Encoding.UTF8.GetBytes(line[ProbeV2AuthenticationMetadataWriter.Marker.Length..]), 262144);
    private static void ExportHandlerRefreshFixture(string actor, Fixture fixture, ProbeV2CaptureFile capture)
    {
        string? root = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_HANDLER_REFRESH_FIXTURE_DIRECTORY");
        if (root is null) return;
        string directory = Path.Combine(root, actor);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "actor.stdout"), fixture.Output + fixture.Logs.ToString());
        File.WriteAllBytes(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536));
        File.WriteAllBytes(Path.Combine(directory, "capture.json"), ProbeV2Wire.Encode(capture, 524288));
        File.WriteAllBytes(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
            fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
            "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536));
    }
    private sealed class Fixture : IDisposable
    {
        internal List<string> Events { get; } = [];
        internal List<NativeProducer> Clients { get; } = [];
        internal List<NativeConsumer> ConsumerClients { get; } = [];
        internal List<KafkaAuthenticationMetrics> Metrics { get; } = [];
        internal ProbeV2Options Options { get; }
        internal TestClock Clock { get; } = new();
        internal RecordingWriter Output { get; }
        internal TextWriter Logs { get; }
        internal int SamplerCalls { get; private set; }
        internal CancellationTokenSource Stopping { get; } = new();
        internal TestHost Host { get; }
        internal Fixture(string? failure = null, string item = "B08", string configuration = "C0", string? statistics = null,
            bool captureAuthenticationMetadata = false, TextWriter? logWriter = null, string stage = "action",
            bool testHandlerRefreshPath = false, int advanceClockOnSeekMilliseconds = 0,
            bool nonmatchingDiagnosticRead = false)
        {
            Options = ProbeV2OptionsTests.Options(item, configuration, stage);
            Options.CaptureAuthenticationMetadata = captureAuthenticationMetadata || testHandlerRefreshPath;
            Options.TestHandlerRefreshPath = testHandlerRefreshPath;
            string root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
            Options.ScenarioRoot = root;
            Output = new(Events, failure);
            Logs = logWriter ?? new StringWriter();
            TestClock clock = Clock;
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ProbeV2Options>>(Microsoft.Extensions.Options.Options.Create(Options));
            services.AddSingleton(Options);
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IHostApplicationLifetime>(new Lifetime(Stopping.Token));
            services.AddParchwellScenarios(value =>
            {
                value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32;
                value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000;
            });
            services.AddSingleton<IScenarioSource>(new ProbeV2ScenarioSource(root));
            foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>()) services.AddParchwellKafka(role, value =>
            { value.NamespaceName = Options.NamespaceName; value.ClientId = Options.PodName; value.EnrichmentGroupId = "probe-v2-group"; });
            services.AddSingleton(new ProbeV2SessionFactory((_, capture, cancellation) =>
            {
                IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
                var proxy = (NativeProducer)(object)producer;
                proxy.Capture = capture;
                proxy.Failure = failure;
                proxy.Statistics = statistics;
                proxy.Clock = clock;
                proxy.Stopping = Stopping;
                proxy.RefreshOnDelay = testHandlerRefreshPath;
                if (capture.CaptureAuthenticationMetadata)
                {
                    using var metrics = new KafkaAuthenticationMetrics();
                    IKafkaTokenProvider provider = TestTokenProvider(testHandlerRefreshPath);
                    var session = new KafkaOAuthBearerSession(new(Options.ScenarioId, Options.RunId, KafkaClientRole.LoadGenProducer, settingsClientId(capture, Clients.Count + 1)),
                        "https://synthetic/.default", "synthetic-principal", provider, clock, metrics,
                        cancellation, captureLifecycle: true, testExpiryCapMilliseconds: testHandlerRefreshPath ? 5000 : null);
                    session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic authentication must install."));
                    capture.AttachAuthentication(session);
                    proxy.Session = session;
                    proxy.SessionCancellationToken = cancellation;
                }
                Clients.Add(proxy);
                Events.Add("client-" + Clients.Count);
                return producer;
            }, (_, capture, cancellation) =>
            {
                var session = new KafkaOAuthBearerSession(new(Options.ScenarioId, Options.RunId, KafkaClientRole.VerifierConsumer,
                    Options.PodName + "-r" + (ConsumerClients.Count + 1)), "https://synthetic/.default", "synthetic-principal",
                    TestTokenProvider(testHandlerRefreshPath), clock, AddMetrics(), cancellation,
                    captureLifecycle: true, testExpiryCapMilliseconds: testHandlerRefreshPath ? 5000 : null);
                session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic consumer authentication must install."));
                capture.AttachAuthentication(session);
                IConsumer<byte[], byte[]> consumer = DispatchProxy.Create<IConsumer<byte[], byte[]>, NativeConsumer>();
                var proxy = (NativeConsumer)(object)consumer;
                proxy.Clock = clock; proxy.Capture = capture; proxy.Session = session; proxy.Requested = Options.Stage;
                proxy.RefreshOnSecondConsume = testHandlerRefreshPath;
                proxy.AdvanceClockOnSeekMilliseconds = advanceClockOnSeekMilliseconds;
                proxy.NonmatchingDiagnosticRead = nonmatchingDiagnosticRead;
                proxy.SessionCancellationToken = cancellation;
                proxy.Options = Options; proxy.Stopping = Stopping;
                ConsumerClients.Add(proxy); Events.Add("client-" + (Clients.Count + ConsumerClients.Count));
                return (consumer, session);
            }, (_, _, _) => Enumerable.Range(0, 32).ToArray(),
            () => () => { SamplerCalls++; return new(SamplerCalls, MetadataE0.AddMinutes(SamplerCalls), SamplerCalls, 1000, 1, "observed"); },
            testHandlerRefreshPath));
            services.AddSingleton(new ProbeV2Inputs(Options, clock));
            services.AddSingleton<ProbeV2Worker>();
            Host = new(services.BuildServiceProvider(), Events, failure);
        }
        private KafkaAuthenticationMetrics AddMetrics()
        {
            var metrics = new KafkaAuthenticationMetrics();
            Metrics.Add(metrics);
            return metrics;
        }
        private static IKafkaTokenProvider TestTokenProvider(bool observed) => observed
            ? new WorkloadIdentityTokenProvider(new SyntheticCredential())
            : new SyntheticTokenProvider();
        public void Dispose() { Host.DisposeProvider(); Output.Dispose(); if (Logs is IDisposable disposable) disposable.Dispose(); Stopping.Dispose(); foreach (KafkaAuthenticationMetrics metrics in Metrics) metrics.Dispose(); }
        private string settingsClientId(ProbeV2Capture _, int repeat) => Options.PodName + "-r" + repeat;
    }
    private sealed class RecordingWriter(List<string> events, string? failure) : StringWriter
    {
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            string value = buffer.ToString();
            string kind = value.StartsWith("PARCHWELL_PROBE_V2_REGISTRATION ", StringComparison.Ordinal) ? "registration" : "capture";
            events.Add(kind);
            if (kind == failure) throw new IOException("synthetic_write_failure");
            return base.WriteLineAsync(buffer, cancellationToken);
        }
    }
    private sealed class TestHost(ServiceProvider services, List<string> events, string? failure) : IHost
    {
        public IServiceProvider Services => services;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            events.Add("stop");
            return failure == "stop" ? Task.FromException(new IOException("synthetic_stop_failure")) : Task.CompletedTask;
        }
        public void Dispose()
        {
            events.Add("dispose");
            if (failure == "dispose") throw new IOException("synthetic_dispose_failure");
            services.Dispose();
        }
        internal void DisposeProvider() => services.Dispose();
    }
    private sealed class Lifetime(CancellationToken stopping) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
    public class NativeProducer : DispatchProxy
    {
        internal ProbeV2Capture Capture = null!;
        internal TestClock Clock = null!;
        internal CancellationTokenSource Stopping = null!;
        internal string? Failure;
        internal string? Statistics;
        internal KafkaOAuthBearerSession? Session;
        internal CancellationToken SessionCancellationToken;
        internal bool RefreshOnDelay;
        private bool Refreshed;
        internal int ProduceCalls, DisposeCalls;
        internal object? Destination;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce")
            {
                ProduceCalls++;
                Destination = args![0];
                var message = (Message<byte[], byte[]>)args[1]!;
                bool broker = Failure == "broker";
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new()
                {
                    Topic = broker ? "document-state" : DestinationTopic(args[0], "document-events"),
                    Partition = broker ? Partition.Any : DestinationPartition(args[0], 31),
                    Offset = broker ? Offset.Unset : new Offset(ProduceCalls == 1 ? 7 : 8), Message = message,
                    Status = broker ? PersistenceStatus.NotPersisted : PersistenceStatus.Persisted,
                    Error = new Error(broker ? ErrorCode.TopicAuthorizationFailed : ErrorCode.NoError)
                });
                Clock.OnDelay = () =>
                {
                    if (RefreshOnDelay && Session is not null && !Refreshed)
                    {
                        Refreshed = true;
                        Session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic refresh must install."));
                        Session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic refresh must install."));
                    }
                    Capture.Statistics(Statistics ?? "{\"time\":" + Clock.Milliseconds / 1000 + ",\"ts\":1,\"brokers\":{}}");
                };
                if (Failure == "cancellation") Stopping.Cancel();
                return null;
            }
            if (targetMethod?.Name == "Dispose")
            {
                DisposeCalls++;
                if (Failure == "client_cleanup") throw new IOException("synthetic_cleanup_failure");
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }

        private static string DestinationTopic(object? destination, string fallback) => destination is TopicPartition topicPartition ? topicPartition.Topic : fallback;
        private static Partition DestinationPartition(object? destination, int fallback) => destination is TopicPartition topicPartition ? topicPartition.Partition : new Partition(fallback);
    }

    public class NativeConsumer : DispatchProxy
    {
        internal ProbeV2Capture Capture = null!;
        internal TestClock Clock = null!;
        internal KafkaOAuthBearerSession Session = null!;
        internal ProbeV2Options Options = null!;
        internal CancellationTokenSource Stopping = null!;
        internal string Requested = "";
        internal bool RefreshOnSecondConsume;
        internal int ConsumeCalls, SeekCalls, DisposeCalls;
        internal int PostSeekConsumeCalls;
        internal int AdvanceClockOnSeekMilliseconds;
        internal bool NonmatchingDiagnosticRead;
        internal CancellationToken SessionCancellationToken;
        internal bool SessionCancelledAtDispose;
        internal TopicPartitionOffset? Assigned;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Assign") { Assigned = (TopicPartitionOffset)args![0]!; return null; }
            if (targetMethod?.Name == "Seek")
            {
                Assigned = (TopicPartitionOffset)args![0]!;
                SeekCalls++;
                Clock.Milliseconds += AdvanceClockOnSeekMilliseconds;
                return null;
            }
            if (targetMethod?.Name == "Consume")
            {
                ConsumeCalls++;
                if (SeekCalls > 0) PostSeekConsumeCalls++;
                if (SeekCalls > 0 && NonmatchingDiagnosticRead)
                    return new ConsumeResult<byte[], byte[]>
                    {
                        Topic = "document-other", Partition = new Partition(0), Offset = new Offset(999),
                        Message = new Message<byte[], byte[]> { Key = [], Value = [] }
                    };
                if (RefreshOnSecondConsume && ConsumeCalls == 2)
                {
                    Session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic consumer refresh must install."));
                    Session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic consumer refresh must install."));
                    return null;
                }
                TimeSpan timeout = args![0] is TimeSpan duration ? duration : TimeSpan.FromMilliseconds(100);
                Clock.Milliseconds += (long)Math.Max(1, timeout.TotalMilliseconds);
                if (Assigned is null) return null;
                int repeat = int.Parse(Options.PodName[(Options.PodName.LastIndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                ProbeV2Stage stage = ProbeV2Stage.For(Options.ScenarioId, Options.ConfigurationId, Options.Stage);
                byte[] key = stage.Key(Options, repeat);
                byte[]? value = stage.Value(Options, repeat);
                return new ConsumeResult<byte[], byte[]>
                {
                    Topic = Assigned.Value.Topic, Partition = Assigned.Value.Partition, Offset = Assigned.Value.Offset,
                    Message = new Message<byte[], byte[]> { Key = key, Value = value! }
                };
            }
            if (targetMethod?.Name == "Dispose")
            {
                DisposeCalls++;
                SessionCancelledAtDispose = SessionCancellationToken.IsCancellationRequested;
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) => new("SYNTHETIC_TOKEN_MUST_NOT_APPEAR", MetadataE0);
    }

    private sealed class SyntheticCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("SYNTHETIC_TOKEN_MUST_NOT_APPEAR", MetadataE0);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromException<AccessToken>(new InvalidOperationException("Synthetic credential async path was not expected."));
    }

    private sealed class MetadataFailingWriter : StringWriter
    {
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Span.StartsWith(ProbeV2AuthenticationMetadataWriter.Marker, StringComparison.Ordinal))
                throw new IOException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR");
            return base.WriteLineAsync(buffer, cancellationToken);
        }
    }
}
