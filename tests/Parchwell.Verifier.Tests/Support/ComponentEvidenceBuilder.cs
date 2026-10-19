using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Parchwell.Verifier.Tests.Component;


/// <summary>
/// The Toxiproxy toxic applied downstream between the producer and the broker, or None for a
/// healthy control. These are TCP faults, not Kafka acknowledgement selectors
/// (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal enum SpikeFault { None, Latency, Timeout, ResetPeer }

/// <summary>
/// The frozen timings every trial must use. Approved estimates dated 2026-10-14, not measurements
/// (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal sealed record TimingSettings(int MessageTimeoutMs = 5000, int SocketTimeoutMs = 2000, int LatencyMs = 10000);

/// <summary>One cell of the approved matrix: a retries value, a fault and a repeat number.</summary>
internal sealed record TrialConfiguration(int Retries, SpikeFault Fault, int Repeat);

/// <summary>
/// The message one Produce call submits. Instance and attempt IDs travel as headers so a callback
/// or a consumed record can be attributed to this exact attempt (ADR-012 identity join).
/// </summary>
internal sealed record AttemptMessage(string Topic, string InstanceId, string AttemptId, byte[] Key, byte[] Payload);

internal sealed record HeaderObservation(string Key, byte[]? Value);

/// <summary>A delivery callback as the producer reported it, with its persistence status and the returned bytes.</summary>
internal sealed record DeliveryObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    bool IsError, string ErrorCode, string ErrorReason, string PersistenceStatus, string InstanceId, string AttemptId,
    byte[]? Key = null, byte[]? Payload = null, IReadOnlyList<HeaderObservation>? Headers = null);

/// <summary>A record read by the independent observer, which reads the broker directly, not through the proxy.</summary>
internal sealed record ConsumedObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    string? InstanceId, string? AttemptId, byte[] Key, byte[] Payload, IReadOnlyList<HeaderObservation>? Headers = null);

/// <summary>
/// A bounded read of one partition from Start up to the End high watermark captured when the scan
/// began. Complete is true only when Position reached End; an empty poll never counts.
/// </summary>
internal sealed record ObservationScan(long Start, long End, long Position, bool Complete,
    IReadOnlyList<ConsumedObservation> Records, string? Failure);

internal sealed record ProducerDiagnostics(string ManagedVersion, string NativeVersion,
    IReadOnlyList<string> Logs, IReadOnlyList<string> Statistics, IReadOnlyList<string> Errors);

internal sealed record InfrastructureDiagnostics(string ProxyState, string Metrics, string KafkaLogs, string ProxyLogs,
    string? MetricsBeforeTarget = null, IReadOnlyDictionary<string, string>? ContainerIds = null);

/// <summary>
/// Everything retained for one trial. Executed is false for placeholder entries written when the
/// matrix stopped early; those are not observations and carry no reproduction rate.
/// </summary>
internal sealed record TrialEvidence(string TrialId, TrialConfiguration Configuration, TimingSettings Timings,
    AttemptMessage Target, bool Accepted, string? SynchronousError, IReadOnlyList<DeliveryObservation> Callbacks,
    ObservationScan? Scan, IReadOnlyList<string> AdvertisedBrokers, string? FaultState,
    ProducerDiagnostics? Producer, InfrastructureDiagnostics? Infrastructure,
    IReadOnlyList<string> Failures, bool CleanupSucceeded, DateTimeOffset Started, DateTimeOffset Sealed,
    string? ExpectedProducerEndpoint = null, bool RouteVerified = false, bool FaultVerified = false,
    bool HealthyControlVerified = false, bool IndependentObserver = false,
    DateTimeOffset? FaultActivated = null, DateTimeOffset? Submitted = null,
    AttemptMessage? WarmupTarget = null, IReadOnlyList<DeliveryObservation>? WarmupCallbacks = null,
    ObservationScan? WarmupScan = null, bool Executed = true);

/// <summary>
/// The full record of one spike run, written as experiment.json. ImagePins are the configured image
/// references, not observed image identities (docs/operations/phantom-spike/README.md).
/// </summary>
internal sealed record ExperimentEvidence(string SourceCommit, bool Dirty, DateTimeOffset Started,
    DateTimeOffset Finished, TimingSettings Timings, IReadOnlyList<TrialEvidence> Trials,
    IReadOnlyList<TrialEvidence> CalibrationTrials, IReadOnlyList<string> Failures, bool CleanupSucceeded,
    IReadOnlyDictionary<string, string>? ImagePins = null);


/// <summary>
/// The outcome of one Produce call: whether it was accepted synchronously, and every delivery
/// callback received for it, kept as a list so a duplicate callback stays visible.
/// </summary>
internal sealed record PendingProduce
{
    private readonly ConcurrentQueue<DeliveryObservation> callbacks;

