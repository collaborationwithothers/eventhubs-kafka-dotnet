using Confluent.Kafka;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed record ProbeV2HandlerServiceObservation(
    string Action,
    string ClientId,
    int Repeat,
    string? Topic,
    int? Partition,
    long? Offset,
    DateTimeOffset? ObservedAt,
    long? Timestamp,
    string? PersistenceStatus,
    int? ErrorCode,
    bool? ErrorIsFatal,
    string? KeyBase64,
    string? ValueBase64);

internal sealed record ProbeV2HandlerErrorObservation(
    int Code,
    bool IsFatal,
    DateTimeOffset? ObservedAt,
    long? Timestamp);

internal sealed record ProbeV2HandlerRefreshMetadata(
    int TestExpiryCapMs,
    int ObservationLimitMs,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset? OperationAdmittedAt,
    DateTimeOffset? SeekReturnedAt,
    long? StartedTimestamp,
    long? EndedTimestamp,
    long? OperationAdmittedTimestamp,
    long? SeekReturnedTimestamp,
    long TimestampFrequency,
    bool OperationAttempted,
    int? TriggerAttempt,
    ProbeV2HandlerServiceObservation? BeforeService,
    ProbeV2HandlerServiceObservation? AfterService,
    int DiagnosticNonmatches,
    IReadOnlyList<ProbeV2HandlerErrorObservation> DiagnosticErrors,
    bool ObservationClockUnavailable,
    bool CaptureLimitReached,
    bool DuplicateBeforeObservation,
    bool DuplicateAfterObservation,
    bool Cancelled,
    string? OperationExceptionType,
    bool ClientCleanupCompleted);

/// <summary>Captures bounded raw service observations for the opt-in handler refresh-path diagnostic.</summary>
internal sealed class ProbeV2HandlerRefreshPath(TimeProvider clock, int repeat, string clientId)
{
    private const int MaximumNonmatches = 4096;
    private const int MaximumErrors = 8;
    private readonly List<ProbeV2HandlerErrorObservation> errors = new(MaximumErrors);
    private ProbeV2HandlerServiceObservation? beforeService;
    private ProbeV2HandlerServiceObservation? afterService;
    private long? startedTimestamp;
    private long? startedAt;
    private long? endedTimestamp;
    private long? endedAt;
    private long? operationAdmittedTimestamp;
    private long? operationAdmittedAt;
    private long? seekReturnedTimestamp;
    private long? seekReturnedAt;
    private long timestampFrequency;
    private int operationAttempted;
    private int? triggerAttempt;
    private int diagnosticNonmatches;
    private int clockUnavailable;
    private int captureLimitReached;
    private int duplicateBeforeObservation;
    private int duplicateAfterObservation;
    private int cancelled;
    private string? operationExceptionType;

    internal void ObserveBeforeDelivery(DeliveryReport<byte[], byte[]> report)
    {
        ProbeV2HandlerServiceObservation observation = Delivery("delivery", report);
        if (Interlocked.CompareExchange(ref beforeService, observation, null) is not null)
            Interlocked.Exchange(ref duplicateBeforeObservation, 1);
    }

    internal void ObserveBeforeRead(ConsumeResult<byte[], byte[]>? result, ProbeV2Requested requested)
    {
        if (!Matches(result, requested)) return;
        ProbeV2HandlerServiceObservation observation = Read("read", result!);
        Interlocked.CompareExchange(ref beforeService, observation, null);
    }

    internal void StartWindow(long timestamp)
    {
        startedTimestamp = timestamp;
        startedAt = SampleClock().UtcTicks;
        try { timestampFrequency = clock.TimestampFrequency; }
        catch (Exception) { SetClockUnavailable(); }
    }

