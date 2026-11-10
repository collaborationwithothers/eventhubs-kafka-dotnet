using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptJournal : IAsyncDisposable
{
    internal const long MaximumBytes = 16777216;
    private readonly FileStream file;
    private readonly B10AbruptWire.Identity identity;
    private readonly TimeProvider clock;
    private readonly Func<CancellationToken, Task>? beforeFlush;
    private readonly object sync = new();
    private readonly List<B10AbruptWire.Frame> frames = [];
    private readonly List<string> gaps = [];
    private readonly Channel<(string Kind, JsonElement Data)> pending = Channel.CreateBounded<(string, JsonElement)>(new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private Task? worker;
    private long nativeErrors;
    private long queued;
    private long completed;
    private bool gapWritten;
    private int queueFull;

    internal B10AbruptJournal(string path, B10AbruptWire.Identity identity, TimeProvider timeProvider, Func<CancellationToken, Task>? beforeFlush = null)
    {
        Path = path; this.identity = identity; clock = timeProvider; this.beforeFlush = beforeFlush;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
    }
    internal string Path { get; }
    internal IReadOnlyList<B10AbruptWire.Frame> Frames { get { lock (sync) return frames.ToArray(); } }
    internal IReadOnlyList<string> Gaps { get { lock (sync) { MergeCallbackGap(); return gaps.ToArray(); } } }
    private void MergeCallbackGap() { if (Volatile.Read(ref queueFull) != 0 && !gaps.Contains("queue_full", StringComparer.Ordinal)) gaps.Add("queue_full"); }
    internal long NativeErrorCount => Interlocked.Read(ref nativeErrors);
    internal void LatchGap(string reason) { lock (sync) if (!gaps.Contains(reason, StringComparer.Ordinal)) gaps.Add(reason); }
    internal Task<B10AbruptWire.Frame> AppendPreparedAndFlushAsync(JsonElement data, CancellationToken cancellationToken = default) => AppendAsync("prepared", data, cancellationToken);
    internal Task<B10AbruptWire.Frame> AppendAcceptedAndFlushAsync(JsonElement data, CancellationToken cancellationToken = default) => AppendAsync("accepted", data, cancellationToken);
    internal Task<B10AbruptWire.Frame> AppendAsync(string kind, JsonElement data, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            B10AbruptWire.Frame frame = AppendLocked(kind, data, clock.GetUtcNow(), cancellationToken);
            if (kind == "child_started") worker ??= Task.Run(WriteLoopAsync, CancellationToken.None);
            return Task.FromResult(frame);
        }
    }
    internal Task<B10AbruptWire.Frame> AppendCutAsync(string cutAttemptId, CancellationToken cancellationToken = default) => AppendBoundaryAsync("cut", cutAttemptId, TimeSpan.FromSeconds(120), cancellationToken);
    internal Task<B10AbruptWire.Frame> AppendBoundaryAsync(string kind, string cutAttemptId, TimeSpan releaseDelay, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            DateTimeOffset now = clock.GetUtcNow();
            var ids = Unconfirmed(frames, "same_instance");
            JsonElement data = kind == "cut"
                ? B10AbruptWire.Element(new { arm = "same_instance", cut_attempt_id = cutAttemptId, release_utc = B10AbruptPublisher.Utc(now + releaseDelay), unconfirmed_attempt_ids = ids })
                : B10AbruptWire.Element(new { arm = "cross_restart", cut_attempt_id = cutAttemptId, release_utc = B10AbruptPublisher.Utc(now + releaseDelay) });
            return Task.FromResult(AppendLocked(kind, data, now, cancellationToken));
        }
    }
    private B10AbruptWire.Frame AppendLocked(string kind, JsonElement data, DateTimeOffset now, CancellationToken cancellationToken)
    {
        MergeCallbackGap();
        B10AbruptWire.ValidateData(kind, data);
        if (frames.Count == 0 && kind != "child_started") throw B10AbruptWire.Gap("malformed_frame");
        if (gaps.Count > 0 && kind != "gap" && !gapWritten)
        {
            gapWritten = true;
            AppendLocked("gap", B10AbruptWire.Element(new { reason = gaps[0], first_missing_sequence = (long?)null, capture_status = "invalid" }), now, cancellationToken);
        }
        var frame = new B10AbruptWire.Frame(identity, checked(frames.Count + 1L), now.ToString("O", CultureInfo.InvariantCulture), clock.GetTimestamp(), clock.TimestampFrequency, kind, data.Clone());
        byte[] bytes = B10AbruptWire.WriteFrame(frame);
        string? gap = bytes.Length > B10AbruptWire.MaximumBodyBytes ? "frame_oversize" : frames.Count >= 4096 ? "journal_entries_exceeded" : !FitsByteBudget(file.Length, bytes.LongLength + 1) ? "journal_bytes_exceeded" : null;
        if (gap is not null) { LatchGap(gap); throw B10AbruptWire.Gap(gap); }
        try
        {
            file.Write(bytes); file.WriteByte(10);
            beforeFlush?.Invoke(cancellationToken).GetAwaiter().GetResult();
            file.Flush();
            frames.Add(frame);
            return frame;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        { LatchGap("writer_failed"); throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_capture_gap:writer_failed", exception)); }
    }
    internal static bool FitsByteBudget(long existing, long added) => existing >= 0 && added >= 0 && existing <= MaximumBytes && added <= MaximumBytes - existing;
    internal bool TryCaptureTerminal(JsonElement data) => Enqueue("terminal", data);
    internal bool TryCaptureNativeError(int errorCode, string errorName, bool isError, bool isFatal, long monotonicTicks)
    {
        lock (pending)
        {
            long next = checked(nativeErrors + 1);
            if (!Enqueue("native_error", B10AbruptWire.Element(new { capture_sequence = next, error_code = errorCode, error_name = errorName, is_error = isError, is_fatal = isFatal, callback_monotonic_ticks = monotonicTicks }))) return false;
            Interlocked.Exchange(ref nativeErrors, next); return true;
        }
    }
    private bool Enqueue(string kind, JsonElement data)
    {
        if (Volatile.Read(ref queueFull) != 0) return false;
        if (pending.Writer.TryWrite((kind, data.Clone()))) { Interlocked.Increment(ref queued); return true; }
        // The callback never waits for the journal lock or performs I/O.
        Interlocked.Exchange(ref queueFull, 1);
        return false;
    }
    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var item in pending.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await AppendAsync(item.Kind, item.Data).ConfigureAwait(false);
                Interlocked.Increment(ref completed);
            }
        }
        catch (Exception) { LatchGap("writer_failed"); }
    }
    internal async Task DrainAvailable(CancellationToken cancellationToken = default)
    {
        long target = Interlocked.Read(ref queued);
        if (worker is null) worker = Task.Run(WriteLoopAsync, CancellationToken.None);
        while (Interlocked.Read(ref completed) < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (worker.IsCompleted) throw B10AbruptWire.Gap("writer_failed");
            await Task.Delay(TimeSpan.FromMilliseconds(1), clock, cancellationToken).ConfigureAwait(false);
        }
    }
    internal async Task<B10AbruptWire.Descriptor> SealAsync(CancellationToken cancellationToken = default)
    {
        await DrainAvailable(cancellationToken).ConfigureAwait(false);
        lock (sync) { MergeCallbackGap(); if (gaps.Count != 0) throw B10AbruptWire.Gap(gaps[0]); file.Flush(); return B10AbruptWire.Describe(Path, "journal.jsonl"); }
    }
    public async ValueTask DisposeAsync()
    {
        pending.Writer.TryComplete();
        if (worker is not null) await worker.ConfigureAwait(false);
        await file.DisposeAsync().ConfigureAwait(false);
    }
    internal sealed record Snapshot(IReadOnlyList<B10AbruptWire.Frame> Frames, IReadOnlyList<string> Gaps, IReadOnlyDictionary<string, string> Failures, IReadOnlyList<string> ControlUnconfirmedAttemptIds, IReadOnlyList<string> KillUnconfirmedAttemptIds);
    internal static Snapshot Read(byte[] bytes, B10AbruptWire.Identity? expectedIdentity = null)
    {
        List<B10AbruptWire.Frame> records = []; List<string> missing = []; Dictionary<string, string> failures = new(StringComparer.Ordinal);
        void Gap(string reason) { if (!missing.Contains(reason, StringComparer.Ordinal)) missing.Add(reason); }
        if (bytes.LongLength > MaximumBytes) { Gap("journal_bytes_exceeded"); return new(records, missing, failures, [], []); }
        if (bytes.Length == 0 || bytes[^1] != 10) Gap("malformed_frame");
        int start = 0, physicalRecords = 0; long next = 1, native = 1;
        while (start < bytes.Length)
        {
            if (++physicalRecords > 4096) { Gap("journal_entries_exceeded"); break; }
            int length = bytes.AsSpan(start).IndexOf((byte)10); if (length < 0) break;
            try
            {
                var frame = B10AbruptWire.ReadFrame(bytes.AsMemory(start, length));
                if (frame.Sequence != next) Gap("sequence_gap"); next = checked(frame.Sequence + 1);
                expectedIdentity ??= frame.Identity;
                if (frame.Identity != expectedIdentity || records.Count == 0 && frame.Kind != "child_started" || records.Count > 0 && frame.Kind == "child_started") Gap("malformed_frame");
                if (frame.Kind == "gap") Gap(frame.Data.GetProperty("reason").GetString()!);
                if (frame.Kind == "native_error" && frame.Data.GetProperty("capture_sequence").GetInt64() != native++) { Gap("sequence_gap"); start += length + 1; continue; }
                records.Add(frame);
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or KeyNotFoundException or OverflowException or JsonException) { Gap(exception.Message.EndsWith("frame_oversize", StringComparison.Ordinal) ? "frame_oversize" : "malformed_frame"); }
            start += length + 1;
        }
        Dictionary<string,B10AbruptWire.Frame> prepared = new(StringComparer.Ordinal); HashSet<string> accepted = new(StringComparer.Ordinal), rejected = new(StringComparer.Ordinal), terminal = new(StringComparer.Ordinal); Dictionary<string,List<B10AbruptWire.Frame>> reports = new(StringComparer.Ordinal);
        string[] control = []; bool cut = false, kill = false;
        foreach (var frame in records)
        {
            var data = frame.Data;
            string? id = data.TryGetProperty("attempt_id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (frame.Kind == "prepared")
            {
                if (!prepared.TryAdd(id!,frame)) Gap("malformed_frame");
                string arm = data.GetProperty("arm").GetString()!;
                if (data.GetProperty("key").GetString() != $"B10/{frame.Identity.RunId}/C1/1/{arm}" || data.GetProperty("probe_id").GetString() != data.GetProperty("key").GetString()) Gap("malformed_frame");
                long ordinal = data.GetProperty("ordinal").GetInt64();
                var previous = prepared.Values.Where(item => item.Sequence < frame.Sequence && item.Data.GetProperty("arm").GetString() == arm).ToArray();
                if (data.GetProperty("role").GetString() == "original")
                {
                    if (previous.Count(item => item.Data.GetProperty("role").GetString() == "original") != ordinal) Gap("malformed_frame");
                }
                else
                {
                    string predecessor = data.GetProperty("predecessor_attempt_id").GetString()!;
                    if (arm != "same_instance" || !prepared.TryGetValue(predecessor, out var original) || original.Data.GetProperty("role").GetString() != "original" || original.Data.GetProperty("arm").GetString() != arm || !accepted.Contains(predecessor) || original.Data.GetProperty("ordinal").GetInt64() != ordinal || original.Data.GetProperty("payload_length").GetInt64() != data.GetProperty("payload_length").GetInt64() || original.Data.GetProperty("payload_sha256").GetString() != data.GetProperty("payload_sha256").GetString() || previous.Any(item => item.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.String && item.Data.GetProperty("predecessor_attempt_id").GetString() == predecessor)) Gap("malformed_frame");
                }
            }
            if (frame.Kind == "pre_acceptance_report")
            {
                string arm = data.GetProperty("arm").GetString()!; if (!reports.TryGetValue(arm,out var list)) reports[arm] = list = [];
                list.Add(frame); if (list.Count > 1) failures.TryAdd(arm,"b10_abrupt_report_binding_invalid");
                if (!prepared.TryGetValue(id!,out var original) || original.Data.GetProperty("ordinal").GetInt32() != 0 || original.Data.GetProperty("role").GetString() != "original" || original.Data.GetProperty("arm").GetString() != arm || data.GetProperty("report_identity").GetProperty("run_id").GetString() != frame.Identity.RunId || data.GetProperty("report_identity").GetProperty("client_id").GetString() != frame.Identity.InstanceId || DateTimeOffset.Parse(data.GetProperty("report_capture_utc").GetString()!,CultureInfo.InvariantCulture) >= DateTimeOffset.Parse(frame.ObservedUtc,CultureInfo.InvariantCulture)) failures.TryAdd(arm,"b10_abrupt_report_binding_invalid");
            }
            if (frame.Kind == "post_drain_report")
            {
                var drained = records.LastOrDefault(record => record.Sequence < frame.Sequence && record.Kind == "control_drained");
                bool matching = data.GetProperty("report_identity").GetProperty("run_id").GetString() == frame.Identity.RunId && data.GetProperty("report_identity").GetProperty("client_id").GetString() == frame.Identity.InstanceId;
                if (!matching || drained is null || !drained.Data.GetProperty("flush_returned").GetBoolean() || drained.Data.GetProperty("out_queue_length").GetInt64() != 0 || DateTimeOffset.ParseExact(data.GetProperty("report_capture_utc").GetString()!,"O",CultureInfo.InvariantCulture) < DateTimeOffset.ParseExact(drained.ObservedUtc,"O",CultureInfo.InvariantCulture) || records.Count(record => record.Kind == "post_drain_report") != 1) failures.TryAdd("same_instance","b10_abrupt_control_incomplete");
            }
            if (frame.Kind is "accepted" or "synchronous_rejection")
            {
                if (!prepared.TryGetValue(id!,out var original) || original.Sequence != data.GetProperty("prepared_sequence").GetInt64() || accepted.Contains(id!) || rejected.Contains(id!)) { Gap("malformed_frame"); continue; }
                if (frame.Kind == "accepted")
                {
                    accepted.Add(id!);
                    if (original.Data.GetProperty("ordinal").GetInt32() == 0 && original.Data.GetProperty("role").GetString() == "original")
                    { string arm = original.Data.GetProperty("arm").GetString()!; if (!reports.TryGetValue(arm,out var bound) || bound.Count == 0) failures.TryAdd(arm,"b10_abrupt_sample_missing"); }
                }
                else rejected.Add(id!);
            }
            if (frame.Kind == "terminal") { if (!prepared.ContainsKey(id!) || !terminal.Add(id!) || rejected.Contains(id!)) Gap("malformed_frame"); int code = data.GetProperty("error_code").GetInt32(); int? status = data.GetProperty("persistence_status").ValueKind == JsonValueKind.Null ? null : data.GetProperty("persistence_status").GetInt32(); if (data.GetProperty("delivered").GetBoolean() != (code == 0) || !B10AbruptProducer.IsPermittedTerminal(code,status)) failures.TryAdd("guards","b10_abrupt_guard_failed"); }
            if (frame.Kind == "native_error" && (data.GetProperty("is_error").GetBoolean() || data.GetProperty("is_fatal").GetBoolean())) failures.TryAdd("guards","b10_abrupt_guard_failed");
            if (frame.Kind is "cut" or "kill_intent")
            {
                if (frame.Kind == "cut" ? cut : kill) Gap("malformed_frame");
                DateTimeOffset observedUtc = DateTimeOffset.ParseExact(frame.ObservedUtc,"O",CultureInfo.InvariantCulture);
                DateTimeOffset releaseUtc = DateTimeOffset.ParseExact(data.GetProperty("release_utc").GetString()!,"O",CultureInfo.InvariantCulture);
                if (releaseUtc - observedUtc != TimeSpan.FromSeconds(120)) Gap("malformed_frame");
                var previousProducing = records.LastOrDefault(record => record.Sequence < frame.Sequence && B10AbruptWire.IsProducingKind(record.Kind));
                if (previousProducing?.Kind != "accepted" || previousProducing.Data.GetProperty("attempt_id").GetString() != data.GetProperty("cut_attempt_id").GetString()) Gap("malformed_frame");
                string arm = frame.Kind == "cut" ? "same_instance" : "cross_restart";
                string boundary = data.GetProperty("cut_attempt_id").GetString()!;
                if (!prepared.TryGetValue(boundary,out var original) || !accepted.Contains(boundary) || original.Data.GetProperty("arm").GetString() != arm || original.Data.GetProperty("ordinal").GetInt32() != 4) Gap("malformed_frame");
                if (frame.Kind == "cut") { cut = true; control = Unconfirmed(records.Where(record=>record.Sequence < frame.Sequence),arm); if (!control.SequenceEqual(data.GetProperty("unconfirmed_attempt_ids").EnumerateArray().Select(item=>item.GetString()!),StringComparer.Ordinal)) Gap("malformed_frame"); } else kill = true;
            }
        }
        return new(records, missing, failures, control, Unconfirmed(records,"cross_restart"));
    }
    internal static string[] Unconfirmed(IEnumerable<B10AbruptWire.Frame> source, string arm)
    {
        var records = source.ToArray();
        var accepted = records.Where(frame=>frame.Kind == "accepted").Select(frame=>frame.Data.GetProperty("attempt_id").GetString()!).ToHashSet(StringComparer.Ordinal);
        var final = records.Where(frame=>frame.Kind is "terminal" or "synchronous_rejection").Select(frame=>frame.Data.GetProperty("attempt_id").GetString()!).ToHashSet(StringComparer.Ordinal);
        return records.Where(frame=>frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == arm && frame.Data.GetProperty("role").GetString() == "original").Select(frame=>frame.Data.GetProperty("attempt_id").GetString()!).Where(id=>accepted.Contains(id) && !final.Contains(id)).ToArray();
    }
}
