using System.Globalization;
using System.Text.Json;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptJournal : IAsyncDisposable
{
    internal const long MaximumBytes = 16777216;
    private readonly FileStream file;
    private readonly B10AbruptWire.Identity identity;
    private readonly TimeProvider timeProvider;
    private readonly List<B10AbruptWire.Frame> frames = [];
    private readonly List<string> gaps = [];
    private readonly Queue<JsonElement> terminals = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly object sync = new();
    private bool sealedJournal;
    private readonly Func<CancellationToken, Task>? beforeFlush;

    internal B10AbruptJournal(string path, B10AbruptWire.Identity identity, TimeProvider timeProvider, Func<CancellationToken, Task>? beforeFlush = null)
    {
        Path = path;
        this.identity = identity;
        this.timeProvider = timeProvider;
        this.beforeFlush = beforeFlush;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
    }

    internal string Path { get; }
    internal IReadOnlyList<B10AbruptWire.Frame> Frames { get { lock (sync) return frames.ToArray(); } }
    internal IReadOnlyList<string> Gaps { get { lock (sync) return gaps.ToArray(); } }
    internal void LatchGap(string reason) { lock (sync) { if (!gaps.Contains(reason, StringComparer.Ordinal)) gaps.Add(reason); } }

    internal Task<B10AbruptWire.Frame> AppendPreparedAndFlushAsync(JsonElement data, CancellationToken cancellationToken = default) => AppendAsync("prepared", data, cancellationToken);
    internal Task<B10AbruptWire.Frame> AppendAcceptedAndFlushAsync(JsonElement data, CancellationToken cancellationToken = default) => AppendAsync("accepted", data, cancellationToken);
    internal Task<B10AbruptWire.Frame> CheckpointAsync(JsonElement data, CancellationToken cancellationToken = default) => AppendAsync("checkpoint", data, cancellationToken);

    internal async Task<B10AbruptWire.Frame> AppendAsync(string kind, JsonElement data, CancellationToken cancellationToken = default)
    {
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (sealedJournal || Gaps.Count != 0) throw B10AbruptWire.Gap(Gaps.Count == 0 ? "writer_failed" : Gaps[0]);
            B10AbruptWire.ValidateData(kind, data);
            if (frames.Count >= B10AbruptWire.MaximumEntries) { LatchGap("journal_entries_exceeded"); throw B10AbruptWire.Gap("journal_entries_exceeded"); }
            var frame = new B10AbruptWire.Frame(identity, checked(frames.Count + 1L), timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                timeProvider.GetTimestamp(), timeProvider.TimestampFrequency, kind, data.Clone());
            var bytes = B10AbruptWire.WriteFrame(frame);
            if (!FitsByteBudget(file.Length, bytes.LongLength + 1)) { LatchGap("journal_bytes_exceeded"); throw B10AbruptWire.Gap("journal_bytes_exceeded"); }
            try
            {
                await file.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await file.WriteAsync(new byte[] { 10 }, cancellationToken).ConfigureAwait(false);
                if (beforeFlush is not null) await beforeFlush(cancellationToken).ConfigureAwait(false);
                file.Flush(true);
                var end = file.Position;
                file.Position = end - bytes.Length - 1;
                var readback = new byte[bytes.Length + 1];
                await file.ReadExactlyAsync(readback, cancellationToken).ConfigureAwait(false);
                if (!readback.AsSpan(0, bytes.Length).SequenceEqual(bytes) || readback[^1] != 10) throw new IOException("journal_readback_mismatch");
                file.Position = end;
                lock (sync) frames.Add(frame);
                return frame;
            }
            catch { LatchGap("writer_failed"); throw B10AbruptWire.Gap("writer_failed"); }
        }
        finally { writer.Release(); }
    }

    internal static bool FitsByteBudget(long existing, long added) => existing >= 0 && added >= 0 && existing <= MaximumBytes && added <= MaximumBytes - existing;

    internal bool TryCaptureTerminal(JsonElement data)
    {
        lock (sync)
        {
            if (gaps.Count != 0) return false;
            if (terminals.Count == 256) { LatchGap("queue_full"); return false; }
            terminals.Enqueue(data.Clone());
            return true;
        }
    }

    internal async Task DrainAvailable(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            JsonElement terminal;
            lock (sync) { if (!terminals.TryDequeue(out terminal)) return; }
            await AppendAsync("terminal", terminal, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task DrainToEofAsync(Stream stream, long nextSequence, CancellationToken cancellationToken = default)
    {
        try
        {
            while (await B10AbruptWire.ReadMessageAsync(stream, nextSequence, cancellationToken).ConfigureAwait(false) is { } message)
            {
                nextSequence = checked(nextSequence + 1);
                if (message.Kind is not ("terminal" or "capture_gap")) { LatchGap("malformed_frame"); throw B10AbruptWire.Gap("malformed_frame"); }
                if (message.Kind == "capture_gap") LatchGap(message.Data.GetProperty("reason").GetString()!);
                else await AppendAsync("terminal", message.Data, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { throw new InvalidOperationException("b10_abrupt_eof_incomplete"); }
        catch (InvalidDataException exception) { LatchGap(exception.Message.Split(':')[^1]); throw; }
    }

    internal async Task<B10AbruptWire.Descriptor> SealAsync(CancellationToken cancellationToken = default)
    {
        await DrainAvailable(cancellationToken).ConfigureAwait(false);
        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Gaps.Count != 0) throw B10AbruptWire.Gap(Gaps[0]);
            file.Flush(true);
            sealedJournal = true;
            return B10AbruptWire.Describe(Path, "journal.jsonl");
        }
        finally { writer.Release(); }
    }

    public async ValueTask DisposeAsync() { await file.DisposeAsync().ConfigureAwait(false); writer.Dispose(); }
}
