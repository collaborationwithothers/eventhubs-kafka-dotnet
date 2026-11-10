using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Kafka.Statistics;
using Parchwell.Scenarios;
using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptProducer
{
    private readonly Func<IProducer<byte[], byte[]>> producerFactory;
    private readonly TimeProvider clock;
    private readonly B10AbruptStatistics statistics;
    private readonly Func<string, ILedgerStore> ledgerFactory;
    private readonly Func<string, JsonElement, CancellationToken, Task<long>> sendDurable;
    private readonly Func<JsonElement, bool> captureTerminal;
    private readonly Func<string, CancellationToken, Task<ReleaseInfo>> waitForRelease;
    private readonly Func<string, JsonElement, CancellationToken, Task> persistCohortSeal;
    private readonly RunEvidenceSession? evidence;
    private readonly B10AbruptWire.Identity? diagnosticIdentity;
    private readonly SuccessorCompletion? completion;
    private readonly B10AbruptTimeline timeline;
    private readonly Func<long> nativeErrorCount;
    private readonly object gate = new();
    private readonly List<ProbeAttempt> probes = [];
    private IProducer<byte[], byte[]>? producer;
    private RunIdentity identity = null!;
    private RunExecutionOptions options = null!;
    private WorkloadProfile profile = null!;
    private long started;
    private bool timeoutSealed;
    private bool controlSealed;
    private bool killAdmitted;
    private volatile bool faulted;
    private int faultReason;
    private long faultErrorCode;
    private static readonly string[] SuccessorNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    internal B10AbruptProducer(Func<IProducer<byte[], byte[]>> producerFactory, TimeProvider clock,
        B10AbruptStatistics statistics, Func<string, ILedgerStore> ledgerFactory,
        Func<string, JsonElement, CancellationToken, Task<long>> sendDurable,
        Func<JsonElement, bool> captureTerminal,
        Func<string, CancellationToken, Task<ReleaseInfo>> waitForRelease,
        Func<string, JsonElement, CancellationToken, Task> persistCohortSeal, RunEvidenceSession? evidence = null,
        B10AbruptWire.Identity? diagnosticIdentity = null,
        SuccessorCompletion? completion = null,
        B10AbruptTimeline? timeline = null,
        Func<long>? nativeErrorCount = null)
    {
        this.producerFactory = producerFactory;
        this.clock = clock;
        this.statistics = statistics;
        this.ledgerFactory = ledgerFactory;
        this.sendDurable = sendDurable;
        this.captureTerminal = captureTerminal;
        this.waitForRelease = waitForRelease;
        this.persistCohortSeal = persistCohortSeal;
        this.evidence = evidence;
        this.diagnosticIdentity = diagnosticIdentity;
        this.completion = completion;
        this.timeline = timeline ?? new B10AbruptTimeline(clock, "child", null);
        this.nativeErrorCount = nativeErrorCount ?? (() => 0);
    }

    /// <summary>The highest native error sequence the first child has assigned so far; 0 when the child has no channel.</summary>
    internal long NativeErrorCount => nativeErrorCount();

    /// <summary>The successor's two completion steps. The producer runs them in order around native disposal.</summary>
    internal sealed record SuccessorCompletion(
        Func<CancellationToken, Task<KafkaStatisticsCompletion>> CompleteStatistics,
        Func<KafkaStatisticsCompletion, CancellationToken, Task<IReadOnlyDictionary<string, string>>> CompleteEvidence);

    internal async Task RunFirstAsync(WorkloadProfile workload, RunIdentity runIdentity,
        RunExecutionOptions executionOptions, CancellationToken cancellationToken = default)
    {
        Initialize(workload, runIdentity, executionOptions);
        try
        {
            await RunTimeoutCohortAsync(cancellationToken).ConfigureAwait(false);
            await RunControlAsync(cancellationToken).ConfigureAwait(false);
            await AdmitKillOriginalAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(Timeout.InfiniteTimeSpan, clock, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Once a kill original reaches Produce, even error cleanup must not flush or dispose it.
            if (!killAdmitted) await CleanupAsync().ConfigureAwait(false);
        }
    }

    internal void Initialize(WorkloadProfile workload, RunIdentity runIdentity, RunExecutionOptions executionOptions)
    {
        if (producer is not null) throw Attach(new InvalidOperationException("b10_abrupt_configuration_invalid"));
        executionOptions.Validate();
        runIdentity.Validate();
        if (runIdentity.ScenarioId != "B10" || runIdentity.ConfigurationId != "C1")
            throw Attach(new InvalidDataException("b10_abrupt_configuration_invalid"));
        profile = workload;
        identity = runIdentity;
        options = executionOptions;
        _ = WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes).GetEnumerator();
        Directory.CreateDirectory(Path.Combine(options.OutputRoot, "abrupt-child"));
        producer = producerFactory();
        statistics.MarkConstructed(clock.GetUtcNow());
        started = clock.GetTimestamp();
        timeline.Mark(B10AbruptStage.ProducerConstructed);
    }

    internal async Task RunTimeoutCohortAsync(CancellationToken cancellationToken = default)
    {
        if (timeoutSealed || probes.Count != 0) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
            [Fact(B10AbruptFact.TimeoutSealed, timeoutSealed), Fact(B10AbruptFact.ProbeCount, probes.Count)]);
        long plannedCount = WorkloadGenerator.GetPlannedAttemptCount(profile);
        var retryPolicy = options.ApplicationRetry ?? ApplicationRetryPolicy.ForScenario("B10");
        if (checked(plannedCount * (1L + retryPolicy.MaxRetriesPerEvent)) > options.MaxAttempts)
            throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                [Fact(B10AbruptFact.PlannedCount, plannedCount), Fact(B10AbruptFact.MaximumRetries, retryPolicy.MaxRetriesPerEvent),
                 Fact(B10AbruptFact.MaximumAttempts, options.MaxAttempts)]);
        var inventory = new AttemptInventory(identity, options.MaxAttempts);
        var retryQueue = new ConcurrentQueue<PlannedEvent>();
        var retryCounts = new Dictionary<long, int>();
        var rows = new ConcurrentDictionary<string, LedgerRow>(StringComparer.Ordinal);
        await using ILedgerStore ledger = ledgerFactory("timeout");
        long admitted = 0, retryAdmitted = 0, delivered = 0, timedOut = 0, synchronousRejections = 0, other = 0;

        void Invoke(PlannedEvent planned, bool scheduled)
        {
            CheckGuards();
            cancellationToken.ThrowIfCancellationRequested();
            long ordinal = admitted;
            string attemptId = identity.InstanceId + "-timeout-" + ordinal.ToString(CultureInfo.InvariantCulture);
            var entry = new AttemptEntry(identity.InstanceId, attemptId, ordinal, planned.TenantId,
                planned.DocumentId, planned.Key, planned.Sequence, planned.EventType, planned.DueMicroseconds, planned.DueMicroseconds);
            byte[] payload = WorkloadGenerator.GeneratePayload(planned, profile.Seed);
            inventory.Reserve(entry);
            long invoked = clock.GetTimestamp();
            long elapsed = ElapsedMicroseconds(invoked);
            if (scheduled && elapsed - planned.DueMicroseconds > options.MaxScheduleLateness.TotalMicroseconds)
            {
                inventory.CancelReservation();
                throw Attach(new InvalidDataException("b10_abrupt_deadline_exhausted"),
                    [Fact(B10AbruptFact.LatenessMicroseconds, elapsed - planned.DueMicroseconds),
                     Fact(B10AbruptFact.MaximumLatenessMicroseconds, (long)options.MaxScheduleLateness.TotalMicroseconds),
                     Fact(B10AbruptFact.AdmittedCount, admitted)]);
            }
            entry = inventory.CommitReservation(elapsed);
            Interlocked.Increment(ref admitted);
            int callbackSeen = 0;
            try
            {
                var message = Message(planned.Key, payload, attemptId, planned.Sequence);
                message.Headers.Add("tenant-id", Encoding.UTF8.GetBytes(planned.TenantId));
                message.Headers.Add("document-id", Encoding.UTF8.GetBytes(planned.DocumentId));
                message.Headers.Add("event-type", Encoding.UTF8.GetBytes(planned.EventType));
                producer!.Produce(options.Topic, message, report =>
                {
                    try
                    {
                        if (Interlocked.Exchange(ref callbackSeen, 1) != 0) { Fault(1); return; }
                        switch (report.Error.Code)
                        {
                            case ErrorCode.NoError: Interlocked.Increment(ref delivered); break;
                            case ErrorCode.Local_MsgTimedOut: Interlocked.Increment(ref timedOut); break;
                            default: Interlocked.Increment(ref other); break;
                        }
                        long completed = clock.GetTimestamp();
                        int? status = Enum.IsDefined(report.Status) ? (int)report.Status : null;
                        var row = new LedgerRow(1, identity, entry, "callback", !report.Error.IsError,
                            (int)report.Error.Code, report.Error.Code.ToString(), status,
                            status.HasValue ? "native_callback" : "missing", ElapsedMicroseconds(completed),
                            clock.GetElapsedTime(invoked, completed).TotalMilliseconds, null);
                        if (!ledger.TryAppend(row) || !rows.TryAdd(entry.AttemptId, row)) Fault(2);
                        if (!IsPermittedTerminal(row.ErrorCode, status)) Fault(3, row.ErrorCode);
                        if (ApplicationRetryPolicy.IsPermittedTimeout(row)) retryQueue.Enqueue(planned);
                    }
                    catch { Fault(4); }
                });
            }
            catch (ProduceException<byte[], byte[]> exception) when (exception.Error.Code == ErrorCode.Local_QueueFull)
            {
                Interlocked.Increment(ref synchronousRejections);
                long completed = clock.GetTimestamp();
                var row = new LedgerRow(1, identity, entry, "synchronous_rejection", false,
                    (int)exception.Error.Code, exception.Error.Code.ToString(), null, "pre_enqueue",
                    ElapsedMicroseconds(completed), null, clock.GetElapsedTime(invoked, completed).TotalMilliseconds);
                if (!ledger.TryAppend(row) || !rows.TryAdd(entry.AttemptId, row)) Fault(2);
            }
        }

        void AdmitRetries()
        {
            while (retryQueue.TryDequeue(out PlannedEvent? retry))
            {
                int used = retryCounts.GetValueOrDefault(retry.Ordinal);
                if (used >= retryPolicy.MaxRetriesPerEvent) continue;
                retryCounts[retry.Ordinal] = used + 1;
                Interlocked.Increment(ref retryAdmitted);
                Invoke(retry, scheduled: false);
            }
        }

        foreach (var planned in WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes, cancellationToken))
        {
            AdmitRetries();
            await DelayUntilElapsedAsync(planned.DueMicroseconds, cancellationToken).ConfigureAwait(false);
            Invoke(planned, scheduled: true);
        }
        timeline.Mark(B10AbruptStage.TimeoutScheduleComplete,
            Fact(B10AbruptFact.PlannedCount, plannedCount), Fact(B10AbruptFact.AdmittedCount, admitted),
            Fact(B10AbruptFact.RetryAdmittedCount, retryAdmitted));
        long drainStart = clock.GetTimestamp();
        DateTimeOffset drained;
        do
        {
            await DrainAsync(options.ProducerDrainTimeout - clock.GetElapsedTime(drainStart), cancellationToken).ConfigureAwait(false);
            drained = clock.GetUtcNow();
            if (retryQueue.IsEmpty) break;
            AdmitRetries();
        } while (true);
        timeline.Mark(B10AbruptStage.TimeoutDrained,
            Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(drainStart)),
            Fact(B10AbruptFact.BudgetMilliseconds, options.ProducerDrainTimeout),
            Fact(B10AbruptFact.AdmittedCount, admitted), Fact(B10AbruptFact.DeliveredCount, Interlocked.Read(ref delivered)),
            Fact(B10AbruptFact.TimedOutCount, Interlocked.Read(ref timedOut)),
            Fact(B10AbruptFact.SynchronousRejectionCount, Interlocked.Read(ref synchronousRejections)),
            Fact(B10AbruptFact.OtherErrorCount, Interlocked.Read(ref other)), Fact(B10AbruptFact.RetryAdmittedCount, retryAdmitted));
        CheckGuards();
        var entries = inventory.Seal();
        var manifest = new RunManifest(1, identity, "1", plannedCount, admitted, entries,
            clock.TimestampFrequency, started, "complete", true, null, [],
            ImmutableDictionary<string, string>.Empty.Add("dotnet", Environment.Version.ToString()),
            ImmutableDictionary<string, string>.Empty, []);
        _ = LedgerValidator.Validate(rows.Values, manifest, identity);
        await ledger.DrainAsync(cancellationToken).ConfigureAwait(false);
        await ledger.SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
        string directory = Path.Combine(options.OutputRoot, "timeout");
        _ = await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"),
            Path.Combine(directory, "manifest.json"), identity, options.MaxAttempts, options.MaxRowBytes,
            options.MaxLedgerReadbackBytes, options.MaxManifestBytes, cancellationToken).ConfigureAwait(false);
        await WaitForPostDrainAsync(0, drained, cancellationToken).ConfigureAwait(false);
        B10AbruptStatistics.NumberedReport report = statistics.SelectPostDrain(drained)!;
        await WriteDurableAsync(Path.Combine(directory, "post-drain-report.json"), Encoding.UTF8.GetBytes(report.Report.RawJson), cancellationToken).ConfigureAwait(false);
        await persistCohortSeal("timeout", JsonSerializer.SerializeToElement(new
        {
            cohort = "timeout", count = entries.Length, inventory = Describe(Path.Combine(directory, "manifest.json")),
            ledger = Describe(Path.Combine(directory, "ledger.jsonl")), drain_complete = true,
            guard_complete = true, post_drain_statistics = Describe(Path.Combine(directory, "post-drain-report.json")), fatal_capture_status = "journal",
            native_error_count = nativeErrorCount()
        }), cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.CohortSealed, Fact(B10AbruptFact.Arm, 0), Fact(B10AbruptFact.EntryCount, entries.Length));
        timeoutSealed = true;
    }

    internal async Task RunControlAsync(CancellationToken cancellationToken = default)
    {
        if (!timeoutSealed || controlSealed) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
            [Fact(B10AbruptFact.TimeoutSealed, timeoutSealed), Fact(B10AbruptFact.ControlSealed, controlSealed)]);
        byte[] payload = ProbePayload();
        for (int i = 0; i < B10AbruptOptions.ProbeBurstSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await InvokeProbe("same_instance", payload, null, i, cancellationToken).ConfigureAwait(false);
        }
        ReleaseInfo info = await waitForRelease("same_instance", cancellationToken).ConfigureAwait(false);
        ProbeAttempt? original;
        lock (gate) original = probes.SingleOrDefault(attempt => attempt.AttemptId == info.SelectedAttemptId &&
            attempt.PredecessorAttemptId is null && attempt.Key.EndsWith("/same_instance", StringComparison.Ordinal) && attempt.Accepted);
        if (original is null) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
        DateTimeOffset release = info.Release;
        timeline.Mark(B10AbruptStage.ReleaseRead, Fact(B10AbruptFact.Arm, 1), Fact(B10AbruptFact.RemainingMilliseconds, release - clock.GetUtcNow()));
        await WaitForReleaseAsync(release, false, cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.ReleaseReached);
        _ = await InvokeProbe("same_instance", payload, original.AttemptId, original.Sequence, cancellationToken).ConfigureAwait(false);
        await DrainAsync(options.ProducerDrainTimeout, cancellationToken).ConfigureAwait(false);
        MarkTerminals();
        timeline.WriteChildBurst(probes.Where(attempt => attempt.PredecessorAttemptId is null &&
            attempt.Key.EndsWith("/same_instance", StringComparison.Ordinal)).Select(attempt =>
                new B10AbruptChildBurstMember(attempt.Sequence, attempt.InvokedTimestamp,
                    attempt.Terminal is null ? -1 : attempt.CallbackTimestamp, attempt.TerminalWrittenTimestamp)).ToArray());
        timeline.Mark(B10AbruptStage.CohortDrained);
        DateTimeOffset drained = clock.GetUtcNow();
        await WaitForPostDrainAsync(1, drained, cancellationToken).ConfigureAwait(false);
        CheckGuards();
        JsonElement seal = await SealProbeCohortAsync("same_instance", "control", drained, cancellationToken).ConfigureAwait(false);
        await persistCohortSeal("control", seal, cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.CohortSealed, Fact(B10AbruptFact.Arm, 1), Fact(B10AbruptFact.EntryCount, B10AbruptOptions.ProbeBurstSize + 1));
        controlSealed = true;
    }

    internal async Task AdmitKillOriginalAsync(CancellationToken cancellationToken = default)
    {
        if (!timeoutSealed || !controlSealed || killAdmitted) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
            [Fact(B10AbruptFact.TimeoutSealed, timeoutSealed), Fact(B10AbruptFact.ControlSealed, controlSealed),
             Fact(B10AbruptFact.KillAdmitted, killAdmitted)]);
        byte[] payload = ProbePayload();
        for (int i = 0; i < B10AbruptOptions.ProbeBurstSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await InvokeProbe("cross_restart", payload, null, i, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<ProbeAttempt> InvokeProbe(string arm, byte[] payload, string? predecessorAttemptId,
        long sequence, CancellationToken cancellationToken = default)
    {
        CheckGuards();
        if (arm is not ("same_instance" or "cross_restart") || payload.Length > options.MaxPayloadBytes)
            throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                [Fact(B10AbruptFact.ArmKnown, arm is "same_instance" or "cross_restart"), Fact(B10AbruptFact.PayloadLength, payload.Length),
                 Fact(B10AbruptFact.MaximumPayloadBytes, options.MaxPayloadBytes)]);
        int ordinal;
        lock (gate)
        {
            ProbeAttempt[] previous = probes.Where(entry => entry.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
            ProbeAttempt? predecessor = previous.SingleOrDefault(entry => entry.AttemptId == predecessorAttemptId && entry.PredecessorAttemptId is null);
            if (predecessorAttemptId is null ? sequence < 0 || sequence >= B10AbruptOptions.ProbeBurstSize ||
                previous.Count(entry => entry.PredecessorAttemptId is null) != sequence :
                arm != "same_instance" || predecessor is null || !predecessor.Accepted || predecessor.Sequence != sequence ||
                previous.Any(entry => entry.PredecessorAttemptId is not null))
                throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                    [Fact(B10AbruptFact.PreviousCount, previous.Length), Fact(B10AbruptFact.IsOriginal, predecessorAttemptId is null),
                     Fact(B10AbruptFact.PredecessorMatches, predecessor is not null),
                     Fact(B10AbruptFact.PreviousAccepted, previous.Length == 1 && previous[0].Accepted)]);
            ordinal = probes.Count;
        }
        string key = $"B10/{identity.RunId}/{identity.ConfigurationId}/1/{arm}";
        string attemptId = identity.InstanceId + "-" + arm + "-" + ordinal.ToString(CultureInfo.InvariantCulture);
        var attempt = new ProbeAttempt(attemptId, key, payload, predecessorAttemptId, ordinal) { Sequence = sequence };
        var prepared = JsonSerializer.SerializeToElement(new
        {
            attempt_id = attemptId, invocation_ordinal = ordinal, cohort = arm, probe_id = key, key, sequence,
            payload_length = payload.Length, payload_sha256 = Hash(payload), predecessor_attempt_id = predecessorAttemptId
        });
        long preparedSequence = await sendDurable("prepared", prepared, cancellationToken).ConfigureAwait(false);
        bool recordStage = sequence == 0 || predecessorAttemptId is not null;
        if (recordStage) timeline.Mark(B10AbruptStage.ProbePrepared);
        if (predecessorAttemptId is null && sequence == 0)
            await WriteDurableAsync(Path.Combine(options.OutputRoot, "abrupt-child", arm + "-payload.bin"), payload, cancellationToken).ConfigureAwait(false);
        lock (gate) probes.Add(attempt);
        if (arm == "cross_restart") killAdmitted = true;
        try
        {
            attempt.InvokedTimestamp = clock.GetTimestamp();
            producer!.Produce(options.Topic, Message(key, payload, attemptId, sequence), report => RecordTerminal(attempt, report));
            if (recordStage) timeline.Hold(B10AbruptStage.ProbeProduced);
        }
        catch (ProduceException<byte[], byte[]> exception)
        {
            await sendDurable("synchronous_rejection", JsonSerializer.SerializeToElement(new
            {
                attempt_id = attemptId, prepared_sequence = preparedSequence,
                error_code = (int)exception.Error.Code, error_reason = exception.Error.Reason
            }), cancellationToken).ConfigureAwait(false);
            throw Attach(new InvalidDataException("b10_abrupt_guard_failed", exception),
                [Fact(B10AbruptFact.ErrorCode, (int)exception.Error.Code), Fact(B10AbruptFact.Arm, ArmCode(arm))]);
        }
        DateTimeOffset acceptance = clock.GetUtcNow();
        if (predecessorAttemptId is null && sequence == 0)
        {
            B10AbruptStatistics.NumberedReport? selected = statistics.SelectPreAcceptance(acceptance);
            timeline.Hold(B10AbruptStage.ProbeReportSelected, Fact(B10AbruptFact.Arm, ArmCode(arm)),
                Fact(B10AbruptFact.ReportPresent, selected is not null));
            if (selected is not null)
            {
                byte[] raw = Encoding.UTF8.GetBytes(selected.Report.RawJson);
                await WriteDurableAsync(Path.Combine(options.OutputRoot, "abrupt-child", arm + "-pre-acceptance-report.json"), raw, cancellationToken).ConfigureAwait(false);
                await sendDurable("pre_acceptance_report", JsonSerializer.SerializeToElement(new
                {
                    arm, attempt_id = attemptId, report_length = raw.Length, report_sha256 = Hash(raw),
                    report_identity = selected.Identity(), report_sequence = selected.Sequence,
                    report_capture_utc = selected.Report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)
                }), cancellationToken).ConfigureAwait(false);
            }
        }
        await sendDurable("accepted", JsonSerializer.SerializeToElement(new
        {
            attempt_id = attemptId, prepared_sequence = preparedSequence
        }), cancellationToken).ConfigureAwait(false);
        if (recordStage) timeline.Mark(B10AbruptStage.ProbeAccepted, Fact(B10AbruptFact.Arm, ArmCode(arm)),
            Fact(B10AbruptFact.IsOriginal, predecessorAttemptId is null),
            Fact(B10AbruptFact.ProduceToAcceptedMilliseconds, clock.GetElapsedTime(attempt.InvokedTimestamp)),
            Fact(B10AbruptFact.ProduceTicks, attempt.InvokedTimestamp));
        attempt.Accepted = true;
        return attempt;
    }

    internal void RecordTerminal(ProbeAttempt attempt, DeliveryReport<byte[], byte[]> report)
    {
        try
        {
            // Delivery-callback path: record the timestamp only. No timeline, file or stream I/O here (ADR-021 rule 4).
            attempt.CallbackTimestamp = clock.GetTimestamp();
            int? status = Enum.IsDefined(report.Status) ? (int)report.Status : null;
            var terminal = JsonSerializer.SerializeToElement(new
            {
                attempt_id = attempt.AttemptId, completion_source = "callback", delivered = !report.Error.IsError,
                error_code = (int)report.Error.Code, persistence_status = status
            });
            lock (gate)
            {
                if (attempt.Terminal is not null) { Fault(5); return; }
                attempt.Terminal = terminal;
                if (!IsPermittedTerminal((int)report.Error.Code, status)) Fault(3, (int)report.Error.Code);
            }
            if (!captureTerminal(terminal)) Fault(6);
        }
        catch { Fault(4); }
    }

    // Called from the child channel's writer loop after it writes a terminal frame, and after each probe cohort drain.
    internal void MarkTerminals(string? writtenAttemptId = null)
    {
        try
        {
            List<(ProbeAttempt Attempt, JsonElement Terminal)> ready = [];
            lock (gate)
                foreach (ProbeAttempt attempt in probes)
                    if ((writtenAttemptId is null || attempt.AttemptId == writtenAttemptId) && attempt.Terminal is { } terminal)
                    {
                        if (writtenAttemptId is not null && attempt.TerminalWrittenTimestamp == -1)
                            attempt.TerminalWrittenTimestamp = clock.GetTimestamp();
                        if (attempt.TryClaimTerminalMark()) ready.Add((attempt, terminal));
                    }
            foreach ((ProbeAttempt attempt, JsonElement terminal) in ready)
                if (attempt.Sequence == 0 || attempt.PredecessorAttemptId is not null) timeline.Mark(B10AbruptStage.ProbeTerminal,
                    Fact(B10AbruptFact.Arm, ArmCode(attempt.Key[(attempt.Key.LastIndexOf('/') + 1)..])),
                    Fact(B10AbruptFact.IsOriginal, attempt.PredecessorAttemptId is null),
                    Fact(B10AbruptFact.ErrorCode, NumberOrMinusOne(terminal, "error_code")),
                    Fact(B10AbruptFact.PersistenceStatus, NumberOrMinusOne(terminal, "persistence_status")),
                    Fact(B10AbruptFact.Delivered, terminal.GetProperty("delivered").ValueKind == JsonValueKind.True),
                    Fact(B10AbruptFact.ProduceToTerminalMilliseconds, clock.GetElapsedTime(attempt.InvokedTimestamp, attempt.CallbackTimestamp)),
                    Fact(B10AbruptFact.ProduceTicks, attempt.InvokedTimestamp),
                    Fact(B10AbruptFact.CallbackTicks, attempt.CallbackTimestamp));
        }
        catch (Exception) { }
    }

    internal static bool IsPermittedTerminal(int errorCode, int? persistenceStatus) =>
        errorCode switch
        {
            (int)ErrorCode.NoError => persistenceStatus is (int)PersistenceStatus.NotPersisted or (int)PersistenceStatus.Persisted,
            // This is the callback error/status combination admitted by ApplicationRetryPolicy.IsPermittedTimeout.
            (int)ErrorCode.Local_MsgTimedOut => persistenceStatus is (int)PersistenceStatus.NotPersisted or (int)PersistenceStatus.PossiblyPersisted,
            _ => false
        };

    internal async Task<SuccessorArtifacts> RunSuccessorAsync(WorkloadProfile workload, RunIdentity runIdentity,
        RunExecutionOptions executionOptions, JsonElement selectedEntries, DateTimeOffset releaseUtc,
        CancellationToken cancellationToken = default)
    {
        // Host validates the receipt and pod/source/image binding before invoking this method.
        if (selectedEntries.ValueKind != JsonValueKind.Array || selectedEntries.GetArrayLength() != 1)
            throw Attach(new InvalidDataException("b10_abrupt_successor_binding_invalid"),
                [Fact(B10AbruptFact.ArrayLength, selectedEntries.ValueKind == JsonValueKind.Array ? selectedEntries.GetArrayLength() : -1)]);
        var selected = selectedEntries[0];
        string key = selected.GetProperty("key").GetString()!;
        string originalId = selected.GetProperty("attempt_id").GetString()!;
        byte[] payload = Convert.FromBase64String(selected.GetProperty("payload_base64").GetString()!);
        long sequence = selected.GetProperty("sequence").GetInt64();
        List<B10AbruptFactValue> facts = [];
        if (!Check(facts, B10AbruptFact.KeyMatches, key == $"B10/{runIdentity.RunId}/{runIdentity.ConfigurationId}/1/cross_restart") ||
            !Check(facts, B10AbruptFact.SequenceInBurst, sequence is >= 0 and < B10AbruptOptions.ProbeBurstSize) ||
            !Check(facts, B10AbruptFact.LengthMatches, selected.GetProperty("payload_length").GetInt32() == payload.Length) ||
            !Check(facts, B10AbruptFact.HashMatches, selected.GetProperty("payload_sha256").GetString() == Hash(payload)) ||
            !Check(facts, B10AbruptFact.PayloadWithinLimit, payload.Length <= executionOptions.MaxPayloadBytes) ||
            !Check(facts, B10AbruptFact.InstanceDistinct, !originalId.StartsWith(runIdentity.InstanceId + "-", StringComparison.Ordinal)))
            throw Attach(new InvalidDataException("b10_abrupt_successor_binding_invalid"), [.. facts]);
        Initialize(workload, runIdentity, executionOptions);
        DateTimeOffset ready = clock.GetUtcNow();
        timeline.Mark(B10AbruptStage.SuccessorReady);
        try
        {
            await WaitForReleaseAsync(releaseUtc, true, cancellationToken).ConfigureAwait(false);
            timeline.Mark(B10AbruptStage.ReleaseReached);
            string attemptId = identity.InstanceId + "-replay-0";
            var attempt = new ProbeAttempt(attemptId, key, payload, originalId, 0) { Sequence = sequence };
            probes.Add(attempt);
            DateTimeOffset invoked = clock.GetUtcNow();
            long invocationTimestamp = clock.GetTimestamp();
            attempt.InvokedTimestamp = invocationTimestamp;
            producer!.Produce(options.Topic, Message(key, payload, attemptId, sequence), report => RecordTerminal(attempt, report));
            await DrainAsync(options.ProducerDrainTimeout, cancellationToken).ConfigureAwait(false);
            MarkTerminals();
            timeline.Mark(B10AbruptStage.CohortDrained);
            DateTimeOffset drained = clock.GetUtcNow();
            await WaitForPostDrainAsync(2, drained, cancellationToken).ConfigureAwait(false);
            CheckGuards();
            _ = await SealProbeCohortAsync("cross_restart", "successor", drained, cancellationToken).ConfigureAwait(false);
            timeline.Mark(B10AbruptStage.CohortSealed, Fact(B10AbruptFact.Arm, 2), Fact(B10AbruptFact.EntryCount, 1));
            string directory = Path.Combine(options.OutputRoot, "successor");
            // Statistics completion needs the live client: it waits for a report after the completion boundary, which a disposed client never emits.
            // Disposal comes next so error callbacks it raises reach the evidence session before that session is completed.
            KafkaStatisticsCompletion? statisticsCompletion = completion is null ? null :
                await completion.CompleteStatistics(cancellationToken).ConfigureAwait(false);
            await CleanupAsync().ConfigureAwait(false);
            IReadOnlyDictionary<string, string>? captured = completion is null ? null :
                await completion.CompleteEvidence(statisticsCompletion!, cancellationToken).ConfigureAwait(false);
            CheckGuards();
            if (captured is not null)
            {
                foreach (string name in SuccessorNames.Skip(2))
                {
                    bool present = captured.TryGetValue(name, out string? path);
                    bool exists = present && File.Exists(path);
                    if (!present || !exists)
                        throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
                            [Fact(B10AbruptFact.NameIndex, Array.IndexOf(SuccessorNames, name)),
                             Fact(B10AbruptFact.CapturedPresent, present), Fact(B10AbruptFact.FileExists, exists)]);
                    if (Path.GetFullPath(path!) != Path.GetFullPath(Path.Combine(directory, name)))
                        await WriteDurableAsync(Path.Combine(directory, name), await B10AbruptPublisher.ReadFileAsync(path!, 134217728, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                }
                CheckGuards();
            }
            else
            {
            await WriteDurableAsync(Path.Combine(directory, "statistics.jsonl"), statistics.CapturedBytes(), cancellationToken).ConfigureAwait(false);
            await WriteDurableAsync(Path.Combine(directory, "request-lifecycle.jsonl"), [], cancellationToken).ConfigureAwait(false);
            await WriteDurableAsync(Path.Combine(directory, "capabilities.json"), Envelope("capabilities", new
            {
                request_capture = "unavailable",
                fatal_capture_status = "not_implemented_pr3"
            }), cancellationToken).ConfigureAwait(false);
            }
            return new SuccessorArtifacts(SuccessorNames.ToDictionary(name => name, name => Path.Combine(directory, name), StringComparer.Ordinal),
                JsonSerializer.SerializeToElement(new[] { new { attempt_id = attemptId, predecessor_attempt_id = originalId,
                    key, sequence, payload_sha256 = Hash(payload), invocation_utc = invoked.ToString("O", CultureInfo.InvariantCulture),
                    monotonic_ticks = invocationTimestamp } }), ready, releaseUtc);
        }
        finally { await CleanupAsync().ConfigureAwait(false); }
    }

    private async Task<JsonElement> SealProbeCohortAsync(string arm, string directoryName, DateTimeOffset drained, CancellationToken cancellationToken)
    {
        ProbeAttempt[] entries;
        lock (gate) entries = probes.Where(probe => probe.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
        if (entries.Length != (directoryName == "control" ? B10AbruptOptions.ProbeBurstSize + 1 : 1) || entries.Any(entry => entry.Terminal is null))
            throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
                [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.EntryCount, entries.Length),
                 Fact(B10AbruptFact.ExpectedCount, directoryName == "control" ? B10AbruptOptions.ProbeBurstSize + 1 : 1),
                 Fact(B10AbruptFact.NoTerminalCount, entries.Count(entry => entry.Terminal is null))]);
        string directory = Path.Combine(options.OutputRoot, directoryName);
        Directory.CreateDirectory(directory);
        var inventory = entries.Select(entry => new { attempt_id = entry.AttemptId, invocation_ordinal = entry.Ordinal,
            predecessor_attempt_id = entry.PredecessorAttemptId, key = entry.Key, sequence = entry.Sequence,
            payload_length = entry.Payload.Length, payload_sha256 = Hash(entry.Payload) }).ToArray();
        byte[] ledgerBytes = Encoding.UTF8.GetBytes(string.Join('\n', entries.Select(entry => entry.Terminal!.Value.GetRawText())) + "\n");
        await WriteDurableAsync(Path.Combine(directory, "ledger.jsonl"), ledgerBytes, cancellationToken).ConfigureAwait(false);
        byte[] retainedLedger = await B10AbruptPublisher.ReadFileAsync(Path.Combine(directory, "ledger.jsonl"), 268435456, cancellationToken).ConfigureAwait(false);
        if (!retainedLedger.AsSpan().SequenceEqual(ledgerBytes)) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
            [Fact(B10AbruptFact.LengthMatches, retainedLedger.Length == ledgerBytes.Length)]);
        await WriteDurableAsync(Path.Combine(directory, "manifest.json"), Envelope("cohort_inventory", new
        {
            instance_id = identity.InstanceId,
            cohort = arm, inventory_count = entries.Length, inventory, bookkeeping_complete = true
        }), cancellationToken).ConfigureAwait(false);
        B10AbruptStatistics.NumberedReport report = statistics.SelectPostDrain(drained)!;
        await WriteDurableAsync(Path.Combine(directory, "post-drain-report.json"), Encoding.UTF8.GetBytes(report.Report.RawJson), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new
        {
            cohort = arm, count = entries.Length, inventory = Describe(Path.Combine(directory, "manifest.json")),
            ledger = Describe(Path.Combine(directory, "ledger.jsonl")), drain_complete = true,
            guard_complete = true, post_drain_statistics = Describe(Path.Combine(directory, "post-drain-report.json")), fatal_capture_status = "journal",
            native_error_count = nativeErrorCount()
        });
    }

    private async Task WaitForPostDrainAsync(int arm, DateTimeOffset drained, CancellationToken cancellationToken)
    {
        long waitBegin = clock.GetTimestamp();
        statistics.MarkDrainBoundary(drained, clock.GetTimestamp(), clock.TimestampFrequency);
        using var deadline = new CancellationTokenSource(options.StatisticsCompletionTimeout ?? options.CleanupTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            while (statistics.SelectPostDrain(drained) is null)
            {
                CheckGuards();
                await Task.Delay(TimeSpan.FromMilliseconds(10), clock, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        { throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
            [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(waitBegin)),
             Fact(B10AbruptFact.BudgetMilliseconds, options.StatisticsCompletionTimeout ?? options.CleanupTimeout),
             .. statistics.PostDrainFacts(drained)]); }
        timeline.Mark(B10AbruptStage.PostDrainReport, Fact(B10AbruptFact.Arm, arm),
            Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(waitBegin)), Fact(B10AbruptFact.ReportCount, statistics.ReportCount));
    }

    private async Task DrainAsync(TimeSpan budget, CancellationToken cancellationToken)
    {
        if (killAdmitted) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
            [Fact(B10AbruptFact.KillAdmitted, killAdmitted)]);
        long begin = clock.GetTimestamp();
        int lastOutstanding = -1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckGuards();
            TimeSpan remaining = budget - clock.GetElapsedTime(begin);
            if (remaining <= TimeSpan.Zero) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
                [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget),
                 Fact(B10AbruptFact.OutstandingCount, lastOutstanding)]);
            lastOutstanding = producer!.Flush(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100));
            if (lastOutstanding == 0) return;
            await Task.Yield();
        }
    }

    private async Task WaitForReleaseAsync(DateTimeOffset release, bool successor, CancellationToken cancellationToken)
    {
        TimeSpan remaining = release - clock.GetUtcNow();
        if (remaining < TimeSpan.Zero) throw Attach(new InvalidDataException(successor ? "b10_abrupt_successor_late" : "b10_abrupt_control_incomplete"),
            [Fact(B10AbruptFact.RemainingMilliseconds, remaining), Fact(B10AbruptFact.Arm, successor ? 2 : 1)]);
        long begin = clock.GetTimestamp();
        while (true)
        {
            TimeSpan delay = remaining - clock.GetElapsedTime(begin);
            if (delay <= TimeSpan.Zero) return;
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DelayUntilElapsedAsync(long microseconds, CancellationToken cancellationToken)
    {
        while (true)
        {
            long remaining = microseconds - ElapsedMicroseconds(clock.GetTimestamp());
            if (remaining <= 0) return;
            await Task.Delay(TimeSpan.FromMicroseconds(remaining), clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CleanupAsync()
    {
        if (producer is null || killAdmitted) return;
        IProducer<byte[], byte[]> owned = producer;
        producer = null;
        await Task.Run(owned.Dispose).WaitAsync(options.CleanupTimeout, clock).ConfigureAwait(false);
    }

    private void CheckGuards()
    {
        if (faulted || evidence is { IsFaulted: true } || evidence is { GuardObservationsComplete: false } ||
            evidence?.GuardObservations.Contains("native_error_callback_observed", StringComparer.Ordinal) == true)
            throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                [Fact(B10AbruptFact.Faulted, faulted), Fact(B10AbruptFact.FaultReason, Volatile.Read(ref faultReason)),
                 Fact(B10AbruptFact.ErrorCode, Interlocked.Read(ref faultErrorCode)), Fact(B10AbruptFact.EvidenceFaulted, evidence is { IsFaulted: true }),
                 Fact(B10AbruptFact.GuardObservationsComplete, evidence is null || evidence.GuardObservationsComplete),
                 Fact(B10AbruptFact.NativeErrorObserved, evidence?.GuardObservations.Contains("native_error_callback_observed", StringComparer.Ordinal) == true)]);
    }

    private void Fault(int reason, long errorCode = 0)
    {
        if (Interlocked.CompareExchange(ref faultReason, reason, 0) == 0) Interlocked.Exchange(ref faultErrorCode, errorCode);
        faulted = true;
    }

    private byte[] ProbePayload() => WorkloadGenerator.GeneratePayload(
        WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes).First(), profile.Seed);
    private long ElapsedMicroseconds(long timestamp) => checked((long)clock.GetElapsedTime(started, timestamp).TotalMicroseconds);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private Message<byte[], byte[]> Message(string key, byte[] payload, string attemptId, long sequence) => new()
    {
        Key = Encoding.UTF8.GetBytes(key), Value = payload,
        Headers = new Headers
        {
            { "instance-id", Encoding.UTF8.GetBytes(identity.InstanceId) },
            { "attempt-id", Encoding.UTF8.GetBytes(attemptId) }, { "sequence", Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture)) }
        }
    };

    private byte[] Envelope(string kind, object data)
    {
        if (diagnosticIdentity is null) throw Attach(new InvalidDataException("b10_abrupt_configuration_invalid"));
        return B10AbruptWire.SerializeEnvelope(kind, diagnosticIdentity, data);
    }

    private static object Describe(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new { name = Path.GetFileName(path), length = bytes.Length, sha256 = Hash(bytes) };
    }

    internal static async Task WriteDurableAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
    }

    internal sealed class ProbeAttempt(string attemptId, string key, byte[] payload, string? predecessorAttemptId, int ordinal)
    {
        internal string AttemptId { get; } = attemptId;
        internal string Key { get; } = key;
        internal byte[] Payload { get; } = payload;
        internal string? PredecessorAttemptId { get; } = predecessorAttemptId;
        internal int Ordinal { get; } = ordinal;
        internal long Sequence { get; init; }
        internal JsonElement? Terminal { get; set; }
        internal bool Accepted { get; set; }
        private long invokedTimestamp;
        private long callbackTimestamp;
        private long terminalWrittenTimestamp = -1;
        private int terminalMarked;
        internal long InvokedTimestamp { get => Interlocked.Read(ref invokedTimestamp); set => Interlocked.Exchange(ref invokedTimestamp, value); }
        internal long CallbackTimestamp { get => Interlocked.Read(ref callbackTimestamp); set => Interlocked.Exchange(ref callbackTimestamp, value); }
        internal long TerminalWrittenTimestamp { get => Interlocked.Read(ref terminalWrittenTimestamp); set => Interlocked.Exchange(ref terminalWrittenTimestamp, value); }
        internal bool TryClaimTerminalMark() => Interlocked.Exchange(ref terminalMarked, 1) == 0;
    }

    internal sealed record ReleaseInfo(DateTimeOffset Release, string? SelectedAttemptId);

    internal sealed record SuccessorArtifacts(IReadOnlyDictionary<string, string> Paths, JsonElement Invocations,
        DateTimeOffset ReadyUtc, DateTimeOffset ReleaseUtc);
}
