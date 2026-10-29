using System.Text;
using Confluent.Kafka;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed class ProbeV2Capture(ProbeV2Limits limits, TimeProvider clock, long started, int windowMs, ProbeV2DiagnosticBudget? sharedBudget = null, string logContext = "")
{
    private readonly object gate = new();
    private readonly ProbeV2DiagnosticBudget diagnosticBudget = sharedBudget ?? new(limits.MaxDiagnostics);
    private readonly List<ProbeV2Error> errors = [];
    private readonly List<ProbeV2Statistics> statistics = [];
    private readonly List<string> diagnostics = [];
    private readonly List<string> logs = [];
    private long sequence;
    private int logBytes;
    private bool logCapped;
    private bool limit;
    private bool terminal;
    private long terminalElapsed;
    private long? terminalUnix;
    private string? result;
    private ProbeV2Error? terminalError;
    private ProbeV2Delivery? delivery;
    private ProbeV2Record? record;
    private int recordsSeen;
    private int eofCount;
    internal long ElapsedMs => (long)clock.GetElapsedTime(started).TotalMilliseconds;
    internal bool Terminal { get { lock (gate) return terminal; } }
    internal bool Limit { get { lock (gate) return limit; } }
    internal bool HasErrors { get { lock (gate) return errors.Count != 0; } }

    internal void Error(Error error)
    {
        lock (gate)
        {
            if (errors.Count >= limits.MaxClientErrors)
            { MarkLimit(nameof(limits.MaxClientErrors), errors.Count + 1L, limits.MaxClientErrors); return; }
            if (Encoding.UTF8.GetByteCount(error.Reason) > limits.MaxErrorBytes)
            { MarkLimit(nameof(limits.MaxErrorBytes), Encoding.UTF8.GetByteCount(error.Reason), limits.MaxErrorBytes); return; }
            var value = ErrorValue(error, false, ++sequence);
            errors.Add(value);
            if (error.IsFatal) ObserveTerminal("error", value, value.ObservedElapsedMs);
        }
    }
    internal void Exception(Error error, bool constructing)
    {
        lock (gate)
        {
            if (Encoding.UTF8.GetByteCount(error.Reason) > limits.MaxErrorBytes)
            { MarkLimit(nameof(limits.MaxErrorBytes), Encoding.UTF8.GetByteCount(error.Reason), limits.MaxErrorBytes); return; }
            ProbeV2Error value = ErrorValue(error, constructing, ++sequence);
            ObserveTerminal("error", value, value.ObservedElapsedMs);
        }
    }
    internal void Delivery(DeliveryReport<byte[], byte[]> report)
    {
        lock (gate)
        {
            long order = ++sequence;
            if (report.Error.IsError && Encoding.UTF8.GetByteCount(report.Error.Reason) > limits.MaxErrorBytes)
            { MarkLimit(nameof(limits.MaxErrorBytes), Encoding.UTF8.GetByteCount(report.Error.Reason), limits.MaxErrorBytes); return; }
            ProbeV2Error? error = report.Error.IsError ? ErrorValue(report.Error, false, order) : null;
            long elapsed = ElapsedMs;
            if (!string.IsNullOrEmpty(report.Topic) && report.Partition.Value >= 0 && report.Offset.Value >= 0)
                delivery ??= new(report.Topic, report.Partition.Value, report.Offset.Value, report.Status.ToString(), error, elapsed, order);
            ObserveTerminal(error is null ? "success" : "error", error, elapsed);
        }
    }
    internal void Read(ConsumeResult<byte[], byte[]>? value, ProbeV2Requested requested, bool unassigned = false)
    {
        if (value is null) return;
        lock (gate)
        {
            long order = ++sequence;
            if (value.IsPartitionEOF) { eofCount++; return; }
            if (unassigned) { Diagnostic("unassigned_record"); MarkLimit(); return; }
            recordsSeen++;
            if (recordsSeen > limits.MaxNonmatchingRecords)
            { MarkLimit(nameof(limits.MaxNonmatchingRecords), recordsSeen, limits.MaxNonmatchingRecords); return; }
            if (value.Topic != requested.Topic || value.Partition.Value != requested.Partition || value.Offset.Value != requested.Offset) return;
            if (value.Message?.Key is not byte[] key) { MarkLimit(); return; }
            if (key.Length > limits.MaxKeyBytes) { MarkLimit(nameof(limits.MaxKeyBytes), key.Length, limits.MaxKeyBytes); return; }
            if (value.Message.Value?.Length > limits.MaxValueBytes)
            { MarkLimit(nameof(limits.MaxValueBytes), value.Message.Value.Length, limits.MaxValueBytes); return; }
            record ??= new(value.Topic, value.Partition.Value, value.Offset.Value, Convert.ToBase64String(key),
                value.Message.Value is null ? null : Convert.ToBase64String(value.Message.Value), ElapsedMs, order);
            ObserveTerminal("success", null, record.ObservedElapsedMs);
        }
    }
    internal void NoOutcome()
    {
        lock (gate)
        {
            if (terminal || limit || errors.Any(error => error.ObservedElapsedMs <= windowMs) ||
                record is not null && record.ObservedElapsedMs <= windowMs || ElapsedMs < windowMs) return;
            terminal = true;
            terminalElapsed = windowMs;
            terminalUnix = clock.GetUtcNow().ToUnixTimeMilliseconds();
            result = "no_outcome";
        }
    }
    internal void Statistics(string raw)
    {
        lock (gate)
        {
            long order = ++sequence;
            if (!terminal || statistics.Count != 0 || limit || ElapsedMs - terminalElapsed > limits.PostTerminalStatisticsSeconds * 1000L) return;
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                int bytes = utf8.GetByteCount(raw);
                if (bytes > limits.MaxStatisticsReportBytes)
                { MarkLimit(nameof(limits.MaxStatisticsReportBytes), bytes, limits.MaxStatisticsReportBytes); return; }
                statistics.Add(new(order, ElapsedMs, Convert.ToBase64String(utf8.GetBytes(raw))));
            }
            catch (EncoderFallbackException) { MarkLimit("capture_rejected:StatisticsUtf8"); }
        }
    }
    internal void Log(LogMessage message)
    {
        lock (gate)
        {
            if (message.Message.Length > limits.MaxClientLogBytes || message.Facility.Length > limits.MaxClientLogBytes)
            { logCapped = true; return; }
            string line = "probe_v2_client_log " + logContext + message.Facility.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + " " +
                message.Message.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
            int bytes = Encoding.UTF8.GetByteCount(line) + 1;
            if (bytes > limits.MaxClientLogBytes - logBytes - Encoding.UTF8.GetByteCount("probe_v2_client_log_capped " + logContext) - 1) { logCapped = true; return; }
            logs.Add(line);
            logBytes += bytes;
        }
    }
    internal IReadOnlyList<string> Logs()
    {
        lock (gate) return logCapped ? [.. logs, "probe_v2_client_log_capped " + logContext] : logs.ToArray();
    }
    internal void Diagnostic(string reason)
    {
        lock (gate)
        {
            if (diagnostics.Contains(reason, StringComparer.Ordinal)) return;
            if (Encoding.UTF8.GetByteCount(reason) > limits.MaxDiagnosticBytes)
            { MarkLimit(nameof(limits.MaxDiagnosticBytes), Encoding.UTF8.GetByteCount(reason), limits.MaxDiagnosticBytes); return; }
            if (!diagnosticBudget.TryAdd(diagnostics, reason))
            { MarkLimit(nameof(limits.MaxDiagnostics), limits.MaxDiagnostics + 1L, limits.MaxDiagnostics); return; }
        }
    }
    internal void CaptureLimit() { lock (gate) MarkLimit(); }
    internal void CaptureLimit(string name, long observed, long allowed) { lock (gate) MarkLimit(name, observed, allowed); }
    internal ProbeV2Repeat Snapshot(int repeat, string clientId, ProbeV2Stage stage, IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<string> omitted, int[]? metadata, ProbeV2Requested requested)
    {
        lock (gate) return new(repeat, "required", clientId, stage.Action, terminal, terminal ? terminalElapsed : ElapsedMs,
            terminalUnix, settings, omitted, metadata, requested, delivery, new(recordsSeen, eofCount, record), result,
            terminalError, errors.ToArray(), statistics.ToArray(), diagnostics.AsReadOnly());
    }
    private ProbeV2Error ErrorValue(Error error, bool constructing, long order) => new((int)error.Code, error.IsFatal,
        constructing ? "construction" : error.IsBrokerError ? "broker" : "local", error.Reason, ElapsedMs, order);
    private void ObserveTerminal(string outcome, ProbeV2Error? error, long elapsed)
    {
        if (terminal || limit || elapsed > windowMs) return;
        terminal = true;
        terminalElapsed = elapsed;
        terminalUnix = clock.GetUtcNow().ToUnixTimeMilliseconds();
        result = outcome;
        terminalError = error;
    }
    private void MarkLimit(string name, long observed, long allowed) =>
        MarkLimit(FormattableString.Invariant($"capture_rejected:{name}:observed={observed}:allowed={allowed}"));
    private void MarkLimit(string? detail = null)
    {
        limit = true;
        if (!diagnostics.Contains("capture_limit", StringComparer.Ordinal))
        {
            diagnosticBudget.MarkLimit(diagnostics, detail);
        }
    }
}

internal sealed class ProbeV2DiagnosticBudget(int maximum)
{
    private readonly object gate = new();
    private readonly List<List<string>> lists = [];
    private int count;
    internal bool TryAdd(List<string> target, string value)
    {
        lock (gate)
        {
            if (count >= maximum) return false;
            if (!lists.Contains(target)) lists.Add(target);
            target.Add(value);
            count++;
            return true;
        }
    }
    internal void MarkLimit(List<string> target, string? detail)
    {
        lock (gate)
        {
            if (!lists.Contains(target)) lists.Add(target);
            foreach (string value in detail is null ? ["capture_limit"] : new[] { "capture_limit", detail })
            {
                if (count >= maximum)
                {
                    List<string> previous = lists.Last(value => value.Any(reason => reason != "capture_limit" && !reason.StartsWith("capture_rejected:", StringComparison.Ordinal)));
                    previous.RemoveAt(previous.FindLastIndex(reason => reason != "capture_limit" && !reason.StartsWith("capture_rejected:", StringComparison.Ordinal)));
                    count--;
                }
                target.Add(value);
                count++;
            }
        }
    }
}
