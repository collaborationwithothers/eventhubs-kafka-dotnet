using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Common.SingleAction;
using Parchwell.LoadGen.SingleAction;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleAction;

#pragma warning disable CA1707
public sealed class SingleActionCaptureTests
{
    [Theory]
    [InlineData("B01", "C1", "B01:C1:run-1:r3")]
    [InlineData("F04", "C0", "f04-seed:run-1")]
    [InlineData("F04", "C1", "F04:C1:run-1:r3")]
    [InlineData("F04", "C3", "F04:C3:run-1:r3")]
    public void Given_ProduceDeclaration_When_CreatingKey_Then_OnlyF04ControlUsesSeedKey(string item, string configuration, string expected) =>
        Assert.Equal(expected, Encoding.UTF8.GetString(SingleActionWorker.ProduceKey(item, configuration, "run-1", 3)));

    [Theory]
    [InlineData(329999, true)]
    [InlineData(330000, true)]
    [InlineData(330001, false)]
    public async Task Given_DeliveryAtWindowBoundary_When_Capturing_Then_UsesInclusiveMonotonicWindow(int elapsed, bool terminal)
    {
        var fixture = new Fixture();
        fixture.Native.ActionElapsed = elapsed;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.Equal(terminal, repeat.Terminal);
        Assert.Equal(terminal ? "success" : null, repeat.Result);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
        Assert.Equal(terminal ? elapsed : null, repeat.TerminalUnixMs);
        Assert.Equal(terminal ? 20000 : 0, fixture.Clock.Milliseconds - elapsed);
    }

