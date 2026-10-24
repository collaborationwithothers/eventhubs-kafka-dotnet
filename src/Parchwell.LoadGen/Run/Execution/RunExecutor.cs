using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Authentication;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Executes one finite deterministic workload through a native producer and bounded evidence stores.</summary>
public sealed class RunExecutor
{
    private const int AsynchronousFailure = 1;
    private const int MissingPersistence = 2;
    private const int PossiblyPersisted = 4;
    private const int CallbackFailure = 8;
    private const int LedgerFailure = 16;
    private const int NativeProducerException = 32;
    private readonly Func<IProducer<byte[], byte[]>> producerFactory;
    private readonly ILedgerStore ledger;
    private readonly TimeProvider clock;
    private readonly KafkaStatisticsSession? statistics;
    private readonly RunEvidenceSession? evidence;
    private readonly KafkaOAuthBearerSession? authentication;

    /// <summary>Creates an executor over the native producer, ledger, clock and optional evidence sessions.</summary>
    public RunExecutor(Func<IProducer<byte[], byte[]>> producerFactory, ILedgerStore ledger,
        TimeProvider clock, KafkaStatisticsSession? statistics = null, RunEvidenceSession? evidence = null,
        KafkaOAuthBearerSession? authentication = null)
    {
        this.producerFactory = producerFactory ?? throw new ArgumentNullException(nameof(producerFactory));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.statistics = statistics;
        this.evidence = evidence;
        this.authentication = authentication;
    }

