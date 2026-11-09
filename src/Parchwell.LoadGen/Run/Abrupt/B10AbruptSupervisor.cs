using System.Globalization;
using System.Runtime.CompilerServices;
using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;
using System.Text.Json;
using System.Threading.Channels;
using System.Text;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Ledger;

namespace Parchwell.LoadGen.Run.Abrupt;

internal interface IB10AbruptProcess : IDisposable
{
    OwnedProducerProcess.ProcessIdentity Identity { get; }
    Stream Events { get; }
    bool EventsReady();
    Stream Acknowledgements { get; }
    bool HasExited { get; }
    OwnedProducerProcess.SignalObservation Stop();
    bool ConfirmStopped(TimeSpan timeout, TimeProvider clock);
    OwnedProducerProcess.SignalObservation Resume();
    OwnedProducerProcess.SignalObservation Kill();
    OwnedProducerProcess.ExitObservation ObserveExit();
    B10AbruptCgroup.Observation CaptureBefore();
    B10AbruptCgroup.Observation CaptureAfter(B10AbruptCgroup.Observation before);
}

internal sealed class B10AbruptOwnedProcess(OwnedProducerProcess process) : IB10AbruptProcess
{
    public OwnedProducerProcess.ProcessIdentity Identity => process.Identity;
    public Stream Events => process.Events;
    public bool EventsReady() => process.EventsReady();
    public Stream Acknowledgements => process.Acknowledgements;
    public bool HasExited => process.HasExited;
    public OwnedProducerProcess.SignalObservation Stop() => process.RequestStop();
    public bool ConfirmStopped(TimeSpan timeout, TimeProvider clock) => process.ConfirmStopped(timeout, clock);
    public OwnedProducerProcess.SignalObservation Resume() => process.Resume();
    public OwnedProducerProcess.SignalObservation Kill() => process.RequestKill();
    public OwnedProducerProcess.ExitObservation ObserveExit() => process.ObserveExit();
    public B10AbruptCgroup.Observation CaptureBefore() => B10AbruptCgroup.CaptureBefore(process.Identity);
    public B10AbruptCgroup.Observation CaptureAfter(B10AbruptCgroup.Observation before) => B10AbruptCgroup.CaptureAfter(before);
    public void Dispose() => process.Dispose();
}