    [Theory]
    [InlineData(119999, true)]
    [InlineData(120000, true)]
    [InlineData(120001, false)]
    public async Task Given_AssignmentAtWindowBoundary_When_SubscriptionCaptures_Then_WindowIsInclusive(int elapsed, bool terminal)
    {
        var fixture = new Fixture();
        fixture.Native.ActionElapsed = elapsed;
        fixture.Native.AssignOnPoll = true;
        var repeat = await fixture.RunAsync(Action("subscribe", 120000));
        Assert.Equal(terminal, repeat.Terminal);
        Assert.Equal(1, fixture.Native.SubscribeCalls);
        Assert.Equal(0, fixture.Native.AssignCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_Transactions_When_Capturing_Then_OnlyInitializesOnceWithLiteralTimeout()
    {
        var fixture = new Fixture();
        var repeat = await fixture.RunAsync(Action("init_transactions", 150000));
        Assert.Equal("success", repeat.Result);
        Assert.Equal(1, fixture.Native.InitCalls);
        Assert.Equal(TimeSpan.FromMilliseconds(120000), fixture.Native.InitTimeout);
        Assert.Equal(0, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "error")]
    public async Task Given_NativeDelivery_When_Capturing_Then_PreservesMeasuredOutcomeWithoutRetry(bool fails, string expected)
    {
        var fixture = new Fixture();
        fixture.Native.Error = fails ? new Error(ErrorCode.TopicAuthorizationFailed) : new Error(ErrorCode.NoError);
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.Equal(expected, repeat.Result);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        if (fails)
        {
            Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, repeat.Error!.Code);
            Assert.Equal("broker", repeat.Error.Origin);
            Assert.False(repeat.Error.IsFatal);
        }
        else Assert.Null(repeat.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_ConstructionFailure_When_Capturing_Then_OnlyKafkaExceptionHasMeasuredErrorTuple(bool nativeCode)
    {
        var fixture = new Fixture();
        fixture.Native.ConstructionFailure = nativeCode ? new KafkaException(new Error(ErrorCode.Local_InvalidArg)) : new ArgumentException("synthetic");
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.Equal(nativeCode, repeat.Terminal);
        Assert.Equal(0, fixture.Native.ProduceCalls);
        Assert.Equal(0, fixture.Native.DisposeCalls);
        if (nativeCode)
        {
            Assert.Equal("error", repeat.Result);
            Assert.Equal("construction", repeat.Error!.Origin);
            Assert.Equal((int)ErrorCode.Local_InvalidArg, repeat.Error.Code);
        }
        else
        {
            Assert.Null(repeat.Result);
            Assert.Null(repeat.Error);
            Assert.Contains("construction:ArgumentException", repeat.Diagnostics);
        }
    }

    [Theory]
    [InlineData("produce_one")]
    [InlineData("init_transactions")]
    [InlineData("subscribe")]
    [InlineData("assign_consume")]
    public async Task Given_MeasuredActionException_When_Capturing_Then_RecordsActualCodeOriginAndFatality(string action)
    {
        var fixture = new Fixture();
        var error = new Error(ErrorCode.TopicAuthorizationFailed, "synthetic measured error", true);
        fixture.Native.ActionException = action switch
        {
            "produce_one" => new ProduceException<byte[], byte[]>(error, new()),
            "subscribe" or "assign_consume" => new ConsumeException(new ConsumeResult<byte[], byte[]>(), error),
            _ => new KafkaException(error)
        };
        var repeat = await fixture.RunAsync(Action(action, action == "produce_one" ? 330000 : 150000));
        Assert.True(repeat.Terminal);
        Assert.Equal("error", repeat.Result);
        Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, repeat.Error!.Code);
        Assert.Equal("broker", repeat.Error.Origin);
        Assert.True(repeat.Error.IsFatal);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_FatalConsumerCallback_When_Capturing_Then_RecordsItAsTerminalAndSeparatelyAsClientError()
    {
        var fixture = new Fixture();
        fixture.Native.FatalOnPoll = true;
        var repeat = await fixture.RunAsync(Action("subscribe", 120000));
        Assert.Equal("error", repeat.Result);
        Assert.True(repeat.Error!.IsFatal);
        Assert.Equal("local", repeat.Error.Origin);
        Assert.Equal(repeat.Error.Code, Assert.Single(repeat.ClientErrors).Code);
        Assert.Equal(1, fixture.Native.SubscribeCalls);
    }

    [Fact]
    public async Task Given_NoDelivery_When_WindowExpires_Then_NoTerminalOutcomeAndNoRetry()
    {
        var fixture = new Fixture();
        fixture.Native.NoDelivery = true;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.False(repeat.Terminal);
        Assert.Equal(330000, repeat.ElapsedMs);
        Assert.Contains("action_window_elapsed", repeat.Diagnostics);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_ErrorLimitBeforeDelivery_When_Capturing_Then_StopsWithoutWaitingForActionWindow()
    {
        var fixture = new Fixture();
        fixture.Native.NoDelivery = true;
        fixture.Native.ProduceErrors = 65;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.False(repeat.Terminal);
        Assert.Equal(0, repeat.ElapsedMs);
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(64, repeat.ClientErrors.Count);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_PostTerminalConsumeError_When_CollectingStatistics_Then_PreservesChosenSubscriptionSuccess()
    {
        var fixture = new Fixture();
        fixture.Native.AssignOnPoll = true;
        fixture.Native.PostTerminalConsumeError = true;
        var repeat = await fixture.RunAsync(Action("subscribe", 120000));
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, Assert.Single(repeat.ClientErrors).Code);
        Assert.Equal(20000, fixture.Clock.Milliseconds);
        Assert.Equal(1, fixture.Native.SubscribeCalls);
    }

    [Theory]
    [InlineData("C1", "statistics")]
    [InlineData("C1", "client_errors")]
    [InlineData("C3", "statistics")]
    [InlineData("C3", "client_errors")]
    public async Task Given_F04ProduceSuccessBeforeCallbackOverflow_When_Capturing_Then_PreservesMeasuredSuccessAndCaptureLimit(string configuration, string overflow)
    {
        var fixture = new Fixture();
        fixture.Options.ConfigurationId = configuration;
        fixture.Native.ActionElapsed = 7;
        fixture.Clock.OnDelay = () =>
        {
            for (int index = 0; index < 65; index++)
            {
                if (overflow == "statistics") fixture.Native.Capture.Statistics("{\"time\":1,\"ts\":1}");
                else fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
            }
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Null(repeat.Matched);
        Assert.Equal(7, repeat.ElapsedMs);
        Assert.Equal(7, repeat.TerminalUnixMs);
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(overflow == "statistics" ? 64 : 0, repeat.Statistics.Count);
        Assert.Equal(overflow == "client_errors" ? 64 : 0, repeat.ClientErrors.Count);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_F04MatchingConsumeSuccessBeforeStatisticsOverflow_When_Capturing_Then_PreservesExactMatchedEvidence()
    {
        var fixture = new Fixture();
        fixture.Native.Records.Enqueue(Record("document-state"));
        fixture.Native.PostTerminalStatisticsOverflow = true;
        var repeat = await fixture.RunAsync(Action("assign_consume", 120000, "document-state"));
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Equal("document-state", repeat.Matched!.Topic);
        Assert.Equal(0, repeat.Matched.Partition);
        Assert.Equal("f04-seed:run-1", Encoding.UTF8.GetString(Convert.FromBase64String(repeat.Matched.KeyBase64)));
        Assert.Equal(SingleActionWorker.Payload(1000), Convert.FromBase64String(repeat.Matched.ValueBase64));
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(64, repeat.Statistics.Count);
        Assert.Equal(1, fixture.Native.AssignCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_CallbackLimitAndDeliveryBeforeWorkerResumes_When_Capturing_Then_PreservesOnlyDeliveryObservedBeforeLimit(bool deliveryFirst)
    {
        var fixture = new Fixture();
        fixture.Native.ProduceErrors = deliveryFirst ? 0 : 65;
        fixture.Native.ErrorsAfterDelivery = deliveryFirst ? 65 : 0;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.Equal(deliveryFirst, repeat.Terminal);
        Assert.Equal(deliveryFirst ? "success" : null, repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Equal(deliveryFirst ? 0 : null, repeat.TerminalUnixMs);
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(64, repeat.ClientErrors.Count);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(true, "error")]
    [InlineData(false, "success")]
    public async Task Given_AssignmentAndFatalCallbacksInOnePoll_When_Capturing_Then_FirstTerminalCallbackWins(bool fatalFirst, string expected)
    {
        var fixture = new Fixture();
        fixture.Native.AssignOnPoll = true;
        fixture.Native.FatalOnPoll = true;
        fixture.Native.FatalBeforeAssignment = fatalFirst;
        var repeat = await fixture.RunAsync(Action("subscribe", 120000));
        Assert.True(repeat.Terminal);
        Assert.Equal(expected, repeat.Result);
        Assert.Equal(fatalFirst, repeat.Error is not null);
        Assert.Single(repeat.ClientErrors);
        Assert.Equal(1, fixture.Native.SubscribeCalls);
    }

    [Fact]
    public async Task Given_AssignmentBeforeConsumeExceptionInSamePoll_When_Capturing_Then_PreservesEarlierSuccess()
    {
        var fixture = new Fixture();
        fixture.Native.AssignOnPoll = true;
        fixture.Native.ThrowAfterFirstAssignment = true;
        var repeat = await fixture.RunAsync(Action("subscribe", 120000));
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Equal(1, fixture.Native.SubscribeCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Fact]
    public async Task Given_CaptureLimitBeforeMatchingRecordReturns_When_Capturing_Then_LeavesTerminalAndMatchedAbsent()
    {
        var fixture = new Fixture();
        fixture.Native.Records.Enqueue(Record("document-state"));
        fixture.Native.StatisticsOverflowOnFirstPoll = true;
        var repeat = await fixture.RunAsync(Action("assign_consume", 120000, "document-state"));
        Assert.False(repeat.Terminal);
        Assert.Null(repeat.Result);
        Assert.Null(repeat.Error);
        Assert.Null(repeat.Matched);
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(64, repeat.Statistics.Count);
        Assert.Equal(1, fixture.Native.AssignCalls);
    }

    [Fact]
    public async Task Given_4095UnrelatedRecordsThenMatch_When_Assigned_Then_SucceedsWithoutAdditionalAssignment()
    {
        var fixture = new Fixture();
        fixture.Native.Records = new Queue<ConsumeResult<byte[], byte[]>>(Enumerable.Repeat(Record("wrong"), 4095).Append(Record("document-state")));
        var repeat = await fixture.RunAsync(Action("assign_consume", 120000, "document-state"));
        Assert.Equal("success", repeat.Result);
        Assert.NotNull(repeat.Matched);
        Assert.Equal(1, fixture.Native.AssignCalls);
        Assert.Equal(0, fixture.Native.SubscribeCalls);
        Assert.Equal(new TopicPartitionOffset("document-state", new Partition(0), new Offset(0)), fixture.Native.Assignment);
    }

    [Fact]
    public async Task Given_4096UnrelatedRecords_When_Assigned_Then_CaptureLimitPrevents4097thPoll()
    {
        var fixture = new Fixture();
        fixture.Native.Records = new Queue<ConsumeResult<byte[], byte[]>>(Enumerable.Repeat(Record("wrong"), 4096).Append(Record("document-state")));
        var repeat = await fixture.RunAsync(Action("assign_consume", 120000, "document-state"));
        Assert.False(repeat.Terminal);
        Assert.Contains("capture_limit", repeat.Diagnostics);
        Assert.Equal(4096, fixture.Native.PollCalls);
        Assert.Single(fixture.Native.Records);
    }

    [Theory]
    [InlineData("eof")]
    [InlineData("topic")]
    [InlineData("partition")]
    [InlineData("key")]
    [InlineData("value")]
    [InlineData("empty")]
    public async Task Given_NoExactMarker_When_Assigned_Then_DoesNotSucceed(string mismatch)
    {
        var fixture = new Fixture();
        ConsumeResult<byte[], byte[]> record = Record("document-state");
        if (mismatch == "eof") record.IsPartitionEOF = true;
        if (mismatch == "topic") record.Topic = "document-events";
        if (mismatch == "partition") record.Partition = new Partition(1);
        if (mismatch == "key") record.Message.Key = Encoding.UTF8.GetBytes("wrong");
        if (mismatch == "value") record.Message.Value[999] = (byte)'x';
        fixture.Native.Records = new Queue<ConsumeResult<byte[], byte[]>>(mismatch == "empty" ? [] : [record]);
        var repeat = await fixture.RunAsync(Action("assign_consume", 120000, "document-state"));
        Assert.False(repeat.Terminal);
        Assert.Null(repeat.Matched);
    }

    [Theory]
    [InlineData("errors", 63, false)]
    [InlineData("errors", 64, false)]
    [InlineData("errors", 65, true)]
    [InlineData("reports", 63, false)]
    [InlineData("reports", 64, false)]
    [InlineData("reports", 65, true)]
    [InlineData("queue", 127, false)]
    [InlineData("queue", 128, false)]
    [InlineData("queue", 129, true)]
    [InlineData("reason", 4095, false)]
    [InlineData("reason", 4096, false)]
    [InlineData("reason", 4097, true)]
    [InlineData("report_bytes", 262143, false)]
    [InlineData("report_bytes", 262144, false)]
    [InlineData("report_bytes", 262145, true)]
    [InlineData("diagnostic_bytes", 2047, false)]
    [InlineData("diagnostic_bytes", 2048, false)]
    [InlineData("diagnostic_bytes", 2049, true)]
    public void Given_BoundedRawCallbacks_When_LimitIsCrossed_Then_MarksIncompleteWithoutTruncation(string kind, int count, bool limited)
    {
        var fixture = new Fixture();
        var capture = new SingleActionCapture(fixture.Options, fixture.Clock, 0, new(100));
        if (kind == "reason") capture.Error(new Error(ErrorCode.Local_Fail, new string('x', count)));
        else if (kind == "report_bytes") capture.Statistics(new string('x', count));
        else if (kind == "diagnostic_bytes") capture.Diagnostic(new string('x', count));
        else for (int index = 0; index < count; index++)
        {
            if (kind == "reports") capture.Statistics("{\"time\":1,\"ts\":2}");
            else if (kind == "errors") capture.Error(new Error(ErrorCode.Local_Fail));
            else capture.AssignedPartitions(1);
            if (kind != "queue") capture.Drain();
        }
        capture.Drain();
        Assert.Equal(limited, capture.Limit);
        var snapshot = capture.Snapshot();
        Assert.Equal(limited, snapshot.Diagnostics.Contains("capture_limit", StringComparer.Ordinal));
        Assert.True(snapshot.Errors.Length <= 64);
        Assert.True(snapshot.Statistics.Length <= 64);
    }

    [Fact]
    public async Task Given_NativeStatistics_When_Capturing_Then_PreservesExactUtf8AndNativeTimestampFields()
    {
        var fixture = new Fixture();
        const string raw = "{\"time\":7,\"ts\":123456,\"brokers\":{}}";
        fixture.Native.Report = raw;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        SingleActionStatistics statistics = Assert.Single(repeat.Statistics);
        Assert.Equal(raw, Encoding.UTF8.GetString(Convert.FromBase64String(statistics.RawBase64)));
        Assert.Equal(1, statistics.Sequence);
        Assert.Equal(0, statistics.ObservedElapsedMs);
    }

    [Fact]
    public void Given_StatisticsThatCannotEncodeAsUtf8_When_CallbackArrives_Then_MarksIncompleteWithoutReplacingBytes()
    {
        var fixture = new Fixture();
        var capture = new SingleActionCapture(fixture.Options, fixture.Clock, 0, new(100));
        capture.Statistics("\ud800");
        Assert.True(capture.Limit);
        Assert.Empty(capture.Snapshot().Statistics);
        Assert.Contains("capture_limit", capture.Snapshot().Diagnostics);
    }

    [Fact]
    public void Given_EncodedCapture_When_OverByteLimit_Then_ThrowsInsteadOfReturningPartialJson()
    {
        byte[] bytes = SingleActionWire.Encode(new { value = "synthetic" }, 100);
        Assert.Equal(bytes, SingleActionWire.Encode(new { value = "synthetic" }, bytes.Length));
        Assert.Equal(bytes, SingleActionWire.Encode(new { value = "synthetic" }, bytes.Length + 1));
        Assert.Throws<InvalidDataException>(() => SingleActionWire.Encode(new { value = "synthetic" }, bytes.Length - 1));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void Given_ConfigurationDiagnosticBudget_When_CrossedAcrossRepeats_Then_StaysBoundedAndMarksIncomplete(int count, bool limited)
    {
        var fixture = new Fixture();
        var budget = new SingleActionDiagnosticBudget(100);
        var first = new SingleActionCapture(fixture.Options, fixture.Clock, 0, budget);
        var second = new SingleActionCapture(fixture.Options, fixture.Clock, 0, budget);
        for (int index = 0; index < 50; index++) first.Diagnostic("first");
        var firstSnapshot = first.Snapshot();
        for (int index = 50; index < count; index++) second.Diagnostic("second");
        Assert.Equal(limited, second.Limit);
        var secondSnapshot = second.Snapshot();
        Assert.Equal(Math.Min(count, 100), firstSnapshot.Diagnostics.Count + secondSnapshot.Diagnostics.Count);
        Assert.Equal(limited, secondSnapshot.Diagnostics.Contains("capture_limit", StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(127, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    public async Task Given_SettingsKeyBoundary_When_Capturing_Then_OnlyOverLimitStopsClientCreation(int count, bool limited)
    {
        var fixture = new Fixture();
        var settings = Enumerable.Range(1, count - 1).ToImmutableDictionary(index => "key" + index,
            _ => new ResolvedSetting("value", null!, null), StringComparer.Ordinal);
        var repeat = await fixture.RunAsync(Action("produce_one", 330000), new(KafkaClientRole.LoadGenProducer, settings, []));
        Assert.Equal(limited, repeat.Diagnostics.Contains("capture_limit", StringComparer.Ordinal));
        Assert.Equal(limited ? 0 : 1, fixture.Native.ProduceCalls);
    }

    [Theory]
    [InlineData(16383, false)]
    [InlineData(16384, false)]
    [InlineData(16385, true)]
    public async Task Given_SettingsUtf8Boundary_When_Capturing_Then_OnlyOverLimitStopsClientCreation(int bytes, bool limited)
    {
        var fixture = new Fixture();
        int fixedBytes = Encoding.UTF8.GetByteCount("client.id") + Encoding.UTF8.GetByteCount("pod-1-r1") + Encoding.UTF8.GetByteCount("test");
        var settings = ImmutableDictionary<string, ResolvedSetting>.Empty.Add("test", new(new string('s', bytes - fixedBytes), null!, null));
        var repeat = await fixture.RunAsync(Action("produce_one", 330000), new(KafkaClientRole.LoadGenProducer, settings, []));
        Assert.Equal(limited, repeat.Diagnostics.Contains("capture_limit", StringComparer.Ordinal));
        Assert.Equal(limited ? 0 : 1, fixture.Native.ProduceCalls);
    }

    private const string FirstRule = "first_error_callback_or_delivery";
    private const string WindowRule = "window_end_no_outcome";

    [Fact]
    public async Task Given_FirstRuleCallbackDuringBuild_When_Capturing_Then_SkipsProduceAndKeepsCallbackOrigin()
    {
        var fixture = new Fixture();
        fixture.Native.OnBuild = () =>
        {
            fixture.Clock.Milliseconds = 7;
            fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.True(repeat.Terminal);
        Assert.Equal("error", repeat.Result);
        Assert.Equal(-196, repeat.Error!.Code);
        Assert.Equal("local", repeat.Error.Origin);
        Assert.False(repeat.Error.IsFatal);
        Assert.Equal(7, repeat.ElapsedMs);
        Assert.Equal(7, repeat.TerminalUnixMs);
        Assert.Equal(0, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Given_CallbackAndDelivery_When_FirstRuleCaptures_Then_GateOrderChoosesOutcomeEvenAtTiedTime(bool callbackFirst, bool tied)
    {
        var fixture = new Fixture();
        fixture.Native.ActionElapsed = 7;
        if (callbackFirst)
        {
            fixture.Native.BeforeDelivery = () =>
            {
                fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
                if (!tied) fixture.Clock.Milliseconds++;
            };
        }
        else fixture.Native.AfterDelivery = () =>
        {
            if (!tied) fixture.Clock.Milliseconds++;
            fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.True(repeat.Terminal);
        Assert.Equal(callbackFirst ? "error" : "success", repeat.Result);
        Assert.Equal(callbackFirst ? -196 : null, repeat.Error?.Code);
        Assert.Equal(7, repeat.ElapsedMs);
        Assert.Equal(7, repeat.TerminalUnixMs);
        Assert.Single(repeat.ClientErrors);
        Assert.Equal(tied ? 20007 : 20008, fixture.Clock.Milliseconds);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(ErrorCode.Local_Fail, -196, "local")]
    [InlineData(ErrorCode.Local_AllBrokersDown, -187, "local")]
    [InlineData(ErrorCode.TopicAuthorizationFailed, 29, "broker")]
    public async Task Given_FirstRuleCallbackCodes_When_Capturing_Then_RetainsActualCodeWithoutFiltering(ErrorCode code, int expected, string origin)
    {
        var fixture = new Fixture();
        fixture.Native.BeforeDelivery = () => fixture.Native.Capture.Error(new Error(code));
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.Equal("error", repeat.Result);
        Assert.Equal(expected, repeat.Error!.Code);
        Assert.Equal(origin, repeat.Error.Origin);
        Assert.False(repeat.Error.IsFatal);
    }

    [Fact]
    public async Task Given_NoDelivery_When_FirstRuleCallbackArrivesOnDelay_Then_TerminatesAndCollectsPostTerminalStatistics()
    {
        var fixture = new Fixture();
        fixture.Native.NoDelivery = true;
        fixture.Clock.OnDelay = () =>
        {
            fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
            fixture.Native.Capture.Statistics("{\"time\":1,\"ts\":1,\"brokers\":{}}");
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.Equal("error", repeat.Result);
        Assert.Equal(100, repeat.ElapsedMs);
        Assert.Equal(100, repeat.TerminalUnixMs);
        Assert.Equal(20100, fixture.Clock.Milliseconds);
        Assert.Single(repeat.Statistics);
        Assert.Equal(1, fixture.Native.ProduceCalls);
        Assert.Equal(1, fixture.Native.DisposeCalls);
    }

    [Theory]
    [InlineData(63, 63, 0)]
    [InlineData(64, 64, 0)]
    [InlineData(65, 64, 1)]
    [InlineData(66, 64, 2)]
    public async Task Given_FirstRuleErrorCountBoundary_When_Capturing_Then_Records64AndCountsOverflow(int count, int recorded, long dropped)
    {
        var fixture = new Fixture();
        fixture.Native.NoDelivery = true;
        fixture.Native.BeforeDelivery = () =>
        {
            for (int index = 0; index < count; index++)
                fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail, index >= 64 ? new string('x', 4097) : "synthetic"));
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.Equal("error", repeat.Result);
        Assert.Equal(-196, repeat.Error!.Code);
        Assert.Equal(recorded, repeat.ClientErrors.Count);
        Assert.Equal(dropped, repeat.ClientErrorsDropped);
        Assert.DoesNotContain("capture_limit", repeat.Diagnostics);
        using JsonDocument json = JsonDocument.Parse(SingleActionWire.Encode(repeat, fixture.Options.MaxCaptureBytes));
        Assert.Equal(dropped, json.RootElement.GetProperty("client_errors_dropped").GetInt64());
    }

    [Fact]
    public async Task Given_StandardErrorOverflow_When_Capturing_Then_StillLimitsAndNeverCountsDroppedErrors()
    {
        var fixture = new Fixture();
        fixture.Native.ProduceErrors = 65;
        var repeat = await fixture.RunAsync(Action("produce_one", 330000));
        Assert.Equal(64, repeat.ClientErrors.Count);
        Assert.Equal(0, repeat.ClientErrorsDropped);
        Assert.Contains("capture_limit", repeat.Diagnostics);
    }

    [Fact]
    public void Given_FirstRuleOversizedRecordedReason_When_Capturing_Then_StillLimitsBeforeChoosingOutcome()
    {
        var fixture = new Fixture();
        var capture = new SingleActionCapture(fixture.Options, fixture.Clock, 0, new(100), FirstRule);
        capture.Error(new Error(ErrorCode.Local_Fail, new string('x', 4097)));
        Assert.True(capture.Limit);
        Assert.False(capture.ProducerOutcome.IsCompleted);
        Assert.Empty(capture.Snapshot().Errors);
        Assert.Equal(0, capture.Snapshot().Dropped);
    }

    [Fact]
    public async Task Given_ChosenCallbackThenConcurrentErrors_When_FirstRuleCaptures_Then_PreservesFirstAndSaturatesRecordedArray()
    {
        var fixture = new Fixture();
        fixture.Native.NoDelivery = true;
        fixture.Native.ActionElapsed = 7;
        fixture.Native.BeforeDelivery = () =>
        {
            fixture.Native.Capture.Error(new Error(ErrorCode.TopicAuthorizationFailed));
            Task.WhenAll(Enumerable.Range(0, 66).Select(_ => Task.Run(() => fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail)))))
                .GetAwaiter().GetResult();
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.Equal(29, repeat.Error!.Code);
        Assert.Equal(7, repeat.ElapsedMs);
        Assert.Equal(7, repeat.TerminalUnixMs);
        Assert.Equal(64, repeat.ClientErrors.Count);
        Assert.Equal(3, repeat.ClientErrorsDropped);
        Assert.DoesNotContain("capture_limit", repeat.Diagnostics);
    }

    [Fact]
    public async Task Given_DeliveryThenStatisticsOverflow_When_FirstRuleCaptures_Then_PreservesSuccessAndStatisticsLimit()
    {
        var fixture = new Fixture();
        fixture.Native.AfterDelivery = () =>
        {
            for (int index = 0; index < 65; index++) fixture.Native.Capture.Statistics("{}");
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.Equal(64, repeat.Statistics.Count);
        Assert.Equal(0, repeat.ClientErrorsDropped);
        Assert.Contains("capture_limit", repeat.Diagnostics);
    }

    [Theory]
    [InlineData(true, -196)]
    [InlineData(false, 29)]
    public async Task Given_ProduceException_When_FirstRuleCaptures_Then_EarlierCallbackWinsOtherwiseExceptionIsOutcome(bool callback, int code)
    {
        var fixture = new Fixture();
        fixture.Native.BeforeDelivery = () =>
        {
            if (callback) fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
            throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.TopicAuthorizationFailed), new());
        };
        var repeat = await fixture.RunAsync(Action("produce_one", 330000) with { TerminalRule = FirstRule });
        Assert.True(repeat.Terminal);
        Assert.Equal("error", repeat.Result);
        Assert.Equal(code, repeat.Error!.Code);
        Assert.Equal(callback ? "local" : "broker", repeat.Error.Origin);
        Assert.Equal(1, fixture.Native.ProduceCalls);
    }

    [Theory]
    [InlineData("subscribe", WindowRule)]
    [InlineData("assign_consume", WindowRule)]
    [InlineData("subscribe", "standard")]
    [InlineData("assign_consume", "standard")]
    public async Task Given_SilentConsumer_When_WindowEnds_Then_OnlyWindowRuleRecordsNoOutcome(string action, string rule)
    {
        var fixture = new Fixture();
        const string raw = "{\"time\":120,\"ts\":1,\"brokers\":{}}";
        fixture.Native.OnPoll = () =>
        {
            if (fixture.Clock.Milliseconds == 120100) fixture.Native.Capture.Statistics(raw);
        };
        var repeat = await fixture.RunAsync(Action(action, 120000) with { TerminalRule = rule });
        bool noOutcome = rule == WindowRule;
        Assert.Equal(noOutcome, repeat.Terminal);
        Assert.Equal(noOutcome ? "no_outcome" : null, repeat.Result);
        Assert.Equal(120000, repeat.ElapsedMs);
        Assert.Equal(noOutcome ? 120000 : null, repeat.TerminalUnixMs);
        Assert.Null(repeat.Error);
        Assert.Null(repeat.Matched);
        Assert.Empty(repeat.ClientErrors);
        Assert.Equal(0, repeat.ClientErrorsDropped);
        Assert.Equal(noOutcome ? 140000 : 120000, fixture.Clock.Milliseconds);
        Assert.Equal(1, fixture.Native.DisposeCalls);
        if (noOutcome)
        {
            var report = Assert.Single(repeat.Statistics);
            Assert.Equal(120100, report.ObservedElapsedMs);
            Assert.Equal(raw, Encoding.UTF8.GetString(Convert.FromBase64String(report.RawBase64)));
        }
        else Assert.Empty(repeat.Statistics);
    }

    [Fact]
    public async Task Given_NonfatalConsumerError_When_WindowRuleExpires_Then_LeavesOutcomeAbsent()
    {
        var fixture = new Fixture();
        fixture.Native.OnFirstPoll = () => fixture.Native.Capture.Error(new Error(ErrorCode.Local_Fail));
        var repeat = await fixture.RunAsync(Action("subscribe", 120000) with { TerminalRule = WindowRule });
        Assert.False(repeat.Terminal);
        Assert.Null(repeat.Result);
        Assert.Single(repeat.ClientErrors);
        Assert.Equal(0, repeat.ClientErrorsDropped);
        Assert.Equal(120000, repeat.ElapsedMs);
        Assert.Equal(120000, fixture.Clock.Milliseconds);
    }

    [Theory]
    [InlineData("subscribe", 119999)]
    [InlineData("subscribe", 120000)]
    [InlineData("subscribe", 120001)]
    [InlineData("assign_consume", 119999)]
    [InlineData("assign_consume", 120000)]
    [InlineData("assign_consume", 120001)]
    public async Task Given_ConsumerSuccessBoundary_When_WindowRuleCaptures_Then_WithinWindowSuccessPrecedesNoOutcome(string action, int observed)
    {
        var fixture = new Fixture();
        if (action == "subscribe")
        {
            fixture.Native.AssignOnPoll = true;
            fixture.Native.ActionElapsed = observed;
        }
        else
        {
            fixture.Native.Records.Enqueue(Record("document-events"));
            fixture.Native.OnFirstPoll = () => fixture.Clock.Milliseconds = observed;
        }
        var repeat = await fixture.RunAsync(Action(action, 120000) with { TerminalRule = WindowRule });
        Assert.True(repeat.Terminal);
        Assert.Equal(observed <= 120000 ? "success" : "no_outcome", repeat.Result);
        Assert.Equal(Math.Min(observed, 120000), repeat.ElapsedMs);
        Assert.Equal(action == "assign_consume" && observed <= 120000, repeat.Matched is not null);
        Assert.Empty(repeat.ClientErrors);
    }

    [Fact]
    public async Task Given_NoOutcomeThenPostTerminalError_When_Capturing_Then_KeepsTupleAndRawCallbackForVerifier()
    {
        var fixture = new Fixture();
        fixture.Native.OnPoll = () =>
        {
            if (fixture.Clock.Milliseconds == 120100) fixture.Native.Capture.Error(new Error(ErrorCode.TopicAuthorizationFailed));
        };
        var repeat = await fixture.RunAsync(Action("subscribe", 120000) with { TerminalRule = WindowRule });
        Assert.True(repeat.Terminal);
        Assert.Equal("no_outcome", repeat.Result);
        Assert.Equal(120000, repeat.ElapsedMs);
        Assert.Equal(120000, repeat.TerminalUnixMs);
        Assert.Null(repeat.Error);
        Assert.Null(repeat.Matched);
        Assert.Equal(29, Assert.Single(repeat.ClientErrors).Code);
        Assert.Equal(0, repeat.ClientErrorsDropped);
    }

    [Theory]
    [InlineData("empty", true)]
    [InlineData("limit", false)]
    [InlineData("error", false)]
    [InlineData("assignment_before", false)]
    [InlineData("assignment_equal", false)]
    [InlineData("assignment_after", true)]
    [InlineData("direct_assignment", true)]
    public void Given_WindowEndObservation_When_ArbitratingUnderGate_Then_RequiresNoErrorsOrTimelySubscriptionAssignment(string state, bool expected)
    {
        var fixture = new Fixture();
        var capture = new SingleActionCapture(fixture.Options, fixture.Clock, 0, new(100), WindowRule);
        if (state == "limit") capture.CaptureLimit();
        if (state == "error") capture.Error(new Error(ErrorCode.Local_Fail));
        if (state.StartsWith("assignment_", StringComparison.Ordinal) || state == "direct_assignment")
        {
            fixture.Clock.Milliseconds = state == "assignment_before" ? 119999 : state == "assignment_after" ? 120001 : 120000;
            capture.AssignedPartitions(1);
        }
        bool observed = false;
        Assert.Equal(expected, capture.TryObserveWindowEnd(120000, state != "direct_assignment", () => observed = true));
        Assert.Equal(expected, observed);
    }

    internal static SingleActionHostOptions Options() => new()
    {
        ScenarioId = "F04", ConfigurationId = "C2", RunId = "run-1", SourceCommit = new string('a', 40),
        JobName = "run-1-f04-c2-single", PodName = "pod-1", PodUid = "uid-1", NamespaceName = "namespace-test",
        BlobEndpoint = "https://example.blob.core.windows.net/", BlobContainer = "single-action", ScenarioRoot = "/app",
        ProcessLifetimeSeconds = 1920, PostTerminalStatisticsSeconds = 20, CleanupSeconds = 10, RegistrationUploadSeconds = 30,
        CaptureUploadSeconds = 30, HostCleanupSeconds = 10, ReceiptUploadSeconds = 30, PollSliceMs = 100,
        MaxCaptureBytes = 134217728, MaxRegistrationBytes = 65536, MaxCompletionBytes = 65536, MaxStatisticsReportBytes = 262144,
        MaxStatisticsReports = 64, MaxClientErrors = 64, MaxErrorBytes = 4096, MaxDiagnostics = 100, MaxDiagnosticBytes = 2048,
        CallbackQueueCapacity = 128, MaxSettingsKeys = 128, MaxSettingsBytes = 16384, MaxNonmatchingRecords = 4096, MaxInputDepth = 32,
        ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }
    };
    internal static SingleActionConfiguration Action(string action, int window, string topic = "document-events") =>
        new(action, KafkaClientRole.LoadGenProducer, topic, 0, action is "subscribe" or "init_transactions" ? 0 : 1000, window, "namespace", "none");
    internal static ResolvedRoleConfiguration Resolved() => new(KafkaClientRole.LoadGenProducer,
        ImmutableDictionary<string, ResolvedSetting>.Empty, []);
    internal static ConsumeResult<byte[], byte[]> Record(string topic) => new()
    {
        Topic = topic, Partition = new Partition(0), Offset = new Offset(0),
        Message = new() { Key = Encoding.UTF8.GetBytes("f04-seed:run-1"), Value = SingleActionWorker.Payload(1000) }
    };
    internal sealed class Fixture
    {
        internal SingleActionHostOptions Options { get; } = SingleActionCaptureTests.Options();
        internal AdvancingClock Clock { get; } = new();
        internal NativeProxy Native { get; }
        private readonly SingleActionWorker _worker;
        internal Fixture(TextWriter? clientLogOutput = null)
        {
            IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProxy>();
            IConsumer<byte[], byte[]> consumer = DispatchProxy.Create<IConsumer<byte[], byte[]>, NativeProxy>();
            Native = (NativeProxy)(object)producer;
            Native.Clock = Clock;
            NativeProxy consumerProxy = (NativeProxy)(object)consumer;
            consumerProxy.Forward = Native;
            var factory = new SingleActionSessionFactory((_, capture, _) =>
            {
                if (Native.ConstructionFailure is not null) throw Native.ConstructionFailure;
                Native.Capture = capture;
                Native.OnBuild?.Invoke();
                return producer;
            }, (_, capture, _) =>
            {
                if (Native.ConstructionFailure is not null) throw Native.ConstructionFailure;
                Native.Capture = capture; return consumer;
            });
            _worker = new(Options, factory, new MemoryPublication(), Clock, clientLogOutput: clientLogOutput ?? new StringWriter());
        }
        internal Task<SingleActionRepeat> RunAsync(SingleActionConfiguration action, ResolvedRoleConfiguration? resolved = null) => _worker.CaptureRepeatAsync(action, resolved ?? Resolved(), 1, new(100), TestContext.Current.CancellationToken);
    }
    internal sealed class MemoryPublication : ISingleActionPublication
    {
        public Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    internal sealed class AdvancingClock : TimeProvider
    {
        internal long Milliseconds;
        internal Action? OnDelay;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Milliseconds);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == TimeSpan.FromMilliseconds(100))
            {
                Milliseconds += 100;
                Action? onDelay = OnDelay;
                OnDelay = null;
                onDelay?.Invoke();
                callback(state);
            }
            return new TimerStub();
        }
        private sealed class TimerStub : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
    public class NativeProxy : DispatchProxy
    {
        internal NativeProxy? Forward;
        internal AdvancingClock Clock = null!;
        internal SingleActionCapture Capture = null!;
        internal int ProduceCalls, InitCalls, SubscribeCalls, AssignCalls, PollCalls, DisposeCalls;
        internal int ActionElapsed;
        internal TimeSpan InitTimeout;
        internal bool AssignOnPoll;
        internal bool FatalOnPoll, NoDelivery, PostTerminalConsumeError, PostTerminalStatisticsOverflow;
        internal bool FatalBeforeAssignment, ThrowAfterFirstAssignment, StatisticsOverflowOnFirstPoll;
        internal int ProduceErrors, ErrorsAfterDelivery;
        internal Action? OnBuild, BeforeDelivery, AfterDelivery, OnFirstPoll, OnPoll, OnDispose;
        internal Exception? ConstructionFailure, ActionException;
        internal Error Error = new(ErrorCode.NoError);
        internal string? Report;
        internal TopicPartitionOffset? Assignment;
        internal Queue<ConsumeResult<byte[], byte[]>> Records = new();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (Forward is not null) return Forward.Invoke(targetMethod, args);
            if (targetMethod?.Name != "Dispose" && ActionException is not null)
            {
                Exception exception = ActionException;
                ActionException = null;
                throw exception;
            }
            switch (targetMethod?.Name)
            {
                case "Produce":
                    ProduceCalls++;
                    Clock.Milliseconds += ActionElapsed;
                    if (Report is not null) Capture.Statistics(Report);
                    for (int index = 0; index < ProduceErrors; index++) Capture.Error(new Error(ErrorCode.Local_Fail));
                    BeforeDelivery?.Invoke();
                    if (!NoDelivery) ((Action<DeliveryReport<byte[], byte[]>>)args![2]!)(new() { Error = Error });
                    AfterDelivery?.Invoke();
                    for (int index = 0; index < ErrorsAfterDelivery; index++) Capture.Error(new Error(ErrorCode.Local_Fail));
                    return null;
                case "InitTransactions": InitCalls++; InitTimeout = (TimeSpan)args![0]!; return null;
                case "Subscribe": SubscribeCalls++; return null;
                case "Assign": AssignCalls++; Assignment = (TopicPartitionOffset)args![0]!; return null;
                case "Consume":
                    PollCalls++;
                    if (PollCalls == 1) OnFirstPoll?.Invoke();
                    if (PollCalls == 1 && StatisticsOverflowOnFirstPoll)
                        for (int index = 0; index < 65; index++) Capture.Statistics("{\"time\":1,\"ts\":1}");
                    if (PollCalls == 2 && PostTerminalStatisticsOverflow)
                        for (int index = 0; index < 65; index++) Capture.Statistics("{\"time\":1,\"ts\":1}");
                    if (PollCalls == 2 && PostTerminalConsumeError)
                        throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.TopicAuthorizationFailed));
                    if (PollCalls == 1 && FatalOnPoll && FatalBeforeAssignment) Capture.Error(new Error(ErrorCode.Local_Fatal, "synthetic fatal", true));
                    if (PollCalls == 1 && AssignOnPoll) { Clock.Milliseconds += ActionElapsed; Capture.AssignedPartitions(1); }
                    else if (Records.Count == 0) Clock.Milliseconds += 100;
                    OnPoll?.Invoke();
                    if (PollCalls == 1 && FatalOnPoll && !FatalBeforeAssignment) Capture.Error(new Error(ErrorCode.Local_Fatal, "synthetic fatal", true));
                    if (PollCalls == 1 && ThrowAfterFirstAssignment)
                        throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.TopicAuthorizationFailed));
                    return Records.TryDequeue(out ConsumeResult<byte[], byte[]>? record) ? record : null;
                case "Dispose": DisposeCalls++; OnDispose?.Invoke(); return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