    /// <summary>Runs the offered schedule once, drains native callbacks, persists a final manifest and returns its exit code.</summary>
    public async Task<RunExecutionResult> ExecuteAsync(WorkloadProfile profile, RunIdentity identity,
        RunExecutionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        identity.Validate();

        var diagnostics = new List<string>();
        var inventory = new AttemptInventory(identity, options.MaxAttempts);
        long plannedCount = 0;
        long startTimestamp = clock.GetTimestamp();
        IProducer<byte[], byte[]>? producer = null;
        KafkaStatisticsCompletion? statisticsCompletion = null;
        var guardFlags = 0;
        var admissionStopped = false;
        var workerFailed = false;
        long admittedAttemptCount = 0;
        string[] expectedBrokerNames = [];

        void AddDiagnostic(string diagnostic)
        {
            lock (diagnostics)
            {
                if (diagnostics.Count < 256 && !diagnostics.Contains(diagnostic, StringComparer.Ordinal))
                    diagnostics.Add(diagnostic);
            }
        }

        bool ObserveInitialAuthenticationFailure()
        {
            Task<KafkaAuthenticationFailure>? failureTask = authentication?.InitialFailureTask;
            if (failureTask is not { IsCompletedSuccessfully: true }) return false;
            KafkaAuthenticationFailure failure = failureTask.GetAwaiter().GetResult();
            workerFailed = true;
            AddDiagnostic("oauthbearer_initial_failure:" + failure.Reason);
            return true;
        }

        try
        {
            expectedBrokerNames = options.ExpectedBrokerNames.ToArray();
            plannedCount = WorkloadGenerator.GetPlannedAttemptCount(profile);
            if (plannedCount > options.MaxAttempts)
                throw new InvalidOperationException("planned_attempt_limit_exceeded");
            _ = WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes, cancellationToken).GetEnumerator();
            if (options.RequireRequestEvidence && (evidence is null || !evidence.RequestCapabilitySupported))
                throw new InvalidOperationException("required_request_evidence_unavailable");
            if (options.RequireStatisticsEvidence && statistics is null)
                throw new InvalidOperationException("required_statistics_evidence_unavailable");
            if (ledger.IsFaulted) throw new InvalidOperationException("ledger_capture_faulted_before_run");

            producer = producerFactory() ?? throw new InvalidOperationException("producer_factory_returned_null");
            if (ObserveInitialAuthenticationFailure()) admissionStopped = true;
            startTimestamp = clock.GetTimestamp();
            var events = WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes, cancellationToken);
            foreach (var planned in events)
            {
                if (ObserveInitialAuthenticationFailure()) break;
                cancellationToken.ThrowIfCancellationRequested();
                if (evidence is { IsFaulted: true })
                {
                    workerFailed = true;
                    AddDiagnostic("evidence_capture_faulted");
                    break;
                }
                if (ledger.IsFaulted || (Volatile.Read(ref guardFlags) & (CallbackFailure | LedgerFailure)) != 0)
                {
                    workerFailed = true;
                    AddDiagnostic("callback_or_ledger_capture_faulted");
                    break;
                }
                var dueTimestamp = AddMicroseconds(startTimestamp, planned.DueMicroseconds, clock.TimestampFrequency);
                await DelayUntilAsync(dueTimestamp, authentication?.InitialFailureTask, cancellationToken).ConfigureAwait(false);
                if (ObserveInitialAuthenticationFailure()) break;
                var payload = WorkloadGenerator.GeneratePayload(planned, profile.Seed);
                string attemptId = string.Concat(identity.InstanceId, "-", planned.Ordinal.ToString(CultureInfo.InvariantCulture));
                var reservation = new AttemptEntry(identity.InstanceId, attemptId, planned.Ordinal, planned.TenantId,
                    planned.DocumentId, planned.Key, planned.Sequence, planned.EventType, planned.DueMicroseconds,
                    planned.DueMicroseconds);
                AttemptEntry? attempt = null;
                long invokedTimestamp = 0;
                var message = new Message<byte[], byte[]>
                {
                    Key = Encoding.UTF8.GetBytes(planned.Key),
                    Value = payload,
                    Headers = CreateHeaders(identity, planned, attemptId)
                };
                var callbackSeen = 0;
                Action<DeliveryReport<byte[], byte[]>> deliveryHandler = report =>
                    RecordDelivery(report, identity, attempt!, invokedTimestamp, startTimestamp,
                        () => Interlocked.Exchange(ref callbackSeen, 1) == 0,
                        () => Interlocked.Or(ref guardFlags, AsynchronousFailure),
                        () => Interlocked.Or(ref guardFlags, MissingPersistence),
                        () => Interlocked.Or(ref guardFlags, PossiblyPersisted),
                        () => Interlocked.Or(ref guardFlags, CallbackFailure),
                        () => Interlocked.Or(ref guardFlags, LedgerFailure));
                inventory.Reserve(reservation);
                bool invocationCommitted = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ObserveInitialAuthenticationFailure()) break;
                    if (evidence is { IsFaulted: true })
                    {
                        workerFailed = true;
                        AddDiagnostic("evidence_capture_faulted");
                        break;
                    }
                    if (ledger.IsFaulted || (Volatile.Read(ref guardFlags) & (CallbackFailure | LedgerFailure)) != 0)
                    {
                        workerFailed = true;
                        AddDiagnostic("callback_or_ledger_capture_faulted");
                        break;
                    }
                    invokedTimestamp = clock.GetTimestamp();
                    var lateness = ToMicroseconds(Math.Max(0, invokedTimestamp - dueTimestamp), clock.TimestampFrequency);
                    if (lateness > DurationMicroseconds(options.MaxScheduleLateness))
                    {
                        AddDiagnostic("schedule_lateness_limit_exceeded");
                        break;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ObserveInitialAuthenticationFailure()) break;
                    attempt = inventory.CommitReservation(ToMicroseconds(invokedTimestamp - startTimestamp, clock.TimestampFrequency));
                    invocationCommitted = true;
                    admittedAttemptCount = checked(admittedAttemptCount + 1);
                    try
                    {
                        producer.Produce(options.Topic, message, deliveryHandler);
                    }
                    catch (ProduceException<byte[], byte[]> exception) when (exception.Error.Code == ErrorCode.Local_QueueFull)
                    {
                        var completedTimestamp = clock.GetTimestamp();
                        var row = new LedgerRow(1, identity, attempt, "synchronous_rejection", false,
                            (int)exception.Error.Code, exception.Error.Code.ToString(), null, "pre_enqueue",
                            ToMicroseconds(completedTimestamp - startTimestamp, clock.TimestampFrequency), null,
                            ElapsedMilliseconds(invokedTimestamp, completedTimestamp, clock.TimestampFrequency));
                        if (!ledger.TryAppend(row))
                        {
                            Interlocked.Or(ref guardFlags, LedgerFailure);
                            AddDiagnostic("ledger_append_failed");
                            workerFailed = true;
                            break;
                        }
                    }
                    catch
                    {
                        Interlocked.Or(ref guardFlags, NativeProducerException);
                        AddDiagnostic("native_produce_exception");
                        throw;
                    }
                }
                finally
                {
                    if (!invocationCommitted) inventory.CancelReservation();
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            admissionStopped = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            admissionStopped = true;
            workerFailed = true;
            AddDiagnostic("execution_cancelled");
        }
        catch (Exception exception)
        {
            admissionStopped = true;
            workerFailed = true;
            AddDiagnostic("execution_failed:" + exception.GetType().Name);
        }

        var entries = inventory.Seal();
        if (admittedAttemptCount != entries.Length)
        {
            workerFailed = true;
            AddDiagnostic("admission_inventory_count_mismatch");
        }
        try
        {
            if (producer is not null)
            {
                using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "producer_drain");
                if (!await DrainProducerAsync(producer, options.ProducerDrainTimeout, CancellationToken.None).ConfigureAwait(false))
                {
                    workerFailed = true;
                    AddDiagnostic("producer_drain_timeout");
                }
                else activity.Succeed();
            }
        }
        catch (Exception exception)
        {
            workerFailed = true;
            AddDiagnostic("producer_drain_failed:" + exception.GetType().Name);
        }

        if (!admissionStopped) AddDiagnostic("admission_state_unresolved");
        if (ledger.IsFaulted)
        {
            workerFailed = true;
            Interlocked.Or(ref guardFlags, LedgerFailure);
            AddDiagnostic("ledger_capture_faulted");
        }
        AddFlagDiagnostic(guardFlags, AsynchronousFailure, "async_delivery_failure_observed", AddDiagnostic);
        AddFlagDiagnostic(guardFlags, MissingPersistence, "persistence_status_missing", AddDiagnostic);
        AddFlagDiagnostic(guardFlags, PossiblyPersisted, "possibly_persisted_observed", AddDiagnostic);
        AddFlagDiagnostic(guardFlags, CallbackFailure, "delivery_callback_failure", AddDiagnostic);
        AddFlagDiagnostic(guardFlags, LedgerFailure, "ledger_capture_failure", AddDiagnostic);
        AddFlagDiagnostic(guardFlags, NativeProducerException, "native_produce_exception_observed", AddDiagnostic);

        try
        {
            using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "ledger_drain");
            using var ledgerDrainDeadline = new CancellationTokenSource(options.LedgerDrainTimeout ?? options.CleanupTimeout, clock);
            await ledger.DrainAsync(ledgerDrainDeadline.Token).ConfigureAwait(false);
            activity.Succeed();
        }
        catch (Exception exception)
        {
            workerFailed = true;
            AddDiagnostic("ledger_drain_failed:" + exception.GetType().Name);
        }

        if (statistics is not null)
        {
            if (expectedBrokerNames.Length == 0)
            {
                try
                {
                    using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "statistics_completion");
                    using var statisticsDeadline = new CancellationTokenSource(options.StatisticsCompletionTimeout ?? options.CleanupTimeout, clock);
                    statisticsCompletion = await statistics.CompleteAsync(statisticsDeadline.Token).ConfigureAwait(false);
                    if (statisticsCompletion.IsComplete) activity.Succeed();
                    if (!statisticsCompletion.IsComplete)
                    {
                        workerFailed = true;
                        AddDiagnostic("statistics_completion_incomplete");
                    }
                }
                catch (Exception exception)
                {
                    workerFailed = true;
                    AddDiagnostic("statistics_completion_failed:" + exception.GetType().Name);
                }
            }
            else
            {
                try
                {
                    using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "statistics_completion");
                    using var statisticsDeadline = new CancellationTokenSource(options.StatisticsCompletionTimeout ?? options.CleanupTimeout, clock);
                    statisticsCompletion = await statistics.CompleteAsync(expectedBrokerNames, statisticsDeadline.Token).ConfigureAwait(false);
                    if (statisticsCompletion.IsComplete) activity.Succeed();
                    if (!statisticsCompletion.IsComplete && options.RequireStatisticsEvidence)
                    {
                        workerFailed = true;
                        AddDiagnostic("statistics_completion_incomplete");
                    }
                    else if (!statisticsCompletion.IsComplete)
                    {
                        workerFailed = true;
                        AddDiagnostic("statistics_completion_incomplete");
                    }
                }
                catch (Exception exception)
                {
                    workerFailed = true;
                    AddDiagnostic("statistics_completion_failed:" + exception.GetType().Name);
                }
            }
        }
        else if (options.RequireStatisticsEvidence)
        {
            workerFailed = true;
            AddDiagnostic("statistics_session_unavailable");
        }

        if (producer is not null)
        {
            try
            {
                using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "producer_dispose");
                Task disposeAttempt = Task.Run(producer.Dispose, CancellationToken.None);
                using var disposeDeadline = new CancellationTokenSource(options.CleanupTimeout, clock);
                await disposeAttempt.WaitAsync(disposeDeadline.Token).ConfigureAwait(false);
                activity.Succeed();
            }
            catch (OperationCanceledException)
            {
                workerFailed = true;
                AddDiagnostic("producer_dispose_timeout");
            }
            catch (Exception exception) { workerFailed = true; AddDiagnostic("producer_dispose_failed:" + exception.GetType().Name); }
        }
        _ = ObserveInitialAuthenticationFailure();

        if (evidence is not null)
        {
            try
            {
                using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "evidence_completion");
                await evidence.CompleteAsync(statisticsCompletion, CancellationToken.None).ConfigureAwait(false);
                if (!evidence.IsFaulted) activity.Succeed();
            }
            catch (Exception exception)
            {
                workerFailed = true;
                AddDiagnostic("evidence_completion_failed:" + exception.GetType().Name);
            }
            try
            {
                using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "evidence_readback");
                using var evidenceReadbackDeadline = new CancellationTokenSource(options.CleanupTimeout, clock);
                await evidence.ValidatePersistedAsync(evidenceReadbackDeadline.Token).ConfigureAwait(false);
                activity.Succeed();
                if (evidence.IsFaulted)
                {
                    workerFailed = true;
                    AddDiagnostic("evidence_capture_faulted");
                }
                foreach (var item in evidence.Diagnostics) AddDiagnostic("evidence:" + item);
            }
            catch (Exception exception)
            {
                workerFailed = true;
                AddDiagnostic("evidence_readback_failed:" + exception.GetType().Name);
            }
        }

        if (!options.RequireRequestEvidence && (evidence is null || !evidence.RequestCapabilitySupported))
            AddDiagnostic("request_evidence_unsupported");
        AddDiagnostic("broker_lifecycle_inventory_unproven");

        var evidenceFiles = evidence?.ArtifactNames.ToImmutableDictionary(name => name,
            name => name, StringComparer.Ordinal)
            ?? ImmutableDictionary<string, string>.Empty;
        var guardObservations = BuildGuardObservations(guardFlags, statisticsCompletion, evidence);
        var artifactPaths = evidenceFiles.ToImmutableDictionary(pair => pair.Key,
            pair => Path.Combine(Path.GetFullPath(options.OutputRoot), pair.Value), StringComparer.Ordinal)
            .SetItem("ledger", Path.Combine(Path.GetFullPath(options.OutputRoot), "ledger.jsonl"));
        var manifestPath = Path.Combine(Path.GetFullPath(options.OutputRoot), "manifest.json");
        artifactPaths = artifactPaths.SetItem("manifest", manifestPath);

        var readbackComplete = false;
        var candidate = CreateManifest(identity, entries, admittedAttemptCount, plannedCount, startTimestamp, diagnostics,
            evidenceFiles, guardObservations, workerFailed, readbackComplete, clock.TimestampFrequency);
        try
        {
            using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "ledger_readback");
            using var ledgerReadbackDeadline = new CancellationTokenSource(options.CleanupTimeout, clock);
            var rows = await ReadLedgerRowsAsync(artifactPaths["ledger"], options.MaxAttempts,
                options.MaxRowBytes, options.MaxLedgerReadbackBytes, ledgerReadbackDeadline.Token).ConfigureAwait(false);
            _ = LedgerValidator.Validate(rows, candidate, identity);
            readbackComplete = true;
            activity.Succeed();
        }
        catch (Exception exception)
        {
            workerFailed = true;
            AddDiagnostic("ledger_readback_failed:" + exception.GetType().Name);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            workerFailed = true;
            AddDiagnostic("execution_cancelled");
        }
        _ = ObserveInitialAuthenticationFailure();
        var bookkeepingComplete = readbackComplete && !workerFailed && admittedAttemptCount == plannedCount &&
            (guardFlags & (CallbackFailure | LedgerFailure | NativeProducerException)) == 0 &&
            (!options.RequireStatisticsEvidence || statisticsCompletion is { IsComplete: true }) &&
            evidence is not { IsFaulted: true };
        var manifest = CreateManifest(identity, entries, admittedAttemptCount, plannedCount, startTimestamp, diagnostics,
            evidenceFiles, guardObservations, workerFailed, bookkeepingComplete, clock.TimestampFrequency);
        try
        {
            using var activity = LoadGenTelemetry.Begin(clock, identity.ScenarioId, identity.RunId, identity.ClientId, "manifest_save");
            _ = LedgerValidator.SerializeManifest(manifest, options.MaxManifestBytes);
            using var manifestDeadline = new CancellationTokenSource(options.CleanupTimeout, clock);
            await ledger.SaveManifestAsync(manifest, manifestDeadline.Token).ConfigureAwait(false);
            activity.Succeed();
        }
        catch (Exception exception)
        {
            workerFailed = true;
            AddDiagnostic("manifest_save_failed:" + exception.GetType().Name);
            manifest = CreateManifest(identity, entries, admittedAttemptCount, plannedCount, startTimestamp, diagnostics,
                evidenceFiles, guardObservations, workerFailed, false, clock.TimestampFrequency);
        }

        return new RunExecutionResult(bookkeepingComplete && !workerFailed ? 0 : 1, manifest, artifactPaths,
            evidence?.Expectations);
    }

    private async Task DelayUntilAsync(long dueTimestamp, Task<KafkaAuthenticationFailure>? initialFailureTask,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = dueTimestamp - clock.GetTimestamp();
            if (remaining <= 0) return;
            var delay = TimeSpan.FromSeconds((double)remaining / clock.TimestampFrequency);
            if (initialFailureTask is null)
            {
                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task delayTask = Task.Delay(delay, clock, delayCancellation.Token);
            Task completed = await Task.WhenAny(delayTask, initialFailureTask).ConfigureAwait(false);
            if (completed == initialFailureTask)
            {
                delayCancellation.Cancel();
                try { await delayTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (delayCancellation.IsCancellationRequested) { }
                return;
            }
            await delayTask.ConfigureAwait(false);
        }
    }

    private async Task<bool> DrainProducerAsync(IProducer<byte[], byte[]> producer, TimeSpan timeout,
        CancellationToken cleanupToken)
    {
        long drainStarted = clock.GetTimestamp();
        using var drainDeadline = new CancellationTokenSource(timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cleanupToken, drainDeadline.Token);
        while (!linked.IsCancellationRequested)
        {
            var elapsed = clock.GetElapsedTime(drainStarted, clock.GetTimestamp());
            var remaining = timeout - elapsed;
            if (remaining <= TimeSpan.Zero) break;
            var queueCount = producer.Flush(remaining < TimeSpan.FromMilliseconds(100)
                ? remaining : TimeSpan.FromMilliseconds(100));
            if (queueCount == 0)
                return !linked.IsCancellationRequested && clock.GetElapsedTime(drainStarted, clock.GetTimestamp()) <= timeout;
            await Task.Yield();
        }
        return false;
    }

    private void RecordDelivery(DeliveryReport<byte[], byte[]> report, RunIdentity identity, AttemptEntry attempt,
        long invokedTimestamp, long startTimestamp, Func<bool> tryStart,
        Action asyncFailure, Action missingPersistence, Action possiblyPersisted, Action callbackFailure, Action ledgerFailure)
    {
        try
        {
            if (!tryStart())
            {
                callbackFailure();
                return;
            }
            var completedTimestamp = clock.GetTimestamp();
            var errorCode = report.Error.Code;
            var delivered = !report.Error.IsError;
            if (!delivered) asyncFailure();
            int? persistence = report.Status is PersistenceStatus status ? (int)status : null;
            var provenance = persistence.HasValue ? "native_callback" : "missing";
            if (!persistence.HasValue) missingPersistence();
            if (persistence == (int)PersistenceStatus.PossiblyPersisted) possiblyPersisted();
            var row = new LedgerRow(1, identity, attempt, "callback", delivered, (int)errorCode,
                Enum.GetName(errorCode), persistence, provenance,
                ToMicroseconds(completedTimestamp - startTimestamp, clock.TimestampFrequency),
                ElapsedMilliseconds(invokedTimestamp, completedTimestamp, clock.TimestampFrequency), null);
            if (!ledger.TryAppend(row))
            {
                ledgerFailure();
            }
        }
        catch
        {
            callbackFailure();
        }
    }

    private static Headers CreateHeaders(RunIdentity identity, PlannedEvent planned, string attemptId)
    {
        var headers = new Headers();
        headers.Add("tenant-id", Encoding.UTF8.GetBytes(planned.TenantId));
        headers.Add("document-id", Encoding.UTF8.GetBytes(planned.DocumentId));
        headers.Add("sequence", Encoding.UTF8.GetBytes(planned.Sequence.ToString(CultureInfo.InvariantCulture)));
        headers.Add("event-type", Encoding.UTF8.GetBytes(planned.EventType));
        headers.Add("instance-id", Encoding.UTF8.GetBytes(identity.InstanceId));
        headers.Add("attempt-id", Encoding.UTF8.GetBytes(attemptId));
        return headers;
    }

    private static long AddMicroseconds(long timestamp, long microseconds, long frequency)
    {
        var delta = (Int128)microseconds * frequency / 1_000_000;
        var result = (Int128)timestamp + delta;
        return result > long.MaxValue ? long.MaxValue : result < long.MinValue ? long.MinValue : (long)result;
    }

    private static long ToMicroseconds(long timestampDelta, long frequency)
    {
        if (frequency <= 0) return 0;
        var value = (Int128)timestampDelta * 1_000_000 / frequency;
        return value > long.MaxValue ? long.MaxValue : value < long.MinValue ? long.MinValue : (long)value;
    }

    private static double ElapsedMilliseconds(long start, long end, long frequency)
        => Math.Max(0, (double)(end - start) * 1_000 / frequency);

    private static long DurationMicroseconds(TimeSpan duration) => duration.Ticks / 10;

    internal static RunManifest CreateManifest(RunIdentity identity, ImmutableArray<AttemptEntry> entries,
        long admittedAttemptCount, long plannedCount, long startTimestamp, List<string> diagnostics, ImmutableDictionary<string, string> evidenceFiles,
        ImmutableArray<string> guardObservations, bool failed, bool bookkeepingComplete, long frequency)
    {
        string status = !failed && bookkeepingComplete && admittedAttemptCount == plannedCount ? "complete"
            : failed ? "failed" : "incomplete";
        ImmutableArray<string> diagnosticSnapshot;
        lock (diagnostics) diagnosticSnapshot = diagnostics.ToImmutableArray();
        var versions = ImmutableDictionary<string, string>.Empty
            .Add("dotnet", Environment.Version.ToString())
            .Add("confluent_kafka", GetManagedKafkaVersion())
            .Add("librdkafka", Library.VersionString)
            .Add("workload_generator", WorkloadGenerator.GeneratorVersion.ToString(CultureInfo.InvariantCulture));
        return new RunManifest(1, identity, WorkloadGenerator.GeneratorVersion.ToString(CultureInfo.InvariantCulture),
            plannedCount, admittedAttemptCount, entries, frequency, startTimestamp, status, bookkeepingComplete,
            entries.Length < plannedCount ? entries.Length : null, diagnosticSnapshot, versions, evidenceFiles, guardObservations);
    }

    private static string GetManagedKafkaVersion()
    {
        var assembly = typeof(IProducer<byte[], byte[]>).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static ImmutableArray<string> BuildGuardObservations(int flags,
        KafkaStatisticsCompletion? completion, RunEvidenceSession? evidence)
    {
        var observations = ImmutableArray.CreateBuilder<string>();
        if ((flags & AsynchronousFailure) != 0) observations.Add("async_delivery_failure_observed");
        if ((flags & MissingPersistence) != 0) observations.Add("persistence_status_missing");
        if ((flags & PossiblyPersisted) != 0) observations.Add("possibly_persisted_observed");
        if ((flags & CallbackFailure) != 0) observations.Add("delivery_callback_failure");
        if ((flags & LedgerFailure) != 0) observations.Add("ledger_capture_failure");
        if ((flags & NativeProducerException) != 0) observations.Add("native_produce_exception_observed");
        if (completion is null) observations.Add("statistics_guard_unproven");
        else if (!completion.IsComplete) observations.Add("statistics_guard_incomplete");
        if (evidence is null)
        {
            observations.Add("evidence_capture_unavailable");
            observations.Add("request_lifecycle_capture_unsupported");
        }
        else
        {
            if (!evidence.RequestCapabilitySupported) observations.Add("request_lifecycle_capture_unsupported");
            observations.AddRange(evidence.GuardObservations);
        }
        observations.Add("broker_lifecycle_inventory_unproven");
        return observations.ToImmutable();
    }

    private static async Task<ImmutableArray<LedgerRow>> ReadLedgerRowsAsync(string path, int maximumRows,
        int maximumRowBytes, long maximumBytes, CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<LedgerRow>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var input = new byte[8192];
        using var rowBuffer = new MemoryStream(Math.Min(maximumRowBytes, 8192));
        long totalBytes = 0;
        while (true)
        {
            var read = await stream.ReadAsync(input, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            totalBytes = checked(totalBytes + read);
            if (totalBytes > maximumBytes) throw new InvalidDataException("ledger_readback_file_over_limit");
            for (var index = 0; index < read; index++)
            {
                if (input[index] == (byte)'\n')
                {
                    if (rowBuffer.Length == 0 || result.Count >= maximumRows)
                        throw new InvalidDataException("ledger_readback_row_limit_exceeded");
                    result.Add(LedgerValidator.DeserializeRow(
                        rowBuffer.GetBuffer().AsSpan(0, checked((int)rowBuffer.Length))));
                    rowBuffer.SetLength(0);
                }
                else
                {
                    if (rowBuffer.Length >= maximumRowBytes)
                        throw new InvalidDataException("ledger_readback_row_limit_exceeded");
                    rowBuffer.WriteByte(input[index]);
                }
            }
        }
        if (rowBuffer.Length != 0) throw new InvalidDataException("ledger_readback_truncated_final_row");
        return result.ToImmutable();
    }

    private static void AddFlagDiagnostic(int flags, int flag, string diagnostic, Action<string> addDiagnostic)
    {
        if ((flags & flag) != 0) addDiagnostic(diagnostic);
    }

}
