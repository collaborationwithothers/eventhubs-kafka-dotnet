#pragma warning disable CA1707
using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerStopPathTests
{
    private static readonly int[] RevokedPartitions = [0, 1];
    private static readonly DateTimeOffset ClockBase = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Stop_while_polling_closes_and_writes_receipt()
    {
        using ConsumerTestHarness harness = Harness("close");
        // The first Consume advances the clock 100 ms, so the stop fires at 1159205000 ms from the clock base,
        // 2026-11-28T10:00:05.0000000Z. Advancing before RunAsync fires no timer: none exists yet.
        harness.Clock.Advance(1159204900);
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return null; });
        Assert.Equal(0, await harness.RunAsync());
        JsonElement stop = Assert.Single(harness.Store.Rows, row => Kind(row) == "stop_observed");
        Assert.Equal("2026-11-28T10:00:05.0000000Z", stop.GetProperty("event_utc").GetString());
        Assert.Equal("stop_observed", Kind(harness.Store.Rows[^1]));
        Assert.True(harness.Calls.IndexOf("close") < harness.Calls.IndexOf("close-returned"));
        Assert.True(harness.Calls.IndexOf("close-returned") < harness.Calls.IndexOf("dispose"));
        JsonElement receipt = harness.Store.StopReceipt!.Value;
        Assert.Equal("close", receipt.GetProperty("shutdown_mode").GetString());
        Assert.Equal("ok", receipt.GetProperty("shutdown_outcome").GetString());
        Assert.Equal("2026-11-28T10:00:05.0000000Z", receipt.GetProperty("stop_observed_utc").GetString());
        Assert.Equal(harness.Store.Rows.Count, receipt.GetProperty("rows").GetInt64());
        Assert.Null(harness.Store.Completion);
        Assert.DoesNotContain("completion", harness.Calls);
        Assert.Empty(harness.Error.ToString());
    }

    [Fact]
    public async Task Message_in_progress_finishes()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.Plan = harness.Plan with { ProcessingDelayMs = 200 };
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(1));
        harness.Clock.ExpectProcessingTimers(2); // Watchdog is timer 1; the inline delay is timer 2.
        bool stopped = false;
        harness.DuringTimerWait = () =>
        {
            // The delay started at 100 ms and ends at 300 ms. Stop 50 ms into it.
            if (!stopped && harness.Clock.NextTimer is { } due && due - harness.Clock.GetTimestamp() == 200)
            {
                stopped = true;
                harness.Clock.Advance(50);
                harness.StopSource.Cancel();
            }
        };
        Assert.Equal(0, await harness.RunAsync());
        Assert.True(stopped);
        JsonElement processed = Assert.Single(harness.Store.Processed);
        Assert.Equal(200, Milliseconds(processed, "processing_ended_utc") - Milliseconds(processed, "processing_started_utc"));
        Assert.True(IndexOfKind(harness, "processed") < IndexOfKind(harness, "stop_observed"));
        Assert.Equal(1, harness.Calls.Count(call => call == "consume"));
        Assert.Single(harness.Recorded.Polls);
        // Every append passed a token that cannot be cancelled, the processed row's included.
        Assert.All(harness.Store.AppendTokensCanBeCanceled, canBeCanceled => Assert.False(canBeCanceled));
        Assert.Equal(false, harness.Store.StopReceiptTokenCanBeCanceled);
    }

    [Fact]
    public async Task Message_returned_during_stop_is_processed()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return ConsumerTestHarness.Message(7); });
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(8));
        Assert.Equal(0, await harness.RunAsync());
        JsonElement processed = Assert.Single(harness.Store.Processed);
        Assert.Equal(7, processed.GetProperty("offset").GetInt64());
        Assert.True(IndexOfKind(harness, "processed") < IndexOfKind(harness, "stop_observed"));
        Assert.Equal(1, harness.Calls.Count(call => call == "consume"));
    }

    [Fact]
    public async Task Dispose_mode_skips_close()
    {
        using ConsumerTestHarness harness = Harness("dispose");
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return null; });
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal(1, harness.Calls.Count(call => call == "dispose"));
        Assert.Equal(0, harness.Recorded.CloseCalls);
        Assert.Equal("dispose", harness.Store.StopReceipt!.Value.GetProperty("shutdown_mode").GetString());
        Assert.Equal("ok", harness.Store.StopReceipt.Value.GetProperty("shutdown_outcome").GetString());
    }

    [Fact]
    public async Task Close_failure_and_timeout_are_recorded()
    {
        using (ConsumerTestHarness failed = Harness("close"))
        {
            failed.Recorded.Polls.Enqueue(() => { failed.StopSource.Cancel(); return null; });
            failed.Recorded.OnClose = () => throw new KafkaException(new Error(ErrorCode.Local_Fail, "synthetic close failure"));
            Assert.Equal(0, await failed.RunAsync());
            Assert.Equal("failed", failed.Store.StopReceipt!.Value.GetProperty("shutdown_outcome").GetString());
            Assert.DoesNotContain("dispose", failed.Calls);
            Assert.Empty(failed.Error.ToString());
        }
        using ConsumerTestHarness timedOut = Harness("close");
        using var release = new ManualResetEventSlim();
        using var closeReturned = new ManualResetEventSlim();
        timedOut.Recorded.Polls.Enqueue(() => { timedOut.StopSource.Cancel(); return null; });
        // The blocked Close raises a revoke only after the 30 s bound has expired and the receipt is written.
        timedOut.Recorded.OnClose = () => { release.Wait(); timedOut.Revoke(false, 0); closeReturned.Set(); };
        bool advanced = false;
        timedOut.DuringTimerWait = () =>
        {
            if (!advanced && timedOut.Recorded.InClose && timedOut.Clock.NextTimer is { } next && next - timedOut.Clock.GetTimestamp() <= 30000)
            { advanced = true; timedOut.Clock.Advance(30001); }
        };
        try
        {
            Assert.Equal(0, await timedOut.RunAsync());
            JsonElement receipt = timedOut.Store.StopReceipt!.Value;
            Assert.Equal("timed_out", receipt.GetProperty("shutdown_outcome").GetString());
            Assert.DoesNotContain("dispose", timedOut.Calls);
            int rowsAtReceipt = timedOut.Store.Rows.Count;
            release.Set();
            Assert.True(closeReturned.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.Equal(rowsAtReceipt, timedOut.Store.Rows.Count);
            Assert.Equal(rowsAtReceipt, receipt.GetProperty("rows").GetInt64());
            Assert.DoesNotContain(timedOut.Store.Rows, row => Kind(row) == "partition_event" && row.GetProperty("event").GetString() == "revoked");
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task Callbacks_during_close_are_recorded()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return null; });
        harness.Recorded.OnClose = () =>
        {
            harness.Revoke(false, 0, 1);
            harness.Handlers.OffsetsCommitted(harness.Consumer, ConsumerProcessingWorkerTests.CommitResult());
        };
        Assert.Equal(0, await harness.RunAsync());
        int stop = IndexOfKind(harness, "stop_observed");
        JsonElement revoked = harness.Store.Rows[stop + 1];
        Assert.Equal("partition_event", Kind(revoked));
        Assert.Equal("revoked", revoked.GetProperty("event").GetString());
        Assert.Equal(RevokedPartitions, revoked.GetProperty("partitions").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("commit", Kind(harness.Store.Rows[stop + 2]));
        Assert.All(harness.Store.AppendTokensCanBeCanceled, canBeCanceled => Assert.False(canBeCanceled));
    }

    [Fact]
    public async Task Stop_row_append_failure()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.Store.FailAppend = row => Kind(row) == "stop_observed";
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return null; });
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_record_append_failed", harness.Error.ToString(), StringComparison.Ordinal);
        Assert.Null(harness.Store.StopReceipt);
        Assert.Null(harness.Store.Completion);
    }

    [Fact]
    public async Task Receipt_failure()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.Store.FailStopReceipt = true;
        harness.Recorded.Polls.Enqueue(() => { harness.StopSource.Cancel(); return null; });
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_stop_receipt_failed scenario_id=", harness.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stop_before_subscribe_uses_existing_path()
    {
        using ConsumerTestHarness harness = Harness("close");
        harness.StopSource.Cancel();
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_fatal_error ", harness.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Store.Rows, row => Kind(row) == "stop_observed");
        Assert.Null(harness.Store.StopReceipt);
    }

    [Fact]
    public async Task Window_end_before_stop_completes()
    {
        using ConsumerTestHarness harness = Harness("close");
        Assert.Equal(0, await harness.RunAsync());
        Assert.NotNull(harness.Store.Completion);
        Assert.DoesNotContain(harness.Store.Rows, row => Kind(row) == "stop_observed");
        Assert.Null(harness.Store.StopReceipt);
    }

    [Fact]
    public async Task Registration_records_shutdown_mode()
    {
        using ConsumerTestHarness harness = Harness("dispose");
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal("dispose", harness.Store.Registration!.Value.GetProperty("shutdown_mode").GetString());
    }

    private static ConsumerTestHarness Harness(string mode)
    {
        var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { StopConfigured = true, ShutdownMode = mode };
        return harness;
    }

    private static int IndexOfKind(ConsumerTestHarness harness, string kind) => harness.Store.Rows.FindIndex(row => Kind(row) == kind);
    private static string? Kind(JsonElement row) => row.GetProperty("kind").GetString();
    private static long Milliseconds(JsonElement row, string name) =>
        (long)(DateTimeOffset.Parse(row.GetProperty(name).GetString()!, CultureInfo.InvariantCulture) - ClockBase).TotalMilliseconds;
}
#pragma warning restore CA1707
