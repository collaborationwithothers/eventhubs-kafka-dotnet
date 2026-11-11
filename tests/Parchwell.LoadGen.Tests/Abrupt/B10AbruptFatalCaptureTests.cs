using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptFatalCaptureTests
{
    [Fact]
    public async Task Given_RealFatalNativeError_When_HostHandlerRuns_Then_JournalHoldsExactlyOneNativeErrorFrame()
    {
        using var temporary = new B10AbruptJournalTests.Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, B10AbruptJournalTests.Identity, TimeProvider.System);
        await journal.AppendAsync("child_started", B10AbruptJournalTests.Started, TestContext.Current.CancellationToken);
        await using var evidence = new RunEvidenceSession(new RunIdentity("B10", "C1", "synthetic", "first", "first", "first"),
            new RunEvidenceOptions(Path.Combine(Path.GetDirectoryName(temporary.Path)!, "capture"), 2048, 1048576, 134217728, 100, TimeSpan.FromSeconds(20)), TimeProvider.System);
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig { EnableIdempotence = true })
            .SetErrorHandler(B10AbruptHost.CreateErrorHandler(evidence, journal, TimeProvider.System)).Build();
        Assert.Equal(0, NativeFatal.Raise(producer, ErrorCode.OutOfOrderSequenceNumber, "offline test"));
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        wait.CancelAfter(TimeSpan.FromSeconds(10));
        while (journal.NativeErrorCount < 1) await Task.Delay(10, wait.Token);
        await Task.Delay(TimeSpan.FromSeconds(1), wait.Token);
        await journal.DrainAvailable(wait.Token);
        Assert.Empty(journal.Gaps);
        Assert.Equal(1, journal.NativeErrorCount);
        JsonElement frame = Assert.Single(journal.Frames, item => item.Kind == "native_error").Data;
        Assert.Equal(["capture_sequence", "error_code", "error_name", "is_error", "is_fatal", "callback_monotonic_ticks"], frame.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, frame.GetProperty("capture_sequence").GetInt64());
        Assert.Equal(45, frame.GetProperty("error_code").GetInt32());
        Assert.Equal("OutOfOrderSequenceNumber", frame.GetProperty("error_name").GetString());
        Assert.True(frame.GetProperty("is_error").GetBoolean());
        Assert.True(frame.GetProperty("is_fatal").GetBoolean());
        Assert.True(frame.GetProperty("callback_monotonic_ticks").GetInt64() > 0);
        string text = await File.ReadAllTextAsync(temporary.Path, wait.Token);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries), line => line.Contains("\"kind\":\"native_error\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_NativeErrorsOutOfSequence_When_SupervisorHandlesThem_Then_SequenceGapIsLatched()
    {
        var records = new B10AbruptSupervisorTests.Records();
        records.Add("native_error", NativeError(1)); records.Add("native_error", NativeError(3));
        var snapshot = records.Read();
        Assert.Contains("sequence_gap", snapshot.Gaps);
        Assert.Single(snapshot.Frames, frame => frame.Kind == "native_error");
    }

    [Fact]
    public void Given_FirstNativeErrorNotNumberedOne_When_SupervisorHandlesIt_Then_SequenceGapIsLatched()
    {
        var records = new B10AbruptSupervisorTests.Records(); records.Add("native_error", NativeError(2));
        var snapshot = records.Read();
        Assert.Contains("sequence_gap", snapshot.Gaps);
        Assert.DoesNotContain(snapshot.Frames, frame => frame.Kind == "native_error");
    }

    [Fact]
    public async Task Given_NativeErrorAfterKillRequest_When_SupervisorHandlesIt_Then_ItIsJournaled()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture();
        fixture.ConfigureControl();
        fixture.StartReports();
        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() =>
            fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("kill_intent", fixture.Journal.Frames[^1].Kind);
        Assert.True(fixture.Journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        await fixture.Journal.DrainAvailable(TestContext.Current.CancellationToken);
        var snapshot = B10AbruptJournal.Read(await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", "journal.jsonl"), TestContext.Current.CancellationToken));
        Assert.Equal(1, Assert.Single(snapshot.Frames, frame => frame.Kind == "native_error").Data.GetProperty("capture_sequence").GetInt64());
        Assert.Empty(snapshot.Gaps);
    }

    [Fact]
    public Task Given_ControlSealCountingAnUnjournaledNativeError_When_Loaded_Then_ControlIncomplete() => RejectCoverageAsync(1, null);

    [Fact]
    public Task Given_ControlSealWithPlaceholderFatalStatus_When_Loaded_Then_ControlIncomplete() => RejectCoverageAsync(0, "not_implemented_pr3");

    [Theory]
    [InlineData(1, "journal")]
    [InlineData(0, "not_implemented_pr3")]
    public Task Given_TimeoutSealWithBadFatalCoverage_When_Loaded_Then_ControlIncompleteNamesTheFatalFact(long count, string status) =>
        RejectCoverageAsync(count, status);

    [Fact]
    public async Task Given_ConcurrentCallbacksAndCountReads_When_Enqueuing_Then_EveryCountedFramePrecedesTheNextMarker()
    {
        using var temporary = new B10AbruptJournalTests.Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, B10AbruptJournalTests.Identity, TimeProvider.System);
        await journal.AppendAsync("child_started", B10AbruptJournalTests.Started, TestContext.Current.CancellationToken);
        var counts = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        using var callbacksDone = new CancellationTokenSource();
        Task markers = Task.Run(async () =>
        {
            for (int index = 0; !callbacksDone.IsCancellationRequested; index++)
            {
                string attempt = "marker-" + index.ToString(CultureInfo.InvariantCulture);
                counts[attempt] = journal.NativeErrorCount;
                Assert.True(journal.TryCaptureTerminal(B10AbruptJournalTests.Terminal(attempt)));
                await journal.DrainAvailable(TestContext.Current.CancellationToken);
                await Task.Yield();
            }
        }, TestContext.Current.CancellationToken);
        Task[] callbacks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            for (int index = 0; index < 125; index++)
            {
                Assert.True(journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
                await journal.DrainAvailable(TestContext.Current.CancellationToken);
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        await Task.WhenAll(callbacks); await callbacksDone.CancelAsync(); await markers;
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        Assert.Empty(journal.Gaps);
        var frames = journal.Frames;
        Assert.Equal(Enumerable.Range(1, 1000).Select(value => (long)value), frames.Where(frame => frame.Kind == "native_error").Select(frame => frame.Data.GetProperty("capture_sequence").GetInt64()));
        Assert.NotEmpty(counts);
        foreach (var (attempt, count) in counts)
        {
            long marker = Assert.Single(frames, frame => frame.Kind == "terminal" && frame.Data.GetProperty("attempt_id").GetString() == attempt).Sequence;
            if (count > 0) Assert.True(Assert.Single(frames, frame => frame.Kind == "native_error" && frame.Data.GetProperty("capture_sequence").GetInt64() == count).Sequence < marker);
        }
    }

    [Fact]
    public async Task Given_FullWriteQueue_When_NativeErrorArrives_Then_QueueFullIsLatchedAndWrittenAsCaptureGap()
    {
        using var temporary = new B10AbruptJournalTests.Temporary();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var journal = new B10AbruptJournal(temporary.Path, B10AbruptJournalTests.Identity, TimeProvider.System,
            async token => { reached.TrySetResult(); await release.Task.WaitAsync(token); });
        var start = Task.Run(() => journal.AppendAsync("child_started", B10AbruptJournalTests.Started, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await reached.Task;
        long length = new FileInfo(temporary.Path).Length;
        for (int index = 0; index < 256; index++) Assert.True(journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        Assert.False(journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        Assert.Equal(length, new FileInfo(temporary.Path).Length);
        release.TrySetResult(); await start;
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        Assert.Equal(["queue_full"], journal.Gaps);
        JsonElement gap = Assert.Single(journal.Frames, frame => frame.Kind == "gap").Data;
        Assert.Equal("queue_full", gap.GetProperty("reason").GetString());
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_HostBuiltProducer_When_NativeFatalErrorIsRaised_Then_ChannelCarriesEveryCallbackAndCountSourceReadsIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-fatal-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        object? runtime = null;
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
            await using var journal = new B10AbruptJournal(Path.Combine(root, "journal.jsonl"), options.Identity(), TimeProvider.System);
            await journal.AppendAsync("child_started", B10AbruptJournalTests.Started, TestContext.Current.CancellationToken);
            await using ServiceProvider services = B10AbruptProducerTests.ProducerHostServices(options, new B10AbruptProducerTests.UnusedTokenProvider());
            var creation = (Task)typeof(B10AbruptHost).GetMethod("CreateProducerAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [services, options, journal, null, TestContext.Current.CancellationToken])!;
            await creation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            runtime = creation.GetType().GetProperty("Result")!.GetValue(creation)!;
            B10AbruptProducer runner = B10AbruptProducerTests.RuntimeMember<B10AbruptProducer>(runtime, "Producer");
            var factory = (Func<IProducer<byte[], byte[]>>)typeof(B10AbruptProducer).GetField("producerFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runner)!;
            using IProducer<byte[], byte[]> producer = factory();
            Assert.Equal(0, NativeFatal.Raise(producer, ErrorCode.OutOfOrderSequenceNumber, "offline test"));
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); wait.CancelAfter(TimeSpan.FromSeconds(10));
            while (journal.NativeErrorCount < 1) await Task.Delay(10, wait.Token);
            await Task.Delay(TimeSpan.FromSeconds(1), wait.Token);
            long count = journal.NativeErrorCount;
            Assert.Equal(count, runner.NativeErrorCount);
            producer.Dispose(); await journal.DrainAvailable(wait.Token);
            await B10AbruptProducerTests.RuntimeMember<KafkaStatisticsSession>(runtime, "Statistics").DisposeAsync();
            await B10AbruptProducerTests.RuntimeMember<RunEvidenceSession>(runtime, "Evidence").DisposeAsync(); runtime = null;
            var messages = journal.Frames.Where(frame => frame.Kind == "native_error").ToArray();
            Assert.Empty(journal.Gaps);
            Assert.Equal(Enumerable.Range(1, messages.Length).Select(value => (long)value), messages.Select(message => message.Data.GetProperty("capture_sequence").GetInt64()));
            Assert.Equal(count, messages.Length);
            JsonElement fatal = Assert.Single(messages, message => message.Data.GetProperty("is_fatal").GetBoolean()).Data;
            Assert.Equal(45, fatal.GetProperty("error_code").GetInt32());
            Assert.Equal("OutOfOrderSequenceNumber", fatal.GetProperty("error_name").GetString());
            JsonElement[] rows = (await File.ReadAllLinesAsync(Path.Combine(root, "capture", "request-lifecycle.jsonl"), wait.Token))
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
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Given_NativeErrorCountThatChangesDuringCohortRuns_When_SealsAreBuilt_Then_EachSealCarriesItsOwnCount()
    {
        using var temporary = new B10AbruptJournalTests.Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, B10AbruptJournalTests.Identity, TimeProvider.System);
        await journal.AppendAsync("child_started", B10AbruptJournalTests.Started, TestContext.Current.CancellationToken);
        Assert.True(journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, 1));
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        long first = journal.NativeErrorCount;
        byte[] firstBytes = await File.ReadAllBytesAsync(temporary.Path, TestContext.Current.CancellationToken);
        Assert.True(journal.TryCaptureNativeError(45, "OutOfOrderSequenceNumber", true, true, 2));
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        long second = journal.NativeErrorCount;
        Assert.Equal(first, B10AbruptJournal.Read(firstBytes).Frames.Count(frame => frame.Kind == "native_error"));
        Assert.Equal(second, B10AbruptJournal.Read(await File.ReadAllBytesAsync(temporary.Path, TestContext.Current.CancellationToken)).Frames.Count(frame => frame.Kind == "native_error"));
        Assert.True(first > 0); Assert.NotEqual(first, second);
    }

    private static async Task RejectCoverageAsync(long count, string? status)
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-fatal-coverage-" + Guid.NewGuid().ToString("N"));
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Directory.GetFiles(Path.Combine(root, "first")).Where(path => Path.GetFileName(path) != "registration.json")
                .ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes, StringComparer.Ordinal);
            JsonObject termination = JsonNode.Parse(files["termination.json"])!.AsObject();
            termination["data"]!["native_error_count"] = count;
            if (status is not null && status != "journal") termination["data"]!["fatal_capture_status"] = status;
            files["termination.json"] = JsonSerializer.SerializeToUtf8Bytes(termination);
            var storage = new MemoryStorage(); var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
            var identity = B10AbruptWireProducerTests.TestOptions(root).Identity();
            await publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishFirstAsync(identity, files, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_publication_failed", failure.Message);
            Assert.Single(storage.Files);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static JsonElement NativeError(long sequence) => B10AbruptWire.Element(new
    { capture_sequence = sequence, error_code = -169, error_name = "Local_Authentication", is_error = true, is_fatal = false, callback_monotonic_ticks = 1L });
    private sealed class MemoryStorage : IB10AbruptStorage
    {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken) { Files.Add(name, bytes); return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) => Task.FromResult(Files[name]);
    }
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

}
