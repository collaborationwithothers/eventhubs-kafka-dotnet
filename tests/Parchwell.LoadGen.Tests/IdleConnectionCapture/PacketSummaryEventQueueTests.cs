using System.Globalization;
using System.Text.Json;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
// Synthetic derived events exercise the bounded handoff without claiming capture completeness.
public sealed class PacketSummaryEventQueueTests
{
    private static readonly string[] ExpectedEnvelopeKeys = ["schema_version", "run_id", "source_commit", "configuration_id", "repeat", "capture_started_utc", "events", "capture_ended_utc"];
    private static readonly string[] ExpectedEventKeys = ["flow", "direction", "elapsed_ns", "flags"];
    private static readonly string[] ExpectedSynFlags = ["SYN"];
    private static readonly string[] ExpectedAckFlags = ["ACK"];

    [Fact]
    public void Given_DefaultQueue_When_DequeueingEmpty_Then_RemainsActiveWithProductionCapacity()
    {
        var queue = new PacketSummaryEventQueue();
        Assert.Equal(256, queue.Capacity);
        AssertActive(queue, 0);
        AssertNoItem(queue, PacketSummaryDequeueResult.Empty);
        AssertActive(queue, 0);
    }

    [Theory]
    [InlineData(1)] [InlineData(256)]
    public void Given_AllowedTestCapacity_When_Constructing_Then_StartsEmptyAndActive(int capacity)
    {
        var queue = new PacketSummaryEventQueue(capacity);
        Assert.Equal(capacity, queue.Capacity);
        AssertActive(queue, 0);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(257)]
    public void Given_DisallowedTestCapacity_When_Constructing_Then_ThrowsBeforeUse(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketSummaryEventQueue(capacity));

    [Fact]
    public void Given_FullRingWithRemovedPrefix_When_AppendingAcrossWraparound_Then_PreservesFifoAndCounts()
    {
        var queue = new PacketSummaryEventQueue(3);
        for (int n = 0; n < 3; n++)
        {
            Assert.True(queue.TryEnqueue(Event(n)));
            AssertActive(queue, n + 1);
        }
        AssertItem(queue, Event(0));
        AssertActive(queue, 2);
        AssertItem(queue, Event(1));
        AssertActive(queue, 1);
        Assert.True(queue.TryEnqueue(Event(3)));
        AssertActive(queue, 2);
        Assert.True(queue.TryEnqueue(Event(4)));
        AssertActive(queue, 3);
        for (int n = 2; n < 5; n++)
        {
            AssertItem(queue, Event(n));
            AssertActive(queue, 4 - n);
        }
        AssertNoItem(queue, PacketSummaryDequeueResult.Empty);
        AssertActive(queue, 0);
    }

    [Fact]
    public void Given_ProductionResidentBoundary_When_ExceedingCapacity_Then_DiscardsRemainderAndKeepsFirstFailure()
    {
        var queue = new PacketSummaryEventQueue();
        for (int n = 0; n < 255; n++) Assert.True(queue.TryEnqueue(Event(n)));
        AssertActive(queue, 255);
        Assert.True(queue.TryEnqueue(Event(255)));
        AssertActive(queue, 256);
        Assert.False(queue.TryEnqueue(Event(256)));
        AssertTerminal(queue, "packet_queue_capacity_exceeded");
    }

    [Fact]
    public void Given_CapacityOne_When_AppendingSecondResidentEvent_Then_FaultsAndClears()
    {
        var queue = new PacketSummaryEventQueue(1);
        Assert.True(queue.TryEnqueue(Event(0)));
        AssertActive(queue, 1);
        Assert.False(queue.TryEnqueue(Event(1)));
        AssertTerminal(queue, "packet_queue_capacity_exceeded");
    }

