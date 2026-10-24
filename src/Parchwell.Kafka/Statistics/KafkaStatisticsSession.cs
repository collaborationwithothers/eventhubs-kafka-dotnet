using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Statistics;

/// <summary>Captures and processes statistics reports for one native Kafka client lifetime.</summary>
public sealed class KafkaStatisticsSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly KafkaStatisticsIdentity _identity;
    private readonly KafkaStatisticsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IKafkaStatisticsEvidenceSink _sink;
    private readonly KafkaStatisticsMetrics _metrics;
    private readonly bool _nativeClockCompatible;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly long _sessionStartTimestamp;
    private readonly Channel<CapturedReport> _queue;
    private readonly CancellationTokenSource _workerCancellation = new();
    private readonly Task _worker;
    private readonly ConcurrentDictionary<string, byte> _diagnosticCodes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _trackedBrokers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _postDrainBrokers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BrokerCounters> _counters = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _coverageReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _captureOpen = true;
    private bool _disposed;
    private bool _completionStarted;
    private bool _completionFinished;
    private bool _coverageComplete;
    private bool _sinkFailed;
    private bool _workerDrained;
    private bool _workerFinished;
    private bool _workerCancellationRequested;
    private bool _workerCancellationCallbacksFinished;
    private bool _workerCancellationDisposed;
    private readonly KafkaStatisticsCompletionState _completionState = new();
    private long? _lastNativeTimestamp;
    private long? _nativeCreationTimestamp;
    private string? _nativeName;
    private long? _completionBoundaryMicroseconds;
    private long? _firstPostDrainTimestampMicroseconds;
    private CompletionMode _completionMode;
    private string[] _expectedBrokers = [];

    internal KafkaStatisticsSession(
        KafkaStatisticsIdentity identity,
        KafkaStatisticsOptions options,
        TimeProvider timeProvider,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics,
        bool nativeClockCompatible)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (!_options.IsValid(out string? failure))
        {
            throw new ArgumentException(failure, nameof(options));
        }

        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _nativeClockCompatible = nativeClockCompatible;
        _sessionStartTimestamp = _timeProvider.GetTimestamp();
        if (!_nativeClockCompatible)
        {
            AddDiagnostic("clock_unverified");
        }

        _queue = Channel.CreateBounded<CapturedReport>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessReportsAsync);
    }

    /// <summary>Records a native callback report without blocking the Kafka callback thread.</summary>
    /// <remarks>
    /// Reports beyond the configured UTF-16 length or queue capacity are rejected and latch
    /// incomplete evidence. A call after the completion freeze is diagnosed as a late capture.
    /// </remarks>
    /// <param name="json">The original librdkafka statistics JSON.</param>
    public void Capture(string json)
    {
        if (!_completionState.TryBeginCapture())
        {
            AddDiagnostic("late_capture");
            return;
        }

        bool entered = false;
        try
        {
            long receiptTimestamp = _timeProvider.GetTimestamp();
            DateTimeOffset receivedAt = _timeProvider.GetUtcNow();
            entered = Monitor.TryEnter(_gate);
            if (!entered)
            {
                _completionState.TryRecordCaptureLoss();
                AddDiagnostic("capture_contention");
                return;
            }

            if (!_captureOpen || _disposed)
            {
                _completionState.TryRecordCaptureLoss();
                AddDiagnostic("late_capture");
                return;
            }

            if (json is null || json.Length > _options.MaxSnapshotCharacters)
            {
                AddDiagnostic("oversized_snapshot");
                return;
            }

            if (!_queue.Writer.TryWrite(new CapturedReport(json, receivedAt, receiptTimestamp)))
            {
                AddDiagnostic("capture_queue_overflow");
            }
        }
        finally
        {
            if (entered)
            {
                Monitor.Exit(_gate);
            }

            _completionState.EndCapture();
        }
    }

    /// <summary>Waits for a retained post-drain report, stops capture, and drains accepted evidence within the configured deadline.</summary>
    /// <param name="cancellationToken">Cancels the wait while preserving an incomplete session result.</param>
    /// <returns>The bounded capture status, diagnostics, and first successfully written post-drain native timestamp.</returns>
    /// <remarks>
    /// Completion requires a validated nonempty broker snapshot generated strictly after the drain
    /// boundary and successfully written to the evidence sink. It proves capture completion only,
    /// not independent broker inventory or the full ADR-013 coverage oracle. MissingBrokers is
    /// empty because this overload accepts no inventory; workers must not infer an inventory from it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Completion has already been requested for this session.</exception>
    public Task<KafkaStatisticsCompletion> CompleteAsync(CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(CompletionMode.CaptureOnly, [], cancellationToken);

    /// <summary>Waits for post-drain samples, stops capture, and drains accepted evidence within the configured deadline.</summary>
    /// <param name="expectedBrokerNames">The bounded broker names whose post-drain samples must be observed.</param>
    /// <param name="cancellationToken">Cancels the wait while preserving an incomplete session result.</param>
    /// <returns>The bounded session diagnostics and names that did not receive a fresh sample.</returns>
    /// <remarks>
    /// A successful result is frozen after native-clock coverage and accepted evidence drain. It
    /// proves this session's captured coverage only; the harness remains responsible for its full
    /// broker lifecycle inventory and the complete ADR-013 eligibility decision.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="expectedBrokerNames"/> is null.</exception>
    /// <exception cref="ArgumentException">Expected names are empty, duplicated, blank or exceed the configured series limit.</exception>
    /// <exception cref="InvalidOperationException">Completion has already been requested for this session.</exception>
    public async Task<KafkaStatisticsCompletion> CompleteAsync(
        IEnumerable<string> expectedBrokerNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedBrokerNames);
        string[] expected = ValidateExpectedBrokers(expectedBrokerNames);
        return await CompleteCoreAsync(CompletionMode.CallerInventory, expected, cancellationToken).ConfigureAwait(false);
    }

    private async Task<KafkaStatisticsCompletion> CompleteCoreAsync(
        CompletionMode mode,
        string[] expected,
        CancellationToken cancellationToken)
    {
        long completionStartTimestamp = _timeProvider.GetTimestamp();
        using CancellationTokenSource deadline = new(_options.CompletionTimeout, _timeProvider);
        bool wasDisposed;
        lock (_gate)
        {
            if (_completionStarted)
            {
                throw new InvalidOperationException("Statistics session completion can be requested only once.");
            }

            _completionStarted = true;
            _completionMode = mode;
            _expectedBrokers = expected;
            _completionBoundaryMicroseconds = _nativeClockCompatible
                ? ToMicrosecondsCeiling(_timeProvider.GetTimestamp(), _timeProvider.TimestampFrequency)
                : null;
            if (_disposed)
            {
                AddDiagnostic("disposed_before_completion");
            }

            wasDisposed = _disposed;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            _completionState.RecordCancellation);
        using CancellationTokenRegistration deadlineRegistration = deadline.Token.Register(
            _completionState.RecordDeadline);

        bool coverageReached = false;
        if (_nativeClockCompatible && !wasDisposed)
        {
            lock (_gate)
            {
                coverageReached = _coverageComplete;
            }

            try
            {
                if (!coverageReached)
                {
                    await _coverageReached.Task.WaitAsync(linked.Token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        coverageReached = _coverageComplete;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    coverageReached = _coverageComplete;
                    AddDiagnostic(cancellationToken.IsCancellationRequested
                        ? "completion_cancelled"
                        : coverageReached ? "sink_drain_timeout" : "completion_timeout");
                }
            }
        }

        CloseCapture();
        try
        {
            await _worker.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                coverageReached = _coverageComplete;
                AddDiagnostic(cancellationToken.IsCancellationRequested
                    ? "completion_cancelled"
                    : coverageReached ? "sink_drain_timeout" : "completion_timeout");
            }

            CancelWorker();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is reported through the completion object so callers can persist the evidence state.
            AddDiagnosticThreadSafe("completion_cancelled");
        }

        return BuildCompletion(completionStartTimestamp, cancellationToken, deadline.Token);
    }

    /// <summary>Stops capture and waits a bounded time for queued reports to finish.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_completionStarted && !_completionFinished)
            {
                AddDiagnostic("disposed_during_completion");
                _coverageReached.TrySetResult();
            }
            else if (!_completionStarted)
            {
                AddDiagnostic("abandoned_without_completion");
            }

            _captureOpen = false;
            _queue.Writer.TryComplete();
        }

        try
        {
            await _worker.WaitAsync(_options.CompletionTimeout, _timeProvider).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            AddDiagnosticThreadSafe("sink_drain_timeout");
            CancelWorker();
        }
        catch (OperationCanceledException)
        {
            CancelWorker();
        }
    }

    private async Task ProcessReportsAsync()
    {
        try
        {
            await foreach (CapturedReport captured in _queue.Reader.ReadAllAsync(_workerCancellation.Token).ConfigureAwait(false))
            {
                _workerCancellation.Token.ThrowIfCancellationRequested();
                await ProcessCapturedReportAsync(captured).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_workerCancellation.IsCancellationRequested)
        {
            // The session has already recorded that its evidence drain was incomplete.
        }
        catch (Exception)
        {
            AddDiagnosticThreadSafe("processing_failure");
        }
        finally
        {
            lock (_gate)
            {
                _workerDrained = true;
                _workerFinished = true;
                TryDisposeWorkerCancellation();
            }
        }
    }

    private async Task ProcessCapturedReportAsync(CapturedReport captured)
    {
        (KafkaStatisticsSnapshot? snapshot, ImmutableArray<KafkaStatisticsDiagnostic> parseDiagnostics) =
            KafkaStatisticsParser.Parse(captured.Json, _options.MaxBrokerSeries);
        List<KafkaStatisticsDiagnostic> reportDiagnostics = new(parseDiagnostics);
        List<MetricEmission> emissions = [];
        bool snapshotValid = false;
        if (!parseDiagnostics.IsEmpty)
        {
            lock (_gate)
            {
                foreach (KafkaStatisticsDiagnostic diagnostic in parseDiagnostics)
                {
                    AddDiagnostic(diagnostic.Code);
                }
            }
        }

        if (snapshot is not null)
        {
            lock (_gate)
            {
                snapshotValid = ValidateSnapshot(snapshot, captured.ReceiptTimestamp, reportDiagnostics);
                if (!snapshotValid)
                {
                    // Invalid identity, generation order or client lifetime must never emit metrics.
                }
                else
                {
                    UpdateCounterState(snapshot, reportDiagnostics, emissions);
                    if (_completionMode == CompletionMode.CallerInventory)
                    {
                        ObservePostDrainCoverage(snapshot);
                    }
                }
            }
        }

        foreach (MetricEmission emission in emissions)
        {
            try
            {
                _metrics.Record(_identity, emission.Broker, emission.RequestTimeoutDelta, emission.DisconnectDelta);
            }
            catch (Exception)
            {
                AddReportDiagnostic("metrics_recording_failure", reportDiagnostics);
            }
        }

        KafkaStatisticsReport report = new(
            _identity,
            _sessionId,
            captured.ReceivedAt,
            captured.Json,
            snapshot,
            reportDiagnostics.ToImmutableArray());

        try
        {
            await _sink.WriteAsync(report, _workerCancellation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_completionMode == CompletionMode.CaptureOnly
                    && !_completionFinished
                    && snapshotValid
                    && snapshot is not null
                    && !snapshot.Brokers.IsEmpty
                    && _nativeClockCompatible
                    && _completionBoundaryMicroseconds is long boundary
                    && snapshot.TimestampMicroseconds > boundary)
                {
                    _firstPostDrainTimestampMicroseconds ??= snapshot.TimestampMicroseconds;
                    _coverageComplete = true;
                    _coverageReached.TrySetResult();
                }
            }
        }
        catch (OperationCanceledException) when (_workerCancellation.IsCancellationRequested)
        {
            lock (_gate)
            {
                _sinkFailed = true;
                AddDiagnostic("evidence_delivery_cancelled");
            }
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _sinkFailed = true;
                AddDiagnostic("evidence_sink_failure");
            }
        }
    }

    private bool ValidateSnapshot(
        KafkaStatisticsSnapshot snapshot,
        long receiptTimestamp,
        List<KafkaStatisticsDiagnostic> reportDiagnostics)
    {
        bool valid = true;
        bool expectedConsumer = _identity.Role is KafkaClientRole.EnrichmentConsumer or KafkaClientRole.VerifierConsumer;
        string expectedType = expectedConsumer ? "consumer" : "producer";
        if (!StringComparer.Ordinal.Equals(snapshot.ClientId, _identity.ClientId))
        {
            AddReportDiagnostic("client_id_mismatch", reportDiagnostics);
            valid = false;
        }

        if (!StringComparer.Ordinal.Equals(snapshot.Type, expectedType))
        {
            AddReportDiagnostic("client_type_mismatch", reportDiagnostics);
            valid = false;
        }

        if (_nativeName is not null && !StringComparer.Ordinal.Equals(snapshot.Name, _nativeName))
        {
            AddReportDiagnostic("client_name_changed", reportDiagnostics);
            valid = false;
        }

        if (_lastNativeTimestamp is long previousTimestamp && snapshot.TimestampMicroseconds <= previousTimestamp)
        {
            AddReportDiagnostic("non_increasing_timestamp", reportDiagnostics);
            valid = false;
        }

        long creationTimestamp = snapshot.TimestampMicroseconds - snapshot.AgeMicroseconds;
        if (_nativeCreationTimestamp is long knownCreation
            && knownCreation != creationTimestamp)
        {
            AddReportDiagnostic("client_instance_changed", reportDiagnostics);
            valid = false;
        }

        if (_nativeClockCompatible)
        {
            long sessionStartMicroseconds = ToMicrosecondsCeiling(_sessionStartTimestamp, _timeProvider.TimestampFrequency);
            if (creationTimestamp < sessionStartMicroseconds)
            {
                AddReportDiagnostic("late_attachment", reportDiagnostics);
                valid = false;
            }

            long receiptMicroseconds = ToMicrosecondsFloor(receiptTimestamp, _timeProvider.TimestampFrequency);
            if (snapshot.TimestampMicroseconds > receiptMicroseconds)
            {
                AddReportDiagnostic("future_native_timestamp", reportDiagnostics);
                valid = false;
            }
        }

        if (valid)
        {
            _nativeName ??= snapshot.Name;
            _lastNativeTimestamp = snapshot.TimestampMicroseconds;
            _nativeCreationTimestamp ??= creationTimestamp;
        }

        return valid;
    }

    private void UpdateCounterState(
        KafkaStatisticsSnapshot snapshot,
        List<KafkaStatisticsDiagnostic> reportDiagnostics,
        List<MetricEmission> emissions)
    {
        foreach (KafkaBrokerStatistics broker in snapshot.Brokers)
        {
            if (!_trackedBrokers.Contains(broker.Name))
            {
                if (_trackedBrokers.Count >= _options.MaxBrokerSeries)
                {
                    AddReportDiagnostic("broker_series_limit_exceeded", reportDiagnostics, broker.Name, "brokers");
                    continue;
                }

                _trackedBrokers.Add(broker.Name);
            }

            if (!_counters.TryGetValue(broker.Name, out BrokerCounters? counters))
            {
                counters = new BrokerCounters();
                _counters.Add(broker.Name, counters);
            }

            long? requestTimeoutDelta = counters.RequestTimeouts.Observe(broker.RequestTimeouts);
            long? disconnectDelta = counters.Disconnects.Observe(broker.Disconnects);
            counters.Retries.Observe(broker.TransmissionRetries);
            counters.TransmissionErrors.Observe(broker.TransmissionErrors);
            counters.ReceiveErrors.Observe(broker.ReceiveErrors);

            if (counters.HasReset)
            {
                AddReportDiagnostic("counter_reset", reportDiagnostics, broker.Name);
            }

            emissions.Add(new MetricEmission(broker, requestTimeoutDelta, disconnectDelta));
        }
    }

    private void ObservePostDrainCoverage(KafkaStatisticsSnapshot snapshot)
    {
        if (!_nativeClockCompatible
            || _completionBoundaryMicroseconds is not long boundary
            || snapshot.TimestampMicroseconds <= boundary)
        {
            return;
        }

        foreach (KafkaBrokerStatistics broker in snapshot.Brokers)
        {
            if (Array.IndexOf(_expectedBrokers, broker.Name) >= 0)
            {
                _postDrainBrokers.Add(broker.Name);
            }
        }

        _coverageComplete = _expectedBrokers.Length > 0
            && _expectedBrokers.All(_postDrainBrokers.Contains);
        if (_coverageComplete)
        {
            _coverageReached.TrySetResult();
        }
    }

    private string[] ValidateExpectedBrokers(IEnumerable<string> expectedBrokerNames)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string? name in expectedBrokerNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Expected broker names must be nonempty.", nameof(expectedBrokerNames));
            }

            if (!names.Add(name))
            {
                throw new ArgumentException("Expected broker names must be unique.", nameof(expectedBrokerNames));
            }

            if (names.Count > _options.MaxBrokerSeries)
            {
                throw new ArgumentException("Expected broker names exceed the configured broker-series limit.", nameof(expectedBrokerNames));
            }
        }

        if (names.Count == 0)
        {
            throw new ArgumentException("At least one expected broker name is required.", nameof(expectedBrokerNames));
        }

        return names.ToArray();
    }

    private void CloseCapture()
    {
        lock (_gate)
        {
            _captureOpen = false;
            _queue.Writer.TryComplete();
        }
    }

    private KafkaStatisticsCompletion BuildCompletion(
        long completionStartTimestamp,
        CancellationToken cancellationToken,
        CancellationToken deadlineToken)
    {
        lock (_gate)
        {
            string[] missing = _expectedBrokers
                .Where(name => !_postDrainBrokers.Contains(name))
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] diagnosticCodes = _diagnosticCodes.Keys.ToArray();

            // Sealing also fails the session if a callback is still registered at this boundary.
            // Final outcome checks follow the seal so cancellation or deadline expiry during the
            // worker-drain-to-seal race cannot be hidden by a diagnostic snapshot.
            _completionState.SealFlags();
            RecordCompletionConditions(completionStartTimestamp, cancellationToken, deadlineToken);

            int sealedFlags = _completionState.SnapshotFlags();
            if ((sealedFlags & KafkaStatisticsCompletionState.CaptureLost) != 0)
            {
                diagnosticCodes = [.. diagnosticCodes, "capture_loss"];
            }

            if ((sealedFlags & KafkaStatisticsCompletionState.CancellationObserved) != 0)
            {
                diagnosticCodes = [.. diagnosticCodes, "completion_cancelled"];
            }

            if ((sealedFlags & KafkaStatisticsCompletionState.DeadlineObserved) != 0)
            {
                diagnosticCodes = [.. diagnosticCodes, "completion_timeout"];
            }

            ImmutableArray<KafkaStatisticsDiagnostic> diagnostics = diagnosticCodes
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(code => new KafkaStatisticsDiagnostic(code))
                .ToImmutableArray();
            bool complete = _nativeClockCompatible
                && _coverageComplete
                && _workerDrained
                && !_sinkFailed
                && diagnostics.IsEmpty
                && missing.Length == 0;
            _completionFinished = true;
            return new KafkaStatisticsCompletion(complete, diagnostics, missing.ToImmutableArray())
            {
                DrainBoundaryMicroseconds = _completionBoundaryMicroseconds,
                FirstPostDrainTimestampMicroseconds = _firstPostDrainTimestampMicroseconds
            };
        }
    }

    private void RecordCompletionConditions(
        long completionStartTimestamp,
        CancellationToken cancellationToken,
        CancellationToken deadlineToken)
    {
        long nowTimestamp = _timeProvider.GetTimestamp();

        if (cancellationToken.IsCancellationRequested)
        {
            _completionState.RecordCancellation();
        }

        if (deadlineToken.IsCancellationRequested
            || _timeProvider.GetElapsedTime(completionStartTimestamp, nowTimestamp) >= _options.CompletionTimeout)
        {
            _completionState.RecordDeadline();
        }
    }

    private void AddReportDiagnostic(
        string code,
        List<KafkaStatisticsDiagnostic> diagnostics,
        string? broker = null,
        string? field = null)
    {
        AddDiagnostic(code);
        if (!diagnostics.Any(item => StringComparer.Ordinal.Equals(item.Code, code)
            && StringComparer.Ordinal.Equals(item.Broker, broker)
            && StringComparer.Ordinal.Equals(item.Field, field)))
        {
            diagnostics.Add(new KafkaStatisticsDiagnostic(code, broker, field));
        }
    }

    private void AddDiagnosticThreadSafe(string code)
    {
        lock (_gate)
        {
            AddDiagnostic(code);
        }
    }

    private void AddDiagnostic(string code) => _diagnosticCodes.TryAdd(code, 0);

    private void CancelWorker()
    {
        CancellationTokenSource source;
        lock (_gate)
        {
            if (_workerCancellationDisposed || _workerCancellationRequested)
            {
                return;
            }

            _workerCancellationRequested = true;
            source = _workerCancellation;
        }

        Task cancellationTask;
        try
        {
            cancellationTask = source.CancelAsync();
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _workerCancellationCallbacksFinished = true;
                AddDiagnostic("worker_cancellation_failure");
                TryDisposeWorkerCancellation();
            }

            return;
        }

        _ = ObserveWorkerCancellationAsync(cancellationTask);
    }

    private async Task ObserveWorkerCancellationAsync(Task cancellationTask)
    {
        try
        {
            await cancellationTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            AddDiagnostic("worker_cancellation_failure");
        }
        finally
        {
            lock (_gate)
            {
                _workerCancellationCallbacksFinished = true;
                TryDisposeWorkerCancellation();
            }
        }
    }

    private void TryDisposeWorkerCancellation()
    {
        if (!_workerCancellationDisposed
            && _workerFinished
            && (!_workerCancellationRequested || _workerCancellationCallbacksFinished))
        {
            _workerCancellationDisposed = true;
            _workerCancellation.Dispose();
        }
    }

    private static long ToMicrosecondsCeiling(long timestamp, long frequency)
    {
        if (frequency <= 0)
        {
            return long.MaxValue;
        }

        // A rounded-up post-drain boundary makes a native timestamp in the same microsecond fail
        // the required strict-greater comparison. Native ts is an integer microsecond clock value
        // generated with rd_clock() while formatting the report (librdkafka v2.16.0 rdkafka.c).
        Int128 numerator = (Int128)timestamp * 1_000_000;
        Int128 converted = numerator >= 0
            ? (numerator + frequency - 1) / frequency
            : numerator / frequency;
        return converted > long.MaxValue ? long.MaxValue
            : converted < long.MinValue ? long.MinValue
            : (long)converted;
    }

    private static long ToMicrosecondsFloor(long timestamp, long frequency)
    {
        if (frequency <= 0)
        {
            return long.MinValue;
        }

        // Native ts is floored to microseconds, so future-generation checks use the floored receipt
        // clock boundary; rounding up here could accept a native time up to one microsecond ahead.
        Int128 converted = (Int128)timestamp * 1_000_000 / frequency;
        return converted > long.MaxValue ? long.MaxValue
            : converted < long.MinValue ? long.MinValue
            : (long)converted;
    }

    private enum CompletionMode { CallerInventory, CaptureOnly }

    private sealed record CapturedReport(string Json, DateTimeOffset ReceivedAt, long ReceiptTimestamp);
    private sealed record MetricEmission(
        KafkaBrokerStatistics Broker,
        long? RequestTimeoutDelta,
        long? DisconnectDelta);

    private sealed class BrokerCounters
    {
        internal CounterSeries RequestTimeouts { get; } = new();
        internal CounterSeries Disconnects { get; } = new();
        internal CounterSeries Retries { get; } = new();
        internal CounterSeries TransmissionErrors { get; } = new();
        internal CounterSeries ReceiveErrors { get; } = new();
        internal bool HasReset => RequestTimeouts.Quarantined || Disconnects.Quarantined
            || Retries.Quarantined || TransmissionErrors.Quarantined || ReceiveErrors.Quarantined;
    }

    private sealed class CounterSeries
    {
        private long? _previous;
        internal bool Quarantined { get; private set; }

        internal long? Observe(long value)
        {
            if (Quarantined)
            {
                return null;
            }

            if (_previous is not long previous)
            {
                _previous = value;
                return value;
            }

            if (value < previous)
            {
                Quarantined = true;
                return null;
            }

            _previous = value;
            return value - previous;
        }
    }
}

