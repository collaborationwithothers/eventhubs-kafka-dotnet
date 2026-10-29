#pragma warning disable CA1707
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.SeedF04;
using Xunit;

namespace Parchwell.Enrichment.Tests.SeedF04;

public sealed class SeedF04Tests
{
    private static readonly string[] ClientCleanupOrder = ["produce", "flush", "dispose"];
    private static readonly string[] ReceiptFields = ["schema_version", "mode", "run_id", "source_commit", "job_name", "pod_name", "pod_uid", "service_account", "topic", "partition", "key_base64", "value_sha256", "delivered", "error", "handler_exit_code", "cleanup_completed"];
    [Fact]
    public async Task Given_valid_seed_When_executed_Then_one_exact_marker_is_delivered_and_cleaned()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        var recorded = (RecordingProducer)(object)producer;
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.True(result.Delivered);
        Assert.Null(result.Error);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(1, recorded.ProduceCount);
        Assert.Equal("document-state", recorded.TopicPartition!.Topic);
        Assert.Equal(0, recorded.TopicPartition.Partition.Value);
        Assert.Equal("f04-seed:synthetic-run", Encoding.UTF8.GetString(recorded.Message!.Key));
        Assert.Equal(Enumerable.Repeat((byte)'s', 1000), recorded.Message.Value);
        Assert.Equal("68fe9c9f499dd1a6bf07c6f737c2b6a8118cca37877336ee0f2674209554d3c6", Convert.ToHexStringLower(SHA256.HashData(recorded.Message.Value)));
        Assert.Equal(ClientCleanupOrder, recorded.Events);
    }

    [Theory]
    [InlineData(ErrorCode.TopicAuthorizationFailed, "broker")]
    [InlineData(ErrorCode.Local_MsgTimedOut, "local")]
    [InlineData(ErrorCode.Unknown, "broker")]
    public async Task Given_delivery_failure_When_executed_Then_measured_error_is_retained(ErrorCode code, string origin)
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        ((RecordingProducer)(object)producer).DeliveryError = new Error(code);
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.Delivered);
        Assert.Equal((int)code, result.Error!.Code);
        Assert.Equal(origin, result.Error.Origin);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Given_dispose_failure_When_executed_Then_cleanup_does_not_succeed()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        ((RecordingProducer)(object)producer).FailDispose = true;
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.CleanupCompleted);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Given_flush_failure_When_executed_Then_disposal_is_attempted_and_cleanup_fails()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        var recorded = (RecordingProducer)(object)producer;
        recorded.FailFlush = true;
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.CleanupCompleted);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(ClientCleanupOrder, recorded.Events);
    }

    [Fact]
    public async Task Given_native_construction_failure_When_executed_Then_actual_error_has_construction_origin()
    {
        var worker = new SeedF04Worker(Options.Create(ValidOptions()),
            () => throw new KafkaException(new Error(ErrorCode.Local_InvalidArg)), TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.Delivered);
        Assert.Equal(1, result.ExitCode);
        Assert.True(result.CleanupCompleted);
        Assert.Equal((int)ErrorCode.Local_InvalidArg, result.Error!.Code);
        Assert.Equal("construction", result.Error.Origin);
    }

    [Fact]
    public async Task Given_code_less_construction_failure_When_executed_Then_no_native_error_is_invented()
    {
        var worker = new SeedF04Worker(Options.Create(ValidOptions()),
            () => throw new ArgumentException("synthetic_invalid_configuration"), TimeProvider.System);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.Delivered);
        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Given_action_deadline_without_delivery_When_executed_Then_one_produce_does_not_become_success()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        var recorded = (RecordingProducer)(object)producer;
        recorded.SuppressDelivery = true;
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, new ExpiredActionClock());
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.False(result.Delivered);
        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.Error);
        Assert.Equal(1, recorded.ProduceCount);
        Assert.True(result.CleanupCompleted);
    }

    [Theory]
    [InlineData(329999, true)]
    [InlineData(330000, true)]
    [InlineData(330001, false)]
    public async Task Given_delivery_at_monotonic_window_boundary_When_timer_is_delayed_Then_only_inclusive_window_succeeds(int elapsed, bool delivered)
    {
        var clock = new MeasuredCallbackClock();
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
        var recorded = (RecordingProducer)(object)producer;
        recorded.BeforeDelivery = () => clock.Milliseconds = elapsed;
        var worker = new SeedF04Worker(Options.Create(ValidOptions()), () => producer, clock);
        SeedF04Result result = await worker.ExecuteAsync(CancellationToken.None);
        Assert.Equal(delivered, result.Delivered);
        Assert.Equal(delivered ? 0 : 1, result.ExitCode);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(1, recorded.ProduceCount);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(4095, true)]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void Given_native_error_reason_size_When_receipt_is_encoded_Then_fixed_utf8_bound_is_enforced(int reasonBytes, bool valid)
    {
        var result = new SeedF04Result(false, new SeedF04Error(29, false, "broker", new string('s', reasonBytes)), 1, true);
        if (valid) Assert.True(Encoding.UTF8.GetByteCount(SeedF04Receipt.CreateLine(ValidOptions(), result)) <= 65536);
        else Assert.Throws<InvalidOperationException>(() => SeedF04Receipt.CreateLine(ValidOptions(), result));
    }

    [Fact]
    public void Given_successful_result_When_receipt_is_encoded_Then_exact_closed_bounded_contract_is_written()
    {
        string line = SeedF04Receipt.CreateLine(ValidOptions(), new SeedF04Result(true, null, 0, true));
        Assert.StartsWith("PARCHWELL_F04_SEED_RECEIPT ", line);
        Assert.True(Encoding.UTF8.GetByteCount(line) <= 65536);
        using JsonDocument document = JsonDocument.Parse(line[SeedF04Receipt.Prefix.Length..]);
        JsonElement root = document.RootElement;
        Assert.Equal(ReceiptFields, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("single-action-seed", root.GetProperty("mode").GetString());
        Assert.Equal("enrichment-consumer", root.GetProperty("service_account").GetString());
        Assert.Equal(0, root.GetProperty("handler_exit_code").GetInt32());
        Assert.True(root.GetProperty("cleanup_completed").GetBoolean());
    }

    [Fact]
    public void Given_unclean_result_When_receipt_is_requested_Then_success_is_suppressed() =>
        Assert.Throws<InvalidOperationException>(() => SeedF04Receipt.CreateLine(ValidOptions(), new SeedF04Result(true, null, 0, false)));

    internal static SeedF04Options ValidOptions() => new()
    {
        RunId = "synthetic-run", SourceCommit = new string('a', 40), JobName = "synthetic-run-f04-seed",
        PodName = "synthetic-run-f04-seed-abcde", PodUid = "11111111-2222-3333-4444-555555555555",
        NamespaceName = "synthetic-eh", ActionWindowMs = 330000, ProcessLifetimeSeconds = 390,
        CleanupSeconds = 10, HostCleanupSeconds = 10, MaxReceiptBytes = 65536
    };

    private sealed class ExpiredActionClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            TimeProvider.System.CreateTimer(callback, state, dueTime == TimeSpan.FromMilliseconds(330000) ? TimeSpan.Zero : dueTime, period);
    }

    private sealed class MeasuredCallbackClock : TimeProvider
    {
        internal long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }

    public class RecordingProducer : DispatchProxy
    {
        public int ProduceCount { get; private set; }
        public TopicPartition? TopicPartition { get; private set; }
        public Message<byte[], byte[]>? Message { get; private set; }
        public List<string> Events { get; } = [];
        public Error DeliveryError { get; set; } = new(ErrorCode.NoError);
        public bool FailDispose { get; set; }
        public bool FailFlush { get; set; }
        public bool SuppressDelivery { get; set; }
        public Action? BeforeDelivery { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "Produce":
                    ProduceCount++;
                    Events.Add("produce");
                    TopicPartition = (TopicPartition)args![0]!;
                    Message = (Message<byte[], byte[]>)args[1]!;
                    BeforeDelivery?.Invoke();
                    if (!SuppressDelivery)
                        ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]> { Error = DeliveryError });
                    return null;
                case "Flush":
                    Events.Add("flush");
                    if (FailFlush) throw new InvalidOperationException("synthetic_flush_failure");
                    return null;
                case "Dispose":
                    Events.Add("dispose");
                    if (FailDispose) throw new InvalidOperationException("synthetic_dispose_failure");
                    return null;
                default: throw new InvalidOperationException(targetMethod.Name);
            }
        }
    }
}
#pragma warning restore CA1707
