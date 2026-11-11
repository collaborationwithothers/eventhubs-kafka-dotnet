using System.Globalization;
using System.Text.Json;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptParent(B10AbruptOptions options, TimeProvider clock, B10AbruptPublisher publisher,
    ChildCommand? command = null, TimeSpan? waitBound = null,
    Func<B10AbruptCgroup.Observation>? captureBefore = null,
    Func<B10AbruptCgroup.Observation, B10AbruptCgroup.Observation>? captureAfter = null,
    Func<CancellationToken, Task>? validateCapturedStatistics = null, long? parentStartedTimestamp = null)
{
    internal sealed record Classification(string Termination, string? Outcome, string? Detail, JsonElement? SignalObservation, bool SuccessorPermitted);
    internal static Classification Classify(B10AbruptJournal.Snapshot journal, bool exitObserved, int? exitCode,
        B10AbruptProcess.ProcessIdentity? child, B10AbruptCgroup.Observation? before, B10AbruptCgroup.Observation? after)
    {
        if (!exitObserved) return new("unknown", "b10_abrupt_exit_unobserved", null, null, false);
        var intent = journal.Frames.LastOrDefault(frame => frame.Kind == "kill_intent");
        var noKill = journal.Frames.LastOrDefault(frame => frame.Kind == "no_kill");
        if (intent is null && noKill is not null) return new("no_kill", noKill.Data.GetProperty("reason").GetString(), null, null, false);
        var signal = journal.Frames.LastOrDefault(frame => frame.Kind == "signal_failed" && intent is not null && frame.Sequence > intent.Sequence);
        if (signal is not null) return new("unexpected_exit", "b10_abrupt_signal_failed", null, signal.Data, false);
        var started = journal.Frames.FirstOrDefault(frame => frame.Kind == "child_started");
        bool matches = started is not null && child is not null && started.Data.GetProperty("pid").GetInt32() == child.Pid && started.Data.GetProperty("start_time_ticks").GetInt64() == child.StartIdentity && started.Data.GetProperty("boot_id").GetString() == child.BootId;
        bool cgroup = before is not null && after is not null && before == after;
        if (exitCode == 137 && intent is not null && journal.Frames.LastOrDefault(frame => B10AbruptWire.IsProducingKind(frame.Kind)) == intent && matches && cgroup)
        {
            bool nonempty = journal.KillUnconfirmedAttemptIds.Count != 0;
            return new("intended_self_kill", nonempty ? null : "kill_too_late", nonempty ? null : "no_unconfirmed_attempt", null, nonempty && journal.Gaps.Count == 0 && journal.Failures.Count == 0);
        }
        return new("unexpected_exit", "b10_abrupt_unexpected_exit", null, null, false);
    }
    private readonly B10AbruptTimeline timeline = new(clock, "parent", Console.Error);
    internal async Task<JsonElement> RunAsync(CancellationToken cancellationToken = default)
    {
        try { return await RunCoreAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception)
        {
            timeline.Hold(B10AbruptStage.FailureObserved);
            try
            {
                byte[] bytes = File.Exists(Path.Combine(options.OutputRoot,"abrupt","journal.jsonl"))
                    ? await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot,"abrupt","journal.jsonl"),16777216,CancellationToken.None).ConfigureAwait(false) : [];
                await B10AbruptDiagnostics.WriteFailureAsync(options,clock,exception,B10AbruptJournal.Read(bytes,options.Identity()),timeline).ConfigureAwait(false);
            }
            catch (Exception) { }
            throw;
        }
    }
    private async Task<JsonElement> RunCoreAsync(CancellationToken cancellationToken)
    {
        long begin = parentStartedTimestamp ?? clock.GetTimestamp();
        await publisher.RegisterDiagnosticAsync(options.Identity(), cancellationToken).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.ParentRegistered);
        B10AbruptCgroup.Observation? before = null, after = null;
        List<string> extraGaps = [];
        try { before = (captureBefore ?? B10AbruptCgroup.CaptureBefore)(); }
        catch (Exception exception) { extraGaps.Add(FailureName(exception)); }
        var child = B10AbruptProcess.Start(command ?? ChildCommand.ForRunningApphost(), options);
        B10AbruptProcess.ProcessIdentity? observed = null;
        try { observed = B10AbruptProcess.ReadIdentity(child.Id); }
        catch (Exception exception) when (exception is IOException or InvalidDataException) { extraGaps.Add("b10_abrupt_child_identity_mismatch"); }
        timeline.Mark(B10AbruptStage.ChildStarted);
        TimeSpan remaining = waitBound ?? TimeSpan.FromSeconds(720) - clock.GetElapsedTime(begin);
        int milliseconds = (int)Math.Clamp(remaining.TotalMilliseconds, 0, int.MaxValue);
        bool exited = await Task.Run(() => child.WaitForExit(milliseconds), cancellationToken).ConfigureAwait(false);
        if (exited)
        {
            timeline.Mark(B10AbruptStage.ExitObserved);
            if (before is not null)
            {
                try { after = (captureAfter ?? B10AbruptCgroup.CaptureAfter)(before); _ = B10AbruptCgroup.Compare(before, after); }
                catch (Exception exception) { extraGaps.Add(FailureName(exception)); }
            }
        }
        byte[] bytes;
        try { bytes = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt", "journal.jsonl"), 16777216, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { bytes = []; extraGaps.Add("writer_failed"); }
        var snapshot = B10AbruptJournal.Read(bytes, options.Identity());
        if (exited && validateCapturedStatistics is not null)
        {
            try { await validateCapturedStatistics(cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) { extraGaps.Add(FailureName(exception)); }
        }
        timeline.Mark(B10AbruptStage.JournalRead);
        Classification result = Classify(snapshot, exited, exited ? child.ExitCode : null, observed, before, after);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["journal.jsonl"] = bytes };
        List<B10AbruptWire.Descriptor> reports = [];
        foreach (var frame in snapshot.Frames.Where(frame => frame.Kind is "pre_acceptance_report" or "post_drain_report"))
        {
            var data = frame.Data;
            string name = data.GetProperty("name").GetString()!;
            byte[] report;
            try { report = await B10AbruptPublisher.ReadFileAsync(Path.Combine(options.OutputRoot, "abrupt", name), 1048576, cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_publication_failed", exception)); }
            var descriptor = B10AbruptPublisher.Describe(name, report);
            if (descriptor.Length != data.GetProperty("length").GetInt64() || descriptor.Sha256 != data.GetProperty("sha256").GetString() || !files.TryAdd(name, report)) throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_publication_failed"));
            reports.Add(descriptor);
        }
        string[] gaps = snapshot.Gaps.Concat(extraGaps).Concat(snapshot.Failures.Values).Distinct(StringComparer.Ordinal).ToArray();
        files["abrupt-manifest.json"] = B10AbruptWire.SerializeEnvelope("abrupt_manifest", options.Identity(), new { journal = B10AbruptPublisher.Describe("journal.jsonl", bytes), journal_records = snapshot.Frames.Count, report_files = reports, gaps });
        var started = snapshot.Frames.FirstOrDefault(frame => frame.Kind == "child_started");
        files["termination.json"] = B10AbruptWire.SerializeEnvelope("termination", options.Identity(), new
        {
            termination = result.Termination, outcome = result.Outcome, detail = result.Detail, signal_observation = result.SignalObservation,
            successor_permitted = result.SuccessorPermitted && gaps.Length == 0,
            child = observed is null ? null : new { pid = observed.Pid, start_time_ticks = started?.Data.GetProperty("start_time_ticks").GetInt64(), boot_id = observed.BootId, parent_observed_start_time_ticks = observed.StartIdentity },
            exit_observed = exited, exit_code_local_observation = exited ? child.ExitCode : (int?)null, runtime_version = Environment.Version.ToString(), wait_bound_milliseconds = milliseconds,
            cgroup_before = before, cgroup_after = after, control_unconfirmed_attempt_ids = snapshot.ControlUnconfirmedAttemptIds, kill_unconfirmed_attempt_ids = snapshot.KillUnconfirmedAttemptIds, capture_gaps = gaps,
            native_error_count = snapshot.Frames.Count(frame => frame.Kind == "native_error"), native_fatal_count = snapshot.Frames.Count(frame => frame.Kind == "native_error" && frame.Data.GetProperty("is_fatal").GetBoolean()),
            native_tail = result.Termination == "intended_self_kill" ? "unknown_due_to_intended_kill" : null,
            manifest = B10AbruptPublisher.Describe("abrupt-manifest.json", files["abrupt-manifest.json"])
        });
        if (exited) child.Dispose();
        await publisher.PublishFirstAsync(options.Identity(), files, cancellationToken, timeline).ConfigureAwait(false);
        timeline.Mark(B10AbruptStage.PublicationComplete);
        return B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
    }
    internal static string FailureName(Exception exception)
    {
        if (exception is OperationCanceledException) return "b10_abrupt_deadline_exhausted";
        string value = exception.Message.Split(':')[0];
        return value is "b10_abrupt_configuration_invalid" or "b10_abrupt_platform_unsupported" or
            "b10_abrupt_signal_preflight_failed" or "b10_abrupt_child_start_failed" or "b10_abrupt_child_identity_mismatch" or
            "b10_abrupt_signal_failed" or "b10_abrupt_exit_unobserved" or "b10_abrupt_unexpected_exit" or
            "b10_abrupt_capture_gap" or "b10_abrupt_sample_missing" or "b10_abrupt_report_binding_invalid" or
            "b10_abrupt_guard_failed" or "b10_abrupt_control_incomplete" or "b10_abrupt_exposure_inconclusive" or
            "kill_too_early" or "kill_too_late" or "b10_abrupt_cgroup_unavailable" or "b10_abrupt_oom_conflict" or
            "b10_abrupt_publication_failed" or "b10_abrupt_publication_ambiguous" or "b10_abrupt_successor_binding_invalid" or
            "b10_abrupt_successor_late" or "b10_abrupt_deadline_exhausted" or "b10_abrupt_cut_rejected" or
            "b10_abrupt_diagnostic_not_scientific" ? value : "b10_abrupt_capture_gap";

    }
}
