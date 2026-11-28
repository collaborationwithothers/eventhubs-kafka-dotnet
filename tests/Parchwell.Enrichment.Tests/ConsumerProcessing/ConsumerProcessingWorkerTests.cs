#pragma warning disable CA1707
using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerProcessingWorkerTests
{
    private static readonly int[] PartitionsOneThree = [1, 3];
    private static readonly int[] CommitErrorCodes = [0, -172];
    private static readonly int[] PartitionsZeroTwo = [0, 2];
    private static readonly int[] PartitionsZeroOne = [0, 1];
    private static readonly bool[] SlowFlags = [true, false, false];
    private static readonly int[] PartitionZero = [0];
    private static readonly int[] WatchdogExitCodes = [124];
    [Theory]
    [InlineData("RunId", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ScenarioId", "C04")]
    [InlineData("Phase", "C2")]
    [InlineData("RepeatNumber", "0")]
    [InlineData("RepeatNumber", "6")]
    [InlineData("Role", "observer")]
    [InlineData("JobName", "run1-c1-r2-consumer-witness")]
    [InlineData("PodName", "other-pod")]
    [InlineData("PodUid", "11111111222233334444555555555555")]
    [InlineData("NodeName", "node name")]
    [InlineData("SourceCommit", "Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("NamespaceName", "azure-test")]
    [InlineData("BlobEndpoint", "http://synthetic.blob.core.windows.net")]
    public async Task Given_U1_invalid_identity_When_executed_Then_no_store_or_consumer_is_constructed(string property, string value)
    {
        using var harness = new ConsumerTestHarness();
        typeof(ConsumerProcessingOptions).GetProperty(property)!.SetValue(harness.Options,
            property == "RepeatNumber" ? int.Parse(value, CultureInfo.InvariantCulture) : value);
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_identity_invalid");
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
        Assert.Empty(harness.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Given_U1b_maximum_run_id_and_boundary_repeat_When_validated_Then_identity_passes(int repeat) =>
        ConsumerTestHarness.ValidOptions(new string('a', 29), repeat).Validate();

    [Fact]
    public async Task Given_U2_no_plan_source_When_executed_Then_clients_are_not_constructed()
    {
        using var harness = new ConsumerTestHarness { NoPlanSource = true };
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_plan_invalid");
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
    }

    [Theory]
    [InlineData("C18", null)]
    [InlineData("C19", "consumer_identity_invalid")]
    public void Given_scenario_id_When_options_validated_Then_C18_is_admitted_and_C19_is_rejected(string scenario, string? failure)
    {
        ConsumerProcessingOptions options = ConsumerTestHarness.ValidOptions();
        options.ScenarioId = scenario;
        if (failure is null) options.Validate();
        else Assert.Equal(failure, Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Fact]
    public async Task Given_U3_unsupported_setting_When_executed_Then_setting_failure_precedes_registration()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { ConsumerSettings = new Dictionary<string, string> { ["fetch.wait.max.ms"] = "100" } };
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_setting_not_supported");
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
    }

    [Theory]
    [InlineData("delay", -1, false)]
    [InlineData("delay", 0, true)]
    [InlineData("delay", 60000, true)]
    [InlineData("delay", 60001, false)]
    [InlineData("window", 999, false)]
    [InlineData("window", 1000, true)]
    [InlineData("witness-slow", 0, false)]
    [InlineData("pause-auto-store", 0, false)]
    [InlineData("equal-window", 5000, false)]
    [InlineData("poll-with-session", 29999, false)]
    [InlineData("poll-with-session", 30000, true)]
    [InlineData("poll-alone", 20000, false)]
    public async Task Given_U4_plan_boundary_When_executed_Then_validation_controls_registration(string field, int value, bool valid)
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = field switch
        {
            "delay" => harness.Plan with { ProcessingDelayMs = value },
            "window" => harness.Plan with { WindowMs = value },
            "witness-slow" => harness.Plan with { SlowMessage = new ConsumerSlowMessage(1, 100, "inline") },
            "pause-auto-store" => harness.Plan with { SlowMessage = new ConsumerSlowMessage(1, 100, "pause"), ConsumerSettings = new Dictionary<string, string> { ["enable.auto.offset.store"] = "true" } },
            "equal-window" => harness.Plan with { WindowMs = value, SlowMessage = new ConsumerSlowMessage(1, value, "inline") },
            "poll-with-session" => harness.Plan with { ConsumerSettings = new Dictionary<string, string> { ["session.timeout.ms"] = "30000", ["max.poll.interval.ms"] = value.ToString(CultureInfo.InvariantCulture) } },
            _ => harness.Plan with { ConsumerSettings = new Dictionary<string, string> { ["max.poll.interval.ms"] = value.ToString(CultureInfo.InvariantCulture) } }
        };
        if (field == "witness-slow")
        {
            harness.Options.Role = "witness";
            harness.Options.JobName = "run1-c1-r2-consumer-witness";
            harness.Options.PodName = harness.Options.JobName + "-abcde";
        }
        Assert.Equal(valid ? 0 : 1, await harness.RunAsync());
        if (!valid)
        {
            AssertFailure(harness, "consumer_plan_invalid");
            Assert.Equal(0, harness.StoreFactories);
            Assert.Equal(0, harness.ConsumerFactories);
        }
    }

    [Fact]
    public async Task Given_U5_registration_failure_When_executed_Then_consumer_is_not_constructed()
    {
        using var harness = new ConsumerTestHarness();
        harness.Store.FailRegistration = true;
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_registration_failed");
        Assert.Equal(0, harness.ConsumerFactories);
        Assert.Null(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_U6_subject_registration_When_executed_Then_exact_identity_and_builder_settings_are_recorded()
    {
        using var harness = new ConsumerTestHarness();
        Assert.Equal(0, await harness.RunAsync());
        JsonElement registration = harness.Store.Registration!.Value;
        Assert.Equal("run1/C1/r2/consumer/run1-c1-r2-consumer-subject-abcde/registration.json", harness.Options.Prefix + "registration.json");
        Assert.Equal("consumer_registration", registration.GetProperty("kind").GetString());
        Assert.Equal("C03", registration.GetProperty("scenario_id").GetString());
        Assert.Equal("C1", registration.GetProperty("configuration_id").GetString());
        Assert.Equal("C1", registration.GetProperty("phase").GetString());
        Assert.Equal(2, registration.GetProperty("repeat_number").GetInt32());
        Assert.Equal("subject", registration.GetProperty("role").GetString());
        Assert.Equal(harness.Options.PodName, registration.GetProperty("instance_id").GetString());
        Assert.Equal(harness.Options.NodeName, registration.GetProperty("node_name").GetString());
        Assert.Equal("run1-c1-r2-consumers", registration.GetProperty("group_id").GetString());
        Assert.Equal(ConsumerRecordCodecTests.RegistrationFields.Order(StringComparer.Ordinal), registration.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        JsonProperty[] settings = registration.GetProperty("effective_settings").EnumerateObject().ToArray();
        Assert.Equal(settings.Select(property => property.Name).Order(StringComparer.Ordinal), settings.Select(property => property.Name));
        Assert.Equal(harness.Configuration!.OrderBy(pair => pair.Key, StringComparer.Ordinal), settings.Select(property => new KeyValuePair<string, string>(property.Name, property.Value.GetString()!)));
        Assert.True(harness.Calls.IndexOf("registration") < harness.Calls.IndexOf("construct"));
    }

    [Fact]
    public async Task Given_U7_three_messages_When_processed_Then_rows_precede_offset_stores_and_sequences_are_contiguous()
    {
        using var harness = ThreeMessages();
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal(new long[] { 1, 2, 3 }, harness.Recorded.StoredOffsets);
        Assert.Equal(new long[] { 0, 1, 2, 3 }, harness.Store.Rows.Select(row => row.GetProperty("row_sequence").GetInt64()));
        for (int index = 0; index < 3; index++)
        {
            int store = harness.Calls.IndexOf("store:" + index.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("append:processed", harness.Calls[store - 1]);
        }
    }

    [Fact]
    public async Task Given_U8_second_processed_append_failure_When_executed_Then_only_first_offset_is_stored_and_close_runs()
    {
        using var harness = ThreeMessages();
        harness.Store.FailAppend = row => Kind(row) == "processed" && row.GetProperty("offset").GetInt64() == 1;
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(new long[] { 1 }, harness.Recorded.StoredOffsets);
        Assert.Equal(0, harness.Recorded.CommitCalls);
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Null(harness.Store.Completion);
        AssertFailure(harness, "consumer_record_append_failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_U8b_handler_append_failure_When_rethrown_unwrapped_by_consume_or_close_Then_append_failure_wins(bool duringClose)
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        harness.Store.FailAppend = row => Kind(row) == "partition_event" && row.GetProperty("event").GetString() == "revoked";
        if (duringClose) harness.Recorded.OnClose = () => harness.Revoke(false, 0);
        else harness.Recorded.Polls.Enqueue(() => { harness.Revoke(false, 0); return ConsumerTestHarness.Message(1); });
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(new long[] { 1 }, harness.Recorded.StoredOffsets);
        Assert.Equal(2, harness.Store.Rows.Count);
        Assert.Equal(3, harness.Store.AppendAttempts);
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Null(harness.Store.Completion);
        AssertFailure(harness, "consumer_record_append_failed");
    }

    [Fact]
    public async Task Given_U9_auto_offset_store_When_three_messages_are_processed_Then_no_manual_store_is_made()
    {
        using var harness = ThreeMessages();
        harness.Plan = harness.Plan with { ConsumerSettings = new Dictionary<string, string> { ["enable.auto.offset.store"] = "true" } };
        Assert.Equal(0, await harness.RunAsync());
        Assert.Empty(harness.Recorded.StoredOffsets);
        Assert.Equal(3, harness.Store.Processed.Count());
    }

    [Fact]
    public async Task Given_U10_store_state_then_other_failure_When_processed_Then_state_continues_and_other_error_is_fatal()
    {
        using var harness = ThreeMessages();
        harness.Recorded.OnStore = message => throw new KafkaException(new Error(message.Offset.Value == 0 ? ErrorCode.Local_State : ErrorCode.Local_Fail));
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(2, harness.Store.Processed.Count());
        Assert.Contains("store:0", harness.Calls);
        Assert.Contains("store:1", harness.Calls);
        Assert.DoesNotContain("store:2", harness.Calls);
        AssertFailure(harness, "consumer_fatal_error");
    }

    [Fact]
    public async Task Given_U11_revoke_and_commit_callback_When_served_Then_entries_are_sorted_and_success_reason_is_null()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() =>
        {
            harness.Revoke(false, 3, 1);
            harness.Handlers.OffsetsCommitted(harness.Consumer, CommitResult());
            return null;
        });
        Assert.Equal(0, await harness.RunAsync());
        JsonElement revoked = harness.Store.Rows[1];
        Assert.Equal("revoked", revoked.GetProperty("event").GetString());
        Assert.Equal(PartitionsOneThree, revoked.GetProperty("partitions").EnumerateArray().Select(value => value.GetInt32()));
        JsonElement commit = harness.Store.Rows[2];
        Assert.Equal("callback", commit.GetProperty("source").GetString());
        Assert.Equal(0, commit.GetProperty("error_code").GetInt32());
        Assert.Equal(JsonValueKind.Null, commit.GetProperty("error_reason").ValueKind);
        JsonElement[] offsets = commit.GetProperty("offsets").EnumerateArray().ToArray();
        Assert.Equal(PartitionsOneThree, offsets.Select(offset => offset.GetProperty("partition").GetInt32()));
        Assert.Equal(CommitErrorCodes, offsets.Select(offset => offset.GetProperty("error_code").GetInt32()));
        Assert.Equal(new long[] { 12, -1001 }, offsets.Select(offset => offset.GetProperty("offset").GetInt64()));
        Assert.Equal(0, harness.Recorded.CommitCalls);
    }

    [Fact]
    public async Task Given_U12_lost_partitions_When_window_ends_Then_loss_is_recorded_without_commit()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => { harness.Revoke(true, 0, 2); return null; });
        Assert.Equal(0, await harness.RunAsync());
        JsonElement lost = harness.Store.Rows[1];
        Assert.Equal("lost", lost.GetProperty("event").GetString());
        Assert.Equal(PartitionsZeroTwo, lost.GetProperty("partitions").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(0, harness.Recorded.CommitCalls);
    }

    [Fact]
    public async Task Given_U13_nonfatal_max_poll_error_When_consumed_Then_next_message_is_processed()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_MaxPollExceeded)));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        Assert.Equal(0, await harness.RunAsync());
        Assert.Single(harness.Store.Processed);
    }

    [Fact]
    public async Task Given_U14_fatal_consume_error_When_consumed_Then_receipt_is_suppressed()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail, "synthetic fatal", true)));
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_fatal_error");
        Assert.Null(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_U15_inline_slow_second_message_When_processed_Then_no_poll_occurs_during_delay()
    {
        using var harness = ThreeMessages();
        harness.Plan = harness.Plan with { WindowMs = 12000, SlowMessage = new ConsumerSlowMessage(2, 10000, "inline") };
        harness.Clock.ExpectProcessingTimers(2); // Watchdog is timer 1; the inline delay is timer 2.
        Assert.Equal(0, await harness.RunAsync());
        JsonElement[] rows = harness.Store.Processed.ToArray();
        Assert.False(rows[0].GetProperty("slow").GetBoolean());
        Assert.True(rows[1].GetProperty("slow").GetBoolean());
        long started = Milliseconds(rows[1], "processing_started_utc");
        long ended = Milliseconds(rows[1], "processing_ended_utc");
        Assert.Equal(10000, ended - started);
        Assert.DoesNotContain(harness.Recorded.PollTimes, time => time >= started && time < ended);
    }

    [Theory]
    [InlineData(30000)]
    [InlineData(60000)]
    public async Task Given_processing_delay_outlasting_window_When_clock_drives_declared_timer_Then_processing_finishes_before_close(int delay)
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { ProcessingDelayMs = delay };
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        harness.Clock.ExpectProcessingTimers(2); // Watchdog, then one processing delay, then close timeout.
        Assert.Equal(0, await harness.RunAsync());
        JsonElement row = Assert.Single(harness.Store.Processed);
        Assert.Equal(delay, Milliseconds(row, "processing_ended_utc") - Milliseconds(row, "processing_started_utc"));
        Assert.Equal(100 + delay, harness.Clock.GetTimestamp());
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Empty(harness.ExitCodes);
        Assert.NotNull(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_buffered_processing_delays_equal_to_close_timeout_When_clock_drives_declared_timers_Then_buffer_finishes_and_close_succeeds()
    {
        using var harness = ThreeMessages();
        harness.Plan = harness.Plan with
        {
            WindowMs = 70000, ProcessingDelayMs = 30000, SlowMessage = new ConsumerSlowMessage(1, 1000, "pause")
        };
        harness.Clock.ExpectProcessingTimers(2, 3); // Watchdog, then the two buffered processing delays, then close timeout.
        Assert.Equal(0, await harness.RunAsync());
        JsonElement[] rows = harness.Store.Processed.ToArray();
        Assert.Equal(new long[] { 0, 1, 2 }, rows.Select(row => row.GetProperty("offset").GetInt64()));
        Assert.Equal(1000, Milliseconds(rows[0], "processing_ended_utc") - Milliseconds(rows[0], "processing_started_utc"));
        Assert.Equal(30000, Milliseconds(rows[1], "processing_ended_utc") - Milliseconds(rows[1], "processing_started_utc"));
        Assert.Equal(30000, Milliseconds(rows[2], "processing_ended_utc") - Milliseconds(rows[2], "processing_started_utc"));
        Assert.Equal(70000, harness.Clock.GetTimestamp());
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Empty(harness.ExitCodes);
        Assert.NotNull(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_U16_pause_slow_message_When_two_messages_arrive_Then_polling_continues_and_buffer_is_processed_in_order()
    {
        using var harness = ThreeMessages();
        harness.Plan = harness.Plan with { WindowMs = 2000, SlowMessage = new ConsumerSlowMessage(1, 1000, "pause") };
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal(PartitionsZeroOne, Assert.Single(harness.Recorded.Pauses).Select(partition => partition.Partition.Value));
        Assert.Equal(PartitionsZeroOne, Assert.Single(harness.Recorded.Resumes).Select(partition => partition.Partition.Value));
        JsonElement[] rows = harness.Store.Processed.ToArray();
        Assert.Equal(new long[] { 0, 1, 2 }, rows.Select(row => row.GetProperty("offset").GetInt64()));
        Assert.Equal(SlowFlags, rows.Select(row => row.GetProperty("slow").GetBoolean()));
        long started = Milliseconds(rows[0], "processing_started_utc");
        long ended = Milliseconds(rows[0], "processing_ended_utc");
        Assert.True(harness.Recorded.PollTimes.Count(time => time >= started && time < ended) >= 10);
        int pauseIndex = harness.Calls.IndexOf("pause");
        Assert.Equal("consume", harness.Calls[pauseIndex + 1]);
        int resumeIndex = harness.Calls.IndexOf("resume");
        Assert.Equal("store:0", harness.Calls[resumeIndex - 1]);
        Assert.Equal("append:processed", harness.Calls[resumeIndex + 1]);
    }

    [Fact]
    public async Task Given_U17_buffered_partition_lost_during_pause_When_buffer_drains_Then_lost_message_is_dropped()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { WindowMs = 2000, SlowMessage = new ConsumerSlowMessage(1, 1000, "pause") };
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(1, 1));
        harness.Recorded.Polls.Enqueue(() => { harness.Revoke(true, 1); return null; });
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal(0, Assert.Single(harness.Store.Processed).GetProperty("offset").GetInt64());
        Assert.Equal(new long[] { 1 }, harness.Recorded.StoredOffsets);
        Assert.Equal(PartitionZero, Assert.Single(harness.Recorded.Resumes).Select(partition => partition.Partition.Value));
    }

    [Fact]
    public async Task Given_U18_close_callbacks_When_window_ends_Then_receipt_includes_callbacks_and_follows_close_return()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(1));
        harness.Recorded.OnClose = () => { harness.Revoke(false, 0, 1); harness.Handlers.OffsetsCommitted(harness.Consumer, CommitResult()); };
        Assert.Equal(0, await harness.RunAsync());
        Assert.Equal("revoked", harness.Store.Rows[^2].GetProperty("event").GetString());
        Assert.Equal("commit", Kind(harness.Store.Rows[^1]));
        JsonElement receipt = harness.Store.Completion!.Value;
        Assert.Equal(harness.Store.Rows.Count, receipt.GetProperty("rows").GetInt64());
        Assert.Equal(1, receipt.GetProperty("record_blobs").GetInt32());
        Assert.Equal(2, receipt.GetProperty("messages_processed").GetInt64());
        Assert.True(harness.Calls.IndexOf("close-returned") < harness.Calls.IndexOf("completion"));
        Assert.Equal(0, harness.Recorded.CommitCalls);
    }

    [Fact]
    public async Task Given_close_task_waiting_before_entry_When_clock_driver_runs_Then_shutdown_deadlines_are_not_advanced()
    {
        using var harness = new ConsumerTestHarness();
        using var closeScheduled = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        harness.Recorded.BeforeClose = () => { closeScheduled.Set(); release.Wait(); };
        int shutdownWaits = 0;
        harness.DuringTimerWait = () =>
        {
            if (!release.IsSet && closeScheduled.IsSet && !harness.Recorded.InClose &&
                harness.Clock.NextTimer is { } due && due - harness.Clock.GetTimestamp() == 30000)
            {
                shutdownWaits++;
                if (shutdownWaits == 2) release.Set();
            }
        };
        try
        {
            Assert.Equal(0, await harness.RunAsync());
            Assert.Equal(2, shutdownWaits);
            Assert.Equal(1000, harness.Clock.GetTimestamp());
            Assert.Empty(harness.ExitCodes);
            Assert.NotNull(harness.Store.Completion);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_U19_close_or_receipt_failure_When_stopping_Then_named_failure_is_returned(bool receipt)
    {
        using var harness = new ConsumerTestHarness();
        if (receipt) harness.Store.FailCompletion = true;
        else harness.Recorded.OnClose = () => throw new InvalidOperationException("synthetic_close_failure");
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, receipt ? "consumer_completion_failed" : "consumer_close_failed");
        Assert.Null(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_U19_close_blocks_beyond_thirty_seconds_When_deadline_fires_Then_close_failure_has_no_receipt()
    {
        using var harness = new ConsumerTestHarness();
        using var release = new ManualResetEventSlim();
        harness.Recorded.OnClose = () => release.Wait();
        bool advanced = false;
        harness.DuringTimerWait = () =>
        {
            if (!advanced && harness.Recorded.InClose && harness.Clock.NextTimer is { } next && next - harness.Clock.GetTimestamp() <= 30000)
            { advanced = true; harness.Clock.Advance(30001); }
        };
        try
        {
            Assert.Equal(1, await harness.RunAsync());
            AssertFailure(harness, "consumer_close_failed");
            Assert.Null(harness.Store.Completion);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData("attempt-id")]
    [InlineData("sequence")]
    [InlineData("key")]
    public async Task Given_U22_malformed_or_missing_identity_field_When_processed_Then_null_is_recorded_and_processing_continues(string field)
    {
        using var harness = new ConsumerTestHarness();
        ConsumeResult<byte[], byte[]> message = ConsumerTestHarness.Message(0);
        if (field == "key") message.Message.Key = [0xff];
        else
        {
            message.Message.Headers.Remove(field);
            if (field == "sequence") message.Message.Headers.Add(field, Encoding.UTF8.GetBytes("12x"));
        }
        harness.Recorded.Polls.Enqueue(() => message);
        Assert.Equal(0, await harness.RunAsync());
        JsonElement row = Assert.Single(harness.Store.Processed);
        Assert.Equal(JsonValueKind.Null, row.GetProperty(field.Replace('-', '_')).ValueKind);
    }

    [Fact]
    public async Task Given_U25_close_blocks_past_process_watchdog_When_clock_passes_window_plus_budget_Then_exit_124_is_requested_once()
    {
        using var harness = new ConsumerTestHarness();
        using var release = new ManualResetEventSlim();
        harness.Recorded.OnClose = () => release.Wait();
        bool advanced = false;
        harness.DuringTimerWait = () =>
        {
            if (!advanced && harness.Recorded.InClose && harness.Clock.NextTimer is { } next && next - harness.Clock.GetTimestamp() <= 30000)
            { advanced = true; harness.Clock.Advance(121001); }
        };
        try
        {
            await harness.RunAsync();
            Assert.Equal(WatchdogExitCodes, harness.ExitCodes);
            Assert.Null(harness.Store.Completion);
        }
        finally { release.Set(); }
    }

    internal static CommittedOffsets CommitResult() => new(
        [new TopicPartitionOffsetError("document-events", 3, Offset.Unset, new Error(ErrorCode.Local_State)),
         new TopicPartitionOffsetError("document-events", 1, 12, new Error(ErrorCode.NoError))], new Error(ErrorCode.NoError));
    private static ConsumerTestHarness ThreeMessages()
    {
        var harness = new ConsumerTestHarness();
        for (long offset = 0; offset < 3; offset++)
        {
            long captured = offset;
            harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(captured));
        }
        return harness;
    }
    private static void AssertFailure(ConsumerTestHarness harness, string reason) =>
        Assert.StartsWith("consumer_processing_failed reason=" + reason + " ", harness.Error.ToString());
    private static string? Kind(JsonElement row) => row.GetProperty("kind").GetString();
    private static long Milliseconds(JsonElement row, string name) => (long)(DateTimeOffset.Parse(row.GetProperty(name).GetString()!, CultureInfo.InvariantCulture) - new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero)).TotalMilliseconds;
}
#pragma warning restore CA1707
