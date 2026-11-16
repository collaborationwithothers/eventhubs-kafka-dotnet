#pragma warning disable CA1707
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed partial class ConsumerSelfKillTests
{
    private static readonly int[] SignalZero = [0];
    private static readonly int[] SignalZeroNine = [0, 9];
    private static readonly int[] ExitOne = [1];
    private static readonly string[] StoredCalls = ["store:10", "store:20"];
    private static readonly string[] KillIntentProperties = ["event_utc", "instance_id", "kind", "offset", "partition", "row_sequence", "schema_version"];

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1000000, true)]
    [InlineData(1000001, false)]
    public async Task Given_K1_kill_count_boundary_When_executed_Then_validation_controls_registration(int killCount, bool valid)
    {
        using var harness = Harness(killCount);
        Assert.Equal(1, await harness.RunAsync());
        if (!valid)
        {
            AssertFailure(harness, "consumer_plan_invalid");
            Assert.Empty(harness.Signals);
            Assert.Equal(0, harness.StoreFactories);
            Assert.Equal(0, harness.ConsumerFactories);
            return;
        }
        Assert.Equal(1, harness.StoreFactories);
        Assert.NotNull(harness.Store.Registration);
        Assert.Equal(1, harness.ConsumerFactories);
        Assert.Equal(SignalZero, harness.Signals);
        Assert.DoesNotContain(harness.Store.Rows, row => Kind(row) == "kill_intent");
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Null(harness.Store.Completion);
        AssertFailure(harness, "consumer_kill_count_not_reached");
    }

    [Theory]
    [InlineData("witness")]
    [InlineData("slow-message")]
    public async Task Given_K2_kill_count_with_witness_or_slow_message_When_executed_Then_plan_is_invalid(string variant)
    {
        using var harness = Harness(3);
        if (variant == "witness")
        {
            harness.Options.Role = "witness";
            harness.Options.JobName = "run1-c1-r2-consumer-witness";
            harness.Options.PodName = harness.Options.JobName + "-abcde";
        }
        else harness.Plan = harness.Plan with { SlowMessage = new ConsumerSlowMessage(1, 500, "inline") };
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_plan_invalid");
        Assert.Empty(harness.Signals);
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
    }

    [Fact]
    public async Task Given_K3_kill_count_When_executed_Then_signal_zero_precedes_registration_and_window_end_fails()
    {
        using var harness = Harness(3);
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal("signal:0", harness.Calls[0]);
        Assert.True(harness.Calls.IndexOf("signal:0") < harness.Calls.IndexOf("registration"));
        AssertNotReached(harness);
    }

    [Theory]
    [InlineData("nonzero")]
    [InlineData("entry-point")]
    [InlineData("library")]
    public async Task Given_K4_preflight_cannot_signal_When_executed_Then_nothing_is_registered(string variant)
    {
        using var harness = Harness(3);
        harness.Signal = variant switch
        {
            "nonzero" => _ => (-1, 1),
            "entry-point" => _ => throw new EntryPointNotFoundException(),
            _ => _ => throw new DllNotFoundException()
        };
        Assert.Equal(1, await harness.RunAsync());
        AssertFailure(harness, "consumer_self_kill_unavailable");
        Assert.Equal(SignalZero, harness.Signals);
        Assert.Equal(0, harness.StoreFactories);
        Assert.Equal(0, harness.ConsumerFactories);
    }

    [Fact]
    public async Task Given_K5_kill_count_three_When_third_message_arrives_Then_intent_row_then_signal_nine_with_nothing_between()
    {
        using var harness = Harness(3);
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(10, 0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(20, 1));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(11, 0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(21, 1));
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(["assigned", "processed", "processed", "kill_intent"], harness.Store.Rows.Select(Kind));
        Assert.Equal([0L, 1, 2, 3], harness.Store.Rows.Select(row => row.GetProperty("row_sequence").GetInt64()));
        Assert.Equal([(0, 10L), (1, 20L)], harness.Store.Processed.Select(row => (row.GetProperty("partition").GetInt32(), row.GetProperty("offset").GetInt64())));
        JsonElement intent = harness.Store.Rows[3];
        Assert.Equal(0, intent.GetProperty("partition").GetInt32());
        Assert.Equal(11, intent.GetProperty("offset").GetInt64());
        Assert.Equal(StoredCalls, harness.Calls.Where(call => call.StartsWith("store:", StringComparison.Ordinal)));
        Assert.Equal(SignalZeroNine, harness.Signals);
        Assert.Equal(["consume", "append:kill_intent", "signal:9"], harness.Calls.TakeLast(3));
        Assert.Equal(3, harness.Calls.Count(call => call == "consume"));
        Assert.Equal(0, harness.Recorded.CloseCalls);
        Assert.DoesNotContain("dispose", harness.Calls);
        Assert.Equal(0, harness.Recorded.CommitCalls);
        Assert.Null(harness.Store.Completion);
        Assert.Equal(ExitOne, harness.ExitCodes);
        AssertFailure(harness, "consumer_self_kill_failed");
    }

    [Fact]
    public async Task Given_K6_kill_count_one_with_auto_store_and_delay_When_first_message_arrives_Then_no_processing_or_wait()
    {
        using var harness = Harness(1, autoOffsetStore: "true");
        harness.Plan = harness.Plan with { ProcessingDelayMs = 5000 };
        DateTimeOffset consumedAt = default;
        harness.Recorded.Polls.Enqueue(() => { consumedAt = harness.Clock.GetUtcNow(); return ConsumerTestHarness.Message(7, 1); });
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(["assigned", "kill_intent"], harness.Store.Rows.Select(Kind));
        JsonElement intent = harness.Store.Rows[1];
        Assert.Equal(1, intent.GetProperty("row_sequence").GetInt64());
        Assert.Equal(1, intent.GetProperty("partition").GetInt32());
        Assert.Equal(7, intent.GetProperty("offset").GetInt64());
        Assert.Empty(harness.Store.Processed);
        Assert.DoesNotContain(harness.Calls, call => call.StartsWith("store:", StringComparison.Ordinal));
        Assert.Equal(consumedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture), intent.GetProperty("event_utc").GetString());
        Assert.Equal(SignalZeroNine, harness.Signals);
    }

    [Fact]
    public async Task Given_K7_kill_intent_row_When_written_Then_property_set_equals_the_verifier_row()
    {
        using var harness = Harness(1);
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(5, 0));
        Assert.Equal(1, await harness.RunAsync());
        JsonElement intent = harness.Store.Rows.Single(row => Kind(row) == "kill_intent");
        Assert.Equal(KillIntentProperties, intent.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, intent.GetProperty("schema_version").GetInt32());
        Assert.Equal(harness.Options.PodName, intent.GetProperty("instance_id").GetString());
        Assert.Matches(EventTime(), intent.GetProperty("event_utc").GetString()!);
    }

    [Fact]
    public async Task Given_K8_intent_append_fails_When_executed_Then_no_signal_nine_and_append_failure_path_runs()
    {
        using var harness = Harness(3);
        harness.Store.FailAppend = row => Kind(row) == "kill_intent";
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(10, 0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(20, 1));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(11, 0));
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(SignalZero, harness.Signals);
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Null(harness.Store.Completion);
        AssertFailure(harness, "consumer_record_append_failed");
    }

    [Fact]
    public async Task Given_K9_two_messages_When_window_ends_Then_kill_count_not_reached()
    {
        using var harness = Harness(3);
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(10, 0));
        harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(20, 1));
        Assert.Equal(1, await harness.RunAsync());
        Assert.Equal(2, harness.Store.Processed.Count());
        AssertNotReached(harness);
    }

    [Fact]
    public async Task Given_K10_no_messages_When_window_ends_Then_kill_count_not_reached()
    {
        using var harness = Harness(3);
        Assert.Equal(1, await harness.RunAsync());
        Assert.Empty(harness.Store.Processed);
        AssertNotReached(harness);
    }

    [Fact]
    public async Task Given_K11_default_plan_When_executed_Then_no_signal_is_sent_and_receipt_is_written()
    {
        using var harness = new ConsumerTestHarness();
        for (int offset = 1; offset <= 3; offset++)
        {
            long value = offset;
            harness.Recorded.Polls.Enqueue(() => ConsumerTestHarness.Message(value));
        }
        Assert.Equal(0, await harness.RunAsync());
        Assert.Empty(harness.Signals);
        Assert.NotNull(harness.Store.Completion);
    }

    [Fact]
    public void Given_K12_production_seam_When_signal_zero_is_sent_to_this_process_Then_it_returns_zero() =>
        Assert.Equal(0, ConsumerSelfSignal.Send(0).ReturnValue);

    private static ConsumerTestHarness Harness(int killCount, string autoOffsetStore = "false")
    {
        var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with
        {
            KillCount = killCount,
            ConsumerSettings = new Dictionary<string, string> { ["enable.auto.offset.store"] = autoOffsetStore }
        };
        return harness;
    }

    private static void AssertNotReached(ConsumerTestHarness harness)
    {
        Assert.DoesNotContain(harness.Store.Rows, row => Kind(row) == "kill_intent");
        Assert.Equal(1, harness.Recorded.CloseCalls);
        Assert.Null(harness.Store.Completion);
        Assert.Equal(SignalZero, harness.Signals);
        AssertFailure(harness, "consumer_kill_count_not_reached");
    }

    private static void AssertFailure(ConsumerTestHarness harness, string reason) =>
        Assert.StartsWith("consumer_processing_failed reason=" + reason + " ", harness.Error.ToString(), StringComparison.Ordinal);

    private static string? Kind(JsonElement row) => row.GetProperty("kind").GetString();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")]
    private static partial Regex EventTime();
}
#pragma warning restore CA1707
