using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
/// <summary>
/// Fatal-error capture (ADR-021 rule 12): every first-child error callback reaches the supervisor's journal as a numbered
/// <c>native_error</c> record, so a kill cannot lose an error the child's own evidence session still held in memory.
/// </summary>
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptFatalCaptureTests
{
    // T6: a real librdkafka fatal error, through the host's handler, a real pipe, the real channel, supervisor handling and the journal on disk.
    [Fact]
    public async Task Given_RealFatalNativeError_When_HostHandlerRuns_Then_JournalHoldsExactlyOneNativeErrorFrame()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        string capture = Path.Combine(fixture.Root, "capture");
        Directory.CreateDirectory(capture);
        await using var evidence = new RunEvidenceSession(new RunIdentity("B10", "C1", "synthetic", "synthetic-first", "synthetic-first", "synthetic-first"),
            new RunEvidenceOptions(capture, 2048, 1048576, 134217728, 100, TimeSpan.FromSeconds(20)), TimeProvider.System);
        using var events = new AnonymousPipeServerStream(PipeDirection.In);
        using var acknowledgements = new AnonymousPipeServerStream(PipeDirection.Out);
        await using B10AbruptChildChannel channel = B10AbruptChildChannel.Connect(events.GetClientHandleAsString(), acknowledgements.GetClientHandleAsString(), new string('c', 64));
        B10AbruptWire.Message hello = (await B10AbruptWire.ReadMessageAsync(events, 1, TestContext.Current.CancellationToken))!;
        Assert.Equal("hello", hello.Kind);
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig { EnableIdempotence = true })
            .SetErrorHandler(B10AbruptHost.CreateErrorHandler(evidence, channel, TimeProvider.System)).Build();

        Assert.Equal(0, NativeFatal.Raise(producer, ErrorCode.OutOfOrderSequenceNumber, "offline test"));

        B10AbruptWire.Message message = (await B10AbruptWire.ReadMessageAsync(events, 2, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))!;
        Assert.Equal("native_error", message.Kind);
        await fixture.Supervisor.HandleAsync(message, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Null(channel.Gap);
        Assert.Equal(1, channel.NativeErrorCount);
        JsonElement frame = Assert.Single(fixture.Journal.Frames, item => item.Kind == "native_error").Data;
        Assert.Equal(["capture_sequence", "error_code", "error_name", "is_error", "is_fatal", "callback_monotonic_ticks"],
            frame.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, frame.GetProperty("capture_sequence").GetInt64());
        Assert.Equal(45, frame.GetProperty("error_code").GetInt32());
        Assert.Equal("OutOfOrderSequenceNumber", frame.GetProperty("error_name").GetString());
        Assert.True(frame.GetProperty("is_error").GetBoolean());
        Assert.True(frame.GetProperty("is_fatal").GetBoolean());
        Assert.True(frame.GetProperty("callback_monotonic_ticks").GetInt64() > 0);
        string journal = await File.ReadAllTextAsync(fixture.Journal.Path, TestContext.Current.CancellationToken);
        Assert.Single(journal.Split('\n', StringSplitOptions.RemoveEmptyEntries), line => line.Contains("\"kind\":\"native_error\"", StringComparison.Ordinal));
    }

    // T7: numbering is checked by the supervisor, before and after the kill request.
    [Fact]
    public async Task Given_NativeErrorsOutOfSequence_When_SupervisorHandlesThem_Then_SequenceGapIsLatched()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        await fixture.Supervisor.HandleAsync(NativeError(1), TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.HandleAsync(NativeError(3), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_capture_gap", failure.Message);
        Assert.Contains("sequence_gap", fixture.Journal.Gaps);
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "native_error");
    }

    [Fact]
    public async Task Given_FirstNativeErrorNotNumberedOne_When_SupervisorHandlesIt_Then_SequenceGapIsLatched()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.HandleAsync(NativeError(2), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_capture_gap", failure.Message);
        Assert.Contains("sequence_gap", fixture.Journal.Gaps);
        Assert.Empty(fixture.Journal.Frames);
    }

    [Fact]
    public async Task Given_NativeErrorAfterKillRequest_When_SupervisorHandlesIt_Then_ItIsJournaled()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        await fixture.Supervisor.HandleAsync(NativeError(1), TestContext.Current.CancellationToken, afterKill: true);
        Assert.Equal(1, Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "native_error").Data.GetProperty("capture_sequence").GetInt64());
        Assert.Empty(fixture.Journal.Gaps);
    }

    // T8: a seal's native_error_count is honest only when the journal already holds those frames.
    [Fact]
    public async Task Given_ControlSealCountingAnUnjournaledNativeError_When_Loaded_Then_ControlIncomplete()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        await B10AbruptSupervisorTests.WriteControlSealAsync(fixture, 6, "original-3", nativeErrorCount: 1);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptSupervisorTests.LoadControlSealAsync(fixture));
        Assert.Equal("b10_abrupt_control_incomplete", failure.Message);
        await fixture.Supervisor.HandleAsync(NativeError(1), TestContext.Current.CancellationToken, afterKill: true);
        await B10AbruptSupervisorTests.LoadControlSealAsync(fixture);
        Assert.True(((Dictionary<string, byte[]>)typeof(B10AbruptSupervisor).GetField("_files", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Supervisor)!).ContainsKey("control-seal.json"));
    }

    [Fact]
    public async Task Given_ControlSealWithPlaceholderFatalStatus_When_Loaded_Then_ControlIncomplete()
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        await B10AbruptSupervisorTests.WriteControlSealAsync(fixture, 6, "original-3", fatalCaptureStatus: "not_implemented_pr3");
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptSupervisorTests.LoadControlSealAsync(fixture));
        Assert.Equal("b10_abrupt_control_incomplete", failure.Message);
    }

    [Theory]
    [InlineData(1, "journal")]
    [InlineData(0, "not_implemented_pr3")]
    public async Task Given_TimeoutSealWithBadFatalCoverage_When_Loaded_Then_ControlIncompleteNamesTheFatalFact(long count, string status)
    {
        await using var fixture = new B10AbruptSupervisorTests.DeadlineFixture();
        B10AbruptWire.Identity identity = B10AbruptWireProducerTests.TestOptions(fixture.Root).Identity();
        var descriptor = new { name = "x", length = 1, sha256 = B10AbruptPublisher.Hash([1]) };
        byte[] seal = B10AbruptWire.SerializeEnvelope("cohort_seal", identity, new
        {
            cohort = "timeout", inventory = descriptor, count = 1, ledger = descriptor, drain_complete = true, post_drain_statistics = descriptor,
            guard_complete = true, fatal_capture_status = status, native_error_count = count
        });
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root, "abrupt-child", "timeout-seal.json"), seal, TestContext.Current.CancellationToken);
        var load = (Task)typeof(B10AbruptSupervisor).GetMethod("LoadSealAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Supervisor, ["timeout", TestContext.Current.CancellationToken])!;
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => load);
        Assert.Equal("b10_abrupt_control_incomplete", failure.Message);
        JsonElement facts = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(failure)).GetProperty("detail").GetProperty("facts");
        Assert.Equal(0, facts.GetProperty("fatal_status_matches").GetInt32());
    }

    // T9: the count a seal reads never runs ahead of the stream. 1000 callbacks race 8 threads; a marker is enqueued right after each count read.
    [Fact]
    public async Task Given_ConcurrentCallbacksAndCountReads_When_Enqueuing_Then_EveryCountedFramePrecedesTheNextMarker()
    {
        using var server = new AnonymousPipeServerStream(PipeDirection.In);
        using var client = new AnonymousPipeClientStream(PipeDirection.Out, server.ClientSafePipeHandle);
        B10AbruptChildChannel channel = NewChannel(client);
        // The queue holds 256 messages; at most 200 are in flight, so no enqueue can fail for want of room.
        using var slots = new SemaphoreSlim(200, 200);
        var order = new List<(string Kind, long Capture, string Attempt)>();
        Task reader = Task.Run(async () =>
        {
            long expected = 2;
            while (await B10AbruptWire.ReadMessageAsync(server, expected++, TestContext.Current.CancellationToken) is { } message)
            {
                order.Add((message.Kind, message.Kind == "native_error" ? message.Data.GetProperty("capture_sequence").GetInt64() : 0,
                    message.Kind == "terminal" ? message.Data.GetProperty("attempt_id").GetString()! : ""));
                slots.Release();
            }
        }, TestContext.Current.CancellationToken);
        var markerCounts = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        using var callbacksDone = new CancellationTokenSource();
        Task markers = Task.Run(async () =>
        {
            for (int index = 0; !callbacksDone.IsCancellationRequested; index++)
            {
                await slots.WaitAsync(TestContext.Current.CancellationToken);
                string attempt = "marker-" + index.ToString(CultureInfo.InvariantCulture);
                markerCounts[attempt] = channel.NativeErrorCount;
                Assert.True(channel.TryCaptureTerminal(B10AbruptWire.Element(new
                { attempt_id = attempt, completion_source = "delivery_report", delivered = true, error_code = 0, persistence_status = 2 })));
            }
        }, TestContext.Current.CancellationToken);
        Task[] callbacks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            for (int index = 0; index < 125; index++)
            {
                await slots.WaitAsync(TestContext.Current.CancellationToken);
                Assert.True(channel.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        await Task.WhenAll(callbacks);
        await callbacksDone.CancelAsync();
        await markers;
        await channel.DisposeAsync();
        await reader;

        Assert.Null(channel.Gap);
        Assert.Equal(Enumerable.Range(1, 1000).Select(value => (long)value), order.Where(item => item.Kind == "native_error").Select(item => item.Capture));
        int violations = 0;
        foreach (var (attempt, count) in markerCounts)
        {
            int marker = order.FindIndex(item => item.Attempt == attempt);
            int frame = count == 0 ? -1 : order.FindIndex(item => item.Kind == "native_error" && item.Capture == count);
            if (marker < 0 || frame > marker) violations++;
        }
        Assert.NotEmpty(markerCounts);
        Assert.Equal(0, violations);
    }

    // T11: a callback that finds the 256-message queue full latches the existing gap, and the next written message says so.
    [Fact]
    public async Task Given_FullWriteQueue_When_NativeErrorArrives_Then_QueueFullIsLatchedAndWrittenAsCaptureGap()
    {
        var events = new BlockingStream();
        B10AbruptChildChannel channel = NewChannel(events);
        Assert.True(channel.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        await events.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        for (int index = 0; index < 256; index++) Assert.True(channel.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        Assert.False(channel.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        Assert.Equal("queue_full", channel.Gap);
        events.Release.TrySetResult();
        await channel.DisposeAsync();

        using var written = new MemoryStream(events.Bytes());
        B10AbruptWire.Message first = (await B10AbruptWire.ReadMessageAsync(written, 2, TestContext.Current.CancellationToken))!;
        B10AbruptWire.Message gap = (await B10AbruptWire.ReadMessageAsync(written, 3, TestContext.Current.CancellationToken))!;
        Assert.Equal("native_error", first.Kind);
        Assert.Equal("capture_gap", gap.Kind);
        Assert.Equal("queue_full", gap.Data.GetProperty("reason").GetString());
    }

    // T15: the error handler the host installs on the producer it builds reaches the channel, and the producer's count source reads the channel.
    [Fact]
    public async Task Given_HostBuiltProducer_When_NativeFatalErrorIsRaised_Then_ChannelCarriesEveryCallbackAndCountSourceReadsIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-fatal-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        object? runtime = null;
        B10AbruptChildChannel? channel = null;
        byte[] stream = [];
        try
        {
            B10AbruptOptions options = B10AbruptProducerTests.HostOptions(root);
            await File.WriteAllTextAsync(options.StartLineBindingPath, JsonSerializer.Serialize(new
            {
                start_line = "synthetic", broker_inventory = JsonSerializer.Serialize(new
                {
                    schema_version = 1, identity = new { run_id = options.RunId, scenario_id = "B10", configuration_id = "C1" },
                    brokers = new[] { new { name = "broker" } }, source = "kafka_metadata", source_commit = options.SourceCommit
                }), completion = "synthetic"
            }), TestContext.Current.CancellationToken);
            var events = new RecordingStream();
            channel = NewChannel(events);
            await using ServiceProvider services = B10AbruptProducerTests.ProducerHostServices(options, new B10AbruptProducerTests.UnusedTokenProvider());
            var creation = (Task)typeof(B10AbruptHost).GetMethod("CreateProducerAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [services, options, channel, null, TestContext.Current.CancellationToken])!;
            await creation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            runtime = creation.GetType().GetProperty("Result")!.GetValue(creation)!;
            B10AbruptProducer runner = B10AbruptProducerTests.RuntimeMember<B10AbruptProducer>(runtime, "Producer");
            var factory = (Func<IProducer<byte[], byte[]>>)typeof(B10AbruptProducer).GetField("producerFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runner)!;
            using IProducer<byte[], byte[]> producer = factory();

            Assert.Equal(0, NativeFatal.Raise(producer, ErrorCode.OutOfOrderSequenceNumber, "offline test"));
            using (var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
            {
                wait.CancelAfter(TimeSpan.FromSeconds(10));
                while (channel.NativeErrorCount < 1) await Task.Delay(10, wait.Token);
            }
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            long count = channel.NativeErrorCount;
            Assert.Equal(count, runner.NativeErrorCount);
            producer.Dispose();
            await channel.DisposeAsync();
            stream = events.Bytes();
            await B10AbruptProducerTests.RuntimeMember<KafkaStatisticsSession>(runtime, "Statistics").DisposeAsync();
            await B10AbruptProducerTests.RuntimeMember<RunEvidenceSession>(runtime, "Evidence").DisposeAsync();
            runtime = null;

            using var written = new MemoryStream(stream);
            var messages = new List<B10AbruptWire.Message>();
            while (await B10AbruptWire.ReadMessageAsync(written, 2 + messages.Count, TestContext.Current.CancellationToken) is { } message) messages.Add(message);
            Assert.Null(channel.Gap);
            Assert.All(messages, message => Assert.Equal("native_error", message.Kind));
            Assert.Equal(Enumerable.Range(1, messages.Count).Select(value => (long)value), messages.Select(message => message.Data.GetProperty("capture_sequence").GetInt64()));
            Assert.Equal(count, messages.Count);
            JsonElement fatal = Assert.Single(messages, message => message.Data.GetProperty("is_fatal").GetBoolean()).Data;
            Assert.Equal(45, fatal.GetProperty("error_code").GetInt32());
            Assert.Equal("OutOfOrderSequenceNumber", fatal.GetProperty("error_name").GetString());
            // The evidence session still captures the same callbacks, in the same order.
            JsonElement[] rows = (await File.ReadAllLinesAsync(Path.Combine(root, "capture", "request-lifecycle.jsonl"), TestContext.Current.CancellationToken))
                .Where(line => line.Length > 0).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
                .Where(row => row.GetProperty("event_type").GetString() == "native_error").ToArray();
            Assert.Equal(messages.Select(message => (message.Data.GetProperty("error_code").GetInt32(), message.Data.GetProperty("is_fatal").GetBoolean())),
                rows.Select(row => (row.GetProperty("error_code").GetInt32(), row.GetProperty("is_fatal").GetBoolean())));
        }
        finally
        {
            if (runtime is not null)
            {
                await B10AbruptProducerTests.RuntimeMember<KafkaStatisticsSession>(runtime, "Statistics").DisposeAsync();
                await B10AbruptProducerTests.RuntimeMember<RunEvidenceSession>(runtime, "Evidence").DisposeAsync();
            }
            if (channel is not null) await channel.DisposeAsync();
            Directory.Delete(root, true);
        }
    }

    // T16: each cohort seal carries the count as it stands when that seal is built, not a constant or a value read once.
    [Fact]
    public async Task Given_NativeErrorCountThatChangesDuringCohortRuns_When_SealsAreBuilt_Then_EachSealCarriesItsOwnCount()
    {
        long counter = 0;
        await using var fixture = new B10AbruptProducerTests.Fixture(nativeErrorCount: () => Interlocked.Read(ref counter));
        fixture.StartReports();
        // Each produce call stands for one native error callback, so the count moves while a cohort runs.
        fixture.Native.OnProduce = (_, callback) => { Interlocked.Increment(ref counter); callback(B10AbruptProducerTests.Report()); };
        fixture.Selected = _ => "pod-1-same_instance-3";
        fixture.Runner.Initialize(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options);

        await fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        long afterTimeout = Interlocked.Read(ref counter);
        await fixture.Runner.RunControlAsync(TestContext.Current.CancellationToken);
        long afterControl = Interlocked.Read(ref counter);

        Assert.Equal(2, fixture.Seals.Count);
        Assert.Equal(afterTimeout, fixture.Seals[0].GetProperty("native_error_count").GetInt64());
        Assert.Equal(afterControl, fixture.Seals[1].GetProperty("native_error_count").GetInt64());
        Assert.True(afterTimeout > 0);
        Assert.NotEqual(afterTimeout, afterControl);
        Assert.All(fixture.Seals, seal => Assert.Equal("journal", seal.GetProperty("fatal_capture_status").GetString()));
    }

    private static B10AbruptWire.Message NativeError(long sequence, bool fatal = false) => new("native_error", 2, B10AbruptWire.Element(new
    {
        capture_sequence = sequence, error_code = fatal ? 45 : -169, error_name = fatal ? "OutOfOrderSequenceNumber" : "Local_Authentication",
        is_error = true, is_fatal = fatal, callback_monotonic_ticks = 1L
    }));

    private static B10AbruptChildChannel NewChannel(Stream events) => (B10AbruptChildChannel)Activator.CreateInstance(typeof(B10AbruptChildChannel),
        BindingFlags.Instance | BindingFlags.NonPublic, null, [events, Stream.Null, 1L], CultureInfo.InvariantCulture)!;

    /// <summary>librdkafka's documented test hook, resolved from the library the process loaded (rd_kafka.h v2.16.0, rd_kafka_test_fatal_error).</summary>
    private static class NativeFatal
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int TestFatalError(IntPtr client, int error, [MarshalAs(UnmanagedType.LPStr)] string reason);

        // Accepted in test code only (decision 6750891952): the member is internal, so a Confluent.Kafka upgrade that renames it is a planned fix, not a skip.
        internal static int Raise(IProducer<byte[], byte[]> producer, ErrorCode code, string reason)
        {
            object handle = typeof(Handle).GetProperty("LibrdkafkaHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(producer.Handle)!;
            IntPtr client = ((SafeHandle)handle).DangerousGetHandle();
            string library = File.ReadLines("/proc/self/maps").Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1])
                .First(path => path.Contains("librdkafka", StringComparison.Ordinal));
            IntPtr export = NativeLibrary.GetExport(NativeLibrary.Load(library), "rd_kafka_test_fatal_error");
            return Marshal.GetDelegateForFunctionPointer<TestFatalError>(export)(client, (int)code, reason);
        }
    }

    private class RecordingStream : MemoryStream
    {
        private readonly object gate = new();
        internal byte[] Bytes() { lock (gate) return ToArray(); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { lock (gate) base.Write(buffer.Span); return ValueTask.CompletedTask; }
    }

    private sealed class BlockingStream : RecordingStream
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int blocked;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref blocked, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.ConfigureAwait(false);
            }
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}
