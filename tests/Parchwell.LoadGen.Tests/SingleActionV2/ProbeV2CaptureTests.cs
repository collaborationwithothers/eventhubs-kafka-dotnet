using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2CaptureTests
{
    [Theory]
    [InlineData("record",119999,"success")][InlineData("record",120000,"success")][InlineData("record",120001,"no_outcome")]
    [InlineData("fatal",119999,"error")][InlineData("fatal",120000,"error")][InlineData("fatal",120001,"no_outcome")]
    [InlineData("nonfatal",119999,null)][InlineData("nonfatal",120000,null)][InlineData("nonfatal",120001,"no_outcome")]
    public void Given_FinalPollCrossesDeadline_When_CapturingSilence_Then_OnlyInWindowObservationsDisqualifyIt(string observation,long elapsed,string? outcome)
    {
        var clock = new TestClock { Milliseconds = elapsed };
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(),clock,0,120000);
        var request = new ProbeV2Requested("document-state","explicit",0,41,"aw==","dg==");
        if (observation == "record")
            capture.Read(new ConsumeResult<byte[],byte[]> { Topic = "document-state",Partition = 0,Offset = 41,Message = new() { Key = [107],Value = [118] } },request);
        else capture.Error(new Error(observation == "fatal" ? ErrorCode.Local_Fail : ErrorCode.Local_Transport,"synthetic",observation == "fatal"));
        clock.Milliseconds = Math.Max(elapsed,120000);
        capture.NoOutcome();
        var result = capture.Snapshot(1,"pod-r1",ProbeV2Stage.For("F06","C10","action"),new Dictionary<string,string>(),[],null,request);
        Assert.Equal(outcome,result.Result);
        Assert.Equal(outcome is not null,result.Terminal);
        if (observation == "record") Assert.Equal(elapsed,result.ReadObservation.Record!.ObservedElapsedMs);
        else Assert.Equal(elapsed,Assert.Single(result.ClientErrors).ObservedElapsedMs);
        Assert.False(capture.Limit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eA==")]
    public void Given_PresentRecordValue_When_CapturedAndSerialized_Then_NullEmptyAndBytesRemainDistinct(string? encoded)
    {
        var clock = new TestClock();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), clock, 0, 120000);
        var request = new ProbeV2Requested("document-state", "explicit", 0, 41, "aw==", null);
        capture.Read(new ConsumeResult<byte[], byte[]>
        {
            Topic = "document-state", Partition = 0, Offset = 41,
            Message = new() { Key = Encoding.UTF8.GetBytes("k"), Value = encoded is null ? null! : Convert.FromBase64String(encoded) }
        }, request);
        ProbeV2Repeat result = capture.Snapshot(1, "pod-r1", ProbeV2Stage.For("E01", "C1", "read"), new Dictionary<string, string>(), [], null, request);
        Assert.Equal("success", result.Result);
        Assert.Equal(encoded, result.ReadObservation.Record!.ValueBase64);
        byte[] bytes = ProbeV2Wire.Encode(result, 524288);
        Assert.Equal(encoded, ProbeV2Wire.Read<ProbeV2Repeat>(bytes, 524288).ReadObservation.Record!.ValueBase64);
    }

    [Fact]
    public void Given_WrongOffsetThenExactOffsetWithWrongValue_When_Reading_Then_CapturesExactLocationWithoutJudgingValue()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 120000);
        var requested = new ProbeV2Requested("document-events", "explicit", 30, 9, "aw==", "dg==");
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = 30, Offset = 8, Message = new() { Key = [107], Value = [120] } };
        capture.Read(record, requested);
        Assert.False(capture.Terminal);
        record.Offset = 9;
        capture.Read(record, requested);
        ProbeV2Repeat result = capture.Snapshot(1, "pod-r1", ProbeV2Stage.For("B08", "C0", "read"), new Dictionary<string, string>(), [], null, requested);
        Assert.Equal(2, result.ReadObservation.RecordsSeen);
        Assert.Equal("eA==", result.ReadObservation.Record!.ValueBase64);
        Assert.Equal(9, result.ReadObservation.Record.Offset);
    }

    [Theory]
    [InlineData(119999, false)]
    [InlineData(120000, true)]
    [InlineData(120001, true)]
    public void Given_ReadWindowBoundary_When_ObservingSilence_Then_RequiresFullWindow(long elapsed, bool terminal)
    {
        var clock = new TestClock { Milliseconds = elapsed };
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), clock, 0, 120000);
        capture.NoOutcome();
        Assert.Equal(terminal, capture.Terminal);
    }

    [Fact]
    public void Given_NonfatalCallback_When_WindowExpires_Then_SilenceIsNotInvented()
    {
        var clock = new TestClock();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), clock, 0, 120000);
        capture.Error(new Error(ErrorCode.Local_Transport));
        clock.Milliseconds = 120000;
        capture.NoOutcome();
        Assert.False(capture.Terminal);
        Assert.True(capture.HasErrors);
    }

    [Fact]
    public void Given_FirstPostTerminalStatistics_When_MoreReportsArrive_Then_RetainsOnlyFirstWithoutOverflow()
    {
        var clock = new TestClock();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), clock, 0, 330000);
        capture.Statistics("before");
        capture.Delivery(new() { Topic = "document-events", Partition = 30, Offset = 9, Status = PersistenceStatus.Persisted, Error = new Error(ErrorCode.NoError) });
        capture.Statistics("first");
        capture.Statistics("second");
        var result = capture.Snapshot(1, "pod-r1", ProbeV2Stage.For("B08", "C0", "action"), new Dictionary<string, string>(), [], null,
            new("document-events", "automatic", null, null, "aw==", "dg=="));
        Assert.Equal("first", Encoding.UTF8.GetString(Convert.FromBase64String(Assert.Single(result.Statistics).RawBase64)));
        Assert.False(capture.Limit);
    }

    [Theory]
    [InlineData(ErrorCode.Local_Transport, -195, "local")]
    [InlineData(ErrorCode.Unknown, -1, "broker")]
    [InlineData(ErrorCode.TopicAuthorizationFailed, 29, "broker")]
    public void Given_DeliveryErrorCode_When_Captured_Then_OriginFollowsLocalErrorFlag(ErrorCode code, int expected, string origin)
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000);
        capture.Delivery(new() { Topic = "document-state", Partition = Partition.Any, Offset = Offset.Unset,
            Status = PersistenceStatus.NotPersisted, Error = new Error(code) });
        ProbeV2Repeat result = Snapshot(capture);
        Assert.Equal("error", result.Result);
        Assert.Equal(expected, result.Error!.Code);
        Assert.Equal(origin, result.Error.Origin);
    }

    [Fact]
    public void Given_UnknownCodeDuringConstruction_When_Captured_Then_ConstructionOriginTakesPrecedence()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000);
        capture.Exception(new Error(ErrorCode.Unknown), true);
        ProbeV2Repeat result = Snapshot(capture);
        Assert.Equal("error", result.Result);
        Assert.Equal(-1, result.Error!.Code);
        Assert.Equal("construction", result.Error.Origin);
    }

    [Fact]
    public void Given_ErrorDeliveryWithUnsetLocation_When_Captured_Then_ErrorRetainedAndNoLocationInvented()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000);
        capture.Delivery(new() { Topic = "document-state", Partition = Partition.Any, Offset = Offset.Unset,
            Status = PersistenceStatus.NotPersisted, Error = new Error(ErrorCode.TopicAuthorizationFailed) });
        ProbeV2Repeat result = Snapshot(capture);
        Assert.Equal("error", result.Result);
        Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, result.Error!.Code);
        Assert.Null(result.Delivery);
    }

    [Fact]
    public void Given_FatalCallbackThenAcknowledgedDelivery_When_Captured_Then_FirstTerminalWinsAndLocationSurvives()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000);
        capture.Error(new Error(ErrorCode.Local_Fail, "synthetic", true));
        capture.Delivery(new() { Topic = "document-state", Partition = 0, Offset = 4, Status = PersistenceStatus.Persisted, Error = new Error(ErrorCode.NoError) });
        ProbeV2Repeat result = Snapshot(capture);
        Assert.Equal("error", result.Result);
        Assert.True(result.Error!.IsFatal);
        Assert.Equal(4, result.Delivery!.Offset);
        Assert.True(result.Error.Sequence < result.Delivery.Sequence);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void Given_ErrorBudgetBoundary_When_CallbacksArrive_Then_OverflowIsIncomplete(int count, bool limited)
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000);
        for (int i = 0; i < count; i++) capture.Error(new Error(ErrorCode.Local_Transport));
        Assert.Equal(limited, capture.Limit);
        Assert.Equal(Math.Min(count, 8), Snapshot(capture).ClientErrors.Count);
    }

    [Fact]
    public void Given_DuplicateOrMissingOrUnknownWireKey_When_Reading_Then_Rejects()
    {
        byte[] bytes = ProbeV2Wire.Encode(ProbeV2Wire.Skipped(1, "assign_consume", "not_applicable_no_delivery"), 524288);
        string json = Encoding.UTF8.GetString(bytes);
        Assert.Throws<JsonException>(() => ProbeV2Wire.Read<ProbeV2Repeat>(Encoding.UTF8.GetBytes(json.Replace("\"repeat\":1", "\"repeat\":1,\"repeat\":1", StringComparison.Ordinal)), 524288));
        Assert.Throws<JsonException>(() => ProbeV2Wire.Read<ProbeV2Repeat>(Encoding.UTF8.GetBytes(json.Replace("\"repeat\":1,", "", StringComparison.Ordinal)), 524288));
        Assert.Throws<JsonException>(() => ProbeV2Wire.Read<ProbeV2Repeat>(Encoding.UTF8.GetBytes(json.Replace("\"repeat\":1", "\"repeat\":1,\"extra\":1", StringComparison.Ordinal)), 524288));
    }

    [Fact]
    public void Given_SharedDiagnosticBudget_When_LaterRepeatOverflows_Then_TotalRemainsBoundedAndGapRetained()
    {
        var budget = new ProbeV2DiagnosticBudget(100);
        var first = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000, budget);
        for (int index = 0; index < 100; index++) first.Diagnostic("synthetic-" + index);
        ProbeV2Repeat firstSnapshot = Snapshot(first);
        var second = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000, budget);
        second.Diagnostic("overflow");
        ProbeV2Repeat secondSnapshot = Snapshot(second);
        Assert.Equal(100, firstSnapshot.Diagnostics.Count + secondSnapshot.Diagnostics.Count);
        Assert.Contains("capture_limit", secondSnapshot.Diagnostics);
        Assert.True(second.Limit);
    }

    [Fact]
    public void Given_ClientLogsBeyondBudget_When_Sealed_Then_BytesStayBoundedAndScientificCaptureIsUnaffected()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000, logContext: "scenario_id=B08 run_id=run-1 repeat=1 ");
        for (int index = 0; index < 20; index++) capture.Log(new("native", SyslogLevel.Error, "synthetic", new string('x', 1024)));
        Assert.True(capture.Logs().Sum(line => Encoding.UTF8.GetByteCount(line) + 1) <= 16384);
        Assert.Contains(capture.Logs(), line => line.StartsWith("probe_v2_client_log_capped", StringComparison.Ordinal));
        Assert.False(capture.Limit);
        Assert.Empty(Snapshot(capture).Diagnostics);
    }

    private static ProbeV2Repeat Snapshot(ProbeV2Capture capture) => capture.Snapshot(1, "pod-r1", ProbeV2Stage.For("F06", "C2", "action"),
        new Dictionary<string, string>(), [], null, new("document-state", "explicit", 0, null, "aw==", "dg=="));
}

internal sealed class TestClock : TimeProvider
{
    internal List<TimerStub> Timers { get; } = [];
    internal long Milliseconds;
    internal Action? OnDelay;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => Milliseconds;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Milliseconds);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime > TimeSpan.Zero && dueTime <= TimeSpan.FromMilliseconds(100))
        {
            Milliseconds += (long)dueTime.TotalMilliseconds;
            Action? action = OnDelay; OnDelay = null; action?.Invoke();
            callback(state);
        }
        var timer = new TimerStub(dueTime);
        Timers.Add(timer);
        return timer;
    }
    internal sealed class TimerStub(TimeSpan initialDueTime) : ITimer
    {
        internal TimeSpan InitialDueTime { get; } = initialDueTime;
        internal List<TimeSpan> ChangedDueTimes { get; } = [];
        internal bool IsDisposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period) { ChangedDueTimes.Add(dueTime); return true; }
        public void Dispose() => IsDisposed = true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
