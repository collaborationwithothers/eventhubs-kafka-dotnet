using System.Collections.Immutable;
using System.Reflection;
using System.Text;
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
        internal Fixture()
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
                Native.Capture = capture; return producer;
            }, (_, capture, _) =>
            {
                if (Native.ConstructionFailure is not null) throw Native.ConstructionFailure;
                Native.Capture = capture; return consumer;
            });
            _worker = new(Options, factory, new MemoryPublication(), Clock);
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
                    if (!NoDelivery) ((Action<DeliveryReport<byte[], byte[]>>)args![2]!)(new() { Error = Error });
                    for (int index = 0; index < ErrorsAfterDelivery; index++) Capture.Error(new Error(ErrorCode.Local_Fail));
                    return null;
                case "InitTransactions": InitCalls++; InitTimeout = (TimeSpan)args![0]!; return null;
                case "Subscribe": SubscribeCalls++; return null;
                case "Assign": AssignCalls++; Assignment = (TopicPartitionOffset)args![0]!; return null;
                case "Consume":
                    PollCalls++;
                    if (PollCalls == 1 && StatisticsOverflowOnFirstPoll)
                        for (int index = 0; index < 65; index++) Capture.Statistics("{\"time\":1,\"ts\":1}");
                    if (PollCalls == 2 && PostTerminalStatisticsOverflow)
                        for (int index = 0; index < 65; index++) Capture.Statistics("{\"time\":1,\"ts\":1}");
                    if (PollCalls == 2 && PostTerminalConsumeError)
                        throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.TopicAuthorizationFailed));
                    if (PollCalls == 1 && FatalOnPoll && FatalBeforeAssignment) Capture.Error(new Error(ErrorCode.Local_Fatal, "synthetic fatal", true));
                    if (PollCalls == 1 && AssignOnPoll) { Clock.Milliseconds += ActionElapsed; Capture.AssignedPartitions(1); }
                    else if (Records.Count == 0) Clock.Milliseconds += 100;
                    if (PollCalls == 1 && FatalOnPoll && !FatalBeforeAssignment) Capture.Error(new Error(ErrorCode.Local_Fatal, "synthetic fatal", true));
                    if (PollCalls == 1 && ThrowAfterFirstAssignment)
                        throw new ConsumeException(new ConsumeResult<byte[], byte[]>(), new Error(ErrorCode.TopicAuthorizationFailed));
                    return Records.TryDequeue(out ConsumeResult<byte[], byte[]>? record) ? record : null;
                case "Dispose": DisposeCalls++; return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
