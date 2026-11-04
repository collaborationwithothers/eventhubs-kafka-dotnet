using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
// Local completion requires the entire file protocol, while faults retain bounded ownership cleanup.
public sealed class PacketSummaryFileWriterTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_EmptyOrTwoEvents_When_Completing_Then_FinalizesOnceAfterTailFlushAndClose(bool populated)
    {
        using var factory = new RecordingFactory();
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        factory.BeforeOperation = operation =>
        {
            if (operation is "flush" or "close" or "finalize")
                Assert.Equal(PacketSummaryWriterState.Active, writer.State);
        };
        Assert.Equal(PacketSummaryWriterState.Active, writer.State);
        if (populated)
        {
            Assert.True(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
            Assert.True(writer.TryAppend(PacketSummaryFileTestData.SecondEvent));
        }
        Assert.True(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
        Assert.Null(writer.FailureCode);
        Assert.False(writer.CleanupFailed);
        Assert.Equal(populated ? 2 : 0, writer.EventCount);
        Assert.Equal(factory.Stream.Length, writer.BytesWritten);
        string expected = populated
            ? "create,prefix,event,separator,event,tail,flush,close,finalize"
            : "create,prefix,tail,flush,close,finalize";
        Assert.Equal(expected, factory.Trace);
        byte[] bytes = factory.Stream.ToArray();
        PacketSummaryFileTestData.AssertJson(bytes, populated);
        writer.Dispose();
        writer.Dispose();
        Assert.Equal(expected, factory.Trace);
        Assert.Equal(bytes, factory.Stream.ToArray());
        Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
        Assert.Null(writer.FailureCode);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("relative")] [InlineData("/invalid\0directory")]
    public void Given_InvalidPathAndUnsupportedPlatform_When_Constructing_Then_PathFailurePrecedesPlatformWithoutCreate(string? directory)
    {
        using var factory = new RecordingFactory { IsSupported = false };
        using var writer = new PacketSummaryFileWriter(directory!, PacketSummaryFileTestData.Identity(), factory);
        AssertFault(writer, factory, "packet_summary_path_invalid", "");
        Assert.Equal(0, writer.EventCount);
        Assert.Equal(0, writer.BytesWritten);
    }

    [Fact]
    public void Given_InvalidIdentityAndUnsupportedPlatform_When_Constructing_Then_IdentityFailsBeforeCreate()
    {
        using var factory = new RecordingFactory { IsSupported = false };
        var identity = new PacketSummaryIdentity("INVALID", new string('a', 40), "C0", 1, PacketSummaryFileTestData.StartedUtc);
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, identity, factory);
        AssertFault(writer, factory, "packet_identity_invalid", "");
        Assert.Equal(0, writer.EventCount);
        Assert.Equal(0, writer.BytesWritten);
    }

    [Fact]
    public void Given_NullIdentity_When_Constructing_Then_ThrowsArgumentErrorWithoutCreate()
    {
        using var factory = new RecordingFactory();
        Assert.Throws<ArgumentNullException>(() => new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, null!, factory));
        Assert.Empty(factory.Trace);
    }

    [Fact]
    public void Given_UnsupportedPlatform_When_Constructing_Then_FaultsWithoutCreate()
    {
        using var factory = new RecordingFactory { IsSupported = false };
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        AssertFault(writer, factory, "packet_summary_platform_unsupported", "");
    }

    [Theory]
    [InlineData(1, false, false)] [InlineData(2, false, false)] [InlineData(3, false, false)]
    [InlineData(1, true, false)] [InlineData(2, true, false)] [InlineData(3, true, false)]
    [InlineData(1, false, true)] [InlineData(2, false, true)] [InlineData(3, false, true)]
    public void Given_WriteFaultCancellationOrPartialConsumption_When_WritingEachStage_Then_PreservesCoreCodeAndCleansOnce(
        int failCall, bool cancel, bool consumePrefix)
    {
        using var factory = new RecordingFactory { Cancel = cancel };
        factory.Stream.FailCall = failCall;
        factory.Stream.ConsumePrefix = consumePrefix;
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        if (failCall >= 2) Assert.Equal(failCall > 2, writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
        if (failCall == 3) Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        string expected = failCall switch
        {
            1 => "create,prefix,close,delete",
            2 => "create,prefix,event,close,delete",
            _ => "create,prefix,event,tail,close,delete"
        };
        AssertFault(writer, factory, "packet_summary_write_failed", expected);
        Assert.False(writer.CleanupFailed);
        Assert.Equal(failCall == 3 ? 1 : 0, writer.EventCount);
        Assert.Equal(factory.Stream.Length - (consumePrefix ? 1 : 0), writer.BytesWritten);
    }

    [Theory]
    [InlineData("event", "packet_event_invalid")]
    [InlineData("end", "packet_identity_invalid")]
    [InlineData("offset", "packet_identity_invalid")]
    [InlineData("count", "packet_event_count_exceeded")]
    public void Given_CoreValidationFailure_When_AppendingOrCompleting_Then_PreservesCodeAndNeverFinalizes(string failure, string expectedCode)
    {
        using var factory = new RecordingFactory();
        var budget = new PacketSummaryBudget(1, 256, 1000, 400);
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory, budget);
        if (failure == "count") Assert.True(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
        long before = writer.BytesWritten;
        bool result = failure switch
        {
            "event" => writer.TryAppend(new PacketSummaryEvent(0, PacketDirection.Outbound, 0, TcpControlFlags.Syn)),
            "end" => writer.TryComplete(PacketSummaryFileTestData.StartedUtc.AddTicks(-1)),
            "offset" => writer.TryComplete(PacketSummaryFileTestData.EndedUtc.ToOffset(TimeSpan.FromHours(1))),
            _ => writer.TryAppend(PacketSummaryFileTestData.SecondEvent)
        };
        Assert.False(result);
        AssertFault(writer, factory, expectedCode, failure == "count" ? "create,prefix,event,close,delete" : "create,prefix,close,delete");
        Assert.Equal(before, writer.BytesWritten);
        Assert.Equal(failure == "count" ? 1 : 0, writer.EventCount);
    }

    [Theory]
    [InlineData("create", false, "packet_summary_open_failed", "create")]
    [InlineData("flush", false, "packet_summary_flush_failed", "create,prefix,tail,flush,close,delete")]
    [InlineData("close", false, "packet_summary_close_failed", "create,prefix,tail,flush,close,delete")]
    [InlineData("finalize", false, "packet_summary_finalize_failed", "create,prefix,tail,flush,close,finalize,delete")]
    [InlineData("create", true, "packet_summary_open_failed", "create")]
    [InlineData("flush", true, "packet_summary_flush_failed", "create,prefix,tail,flush,close,delete")]
    [InlineData("close", true, "packet_summary_close_failed", "create,prefix,tail,flush,close,delete")]
    [InlineData("finalize", true, "packet_summary_finalize_failed", "create,prefix,tail,flush,close,finalize,delete")]
    [InlineData("missing-native", false, "packet_summary_finalize_failed", "create,prefix,tail,flush,close,finalize,delete")]
    public void Given_BoundaryStageFailure_When_Finalizing_Then_StopsLaterStagesAndExposesOnlyFixedCode(
        string failure, bool cancel, string expectedCode, string expectedTrace)
    {
        using var factory = new RecordingFactory { Failure = failure, Cancel = cancel };
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        AssertFault(writer, factory, expectedCode, expectedTrace);
        Assert.Equal(failure == "close", writer.CleanupFailed);
        if (failure == "create")
        {
            Assert.Equal(0, writer.BytesWritten);
            Assert.Equal(0, writer.EventCount);
        }
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void Given_CleanupFailures_When_CoreFaults_Then_FirstCodeSurvivesAndCleanupNeverRetries(bool closeFailure, bool deleteFailure)
    {
        using var factory = new RecordingFactory { FailClose = closeFailure, FailDelete = deleteFailure };
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        Assert.False(writer.TryAppend(new PacketSummaryEvent(0, PacketDirection.Outbound, 0, TcpControlFlags.Syn)));
        AssertFault(writer, factory, "packet_event_invalid", "create,prefix,close,delete");
        Assert.True(writer.CleanupFailed);
    }

    [Fact]
    public void Given_ActiveWriter_When_DisposingRepeatedly_Then_AbandonsAndCleansOwnedPartialOnce()
    {
        using var factory = new RecordingFactory();
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        writer.Dispose();
        AssertFault(writer, factory, "packet_summary_abandoned", "create,prefix,close,delete");
        Assert.False(writer.CleanupFailed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_CompletedWriter_When_AppendingOrCompletingAgain_Then_FaultsWithoutDeletingFinal(bool append)
    {
        using var factory = new RecordingFactory();
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        Assert.True(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        byte[] finalBytes = factory.Stream.ToArray();
        Assert.False(append ? writer.TryAppend(PacketSummaryFileTestData.FirstEvent) : writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        AssertFault(writer, factory, "packet_summary_state_invalid", "create,prefix,tail,flush,close,finalize");
        Assert.Equal(finalBytes, factory.Stream.ToArray());
    }

    private static void AssertFault(PacketSummaryFileWriter writer, RecordingFactory factory, string code, string trace)
    {
        Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
        Assert.Equal(code, writer.FailureCode);
        Assert.Equal(trace, factory.Trace);
        long bytes = writer.BytesWritten;
        int events = writer.EventCount;
        Assert.False(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
        Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        writer.Dispose();
        writer.Dispose();
        Assert.Equal(code, writer.FailureCode);
        Assert.Equal(trace, factory.Trace);
        Assert.Equal(bytes, writer.BytesWritten);
        Assert.Equal(events, writer.EventCount);
        string exposed = writer.FailureCode + writer.State + Encoding.UTF8.GetString(factory.Stream.ToArray());
        Assert.DoesNotContain("PATH_SENTINEL_334", exposed, StringComparison.Ordinal);
        Assert.DoesNotContain("PAYLOAD_SENTINEL_334", exposed, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.34", exposed, StringComparison.Ordinal);
    }

    private sealed class RecordingFactory : IPacketSummaryFileFactory, IPacketSummaryStagingFile, IDisposable
    {
        private const string SensitiveMessage = "PATH_SENTINEL_334 PAYLOAD_SENTINEL_334 192.0.2.34";
        private readonly List<string> _operations = [];
        public RecordingFactory() => Stream = new RecordingStream(this);
        public RecordingStream Stream { get; }
        public bool IsSupported { get; init; } = true;
        public string? Failure { get; init; }
        public bool Cancel { get; init; }
        public bool FailClose { get; init; }
        public bool FailDelete { get; init; }
        public Action<string>? BeforeOperation { get; set; }
        public string Trace => string.Join(',', _operations);
        Stream IPacketSummaryStagingFile.Content => Stream;
        public IPacketSummaryStagingFile Create(string directory)
        {
            Assert.Equal(PacketSummaryFileTestData.Directory, directory);
            Record("create");
            return this;
        }
        public void FlushToDisk() => Record("flush");
        public void Close() => Record("close");
        public void FinalizeFile() => Record("finalize");
        public void DeletePartial() => Record("delete");
        public void Dispose() => Stream.Dispose();
        public void Record(string operation)
        {
            _operations.Add(operation);
            BeforeOperation?.Invoke(operation);
            if (operation == "finalize" && Failure == "missing-native") throw new EntryPointNotFoundException(SensitiveMessage);
            if (operation == Failure || (operation == "close" && FailClose) || (operation == "delete" && FailDelete)) ThrowFailure();
        }
        public void ThrowFailure()
        {
            if (Cancel) throw new OperationCanceledException(SensitiveMessage);
            throw new IOException(SensitiveMessage);
        }
    }

    private sealed class RecordingStream(RecordingFactory factory) : MemoryStream
    {
        private int _calls;
        public int FailCall { get; set; }
        public bool ConsumePrefix { get; set; }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            string operation = buffer[0] switch
            {
                (byte)']' => "tail",
                (byte)',' => "separator",
                _ => _calls == 0 ? "prefix" : "event"
            };
            _calls++;
            factory.Record(operation);
            if (_calls == FailCall)
            {
                if (ConsumePrefix) base.Write(buffer[..1]);
                factory.ThrowFailure();
            }
            base.Write(buffer);
        }
    }
}

internal static class PacketSummaryFileTestData
{
    private static readonly string[] EnvelopeKeys = ["schema_version", "run_id", "source_commit", "configuration_id", "repeat", "capture_started_utc", "events", "capture_ended_utc"];
    private static readonly string[] EventKeys = ["flow", "direction", "elapsed_ns", "flags"];
    private static readonly string[] SynFlags = ["SYN"];
    private static readonly string[] AckFlags = ["ACK"];
    public static string Directory => Path.GetFullPath("packet-summary-synthetic-334");
    public static DateTimeOffset StartedUtc => new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
    public static DateTimeOffset EndedUtc => StartedUtc.AddSeconds(1);
    public static PacketSummaryIdentity Identity() => new("capture-file-334", new string('a', 40), "C0", 1, StartedUtc);
    public static PacketSummaryEvent FirstEvent => new(1, PacketDirection.Outbound, 0, TcpControlFlags.Syn);
    public static PacketSummaryEvent SecondEvent => new(1, PacketDirection.Inbound, 1, TcpControlFlags.Ack);

    public static void AssertJson(byte[] bytes, bool populated)
    {
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal((byte)'}', bytes[^1]);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Assert.Equal(EnvelopeKeys, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("capture-file-334", root.GetProperty("run_id").GetString());
        Assert.Equal(new string('a', 40), root.GetProperty("source_commit").GetString());
        Assert.Equal("C0", root.GetProperty("configuration_id").GetString());
        Assert.Equal(1, root.GetProperty("repeat").GetInt32());
        Assert.Equal(StartedUtc.ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_started_utc").GetString());
        Assert.Equal(EndedUtc.ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_ended_utc").GetString());
        JsonElement events = root.GetProperty("events");
        Assert.Equal(populated ? 2 : 0, events.GetArrayLength());
        if (!populated) return;
        AssertEvent(events[0], "outbound", 0, SynFlags);
        AssertEvent(events[1], "inbound", 1, AckFlags);
    }

    private static void AssertEvent(JsonElement value, string direction, long elapsed, string[] flags)
    {
        Assert.Equal(EventKeys, value.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, value.GetProperty("flow").GetInt32());
        Assert.Equal(direction, value.GetProperty("direction").GetString());
        Assert.Equal(elapsed, value.GetProperty("elapsed_ns").GetInt64());
        Assert.Equal(flags, value.GetProperty("flags").EnumerateArray().Select(flag => flag.GetString()));
    }
}
#pragma warning restore CA1707
