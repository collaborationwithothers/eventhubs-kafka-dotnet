using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.IdleObservation;

/// <summary>Judges idle-observation captures; owns every predicate of the verifier contract's idle-observation section.</summary>
internal static class IdleObservationJudgement
{
    internal const long ReferenceSlackMs = 20000;
    internal const long IdleTimedQuietMs = 225000;
    internal const long ClientCloseLimitMs = 225000;
    internal const long ClientCloseQuietMs = 163000;
    internal static readonly string[] Items = ["A02", "A03", "A04"];
    private static readonly string[] UpSources = ["learned", "configured", "logical"];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static string[] Configurations(string item) => item == "A03" ? ["C0", "C1", "C2"] : ["C0", "C1"];

    internal static IdleReport ParseReport(SingleActionStatistic statistic, int maxBytes, int maxBrokers)
    {
        try
        {
            byte[] raw = Convert.FromBase64String(statistic.RawBase64);
            if (raw.Length > maxBytes) throw new FormatException("idle_statistics_shape");
            using JsonDocument document = JsonDocument.Parse(StrictUtf8.GetString(raw), new JsonDocumentOptions { MaxDepth = 64 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("brokers", out JsonElement brokers) || brokers.ValueKind != JsonValueKind.Object)
                throw new FormatException("idle_statistics_shape");
            var result = new Dictionary<string, IdleBroker>(StringComparer.Ordinal);
            foreach (JsonProperty broker in brokers.EnumerateObject())
            {
                if (result.Count == maxBrokers || broker.Value.ValueKind != JsonValueKind.Object || result.ContainsKey(broker.Name)) throw new FormatException("idle_statistics_shape");
                JsonElement value = broker.Value;
                if (!value.TryGetProperty("req", out JsonElement requests) || requests.ValueKind != JsonValueKind.Object) throw new FormatException("idle_statistics_shape");
                long txIdle = Integer(value, "txidle"), rxIdle = Integer(value, "rxidle");
                if (txIdle < -1 || rxIdle < -1) throw new FormatException("idle_statistics_shape");
                result.Add(broker.Name, new(Integer(value, "nodeid"), Text(value, "source"), Text(value, "state"), txIdle, rxIdle,
                    Integer(value, "connects"), Integer(value, "disconnects"), Integer(value, "req_timeouts"), Integer(requests, "Metadata")));
            }
            return new(statistic.Sequence, statistic.ObservedElapsedMs, result);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or ArgumentException) { throw new FormatException("idle_statistics_shape", exception); }
    }

    private static long Integer(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : throw new FormatException("idle_statistics_shape");
    private static string Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new FormatException("idle_statistics_shape");

    internal static long Delta(IdleReport from, IdleReport to, Func<IdleBroker, long> field)
    {
        long total = 0;
        foreach (var (key, broker) in to.Brokers)
            total = checked(total + field(broker) - (from.Brokers.TryGetValue(key, out IdleBroker? earlier) ? field(earlier) : 0));
        return total;
    }

    internal static long QuietMs(IdleBroker? before)
    {
        if (before is null || (before.TxIdleUs == -1 && before.RxIdleUs == -1)) return 0;
        long micros = before.TxIdleUs == -1 ? before.RxIdleUs : before.RxIdleUs == -1 ? before.TxIdleUs : Math.Min(before.TxIdleUs, before.RxIdleUs);
        return micros / 1000;
    }

    internal static bool Monotonic(IReadOnlyList<IdleReport> reports)
    {
        for (int index = 1; index < reports.Count; index++)
            foreach (var (key, broker) in reports[index].Brokers)
                if (reports[index - 1].Brokers.TryGetValue(key, out IdleBroker? earlier) && (broker.Connects < earlier.Connects ||
                    broker.Disconnects < earlier.Disconnects || broker.ReqTimeouts < earlier.ReqTimeouts || broker.Metadata < earlier.Metadata))
                    return false;
        return true;
    }

    internal static IdleWindows? Windows(IReadOnlyList<IdleReport> reports, long start, long end, long terminal)
    {
        int first = -1, last = -1, resume = -1;
        for (int index = 0; index < reports.Count; index++)
        {
            long at = reports[index].ObservedElapsedMs;
            if (first < 0 && at >= start) first = index;
            if (at <= end) last = index;
            if (resume < 0 && at >= terminal) resume = index;
        }
        if (first < 0 || reports[first].ObservedElapsedMs - start > ReferenceSlackMs || last <= first ||
            end - reports[last].ObservedElapsedMs > ReferenceSlackMs || resume < last) return null;
        IdleReport r0 = reports[first], r1 = reports[last], r2 = reports[resume];
        long idleTimed = 0, idleOther = 0, resumeTimed = 0, resumeOther = 0;
        long? close = null, closeQuiet = null;
        var timedOut = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        bool reconnected = false;
        for (int index = first + 1; index <= resume; index++)
        {
            bool idle = index <= last;
            long? quietest = null;
            foreach (var (key, broker) in reports[index].Brokers)
            {
                IdleBroker? earlier = reports[index - 1].Brokers.GetValueOrDefault(key);
                long rose = broker.Disconnects - (earlier?.Disconnects ?? 0);
                if (!idle)
                {
                    // Resume timeout sequence: on one broker key, a request timeout, then a disconnect, then a connection attempt.
                    if (broker.ReqTimeouts > (earlier?.ReqTimeouts ?? 0)) timedOut.Add(key);
                    if (rose > 0 && timedOut.Contains(key)) dropped.Add(key);
                    if (broker.Connects > (earlier?.Connects ?? 0) && dropped.Contains(key)) reconnected = true;
                }
                if (rose <= 0) continue;
                long quiet = QuietMs(earlier);
                quietest = Math.Min(quietest ?? quiet, quiet);
                bool timed = quiet >= IdleTimedQuietMs;
                if (idle && timed) idleTimed += rose;
                else if (idle) idleOther += rose;
                else if (timed) resumeTimed += rose;
                else resumeOther += rose;
            }
            if (idle && close is null && quietest is not null) (close, closeQuiet) = (reports[index].ObservedElapsedMs - start, quietest);
        }
        return new(idleTimed, idleOther, Delta(r0, r1, b => b.Connects), Delta(r0, r1, b => b.Metadata), Delta(r0, r1, b => b.ReqTimeouts), close, closeQuiet,
            resumeTimed, resumeOther, Delta(r1, r2, b => b.Connects), Delta(r1, r2, b => b.ReqTimeouts), reconnected);
    }

    internal static IdleRepeatResult JudgeRepeat(IdleRepeat repeat, int ordinal, string pod, IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<string> omissions, int idleSeconds, int maxReportBytes, int maxBrokers)
    {
        var invalid = new List<string>();
        var incomplete = new List<string>();
        if (repeat.Repeat != ordinal || repeat.ClientId != $"{pod}-r{ordinal}") invalid.Add("idle_identity");
        if (repeat.Settings.Count != settings.Count || repeat.Settings.Any(pair => !settings.TryGetValue(pair.Key, out string? value) || value != pair.Value) ||
            !repeat.OmittedKeys.SequenceEqual(omissions, StringComparer.Ordinal)) invalid.Add("idle_settings");
        if (repeat.Diagnostics.Length != 0) incomplete.Add("idle_diagnostics");
        var reports = new List<IdleReport>();
        try { foreach (SingleActionStatistic statistic in repeat.Statistics) reports.Add(ParseReport(statistic, maxReportBytes, maxBrokers)); }
        catch (FormatException) { invalid.Add("idle_statistics_shape"); reports.Clear(); }
        if (!repeat.Statistics.Select(s => s.Sequence).SequenceEqual(Enumerable.Range(1, repeat.Statistics.Length)) ||
            repeat.Statistics.Zip(repeat.Statistics.Skip(1)).Any(pair => pair.Second.ObservedElapsedMs < pair.First.ObservedElapsedMs)) invalid.Add("idle_statistics_order");
        else if (!Monotonic(reports)) invalid.Add("idle_counter_decreased");
        string? connection = null;
        IdleWindows? windows = null;
        if (repeat.First is not { Terminal: true }) incomplete.Add("idle_first_not_terminal");
        else if (repeat.First.Result != "success") connection = "first_failed";
        else if (repeat.IdleStartedElapsedMs is not long start || repeat.IdleEndedElapsedMs is not long end || end - start < idleSeconds * 1000L ||
            repeat.Second is null || repeat.Second.Terminal && repeat.Second.TerminalElapsedMs is null) invalid.Add("idle_window");
        else if (!repeat.Second.Terminal) incomplete.Add("idle_second_not_terminal");
        else if (invalid.Count == 0)
        {
            windows = Windows(reports, start, end, repeat.Second.TerminalElapsedMs!.Value);
            if (windows is null) incomplete.Add("idle_reference_missing");
            else connection = windows.IdleTimedDisconnects > 0 ? "closed_while_idle" : windows.ResumeIdleTimedDisconnects > 0 ? "stale_on_resume"
                : windows.ResumeTimeoutReconnect ? "timed_out_on_resume" : "kept";
        }
        string status = invalid.Count > 0 ? "invalid" : incomplete.Count > 0 ? "incomplete" : "complete_valid";
        int up = reports.Count == 0 ? 0 : reports.Max(report => report.Brokers.Values.Count(b => UpSources.Contains(b.Source) && b.State == "UP"));
        return new(ordinal, repeat.ClientId, status, [.. invalid, .. incomplete], status == "complete_valid" ? connection : null,
            Outcome(repeat.First), Outcome(repeat.Second), windows, up, [.. reports],
            [.. repeat.ClientErrors.Select(e => new IdleClientErrorRow(e.Code, e.IsFatal, e.Origin, e.ObservedElapsedMs))], repeat.ClientErrorsDropped);
    }

    private static IdleOutcome? Outcome(IdleProduce? produce) => produce is { Terminal: true, Result: not null, TerminalElapsedMs: long terminal }
        ? new(produce.Result, produce.Error?.Code, produce.Error?.IsFatal, produce.Error?.Origin, terminal - produce.StartedElapsedMs) : null;

    internal static (string[] Reasons, bool Invalid) CaptureProblems(SingleActionRegistration? registration, IdleCapture? capture,
        SingleActionCompletion? completion, string run, string item, string configuration, string pod, string uid, string commit, string runKind)
    {
        if (registration is null || capture is null || completion is null) return (["idle_capture_missing"], false);
        string job = $"{run}-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-idle";
        var identity = new SingleActionIdentity(item, configuration, run, pod);
        bool Same(int schema, string mode, SingleActionIdentity actual, string jobName, string podName, string podUid, string source) =>
            schema == 1 && mode == "idle-observation" && actual == identity && jobName == job && podName == pod && podUid == uid && source == commit;
        if (!Same(registration.SchemaVersion, registration.Mode, registration.Identity, registration.JobName, registration.PodName, registration.PodUid, registration.SourceCommit) ||
            !Same(completion.SchemaVersion, completion.Mode, completion.Identity, completion.JobName, completion.PodName, completion.PodUid, completion.SourceCommit) ||
            !Same(capture.SchemaVersion, capture.Mode, capture.Identity, capture.JobName, capture.PodName, capture.PodUid, capture.SourceCommit) ||
            !capture.Synthetic || capture.RunKind != runKind || capture.Topic != "document-events" || capture.Partition != 0 || capture.PayloadBytes != 1000 ||
            capture.IdleSeconds != 420 || capture.StatisticsIntervalMs != 15000 || capture.MaxStatisticsReports != 80 ||
            !capture.Repeats.Select(r => r.Repeat).SequenceEqual([1, 2, 3, 4, 5]))
            return (["idle_capture_identity"], true);
        return completion.RepeatsAttempted == 5 && completion.HandlerExitCode == 0 && completion.CleanupCompleted ? ([], false) : (["idle_completion"], false);
    }

    internal static IdleConfigurationResult Configuration(string id, string[] captureReasons, bool captureInvalid, IdleRepeatResult[] repeats, string? sha256)
    {
        if (captureReasons.Length > 0)
            repeats = [.. Enumerable.Range(1, 5).Select(ordinal => new IdleRepeatResult(ordinal, null, captureInvalid ? "invalid" : "incomplete",
                captureReasons, null, null, null, null, 0, [], [], 0))];
        string status = repeats.Any(r => r.Status == "invalid") ? "invalid" : repeats.Length == 5 && repeats.All(r => r.Status == "complete_valid") ? "complete_valid" : "incomplete";
        string? summary = status != "complete_valid" ? null
            : repeats.Any(r => r.ConnectionClass == "first_failed") ? "first_failed"
            : repeats.All(r => r.ConnectionClass == "kept") ? "kept"
            : repeats.All(r => r.ConnectionClass is "closed_while_idle" or "stale_on_resume" or "timed_out_on_resume") ? "not_kept" : "unequal";
        return new(id, status, summary, sha256, repeats);
    }

    internal static IdleItemAssessment Assess(string item, string runKind, IReadOnlyDictionary<string, IdleConfigurationResult> results)
    {
        string[] names = item switch { "A02" => ["H_keepalive", "H_resume"], "A03" => ["H_metadata_age", "H_metadata_cadence", "H_refresh_interval"], _ => ["H_client_close", "H_default_close"] };
        IdleItemAssessment Result(string? verdict, string reason, string? decided = null) =>
            new(verdict, reason, names.ToDictionary(n => n, n => decided is null ? "undecided" : n == names[0] ? decided : "recorded_only", StringComparer.Ordinal));
        if (runKind != "batch") return Result(null, "smoke_only");
        if (Configurations(item).Any(c => !results.TryGetValue(c, out IdleConfigurationResult? r) || r.Status != "complete_valid")) return Result(null, "incomplete_configuration");
        if (results.Values.Any(r => r.Summary == "first_failed")) return Result("untestable", "first_produce_failed");
        if (results.Values.Any(r => r.Summary == "unequal")) return Result(null, "unequal_classes");
        if (item == "A04")
            return Result("undocumented", "not_in_librdkafka_table", results["C1"].Repeats.All(r => r.Windows!.FirstCloseQuietMs >= ClientCloseQuietMs &&
                r.Windows.FirstCloseAfterIdleMs < ClientCloseLimitMs) ? "holds" : "contradicted");
        string s0 = results["C0"].Summary!, s1 = results["C1"].Summary!;
        bool keepalive = item == "A02";
        if (s0 == "kept" && s1 == "not_kept") return Result(null, "inverse_effect", "undecided");
        if (s1 == "not_kept") return Result("contradicted", keepalive ? "closed_with_keepalive" : "closed_with_microsoft_value", "contradicted");
        if (s0 == "not_kept") return Result("confirmed", keepalive ? "keepalive_kept_connection" : "microsoft_value_kept_connection", "holds");
        return Result("untestable", "no_idle_close_observed", "undecided");
    }

    internal static string ItemStatus(IEnumerable<IdleConfigurationResult> results) =>
        results.Any(r => r.Status == "invalid") ? "invalid" : results.All(r => r.Status == "complete_valid") ? "complete_valid" : "incomplete";

    internal static int ExitCode(IEnumerable<IdleCandidateReference> candidates) =>
        candidates.Any(c => c.Status != "complete_valid" || c.Reason == "incomplete_configuration") ? 1 : 0;
}