    [Fact]
    public void Given_CapacityTwo_When_ReusingSlotsFor257Pairs_Then_ResidentBoundDoesNotCapCumulativeTraffic()
    {
        var queue = new PacketSummaryEventQueue(2);
        for (int n = 0; n < 257; n++)
        {
            Assert.True(queue.TryEnqueue(Event(n)));
            AssertActive(queue, 1);
            AssertItem(queue, Event(n));
            AssertActive(queue, 0);
        }
        AssertNoItem(queue, PacketSummaryDequeueResult.Empty);
    }

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(65, 0, 0)]
    [InlineData(1, 2, 0)] [InlineData(1, 0, -1)]
    public void Given_QueuedValidEvent_When_AppendingInvalidShape_Then_FaultsAndClearsRemainder(int flow, int direction, long elapsed)
    {
        var queue = new PacketSummaryEventQueue(2);
        Assert.True(queue.TryEnqueue(Event(0)));
        Assert.False(queue.TryEnqueue(new PacketSummaryEvent(flow, (PacketDirection)direction, elapsed, TcpControlFlags.Ack)));
        AssertTerminal(queue, "packet_event_invalid");
    }

    [Fact]
    public void Given_FullQueue_When_AppendingInvalidEvent_Then_EncodingFailurePrecedesCapacityFailure()
    {
        var queue = new PacketSummaryEventQueue(1);
        Assert.True(queue.TryEnqueue(Event(0)));
        Assert.False(queue.TryEnqueue(default));
        AssertTerminal(queue, "packet_event_invalid");
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void Given_SealedQueue_When_AppendingValidOrInvalidEvent_Then_StateFailurePrecedesSyntaxAndClears(bool populated, bool invalid)
    {
        var queue = new PacketSummaryEventQueue(2);
        if (populated) Assert.True(queue.TryEnqueue(Event(0)));
        Assert.True(queue.TryCompleteAdding());
        Assert.Equal(populated ? PacketSummaryQueueState.Draining : PacketSummaryQueueState.Completed, queue.State);
        Assert.False(queue.TryEnqueue(invalid ? default : Event(1)));
        AssertTerminal(queue, "packet_queue_state_invalid");
    }

    [Fact]
    public void Given_EmptyQueue_When_SealingRepeatedly_Then_CompletesAndReturnsNoItem()
    {
        var queue = new PacketSummaryEventQueue();
        Assert.True(queue.TryCompleteAdding());
        Assert.True(queue.TryCompleteAdding());
        AssertCompleted(queue);
        AssertNoItem(queue, PacketSummaryDequeueResult.Completed);
        AssertCompleted(queue);
    }

    [Fact]
    public void Given_TwoQueuedEvents_When_SealingAndDraining_Then_LastItemIsReturnedAsQueueCompletes()
    {
        var queue = new PacketSummaryEventQueue(2);
        Assert.True(queue.TryEnqueue(Event(0)));
        Assert.True(queue.TryEnqueue(Event(1)));
        Assert.True(queue.TryCompleteAdding());
        Assert.Equal(PacketSummaryQueueState.Draining, queue.State);
        Assert.Equal(2, queue.Count);
        Assert.True(queue.TryCompleteAdding());
        Assert.Equal(PacketSummaryQueueState.Draining, queue.State);
        Assert.Equal(2, queue.Count);
        AssertItem(queue, Event(0));
        Assert.Equal(PacketSummaryQueueState.Draining, queue.State);
        Assert.Equal(1, queue.Count);
        Assert.Null(queue.FailureCode);
        AssertItem(queue, Event(1));
        AssertCompleted(queue);
        Assert.True(queue.TryCompleteAdding());
        AssertNoItem(queue, PacketSummaryDequeueResult.Completed);
        AssertCompleted(queue);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public void Given_UnfinishedQueue_When_Aborting_Then_ClearsAndPreservesAbandonment(bool populated, bool seal)
    {
        var queue = new PacketSummaryEventQueue(2);
        if (populated) Assert.True(queue.TryEnqueue(Event(0)));
        if (seal) Assert.True(queue.TryCompleteAdding());
        queue.Abort();
        AssertTerminal(queue, "packet_queue_abandoned");
    }

    [Fact]
    public void Given_CompletedQueue_When_AbortingRepeatedly_Then_PreservesCompletionWithoutFailure()
    {
        var queue = new PacketSummaryEventQueue();
        Assert.True(queue.TryCompleteAdding());
        queue.Abort();
        queue.Abort();
        AssertCompleted(queue);
        AssertNoItem(queue, PacketSummaryDequeueResult.Completed);
    }

    [Fact]
    public void Given_ExistingEncodingFailure_When_EnqueueingSealingOrAborting_Then_PreservesFirstFailure()
    {
        var queue = new PacketSummaryEventQueue(1);
        Assert.False(queue.TryEnqueue(default));
        AssertTerminal(queue, "packet_event_invalid");
    }

    [Theory]
    [InlineData(1, 0, 0, 0)] [InlineData(1, 1, 1, 16)]
    [InlineData(64, 1, 0, 0)] [InlineData(64, 0, long.MaxValue, 255)]
    public void Given_ValidDerivedValues_When_QueueingAndReencoding_Then_PreservesStructAndExactBytes(int flow, int direction, long elapsed, int flags)
    {
        var expected = new PacketSummaryEvent(flow, (PacketDirection)direction, elapsed, (TcpControlFlags)flags);
        Assert.True(PacketSummaryEncoding.TryEncodeEvent(expected, out var before, out var beforeFailure));
        Assert.Null(beforeFailure);
        Assert.InRange(before.Length, 1, PacketSummaryBudget.MaximumEventBytes);
        var queue = new PacketSummaryEventQueue(1);
        Assert.True(queue.TryEnqueue(expected));
        Assert.Equal(PacketSummaryDequeueResult.Item, queue.TryDequeue(out var actual));
        Assert.Equal(expected, actual);
        AssertActive(queue, 0);
        Assert.True(PacketSummaryEncoding.TryEncodeEvent(actual, out var after, out var afterFailure));
        Assert.Null(afterFailure);
        Assert.Equal(before.ToArray(), after.ToArray());
    }

    [Fact]
    public void Given_SealedSyntheticEvents_When_DrainingIntoRealWriter_Then_CompletesSeparatelyAndPreservesExactSchema()
    {
        var started = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
        var identity = new PacketSummaryIdentity("capture-queue-334", new string('a', 40), "C0", 1, started);
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, identity);
        var queue = new PacketSummaryEventQueue();
        Assert.True(queue.TryEnqueue(new PacketSummaryEvent(1, PacketDirection.Outbound, 0, TcpControlFlags.Syn)));
        Assert.True(queue.TryEnqueue(new PacketSummaryEvent(1, PacketDirection.Inbound, 1, TcpControlFlags.Ack)));
        Assert.True(queue.TryCompleteAdding());
        for (int n = 0; n < 2; n++)
        {
            Assert.Equal(PacketSummaryDequeueResult.Item, queue.TryDequeue(out var value));
            Assert.True(writer.TryAppend(value));
        }
        AssertNoItem(queue, PacketSummaryDequeueResult.Completed);
        AssertCompleted(queue);
        Assert.Equal(PacketSummaryWriterState.Active, writer.State);
        Assert.Equal(2, writer.EventCount);
        Assert.True(writer.TryComplete(started.AddSeconds(1)));
        Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
        Assert.Null(writer.FailureCode);
        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        JsonElement root = document.RootElement;
        Assert.Equal(ExpectedEnvelopeKeys, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal(identity.RunId, root.GetProperty("run_id").GetString());
        Assert.Equal(identity.SourceCommit, root.GetProperty("source_commit").GetString());
        Assert.Equal("C0", root.GetProperty("configuration_id").GetString());
        Assert.Equal(1, root.GetProperty("repeat").GetInt32());
        Assert.Equal(started.ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_started_utc").GetString());
        Assert.Equal(started.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_ended_utc").GetString());
        JsonElement events = root.GetProperty("events");
        Assert.Equal(2, events.GetArrayLength());
        for (int n = 0; n < 2; n++)
        {
            JsonElement value = events[n];
            Assert.Equal(ExpectedEventKeys, value.EnumerateObject().Select(p => p.Name));
            Assert.Equal(1, value.GetProperty("flow").GetInt32());
            Assert.Equal(n == 0 ? "outbound" : "inbound", value.GetProperty("direction").GetString());
            Assert.Equal((long)n, value.GetProperty("elapsed_ns").GetInt64());
            Assert.Equal(n == 0 ? ExpectedSynFlags : ExpectedAckFlags,
                value.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
        }
    }

    [Fact]
    public async Task Given_CapacityOne_When_TasksExchangeEightEvents_Then_PreservesOrderAndCompletes()
    {
        var queue = new PacketSummaryEventQueue(1);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var producerPermit = new SemaphoreSlim(1, 1);
        using var consumerPermit = new SemaphoreSlim(0, 1);
        CancellationToken token = cancellation.Token;
        Task producer = Task.Run(async () =>
        {
            try
            {
                for (int n = 0; n < 8; n++)
                {
                    await producerPermit.WaitAsync(token);
                    Assert.True(queue.TryEnqueue(Event(n)));
                    consumerPermit.Release();
                }
                Assert.True(queue.TryCompleteAdding());
            }
            catch
            {
                await cancellation.CancelAsync();
                throw;
            }
        }, token);
        Task consumer = Task.Run(async () =>
        {
            try
            {
                for (int n = 0; n < 8; n++)
                {
                    await consumerPermit.WaitAsync(token);
                    AssertItem(queue, Event(n));
                    producerPermit.Release();
                }
            }
            catch
            {
                await cancellation.CancelAsync();
                throw;
            }
        }, token);
        Task both = Task.WhenAll(producer, consumer);
        try
        {
            await both.WaitAsync(token);
        }
        finally
        {
            await cancellation.CancelAsync();
            await both;
        }
        AssertCompleted(queue);
        AssertNoItem(queue, PacketSummaryDequeueResult.Completed);
    }

    private static PacketSummaryEvent Event(int n) => new(1, PacketDirection.Outbound, n, TcpControlFlags.Ack);

    private static void AssertItem(PacketSummaryEventQueue queue, PacketSummaryEvent expected)
    {
        Assert.Equal(PacketSummaryDequeueResult.Item, queue.TryDequeue(out var actual));
        Assert.Equal(expected, actual);
    }

    private static void AssertNoItem(PacketSummaryEventQueue queue, PacketSummaryDequeueResult expected)
    {
        var value = Event(42);
        Assert.Equal(expected, queue.TryDequeue(out value));
        Assert.Equal(default, value);
    }

    private static void AssertActive(PacketSummaryEventQueue queue, int count)
    {
        Assert.Equal(PacketSummaryQueueState.Active, queue.State);
        Assert.Equal(count, queue.Count);
        Assert.Null(queue.FailureCode);
    }

    private static void AssertCompleted(PacketSummaryEventQueue queue)
    {
        Assert.Equal(PacketSummaryQueueState.Completed, queue.State);
        Assert.Equal(0, queue.Count);
        Assert.Null(queue.FailureCode);
    }

    private static void AssertTerminal(PacketSummaryEventQueue queue, string failure)
    {
        Assert.Equal(PacketSummaryQueueState.Faulted, queue.State);
        Assert.Equal(0, queue.Count);
        Assert.Equal(failure, queue.FailureCode);
        AssertNoItem(queue, PacketSummaryDequeueResult.Faulted);
        Assert.False(queue.TryEnqueue(Event(1)));
        Assert.False(queue.TryEnqueue(default));
        Assert.False(queue.TryCompleteAdding());
        queue.Abort();
        queue.Abort();
        Assert.Equal(PacketSummaryQueueState.Faulted, queue.State);
        Assert.Equal(0, queue.Count);
        Assert.Equal(failure, queue.FailureCode);
        AssertNoItem(queue, PacketSummaryDequeueResult.Faulted);
    }
}
