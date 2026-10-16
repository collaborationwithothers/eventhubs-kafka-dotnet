using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Captures bounded statistics and managed log evidence for one producer run.</summary>
public sealed class RunEvidenceSession : IKafkaStatisticsEvidenceSink, IAsyncDisposable
{
    private const int SchemaVersion = 1;
    private const string StatisticsFileName = "statistics.jsonl";
    private const string LifecycleFileName = "request-lifecycle.jsonl";
    private const string CapabilitiesFileName = "capabilities.json";
    private static readonly string[] KnownMissingLifecycleFields = ["source_event_timestamp", "correlation_id", "source_order"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 32
    };
    private readonly RunIdentity _identity;
    private readonly RunEvidenceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly string _directory;
    private readonly Channel<EvidenceItem> _queue;
    private readonly object _brokerGate = new();
    private readonly object _guardGate = new();
    private readonly HashSet<(string Broker, string Counter)> _guardKeys = [];
    private readonly List<string> _guardObservations = [];
    private readonly ImmutableArray<string> _artifactNames;
    private readonly bool _requestCapabilitySupported;
    private readonly ResolvedRoleConfiguration? _resolvedConfiguration;
    private readonly ConcurrentQueue<string> _diagnostics = new();
    private readonly Task _writerTask;
    private long _receiptSequence;
    private int _diagnosticCount;
    private int _diagnosticsTruncated;
    private int _guardObservationsTruncated;
    private int _faulted;
    private int _completed;
    private ImmutableArray<string> _observedBrokers = [];
    private KafkaStatisticsCompletion? _completion;

