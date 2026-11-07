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
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptProducer
{
    private readonly Func<IProducer<byte[], byte[]>> producerFactory;
    private readonly TimeProvider clock;
    private readonly B10AbruptStatistics statistics;
    private readonly Func<string, ILedgerStore> ledgerFactory;
    private readonly Func<string, JsonElement, CancellationToken, Task<long>> sendDurable;
    private readonly Func<JsonElement, bool> captureTerminal;
    private readonly Func<string, CancellationToken, Task<DateTimeOffset>> waitForRelease;
    private readonly Func<string, JsonElement, CancellationToken, Task> persistCohortSeal;
    private readonly RunEvidenceSession? evidence;
    private readonly B10AbruptWire.Identity? diagnosticIdentity;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>>? completeEvidence;
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
    private static readonly string[] SuccessorNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    internal B10AbruptProducer(Func<IProducer<byte[], byte[]>> producerFactory, TimeProvider clock,
        B10AbruptStatistics statistics, Func<string, ILedgerStore> ledgerFactory,
        Func<string, JsonElement, CancellationToken, Task<long>> sendDurable,
        Func<JsonElement, bool> captureTerminal,
        Func<string, CancellationToken, Task<DateTimeOffset>> waitForRelease,
        Func<string, JsonElement, CancellationToken, Task> persistCohortSeal, RunEvidenceSession? evidence = null,
        B10AbruptWire.Identity? diagnosticIdentity = null,
        Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>>? completeEvidence = null)
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
        this.completeEvidence = completeEvidence;
    }

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
        if (producer is not null) throw new InvalidOperationException("b10_abrupt_configuration_invalid");
        executionOptions.Validate();
        runIdentity.Validate();
        if (runIdentity.ScenarioId != "B10" || runIdentity.ConfigurationId != "C1")
            throw new InvalidDataException("b10_abrupt_configuration_invalid");
        profile = workload;
        identity = runIdentity;
        options = executionOptions;
        _ = WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes).GetEnumerator();
        Directory.CreateDirectory(Path.Combine(options.OutputRoot, "abrupt-child"));
        producer = producerFactory();
        statistics.MarkConstructed(clock.GetUtcNow());
        started = clock.GetTimestamp();
    }

    internal async Task RunTimeoutCohortAsync(CancellationToken cancellationToken = default)
    {
        if (timeoutSealed || probes.Count != 0) throw new InvalidDataException("b10_abrupt_guard_failed");
        long plannedCount = WorkloadGenerator.GetPlannedAttemptCount(profile);
        var retryPolicy = options.ApplicationRetry ?? ApplicationRetryPolicy.ForScenario("B10");
        if (checked(plannedCount * (1L + retryPolicy.MaxRetriesPerEvent)) > options.MaxAttempts)
            throw new InvalidDataException("b10_abrupt_guard_failed");
        var inventory = new AttemptInventory(identity, options.MaxAttempts);
        var retryQueue = new ConcurrentQueue<PlannedEvent>();
        var retryCounts = new Dictionary<long, int>();
        var rows = new ConcurrentDictionary<string, LedgerRow>(StringComparer.Ordinal);
        await using ILedgerStore ledger = ledgerFactory("timeout");
        long admitted = 0;

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
                throw new InvalidDataException("b10_abrupt_deadline_exhausted");
            }
            entry = inventory.CommitReservation(elapsed);
            admitted++;
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
                        if (Interlocked.Exchange(ref callbackSeen, 1) != 0) { faulted = true; return; }
                        long completed = clock.GetTimestamp();
                        int? status = Enum.IsDefined(report.Status) ? (int)report.Status : null;
                        var row = new LedgerRow(1, identity, entry, "callback", !report.Error.IsError,
                            (int)report.Error.Code, report.Error.Code.ToString(), status,
                            status.HasValue ? "native_callback" : "missing", ElapsedMicroseconds(completed),
                            clock.GetElapsedTime(invoked, completed).TotalMilliseconds, null);
                        if (!ledger.TryAppend(row) || !rows.TryAdd(entry.AttemptId, row)) faulted = true;
                        if (!IsPermittedTerminal(row.ErrorCode, status)) faulted = true;
                        if (ApplicationRetryPolicy.IsPermittedTimeout(row)) retryQueue.Enqueue(planned);
                    }
                    catch { faulted = true; }
                });
            }
            catch (ProduceException<byte[], byte[]> exception) when (exception.Error.Code == ErrorCode.Local_QueueFull)
            {
                long completed = clock.GetTimestamp();
                var row = new LedgerRow(1, identity, entry, "synchronous_rejection", false,
                    (int)exception.Error.Code, exception.Error.Code.ToString(), null, "pre_enqueue",
                    ElapsedMicroseconds(completed), null, clock.GetElapsedTime(invoked, completed).TotalMilliseconds);
                if (!ledger.TryAppend(row) || !rows.TryAdd(entry.AttemptId, row)) faulted = true;
            }
        }

        void AdmitRetries()
        {
            while (retryQueue.TryDequeue(out PlannedEvent? retry))
            {
                int used = retryCounts.GetValueOrDefault(retry.Ordinal);
                if (used >= retryPolicy.MaxRetriesPerEvent) continue;
                retryCounts[retry.Ordinal] = used + 1;
                Invoke(retry, scheduled: false);
            }
        }

        foreach (var planned in WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes, cancellationToken))
        {
            AdmitRetries();
            await DelayUntilElapsedAsync(planned.DueMicroseconds, cancellationToken).ConfigureAwait(false);
            Invoke(planned, scheduled: true);
        }
        long drainStart = clock.GetTimestamp();
        DateTimeOffset drained;
        do
        {
            await DrainAsync(options.ProducerDrainTimeout - clock.GetElapsedTime(drainStart), cancellationToken).ConfigureAwait(false);
            drained = clock.GetUtcNow();
            if (retryQueue.IsEmpty) break;
            AdmitRetries();
        } while (true);
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
        await WaitForPostDrainAsync(drained, cancellationToken).ConfigureAwait(false);
        B10AbruptStatistics.NumberedReport report = statistics.SelectPostDrain(drained)!;
        await WriteDurableAsync(Path.Combine(directory, "post-drain-report.json"), Encoding.UTF8.GetBytes(report.Report.RawJson), cancellationToken).ConfigureAwait(false);
        await persistCohortSeal("timeout", JsonSerializer.SerializeToElement(new
        {
            cohort = "timeout", count = entries.Length, inventory = Describe(Path.Combine(directory, "manifest.json")),
            ledger = Describe(Path.Combine(directory, "ledger.jsonl")), drain_complete = true,
            guard_complete = true, post_drain_statistics = Describe(Path.Combine(directory, "post-drain-report.json")), fatal_capture_status = "not_implemented_pr3"
        }), cancellationToken).ConfigureAwait(false);
        timeoutSealed = true;
    }

    internal async Task RunControlAsync(CancellationToken cancellationToken = default)
    {
        if (!timeoutSealed || controlSealed) throw new InvalidDataException("b10_abrupt_control_incomplete");
        byte[] payload = ProbePayload();
        ProbeAttempt original = await InvokeProbe("same_instance", payload, null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset release = await waitForRelease("same_instance", cancellationToken).ConfigureAwait(false);
        await WaitForReleaseAsync(release, false, cancellationToken).ConfigureAwait(false);
        _ = await InvokeProbe("same_instance", payload, original.AttemptId, cancellationToken).ConfigureAwait(false);
        await DrainAsync(options.ProducerDrainTimeout, cancellationToken).ConfigureAwait(false);
        DateTimeOffset drained = clock.GetUtcNow();
        await WaitForPostDrainAsync(drained, cancellationToken).ConfigureAwait(false);
        CheckGuards();
        JsonElement seal = await SealProbeCohortAsync("same_instance", "control", drained, cancellationToken).ConfigureAwait(false);
        await persistCohortSeal("control", seal, cancellationToken).ConfigureAwait(false);
        controlSealed = true;
    }

    internal async Task AdmitKillOriginalAsync(CancellationToken cancellationToken = default)
    {
        if (!timeoutSealed || !controlSealed || killAdmitted) throw new InvalidDataException("b10_abrupt_control_incomplete");
        _ = await InvokeProbe("cross_restart", ProbePayload(), null, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ProbeAttempt> InvokeProbe(string arm, byte[] payload, string? predecessorAttemptId,
        CancellationToken cancellationToken = default)
    {
        CheckGuards();
        if (arm is not ("same_instance" or "cross_restart") || payload.Length > options.MaxPayloadBytes)
            throw new InvalidDataException("b10_abrupt_guard_failed");
        int ordinal;
        lock (gate)
        {
            ProbeAttempt[] previous = probes.Where(entry => entry.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
            if (predecessorAttemptId is null ? previous.Length != 0 :
                arm != "same_instance" || previous.Length != 1 || previous[0].AttemptId != predecessorAttemptId || !previous[0].Accepted)
                throw new InvalidDataException("b10_abrupt_guard_failed");
            ordinal = probes.Count;
        }
        string key = $"B10/{identity.RunId}/{identity.ConfigurationId}/1/{arm}";
        string attemptId = identity.InstanceId + "-" + arm + "-" + ordinal.ToString(CultureInfo.InvariantCulture);
        var attempt = new ProbeAttempt(attemptId, key, payload, predecessorAttemptId, ordinal);
        var prepared = JsonSerializer.SerializeToElement(new
        {
            attempt_id = attemptId, invocation_ordinal = ordinal, cohort = arm, probe_id = key, key, sequence = 0,
            payload_length = payload.Length, payload_sha256 = Hash(payload), predecessor_attempt_id = predecessorAttemptId
        });
        long preparedSequence = await sendDurable("prepared", prepared, cancellationToken).ConfigureAwait(false);
        if (predecessorAttemptId is null)
            await WriteDurableAsync(Path.Combine(options.OutputRoot, "abrupt-child", arm + "-payload.bin"), payload, cancellationToken).ConfigureAwait(false);
        lock (gate) probes.Add(attempt);
        if (arm == "cross_restart") killAdmitted = true;
        try
        {
            producer!.Produce(options.Topic, Message(key, payload, attemptId, 0), report => RecordTerminal(attempt, report));
        }
        catch (ProduceException<byte[], byte[]> exception)
        {
            await sendDurable("synchronous_rejection", JsonSerializer.SerializeToElement(new
            {
                attempt_id = attemptId, prepared_sequence = preparedSequence,
                error_code = (int)exception.Error.Code, error_reason = exception.Error.Reason
            }), cancellationToken).ConfigureAwait(false);
            throw new InvalidDataException("b10_abrupt_guard_failed", exception);
        }
        DateTimeOffset acceptance = clock.GetUtcNow();
        if (predecessorAttemptId is null)
        {
            B10AbruptStatistics.NumberedReport? selected = statistics.SelectPreAcceptance(acceptance);
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
        attempt.Accepted = true;
        return attempt;
    }

    internal void RecordTerminal(ProbeAttempt attempt, DeliveryReport<byte[], byte[]> report)
    {
        try
        {
            int? status = Enum.IsDefined(report.Status) ? (int)report.Status : null;
            var terminal = JsonSerializer.SerializeToElement(new
            {
                attempt_id = attempt.AttemptId, completion_source = "callback", delivered = !report.Error.IsError,
                error_code = (int)report.Error.Code, persistence_status = status
            });
            lock (gate)
            {
                if (attempt.Terminal is not null) { faulted = true; return; }
                attempt.Terminal = terminal;
                if (!IsPermittedTerminal((int)report.Error.Code, status)) faulted = true;
            }
            if (!captureTerminal(terminal)) faulted = true;
        }
        catch { faulted = true; }
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
            throw new InvalidDataException("b10_abrupt_successor_binding_invalid");
        var selected = selectedEntries[0];
        string key = selected.GetProperty("key").GetString()!;
        string originalId = selected.GetProperty("attempt_id").GetString()!;
        byte[] payload = Convert.FromBase64String(selected.GetProperty("payload_base64").GetString()!);
        if (key != $"B10/{runIdentity.RunId}/{runIdentity.ConfigurationId}/1/cross_restart" ||
            selected.GetProperty("sequence").GetInt64() != 0 ||
            selected.GetProperty("payload_length").GetInt32() != payload.Length ||
            selected.GetProperty("payload_sha256").GetString() != Hash(payload) ||
            payload.Length > executionOptions.MaxPayloadBytes || originalId.StartsWith(runIdentity.InstanceId + "-", StringComparison.Ordinal))
            throw new InvalidDataException("b10_abrupt_successor_binding_invalid");
        Initialize(workload, runIdentity, executionOptions);
        DateTimeOffset ready = clock.GetUtcNow();
        try
        {
            await WaitForReleaseAsync(releaseUtc, true, cancellationToken).ConfigureAwait(false);
            string attemptId = identity.InstanceId + "-replay-0";
            var attempt = new ProbeAttempt(attemptId, key, payload, originalId, 0);
            probes.Add(attempt);
            DateTimeOffset invoked = clock.GetUtcNow();
            long invocationTimestamp = clock.GetTimestamp();
            producer!.Produce(options.Topic, Message(key, payload, attemptId, 0), report => RecordTerminal(attempt, report));
            await DrainAsync(options.ProducerDrainTimeout, cancellationToken).ConfigureAwait(false);
            DateTimeOffset drained = clock.GetUtcNow();
            await WaitForPostDrainAsync(drained, cancellationToken).ConfigureAwait(false);
            CheckGuards();
            _ = await SealProbeCohortAsync("cross_restart", "successor", drained, cancellationToken).ConfigureAwait(false);
            string directory = Path.Combine(options.OutputRoot, "successor");
            IReadOnlyDictionary<string, string>? captured = completeEvidence is null ? null :
                await completeEvidence(cancellationToken).ConfigureAwait(false);
            if (captured is not null)
            {
                foreach (string name in SuccessorNames.Skip(2))
                {
                    if (!captured.TryGetValue(name, out string? path) || !File.Exists(path))
                        throw new InvalidDataException("b10_abrupt_control_incomplete");
                    if (Path.GetFullPath(path) != Path.GetFullPath(Path.Combine(directory, name)))
                        await WriteDurableAsync(Path.Combine(directory, name), await B10AbruptPublisher.ReadFileAsync(path, 134217728, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
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
            await CleanupAsync().ConfigureAwait(false);
            return new SuccessorArtifacts(SuccessorNames.ToDictionary(name => name, name => Path.Combine(directory, name), StringComparer.Ordinal),
                JsonSerializer.SerializeToElement(new[] { new { attempt_id = attemptId, predecessor_attempt_id = originalId,
                    key, sequence = 0, payload_sha256 = Hash(payload), invocation_utc = invoked.ToString("O", CultureInfo.InvariantCulture),
                    monotonic_ticks = invocationTimestamp } }), ready, releaseUtc);
        }
        finally { await CleanupAsync().ConfigureAwait(false); }
    }

    private async Task<JsonElement> SealProbeCohortAsync(string arm, string directoryName, DateTimeOffset drained, CancellationToken cancellationToken)
    {
        ProbeAttempt[] entries;
        lock (gate) entries = probes.Where(probe => probe.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
        if (entries.Length != (directoryName == "control" ? 2 : 1) || entries.Any(entry => entry.Terminal is null))
            throw new InvalidDataException("b10_abrupt_control_incomplete");
        string directory = Path.Combine(options.OutputRoot, directoryName);
        Directory.CreateDirectory(directory);
        var inventory = entries.Select(entry => new { attempt_id = entry.AttemptId, invocation_ordinal = entry.Ordinal,
            predecessor_attempt_id = entry.PredecessorAttemptId, key = entry.Key, sequence = 0,
            payload_length = entry.Payload.Length, payload_sha256 = Hash(entry.Payload) }).ToArray();
        byte[] ledgerBytes = Encoding.UTF8.GetBytes(string.Join('\n', entries.Select(entry => entry.Terminal!.Value.GetRawText())) + "\n");
        await WriteDurableAsync(Path.Combine(directory, "ledger.jsonl"), ledgerBytes, cancellationToken).ConfigureAwait(false);
        byte[] retainedLedger = await B10AbruptPublisher.ReadFileAsync(Path.Combine(directory, "ledger.jsonl"), 268435456, cancellationToken).ConfigureAwait(false);
        if (!retainedLedger.AsSpan().SequenceEqual(ledgerBytes)) throw new InvalidDataException("b10_abrupt_control_incomplete");
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
            guard_complete = true, post_drain_statistics = Describe(Path.Combine(directory, "post-drain-report.json")), fatal_capture_status = "not_implemented_pr3"
        });
    }

    private async Task WaitForPostDrainAsync(DateTimeOffset drained, CancellationToken cancellationToken)
    {
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
        { throw new InvalidDataException("b10_abrupt_control_incomplete"); }
    }

    private async Task DrainAsync(TimeSpan budget, CancellationToken cancellationToken)
    {
        if (killAdmitted) throw new InvalidDataException("b10_abrupt_guard_failed");
        long begin = clock.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckGuards();
            TimeSpan remaining = budget - clock.GetElapsedTime(begin);
            if (remaining <= TimeSpan.Zero) throw new InvalidDataException("b10_abrupt_control_incomplete");
            if (producer!.Flush(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100)) == 0) return;
            await Task.Yield();
        }
    }

    private async Task WaitForReleaseAsync(DateTimeOffset release, bool successor, CancellationToken cancellationToken)
    {
        TimeSpan remaining = release - clock.GetUtcNow();
        if (remaining < TimeSpan.Zero) throw new InvalidDataException(successor ? "b10_abrupt_successor_late" : "b10_abrupt_control_incomplete");
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
            throw new InvalidDataException("b10_abrupt_guard_failed");
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
        if (diagnosticIdentity is null) throw new InvalidDataException("b10_abrupt_configuration_invalid");
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
        internal JsonElement? Terminal { get; set; }
        internal bool Accepted { get; set; }
    }

    internal sealed record SuccessorArtifacts(IReadOnlyDictionary<string, string> Paths, JsonElement Invocations,
        DateTimeOffset ReadyUtc, DateTimeOffset ReleaseUtc);
}
