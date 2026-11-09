using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptProducerTests
{
    private static readonly string[] EventNames = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    [Fact]
    public async Task Given_OutputDirectoryCollision_When_Initializing_Then_ReportsCaptureGapBeforeConstructingProducer()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "abrupt-child");
        await File.WriteAllBytesAsync(path, [1], TestContext.Current.CancellationToken);

        var exception = Assert.Throws<IOException>(() => fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options));

        Assert.Equal(0, fixture.Constructions);
        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.IO.IOException", path);
    }

    [Fact]
    public async Task Given_ExistingDurableDestination_When_Writing_Then_ReportsCaptureGapAndPreservesExistingBytes()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "private-existing-output.bin");
        byte[] retained = [1, 2, 3];
        await File.WriteAllBytesAsync(path, retained, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<IOException>(() => B10AbruptProducer.WriteDurableAsync(
            path, [4, 5], TestContext.Current.CancellationToken));

        Assert.Equal(retained, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.IO.IOException", path);
    }

    [Fact]
    public async Task Given_NewDurableDestination_When_Writing_Then_RetainsExactBytes()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "new-output", "payload.bin");
        byte[] bytes = [1, 2, 3];

        await B10AbruptProducer.WriteDurableAsync(path, bytes, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Given_UnauthorizedLocalAccess_When_Reported_Then_EmitsTypeWithoutPrivateMessageOrPath()
    {
        const string path = "/private-output/synthetic-token";
        var exception = new UnauthorizedAccessException("private-access-message " + path);

        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.UnauthorizedAccessException", path);
    }

    [Fact]
    public void Given_FrozenReleaseUtc_When_InnerClockAdvancesOneDay_Then_ResumingExposesAdvancedUtc()
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var inner = new ReleaseBoundaryClock(release);
        var clock = new ReleaseWindowClock(inner);

        Assert.Equal(release, clock.FreezeUtc());
        inner.Advance(TimeSpan.FromDays(1));
        Assert.Equal(release, clock.GetUtcNow());

        clock.ResumeUtc();
        Assert.Equal(release.AddDays(1), clock.GetUtcNow());
    }

    [Fact]
    public async Task Given_SuppliedClock_When_ObservingStatistics_Then_ReceivedAtUsesItsUtc()
    {
        var received = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(received);
        await using var fixture = new Fixture(clock: clock);

        Assert.Equal(received, fixture.Observe().ReceivedAt);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(received.AddDays(1), fixture.Observe().ReceivedAt);
    }

    [Fact]
    public async Task Given_PreparedAcknowledgementHeld_When_Invoking_Then_NoNativeCallOccurs()
    {
        await using var fixture = new Fixture();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Send = (kind, _) => kind == "prepared" ? release.Task : Task.FromResult(2L);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Task<B10AbruptProducer.ProbeAttempt> operation = fixture.Runner.InvokeProbe("same_instance", [1, 2], null, 0, TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Native.Calls);
        Assert.False(operation.IsCompleted);
        release.SetResult(1);
        await operation;
        Assert.Single(fixture.Native.Calls);
    }

    [Fact]
    public async Task Given_AcceptanceAcknowledgementHeld_When_Invoking_Then_InvocationDoesNotComplete()
    {
        await using var fixture = new Fixture();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Send = (kind, _) => kind == "accepted" ? release.Task : Task.FromResult(1L);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Task<B10AbruptProducer.ProbeAttempt> operation = fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        await fixture.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(operation.IsCompleted);
        Assert.Equal(0, fixture.Releases);
        release.SetResult(2);
        await operation;
    }

    [Fact]
    public async Task Given_CallbackBeforeProduceReturns_When_Invoking_Then_GenuineTerminalPrecedesAcceptance()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["prepared", "terminal", "accepted"], fixture.Events.Select(item => item.Kind));
        Assert.Equal(2, attempt.Terminal.Value.GetProperty("persistence_status").GetInt32());
    }

    [Fact]
    public async Task Given_NoCallback_When_Invoking_Then_NoTerminalOrPersistenceIsInvented()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal);
        Assert.DoesNotContain(fixture.Events, item => item.Kind == "terminal");
    }

    [Fact]
    public async Task Given_OriginalCount_When_InvokingProbe_Then_OnlyNextMemberIsAdmitted()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);

        async Task AssertRejectedAsync(long sequence, int nativeCount)
        {
            int eventCount = fixture.Events.Count;
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe(
                "same_instance", [1], null, sequence, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_guard_failed", exception.Message);
            Assert.Equal(nativeCount, fixture.Native.Calls.Count);
            Assert.Equal(eventCount, fixture.Events.Count);
        }

        await AssertRejectedAsync(1, 0);
        var first = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.True(first.Accepted);
        await AssertRejectedAsync(2, 1);
        for (int sequence = 1; sequence < B10AbruptOptions.ProbeBurstSize; sequence++)
        {
            var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, sequence, TestContext.Current.CancellationToken);
            Assert.True(attempt.Accepted);
        }
        await AssertRejectedAsync(4, 5);
    }

    [Fact]
    public async Task Given_ExistingResend_When_InvokingAnother_Then_GuardRejectsBeforeDurableOrNativeWork()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        for (int sequence = 0; sequence < B10AbruptOptions.ProbeBurstSize; sequence++)
            await fixture.Runner.InvokeProbe("same_instance", [1], null, sequence, TestContext.Current.CancellationToken);
        var resend = await fixture.Runner.InvokeProbe("same_instance", [1], "pod-1-same_instance-3", 3, TestContext.Current.CancellationToken);
        Assert.True(resend.Accepted);
        Assert.Equal(6, fixture.Native.Calls.Count);
        int eventCount = fixture.Events.Count;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe(
            "same_instance", [1], "pod-1-same_instance-3", 3, TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        Assert.Equal(6, fixture.Native.Calls.Count);
        Assert.Equal(eventCount, fixture.Events.Count);
    }

    [Fact]
    public async Task Given_PredecessorSequenceMismatch_When_InvokingResend_Then_GuardRejectsBeforeDurableOrNativeWork()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        for (int sequence = 0; sequence < B10AbruptOptions.ProbeBurstSize; sequence++)
            await fixture.Runner.InvokeProbe("same_instance", [1], null, sequence, TestContext.Current.CancellationToken);
        Assert.Equal(5, fixture.Native.Calls.Count);
        int eventCount = fixture.Events.Count;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe(
            "same_instance", [1], "pod-1-same_instance-3", 2, TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        Assert.Equal(5, fixture.Native.Calls.Count);
        Assert.Equal(eventCount, fixture.Events.Count);
        var resend = await fixture.Runner.InvokeProbe("same_instance", [1], "pod-1-same_instance-3", 3, TestContext.Current.CancellationToken);
        Assert.True(resend.Accepted);
        Assert.Equal(6, fixture.Native.Calls.Count);
    }

    [Fact]
    public async Task Given_ProbeCallback_When_Recorded_Then_NoStageWriteUntilMarkTerminals()
    {
        using var writer = new CallbackStageWriter();
        await using var fixture = new Fixture(timeline: new B10AbruptTimeline(TimeProvider.System, "child", writer));
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) =>
        {
            writer.InCallback = true;
            try { callback(Report()); }
            finally { writer.InCallback = false; }
        };

        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);

        Assert.Equal(0, writer.CallbackWrites);
        Assert.Empty(writer.CallbackOutput.ToString());
        Assert.DoesNotContain("\"stage\":\"probe_terminal\"", writer.ToString(), StringComparison.Ordinal);
        Assert.True(attempt.CallbackTimestamp > 0);
        Assert.True(attempt.CallbackTimestamp >= attempt.InvokedTimestamp);
        fixture.Runner.MarkTerminals();
        fixture.Runner.MarkTerminals();
        Assert.Equal(-1, attempt.TerminalWrittenTimestamp);
        long beforeWritten = TimeProvider.System.GetTimestamp();
        fixture.Runner.MarkTerminals(attempt.AttemptId);
        long written = attempt.TerminalWrittenTimestamp;
        Assert.InRange(written, beforeWritten, TimeProvider.System.GetTimestamp());
        fixture.Runner.MarkTerminals();
        Assert.Equal(written, attempt.TerminalWrittenTimestamp);
        JsonElement terminal = Assert.Single(writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line["b10_abrupt_stage ".Length..])),
            entry => entry.GetProperty("stage").GetString() == "probe_terminal");
        JsonElement facts = terminal.GetProperty("facts");
        Assert.Equal(["arm", "is_original", "error_code", "persistence_status", "delivered", "produce_to_terminal_milliseconds", "produce_ticks", "callback_ticks"],
            facts.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, facts.GetProperty("arm").GetInt32());
        Assert.Equal(1, facts.GetProperty("is_original").GetInt32());
        Assert.Equal(0, facts.GetProperty("error_code").GetInt32());
        Assert.Equal(2, facts.GetProperty("persistence_status").GetInt32());
        Assert.Equal(1, facts.GetProperty("delivered").GetInt32());
        Assert.Equal((long)TimeProvider.System.GetElapsedTime(attempt.InvokedTimestamp, attempt.CallbackTimestamp).TotalMilliseconds,
            facts.GetProperty("produce_to_terminal_milliseconds").GetInt64());
        Assert.Equal(attempt.InvokedTimestamp, facts.GetProperty("produce_ticks").GetInt64());
        Assert.Equal(attempt.CallbackTimestamp, facts.GetProperty("callback_ticks").GetInt64());
    }

    [Fact]
    public async Task Given_TerminalFrame_When_Written_Then_HookRunsOnWriterAfterFrame()
    {
        using var events = new MemoryStream();
        var channel = (B10AbruptChildChannel)Activator.CreateInstance(typeof(B10AbruptChildChannel),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [events, Stream.Null, 1L], CultureInfo.InvariantCulture)!;
        var lengths = new List<long>();
        var hooksComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.TerminalWritten = _ =>
        {
            lengths.Add(events.Length);
            if (lengths.Count == 2) hooksComplete.TrySetResult();
            throw new IOException("synthetic_hook_failure");
        };
        try
        {
            JsonElement terminal = B10AbruptWire.Element(new { attempt_id = "synthetic", error_code = 0,
                persistence_status = 2, delivered = true, completion_source = "native_callback" });
            Assert.True(channel.TryCaptureTerminal(terminal));
            Assert.True(channel.TryCaptureTerminal(terminal));
            await hooksComplete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { await channel.DisposeAsync(); }

        Assert.Equal(2, lengths.Count);
        Assert.All(lengths, length => Assert.True(length > 0));
        Assert.True(lengths[1] > lengths[0]);
        Assert.Null(channel.Gap);
    }

    [Fact]
    public async Task Given_ProbeCallback_When_Recorded_Then_WritesNeitherConsoleStream()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Action<DeliveryReport<byte[], byte[]>>? callback = null;
        fixture.Native.OnProduce = (_, captured) => callback = captured;
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(callback);
        TextWriter previousOutput = Console.Out;
        TextWriter previousErrors = Console.Error;
        try
        {
            Console.SetOut(output);
            Console.SetError(errors);
            callback(Report());
        }
        finally
        {
            Console.SetError(previousErrors);
            Console.SetOut(previousOutput);
        }

        Assert.NotNull(attempt.Terminal);
        Assert.Empty(output.ToString());
        Assert.Empty(errors.ToString());
    }

    [Fact]
    public async Task Given_ReportAndAcceptanceAcknowledgementsHeld_When_Invoking_Then_ProducedAndSelectedStagesWaitForNextMark()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var clock = new ReleaseBoundaryClock(new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero));
        await using var fixture = new Fixture(clock: clock, timeline: new B10AbruptTimeline(clock, "child", writer));
        var reportSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reportRelease = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptedRelease = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Send = (kind, _) =>
        {
            if (kind == "pre_acceptance_report") { reportSent.TrySetResult(); return reportRelease.Task; }
            return kind == "accepted" ? acceptedRelease.Task : Task.FromResult(1L);
        };
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Observe();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Task operation = fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        try
        {
            await reportSent.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(["producer_constructed", "probe_prepared"], StageLines(writer).Select(StageName));
            reportRelease.SetResult(2);
            await fixture.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(operation.IsCompleted);
            Assert.Equal(["producer_constructed", "probe_prepared"], StageLines(writer).Select(StageName));
        }
        finally
        {
            reportRelease.TrySetResult(2);
            acceptedRelease.TrySetResult(3);
            await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(["producer_constructed", "probe_prepared", "probe_produced", "probe_report_selected", "probe_accepted"],
            StageLines(writer).Select(StageName));
        JsonElement selected = Assert.Single(StageLines(writer), entry => StageName(entry) == "probe_report_selected");
        Assert.Equal(1, selected.GetProperty("facts").GetProperty("report_present").GetInt32());
    }

    [Fact]
    public async Task Given_TerminalStageWriterBlocked_When_AnotherCallbackArrives_Then_CallbackRecordsWithoutWaitingForWriter()
    {
        using var writer = new BlockingTerminalWriter();
        await using var fixture = new Fixture(timeline: new B10AbruptTimeline(TimeProvider.System, "child", writer));
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        var original = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        var resend = await fixture.Runner.InvokeProbe("same_instance", [1], original.AttemptId, original.Sequence, TestContext.Current.CancellationToken);
        fixture.Runner.RecordTerminal(original, Report());
        Task mark = Task.Run(() => fixture.Runner.MarkTerminals(), TestContext.Current.CancellationToken);
        Task? callback = null;
        try
        {
            await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            callback = Task.Run(() => fixture.Runner.RecordTerminal(resend, Report()), TestContext.Current.CancellationToken);
            await callback.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.NotNull(resend.Terminal);
            Assert.False(mark.IsCompleted);
            Assert.Equal(2, fixture.Events.Count(entry => entry.Kind == "terminal"));
        }
        finally
        {
            writer.Release.Set();
            await mark.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (callback is not null) await callback.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        fixture.Runner.MarkTerminals();
        Assert.Equal(2, StageLines(writer).Count(entry => StageName(entry) == "probe_terminal"));
    }

    [Fact]
    public async Task Given_ProductionHostAndQueuedTerminals_When_FirstFrameIsWritten_Then_OnlyItsTerminalStageIsMarked()
    {
        await using var fixture = new Fixture();
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        using var events = new TerminalFrameStream();
        var channel = (B10AbruptChildChannel)Activator.CreateInstance(typeof(B10AbruptChildChannel),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [events, Stream.Null, 1L], CultureInfo.InvariantCulture)!;
        var options = HostOptions(fixture.Root);
        await File.WriteAllTextAsync(options.StartLineBindingPath, JsonSerializer.Serialize(new
        {
            start_line = "synthetic", broker_inventory = JsonSerializer.Serialize(new
            {
                schema_version = 1, identity = new { run_id = options.RunId, scenario_id = "B10", configuration_id = "C1" },
                brokers = new[] { new { name = "broker" } }, source = "kafka_metadata", source_commit = options.SourceCommit
            }), completion = "synthetic"
        }), TestContext.Current.CancellationToken);
        var tokenProvider = new UnusedTokenProvider();
        await using ServiceProvider services = ProducerHostServices(options, tokenProvider);
        object? runtime = null;
        TextWriter previousErrors = Console.Error;
        try
        {
            Console.SetError(errors);
            var creation = (Task)typeof(B10AbruptHost).GetMethod("CreateProducerAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [services, options, channel, null, TestContext.Current.CancellationToken])!;
            await creation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            runtime = creation.GetType().GetProperty("Result")!.GetValue(creation)!;
            B10AbruptProducer producer = RuntimeMember<B10AbruptProducer>(runtime, "Producer");
            var original = new B10AbruptProducer.ProbeAttempt("original", "B10/run-1/C1/1/same_instance", [1], null, 0);
            var resend = new B10AbruptProducer.ProbeAttempt("resend", original.Key, [1], original.AttemptId, 1);
            var probes = (List<B10AbruptProducer.ProbeAttempt>)typeof(B10AbruptProducer)
                .GetField("probes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(producer)!;
            probes.AddRange([original, resend]);
            // Do not initialize: the host's native producer factory must remain uncalled.
            producer.RecordTerminal(original, Report());
            await events.FirstFlush.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            producer.RecordTerminal(resend, Report());
            Assert.NotNull(original.Terminal);
            Assert.NotNull(resend.Terminal);
            Assert.Equal(-1, original.TerminalWrittenTimestamp);
            Assert.Equal(-1, resend.TerminalWrittenTimestamp);
            Assert.Empty(StageLines(errors));

            events.ReleaseFirst.TrySetResult();
            await events.SecondWrite.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            JsonElement first = Assert.Single(StageLines(errors));
            Assert.Equal("probe_terminal", StageName(first));
            Assert.Equal(1, first.GetProperty("facts").GetProperty("is_original").GetInt32());
            Assert.True(original.TerminalWrittenTimestamp > 0);
            Assert.Equal(-1, resend.TerminalWrittenTimestamp);
            Assert.Null(channel.Gap);
        }
        finally
        {
            events.ReleaseFirst.TrySetResult();
            events.ReleaseSecond.TrySetResult();
            try
            {
                await channel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                if (runtime is not null)
                {
                    await RuntimeMember<KafkaStatisticsSession>(runtime, "Statistics").DisposeAsync();
                    await RuntimeMember<RunEvidenceSession>(runtime, "Evidence").DisposeAsync();
                }
            }
            finally { Console.SetError(previousErrors); }
        }

        Assert.Equal([1, 0], StageLines(errors).Select(entry => entry.GetProperty("facts").GetProperty("is_original").GetInt32()));
        Assert.Equal(0, tokenProvider.Calls);
    }

    [Fact]
    public async Task Given_SynchronousRejection_When_Invoking_Then_NoAcceptedRecordIsWritten()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, _) => throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        Assert.Equal(["prepared", "synchronous_rejection"], fixture.Events.Select(item => item.Kind));
    }

    [Fact]
    public async Task Given_PreAcceptanceReport_When_Invoking_Then_RawFileAndExactAttributionPrecedeAccepted()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        KafkaStatisticsReport report = fixture.Observe();
        await Task.Delay(2, TestContext.Current.CancellationToken);
        await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        JsonElement handoff = Assert.Single(fixture.Events, item => item.Kind == "pre_acceptance_report").Data;
        Assert.Equal("B10", handoff.GetProperty("report_identity").GetProperty("scenario_id").GetString());
        Assert.Equal("run-1", handoff.GetProperty("report_identity").GetProperty("run_id").GetString());
        Assert.Equal("LoadGenProducer", handoff.GetProperty("report_identity").GetProperty("role").GetString());
        Assert.Equal("client", handoff.GetProperty("report_identity").GetProperty("client_id").GetString());
        Assert.Equal(1, handoff.GetProperty("report_sequence").GetInt64());
        Assert.Equal(report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture), handoff.GetProperty("report_capture_utc").GetString());
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt-child", "same_instance-pre-acceptance-report.json"), TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(report.RawJson), bytes);
        Assert.Equal(bytes.Length, handoff.GetProperty("report_length").GetInt32());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), handoff.GetProperty("report_sha256").GetString());
        Assert.Equal(["prepared", "pre_acceptance_report", "accepted"], fixture.Events.Select(item => item.Kind));
    }

    [Fact]
    public async Task Given_OverflowedCounter_When_Invoking_Then_EarlierReportCannotRepairMissingSample()
    {
        await using var fixture = new Fixture(long.MaxValue - 1);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Observe();
        fixture.Observe();
        await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(fixture.Events, item => item.Kind == "pre_acceptance_report");
        Assert.Contains(fixture.Events, item => item.Kind == "accepted");
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("broker")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    [InlineData("reset")]
    [InlineData("settings")]
    public void Given_InvalidStatistics_When_Selecting_Then_GuardFails(string invalid)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string> { ["a"] = "b" },
            new Dictionary<string, string> { ["a"] = invalid == "settings" ? "changed" : "b" });
        stats.MarkConstructed(DateTimeOffset.MinValue);
        KafkaStatisticsReport report = StatisticsReport(identity);
        if (invalid == "identity") report = report with { Identity = identity with { RunId = "other" } };
        var broker = report.Snapshot!.Brokers[0];
        if (invalid == "broker") broker = broker with { Name = "foreign" };
        if (invalid == "txerrs") broker = broker with { TransmissionErrors = 1 };
        if (invalid == "rxerrs") broker = broker with { ReceiveErrors = 1 };
        if (invalid == "reset")
        {
            stats.Observe(report with { Snapshot = report.Snapshot with { Brokers = [broker with { RequestTimeouts = 1 }] } });
        }
        stats.Observe(report with { Snapshot = report.Snapshot with { Brokers = [broker],
            TimestampMicroseconds = report.Snapshot.TimestampMicroseconds + 1, AgeMicroseconds = report.Snapshot.AgeMicroseconds + 1 } });
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AvailableCapturedStatistics_When_ReadBack_Then_RejectsObservedTransmissionError(bool transmissionError)
    {
        await using var fixture = new Fixture();
        var reportIdentity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var report = StatisticsReport(reportIdentity);
        if (transmissionError) report = report with { Snapshot = report.Snapshot! with
            { Brokers = [report.Snapshot.Brokers[0] with { TransmissionErrors = 1 }] } };
        byte[] row = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, identity = fixture.Identity, statistics_identity = report.Identity,
            session_id = report.SessionId, received_at = report.ReceivedAt, receipt_time = report.ReceivedAt,
            raw_json = report.RawJson, typed_snapshot = report.Snapshot, diagnostics = report.Diagnostics
        }, B10AbruptWire.JsonOptions);
        string path = Path.Combine(fixture.Root, "statistics.jsonl");
        await File.WriteAllBytesAsync(path, [.. row, (byte)'\n'], TestContext.Current.CancellationToken);
        Task check = B10AbruptStatistics.ValidateCapturedAsync(path, reportIdentity, ["broker"],
            new Dictionary<string, string>(), new Dictionary<string, string>(), TestContext.Current.CancellationToken);
        if (transmissionError)
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => check)).Message);
        else await check;
    }

    [Fact]
    public async Task Given_EmptyAvailableStatistics_When_ReadBack_Then_DoesNotInventASampleCompletenessGate()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "statistics.jsonl");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);
        await B10AbruptStatistics.ValidateCapturedAsync(path, new("B10", "run-1", KafkaClientRole.LoadGenProducer, "client"),
            ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Given_DroppedNativePrefix_When_LaterReportReachesSink_Then_GapRemainsSticky()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        stats.MarkConstructed(DateTimeOffset.MinValue);
        var dropped = StatisticsReport(identity);
        var later = StatisticsReport(identity);
        stats.CaptureNative(dropped.RawJson);
        stats.CaptureNative(later.RawJson);
        stats.Observe(later);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
        var valid = StatisticsReport(identity);
        stats.CaptureNative(valid.RawJson);
        stats.Observe(valid);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_NativeOversizeOrQueueOverflow_When_Selecting_Then_NoLaterReportRepairsCapture(bool oversize)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        var report = StatisticsReport(identity);
        if (oversize) stats.CaptureNative(new string('x', 1048577));
        else for (int index = 0; index < 2049; index++) stats.CaptureNative(report.RawJson);
        stats.CaptureNative(report.RawJson);
        stats.Observe(report);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Fact]
    public void Given_JoinedPostDrainPrefixAndPendingLaterCallback_When_Sealing_Then_EligiblePrefixRemainsComplete()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        var report = StatisticsReport(identity);
        DateTimeOffset drained = report.ReceivedAt.AddTicks(-1);
        stats.MarkDrainBoundary(drained, report.Snapshot!.TimestampMicroseconds - 1, 1000000);
        stats.CaptureNative(report.RawJson);
        stats.Observe(report);
        stats.CaptureNative(StatisticsReport(identity).RawJson);
        Assert.NotNull(stats.SelectPostDrain(drained));
        Assert.Equal("not_implemented_pr3", stats.SealCohort("timeout", drained).GetProperty("fatal_capture_status").GetString());
    }

    [Fact]
    public void Given_AdditionalStableQualifyingBroker_When_Selecting_Then_ExpectedSubsetPasses()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.MarkConstructed(DateTimeOffset.MinValue);
        for (int index = 0; index < 2; index++)
        {
            var report = StatisticsReport(identity);
            JsonNode raw = JsonNode.Parse(report.RawJson)!;
            JsonNode additional = raw["brokers"]!["broker"]!.DeepClone();
            additional["name"] = "additional";
            additional["nodeid"] = -1;
            additional["source"] = "configured";
            raw["brokers"]!["additional"] = additional;
            var extra = report.Snapshot!.Brokers[0] with { Name = "additional", NodeId = -1 };
            stats.Observe(report with { RawJson = raw.ToJsonString(), Snapshot = report.Snapshot with { Brokers = report.Snapshot.Brokers.Add(extra) } });
        }
        Assert.NotNull(stats.SelectPreAcceptance(DateTimeOffset.MaxValue));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_MissingSourceOrChangedQualifyingSet_When_Selecting_Then_GuardFails(bool missing)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.MarkConstructed(DateTimeOffset.MinValue);
        stats.Observe(StatisticsReport(identity));
        var next = StatisticsReport(identity);
        JsonNode raw = JsonNode.Parse(next.RawJson)!;
        if (missing) raw["brokers"]!["broker"]!.AsObject().Remove("source");
        else raw["brokers"]!["broker"]!["source"] = "internal";
        stats.Observe(next with { RawJson = raw.ToJsonString() });
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Fact]
    public void Given_ReportAtAcceptance_When_Selecting_Then_NoPreAcceptanceSampleExists()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        var report = StatisticsReport(identity);
        stats.MarkConstructed(report.ReceivedAt.AddSeconds(-1));
        stats.Observe(report);
        Assert.Null(stats.SelectPreAcceptance(report.ReceivedAt));
        Assert.NotNull(stats.SelectPreAcceptance(report.ReceivedAt.AddTicks(1)));
    }

    [Theory]
    [InlineData(PersistenceStatus.NotPersisted)]
    [InlineData(PersistenceStatus.PossiblyPersisted)]
    public async Task Given_TimeoutInDrain_When_RunningCohort_Then_RetriesOnceWithSeparateInventory(PersistenceStatus status)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.StartReports();
        var callbacks = new Queue<Action<DeliveryReport<byte[], byte[]>>>();
        fixture.Native.OnProduce = (_, callback) => callbacks.Enqueue(callback);
        int flush = 0;
        fixture.Native.OnFlush = () =>
        {
            while (callbacks.TryDequeue(out var callback)) callback(flush == 0 ? Report(ErrorCode.Local_MsgTimedOut, status) : Report());
            flush++;
        };
        await fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, fixture.Native.Calls.Count);
        Assert.Equal(fixture.Native.Calls[0].Key, fixture.Native.Calls[1].Key);
        Assert.Equal(fixture.Native.Calls[0].Value, fixture.Native.Calls[1].Value);
        var rows = await LedgerValidator.ReadValidatedAsync(Path.Combine(fixture.Root, "timeout", "ledger.jsonl"),
            Path.Combine(fixture.Root, "timeout", "manifest.json"), fixture.Identity, 8, 16384, 1048576, 1048576, TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Length);
        Assert.NotEqual(rows[0].Attempt.AttemptId, rows[1].Attempt.AttemptId);
        Assert.Single(fixture.Seals);
        Assert.Equal("not_implemented_pr3", fixture.Seals[0].GetProperty("fatal_capture_status").GetString());
    }

    [Theory]
    [InlineData(ErrorCode.NoError, PersistenceStatus.Persisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.PossiblyPersisted, false)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.Persisted, false)]
    [InlineData(ErrorCode.Local_Transport, PersistenceStatus.PossiblyPersisted, false)]
    public async Task Given_TimeoutCohortCallback_When_CheckingGuards_Then_PossiblyPersistedRequiresPermittedTimeout(
        ErrorCode error, PersistenceStatus status, bool permitted)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(error, status));
        Task cohort = fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        if (permitted)
        {
            await cohort;
            Assert.Single(fixture.Seals);
            Assert.Equal(error == ErrorCode.Local_MsgTimedOut ? 2 : 1, fixture.Native.Calls.Count);
        }
        else
        {
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => cohort)).Message);
            Assert.Empty(fixture.Seals);
            Assert.Single(fixture.Native.Calls);
        }
    }

    [Theory]
    [InlineData(ErrorCode.NoError, PersistenceStatus.Persisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.PossiblyPersisted, false)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.Persisted, false)]
    [InlineData(ErrorCode.Local_Transport, PersistenceStatus.PossiblyPersisted, false)]
    public async Task Given_ProbeCallback_When_AdmittingResend_Then_PossiblyPersistedRequiresPermittedTimeout(
        ErrorCode error, PersistenceStatus status, bool permitted)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) => callback(Report(error, status));
        var original = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(original.Terminal);
        Assert.Equal((int)status, original.Terminal.Value.GetProperty("persistence_status").GetInt32());
        Assert.Equal((int)error, original.Terminal.Value.GetProperty("error_code").GetInt32());
        Task resend = fixture.Runner.InvokeProbe("same_instance", [1], original.AttemptId, original.Sequence, TestContext.Current.CancellationToken);
        if (permitted)
        {
            await resend;
            Assert.Equal(2, fixture.Native.Calls.Count);
        }
        else
        {
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => resend)).Message);
            Assert.Single(fixture.Native.Calls);
        }
    }

    [Fact]
    public async Task Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        using var finished = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Action<DeliveryReport<byte[], byte[]>>? originalCallback = null;
        fixture.Native.OnProduce = (message, callback) =>
        {
            string key = Encoding.UTF8.GetString(message.Key);
            if (key.EndsWith("/cross_restart", StringComparison.Ordinal)) return;
            if (key.EndsWith("/same_instance", StringComparison.Ordinal) && originalCallback is null) originalCallback = callback;
            else callback(Report());
        };
        fixture.Release = _ =>
        {
            Assert.NotNull(originalCallback);
            originalCallback(Report());
            Assert.Equal(1 + B10AbruptOptions.ProbeBurstSize, fixture.Native.Calls.Count);
            return Task.FromResult(fixture.Clock.FreezeUtc());
        };
        fixture.Send = (kind, data) =>
        {
            if (kind == "accepted" && data.GetProperty("attempt_id").GetString()!.Contains("cross_restart", StringComparison.Ordinal)) finished.Cancel();
            return Task.FromResult(1L);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, finished.Token));
        Assert.Equal(1, fixture.Constructions);
        Assert.Equal(8, fixture.Native.Calls.Count);
        Assert.Equal(fixture.Native.Calls[1].Key, fixture.Native.Calls[1 + B10AbruptOptions.ProbeBurstSize].Key);
        Assert.Equal(fixture.Native.Calls[1].Value, fixture.Native.Calls[1 + B10AbruptOptions.ProbeBurstSize].Value);
        Assert.Equal(2, fixture.Seals.Count);
        Assert.Equal(0, fixture.Native.FlushAfterKill);
        Assert.Equal(0, fixture.Native.Disposals);
    }

    [Fact]
    public async Task Given_ControlArm_When_Sealed_Then_InventoryHasSixEntriesWithMemberSequences()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        fixture.Selected = _ => "pod-1-same_instance-3";
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        await fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);

        await fixture.Runner.RunControlAsync(TestContext.Current.CancellationToken);

        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "control", "manifest.json"), TestContext.Current.CancellationToken));
        JsonElement inventory = manifest.RootElement.GetProperty("data");
        Assert.Equal(6, inventory.GetProperty("inventory_count").GetInt32());
        Assert.Equal([0L, 1L, 2L, 3L, 4L, 3L], inventory.GetProperty("inventory").EnumerateArray()
            .Select(entry => entry.GetProperty("sequence").GetInt64()));
        Assert.Equal(6, fixture.Seals[1].GetProperty("count").GetInt32());

        int nativeCount = fixture.Native.Calls.Count;
        int eventCount = fixture.Events.Count;
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe(
            "same_instance", [1], "pod-1-same_instance-3", 3, TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        JsonElement facts = Detail(exception, "InvokeProbe").GetProperty("facts");
        Assert.Equal(6, facts.GetProperty("previous_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("is_original").GetInt32());
        Assert.Equal(1, facts.GetProperty("predecessor_matches").GetInt32());
        Assert.Equal(nativeCount, fixture.Native.Calls.Count);
        Assert.Equal(eventCount, fixture.Events.Count);
    }

    [Fact]
    public async Task Given_ControlArm_When_Run_Then_FiveSequentialOriginals()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        await fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        fixture.Selected = _ => "pod-1-same_instance-3";
        var accepted = Channel.CreateUnbounded<int>();
        TaskCompletionSource<long>[] acknowledgements = Enumerable.Range(0, B10AbruptOptions.ProbeBurstSize)
            .Select(_ => new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        fixture.Send = (kind, data) =>
        {
            if (kind != "accepted") return Task.FromResult(1L);
            string attemptId = data.GetProperty("attempt_id").GetString()!;
            JsonElement prepared = fixture.Events.Last(item => item.Kind == "prepared" &&
                item.Data.GetProperty("attempt_id").GetString() == attemptId).Data;
            if (prepared.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null) return Task.FromResult(1L);
            int index = prepared.GetProperty("sequence").GetInt32();
            Assert.True(accepted.Writer.TryWrite(index));
            return acknowledgements[index].Task;
        };

        Task operation = fixture.Runner.RunControlAsync(TestContext.Current.CancellationToken);
        for (int i = 0; i < B10AbruptOptions.ProbeBurstSize; i++)
        {
            Assert.Equal(i, await accepted.Reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.False(operation.IsCompleted);
            Assert.Equal(i + 1, fixture.Events.Count(item => item.Kind == "prepared"));
            Assert.Equal(1 + i + 1, fixture.Native.Calls.Count);
            acknowledgements[i].SetResult(i + 1);
        }
        await operation;

        JsonElement[] originals = fixture.Events.Where(item => item.Kind == "prepared" &&
            item.Data.GetProperty("predecessor_attempt_id").ValueKind == JsonValueKind.Null).Select(item => item.Data).ToArray();
        Assert.Equal([0L, 1L, 2L, 3L, 4L], originals.Select(entry => entry.GetProperty("sequence").GetInt64()));
        JsonElement report = Assert.Single(fixture.Events, item => item.Kind == "pre_acceptance_report").Data;
        Assert.Equal(originals[0].GetProperty("attempt_id").GetString(), report.GetProperty("attempt_id").GetString());
        JsonElement resend = Assert.Single(fixture.Events, item => item.Kind == "prepared" &&
            item.Data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null).Data;
        Assert.Equal(originals[3].GetProperty("attempt_id").GetString(), resend.GetProperty("predecessor_attempt_id").GetString());
        Assert.Equal(3, resend.GetProperty("sequence").GetInt32());
        Assert.Equal("3", Encoding.UTF8.GetString(fixture.Native.Calls[^1].Headers.GetLastBytes("sequence")));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly(long sequence)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        await using var fixture = new Fixture(clock: new ReleaseBoundaryClock(release));
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        byte[] payload = [1, 2, 3];
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-4", key = "B10/run-1/C1/1/cross_restart", sequence,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });

        Task<B10AbruptProducer.SuccessorArtifacts> operation = fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity,
            fixture.Options, selection, release, TestContext.Current.CancellationToken);

        if (sequence == 5)
        {
            Assert.Equal("b10_abrupt_successor_binding_invalid", (await Assert.ThrowsAsync<InvalidDataException>(() => operation)).Message);
            Assert.Empty(fixture.Native.Calls);
            return;
        }
        B10AbruptProducer.SuccessorArtifacts artifacts = await operation;
        Assert.Equal("4", Encoding.UTF8.GetString(Assert.Single(fixture.Native.Calls).Headers.GetLastBytes("sequence")));
        Assert.Equal(sequence, artifacts.Invocations[0].GetProperty("sequence").GetInt64());
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(artifacts.Paths["manifest.json"], TestContext.Current.CancellationToken));
        Assert.Equal(sequence, manifest.RootElement.GetProperty("data").GetProperty("inventory")[0].GetProperty("sequence").GetInt64());
    }

    [Fact]
    public async Task Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        fixture.Native.OnProduce = (message, callback) =>
        {
            if (!Encoding.UTF8.GetString(message.Key).EndsWith("same_instance", StringComparison.Ordinal)) callback(Report());
        };
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        Assert.DoesNotContain(fixture.Native.Calls, call => Encoding.UTF8.GetString(call.Key).EndsWith("cross_restart", StringComparison.Ordinal));
        Assert.Single(fixture.Seals);
        JsonElement facts = Detail(exception, "SealProbeCohortAsync").GetProperty("facts");
        Assert.Equal(["arm", "entry_count", "expected_count", "no_terminal_count"], facts.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, facts.GetProperty("arm").GetInt32());
        Assert.Equal(B10AbruptOptions.ProbeBurstSize + 1, facts.GetProperty("entry_count").GetInt32());
        Assert.Equal(B10AbruptOptions.ProbeBurstSize + 1, facts.GetProperty("expected_count").GetInt32());
        Assert.Equal(B10AbruptOptions.ProbeBurstSize + 1, facts.GetProperty("no_terminal_count").GetInt32());
    }

    [Fact]
    public async Task Given_OutstandingMessages_When_DrainBudgetExpires_Then_DiagnosticNamesOutstandingCountAndBudget()
    {
        var clock = new ManualClock();
        await using var fixture = new Fixture(clock: clock);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.FlushResult = 3;
        fixture.Native.OnFlush = () => clock.Advance(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateAsync(fixture.Runner,
            "DrainAsync", TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        JsonElement detail = Detail(exception, "DrainAsync");
        Assert.Equal(3, detail.GetProperty("facts").GetProperty("outstanding_count").GetInt32());
        Assert.Equal(1000, detail.GetProperty("facts").GetProperty("budget_milliseconds").GetInt32());
    }

    [Fact]
    public async Task Given_NoReportAfterDrainBoundary_When_WaitExpires_Then_DiagnosticNamesEachSelectionPredicate()
    {
        var clock = new ManualClock();
        await using var fixture = new Fixture(clock: clock);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        fixture.Statistics.Observe(StatisticsReport(identity, clock));
        clock.Advance(TimeSpan.FromSeconds(1));
        DateTimeOffset drained = clock.GetUtcNow();
        Task operation = InvokePrivateAsync(fixture.Runner, "WaitForPostDrainAsync", 0, drained, TestContext.Current.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => operation);

        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        JsonElement facts = Detail(exception, "WaitForPostDrainAsync").GetProperty("facts");
        Assert.Equal(1, facts.GetProperty("report_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("snapshot_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("after_drain_utc_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("after_native_boundary_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("expected_brokers_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("qualifying_brokers_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("overflow").GetInt32());
        Assert.Equal(1, facts.GetProperty("drain_boundary_matches").GetInt32());
    }

    [Fact]
    public async Task Given_DuplicateTimeoutCallback_When_CheckingGuards_Then_DiagnosticNamesDuplicateCallbackReason()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) => { callback(Report()); callback(Report()); };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        Assert.Equal(1, Detail(exception, "CheckGuards").GetProperty("facts").GetProperty("fault_reason").GetInt32());
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_MsgTimedOut)]
    public async Task Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains(ErrorCode outcome)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(release);
        await using var fixture = new Fixture(clock: clock);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(outcome, outcome == ErrorCode.NoError ? PersistenceStatus.Persisted : PersistenceStatus.PossiblyPersisted));
        byte[] payload = [1, 2, 3];
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        var artifacts = await fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity, fixture.Options,
            selection, release, TestContext.Current.CancellationToken);
        Assert.Single(fixture.Native.Calls);
        Assert.Equal(payload, fixture.Native.Calls[0].Value);
        Assert.Equal(1, fixture.Native.Disposals);
        Assert.Equal(5, artifacts.Paths.Count);
        Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(artifacts.Paths["manifest.json"], TestContext.Current.CancellationToken));
        Assert.Equal(2, manifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("b10_abrupt", manifest.RootElement.GetProperty("dialect").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("data").GetProperty("inventory_count").GetInt32());
        Assert.Equal("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("predecessor_attempt_id").GetString());
        Assert.NotEqual("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("attempt_id").GetString());
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_Fail)]
    public async Task Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard(ErrorCode code)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        await using var fixture = new Fixture(clock: new ReleaseBoundaryClock(release), captureEvidence: true);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        fixture.Native.OnDispose = () => fixture.Evidence!.CaptureError(new Error(code, "synthetic disposal callback"));
        byte[] payload = [1, 2, 3];
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        Task<B10AbruptProducer.SuccessorArtifacts> operation = fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity,
            fixture.Options, selection, release, TestContext.Current.CancellationToken);
        if (code == ErrorCode.NoError)
        {
            var artifacts = await operation;
            string captured = await File.ReadAllTextAsync(artifacts.Paths["request-lifecycle.jsonl"], TestContext.Current.CancellationToken);
            Assert.Contains("synthetic disposal callback", captured, StringComparison.Ordinal);
            Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        }
        else
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
            Assert.Equal("b10_abrupt_guard_failed", failure.Message);
            Assert.Contains("native_error_callback_observed", fixture.Evidence!.GuardObservations);
            Assert.False(File.Exists(Path.Combine(fixture.Root, "successor", "capabilities.json")));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "successor", "request-lifecycle.jsonl")));
        }
        Assert.Equal(1, fixture.Native.Disposals);
        Assert.Equal(1, fixture.DisposalsAtEvidenceCompletion);
        Assert.False(fixture.Evidence!.IsFaulted);
        Assert.DoesNotContain("error_capture_unavailable", fixture.Evidence.Diagnostics);
        string retained = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "capture", "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        Assert.Contains("synthetic disposal callback", retained, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ReadinessAroundRelease_When_Replaying_Then_NeverInvokesEarlyOrAfterLateReadiness(int ticksFromRelease)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(release.AddTicks(ticksFromRelease));
        await using var fixture = new Fixture(clock: clock);
        fixture.Native.OnProduce = (_, _) => throw new InvalidOperationException("synthetic_invocation_observed");
        byte[] payload = [1];
        JsonElement selected = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = 1, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        Task operation = Task.Run(() => fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity,
            fixture.Options, selected, release, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        if (ticksFromRelease < 0)
        {
            await clock.WaitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Empty(fixture.Native.Calls);
            Assert.False(operation.IsCompleted);
            clock.Advance(TimeSpan.FromTicks(1));
        }
        if (ticksFromRelease > 0)
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
            Assert.Equal("b10_abrupt_successor_late", failure.Message);
            Assert.Empty(fixture.Native.Calls);
        }
        else
        {
            var observation = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
            Assert.Equal("synthetic_invocation_observed", observation.Message);
            Assert.Single(fixture.Native.Calls);
            Assert.Equal(release, clock.GetUtcNow());
        }
    }

    [Fact]
    public async Task Given_MissingPersistenceOnGenuineCallback_When_Running_Then_GuardBlocksControl()
    {
        await using var fixture = new Fixture();
        fixture.Native.OnProduce = (_, callback) => callback(Report(status: null));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunFirstAsync(
            Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", error.Message);
        Assert.Empty(fixture.Seals);
        Assert.Single(fixture.Native.Calls);
    }

    [Fact]
    public async Task Given_CallbackAfterAcceptance_When_Recording_Then_PreservesTerminalForFinalReconciliation()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Action<DeliveryReport<byte[], byte[]>>? callback = null;
        fixture.Native.OnProduce = (_, captured) => callback = captured;
        var attempt = await fixture.Runner.InvokeProbe("cross_restart", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal);
        Assert.NotNull(callback);
        callback(Report());
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["prepared", "accepted", "terminal"], fixture.Events.Select(item => item.Kind));
    }

    private static IEnumerable<JsonElement> StageLines(StringWriter writer) => writer.ToString()
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line["b10_abrupt_stage ".Length..]));

    private static string? StageName(JsonElement entry) => entry.GetProperty("stage").GetString();

    private static T RuntimeMember<T>(object runtime, string name) => (T)runtime.GetType().GetProperty(name)!.GetValue(runtime)!;

    private static B10AbruptOptions HostOptions(string root)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = "child", ["RunId"] = "run-1",
            ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "image@sha256:" + new string('b', 64),
            ["JobName"] = "run-1-loadgen", ["PodName"] = "pod-1", ["PodUid"] = "uid-1", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = AppContext.BaseDirectory,
            ["OutputRoot"] = root, ["StartLineBindingPath"] = Path.Combine(root, "start-line.json"),
            ["EventsPipeHandle"] = "4", ["AckPipeHandle"] = "5", ["IdentityChallenge"] = new('c', 64)
        };
        foreach (var policy in B10AbruptOptions.Policy) values[policy.Key] = policy.Value.ToString(CultureInfo.InvariantCulture);
        return B10AbruptOptions.Read(values.ToDictionary(pair => B10AbruptOptions.Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static ServiceProvider ProducerHostServices(B10AbruptOptions options, UnusedTokenProvider tokenProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        typeof(B10AbruptHost).GetMethod("AddB10KafkaRoles", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, options]);
        services.AddParchwellScenarios(value =>
        { value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32; value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000; });
        services.AddSingleton<IScenarioSource>(new FileSystemScenarioSource(options.ScenarioSourceRoot));
        services.AddSingleton<RunEvidenceSinkRouter>();
        services.AddSingleton<IKafkaStatisticsEvidenceSink>(provider => provider.GetRequiredService<RunEvidenceSinkRouter>());
        services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        services.AddParchwellKafkaAuthentication(value =>
        { value.TenantId = "synthetic-tenant"; value.ClientId = "synthetic-client"; value.TokenFilePath = Path.Combine(options.OutputRoot, "missing-token"); });
        services.AddParchwellKafkaStatistics(value =>
        { value.MaxSnapshotCharacters = 1048576; value.QueueCapacity = 2048; value.MaxBrokerSeries = 64; value.CompletionTimeout = TimeSpan.FromSeconds(1); });
        return services.BuildServiceProvider();
    }

    private static JsonElement Detail(Exception exception, string member)
    {
        JsonElement detail = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(exception)).GetProperty("detail");
        Assert.Equal("B10AbruptProducer.cs", detail.GetProperty("file").GetString());
        Assert.Equal(member, detail.GetProperty("member").GetString());
        return detail;
    }

    private static Task InvokePrivateAsync(B10AbruptProducer runner, string member, params object[] arguments) =>
        (Task)typeof(B10AbruptProducer).GetMethod(member, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, arguments)!;

    private static void AssertCaptureGapReport(Exception exception, string expectedLine, string privatePath)
    {
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), expectedLine);
        Assert.DoesNotContain(privatePath, errors.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(exception.Message, errors.ToString(), StringComparison.Ordinal);
    }

    internal static DeliveryReport<byte[], byte[]> Report(ErrorCode code = ErrorCode.NoError, PersistenceStatus? status = PersistenceStatus.Persisted) => new()
    { Error = new Error(code), Status = status ?? (PersistenceStatus)(-1) };

    internal static KafkaStatisticsReport StatisticsReport(KafkaStatisticsIdentity identity, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        long timestamp = checked((long)((Int128)clock.GetTimestamp() * 1_000_000 / clock.TimestampFrequency));
        return new(identity, "session", clock.GetUtcNow(), JsonSerializer.Serialize(new
        {
            name = "native", client_id = identity.ClientId, type = "producer", ts = timestamp, time = 100, age = timestamp,
            brokers = new Dictionary<string, object>
            {
                ["broker"] = new { name = "broker", nodeid = 0, source = "learned", state = "UP", stateage = 0,
                    req_timeouts = 0, disconnects = 0, txretries = 0, txerrs = 0, rxerrs = 0,
                    throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 } }
            }
        }), new KafkaStatisticsSnapshot("native", identity.ClientId,
            "producer", timestamp, 100, timestamp, [new KafkaBrokerStatistics("broker", 0, "UP", 0, 0, 0, 0, 0, 0,
                new KafkaWindowStatistics(1, 0), new KafkaWindowStatistics(1, 0))]), []);
    }

    internal static WorkloadProfile Profile() => new(1, "test", 7, [new TenantWeight("tenant", 1)], 1,
        EventNames.ToImmutableDictionary(
            name => name, _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal),
        [new LoadStage("one", 1, 1)], new SourceLocation("synthetic"));

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "abrupt-producer-" + Guid.NewGuid().ToString("N"));
        internal RunIdentity Identity { get; } = new("B10", "C1", "run-1", "pod-1", "pod-1", "client");
        internal RunExecutionOptions Options { get; }
        internal B10AbruptProducer Runner { get; }
        internal ReleaseWindowClock Clock { get; }
        internal B10AbruptStatistics Statistics { get; }
        internal NativeProducer Native { get; }
        internal RunEvidenceSession? Evidence { get; }
        internal int DisposalsAtEvidenceCompletion { get; private set; }
        internal List<(string Kind, JsonElement Data)> Events { get; } = [];
        internal List<JsonElement> Seals { get; } = [];
        internal Func<string, JsonElement, Task<long>> Send { get; set; } = (_, _) => Task.FromResult(1L);
        internal Func<string, Task<DateTimeOffset>> Release { get; set; }
        internal Func<string, string?> Selected { get; set; } = arm => "pod-1-" + arm + "-0";
        internal TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Constructions { get; private set; }
        internal int Releases { get; private set; }
        private readonly CancellationTokenSource reportStop = new();
        private Task? reportTask;
        private readonly KafkaStatisticsIdentity statisticsIdentity = new("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");

        internal Fixture(long sequence = 0, TimeProvider? clock = null, bool captureEvidence = false, B10AbruptTimeline? timeline = null)
        {
            Clock = new ReleaseWindowClock(clock ?? TimeProvider.System);
            Release = _ => Task.FromResult(Clock.FreezeUtc());
            Directory.CreateDirectory(Root);
            Options = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1), 16384, 1048576, 1048576, Root, "document-events", ["broker"], false,
                ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
            Statistics = new B10AbruptStatistics(statisticsIdentity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), initialSequence: sequence);
            IProducer<byte[], byte[]> native = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
            Native = (NativeProducer)(object)native;
            if (captureEvidence)
                Evidence = new RunEvidenceSession(Identity,
                    new RunEvidenceOptions(Path.Combine(Root, "capture"), 16, 16384, 1048576, 16, TimeSpan.FromSeconds(1)),
                    Clock);
            var wireIdentity = new B10AbruptWire.Identity("run-1", "B10", "C1", "C1", 1, new string('a', 40),
                "image@sha256:" + new string('b', 64), "run-1-loadgen", "pod-1", "uid-1", "pod-1", null);
            Runner = new B10AbruptProducer(() => { Constructions++; return native; }, Clock, Statistics,
                cohort => new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(Root, cohort, "ledger.jsonl"),
                    Path.Combine(Root, cohort, "manifest.json"), 16, 16384, 1048576, 1048576, TimeSpan.FromSeconds(1))),
                async (kind, data, _) =>
                {
                    if (kind == "prepared" && data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null)
                        Clock.ResumeUtc();
                    Events.Add((kind, data));
                    if (kind == "accepted") Accepted.TrySetResult();
                    return await Send(kind, data);
                }, data => { Events.Add(("terminal", data)); return true; },
                async (arm, _) => { Releases++; return new(await Release(arm), Selected(arm)); },
                (_, seal, _) => { Seals.Add(seal); return Task.CompletedTask; }, evidence: Evidence, diagnosticIdentity: wireIdentity,
                completeEvidence: Evidence is null ? null : async token =>
                {
                    DisposalsAtEvidenceCompletion = Native.Disposals;
                    await Evidence.CompleteAsync(null, token);
                    return Evidence.ArtifactNames.ToDictionary(name => name, name => Path.Combine(Root, "capture", name), StringComparer.Ordinal);
                }, timeline: timeline);
        }

        internal KafkaStatisticsReport Observe()
        {
            var report = StatisticsReport(statisticsIdentity) with { ReceivedAt = Clock.GetUtcNow() };
            Statistics.Observe(report);
            return report;
        }

        internal void StartReports() => reportTask = Task.Run(async () =>
        {
            try
            {
                while (!reportStop.IsCancellationRequested)
                {
                    Observe();
                    await Task.Delay(2, reportStop.Token);
                }
            }
            catch (OperationCanceledException) when (reportStop.IsCancellationRequested) { }
        }, TestContext.Current.CancellationToken);

        public async ValueTask DisposeAsync()
        {
            await reportStop.CancelAsync();
            if (reportTask is not null) await reportTask;
            reportStop.Dispose();
            if (Evidence is not null) await Evidence.DisposeAsync();
            Directory.Delete(Root, true);
        }
    }

    private sealed class UnusedTokenProvider : IKafkaTokenProvider
    {
        internal int Calls { get; private set; }
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("synthetic_token_provider_must_not_run");
        }
    }

    private sealed class TerminalFrameStream : MemoryStream
    {
        private int flushes;
        private int writes;
        internal TaskCompletionSource FirstFlush { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseSecond { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref flushes) == 1) { FirstFlush.TrySetResult(); return ReleaseFirst.Task; }
            return base.FlushAsync(cancellationToken);
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref writes) == 3)
            {
                SecondWrite.TrySetResult();
                await ReleaseSecond.Task.ConfigureAwait(false);
            }
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class BlockingTerminalWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public override void WriteLine(string? value)
        {
            if (value?.Contains("\"stage\":\"probe_terminal\"", StringComparison.Ordinal) == true)
            {
                Entered.TrySetResult();
                Release.Wait();
            }
            base.WriteLine(value);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Release.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class CallbackStageWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
        internal bool InCallback { get; set; }
        internal int CallbackWrites { get; private set; }
        internal StringBuilder CallbackOutput { get; } = new();
        public override void WriteLine(string? value)
        {
            if (InCallback) { CallbackWrites++; CallbackOutput.AppendLine(value); }
            base.WriteLine(value);
        }
    }

    private sealed class ReleaseWindowClock(TimeProvider inner) : TimeProvider
    {
        private readonly object gate = new();
        private DateTimeOffset? frozenUtc;
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            inner.CreateTimer(callback, state, dueTime, period);
        public override DateTimeOffset GetUtcNow()
        {
            lock (gate) return frozenUtc ?? inner.GetUtcNow();
        }
        internal DateTimeOffset FreezeUtc()
        {
            lock (gate)
            {
                frozenUtc = inner.GetUtcNow();
                return frozenUtc.Value;
            }
        }
        internal void ResumeUtc()
        {
            lock (gate) frozenUtc = null;
        }
    }

    private sealed class ReleaseBoundaryClock(DateTimeOffset initialUtc) : TimeProvider
    {
        private long ticks;
        private int timestampReads;
        internal TaskCompletionSource WaitObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => initialUtc.AddTicks(Interlocked.Read(ref ticks));
        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref timestampReads) >= 3) WaitObserved.TrySetResult();
            return Interlocked.Read(ref ticks);
        }
        internal void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = [];
        private readonly object gate = new();
        private readonly Channel<TimeSpan> installedTimers = Channel.CreateUnbounded<TimeSpan>();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            installedTimers.Writer.TryWrite(dueTime);
            return timer;
        }
        internal async Task WaitForTimerAsync(TimeSpan dueTime)
        {
            while (await installedTimers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken) != dueTime) { }
        }
        internal void Advance(TimeSpan elapsed)
        {
            Interlocked.Add(ref ticks, elapsed.Ticks);
            ManualTimer[] snapshot; lock (gate) snapshot = timers.ToArray();
            foreach (var timer in snapshot) timer.Fire(GetTimestamp());
        }
        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { Interlocked.Exchange(ref due, dueTime < TimeSpan.Zero ? long.MaxValue : checked(clock.GetTimestamp() + dueTime.Ticks)); return true; }
            internal void Fire(long now) { long target = Interlocked.Read(ref due); if (now >= target && Interlocked.CompareExchange(ref due, long.MaxValue, target) == target) callback(state); }
            public void Dispose() => Interlocked.Exchange(ref due, long.MaxValue);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    public class NativeProducer : DispatchProxy
    {
        internal List<Message<byte[], byte[]>> Calls { get; } = [];
        internal Action<Message<byte[], byte[]>, Action<DeliveryReport<byte[], byte[]>>> OnProduce { get; set; } = (_, _) => { };
        internal Action OnFlush { get; set; } = () => { };
        internal Action OnDispose { get; set; } = () => { };
        internal int Disposals { get; private set; }
        internal int FlushAfterKill { get; private set; }
        internal int FlushResult { get; set; }
        private bool killed;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Produce":
                    var message = (Message<byte[], byte[]>)args![1]!;
                    Calls.Add(message);
                    killed |= Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal);
                    OnProduce(message, (Action<DeliveryReport<byte[], byte[]>>)args[2]!);
                    return null;
                case "Flush":
                    if (killed) FlushAfterKill++;
                    OnFlush();
                    return FlushResult;
                case "Dispose": Disposals++; OnDispose(); return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
