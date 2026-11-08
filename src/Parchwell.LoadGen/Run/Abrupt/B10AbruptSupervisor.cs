using System.Globalization;
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
    B10AbruptPublisher publisher, Func<IB10AbruptProcess> start, Func<CancellationToken, Task>? validateCapturedStatistics = null)
{
    private readonly Dictionary<string, B10AbruptWire.Frame> _prepared = new(StringComparer.Ordinal);
    private readonly HashSet<string> _accepted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, B10AbruptWire.Frame> _terminals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _reports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _armFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly List<JsonElement> _signals = [];
    private readonly Dictionary<string, long> _armStarts = new(StringComparer.Ordinal);
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
        Directory.CreateDirectory(Path.Combine(options.OutputRoot, "abrupt-child"));
        _files["registration.json"] = await publisher.RegisterDiagnosticAsync(options.Identity(), cancellationToken).ConfigureAwait(false);
        using IB10AbruptProcess child = start();
        _child = child;
        _incoming = Channel.CreateBounded<B10AbruptWire.Message>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
        using var pumpCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var eventReader = new B10AbruptWire.AvailableMessageReader(child.Events, child.EventsReady, CaptureIncoming);
        _eventReader = eventReader;
        Task pump = PumpAsync(eventReader, _incoming.Writer, pumpCancellation.Token);
        try
        {
            while (await _incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_incoming.Reader.TryRead(out var message))
                {
                    await HandleAsync(message, cancellationToken).ConfigureAwait(false);
                    if (_killRequested) break;
                }
                if (_killRequested) break;
            }
            if (!_killRequested) throw Failure("b10_abrupt_unexpected_exit");
            OwnedProducerProcess.ExitObservation exit = await ObserveExitAsync(child, cancellationToken).ConfigureAwait(false);
            await journal.AppendAsync("exit", B10AbruptWire.Element(new
            {
                process = child.Identity, wait_for_exit_observed = true, observed_utc = Utc(), monotonic_ticks = clock.GetTimestamp(),
                exit_code_local_observation = exit.ExitCodeLocalObservation, runtime_version = exit.RuntimeVersion
            }), cancellationToken).ConfigureAwait(false);
            await DrainToEofAsync(_incoming.Reader, pump, cancellationToken).ConfigureAwait(false);
            if (_before is null) throw Failure("b10_abrupt_cgroup_unavailable");
            B10AbruptCgroup.Observation after = child.CaptureAfter(_before);
            _ = B10AbruptCgroup.Compare(_before, after);
            await CheckCapturedErrorsAsync(cancellationToken).ConfigureAwait(false);
            if (validateCapturedStatistics is not null) await validateCapturedStatistics(cancellationToken).ConfigureAwait(false);
            await FinalizeSelectionAsync(cancellationToken).ConfigureAwait(false);
            B10AbruptWire.Descriptor journalDescriptor = await journal.SealAsync(cancellationToken).ConfigureAwait(false);
            _files["journal.jsonl"] = await B10AbruptPublisher.ReadFileAsync(journal.Path, 16777216, cancellationToken).ConfigureAwait(false);
            if (journal.Gaps.Count != 0) throw Failure("b10_abrupt_capture_gap");
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
        }
        catch (Exception failure)
        {
            await AbortAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                using var diagnosticDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
                byte[] diagnostic = B10AbruptWire.SerializeEnvelope("failed_diagnostic", options.Identity(), new
                {
                    mechanical_status = "incomplete", failure = FailureName(failure),
                    process = child.Identity, kill_requested = _killRequested, has_exited = child.HasExited,
                    capture_gaps = journal.Gaps, arm_failures = _armFailures, fatal_capture_status = "not_implemented_pr3",
                    signal_observation = (failure as OwnedProducerProcess.SignalException)?.Observation, cleanup = _cleanup
                });
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
            await pumpCancellation.CancelAsync().ConfigureAwait(false);
            try { await pump.ConfigureAwait(false); }
            catch (Exception) when (pumpCancellation.IsCancellationRequested) { }
        }
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
                if (attempt is null || _prepared.ContainsKey(attempt)) throw Failure("b10_abrupt_capture_gap");
                string cohort = message.Data.GetProperty("cohort").GetString()!;
                if (!afterKill)
                {
                    if (cohort == "same_instance") await LoadSealAsync("timeout", cancellationToken).ConfigureAwait(false);
                    else if (cohort == "cross_restart") await LoadSealAsync("control", cancellationToken).ConfigureAwait(false);
                    else throw Failure("b10_abrupt_control_incomplete");
                }
                if (message.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null) _armStarts.TryAdd(cohort, clock.GetTimestamp());
                var prepared = await journal.AppendPreparedAndFlushAsync(message.Data, cancellationToken).ConfigureAwait(false);
                _prepared.Add(attempt, prepared);
                if (!afterKill) await AckAsync(prepared.Sequence, cancellationToken).ConfigureAwait(false);
                break;
            case "pre_acceptance_report":
                await AcceptReportAsync(message.Data, cancellationToken).ConfigureAwait(false);
                break;
            case "accepted":
                if (attempt is null || !_prepared.TryGetValue(attempt, out var original) ||
                    message.Data.GetProperty("prepared_sequence").GetInt64() != original.Sequence || !_accepted.Add(attempt)) throw Failure("b10_abrupt_capture_gap");
                string arm = original.Data.GetProperty("cohort").GetString()!;
                bool isOriginal = original.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null;
                if (isOriginal && !_reports.ContainsKey(arm)) _armFailures.TryAdd(arm, "b10_abrupt_sample_missing");
                var accepted = await journal.AppendAcceptedAndFlushAsync(message.Data, cancellationToken).ConfigureAwait(false);
                if (!afterKill) await AckAsync(accepted.Sequence, cancellationToken).ConfigureAwait(false);
                if (isOriginal && _armFailures.TryGetValue(arm, out string? failure)) throw Failure(failure);
                if (!afterKill && isOriginal)
                {
                    if (arm == "same_instance") await CheckpointControlAsync(attempt, cancellationToken).ConfigureAwait(false);
                    else await CheckpointKillAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
                break;
            case "terminal":
                if (attempt is null || !_prepared.ContainsKey(attempt) || _terminals.ContainsKey(attempt)) throw Failure("b10_abrupt_capture_gap");
                var terminal = await journal.AppendAsync("terminal", message.Data, cancellationToken).ConfigureAwait(false);
                _terminals.Add(attempt, terminal);
                if (message.Data.GetProperty("persistence_status").ValueKind != JsonValueKind.Number ||
                    !message.Data.GetProperty("persistence_status").TryGetInt32(out int status) ||
                    message.Data.GetProperty("delivered").GetBoolean() != (message.Data.GetProperty("error_code").GetInt32() == 0) ||
                    !B10AbruptProducer.IsPermittedTerminal(message.Data.GetProperty("error_code").GetInt32(), status))
                    throw Failure("b10_abrupt_guard_failed");
                break;
            case "synchronous_rejection":
                var rejection = await journal.AppendAsync(message.Kind, message.Data, cancellationToken).ConfigureAwait(false);
                if (!afterKill) await AckAsync(rejection.Sequence, cancellationToken).ConfigureAwait(false);
                throw Failure("b10_abrupt_guard_failed");
            case "capture_gap":
                journal.LatchGap(message.Data.GetProperty("reason").GetString()!);
                throw Failure("b10_abrupt_capture_gap");
            default: throw Failure("b10_abrupt_capture_gap");
        }
    }

    internal async Task AcceptReportAsync(JsonElement data, CancellationToken cancellationToken)
    {
        string arm = data.TryGetProperty("arm", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "unknown";
        if (_armFailures.ContainsKey(arm)) return;
        try
        {
            if (arm is not ("same_instance" or "cross_restart") || _reports.ContainsKey(arm)) throw Failure("b10_abrupt_report_binding_invalid");
            string attempt = data.GetProperty("attempt_id").GetString()!;
            if (!_prepared.TryGetValue(attempt, out var prepared) || prepared.Data.GetProperty("cohort").GetString() != arm || _accepted.Contains(attempt) ||
                prepared.Data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null) throw Failure("b10_abrupt_report_binding_invalid");
            B10AbruptWire.ExactMembers(data, "arm", "attempt_id", "report_length", "report_sha256", "report_identity", "report_sequence", "report_capture_utc");
            long length = data.GetProperty("report_length").GetInt64();
            if (length is < 1 or > 1048576 || data.GetProperty("report_sequence").GetInt64() < 1) throw Failure("b10_abrupt_report_binding_invalid");
            JsonElement identity = data.GetProperty("report_identity");
            B10AbruptWire.ExactMembers(identity, "scenario_id", "run_id", "role", "client_id");
            if (identity.GetProperty("scenario_id").GetString() != options.ScenarioId || identity.GetProperty("run_id").GetString() != options.RunId ||
                identity.GetProperty("role").GetString() != "LoadGenProducer" || string.IsNullOrEmpty(identity.GetProperty("client_id").GetString()) ||
                !B10AbruptWire.IsUtc(data.GetProperty("report_capture_utc").GetString() ?? "")) throw Failure("b10_abrupt_report_binding_invalid");
            string name = $"abrupt-child/{arm}-pre-acceptance-report.json";
            byte[] bytes = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, name), 1048576, cancellationToken).ConfigureAwait(false);
            if (bytes.Length != length || B10AbruptPublisher.Hash(bytes) != data.GetProperty("report_sha256").GetString()) throw Failure("b10_abrupt_report_binding_invalid");
            _reports.Add(arm, B10AbruptWire.Element(new { name, length, sha256 = B10AbruptPublisher.Hash(bytes),
                report_identity = identity, report_sequence = data.GetProperty("report_sequence"), report_capture_utc = data.GetProperty("report_capture_utc") }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { _armFailures.TryAdd(arm, "b10_abrupt_report_binding_invalid"); }
    }

    internal async Task CheckpointControlAsync(string attempt, CancellationToken cancellationToken)
    {
        var checkpoint = await StopCheckpointAsync("same_instance", attempt, cancellationToken).ConfigureAwait(false);
        if (checkpoint.Data.GetProperty("selected_attempt_ids").GetArrayLength() != 1) throw Failure("b10_abrupt_control_incomplete");
        var resumed = _child!.Resume();
        await RecordSignalAsync("same_instance", resumed, 0, cancellationToken).ConfigureAwait(false);
        await SaveCheckpointAsync("same_instance", checkpoint, cancellationToken).ConfigureAwait(false);
        string progressPath = Path.Combine(options.OutputRoot, "abrupt-child", "same_instance-resumed.json");
        using var progressDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var progressLinked = CancellationTokenSource.CreateLinkedTokenSource(progressDeadline.Token, cancellationToken);
        try
        {
            while (!File.Exists(progressPath)) await Task.Delay(TimeSpan.FromMilliseconds(10), clock, progressLinked.Token).ConfigureAwait(false);
            JsonElement progress = B10AbruptWire.Parse(await B10AbruptPublisher.ReadFileAsync(progressPath, 65536, progressLinked.Token).ConfigureAwait(false));
            B10AbruptWire.ExactMembers(progress, "arm", "identity", "checkpoint_sequence", "checkpoint_sha256");
            if (progress.GetProperty("arm").GetString() != "same_instance" ||
                progress.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != options.Identity() ||
                progress.GetProperty("checkpoint_sequence").GetInt64() != checkpoint.Sequence ||
                progress.GetProperty("checkpoint_sha256").GetString() != B10AbruptPublisher.Hash(B10AbruptWire.WriteFrame(checkpoint)))
                throw Failure("b10_abrupt_resume_unconfirmed");
        }
        catch (OperationCanceledException) { throw Failure("b10_abrupt_resume_unconfirmed"); }
    }

    internal async Task CheckpointKillAsync(string attempt, CancellationToken cancellationToken)
    {
        _before = _child!.CaptureBefore();
        var checkpoint = await StopCheckpointAsync("cross_restart", attempt, cancellationToken).ConfigureAwait(false);
        _killCheckpoint = checkpoint;
        await SaveCheckpointAsync("cross_restart", checkpoint, cancellationToken).ConfigureAwait(false);
        if (checkpoint.Data.GetProperty("accepted_attempt_ids").GetArrayLength() == 0) throw Failure("kill_too_early");
        if (checkpoint.Data.GetProperty("selected_attempt_ids").GetArrayLength() == 0) throw Failure("kill_too_late:no_unconfirmed_attempt");
        var intent = await RecordSignalAsync("cross_restart", new(9, null, null), 0, cancellationToken).ConfigureAwait(false);
        _killRequested = true;
        await RequestKillAsync(_child, intent.Sequence, cancellationToken).ConfigureAwait(false);
    }

    private async Task<B10AbruptWire.Frame> StopCheckpointAsync(string arm, string attempt, CancellationToken cancellationToken)
    {
        long begin = _armStarts.GetValueOrDefault(arm, clock.GetTimestamp());
        TimeSpan budget = TimeSpan.FromSeconds(30);
        TimeSpan remaining = budget - clock.GetElapsedTime(begin);
        if (remaining < TimeSpan.Zero) throw Failure("b10_abrupt_stop_unconfirmed");
        // One tick beyond the inclusive deadline lets an already completed operation at the boundary be observed.
        using var deadline = new CancellationTokenSource(remaining + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        bool stoppedObserved = false;
        try
        {
            if (_child is null || _child.HasExited) throw Failure("b10_abrupt_unexpected_exit");
            await RecordSignalAsync(arm, _child.Stop(), 0, linked.Token).ConfigureAwait(false);
            remaining = budget - clock.GetElapsedTime(begin);
            if (remaining < TimeSpan.Zero || !_child.ConfirmStopped(remaining, clock) || clock.GetElapsedTime(begin) > budget)
                throw Failure("b10_abrupt_stop_unconfirmed");
            stoppedObserved = true;
            if (_eventReader is not null) await DrainAvailableAsync(_eventReader, linked.Token, requireCompleteFrame: true).ConfigureAwait(false);
            if (_incoming is not null)
                while (_incoming.Reader.TryRead(out var message))
                {
                    await HandleAsync(message, linked.Token, afterKill: true).ConfigureAwait(false);
                    if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_deadline_exhausted");
                }
            if (!_reports.TryGetValue(arm, out var report)) throw Failure(_armFailures.GetValueOrDefault(arm, "b10_abrupt_sample_missing"));
            DateTimeOffset utc = clock.GetUtcNow();
            var checkpoint = await journal.CheckpointAsync(B10AbruptWire.Element(new
            {
                arm, accepted_attempt_ids = _accepted.Contains(attempt) ? new[] { attempt } : [],
                selected_attempt_ids = _accepted.Contains(attempt) && !_terminals.ContainsKey(attempt) ? new[] { attempt } : [],
                last_complete_sequence = journal.Frames.Count == 0 ? 0 : journal.Frames[^1].Sequence, pre_acceptance_report = report,
                process = _child.Identity, checkpoint_utc = B10AbruptPublisher.Utc(utc), checkpoint_monotonic_ticks = clock.GetTimestamp(),
                release_utc = B10AbruptPublisher.Utc(utc.AddSeconds(120))
            }), linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_deadline_exhausted");
            return checkpoint;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw Failure(stoppedObserved ? "b10_abrupt_deadline_exhausted" : "b10_abrupt_stop_unconfirmed"); }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        { throw Failure(stoppedObserved ? "b10_abrupt_deadline_exhausted" : "b10_abrupt_stop_unconfirmed"); }
    }

    internal async Task RequestKillAsync(IB10AbruptProcess child, long intentSequence, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        TimeSpan budget = TimeSpan.FromSeconds(10);
        OwnedProducerProcess.SignalObservation? observation = null;
        using var deadline = new CancellationTokenSource(budget + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            observation = await Task.Run(child.Kill, cancellationToken).WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation);
            await RecordSignalAsync("cross_restart", observation, intentSequence, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (observation is not null) throw new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation);
            throw Failure("b10_abrupt_deadline_exhausted");
        }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        {
            if (observation is not null) throw new OwnedProducerProcess.SignalException("b10_abrupt_deadline_exhausted", observation);
            throw Failure("b10_abrupt_deadline_exhausted");
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
            if (!exit.WaitForExitObserved || clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_exit_unobserved");
            return exit;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw Failure("b10_abrupt_exit_unobserved"); }
    }

    internal async Task DrainToEofAsync(ChannelReader<B10AbruptWire.Message> reader, Task pump, CancellationToken cancellationToken)
    {
        long begin = clock.GetTimestamp();
        TimeSpan budget = TimeSpan.FromSeconds(10);
        using var deadline = new CancellationTokenSource(budget + TimeSpan.FromTicks(1), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await foreach (var message in reader.ReadAllAsync(linked.Token).ConfigureAwait(false))
                await HandleAsync(message, linked.Token, afterKill: true).ConfigureAwait(false);
            await pump.WaitAsync(linked.Token).ConfigureAwait(false);
            if (clock.GetElapsedTime(begin) > budget) throw Failure("b10_abrupt_eof_incomplete");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw Failure("b10_abrupt_eof_incomplete"); }
        catch (InvalidDataException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested &&
            exception.Message == "b10_abrupt_capture_gap:writer_failed")
        { throw Failure("b10_abrupt_eof_incomplete"); }
    }

    internal async Task FinalizeSelectionAsync(CancellationToken cancellationToken)
    {
        if (_armFailures.Count != 0) throw Failure(_armFailures.Values.First());
        if (_killCheckpoint is null) throw Failure("kill_too_early");
        string[] candidates = _killCheckpoint.Data.GetProperty("selected_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var entries = new List<JsonElement>();
        foreach (string attempt in candidates.Where(attempt => !_terminals.ContainsKey(attempt)))
        {
            JsonElement prepared = _prepared[attempt].Data;
            byte[] payload = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt-child", "cross_restart-payload.bin"), 900000, cancellationToken).ConfigureAwait(false);
            if (payload.Length != prepared.GetProperty("payload_length").GetInt64() || B10AbruptPublisher.Hash(payload) != prepared.GetProperty("payload_sha256").GetString()) throw Failure("b10_abrupt_capture_gap");
            entries.Add(B10AbruptWire.Element(new { attempt_id = attempt, key = prepared.GetProperty("key"), sequence = prepared.GetProperty("sequence"),
                payload_length = payload.Length, payload_sha256 = B10AbruptPublisher.Hash(payload), payload_base64 = Convert.ToBase64String(payload) }));
        }
        _files["kill-selection.json"] = B10AbruptWire.SerializeEnvelope("selection", options.Identity(), new
        {
            checkpoint = B10AbruptWire.Parse(B10AbruptWire.WriteFrame(_killCheckpoint)),
            checkpoint_descriptor = B10AbruptPublisher.Describe("cross_restart-checkpoint.json", B10AbruptWire.WriteFrame(_killCheckpoint)), candidate_attempt_ids = candidates,
            terminal_exclusions = candidates.Where(_terminals.ContainsKey).Select(attempt => new { attempt_id = attempt, terminal_sequence = _terminals[attempt].Sequence }).ToArray(), entries
        });
        if (entries.Count == 0) throw Failure("kill_too_late:no_unconfirmed_attempt");
    }

    private async Task LoadSealAsync(string cohort, CancellationToken cancellationToken)
    {
        string name = cohort + "-seal.json";
        if (_files.ContainsKey(name)) return;
        byte[] bytes = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt-child", name), 65536, cancellationToken).ConfigureAwait(false);
        JsonElement seal = B10AbruptWire.ReadEnvelope(bytes, "cohort_seal");
        JsonElement data = seal.GetProperty("data");
        if (seal.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != options.Identity() ||
            data.GetProperty("cohort").GetString() != (cohort == "control" ? "same_instance" : cohort) || !data.GetProperty("drain_complete").GetBoolean() ||
            !data.GetProperty("guard_complete").GetBoolean() || data.GetProperty("fatal_capture_status").GetString() != "not_implemented_pr3") throw Failure("b10_abrupt_control_incomplete");
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
            if (rows.Length != data.GetProperty("count").GetInt64()) throw Failure("b10_abrupt_control_incomplete");
        }
        else
        {
            JsonElement inventory = B10AbruptWire.ReadEnvelope(artifacts["manifest.json"], "cohort_inventory").GetProperty("data");
            var entries = inventory.GetProperty("inventory").EnumerateArray().ToArray();
            string[] lines = Encoding.UTF8.GetString(artifacts["ledger.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (entries.Length != 2 || lines.Length != 2 || inventory.GetProperty("inventory_count").GetInt32() != 2 || data.GetProperty("count").GetInt32() != 2 ||
                !inventory.GetProperty("bookkeeping_complete").GetBoolean()) throw Failure("b10_abrupt_control_incomplete");
            var ids = entries.Select(entry => entry.GetProperty("attempt_id").GetString()!).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != 2 || ids.Any(id => !_accepted.Contains(id) || !_terminals.ContainsKey(id))) throw Failure("b10_abrupt_control_incomplete");
            foreach (string line in lines)
            {
                JsonElement row = B10AbruptWire.Parse(Encoding.UTF8.GetBytes(line));
                string attempt = row.GetProperty("attempt_id").GetString()!;
                if (!ids.Remove(attempt) || row.GetRawText() != _terminals[attempt].Data.GetRawText()) throw Failure("b10_abrupt_control_incomplete");
            }
        }
        _files.Add(name, bytes);
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
        }
        catch (Exception exception)
        {
            _cleanup = _cleanup with { Failure = FailureName(exception) };
            return;
        }
        _cleanup = _cleanup with { SignalObservation = observation };
        try { await RecordSignalAsync("cleanup", observation, 0, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception)
        {
            // The journal owns capture gaps; a failed native call is not a failed writer.
            _cleanup = _cleanup with { CaptureFailure = FailureName(exception) };
        }
        if (_cleanup.Failure is not null) return;
        try
        {
            if (!_child.ObserveExit().WaitForExitObserved) _cleanup = _cleanup with { Failure = "b10_abrupt_exit_unobserved" };
        }
        catch (Exception exception) { _cleanup = _cleanup with { Failure = FailureName(exception) }; }
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
            "b10_abrupt_report_binding_invalid" or "b10_abrupt_guard_failed" or "b10_abrupt_control_incomplete" or
            "kill_too_early" or "kill_too_late" or "b10_abrupt_cgroup_unavailable" or "b10_abrupt_oom_conflict" or
            "b10_abrupt_publication_failed" or "b10_abrupt_publication_ambiguous" or "b10_abrupt_successor_binding_invalid" or
            "b10_abrupt_successor_late" or "b10_abrupt_deadline_exhausted" ? name : "b10_abrupt_capture_gap";
    }

    private string Utc() => clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
    private static InvalidDataException Failure(string name) => new(name);
}
