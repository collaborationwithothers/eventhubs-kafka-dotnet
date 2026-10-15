using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;

namespace Parchwell.Kafka.Tests.PhantomSpike;

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