    internal bool TryAdmit(int? thirdSuccessfulAttempt, CancellationToken callerToken)
    {
        if (thirdSuccessfulAttempt is null || Volatile.Read(ref operationAttempted) != 0 ||
            Volatile.Read(ref captureLimitReached) != 0 || Volatile.Read(ref clockUnavailable) != 0)
            return false;
        if (callerToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref cancelled, 1);
            return false;
        }
        if (!WithinWindow()) return false;
        ClockSample sample = SampleClock();
        if (sample.UtcTicks is null || sample.Timestamp is null)
            return false;
        Interlocked.Exchange(ref operationAttempted, 1);
        triggerAttempt = thirdSuccessfulAttempt;
        operationAdmittedAt = sample.UtcTicks;
        operationAdmittedTimestamp = sample.Timestamp;
        return true;
    }

    internal bool CanContinue(CancellationToken callerToken)
    {
        if (callerToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref cancelled, 1);
            return false;
        }
        return Volatile.Read(ref captureLimitReached) == 0 && Volatile.Read(ref clockUnavailable) == 0 && WithinWindow();
    }

    internal TimeSpan Slice(CancellationToken callerToken, int pollSliceMs)
    {
        if (!CanContinue(callerToken)) return TimeSpan.Zero;
        try
        {
            TimeSpan remaining = TimeSpan.FromMilliseconds(20000) - clock.GetElapsedTime(startedTimestamp!.Value);
            return TimeSpan.FromMilliseconds(Math.Min(pollSliceMs, Math.Max(0, remaining.TotalMilliseconds)));
        }
        catch (Exception)
        {
            SetClockUnavailable();
            return TimeSpan.Zero;
        }
    }

    internal void ObserveAfterDelivery(DeliveryReport<byte[], byte[]> report)
    {
        ProbeV2HandlerServiceObservation observation = Delivery("delivery", report);
        if (Interlocked.CompareExchange(ref afterService, observation, null) is not null)
            Interlocked.Exchange(ref duplicateAfterObservation, 1);
    }

    internal void ObserveAfterRead(ConsumeResult<byte[], byte[]>? result, ProbeV2Requested requested)
    {
        if (!Matches(result, requested)) return;
        Interlocked.CompareExchange(ref afterService, Read("read", result!), null);
    }

    internal bool ObserveDiagnosticRead(ConsumeResult<byte[], byte[]>? result, ProbeV2Requested requested)
    {
        if (result is null || result.IsPartitionEOF) return false;
        if (!Matches(result, requested))
        {
            RecordNonmatch();
            return false;
        }
        Interlocked.CompareExchange(ref afterService, Read("read", result), null);
        return true;
    }

    internal void SeekReturned()
    {
        ClockSample sample = SampleClock();
        seekReturnedAt = sample.UtcTicks;
        seekReturnedTimestamp = sample.Timestamp;
    }

    internal void RecordError(Error error)
    {
        if (errors.Count >= MaximumErrors)
        {
            SetCaptureLimit();
            return;
        }
        ClockSample sample = SampleClock();
        errors.Add(new((int)error.Code, error.IsFatal, sample.UtcTicks, sample.Timestamp));
        if (errors.Count >= MaximumErrors) SetCaptureLimit();
    }

    internal void RecordNonmatch()
    {
        if (diagnosticNonmatches >= MaximumNonmatches)
        {
            SetCaptureLimit();
            return;
        }
        diagnosticNonmatches++;
        if (diagnosticNonmatches >= MaximumNonmatches) SetCaptureLimit();
    }

    internal void RecordOperationException(Exception exception) => operationExceptionType = exception.GetType().Name;

    internal void EndWindow()
    {
        if (startedTimestamp is null) return;
        ClockSample sample = SampleClock();
        endedAt = sample.UtcTicks;
        endedTimestamp = sample.Timestamp;
    }

    internal ProbeV2HandlerRefreshMetadata Finish(bool cleanupCompleted, bool callerCancelled)
    {
        if (callerCancelled) Interlocked.Exchange(ref cancelled, 1);
        if (!cleanupCompleted)
            return new(5000, 20000, ToDateTimeOffset(startedAt), ToDateTimeOffset(endedAt),
                ToDateTimeOffset(operationAdmittedAt), ToDateTimeOffset(seekReturnedAt),
                startedTimestamp, endedTimestamp, operationAdmittedTimestamp, seekReturnedTimestamp,
                timestampFrequency, Volatile.Read(ref operationAttempted) != 0, triggerAttempt,
                null, null, diagnosticNonmatches, Array.Empty<ProbeV2HandlerErrorObservation>(),
                Volatile.Read(ref clockUnavailable) != 0, Volatile.Read(ref captureLimitReached) != 0,
                false, false, Volatile.Read(ref cancelled) != 0, operationExceptionType, false);
        return new(5000, 20000, ToDateTimeOffset(startedAt), ToDateTimeOffset(endedAt),
            ToDateTimeOffset(operationAdmittedAt), ToDateTimeOffset(seekReturnedAt),
            startedTimestamp, endedTimestamp, operationAdmittedTimestamp, seekReturnedTimestamp,
            timestampFrequency, Volatile.Read(ref operationAttempted) != 0, triggerAttempt,
            Volatile.Read(ref beforeService), Volatile.Read(ref afterService), diagnosticNonmatches,
            errors.ToArray(), Volatile.Read(ref clockUnavailable) != 0, Volatile.Read(ref captureLimitReached) != 0,
            Volatile.Read(ref duplicateBeforeObservation) != 0, Volatile.Read(ref duplicateAfterObservation) != 0,
            Volatile.Read(ref cancelled) != 0, operationExceptionType, cleanupCompleted);
    }

    private ProbeV2HandlerServiceObservation Delivery(string action, DeliveryReport<byte[], byte[]> report)
    {
        ClockSample sample = SampleClock();
        return new(action, clientId, repeat, report.Topic, report.Partition.Value, report.Offset.Value,
            sample.UtcTicks, sample.Timestamp, report.Status.ToString(), (int)report.Error.Code,
            report.Error.IsFatal, null, null);
    }

    private ProbeV2HandlerServiceObservation Read(string action, ConsumeResult<byte[], byte[]> result)
    {
        ClockSample sample = SampleClock();
        byte[]? key = result.Message?.Key;
        byte[]? value = result.Message?.Value;
        if (key is { Length: > 4096 } || value is { Length: > 4096 }) SetCaptureLimit();
        return new(action, clientId, repeat, result.Topic, result.Partition.Value, result.Offset.Value,
            sample.UtcTicks, sample.Timestamp, null, null, null,
            key is null || key.Length > 4096 ? null : Convert.ToBase64String(key),
            value is null || value.Length > 4096 ? null : Convert.ToBase64String(value));
    }

    private bool Matches(ConsumeResult<byte[], byte[]>? result, ProbeV2Requested requested) =>
        result is not null && !result.IsPartitionEOF && result.Topic == requested.Topic &&
        result.Partition.Value == requested.Partition && result.Offset.Value == requested.Offset;

    private bool WithinWindow()
    {
        if (startedTimestamp is null) return false;
        try { return clock.GetElapsedTime(startedTimestamp.Value) < TimeSpan.FromMilliseconds(20000); }
        catch (Exception)
        {
            SetClockUnavailable();
            return false;
        }
    }

    private ClockSample SampleClock()
    {
        DateTimeOffset? utc = null;
        long? timestamp = null;
        try { utc = clock.GetUtcNow().ToUniversalTime(); }
        catch (Exception) { SetClockUnavailable(); }
        try { timestamp = clock.GetTimestamp(); }
        catch (Exception) { SetClockUnavailable(); }
        return new(utc?.UtcTicks, timestamp);
    }

    private void SetClockUnavailable() => Interlocked.Exchange(ref clockUnavailable, 1);
    private void SetCaptureLimit() => Interlocked.Exchange(ref captureLimitReached, 1);
    internal void RecordClockUnavailable() => SetClockUnavailable();
    private static DateTimeOffset? ToDateTimeOffset(long? utcTicks) =>
        utcTicks is null ? null : new DateTimeOffset(utcTicks.Value, TimeSpan.Zero);

    private readonly record struct ClockSample(long? UtcTicks, long? Timestamp);
}