internal sealed class B10AbruptSupervisor(B10AbruptOptions options, TimeProvider clock, B10AbruptJournal journal,
    B10AbruptPublisher publisher, Func<IB10AbruptProcess> start, Func<CancellationToken, Task>? validateCapturedStatistics = null,
    B10AbruptTimeline? timeline = null)
{
    internal static readonly TimeSpan ChildFailureReportGrace = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan ChildFailureReportPoll = TimeSpan.FromMilliseconds(10);
    private readonly Dictionary<string, B10AbruptWire.Frame> _prepared = new(StringComparer.Ordinal);
    private readonly HashSet<string> _accepted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, B10AbruptWire.Frame> _terminals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _reports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _armFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly List<JsonElement> _signals = [];
    private readonly Dictionary<string, long> _armStarts = new(StringComparer.Ordinal);
    private readonly B10AbruptTimeline _timeline = timeline ?? new B10AbruptTimeline(clock, "supervisor", null);
    private readonly Dictionary<string, Exception> _armFailureCauses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _acceptedOriginals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _preparedTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _acceptedTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _ackSentTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _terminalTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _acceptedAt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _stopRequestedAt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _terminalBeforeAccepted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _stopDrainedFrames = new(StringComparer.Ordinal);
    private Exception? _cleanupFailureCause;
    private Exception? _cleanupCaptureCause;
    private long _ackSequence;
    private bool _killRequested;
    private CleanupDiagnostic? _cleanup;
    private sealed record CleanupDiagnostic(string Arm, string? Failure, OwnedProducerProcess.SignalObservation? SignalObservation, string? CaptureFailure);
    private B10AbruptWire.Frame? _killCheckpoint;
    private IB10AbruptProcess? _child;
    private Channel<B10AbruptWire.Message>? _incoming;
    private B10AbruptWire.AvailableMessageReader? _eventReader;
    internal IReadOnlyDictionary<string, string> ArmFailures => _armFailures;

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        IB10AbruptProcess? child = null;
        CancellationTokenSource? pumpCancellation = null;
        B10AbruptWire.AvailableMessageReader? eventReader = null;
        Task? pump = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(options.OutputRoot, "abrupt-child"));
            _files["registration.json"] = await publisher.RegisterDiagnosticAsync(options.Identity(), cancellationToken).ConfigureAwait(false);
            _timeline.Hold(B10AbruptStage.SupervisorRegistered);
            child = start();
            _child = child;
            _timeline.Hold(B10AbruptStage.ChildStarted);
            _incoming = Channel.CreateBounded<B10AbruptWire.Message>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
            pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            eventReader = new B10AbruptWire.AvailableMessageReader(child.Events, child.EventsReady, CaptureIncoming);
            _eventReader = eventReader;
            pump = PumpAsync(eventReader, _incoming.Writer, pumpCancellation.Token);
            while (await _incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_incoming.Reader.TryRead(out var message))
                {
                    await HandleAsync(message, cancellationToken).ConfigureAwait(false);
                    if (_killRequested) break;
                }
                if (_killRequested) break;
            }
            if (!_killRequested)
            {
                await pump.ConfigureAwait(false);
                _timeline.Hold(B10AbruptStage.EventStreamEnded);
                long graceBegin = clock.GetTimestamp();
                await WaitForChildFailureReportAsync(child, cancellationToken).ConfigureAwait(false);
                _timeline.Hold(B10AbruptStage.GraceEnded, Fact(B10AbruptFact.HasExited, child.HasExited));
                throw Failure("b10_abrupt_unexpected_exit", [Fact(B10AbruptFact.HasExited, child.HasExited), Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(graceBegin)), Fact(B10AbruptFact.BudgetMilliseconds, ChildFailureReportGrace)]);
            }
            OwnedProducerProcess.ExitObservation exit = await ObserveExitAsync(child, cancellationToken).ConfigureAwait(false);
            _timeline.Hold(B10AbruptStage.ExitObserved, Fact(B10AbruptFact.ExitCode, exit.ExitCodeLocalObservation ?? -1));
            await journal.AppendAsync("exit", B10AbruptWire.Element(new
            {
                process = child.Identity, wait_for_exit_observed = true, observed_utc = Utc(), monotonic_ticks = clock.GetTimestamp(),
                exit_code_local_observation = exit.ExitCodeLocalObservation, runtime_version = exit.RuntimeVersion
            }), cancellationToken).ConfigureAwait(false);
            await DrainToEofAsync(_incoming.Reader, pump, cancellationToken).ConfigureAwait(false);
            _timeline.Hold(B10AbruptStage.EofComplete);
            if (_before is null) throw Failure("b10_abrupt_cgroup_unavailable");
            B10AbruptCgroup.Observation after = child.CaptureAfter(_before);
            _ = B10AbruptCgroup.Compare(_before, after);
            await CheckCapturedErrorsAsync(cancellationToken).ConfigureAwait(false);
            if (validateCapturedStatistics is not null) await validateCapturedStatistics(cancellationToken).ConfigureAwait(false);
            await FinalizeSelectionAsync(cancellationToken).ConfigureAwait(false);
            B10AbruptWire.Descriptor journalDescriptor = await journal.SealAsync(cancellationToken).ConfigureAwait(false);
            _files["journal.jsonl"] = await B10AbruptPublisher.ReadFileAsync(journal.Path, 16777216, cancellationToken).ConfigureAwait(false);
            if (journal.Gaps.Count != 0) throw Failure("b10_abrupt_capture_gap", [Fact(B10AbruptFact.JournalGapCount, journal.Gaps.Count)]);
            _files["abrupt-manifest.json"] = B10AbruptWire.SerializeEnvelope("abrupt_manifest", options.Identity(), new
            {
                journal = journalDescriptor, journal_entry_count = journal.Frames.Count,
                timeout_seal = B10AbruptPublisher.Describe("timeout-seal.json", _files["timeout-seal.json"]),
                control_seal = B10AbruptPublisher.Describe("control-seal.json", _files["control-seal.json"]),
                selection = B10AbruptPublisher.Describe("kill-selection.json", _files["kill-selection.json"]),
                gaps = journal.Gaps, registration = B10AbruptPublisher.Describe("registration.json", _files["registration.json"])
            });
            _files["termination.json"] = B10AbruptWire.SerializeEnvelope("termination", options.Identity(), new
            {
                manifest = B10AbruptPublisher.Describe("abrupt-manifest.json", _files["abrupt-manifest.json"]),
                process = child.Identity, signals = _signals, exit, cgroup_before = _before, cgroup_after = after,
                eof_complete = true, mechanical_status = "complete", fatal_capture_status = "not_implemented_pr3",
                native_tail = "unknown_due_to_intended_kill"
            });
            await publisher.PublishFirstAsync(options.Identity(), _files.Where(pair => pair.Key != "registration.json")
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
            _timeline.Mark(B10AbruptStage.PublicationComplete);
        }
        catch (Exception failure)
        {
            _timeline.Hold(B10AbruptStage.FailureObserved);
            await AbortAsync(CancellationToken.None).ConfigureAwait(false);
            _timeline.Mark(B10AbruptStage.AbortComplete, Fact(B10AbruptFact.CleanupFailed, _cleanup?.Failure is not null));
            try
            {
                using var diagnosticDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
                byte[] Build(int reduction) => B10AbruptWire.SerializeEnvelope("failed_diagnostic", options.Identity(), new
                {
                    mechanical_status = "incomplete", failure = FailureName(failure),
                    exception_type = failure.GetType().FullName ?? failure.GetType().Name,
                    diagnostic = Describe(failure, reduce: reduction >= 2),
                    process = child?.Identity, kill_requested = _killRequested, has_exited = child?.HasExited,
                    capture_gaps = journal.Gaps, arm_failures = _armFailures, fatal_capture_status = "not_implemented_pr3",
                    signal_observation = (failure as OwnedProducerProcess.SignalException)?.Observation,
                    cleanup = _cleanup is null ? null : new
                    {
                        arm = _cleanup.Arm, failure = _cleanup.Failure, signal_observation = _cleanup.SignalObservation,
                        capture_failure = _cleanup.CaptureFailure,
                        failure_diagnostic = _cleanupFailureCause is { } cause ? Describe(cause, reduce: reduction >= 2) : null,
                        capture_diagnostic = _cleanupCaptureCause is { } capture ? Describe(capture, reduce: reduction >= 2) : null
                    },
                    startup_cleanup = failure is OwnedProducerProcess.StartupException startup ? new
                    {
                        failure = startup.CleanupFailure is { } cleanupFailure ? FailureName(cleanupFailure) : null,
                        exception_type = startup.CleanupFailure?.GetType().FullName ?? startup.CleanupFailure?.GetType().Name,
                        diagnostic = startup.CleanupFailure is { } cleanupCause ? Describe(cleanupCause, reduce: reduction >= 2) : null,
                        signal_observation = (startup.CleanupFailure as OwnedProducerProcess.SignalException)?.Observation
                    } : null,
                    timeline = _timeline.Snapshot(omitEntries: reduction >= 1)
                });
                byte[] diagnostic;
                try { diagnostic = Build(0); }
                catch (InvalidDataException first) when (first.Message == "b10_abrupt_capture_gap:frame_oversize")
                {
                    try { diagnostic = Build(1); }
                    catch (InvalidDataException second) when (second.Message == "b10_abrupt_capture_gap:frame_oversize") { diagnostic = Build(2); }
                }
                Console.Error.WriteLine(Encoding.UTF8.GetString(diagnostic));
                await using var file = new FileStream(Path.Combine(options.OutputRoot, "abrupt", "failure.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await file.WriteAsync(diagnostic, diagnosticDeadline.Token).ConfigureAwait(false);
                file.Flush(true);
            }
            catch (Exception) { journal.LatchGap("writer_failed"); }
            throw;
        }
        finally
        {
            try
            {
                if (pumpCancellation is not null) await pumpCancellation.CancelAsync().ConfigureAwait(false);
                if (pump is not null)
                {
                    try { await pump.ConfigureAwait(false); }
                    catch (Exception) when (pumpCancellation?.IsCancellationRequested == true) { }
                }
            }
            finally
            {
                try { eventReader?.Dispose(); }
                finally
                {
                    try { pumpCancellation?.Dispose(); }
                    finally { child?.Dispose(); }
                }
            }
        }
    }

    internal async Task WaitForChildFailureReportAsync(IB10AbruptProcess child, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.HasExited) return;
            TimeSpan remaining = ChildFailureReportGrace - clock.GetElapsedTime(begin);
            if (remaining <= TimeSpan.Zero) return;
            TimeSpan delay = remaining < ChildFailureReportPoll ? remaining : ChildFailureReportPoll;
            await WaitForChildFailureReportPollAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForChildFailureReportPollAsync(
        TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using ITimer timer = clock.CreateTimer(
            _ => completion.TrySetResult(), null, delay, Timeout.InfiniteTimeSpan);
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        await completion.Task.ConfigureAwait(false);
    }

    private void CaptureIncoming(B10AbruptWire.Message message)
    {
        if (!_incoming!.Writer.TryWrite(message))
        {
            journal.LatchGap("queue_full");
            throw B10AbruptWire.Gap("queue_full");
        }
    }

    private B10AbruptCgroup.Observation? _before;
    private async Task PumpAsync(B10AbruptWire.AvailableMessageReader reader, ChannelWriter<B10AbruptWire.Message> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!await DrainAvailableAsync(reader, cancellationToken).ConfigureAwait(false))
                await Task.Delay(TimeSpan.FromMilliseconds(1), clock, cancellationToken).ConfigureAwait(false);
            writer.TryComplete();
        }
        catch (Exception exception) { writer.TryComplete(exception); throw; }
    }

    private async Task<bool> DrainAvailableAsync(B10AbruptWire.AvailableMessageReader reader, CancellationToken cancellationToken, bool requireCompleteFrame = false)
    {
        try { return await reader.DrainAvailableAsync(cancellationToken, requireCompleteFrame).ConfigureAwait(false); }
        catch (InvalidDataException exception)
        {
            string reason = exception.Message.StartsWith("b10_abrupt_capture_gap:", StringComparison.Ordinal) ? exception.Message.Split(':')[^1] : "writer_failed";
            journal.LatchGap(reason);
            throw;
        }
        catch (IOException)
        {
            journal.LatchGap("writer_failed");
            throw B10AbruptWire.Gap("writer_failed");
        }
    }

    internal async Task HandleAsync(B10AbruptWire.Message message, CancellationToken cancellationToken, bool afterKill = false)
    {
        string? attempt = message.Data.TryGetProperty("attempt_id", out var id) ? id.GetString() : null;
        switch (message.Kind)
        {
            case "prepared":
                if (attempt is null || _prepared.ContainsKey(attempt)) throw Failure("b10_abrupt_capture_gap", [Fact(B10AbruptFact.AttemptPresent, attempt is not null), Fact(B10AbruptFact.Duplicate, attempt is not null && _prepared.ContainsKey(attempt))]);
                string cohort = message.Data.GetProperty("cohort").GetString()!;
                if (!afterKill)
                {
                    if (cohort == "same_instance") await LoadSealAsync("timeout", cancellationToken).ConfigureAwait(false);
                    else if (cohort == "cross_restart") await LoadSealAsync("control", cancellationToken).ConfigureAwait(false);
                    else throw Failure("b10_abrupt_control_incomplete", [Fact(B10AbruptFact.Cohort, ArmCode(cohort))]);
                }
                if (message.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null) _armStarts.TryAdd(cohort, clock.GetTimestamp());
                var prepared = await journal.AppendPreparedAndFlushAsync(message.Data, cancellationToken).ConfigureAwait(false);
                _preparedTicks[attempt] = clock.GetTimestamp();
                if (RecordMemberStage(prepared)) _timeline.Hold(B10AbruptStage.FramePrepared, Fact(B10AbruptFact.Arm, ArmCode(cohort)), Fact(B10AbruptFact.IsOriginal, message.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null));
                _prepared.Add(attempt, prepared);
                if (!afterKill) await AckAsync(prepared.Sequence, cancellationToken).ConfigureAwait(false);
                break;
            case "pre_acceptance_report":
                await AcceptReportAsync(message.Data, cancellationToken).ConfigureAwait(false);
                string reportArm = message.Data.TryGetProperty("arm", out var reportArmValue) && reportArmValue.ValueKind == JsonValueKind.String ? reportArmValue.GetString()! : "other";
                _timeline.Hold(B10AbruptStage.FrameReport, Fact(B10AbruptFact.Arm, ArmCode(reportArm)), Fact(B10AbruptFact.ArmFailed, _armFailures.ContainsKey(reportArm)));
                break;
            case "accepted":
                B10AbruptWire.Frame? original = null;
                var acceptanceFacts = new List<B10AbruptFactValue>();
                if (!(Check(acceptanceFacts, B10AbruptFact.AttemptPresent, attempt is not null) &&
                    Check(acceptanceFacts, B10AbruptFact.PreparedKnown, _prepared.TryGetValue(attempt!, out original)) &&
                    Check(acceptanceFacts, B10AbruptFact.PreparedSequenceMatches, message.Data.GetProperty("prepared_sequence").GetInt64() == original!.Sequence)) ||
                    Check(acceptanceFacts, B10AbruptFact.Duplicate, !_accepted.Add(attempt!))) throw Failure("b10_abrupt_capture_gap", [.. acceptanceFacts]);
                string arm = original!.Data.GetProperty("cohort").GetString()!;
                bool isOriginal = original!.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null;
                long sequence = original.Data.GetProperty("sequence").GetInt64();
                bool lastOriginal = false;
                if (isOriginal)
                {
                    if (!_acceptedOriginals.TryGetValue(arm, out var originals)) _acceptedOriginals.Add(arm, originals = []);
                    if (sequence != originals.Count || sequence >= B10AbruptOptions.ProbeBurstSize ||
                        (originals.Count != 0 && original.Data.GetProperty("invocation_ordinal").GetInt64() <= _prepared[originals[^1]].Data.GetProperty("invocation_ordinal").GetInt64()))
                        throw Failure("b10_abrupt_capture_gap");
                    originals.Add(attempt!);
                    _acceptedTicks[attempt!] = clock.GetTimestamp();
                    lastOriginal = sequence == B10AbruptOptions.ProbeBurstSize - 1;
                    if (lastOriginal) { _acceptedAt[arm] = _acceptedTicks[attempt!]; _terminalBeforeAccepted[arm] = _terminals.ContainsKey(attempt!); }
                    if (sequence == 0 && !_reports.ContainsKey(arm)) _armFailures.TryAdd(arm, "b10_abrupt_sample_missing");
                }
                var accepted = await journal.AppendAcceptedAndFlushAsync(message.Data, cancellationToken).ConfigureAwait(false);
                if (RecordMemberStage(original)) _timeline.Hold(B10AbruptStage.FrameAccepted, Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.IsOriginal, isOriginal), Fact(B10AbruptFact.TerminalBeforeAccepted, _terminals.ContainsKey(attempt!)));
                if (!afterKill)
                {
                    await AckAsync(accepted.Sequence, cancellationToken).ConfigureAwait(false);
                    if (isOriginal) _ackSentTicks[attempt!] = clock.GetTimestamp();
                    if (RecordMemberStage(original)) _timeline.Hold(B10AbruptStage.AckSent, Fact(B10AbruptFact.Arm, ArmCode(arm)));
                }
                if (isOriginal && _armFailures.TryGetValue(arm, out string? failure)) throw Failure(failure, [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.ReportPresent, _reports.ContainsKey(arm))], _armFailureCauses.GetValueOrDefault(arm));
                if (!afterKill && lastOriginal)
                {
                    if (arm == "same_instance") await CheckpointControlAsync(attempt!, cancellationToken).ConfigureAwait(false);
                    else await CheckpointKillAsync(attempt!, cancellationToken).ConfigureAwait(false);
                }
                break;
            case "terminal":
                if (attempt is null || !_prepared.TryGetValue(attempt, out var terminalPrepared) || _terminals.ContainsKey(attempt)) throw Failure("b10_abrupt_capture_gap", [Fact(B10AbruptFact.AttemptPresent, attempt is not null), Fact(B10AbruptFact.PreparedKnown, attempt is not null && _prepared.ContainsKey(attempt)), Fact(B10AbruptFact.Duplicate, attempt is not null && _terminals.ContainsKey(attempt))]);
                var terminal = await journal.AppendAsync("terminal", message.Data, cancellationToken).ConfigureAwait(false);
                _terminalTicks[attempt] = clock.GetTimestamp();
                _terminals.Add(attempt, terminal);
                JsonElement persistence = message.Data.GetProperty("persistence_status");
                if (persistence.ValueKind != JsonValueKind.Number || !persistence.TryGetInt32(out int status)) throw Failure("b10_abrupt_guard_failed", [Fact(B10AbruptFact.PersistenceStatus, -1)]);
                bool delivered = message.Data.GetProperty("delivered").GetBoolean();
                int errorCode = message.Data.GetProperty("error_code").GetInt32();
                bool permitted = B10AbruptProducer.IsPermittedTerminal(errorCode, status);
                if (delivered != (errorCode == 0) || !permitted) throw Failure("b10_abrupt_guard_failed", [Fact(B10AbruptFact.ErrorCode, errorCode), Fact(B10AbruptFact.PersistenceStatus, status), Fact(B10AbruptFact.Delivered, delivered), Fact(B10AbruptFact.Permitted, permitted)]);
                if (RecordMemberStage(terminalPrepared)) _timeline.Hold(B10AbruptStage.FrameTerminal, Fact(B10AbruptFact.Arm, ArmCode(_prepared.GetValueOrDefault(attempt)?.Data.GetProperty("cohort").GetString())), Fact(B10AbruptFact.ErrorCode, errorCode), Fact(B10AbruptFact.PersistenceStatus, status), Fact(B10AbruptFact.Delivered, delivered), Fact(B10AbruptFact.AfterKill, afterKill));
                break;
            case "synchronous_rejection":
                var rejection = await journal.AppendAsync(message.Kind, message.Data, cancellationToken).ConfigureAwait(false);
                if (!afterKill) await AckAsync(rejection.Sequence, cancellationToken).ConfigureAwait(false);
                throw Failure("b10_abrupt_guard_failed", [Fact(B10AbruptFact.ErrorCode, NumberOrMinusOne(message.Data, "error_code"))]);
            case "capture_gap":
                string reason = message.Data.GetProperty("reason").GetString()!;
                journal.LatchGap(reason);
                throw Failure("b10_abrupt_capture_gap", [Fact(B10AbruptFact.GapReason, GapReasonCode(reason))]);
            default: throw Failure("b10_abrupt_capture_gap");
        }
    }

    internal async Task AcceptReportAsync(JsonElement data, CancellationToken cancellationToken)
    {
        string arm = data.TryGetProperty("arm", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "unknown";
        if (_armFailures.ContainsKey(arm)) return;
        try
        {
            if (arm is not ("same_instance" or "cross_restart") || _reports.ContainsKey(arm)) throw Failure("b10_abrupt_report_binding_invalid", [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.Duplicate, _reports.ContainsKey(arm))]);
            string attempt = data.GetProperty("attempt_id").GetString()!;
            if (!_prepared.TryGetValue(attempt, out var prepared) || prepared.Data.GetProperty("cohort").GetString() != arm || _accepted.Contains(attempt) ||
                (prepared.Data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null || prepared.Data.GetProperty("sequence").GetInt64() != 0)) throw Failure("b10_abrupt_report_binding_invalid", [Fact(B10AbruptFact.PreparedKnown, _prepared.ContainsKey(attempt)), Fact(B10AbruptFact.Duplicate, _accepted.Contains(attempt))]);
            B10AbruptWire.ExactMembers(data, "arm", "attempt_id", "report_length", "report_sha256", "report_identity", "report_sequence", "report_capture_utc");
            long length = data.GetProperty("report_length").GetInt64();
            if (length is < 1 or > 1048576 || data.GetProperty("report_sequence").GetInt64() < 1) throw Failure("b10_abrupt_report_binding_invalid", [Fact(B10AbruptFact.ReportLength, length), Fact(B10AbruptFact.ReportSequence, NumberOrMinusOne(data, "report_sequence"))]);
            JsonElement identity = data.GetProperty("report_identity");
            B10AbruptWire.ExactMembers(identity, "scenario_id", "run_id", "role", "client_id");
            var identityFacts = new List<B10AbruptFactValue>();
            if (!(Check(identityFacts, B10AbruptFact.ScenarioMatches, identity.GetProperty("scenario_id").GetString() == options.ScenarioId) &&
                Check(identityFacts, B10AbruptFact.RunMatches, identity.GetProperty("run_id").GetString() == options.RunId) &&
                Check(identityFacts, B10AbruptFact.RoleMatches, identity.GetProperty("role").GetString() == "LoadGenProducer") &&
                Check(identityFacts, B10AbruptFact.ClientIdPresent, !string.IsNullOrEmpty(identity.GetProperty("client_id").GetString())) &&
                Check(identityFacts, B10AbruptFact.UtcValid, B10AbruptWire.IsUtc(data.GetProperty("report_capture_utc").GetString() ?? "")))) throw Failure("b10_abrupt_report_binding_invalid", [.. identityFacts]);
            string name = $"abrupt-child/{arm}-pre-acceptance-report.json";
            byte[] bytes = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, name), 1048576, cancellationToken).ConfigureAwait(false);
            if (bytes.Length != length || B10AbruptPublisher.Hash(bytes) != data.GetProperty("report_sha256").GetString()) throw Failure("b10_abrupt_report_binding_invalid", [Fact(B10AbruptFact.FileLength, bytes.Length), Fact(B10AbruptFact.ReportLength, length), Fact(B10AbruptFact.HashMatches, B10AbruptPublisher.Hash(bytes) == data.GetProperty("report_sha256").GetString())]);
            _reports.Add(arm, B10AbruptWire.Element(new { name, length, sha256 = B10AbruptPublisher.Hash(bytes),
                report_identity = identity, report_sequence = data.GetProperty("report_sequence"), report_capture_utc = data.GetProperty("report_capture_utc") }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { if (_armFailures.TryAdd(arm, "b10_abrupt_report_binding_invalid")) _armFailureCauses[arm] = exception; }
    }

    internal async Task CheckpointControlAsync(string attempt, CancellationToken cancellationToken)
    {
        var checkpoint = await StopCheckpointAsync("same_instance", attempt, cancellationToken).ConfigureAwait(false);
        if (checkpoint.Data.GetProperty("selected_attempt_ids").GetArrayLength() == 0) throw Failure("b10_abrupt_exposure_inconclusive", [.. ExposureFacts("same_instance", attempt, checkpoint.Data)]);
        var resumed = _child!.Resume();
        _timeline.Hold(B10AbruptStage.ResumeRequested);
        await RecordSignalAsync("same_instance", resumed, 0, cancellationToken).ConfigureAwait(false);
        await SaveCheckpointAsync("same_instance", checkpoint, cancellationToken).ConfigureAwait(false);
        string progressPath = Path.Combine(options.OutputRoot, "abrupt-child", "same_instance-resumed.json");
        long progressBegin = clock.GetTimestamp();
        using var progressDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var progressLinked = CancellationTokenSource.CreateLinkedTokenSource(progressDeadline.Token, cancellationToken);
        try
        {
            while (!File.Exists(progressPath)) await Task.Delay(TimeSpan.FromMilliseconds(10), clock, progressLinked.Token).ConfigureAwait(false);
            JsonElement progress = B10AbruptWire.Parse(await B10AbruptPublisher.ReadFileAsync(progressPath, 65536, progressLinked.Token).ConfigureAwait(false));
            B10AbruptWire.ExactMembers(progress, "arm", "identity", "checkpoint_sequence", "checkpoint_sha256");
            var progressFacts = new List<B10AbruptFactValue>();
            if (!(Check(progressFacts, B10AbruptFact.ArmMatches, progress.GetProperty("arm").GetString() == "same_instance") &&
                Check(progressFacts, B10AbruptFact.IdentityMatches, progress.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) == options.Identity()) &&
                Check(progressFacts, B10AbruptFact.SequenceMatches, progress.GetProperty("checkpoint_sequence").GetInt64() == checkpoint.Sequence) &&
                Check(progressFacts, B10AbruptFact.HashMatches, progress.GetProperty("checkpoint_sha256").GetString() == B10AbruptPublisher.Hash(B10AbruptWire.WriteFrame(checkpoint)))))
                throw Failure("b10_abrupt_resume_unconfirmed", [.. progressFacts]);
            _timeline.Hold(B10AbruptStage.ResumeConfirmed, Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(progressBegin)));
        }
        catch (OperationCanceledException exception) { throw Failure("b10_abrupt_resume_unconfirmed", [Fact(B10AbruptFact.ProgressExists, File.Exists(progressPath)), Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(progressBegin)), Fact(B10AbruptFact.BudgetMilliseconds, TimeSpan.FromSeconds(30))], exception); }
    }

    internal async Task CheckpointKillAsync(string attempt, CancellationToken cancellationToken)
    {
        _before = _child!.CaptureBefore();
        var checkpoint = await StopCheckpointAsync("cross_restart", attempt, cancellationToken).ConfigureAwait(false);
        _killCheckpoint = checkpoint;
        await SaveCheckpointAsync("cross_restart", checkpoint, cancellationToken).ConfigureAwait(false);
        if (checkpoint.Data.GetProperty("accepted_attempt_ids").GetArrayLength() == 0) throw Failure("kill_too_early", [.. ExposureFacts("cross_restart", attempt, checkpoint.Data)]);
        if (checkpoint.Data.GetProperty("selected_attempt_ids").GetArrayLength() == 0) throw Failure("kill_too_late:no_unconfirmed_attempt", [.. ExposureFacts("cross_restart", attempt, checkpoint.Data)]);
        var intent = await RecordSignalAsync("cross_restart", new(9, null, null), 0, cancellationToken).ConfigureAwait(false);
        _timeline.Hold(B10AbruptStage.KillIntent);
        _killRequested = true;
        await RequestKillAsync(_child, intent.Sequence, cancellationToken).ConfigureAwait(false);
    }

    private async Task<B10AbruptWire.Frame> StopCheckpointAsync(string arm, string attempt, CancellationToken cancellationToken)
    {
        long begin = _armStarts.GetValueOrDefault(arm, clock.GetTimestamp());
        TimeSpan budget = TimeSpan.FromSeconds(30);
        TimeSpan remaining = budget - clock.GetElapsedTime(begin);
        B10AbruptFactValue[] Timing(params B10AbruptFactValue[] more) => [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget), Fact(B10AbruptFact.RemainingMilliseconds, remaining), .. more];
        if (remaining < TimeSpan.Zero) throw Failure("b10_abrupt_stop_unconfirmed", Timing());
        // One tick beyond the inclusive deadline lets an already completed operation at the boundary be observed.
        using var deadline = new CancellationTokenSource(remaining + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        bool stoppedObserved = false;
        try
        {
            if (_child is null || _child.HasExited) throw Failure("b10_abrupt_unexpected_exit", [Fact(B10AbruptFact.ChildPresent, _child is not null), Fact(B10AbruptFact.HasExited, _child?.HasExited == true)]);
            _stopRequestedAt[arm] = clock.GetTimestamp();
            _stopDrainedFrames[arm] = 0;
            await RecordSignalAsync(arm, _child.Stop(), 0, linked.Token).ConfigureAwait(false);
            _timeline.Hold(B10AbruptStage.StopRequested, Fact(B10AbruptFact.Arm, ArmCode(arm)));
            remaining = budget - clock.GetElapsedTime(begin);
            bool confirmed = remaining >= TimeSpan.Zero && _child.ConfirmStopped(remaining, clock);
            if (!confirmed || clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_stop_unconfirmed", Timing(Fact(B10AbruptFact.ConfirmResult, confirmed)));
            _timeline.Hold(B10AbruptStage.StopConfirmed, Timing());
            stoppedObserved = true;
            if (_eventReader is not null) await DrainAvailableAsync(_eventReader, linked.Token, requireCompleteFrame: true).ConfigureAwait(false);
            if (_incoming is not null)
                while (_incoming.Reader.TryRead(out var message))
                {
                    await HandleAsync(message, linked.Token, afterKill: true).ConfigureAwait(false);
                    _stopDrainedFrames[arm]++;
                    if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_deadline_exhausted", Timing(Fact(B10AbruptFact.FramesDrainedAfterStop, _stopDrainedFrames[arm])));
                }
            if (!_reports.TryGetValue(arm, out var report)) throw Failure(_armFailures.GetValueOrDefault(arm, "b10_abrupt_sample_missing"), [Fact(B10AbruptFact.Arm, ArmCode(arm)), Fact(B10AbruptFact.ReportPresent, false), Fact(B10AbruptFact.ArmFailed, _armFailures.ContainsKey(arm))], _armFailureCauses.GetValueOrDefault(arm));
            string[] originals = _acceptedOriginals.TryGetValue(arm, out var acceptedOriginals) ? [.. acceptedOriginals] : [];
            DateTimeOffset utc = clock.GetUtcNow();
            var checkpoint = await journal.CheckpointAsync(B10AbruptWire.Element(new
            {
                arm, accepted_attempt_ids = originals,
                selected_attempt_ids = originals.Where(id => !_terminals.ContainsKey(id)).ToArray(),
                last_complete_sequence = journal.Frames.Count == 0 ? 0 : journal.Frames[^1].Sequence, pre_acceptance_report = report,
                process = _child.Identity, checkpoint_utc = B10AbruptPublisher.Utc(utc), checkpoint_monotonic_ticks = clock.GetTimestamp(),
                release_utc = B10AbruptPublisher.Utc(utc.AddSeconds(120))
            }), linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            _timeline.HoldSupervisorBurst(ArmCode(arm), _stopRequestedAt[arm], originals.Select(id => new B10AbruptSupervisorBurstMember(
                _prepared[id].Data.GetProperty("sequence").GetInt64(), _preparedTicks[id], _acceptedTicks[id],
                _ackSentTicks.GetValueOrDefault(id, -1), _terminalTicks.GetValueOrDefault(id, -1))).ToArray());
            _timeline.Mark(B10AbruptStage.CheckpointWritten, [.. ExposureFacts(arm, attempt, checkpoint.Data)]);
            if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_deadline_exhausted", Timing(Fact(B10AbruptFact.FramesDrainedAfterStop, _stopDrainedFrames[arm])));
            return checkpoint;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw Failure(stoppedObserved ? "b10_abrupt_deadline_exhausted" : "b10_abrupt_stop_unconfirmed", Timing(Fact(B10AbruptFact.StopObserved, stoppedObserved)), exception); }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        { throw Failure(stoppedObserved ? "b10_abrupt_deadline_exhausted" : "b10_abrupt_stop_unconfirmed", Timing(Fact(B10AbruptFact.StopObserved, stoppedObserved)), exception); }
    }

    internal async Task RequestKillAsync(IB10AbruptProcess child, long intentSequence, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        TimeSpan budget = TimeSpan.FromSeconds(10);
        OwnedProducerProcess.SignalObservation? observation = null;
        B10AbruptFactValue[] KillTiming() => [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget), Fact(B10AbruptFact.ObservationPresent, observation is not null)];
        using var deadline = new CancellationTokenSource(budget + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            observation = await Task.Run(child.Kill, cancellationToken).WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw Attach(new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation), KillTiming());
            await RecordSignalAsync("cross_restart", observation, intentSequence, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            _timeline.Hold(B10AbruptStage.KillRequested, KillTiming());
            if (clock.GetElapsedTime(begin) > budget) throw Attach(new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation), KillTiming());
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (observation is not null) throw Attach(new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation), KillTiming());
            throw Failure("b10_abrupt_deadline_exhausted", KillTiming(), exception);
        }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        {
            if (observation is not null) throw Attach(new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation), KillTiming());
            throw Failure("b10_abrupt_deadline_exhausted", KillTiming(), exception);
        }
    }

    internal async Task<OwnedProducerProcess.ExitObservation> ObserveExitAsync(IB10AbruptProcess child, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        TimeSpan budget = TimeSpan.FromSeconds(10);
        using var deadline = new CancellationTokenSource(budget + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            var exit = await Task.Run(child.ObserveExit, cancellationToken).WaitAsync(linked.Token).ConfigureAwait(false);
            if (!exit.WaitForExitObserved || clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_exit_unobserved", [Fact(B10AbruptFact.WaitObserved, exit.WaitForExitObserved), Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget)]);
            return exit;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw Failure("b10_abrupt_exit_unobserved", [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget)], exception); }
    }

    internal async Task DrainToEofAsync(ChannelReader<B10AbruptWire.Message> reader, Task pump, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        TimeSpan budget = TimeSpan.FromSeconds(10);
        using var deadline = new CancellationTokenSource(budget + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        int drained = 0;
        try
        {
            await foreach (var message in reader.ReadAllAsync(linked.Token).ConfigureAwait(false))
            {
                await HandleAsync(message, linked.Token, afterKill: true).ConfigureAwait(false);
                drained++;
            }
            await pump.WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_eof_incomplete", [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget), Fact(B10AbruptFact.FramesDrainedAfterKill, drained)]);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw Failure("b10_abrupt_eof_incomplete", [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget), Fact(B10AbruptFact.FramesDrainedAfterKill, drained)], exception); }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        { throw Failure("b10_abrupt_eof_incomplete", [Fact(B10AbruptFact.ElapsedMilliseconds, clock.GetElapsedTime(begin)), Fact(B10AbruptFact.BudgetMilliseconds, budget), Fact(B10AbruptFact.FramesDrainedAfterKill, drained)], exception); }
    }

    internal async Task FinalizeSelectionAsync(CancellationToken cancellationToken)
    {
        if (_armFailures.Count != 0) { string firstArm = _armFailures.Keys.First(); throw Failure(_armFailures[firstArm], [Fact(B10AbruptFact.ArmFailureCount, _armFailures.Count), Fact(B10AbruptFact.Arm, ArmCode(firstArm))], _armFailureCauses.GetValueOrDefault(firstArm)); }
        if (_killCheckpoint is null) throw Failure("kill_too_early");
        string[] candidates = _killCheckpoint.Data.GetProperty("selected_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var entries = new List<JsonElement>();
        foreach (string attempt in candidates.Where(attempt => !_terminals.ContainsKey(attempt)).Take(1))
        {
            JsonElement prepared = _prepared[attempt].Data;
            byte[] payload = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt-child", "cross_restart-payload.bin"), 900000, cancellationToken).ConfigureAwait(false);
            var payloadFacts = new List<B10AbruptFactValue> { Fact(B10AbruptFact.FileLength, payload.Length) };
            if (!(Check(payloadFacts, B10AbruptFact.LengthMatches, payload.Length == prepared.GetProperty("payload_length").GetInt64()) &&
                Check(payloadFacts, B10AbruptFact.HashMatches, B10AbruptPublisher.Hash(payload) == prepared.GetProperty("payload_sha256").GetString()))) throw Failure("b10_abrupt_capture_gap", [.. payloadFacts]);
            entries.Add(B10AbruptWire.Element(new { attempt_id = attempt, key = prepared.GetProperty("key"), sequence = prepared.GetProperty("sequence"),
                payload_length = payload.Length, payload_sha256 = B10AbruptPublisher.Hash(payload), payload_base64 = Convert.ToBase64String(payload) }));
        }
        _files["kill-selection.json"] = B10AbruptWire.SerializeEnvelope("selection", options.Identity(), new
        {
            checkpoint = B10AbruptWire.Parse(B10AbruptWire.WriteFrame(_killCheckpoint)),
            checkpoint_descriptor = B10AbruptPublisher.Describe("cross_restart-checkpoint.json", B10AbruptWire.WriteFrame(_killCheckpoint)), candidate_attempt_ids = candidates,
            terminal_exclusions = candidates.Where(_terminals.ContainsKey).Select(attempt => new { attempt_id = attempt, terminal_sequence = _terminals[attempt].Sequence }).ToArray(), entries
        });
        _timeline.Hold(B10AbruptStage.SelectionFinalized, Fact(B10AbruptFact.CandidateCount, candidates.Length), Fact(B10AbruptFact.TerminalExclusionCount, candidates.Count(_terminals.ContainsKey)), Fact(B10AbruptFact.SelectedCount, entries.Count));
        if (entries.Count == 0) throw Failure("kill_too_late:no_unconfirmed_attempt", [Fact(B10AbruptFact.CandidateCount, candidates.Length), Fact(B10AbruptFact.TerminalExclusionCount, candidates.Count(_terminals.ContainsKey)), Fact(B10AbruptFact.SelectedCount, entries.Count)]);
    }

    private async Task LoadSealAsync(string cohort, CancellationToken cancellationToken)
    {
        string name = cohort + "-seal.json";
        if (_files.ContainsKey(name)) return;
        byte[] bytes = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt-child", name), 65536, cancellationToken).ConfigureAwait(false);
        JsonElement seal = B10AbruptWire.ReadEnvelope(bytes, "cohort_seal");
        JsonElement data = seal.GetProperty("data");
        var sealFacts = new List<B10AbruptFactValue> { Fact(B10AbruptFact.Cohort, ArmCode(cohort == "control" ? "same_instance" : cohort)) };
        if (!(Check(sealFacts, B10AbruptFact.IdentityMatches, seal.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) == options.Identity()) &&
            Check(sealFacts, B10AbruptFact.CohortMatches, data.GetProperty("cohort").GetString() == (cohort == "control" ? "same_instance" : cohort)) &&
            Check(sealFacts, B10AbruptFact.DrainComplete, data.GetProperty("drain_complete").GetBoolean()) &&
            Check(sealFacts, B10AbruptFact.GuardComplete, data.GetProperty("guard_complete").GetBoolean()) &&
            Check(sealFacts, B10AbruptFact.FatalStatusMatches, data.GetProperty("fatal_capture_status").GetString() == "not_implemented_pr3"))) throw Failure("b10_abrupt_control_incomplete", [.. sealFacts]);
        string directory = Path.Combine(options.OutputRoot, cohort);
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pair in new[] { ("inventory", "manifest.json", 67108864), ("ledger", "ledger.jsonl", 268435456), ("post_drain_statistics", "post-drain-report.json", 1048576) })
        {
            artifacts[pair.Item2] = await B10AbruptPublisher.ReadFileAsync(Path.Combine(directory, pair.Item2), pair.Item3, cancellationToken).ConfigureAwait(false);
            B10AbruptPublisher.CheckDescriptor(data.GetProperty(pair.Item1), pair.Item2, artifacts);
        }
        if (cohort == "timeout")
        {
            var rows = await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"),
                new RunIdentity(options.ScenarioId, options.ConfigurationId, options.RunId, options.PodName, options.PodName, options.PodName),
                186000, 16384, 268435456, 67108864, cancellationToken).ConfigureAwait(false);
            if (rows.Length != data.GetProperty("count").GetInt64()) throw Failure("b10_abrupt_control_incomplete", [Fact(B10AbruptFact.Cohort, 0), Fact(B10AbruptFact.RowCount, rows.Length), Fact(B10AbruptFact.SealCount, NumberOrMinusOne(data, "count"))]);
        }
        else
        {
            JsonElement inventory = B10AbruptWire.ReadEnvelope(artifacts["manifest.json"], "cohort_inventory").GetProperty("data");
            var entries = inventory.GetProperty("inventory").EnumerateArray().ToArray();
            string[] lines = Encoding.UTF8.GetString(artifacts["ledger.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var controlFacts = new List<B10AbruptFactValue> { Fact(B10AbruptFact.Cohort, 1), Fact(B10AbruptFact.EntryCount, entries.Length), Fact(B10AbruptFact.LineCount, lines.Length), Fact(B10AbruptFact.InventoryCount, NumberOrMinusOne(inventory, "inventory_count")), Fact(B10AbruptFact.SealCount, NumberOrMinusOne(data, "count")) };
            if (entries.Length != B10AbruptOptions.ProbeBurstSize + 1 || lines.Length != B10AbruptOptions.ProbeBurstSize + 1 || inventory.GetProperty("inventory_count").GetInt32() != B10AbruptOptions.ProbeBurstSize + 1 || data.GetProperty("count").GetInt32() != B10AbruptOptions.ProbeBurstSize + 1 ||
                !Check(controlFacts, B10AbruptFact.BookkeepingComplete, inventory.GetProperty("bookkeeping_complete").GetBoolean())) throw Failure("b10_abrupt_control_incomplete", [.. controlFacts]);
            var ids = entries.Select(entry => entry.GetProperty("attempt_id").GetString()!).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != B10AbruptOptions.ProbeBurstSize + 1 || ids.Any(id => !_accepted.Contains(id) || !_terminals.ContainsKey(id))) throw Failure("b10_abrupt_control_incomplete", [Fact(B10AbruptFact.IdCount, ids.Count), Fact(B10AbruptFact.NotAcceptedCount, ids.Count(id => !_accepted.Contains(id))), Fact(B10AbruptFact.NoTerminalCount, ids.Count(id => !_terminals.ContainsKey(id)))]);
            JsonElement resend = entries.Single(entry => entry.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null);
            JsonElement controlCheckpoint = journal.Frames.Last(frame => frame.Kind == "checkpoint" && frame.Data.GetProperty("arm").GetString() == "same_instance").Data;
            if (resend.GetProperty("predecessor_attempt_id").GetString() != controlCheckpoint.GetProperty("selected_attempt_ids")[0].GetString())
                throw Failure("b10_abrupt_control_incomplete");
            foreach (string line in lines)
            {
                JsonElement row = B10AbruptWire.Parse(Encoding.UTF8.GetBytes(line));
                string attempt = row.GetProperty("attempt_id").GetString()!;
                bool known = ids.Remove(attempt); bool raw = known && row.GetRawText() == _terminals[attempt].Data.GetRawText(); if (!raw) throw Failure("b10_abrupt_control_incomplete", [Fact(B10AbruptFact.UnknownAttempt, !known), Fact(B10AbruptFact.RawMatches, raw)]);
            }
        }
        _files.Add(name, bytes);
        _timeline.Hold(B10AbruptStage.SealLoaded, Fact(B10AbruptFact.Cohort, ArmCode(cohort == "control" ? "same_instance" : cohort)), Fact(B10AbruptFact.SealCount, NumberOrMinusOne(data, "count")));
    }

    private async Task CheckCapturedErrorsAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(options.OutputRoot, "capture", "request-lifecycle.jsonl");
        if (!File.Exists(path)) throw Failure("b10_abrupt_capture_gap");
        byte[] bytes = await B10AbruptPublisher.ReadFileAsync(path, 134217728, cancellationToken).ConfigureAwait(false);
        int completeLength = Array.LastIndexOf(bytes, (byte)'\n') + 1;
        foreach (string line in Encoding.UTF8.GetString(bytes, 0, completeLength).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonElement row = B10AbruptWire.Parse(Encoding.UTF8.GetBytes(line), 134217728);
            if (row.TryGetProperty("event_type", out var type) && type.GetString() == "native_error" &&
                row.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True) throw Failure("b10_abrupt_guard_failed");
        }
    }

    private async Task AckAsync(long sequence, CancellationToken cancellationToken)
    {
        if (_child is null) return;
        await B10AbruptWire.WriteMessageAsync(_child.Acknowledgements, new("ack", checked(++_ackSequence),
            B10AbruptWire.Element(new { acknowledged_sequence = sequence })), cancellationToken).ConfigureAwait(false);
    }
    private async Task<B10AbruptWire.Frame> RecordSignalAsync(string arm, OwnedProducerProcess.SignalObservation signal, long intent, CancellationToken cancellationToken)
    {
        JsonElement data = B10AbruptWire.Element(new { arm, requested_signal = signal.RequestedSignal, process = _child!.Identity,
            intent_sequence = intent, request_utc = Utc(), request_monotonic_ticks = clock.GetTimestamp(), return_value = signal.ReturnValue, errno = signal.Errno });
        _signals.Add(data);
        return await journal.AppendAsync("signal", data, cancellationToken).ConfigureAwait(false);
    }
    private async Task SaveCheckpointAsync(string arm, B10AbruptWire.Frame checkpoint, CancellationToken cancellationToken)
    {
        string path = Path.Combine(options.OutputRoot, "abrupt", arm + "-checkpoint.json");
        await using (var file = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.WriteAsync(B10AbruptWire.WriteFrame(checkpoint), cancellationToken).ConfigureAwait(false);
            file.Flush(true);
        }
        File.Move(path + ".pending", path, overwrite: false);
    }
    internal async Task AbortAsync(CancellationToken cancellationToken)
    {
        if (_child is null || _child.HasExited || _killRequested) return;
        _killRequested = true;
        _cleanup = new("cleanup", null, null, null);
        OwnedProducerProcess.SignalObservation observation;
        try { observation = _child.Kill(); }
        catch (OwnedProducerProcess.SignalException exception)
        {
            observation = exception.Observation;
            _cleanup = _cleanup with { Failure = FailureName(exception) };
            _cleanupFailureCause = exception;
        }
        catch (Exception exception)
        {
            _cleanup = _cleanup with { Failure = FailureName(exception) };
            _cleanupFailureCause = exception;
            return;
        }
        _cleanup = _cleanup with { SignalObservation = observation };
        try { await RecordSignalAsync("cleanup", observation, 0, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception)
        {
            // The journal owns capture gaps; a failed native call is not a failed writer.
            _cleanup = _cleanup with { CaptureFailure = FailureName(exception) };
            _cleanupCaptureCause = exception;
        }
        if (_cleanup.Failure is not null) return;
        try
        {
            if (!_child.ObserveExit().WaitForExitObserved) _cleanup = _cleanup with { Failure = "b10_abrupt_exit_unobserved" };
        }
        catch (Exception exception) { _cleanup = _cleanup with { Failure = FailureName(exception) }; _cleanupFailureCause = exception; }
    }
    internal static string FailureName(Exception exception)
    {
        if (exception is OperationCanceledException) return "b10_abrupt_deadline_exhausted";
        string name = exception.Message.Split(':', 2)[0];
        return name is "b10_abrupt_configuration_invalid" or "b10_abrupt_platform_unsupported" or
            "b10_abrupt_signal_preflight_failed" or "b10_abrupt_child_start_failed" or "b10_abrupt_child_identity_mismatch" or
            "b10_abrupt_target_invalid" or "b10_abrupt_signal_failed" or "b10_abrupt_stop_unconfirmed" or
            "b10_abrupt_resume_unconfirmed" or "b10_abrupt_exit_unobserved" or "b10_abrupt_eof_incomplete" or
            "b10_abrupt_unexpected_exit" or "b10_abrupt_capture_gap" or "b10_abrupt_sample_missing" or
            "b10_abrupt_report_binding_invalid" or "b10_abrupt_guard_failed" or "b10_abrupt_control_incomplete" or "b10_abrupt_exposure_inconclusive" or
            "kill_too_early" or "kill_too_late" or "b10_abrupt_cgroup_unavailable" or "b10_abrupt_oom_conflict" or
            "b10_abrupt_publication_failed" or "b10_abrupt_publication_ambiguous" or "b10_abrupt_successor_binding_invalid" or
            "b10_abrupt_successor_late" or "b10_abrupt_deadline_exhausted" ? name : "b10_abrupt_capture_gap";
    }

    private static bool RecordMemberStage(B10AbruptWire.Frame prepared) =>
        prepared.Data.GetProperty("sequence").GetInt64() == 0 || prepared.Data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null;

    private long AcceptedToStop(string arm) => _acceptedAt.TryGetValue(arm, out long accepted) && _stopRequestedAt.TryGetValue(arm, out long stop)
        ? (long)clock.GetElapsedTime(accepted, stop).TotalMilliseconds : -1;
    private B10AbruptFactValue[] ExposureFacts(string arm, string attempt, JsonElement checkpoint)
    {
        var facts = new List<B10AbruptFactValue> { Fact(B10AbruptFact.Arm, ArmCode(arm)),
            Fact(B10AbruptFact.AcceptedCount, checkpoint.GetProperty("accepted_attempt_ids").GetArrayLength()),
            Fact(B10AbruptFact.SelectedCount, checkpoint.GetProperty("selected_attempt_ids").GetArrayLength()),
            Fact(B10AbruptFact.TerminalBeforeAccepted, _terminalBeforeAccepted.GetValueOrDefault(arm)),
            Fact(B10AbruptFact.FramesDrainedAfterStop, _stopDrainedFrames.GetValueOrDefault(arm)),
            Fact(B10AbruptFact.AcceptedToStopMilliseconds, AcceptedToStop(arm)) };
        if (_terminals.TryGetValue(attempt, out var terminal))
        {
            facts.Add(Fact(B10AbruptFact.ErrorCode, NumberOrMinusOne(terminal.Data, "error_code")));
            facts.Add(Fact(B10AbruptFact.PersistenceStatus, NumberOrMinusOne(terminal.Data, "persistence_status")));
            facts.Add(Fact(B10AbruptFact.Delivered, terminal.Data.TryGetProperty("delivered", out var delivered) && delivered.ValueKind == JsonValueKind.True));
        }
        return [.. facts];
    }

    private string Utc() => clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
    private static InvalidDataException Failure(string name, B10AbruptFactValue[]? facts = null, Exception? inner = null, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => Attach(new InvalidDataException(name, inner), facts, file, member, line);
}