internal sealed class KafkaStatisticsCompletionState
{
    private const int Finalized = 1;
    internal const int CaptureLost = 2;
    internal const int CancellationObserved = 4;
    internal const int DeadlineObserved = 8;
    private const int ActiveCaptureIncrement = 16;
    private const int FlagMask = ActiveCaptureIncrement - 1;

    private int _flags;

    internal bool TryBeginCapture()
    {
        while (true)
        {
            int flags = Volatile.Read(ref _flags);
            if ((flags & Finalized) != 0)
            {
                return false;
            }

            if ((flags & ~FlagMask) == int.MaxValue - (int.MaxValue % ActiveCaptureIncrement))
            {
                TryRecordCaptureLoss();
                return false;
            }

            if (Interlocked.CompareExchange(ref _flags, flags + ActiveCaptureIncrement, flags) == flags)
            {
                return true;
            }
        }
    }

    internal void EndCapture()
    {
        while (true)
        {
            int flags = Volatile.Read(ref _flags);
            if ((flags & ~FlagMask) == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _flags, flags - ActiveCaptureIncrement, flags) == flags)
            {
                return;
            }
        }
    }

    internal bool TryRecordCaptureLoss()
    {
        while (true)
        {
            int flags = Volatile.Read(ref _flags);
            if ((flags & Finalized) != 0)
            {
                return false;
            }

            if ((flags & CaptureLost) != 0)
            {
                return true;
            }

            if (Interlocked.CompareExchange(ref _flags, flags | CaptureLost, flags) == flags)
            {
                return true;
            }
        }
    }

    internal bool Seal()
    {
        return (SealFlags() & CaptureLost) != 0;
    }

    internal int SealFlags()
    {
        while (true)
        {
            int flags = Volatile.Read(ref _flags);
            if ((flags & Finalized) != 0)
            {
                return flags;
            }

            int sealedFlags = flags | Finalized;
            if ((flags & ~FlagMask) != 0)
            {
                sealedFlags |= CaptureLost;
            }

            if (Interlocked.CompareExchange(ref _flags, sealedFlags, flags) == flags)
            {
                return sealedFlags;
            }
        }
    }

    internal void RecordCancellation() => Interlocked.Or(ref _flags, CancellationObserved);

    internal void RecordDeadline() => Interlocked.Or(ref _flags, DeadlineObserved);

    internal int SnapshotFlags() => Volatile.Read(ref _flags);
}
