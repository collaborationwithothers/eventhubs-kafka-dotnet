#pragma warning disable CA1707
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerErrorRowTests
{
    [Fact]
    public async Task Non_fatal_consume_error()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { RecordsConsumerErrors = true };
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Transport, "synthetic transport", false)));
        Assert.Equal(0, await harness.RunAsync());
        JsonElement row = Assert.Single(ErrorRows(harness));
        Assert.Equal("consume", row.GetProperty("source").GetString());
        Assert.Equal(-195, row.GetProperty("error_code").GetInt32());
        Assert.False(row.GetProperty("is_fatal").GetBoolean());
        Assert.Equal("synthetic transport", row.GetProperty("reason").GetString());
        Assert.NotNull(harness.Store.Completion);
    }

    [Fact]
    public async Task Fatal_consume_error()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { RecordsConsumerErrors = true };
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail, "synthetic fatal", true)));
        Assert.Equal(1, await harness.RunAsync());
        JsonElement row = Assert.Single(ErrorRows(harness));
        Assert.True(row.GetProperty("is_fatal").GetBoolean());
        Assert.StartsWith("consumer_processing_failed reason=consumer_fatal_error ", harness.Error.ToString(), StringComparison.Ordinal);
        Assert.Null(harness.Store.Completion);
    }

    [Fact]
    public async Task Given_failing_error_row_append_When_fatal_consume_error_Then_append_failure_is_reported()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { RecordsConsumerErrors = true };
        harness.Store.FailAppend = row => row.GetProperty("kind").GetString() == "consumer_error";
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Fail, "synthetic fatal", true)));
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_record_append_failed ", harness.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Error_handler_row()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { RecordsConsumerErrors = true };
        harness.Recorded.Polls.Enqueue(() => { harness.RaiseError(new Error(ErrorCode.Local_AllBrokersDown, "synthetic brokers down", false)); return null; });
        Assert.Equal(0, await harness.RunAsync());
        JsonElement row = Assert.Single(ErrorRows(harness));
        Assert.Equal("error_handler", row.GetProperty("source").GetString());
        Assert.Equal(-187, row.GetProperty("error_code").GetInt32());
    }

    [Fact]
    public async Task Given_failing_error_row_append_When_error_handler_called_Then_member_ends_with_append_failure()
    {
        using var harness = new ConsumerTestHarness();
        harness.Plan = harness.Plan with { RecordsConsumerErrors = true };
        harness.Store.FailAppend = row => row.GetProperty("kind").GetString() == "consumer_error";
        // Confluent.Kafka ignores an exception thrown by an error handler, so the callback's failure is swallowed here too.
        harness.Recorded.Polls.Enqueue(() =>
        {
            try { harness.RaiseError(new Error(ErrorCode.Local_AllBrokersDown, "synthetic", false)); }
            catch (InvalidOperationException) { }
            return null;
        });
        Assert.Equal(1, await harness.RunAsync());
        Assert.StartsWith("consumer_processing_failed reason=consumer_record_append_failed ", harness.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Off_by_default()
    {
        using var harness = new ConsumerTestHarness();
        harness.Recorded.Polls.Enqueue(() => throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.Local_Transport, "synthetic", false)));
        Assert.Equal(0, await harness.RunAsync());
        Assert.Empty(ErrorRows(harness));
        Assert.Null(harness.Handlers.ErrorReported);
    }

    [Fact]
    public void Factory_sets_error_handler_only_when_callback_set()
    {
        var without = new ProbeBuilder();
        ConsumerProcessingBuilderConfiguration.SetHandlers(without, Handlers(null), (_, _) => { });
        Assert.Null(without.ConfiguredErrorHandler);
        var with = new ProbeBuilder();
        ConsumerProcessingBuilderConfiguration.SetHandlers(with, Handlers((_, _) => { }), (_, _) => { });
        Assert.NotNull(with.ConfiguredErrorHandler);
    }

    private static ConsumerProcessingHandlers Handlers(Action<IConsumer<byte[], byte[]>, Error>? errorReported) =>
        new((_, _) => { }, (_, _) => { }, (_, _) => { }, (_, _) => { }, errorReported);

    private static JsonElement[] ErrorRows(ConsumerTestHarness harness) =>
        [.. harness.Store.Rows.Where(row => row.GetProperty("kind").GetString() == "consumer_error")];

    private sealed class ProbeBuilder() : ConsumerBuilder<byte[], byte[]>(new ConsumerConfig())
    {
        internal object? ConfiguredErrorHandler => ErrorHandler;
    }
}
#pragma warning restore CA1707