    internal PendingProduce(bool accepted, string? synchronousError,
        Task<DeliveryObservation> completion, ConcurrentQueue<DeliveryObservation> callbacks)
    {
        Accepted = accepted;
        SynchronousError = synchronousError;
        Completion = completion;
        this.callbacks = callbacks;
    }

    public bool Accepted { get; }
    public string? SynchronousError { get; }
    public Task<DeliveryObservation> Completion { get; }
    public IReadOnlyList<DeliveryObservation> Callbacks => callbacks.ToArray();
}

// The real client is the thing under test, not an interchangeable transport.
// Callback and configuration behavior source, pinned v2.16.0:
// https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs
// https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
/// <summary>
/// One trial's producer, using the approved spike configuration (not the Event Hubs baseline) and
/// capturing debug logs, statistics and errors as evidence (docs/operations/phantom-spike/README.md).
/// </summary>
internal sealed class ProducerProbe : IDisposable
{
    private const string InstanceHeader = "instance-id";
    private const string AttemptHeader = "attempt-id";
    private readonly IProducer<byte[], byte[]> producer;
    private readonly TimeProvider clock;
    private readonly ConcurrentQueue<string> logs = new();
    private readonly ConcurrentQueue<string> statistics = new();
    private readonly ConcurrentQueue<string> errors = new();
    private readonly string managedVersion;
    private readonly string nativeVersion;
    private bool disposed;

    internal ProducerProbe(string bootstrapServers, TrialConfiguration config,
        TimingSettings timings, TimeProvider clock)
    {
        this.clock = clock;
        var settings = new ProducerConfig
        {
            BootstrapServers = bootstrapServers, // A01: local fault path, not an Event Hubs baseline.
            ClientId = $"phantom-spike-{Guid.NewGuid():N}", // A01/B10: distinguish fixture clients.
            Acks = Acks.All, // B06: require broker acknowledgement.
            EnableIdempotence = false, // B04: approved local mechanism configuration.
            MessageTimeoutMs = timings.MessageTimeoutMs, // B10: bounded local delivery expiry.
            SocketTimeoutMs = timings.SocketTimeoutMs, // A01/B09: request timeout path.
            EnableDeliveryReports = true, // A01/B10: capture actual attempt outcomes.
            EnableBackgroundPoll = true, // A01/B10: serve actual delivery callbacks.
            DeliveryReportFields = "all", // A01/B10: preserve status and identity evidence.
            StatisticsIntervalMs = 1000, // A01/B09/B10: retain native request diagnostics.
            Debug = "broker,topic,msg,metadata,protocol", // A01/B10: verify the producer route.
        };
        settings.Set("retries", config.Retries.ToString(CultureInfo.InvariantCulture)); // B10: approved native retry comparison.
        producer = new ProducerBuilder<byte[], byte[]>(settings)
            .SetLogHandler((_, entry) => logs.Enqueue(Stamp($"{entry.Level} {entry.Facility} {entry.Message}")))
            .SetStatisticsHandler((_, json) => statistics.Enqueue(json))
            .SetErrorHandler((_, error) => errors.Enqueue(Stamp($"{error.Code} {error.Reason}")))
            .Build();
        var assembly = typeof(ProducerConfig).Assembly;
        managedVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "unknown";
        nativeVersion = Library.VersionString;
    }

