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
    private readonly B10AbruptJournal? journal;
    private readonly RunEvidenceSession? evidence;
    private readonly B10AbruptWire.Identity? diagnosticIdentity;
    private readonly SuccessorCompletion? completion;
    private readonly B10AbruptTimeline timeline;
    private readonly Func<(int ReturnValue, int Errno)> selfKill;
    private readonly Action<int> exit;
    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(120);
    private readonly object gate = new();
    private readonly List<ProbeAttempt> probes = [];
    private IProducer<byte[], byte[]>? producer;
    private RunIdentity identity = null!;
    private RunExecutionOptions options = null!;
    private WorkloadProfile profile = null!;
    private long started;
    private bool killAdmitted;
    private bool controlComplete;
    private volatile bool faulted;
    private int faultReason;
    private long faultErrorCode;
    private static readonly string[] SuccessorNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    internal B10AbruptProducer(Func<IProducer<byte[], byte[]>> producerFactory, TimeProvider clock,
        B10AbruptStatistics statistics, B10AbruptJournal? journal = null, RunEvidenceSession? evidence = null,
        B10AbruptWire.Identity? diagnosticIdentity = null, SuccessorCompletion? completion = null,
        B10AbruptTimeline? timeline = null, Func<(int ReturnValue, int Errno)>? selfKill = null,
        Action<int>? exit = null)
    {
        this.producerFactory = producerFactory; this.clock = clock; this.statistics = statistics; this.journal = journal;
        this.evidence = evidence; this.diagnosticIdentity = diagnosticIdentity; this.completion = completion;
        this.timeline = timeline ?? new B10AbruptTimeline(clock, "child", null);
        this.selfKill = selfKill ?? B10AbruptProcess.SelfKill; this.exit = exit ?? Environment.Exit;
    }
    internal long NativeErrorCount => journal?.NativeErrorCount ?? 0;
    internal sealed record SuccessorCompletion(Func<CancellationToken, Task<KafkaStatisticsCompletion>> CompleteStatistics,
        Func<KafkaStatisticsCompletion, CancellationToken, Task<IReadOnlyDictionary<string, string>>> CompleteEvidence);
    internal async Task<int> RunFirstAsync(WorkloadProfile workload, RunIdentity runIdentity,
        RunExecutionOptions executionOptions, CancellationToken cancellationToken = default)
    {
        if (journal is null) throw Attach(new InvalidDataException("b10_abrupt_configuration_invalid"));
        try
        {
            if (journal.Frames.Count == 0)
                await journal.AppendAsync("child_started", B10AbruptWire.Element(new { pid = Environment.ProcessId, start_time_ticks = B10AbruptProc.ReadStartTimeTicks(B10AbruptProc.ReadBounded("/proc/self/stat")), boot_id = B10AbruptProc.ReadBootId() }), cancellationToken).ConfigureAwait(false);
            timeline.Mark(B10AbruptStage.ChildStarted);
            B10AbruptProcess.Preflight();
            Initialize(workload, runIdentity, executionOptions);
            await WaitForInitialReportAsync(cancellationToken).ConfigureAwait(false);
            await RunControlAsync(cancellationToken).ConfigureAwait(false);
            await AdmitKillOriginalAsync(cancellationToken).ConfigureAwait(false);
            var signal = selfKill();
            try { await journal.AppendAsync("signal_failed", B10AbruptWire.Element(new { return_value = signal.ReturnValue, errno = signal.Errno }), CancellationToken.None).ConfigureAwait(false); }
            finally { exit(4); }
            return 4;
        }
        catch (Exception exception) when (!journal.Frames.Any(frame => frame.Kind == "kill_intent"))
        {
            string reason = B10AbruptParent.FailureName(exception);
            if (!B10AbruptWire.NoKillReasons.Contains(reason, StringComparer.Ordinal)) reason = "b10_abrupt_guard_failed";
            try { await journal.AppendAsync("no_kill", B10AbruptWire.Element(new { reason, cross_restart_admitted = killAdmitted }), CancellationToken.None).ConfigureAwait(false); timeline.Mark(B10AbruptStage.NoKillWritten); }
            catch (Exception) { }
            if (killAdmitted) { exit(3); return 3; }
            if (producer is not null)
            {
                try { producer.Flush(options.ProducerDrainTimeout); } catch (Exception) { }
                await CleanupAsync().ConfigureAwait(false);
            }
            return 3;
        }
    }
    internal void Initialize(WorkloadProfile workload, RunIdentity runIdentity, RunExecutionOptions executionOptions)
    {
        if (producer is not null) throw Attach(new InvalidOperationException("b10_abrupt_configuration_invalid"));
        executionOptions.Validate(); runIdentity.Validate();
        if (runIdentity.ScenarioId != "B10" || runIdentity.ConfigurationId != "C1") throw Attach(new InvalidDataException("b10_abrupt_configuration_invalid"));
        profile = workload; identity = runIdentity; options = executionOptions;
        _ = WorkloadGenerator.Generate(profile, options.MaxAttempts, options.MaxPayloadBytes).GetEnumerator();
        producer = producerFactory(); statistics.MarkConstructed(clock.GetUtcNow()); started = clock.GetTimestamp();
        timeline.Mark(B10AbruptStage.ProducerConstructed);
    }
    private async Task WaitForInitialReportAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(options.StatisticsCompletionTimeout ?? options.CleanupTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try { while (statistics.SelectPreAcceptance(clock.GetUtcNow()) is null) await Task.Delay(TimeSpan.FromMilliseconds(10), clock, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException exception) { throw Attach(new InvalidDataException("b10_abrupt_sample_missing", exception)); }
    }
    private async Task<ProbeAttempt> OriginalsAsync(string arm, CancellationToken cancellationToken)
    {
        byte[] payload = ProbePayload(); ProbeAttempt? first = null, last = null;
        for (int ordinal = 0; ordinal < B10AbruptOptions.OriginalCount; ordinal++)
        {
            if (first is not null)
            {
                while (true)
                {
                    TimeSpan remaining = TimeSpan.FromMilliseconds(ordinal * 10) - clock.GetElapsedTime(first.InvokedTimestamp);
                    if (remaining <= TimeSpan.Zero) break;
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds)), clock, cancellationToken).ConfigureAwait(false);
                }
            }
            last = await InvokeProbe(arm, payload, null, ordinal, cancellationToken).ConfigureAwait(false);
            first ??= last;
        }
        if (!last!.Accepted)
        {
            bool any = probes.Any(attempt => attempt.Key.EndsWith("/" + arm, StringComparison.Ordinal) && attempt.Accepted);
            throw Attach(new InvalidDataException(arm == "cross_restart" && !any ? "kill_too_early" : "b10_abrupt_cut_rejected"));
        }
        return last;
    }
    internal async Task RunControlAsync(CancellationToken cancellationToken = default)
    {
        if (journal is null || controlComplete) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
        ProbeAttempt last = await OriginalsAsync("same_instance", cancellationToken).ConfigureAwait(false);
        var cut = await journal.AppendBoundaryAsync("cut", last.AttemptId, ReleaseDelay, cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.CutWritten);
        string[] selected = cut.Data.GetProperty("unconfirmed_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (selected.Length == 0) throw Attach(new InvalidDataException("b10_abrupt_exposure_inconclusive"));
        DateTimeOffset release = DateTimeOffset.ParseExact(cut.Data.GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
        await WaitForReleaseAsync(release, false, cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.ReleaseReached);
        foreach (string id in selected)
        {
            ProbeAttempt original = probes.Single(attempt => attempt.AttemptId == id);
            _ = await InvokeProbe("same_instance", original.Payload, original.AttemptId, original.Sequence, cancellationToken).ConfigureAwait(false);
        }
        int outstanding = producer!.Flush(options.ProducerDrainTimeout);
        DateTimeOffset drained = clock.GetUtcNow();
        await journal.DrainAvailable(cancellationToken).ConfigureAwait(false);
        await journal.AppendAsync("control_drained", B10AbruptWire.Element(new { flush_returned = true, out_queue_length = outstanding }), cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.ControlDrained);
        if (outstanding != 0 || probes.Any(attempt => attempt.Terminal is null)) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
        await WaitForPostDrainAsync(1, drained, cancellationToken).ConfigureAwait(false);
        await BindReportAsync("same_instance", null, statistics.SelectPostDrain(drained)!, true, cancellationToken).ConfigureAwait(false);
        CheckGuards();
        var snapshot = B10AbruptJournal.Read(await B10AbruptPublisher.ReadFileAsync(journal.Path, 16777216, cancellationToken).ConfigureAwait(false));
        if (snapshot.Gaps.Count != 0) throw B10AbruptWire.Gap(snapshot.Gaps[0]);
        if (snapshot.Failures.Count != 0) throw Attach(new InvalidDataException(snapshot.Failures.Values.First()));
        controlComplete = true;
    }
    internal async Task AdmitKillOriginalAsync(CancellationToken cancellationToken = default)
    {
        if (!controlComplete || journal is null) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
        ProbeAttempt last = await OriginalsAsync("cross_restart", cancellationToken).ConfigureAwait(false);
        await journal.AppendBoundaryAsync("kill_intent", last.AttemptId, ReleaseDelay, cancellationToken).ConfigureAwait(false);
        timeline.Hold(B10AbruptStage.KillIntentWritten);
    }
    internal async Task<ProbeAttempt> InvokeProbe(string arm, byte[] payload, string? predecessorAttemptId, long sequence, CancellationToken cancellationToken = default)
    {
        CheckGuards();
        if (journal is null || arm is not ("same_instance" or "cross_restart") || sequence is < 0 or > 4 || payload.Length > options.MaxPayloadBytes) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"));
        lock (gate)
        {
            var previous = probes.Where(attempt => attempt.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
            var predecessor = previous.SingleOrDefault(attempt => attempt.AttemptId == predecessorAttemptId && attempt.PredecessorAttemptId is null);
            bool valid = predecessorAttemptId is null
                ? previous.Count(attempt => attempt.PredecessorAttemptId is null) == sequence
                : arm == "same_instance" && predecessor is not null && predecessor.Accepted && predecessor.Sequence == sequence && predecessor.Payload.AsSpan().SequenceEqual(payload) && !previous.Any(attempt => attempt.PredecessorAttemptId == predecessorAttemptId);
            if (!valid) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"));
        }
        string key = $"B10/{identity.RunId}/{identity.ConfigurationId}/1/{arm}";
        string attemptId = identity.InstanceId + "-" + arm + "-" + probes.Count.ToString(CultureInfo.InvariantCulture);
        var attempt = new ProbeAttempt(attemptId, key, payload, predecessorAttemptId, probes.Count) { Sequence = sequence };
        var prepared = await journal.AppendAsync("prepared", B10AbruptWire.Element(new { attempt_id = attemptId, arm, role = predecessorAttemptId is null ? "original" : "resend", ordinal = sequence, probe_id = key, key, sequence, payload_length = payload.Length, payload_sha256 = Hash(payload), predecessor_attempt_id = predecessorAttemptId }), cancellationToken).ConfigureAwait(false);
        timeline.Hold(B10AbruptStage.ProbePrepared);
        if (predecessorAttemptId is null && sequence == 0)
        {
            var report = statistics.SelectPreAcceptance(clock.GetUtcNow()) ?? throw Attach(new InvalidDataException("b10_abrupt_sample_missing"));
            await BindReportAsync(arm, attemptId, report, false, cancellationToken).ConfigureAwait(false);
        }
        lock (gate) probes.Add(attempt);
        if (arm == "cross_restart") killAdmitted = true;
        try
        {
            attempt.InvokedTimestamp = clock.GetTimestamp();
            producer!.Produce(options.Topic, Message(key, payload, attemptId, sequence), report => RecordTerminal(attempt, report));
        }
        catch (ProduceException<byte[], byte[]> exception)
        {
            JsonElement rejection = B10AbruptWire.Element(new { attempt_id = attemptId, prepared_sequence = prepared.Sequence, error_code = (int)exception.Error.Code, error_reason = exception.Error.Reason });
            await journal.AppendAsync("synchronous_rejection", rejection, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                if (attempt.Terminal is not null) Fault(1);
                else attempt.Terminal = rejection;
            }
            return attempt;
        }
        await journal.AppendAsync("accepted", B10AbruptWire.Element(new { attempt_id = attemptId, prepared_sequence = prepared.Sequence }), cancellationToken).ConfigureAwait(false);
        attempt.Accepted = true; timeline.Hold(B10AbruptStage.ProbeAccepted); return attempt;
    }
    private async Task BindReportAsync(string arm, string? attemptId, B10AbruptStatistics.NumberedReport selected, bool post, CancellationToken cancellationToken)
    {
        string name = post ? "control-post-drain-report.json" : arm + "-pre-acceptance-report.json";
        byte[] raw = Encoding.UTF8.GetBytes(selected.Report.RawJson);
        await WriteDurableAsync(Path.Combine(options.OutputRoot, "abrupt", name), raw, cancellationToken).ConfigureAwait(false);
        await journal!.AppendAsync(post ? "post_drain_report" : "pre_acceptance_report", B10AbruptWire.Element(new { arm, attempt_id = attemptId, name, length = raw.Length, sha256 = Hash(raw), report_identity = selected.Identity(), report_sequence = selected.Sequence, report_capture_utc = selected.Report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture) }), cancellationToken).ConfigureAwait(false);
    }
    internal void RecordTerminal(ProbeAttempt attempt, DeliveryReport<byte[], byte[]> report)
    {
        try
        {
            attempt.CallbackTimestamp = clock.GetTimestamp();
            int? status = Enum.IsDefined(report.Status) ? (int)report.Status : null;
            JsonElement terminal = B10AbruptWire.Element(new { attempt_id = attempt.AttemptId, completion_source = "callback", delivered = !report.Error.IsError, error_code = (int)report.Error.Code, persistence_status = status });
            lock (gate) { if (attempt.Terminal is not null) { Fault(1); return; } attempt.Terminal = terminal; }
            if (journal is not null && !journal.TryCaptureTerminal(terminal)) Fault(2);
            if (!IsPermittedTerminal((int)report.Error.Code, status)) Fault(3, (int)report.Error.Code);
        }
        catch (Exception) { Fault(4); }
    }
    internal static bool IsPermittedTerminal(int errorCode, int? persistenceStatus) => errorCode switch
    {
        (int)ErrorCode.NoError => persistenceStatus is (int)PersistenceStatus.NotPersisted or (int)PersistenceStatus.Persisted,
        (int)ErrorCode.Local_MsgTimedOut => persistenceStatus is (int)PersistenceStatus.NotPersisted or (int)PersistenceStatus.PossiblyPersisted,
        _ => false
    };
    internal async Task<SuccessorArtifacts> RunSuccessorAsync(WorkloadProfile workload, RunIdentity runIdentity,
        RunExecutionOptions executionOptions, JsonElement selectedEntries, DateTimeOffset releaseUtc,
        CancellationToken cancellationToken = default)
    {
        if (selectedEntries.ValueKind != JsonValueKind.Array || selectedEntries.GetArrayLength() is < 1 or > 5) throw Attach(new InvalidDataException("b10_abrupt_successor_binding_invalid"));
        byte[] payload = WorkloadGenerator.GeneratePayload(WorkloadGenerator.Generate(workload, executionOptions.MaxAttempts, executionOptions.MaxPayloadBytes, cancellationToken).First(), workload.Seed);
        foreach (var selected in selectedEntries.EnumerateArray())
            if (selected.GetProperty("key").GetString() != $"B10/{runIdentity.RunId}/C1/1/cross_restart" || selected.GetProperty("sequence").GetInt64() is < 0 or > 4 || selected.GetProperty("payload_length").GetInt64() != payload.Length || selected.GetProperty("payload_sha256").GetString() != Hash(payload) || selected.GetProperty("attempt_id").GetString()!.StartsWith(runIdentity.InstanceId + "-",StringComparison.Ordinal)) throw Attach(new InvalidDataException("b10_abrupt_successor_binding_invalid"));
        Initialize(workload, runIdentity, executionOptions);
        DateTimeOffset ready = clock.GetUtcNow();
        timeline.Mark(B10AbruptStage.SuccessorReady);
        try
        {
            await WaitForReleaseAsync(releaseUtc, true, cancellationToken).ConfigureAwait(false);
            timeline.Mark(B10AbruptStage.ReleaseReached);
            List<object> invocations = [];
            foreach (var selected in selectedEntries.EnumerateArray())
            {
                string key = selected.GetProperty("key").GetString()!, originalId = selected.GetProperty("attempt_id").GetString()!;
                long sequence = selected.GetProperty("sequence").GetInt64();
                string attemptId = identity.InstanceId + "-replay-" + probes.Count.ToString(CultureInfo.InvariantCulture);
                var attempt = new ProbeAttempt(attemptId, key, payload, originalId, probes.Count) { Sequence = sequence };
                probes.Add(attempt); DateTimeOffset invoked = clock.GetUtcNow(); long invocationTimestamp = clock.GetTimestamp();
                attempt.InvokedTimestamp = invocationTimestamp;
                producer!.Produce(options.Topic, Message(key, payload, attemptId, sequence), report => RecordTerminal(attempt, report));
                invocations.Add(new { attempt_id = attemptId, predecessor_attempt_id = originalId, key, sequence, payload_sha256 = Hash(payload), invocation_utc = invoked.ToString("O",CultureInfo.InvariantCulture), monotonic_ticks = invocationTimestamp });
            }
            await DrainAsync(options.ProducerDrainTimeout, cancellationToken).ConfigureAwait(false);
            timeline.Mark(B10AbruptStage.ControlDrained);
            DateTimeOffset drained = clock.GetUtcNow();
            await WaitForPostDrainAsync(2, drained, cancellationToken).ConfigureAwait(false);
            CheckGuards();
            _ = await SealProbeCohortAsync("cross_restart", "successor", drained, cancellationToken).ConfigureAwait(false);
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
                JsonSerializer.SerializeToElement(invocations), ready, releaseUtc);
        }
        finally { await CleanupAsync().ConfigureAwait(false); }
    }

    private async Task<JsonElement> SealProbeCohortAsync(string arm, string directoryName, DateTimeOffset drained, CancellationToken cancellationToken)
    {
        ProbeAttempt[] entries;
        lock (gate) entries = probes.Where(probe => probe.Key.EndsWith("/" + arm, StringComparison.Ordinal)).ToArray();
        if (entries.Length == 0 || entries.Any(entry => entry.Terminal is null))
            throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"),
                [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.EntryCount, entries.Length),
                 Fact(B10AbruptFact.ExpectedCount, entries.Length),
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
            native_error_count = NativeErrorCount
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


    internal sealed record SuccessorArtifacts(IReadOnlyDictionary<string, string> Paths, JsonElement Invocations,
        DateTimeOffset ReadyUtc, DateTimeOffset ReleaseUtc);
}
