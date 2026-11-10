using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptChildChannel : IAsyncDisposable
{
    private readonly Stream events;
    private readonly Stream acknowledgements;
    private readonly Channel<Pending> pending = Channel.CreateBounded<Pending>(new BoundedChannelOptions(256)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task writer;
    private long sequence;
    private long acknowledgementSequence;
    private readonly object nativeErrorGate = new();
    private long nativeErrorCount;
    private string? gap;
    private bool gapSent;
    private sealed record Pending(string Kind, JsonElement Data, TaskCompletionSource<long>? Completion);

    private B10AbruptChildChannel(Stream events, Stream acknowledgements, long initialSequence)
    {
        this.events = events;
        this.acknowledgements = acknowledgements;
        sequence = initialSequence;
        writer = Task.Run(WriteLoopAsync);
    }

    internal string? Gap => Volatile.Read(ref gap);

    // Runs on the writer loop after a terminal frame is written, never on the delivery callback.
    internal Action<string>? TerminalWritten { get; set; }

    internal static B10AbruptChildChannel Connect(string eventsHandle, string ackHandle, string challenge)
    {
        if (!OperatingSystem.IsLinux()) throw B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_platform_unsupported"));
        var events = new AnonymousPipeClientStream(PipeDirection.Out, eventsHandle);
        var ack = new AnonymousPipeClientStream(PipeDirection.In, ackHandle);
        try
        {
            var hello = B10AbruptWire.Element(new { challenge, pid = Environment.ProcessId,
                start_time_ticks = B10AbruptProc.ReadStartTimeTicks(B10AbruptProc.ReadBounded("/proc/self/stat")), boot_id = B10AbruptProc.ReadBootId() });
            B10AbruptWire.WriteMessageAsync(events, new("hello", 1, hello)).GetAwaiter().GetResult();
            return new(events, ack, 1);
        }
        catch { events.Dispose(); ack.Dispose(); throw; }
    }

    internal async Task<long> SendAsync(string kind, JsonElement data, CancellationToken cancellationToken = default)
    {
        if (Gap is not null) throw B10AbruptWire.Gap(Gap);
        B10AbruptWire.ValidateData(kind, data);
        var completion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.Writer.TryWrite(new(kind, data.Clone(), completion)))
        {
            Interlocked.CompareExchange(ref gap, "queue_full", null);
            throw B10AbruptWire.Gap("queue_full");
        }
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The highest <c>capture_sequence</c> assigned to a <c>native_error</c> message. Every message numbered up to the
    /// value read is already in the write queue, so a seal that records it can be checked against the journal.
    /// </summary>
    internal long NativeErrorCount { get { lock (nativeErrorGate) return nativeErrorCount; } }

    // Runs on librdkafka's callback thread: one lock, one non-blocking enqueue, no file or network I/O.
    // The sequence is assigned and the message enqueued in one step, so the count never runs ahead of the queue.
    internal bool TryCaptureNativeError(int errorCode, string errorName, bool isError, bool isFatal, long monotonicTicks)
    {
        lock (nativeErrorGate)
        {
            if (Gap is not null) return false;
            long next = checked(nativeErrorCount + 1);
            JsonElement data = B10AbruptWire.Element(new
            {
                capture_sequence = next, error_code = errorCode, error_name = errorName, is_error = isError, is_fatal = isFatal,
                callback_monotonic_ticks = monotonicTicks
            });
            if (!pending.Writer.TryWrite(new("native_error", data, null)))
            {
                Interlocked.CompareExchange(ref gap, "queue_full", null);
                return false;
            }
            nativeErrorCount = next;
            return true;
        }
    }

    internal bool TryCaptureTerminal(JsonElement data)
    {
        if (Gap is not null) return false;
        if (pending.Writer.TryWrite(new("terminal", data.Clone(), null))) return true;
        Interlocked.CompareExchange(ref gap, "queue_full", null);
        return false;
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var message in pending.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (Gap is { } captureGap && !gapSent)
                    {
                        await B10AbruptWire.WriteMessageAsync(events, new("capture_gap", checked(++sequence),
                            B10AbruptWire.Element(new { reason = captureGap, first_missing_sequence = (long?)null, capture_status = "invalid" }))).ConfigureAwait(false);
                        gapSent = true;
                    }
                    await B10AbruptWire.WriteMessageAsync(events, new(message.Kind, checked(++sequence), message.Data)).ConfigureAwait(false);
                    if (message.Kind is "prepared" or "accepted" or "synchronous_rejection")
                    {
                        var acknowledgement = await B10AbruptWire.ReadMessageAsync(acknowledgements, checked(++acknowledgementSequence)).ConfigureAwait(false);
                        if (acknowledgement?.Kind != "ack") throw B10AbruptWire.Gap("malformed_frame");
                        message.Completion?.TrySetResult(B10AbruptWire.Nonnegative(acknowledgement.Data.GetProperty("acknowledged_sequence")));
                    }
                    else message.Completion?.TrySetResult(0);
                    if (message.Kind == "terminal") NotifyTerminalWritten(message.Data);
                }
                catch (Exception exception)
                {
                    message.Completion?.TrySetException(exception);
                    throw;
                }
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref gap, "writer_failed", null);
            pending.Writer.TryComplete(exception);
            while (pending.Reader.TryRead(out var message)) message.Completion?.TrySetException(exception);
        }
    }

    private void NotifyTerminalWritten(JsonElement terminal) { try { TerminalWritten?.Invoke(terminal.GetProperty("attempt_id").GetString()!); } catch (Exception) { } }

    public async ValueTask DisposeAsync()
    {
        pending.Writer.TryComplete();
        await writer.ConfigureAwait(false);
        await events.DisposeAsync().ConfigureAwait(false);
        await acknowledgements.DisposeAsync().ConfigureAwait(false);
    }
}