    internal PendingProduce ProduceAttempt(AttemptMessage message)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = message.Key.ToArray();
        var payload = message.Payload.ToArray();
        var callbacks = new ConcurrentQueue<DeliveryObservation>();
        var completion = new TaskCompletionSource<DeliveryObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new Message<byte[], byte[]>
        {
            Key = key,
            Value = payload,
            Headers = new Headers
            {
                { InstanceHeader, Encoding.UTF8.GetBytes(message.InstanceId) },
                { AttemptHeader, Encoding.UTF8.GetBytes(message.AttemptId) },
                { "sequence", Encoding.UTF8.GetBytes("1") },
            },
        };
        logs.Enqueue(Stamp($"produce-call {message.InstanceId} {message.AttemptId}"));
        try
        {
            producer.Produce(new TopicPartition(message.Topic, new Partition(0)), target, report =>
            {
                try
                {
                    var observation = new DeliveryObservation(clock.GetUtcNow(), report.Topic,
                        report.Partition.Value, report.Offset.Value, report.Error.IsError,
                        report.Error.Code.ToString(), report.Error.Reason, report.Status.ToString(),
                        ReadIdentity(report.Message?.Headers, InstanceHeader),
                        ReadIdentity(report.Message?.Headers, AttemptHeader),
                        report.Message?.Key?.ToArray(), report.Message?.Value?.ToArray(),
                        report.Message?.Headers?.Select(header =>
                            new HeaderObservation(header.Key, header.GetValueBytes()?.ToArray())).ToArray());
                    callbacks.Enqueue(observation);
                    logs.Enqueue(Stamp($"delivery-callback {message.InstanceId} {message.AttemptId}"));
                    if (report.Message?.Key is not { } returnedKey || !returnedKey.AsSpan().SequenceEqual(key)
                        || report.Message.Value is not { } returnedPayload || !returnedPayload.AsSpan().SequenceEqual(payload))
                    {
                        errors.Enqueue(Stamp($"callback-key-or-payload-conflict {message.InstanceId} {message.AttemptId}"));
                    }
                    completion.TrySetResult(observation);
                }
                catch (Exception exception)
                {
                    // A handler error is diagnostic failure, never a manufactured delivery failure.
                    errors.Enqueue(Stamp($"callback-capture-failed {exception}"));
                    completion.TrySetException(exception);
                }
            });
            logs.Enqueue(Stamp($"produce-returned {message.InstanceId} {message.AttemptId}"));
            return new PendingProduce(true, null, completion.Task, callbacks);
        }
        catch (Exception exception)
        {
            var rejection = Stamp($"produce-synchronous-rejection {exception}");
            errors.Enqueue(rejection);
            completion.TrySetException(exception);
            return new PendingProduce(false, rejection, completion.Task, callbacks);
        }
    }

    internal IReadOnlyList<string> MetadataBrokerEndpoints()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var admin = new DependentAdminClientBuilder(producer.Handle).Build();
        var metadata = admin.GetMetadata(TimeSpan.FromSeconds(5));
        return metadata.Brokers.Select(broker => $"{broker.Host}:{broker.Port}").ToArray();
    }

    internal async Task DrainAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (producer.Flush(TimeSpan.FromMilliseconds(100)) == 0)
            {
                return;
            }
            await Task.Yield();
        }
    }

    internal ProducerDiagnostics SnapshotDiagnostics() => new(managedVersion, nativeVersion,
        logs.ToArray(), statistics.ToArray(), errors.ToArray());

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        producer.Dispose();
    }

    private string Stamp(string value) => $"{clock.GetUtcNow():O} monotonic={clock.GetTimestamp()} {value}";

    private static string ReadIdentity(Headers? headers, string name)
    {
        if (headers is null)
        {
            return string.Empty;
        }
        var matches = headers.Where(header => string.Equals(header.Key, name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 && matches[0].GetValueBytes() is { } bytes
            ? Encoding.UTF8.GetString(bytes) : string.Empty;
    }
}


// This is a test fixture using the real pinned client, not a production client seam.
// APIs: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs
// Admin timeout options: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Admin/CreateTopicsOptions.cs
/// <summary>
/// The independent observer: a consumer on the broker's direct listener, not the proxy, so the
/// producer's fault does not affect what it reads. It scans a fixed offset window and makes no
/// finality claim (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal sealed class DirectObserver : IDisposable
{
    private static readonly TimeSpan QuerySlice = TimeSpan.FromMilliseconds(250);
    private readonly IConsumer<byte[], byte[]> consumer;
    private readonly TopicPartition partition;
    private readonly TimeProvider clock;

    public DirectObserver(string bootstrapServers, string topic, TimeProvider clock)
    {
        this.clock = clock;
        partition = new TopicPartition(topic, new Partition(0));
        consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers, // A01: independent local diagnostic path.
            GroupId = "phantom-spike-direct-observer", // C13: reuse one diagnostic group.
            EnableAutoCommit = false, // C10: explicit bounded assignments, no stored progress.
            EnableAutoOffsetStore = false, // C10: no automatic offset bookkeeping.
            AutoOffsetReset = AutoOffsetReset.Error, // C15: never silently skip an unavailable start.
            IsolationLevel = IsolationLevel.ReadCommitted, // B05: explicit observation isolation.
            EnablePartitionEof = true, // C05/C15: EOF remains diagnostic, not scan completion.
            AllowAutoCreateTopics = false // D01: topics are explicitly created by the fixture.
        }).Build();
    }

    public static async Task CreateTopicAsync(string bootstrapServers, string topic,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers // D01: use the direct local administration path.
        }).Build();
        await admin.CreateTopicsAsync(
            [new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
            new CreateTopicsOptions
            {
                // Planned operational ceiling: the caller's trial token caps the remaining budget.
                RequestTimeout = TimeSpan.FromSeconds(60),
                OperationTimeout = TimeSpan.FromSeconds(60)
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public long GetHighWatermark(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var watermarks = consumer.QueryWatermarkOffsets(partition, QuerySlice);
                cancellationToken.ThrowIfCancellationRequested();
                return watermarks.High.Value;
            }
            catch (KafkaException exception) when (exception.Error.Code is
                ErrorCode.Local_TimedOut or ErrorCode.RequestTimedOut)
            {
                // A query's bounded timeout does not imply that the observation window is complete.
            }
        }
    }

    public Task<ObservationScan> ScanAsync(long start, AttemptMessage target,
        CancellationToken cancellationToken) => Task.Run(() => Scan(start, target, cancellationToken));

    private ObservationScan Scan(long start, AttemptMessage target, CancellationToken cancellationToken)
    {
        var records = new List<ConsumedObservation>();
        long end = -1;
        long position = start;
        try
        {
            if (!string.Equals(target.Topic, partition.Topic, StringComparison.Ordinal))
            {
                return new ObservationScan(start, end, position, false, records,
                    "Observer topic and attempt topic differ.");
            }

            if (start < 0)
            {
                return new ObservationScan(start, end, position, false, records,
                    "The start offset must be explicit and non-negative.");
            }

            end = GetHighWatermark(cancellationToken);
            if (end < start)
            {
                return new ObservationScan(start, end, position, false, records,
                    "The end watermark precedes the start offset.");
            }

            consumer.Assign(new TopicPartitionOffset(partition, new Offset(start)));
            while (position < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var consumed = consumer.Consume(QuerySlice);
                if (consumed is not null && !consumed.IsPartitionEOF)
                {
                    // Preserve every record in the fixed window, including nonmatching identities.
                    if (consumed.Offset.Value >= start && consumed.Offset.Value < end)
                    {
                        var headers = consumed.Message.Headers?.Select(header =>
                            new HeaderObservation(header.Key, header.GetValueBytes()?.ToArray())).ToArray()
                            ?? [];
                        records.Add(new ConsumedObservation(clock.GetUtcNow(), consumed.Topic,
                            consumed.Partition.Value, consumed.Offset.Value,
                            ReadIdentity(headers, "instance-id"), ReadIdentity(headers, "attempt-id"),
                            consumed.Message.Key, consumed.Message.Value, headers));
                    }
                }

                var current = consumer.Position(partition);
                if (current != Offset.Unset)
                {
                    position = current.Value;
                }
                // Empty polls and EOF events never substitute for reaching the captured end.
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ObservationScan(start, end, position, true, records, null);
        }
        catch (OperationCanceledException exception)
        {
            return new ObservationScan(start, end, position, false, records, exception.Message);
        }
        catch (KafkaException exception)
        {
            return new ObservationScan(start, end, position, false, records,
                $"{exception.Error.Code}: {exception.Error.Reason}");
        }
    }

    private static string? ReadIdentity(IReadOnlyList<HeaderObservation> headers, string key)
    {
        var matching = headers.Where(header => string.Equals(header.Key, key, StringComparison.Ordinal))
            .ToArray();
        return matching.Length == 1 && matching[0].Value is { } value
            ? Encoding.UTF8.GetString(value)
            : null;
    }

    public void Dispose() => consumer.Dispose();
}

/// <summary>Projects measured native guard counters into labelled synthetic bookkeeping artifacts.</summary>
internal static class ComponentEvidenceBuilder
{
    internal static long ApplyMeasuredGuardCounters(ComponentRunHarness run, ProducerDiagnostics diagnostics)
    {
        string[] names = ["req_timeouts", "txretries", "txerrs", "rxerrs"];
        var maxima = names.ToDictionary(name => name, _ => 0L, StringComparer.Ordinal);
        foreach (string raw in diagnostics.Statistics)
        {
            using var report = System.Text.Json.JsonDocument.Parse(raw);
            if (!report.RootElement.TryGetProperty("brokers", out var brokers)) continue;
            foreach (var broker in brokers.EnumerateObject())
                foreach (string name in names)
                    if (broker.Value.TryGetProperty(name, out var counter) && counter.TryGetInt64(out long value))
                        maxima[name] = Math.Max(maxima[name], value);
        }
        // Bookkeeping, report timing and broker alias are synthetic. Each counter is copied from
        // the genuine captured native reports, which remain retained beside the full result.
        string[] lines = Encoding.UTF8.GetString(run.Inputs["statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < lines.Length; index++)
        {
            var envelope = System.Text.Json.Nodes.JsonNode.Parse(lines[index])!;
            var native = System.Text.Json.Nodes.JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!;
            foreach (var broker in native["brokers"]!.AsObject())
                foreach (string name in names) broker.Value![name] = maxima[name];
            envelope["raw_json"] = native.ToJsonString();
            if (envelope["typed_snapshot"]?["brokers"] is System.Text.Json.Nodes.JsonArray typed)
                foreach (var broker in typed)
                {
                    broker!["request_timeouts"] = maxima["req_timeouts"];
                    broker["transmission_retries"] = maxima["txretries"];
                    broker["transmission_errors"] = maxima["txerrs"];
                    broker["receive_errors"] = maxima["rxerrs"];
                }
            lines[index] = envelope.ToJsonString();
        }
        run.Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
        return maxima["txretries"];
    }
}