    /// <summary>Creates files with create-only semantics and starts the bounded background writer.</summary>
    public RunEvidenceSession(RunIdentity identity, RunEvidenceOptions options, TimeProvider timeProvider,
        ResolvedRoleConfiguration? resolvedConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Validate();
        identity.Validate();
        _identity = identity;
        _options = options;
        _timeProvider = timeProvider;
        _resolvedConfiguration = resolvedConfiguration;
        _directory = Path.GetFullPath(options.DirectoryPath);
        _artifactNames = [StatisticsFileName, LifecycleFileName, CapabilitiesFileName];
        _requestCapabilitySupported = false;
        Directory.CreateDirectory(_directory);
        _queue = Channel.CreateBounded<EvidenceItem>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        try
        {
            using (new FileStream(Path.Combine(_directory, StatisticsFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            using (new FileStream(Path.Combine(_directory, LifecycleFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("Evidence artifacts must be new for each run.", exception);
        }
        _writerTask = Task.Run(WriterAsync);
    }

    /// <summary>Gets the artifact names owned by this session.</summary>
    public ImmutableArray<string> ArtifactNames => _artifactNames;

    /// <summary>True when capture, queueing, or durable writing has failed.</summary>
    public bool IsFaulted => Volatile.Read(ref _faulted) != 0;

    /// <summary>Gets a bounded snapshot of sticky evidence failures and diagnostics.</summary>
    public ImmutableArray<string> Diagnostics
    {
        get
        {
            ImmutableArray<string> retained = _diagnostics.ToArray().ToImmutableArray();
            if (Volatile.Read(ref _diagnosticsTruncated) == 0) return retained;
            return retained.Take(Math.Max(0, _options.MaximumDiagnostics - 1)).Append("diagnostics_truncated").ToImmutableArray();
        }
    }

    /// <summary>Request identity and complete request lifecycle attribution are not established by these sources.</summary>
    public bool RequestCapabilitySupported => _requestCapabilitySupported;

    /// <summary>Gets broker names seen in received typed statistics reports; this is not an expected inventory.</summary>
    public ImmutableArray<string> GetObservedBrokerNames()
    {
        lock (_brokerGate)
        {
            return _observedBrokers;
        }
    }

    /// <summary>Gets sticky positive statistics guard observations, never cleared by a later zero or reset.</summary>
    public ImmutableArray<string> GuardObservations
    {
        get
        {
            lock (_guardGate)
            {
                return _guardObservations.ToImmutableArray();
            }
        }
    }

    /// <summary>Whether every positive statistics guard observation fit in the bounded retained set.</summary>
    public bool GuardObservationsComplete => Volatile.Read(ref _guardObservationsTruncated) == 0;

    /// <summary>Queues one immutable report and its exact original native JSON for durable persistence.</summary>
    public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (Volatile.Read(ref _completed) != 0)
        {
            Latch("statistics_report_after_completion");
            throw new InvalidOperationException("The evidence session is complete.");
        }
        if (report.Identity is null || report.RawJson is null || string.IsNullOrWhiteSpace(report.SessionId) || !IdentityMatches(report.Identity))
        {
            Latch("statistics_report_identity_mismatch");
            throw new InvalidOperationException("Statistics report identity does not match the evidence session.");
        }
        if (Encoding.UTF8.GetByteCount(report.RawJson) > _options.MaximumReportBytes)
        {
            Latch("statistics_report_exceeds_byte_limit");
            throw new InvalidOperationException("Statistics report exceeds its configured byte limit.");
        }
        if (report.Snapshot is not null)
        {
            ImmutableArray<string> names = report.Snapshot.Brokers.Select(broker => broker.Name)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
            lock (_brokerGate)
            {
                _observedBrokers = _observedBrokers.AddRange(names.Except(_observedBrokers, StringComparer.Ordinal));
            }
            foreach (KafkaBrokerStatistics broker in report.Snapshot.Brokers)
            {
                RecordPositiveGuard(broker.Name, "req_timeouts", broker.RequestTimeouts);
                RecordPositiveGuard(broker.Name, "txretries", broker.TransmissionRetries);
                RecordPositiveGuard(broker.Name, "txerrs", broker.TransmissionErrors);
                RecordPositiveGuard(broker.Name, "rxerrs", broker.ReceiveErrors);
            }
        }

        try
        {
            await _queue.Writer.WriteAsync(new StatisticsItem(report), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ChannelClosedException or OperationCanceledException)
        {
            Latch("statistics_report_queue_failed");
            throw;
        }
    }

    /// <summary>Captures a managed log callback without waiting, performing I/O, or throwing.</summary>
    public void CaptureLog(LogMessage message)
    {
        if (message is null || Volatile.Read(ref _completed) != 0)
        {
            Latch("log_capture_unavailable");
            return;
        }

        try
        {
            if (message.Name is null || message.Facility is null || message.Message is null)
            {
                Latch("lifecycle_capture_has_null_field");
                return;
            }
            long characterCount = (long)message.Name.Length + message.Facility.Length + message.Message.Length +
                _identity.ScenarioId.Length + _identity.ConfigurationId.Length + _identity.RunId.Length +
                _identity.InstanceId.Length + _identity.ExpectedInstanceId.Length + _identity.ClientId.Length +
                "managed_log_callbacknative_logmanaged_log_callback".Length;
            long maximumSafeCharacters = Math.Max(0, (_options.MaximumReportBytes - 512L) / 6L);
            if (characterCount > maximumSafeCharacters)
            {
                Latch("lifecycle_capture_exceeds_character_budget");
                return;
            }
            var log = new LogCapture(
                Interlocked.Increment(ref _receiptSequence),
                _timeProvider.GetUtcNow(),
                _identity,
                message.Name,
                message.Level.ToString(),
                message.Facility,
                message.Message);
            if (!_queue.Writer.TryWrite(new LogItem(log)))
            {
                Latch("lifecycle_capture_overflow");
            }
        }
        catch
        {
            Latch("lifecycle_capture_failed");
        }
    }

    /// <summary>Captures one managed native-client error callback without waiting or performing I/O.</summary>
    public void CaptureError(Error error)
    {
        if (error is null || Volatile.Read(ref _completed) != 0)
        {
            Latch("error_capture_unavailable");
            return;
        }

        try
        {
            long identityCharacters = _identity.ScenarioId.Length + _identity.ConfigurationId.Length +
                _identity.RunId.Length + _identity.InstanceId.Length + _identity.ExpectedInstanceId.Length +
                _identity.ClientId.Length;
            if (identityCharacters + (error.Reason?.Length ?? 0) > Math.Max(0, (_options.MaximumReportBytes - 512L) / 6L))
            {
                Latch("error_capture_exceeds_character_budget");
                return;
            }
            var capture = new ErrorCapture(Interlocked.Increment(ref _receiptSequence), _timeProvider.GetUtcNow(),
                _identity, (int)error.Code, error.Code.ToString(), error.Reason, error.IsError, error.IsFatal);
            if (!_queue.Writer.TryWrite(new ErrorItem(capture)))
            {
                Latch("error_capture_overflow");
                return;
            }
            if (error.IsError)
            {
                lock (_guardGate)
                {
                    if (_guardObservations.Contains("native_error_callback_observed", StringComparer.Ordinal)) return;
                    if (_guardObservations.Count < _options.MaximumDiagnostics)
                        _guardObservations.Add("native_error_callback_observed");
                    else
                    {
                        LatchGuardObservationTruncation();
                    }
                }
            }
        }
        catch
        {
            Latch("error_capture_failed");
        }
    }

    /// <summary>Retains a bounded sticky diagnostic. Overflow permanently faults the session.</summary>
    public void RecordDiagnostic(string diagnostic)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(diagnostic))
            {
                Latch("invalid_diagnostic");
                return;
            }
            int count = Interlocked.Increment(ref _diagnosticCount);
            if (count > _options.MaximumDiagnostics)
            {
                Interlocked.Exchange(ref _faulted, 1);
                Interlocked.Exchange(ref _diagnosticsTruncated, 1);
                return;
            }
            _diagnostics.Enqueue(diagnostic.Length > 512 ? diagnostic[..512] : diagnostic);
        }
        catch
        {
            Interlocked.Exchange(ref _faulted, 1);
        }
    }

    /// <summary>Stops capture, drains within the configured budget, and writes the capability/completion report.</summary>
    public async Task CompleteAsync(KafkaStatisticsCompletion? completion, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }
        _completion = completion;
        _queue.Writer.TryComplete();
        using var timeout = new CancellationTokenSource(_options.CompletionTimeout, _timeProvider);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await _writerTask.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Latch("evidence_writer_completion_timeout");
        }
        catch (OperationCanceledException)
        {
            Latch("evidence_completion_cancelled");
        }
        catch
        {
            Latch("evidence_writer_failed");
        }

        try
        {
            await WriteCapabilitiesAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            string diagnostic = exception is OperationCanceledException && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? "evidence_writer_completion_timeout"
                : exception is OperationCanceledException ? "capabilities_write_cancelled" : "capabilities_write_failed";
            Latch(diagnostic);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _completed) == 0)
        {
            await CompleteAsync(null, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Validates the closed persisted evidence under this session's identity and byte limits.</summary>
    public async Task ValidatePersistedAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Volatile.Read(ref _completed) == 0 || !_writerTask.IsCompletedSuccessfully)
                throw new InvalidDataException("evidence_writer_not_successfully_closed");
            _ = await RunEvidenceReader.ReadAsync(_directory, _identity, _options.MaximumFileBytes,
                (int)Math.Min(_options.MaximumFileBytes, Math.Min(int.MaxValue, (long)_options.MaximumReportBytes + 4096)),
                _options.MaximumDiagnostics, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Latch("persisted_evidence_validation_failed");
            throw;
        }
    }

    private async Task WriterAsync()
    {
        try
        {
            string statisticsPath = Path.Combine(_directory, StatisticsFileName);
            string lifecyclePath = Path.Combine(_directory, LifecycleFileName);
            await using var statistics = new FileStream(statisticsPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var lifecycle = new FileStream(lifecyclePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            long statisticsBytes = 0;
            long lifecycleBytes = 0;
            await foreach (EvidenceItem item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                byte[] bytes;
                FileStream target;
                if (item is StatisticsItem statisticsItem)
                {
                    bytes = SerializeStatistics(statisticsItem.Report);
                    target = statistics;
                    statisticsBytes = checked(statisticsBytes + bytes.Length);
                    if (statisticsBytes > _options.MaximumFileBytes) throw new IOException("statistics_file_byte_limit_exceeded");
                }
                else if (item is LogItem logItem)
                {
                    bytes = SerializeLog(logItem.Capture);
                    if (bytes.Length > _options.MaximumReportBytes) throw new IOException("lifecycle_record_byte_limit_exceeded");
                    target = lifecycle;
                    lifecycleBytes = checked(lifecycleBytes + bytes.Length);
                    if (lifecycleBytes > _options.MaximumFileBytes) throw new IOException("lifecycle_file_byte_limit_exceeded");
                }
                else if (item is ErrorItem errorItem)
                {
                    bytes = SerializeError(errorItem.Capture);
                    if (bytes.Length > _options.MaximumReportBytes) throw new IOException("lifecycle_record_byte_limit_exceeded");
                    target = lifecycle;
                    lifecycleBytes = checked(lifecycleBytes + bytes.Length);
                    if (lifecycleBytes > _options.MaximumFileBytes) throw new IOException("lifecycle_file_byte_limit_exceeded");
                }
                else
                {
                    continue;
                }
                await target.WriteAsync(bytes).ConfigureAwait(false);
                await target.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Latch("evidence_writer_io_failed:" + exception.GetType().Name);
            _queue.Writer.TryComplete(exception);
            throw;
        }
    }

    private byte[] SerializeStatistics(KafkaStatisticsReport report)
    {
        string line = JsonSerializer.Serialize(new
        {
            schema_version = SchemaVersion,
            identity = _identity,
            statistics_identity = report.Identity,
            session_id = report.SessionId,
            received_at = report.ReceivedAt,
            receipt_time = _timeProvider.GetUtcNow(),
            raw_json = report.RawJson,
            typed_snapshot = report.Snapshot,
            diagnostics = report.Diagnostics
        }, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        if (bytes.Length > _options.MaximumReportBytes + 4096)
        {
            throw new IOException("statistics_envelope_byte_limit_exceeded");
        }
        return bytes;
    }

    private static byte[] SerializeLog(LogCapture capture)
    {
        string line = JsonSerializer.Serialize(new
        {
            schema_version = SchemaVersion,
            identity = capture.Identity,
            session_id = "managed_log_callback",
            receipt_sequence = capture.ReceiptSequence,
            received_at = capture.ReceivedAt,
            source_event_timestamp = (string?)null,
            correlation_id = (string?)null,
            source_order = (long?)null,
            known_missing_fields = KnownMissingLifecycleFields,
            inference = (string?)null,
            event_type = "native_log",
            native_name = capture.Name,
            level = capture.Level,
            facility = capture.Facility,
            message = capture.Message,
            provenance = "managed_log_callback"
        }, JsonOptions);
        return Encoding.UTF8.GetBytes(line + "\n");
    }

    private static byte[] SerializeError(ErrorCapture capture)
    {
        string line = JsonSerializer.Serialize(new
        {
            schema_version = SchemaVersion,
            identity = capture.Identity,
            session_id = "managed_error_callback",
            receipt_sequence = capture.ReceiptSequence,
            received_at = capture.ReceivedAt,
            event_type = "native_error",
            error_code = capture.ErrorCode,
            error_name = capture.ErrorName,
            reason = capture.Reason,
            is_error = capture.IsError,
            is_fatal = capture.IsFatal,
            provenance = "managed_error_callback"
        }, JsonOptions);
        return Encoding.UTF8.GetBytes(line + "\n");
    }

    private async Task WriteCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var capability = new
        {
            schema_version = SchemaVersion,
            identity = _identity,
            artifacts = ArtifactNames,
            resolved_configuration = _resolvedConfiguration,
            request_lifecycle_requirements = new[]
            {
                Capability("request_identity", "unsupported",
                    ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md",
                     "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1058",
                     "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71"],
                    "Statistics expose aggregate client and broker counters and windows. The timeout logger is capped by log_first_n and its format has no correlation identifier. LogMessage exposes name, level, facility, and message.",
                    "These sources provide no complete unique identity for every request observed by this collector."),
                Capability("matched_response_error_throttle_attribution", "unsupported",
                    ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md"],
                    "The statistics document defines broker throttle windows and cumulative counters.",
                    "Those aggregate fields cannot match one request with a response, error, or throttle event."),
                Capability("applicable_deadline_provenance", "unproven",
                    ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71",
                     "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5109-L5127"],
                    "Managed LogMessage exposes name, level, facility, and message; request source defines a message-lifetime deadline.",
                    "No managed callback field ties an observed event to the applicable request deadline."),
                Capability("native_retransmission", "unsupported",
                    ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5030-L5037"],
                    "The retry path constructs a new request.",
                    "This adapter records no complete request sequence to establish which request is a retransmission."),
                Capability("request_ordering", "unproven",
                    ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278"],
                    "The managed producer log callback is called spontaneously from internal librdkafka threads.",
                    "A local receipt sequence cannot establish native source-event order."),
                Capability("request_lifecycle_completeness", "unsupported",
                    ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278",
                     "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71",
                     "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1058"],
                    "The managed callback is a spontaneous log callback from internal librdkafka threads. Timeout log emission is limited by log_first_n, and LogMessage provides only name, level, facility, and message.",
                    "This capture cannot establish a complete request lifecycle."),
                Capability("broker_instance_lifecycle_inventory", "unproven",
                    ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md"],
                    "Statistics reports broker objects in periodic samples.",
                    "Samples cannot establish broker instances absent between reports.")
            },
            request_capability_supported = false,
            observed_broker_names = GetObservedBrokerNames(),
            guard_observations = GuardObservations,
            guard_observations_complete = GuardObservationsComplete,
            statistics_completion = _completion is null ? null : new
            {
                _completion.IsComplete,
                diagnostics = _completion.Diagnostics,
                missing_brokers = _completion.MissingBrokers
            },
            session_diagnostics = Diagnostics,
            is_faulted = IsFaulted
        };
        string path = Path.Combine(_directory, CapabilitiesFileName);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(capability, JsonOptions);
        if (bytes.LongLength > _options.MaximumFileBytes) throw new IOException("capabilities_file_byte_limit_exceeded");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object Capability(string requirement, string status, string[] sourceUrls, string sourceFacts, string inference) => new
    {
        requirement,
        status,
        pinned_version = "2.16.0",
        source_urls = sourceUrls,
        source_facts = sourceFacts,
        inference,
        local_test_evidence = "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"
    };

    private bool IdentityMatches(KafkaStatisticsIdentity identity) =>
        StringComparer.Ordinal.Equals(identity.ScenarioId, _identity.ScenarioId) &&
        StringComparer.Ordinal.Equals(identity.RunId, _identity.RunId) &&
        identity.Role == KafkaClientRole.LoadGenProducer &&
        StringComparer.Ordinal.Equals(identity.ClientId, _identity.ClientId);

    private void Latch(string diagnostic)
    {
        Interlocked.Exchange(ref _faulted, 1);
        RecordDiagnostic(diagnostic);
    }

    private void LatchGuardObservationTruncation()
    {
        if (Interlocked.Exchange(ref _guardObservationsTruncated, 1) == 0)
            Latch("statistics_guard_observations_truncated");
    }

    private void RecordPositiveGuard(string broker, string counter, long value)
    {
        if (value <= 0) return;
        lock (_guardGate)
        {
            if (_guardKeys.Contains((broker, counter))) return;
            if (_guardObservations.Count >= _options.MaximumDiagnostics)
            {
                LatchGuardObservationTruncation();
                return;
            }
            _guardKeys.Add((broker, counter));
            _guardObservations.Add($"broker={broker};counter={counter};observed={value}");
        }
    }

    private abstract record EvidenceItem;
    private sealed record StatisticsItem(KafkaStatisticsReport Report) : EvidenceItem;
    private sealed record LogItem(LogCapture Capture) : EvidenceItem;
    private sealed record ErrorItem(ErrorCapture Capture) : EvidenceItem;
    private sealed record LogCapture(long ReceiptSequence, DateTimeOffset ReceivedAt, RunIdentity Identity,
        string Name, string Level, string Facility, string Message);
    private sealed record ErrorCapture(long ReceiptSequence, DateTimeOffset ReceivedAt, RunIdentity Identity,
        int ErrorCode, string ErrorName, string? Reason, bool IsError, bool IsFatal);
}
