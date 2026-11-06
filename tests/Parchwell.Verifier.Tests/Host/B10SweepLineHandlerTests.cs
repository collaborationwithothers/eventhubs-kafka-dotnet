using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class B10SweepLineHandlerTests
{
    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_MetadataHighs_When_Captured_Then_RealReaderAcceptsSortedHighsAndExactReceiptBinding(string phase)
    {
        using var fixture = new CaptureFixture(phase);
        fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(1, 0);
        fixture.Kafka.Query = partition => { fixture.Kafka.Clock.Advance(1); return new WatermarkOffsets(0, partition.Partition.Value == 0 ? 0 : 9); };
        Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose", "note"], fixture.Events);
        B10SweepLine line = B10SweepEvidenceCodec.ReadSweepLine(fixture.Bytes!);
        Assert.Equal([0, 1], line.PartitionHighs.Select(partition => partition.Partition));
        Assert.Equal([0L, 9L], line.PartitionHighs.Select(partition => partition.High));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMilliseconds(2), line.CapturedAt);
        Assert.Equal(fixture.Options.SourceCommit, line.SourceCommit);
        Assert.Equal(fixture.Options.Phase, line.Phase);
        Assert.Equal(fixture.Options.ConfigurationId, line.ConfigurationId);
        Assert.Equal(fixture.Options.RunId, line.RunId);
        Assert.True(B10SweepEvidenceCodec.MatchesSweepLine(line, fixture.Options, line.CapturedAt, [DateTimeOffset.UnixEpoch]));
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _, out _, out _));
        WorkerReceipt receipt = fixture.Receipt();
        Assert.Equal("b10-sweep-line", receipt.Mode); Assert.Equal(fixture.Options.JobName, receipt.JobName);
        Assert.Null(receipt.Identity); Assert.Null(receipt.Repeat); Assert.Null(receipt.PublicationAcceptance); Assert.Null(receipt.RepeatChecks); Assert.Null(receipt.B10Intake);
        Assert.Equal(0, receipt.HandlerExitCode); Assert.True(receipt.CleanupCompleted);
        ArtifactReference reference = Assert.Single(receipt.Artifacts);
        Assert.Equal("verifier", reference.Container); Assert.Equal(PhasePaths.SweepLine(fixture.Options.RunId!, phase), reference.Name);
        Assert.Equal(fixture.Bytes!.LongLength, reference.Length); Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture.Bytes)), reference.Sha256);
        Assert.Equal(1, fixture.Creates); Assert.Equal(2, fixture.Kafka.QueryTimeouts.Count);
    }

    [Theory]
    [InlineData("verifier", -1, true)]
    [InlineData("verifier", 0, true)]
    [InlineData("verifier", 1, false)]
    [InlineData("loadgen", -1, true)]
    [InlineData("loadgen", 0, true)]
    [InlineData("loadgen", 1, false)]
    public async Task Given_LifecycleTimeAtBoundary_When_Captured_Then_CodecRequiresElapsedSettleAndCompletedVerifier(string field, int offset, bool accepted)
    {
        using var fixture = new CaptureFixture();
        if (field == "verifier") fixture.Options.B10LastVerifierCompletedUtc = Utc(DateTimeOffset.UnixEpoch.AddMilliseconds(offset));
        else fixture.Options.B10LastLoadgenTerminatedUtc = Utc(DateTimeOffset.UnixEpoch.AddMilliseconds(-60000 + offset));
        if (accepted) { Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken)); Assert.NotNull(B10SweepEvidenceCodec.ReadSweepLine(fixture.Bytes!)); }
        else { await Assert.ThrowsAsync<FormatException>(() => fixture.Run(TestContext.Current.CancellationToken)); fixture.AssertNoReceipt(); }
        Assert.Equal(accepted ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("negative-partition")]
    [InlineData("negative-high")]
    [InlineData("partitions")]
    [InlineData("output")]
    public async Task Given_InvalidMetadataOrLimit_When_Captured_Then_CleansWithoutPublishing(string invalid)
    {
        using var fixture = new CaptureFixture();
        switch (invalid)
        {
            case "empty": fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(); break;
            case "duplicate": fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(0, 0); break;
            case "negative-partition": fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(-1); break;
            case "negative-high": fixture.Kafka.Query = _ => new WatermarkOffsets(0, -1); break;
            case "partitions": fixture.Options.MaxPartitions = 1; fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(0, 1); break;
            case "output": fixture.Options.MaxOutputBytes = 1; break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Run(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events); Assert.Equal(0, fixture.Creates); fixture.AssertNoReceipt();
    }

    [Theory]
    [InlineData("token")]
    [InlineData("metadata")]
    [InlineData("watermark")]
    [InlineData("admin-cleanup")]
    [InlineData("consumer-cleanup")]
    [InlineData("cleanup-cancel")]
    [InlineData("work-cancel")]
    [InlineData("upload-failure")]
    [InlineData("upload-cancel")]
    public async Task Given_OperationFailureOrCancellation_When_Captured_Then_NeverQueuesFinalReceipt(string failure)
    {
        using var fixture = new CaptureFixture(); using var work = new CancellationTokenSource();
        switch (failure)
        {
            case "token": fixture.Kafka.Poll = () => throw new IOException("synthetic token failure"); break;
            case "metadata": fixture.Kafka.ReadMetadata = () => throw new IOException("synthetic metadata failure"); break;
            case "watermark": fixture.Kafka.Query = _ => throw new IOException("synthetic query failure"); break;
            case "admin-cleanup": fixture.Kafka.AdminDispose = () => throw new IOException("synthetic cleanup failure"); break;
            case "consumer-cleanup": fixture.Kafka.ConsumerDispose = () => throw new IOException("synthetic cleanup failure"); break;
            case "cleanup-cancel": fixture.Kafka.AdminDispose = () => fixture.Kafka.Clock.Advance(100); break;
            case "work-cancel": fixture.Kafka.Query = _ => { work.Cancel(); return new WatermarkOffsets(0, 0); }; break;
            case "upload-failure": fixture.AfterCreate = _ => throw new IOException("synthetic uncertain create"); break;
            case "upload-cancel": fixture.AfterCreate = token => { fixture.Kafka.Clock.Advance(100); token.ThrowIfCancellationRequested(); }; break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Run(work.Token)); fixture.AssertNoReceipt();
        Assert.Equal(1, fixture.Kafka.Events.Count(entry => entry == "admin-dispose"));
        Assert.Equal(1, fixture.Kafka.Events.Count(entry => entry == "consumer-dispose"));
        Assert.Equal(failure.StartsWith("upload", StringComparison.Ordinal) ? 1 : 0, fixture.Creates);
        if (fixture.Creates == 1) Assert.NotNull(fixture.Bytes);
    }

    [Theory]
    [InlineData("cleanup", 99, true)]
    [InlineData("cleanup", 100, false)]
    [InlineData("cleanup", 101, false)]
    [InlineData("upload", 99, true)]
    [InlineData("upload", 100, false)]
    [InlineData("upload", 101, false)]
    public async Task Given_DelayedTimerCallbacks_When_BudgetReached_Then_MonotonicDeadlineSuppressesSuccess(string operation, int elapsed, bool accepted)
    {
        using var fixture = new CaptureFixture(); fixture.Clock = new DelayedClock(fixture.Kafka.Clock);
        if (operation == "cleanup") fixture.Kafka.AdminDispose = () => fixture.Kafka.Clock.Advance(elapsed);
        else fixture.AfterCreate = _ => fixture.Kafka.Clock.Advance(elapsed);
        if (accepted) { Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken)); Assert.NotNull(fixture.Receipt()); }
        else { await Assert.ThrowsAsync<TimeoutException>(() => fixture.Run(TestContext.Current.CancellationToken)); fixture.AssertNoReceipt(); }
        Assert.Equal(accepted || operation == "upload" ? 1 : 0, fixture.Creates);
    }

    [Fact]
    public async Task Given_DestinationExists_When_Captured_Then_OneCreateAttemptPreservesExistingObjectAndNoReceipt()
    {
        using var fixture = new CaptureFixture(); byte[] existing = [1, 2, 3]; fixture.Bytes = existing;
        await Assert.ThrowsAsync<IOException>(() => fixture.Run(TestContext.Current.CancellationToken));
        Assert.Same(existing, fixture.Bytes); Assert.Equal(1, fixture.Creates); fixture.AssertNoReceipt();
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("scenario_id")]
    [InlineData("configuration_id")]
    [InlineData("phase")]
    [InlineData("source_commit")]
    [InlineData("repeat")]
    [InlineData("identity")]
    [InlineData("job_name")]
    [InlineData("cleanup_completed")]
    [InlineData("handler_exit_code")]
    [InlineData("artifacts")]
    [InlineData("container")]
    [InlineData("name")]
    [InlineData("length")]
    [InlineData("sha256")]
    [InlineData("repeat_checks")]
    [InlineData("publication_acceptance")]
    [InlineData("b10_intake")]
    public async Task Given_WriterReceipt_When_WireBindingIsCorrupted_Then_StrictReaderRejectsIt(string field)
    {
        using var fixture = new CaptureFixture(); Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken));
        WorkerReceipt receipt = fixture.Receipt(); JsonObject json = JsonNode.Parse(PhaseWireCodec.WriteReceipt(receipt))!.AsObject();
        switch (field)
        {
            case "schema_version": json[field] = 2; break;
            case "scenario_id": json[field] = "B04"; break;
            case "configuration_id": json[field] = "C1"; break;
            case "phase": json[field] = "C3"; break;
            case "source_commit": json[field] = "invalid"; break;
            case "repeat": json[field] = 1; break;
            case "cleanup_completed": json[field] = false; break;
            case "handler_exit_code": json[field] = 1; break;
            case "job_name": json[field] = "run-1-c0-b10-sweep"; break;
            case "artifacts": json[field] = new JsonArray(); break;
            case "container": json["artifacts"]![0]![field] = "ledger"; break;
            case "name": json["artifacts"]![0]![field] = "run-1/C0/foreign.json"; break;
            case "length": json["artifacts"]![0]![field] = -1; break;
            case "sha256": json["artifacts"]![0]![field] = "invalid"; break;
            default: json[field] = new JsonObject(); break;
        }
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadReceipt(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    [InlineData(1L, true)]
    public async Task Given_ReceiptArtifactLengthBoundary_When_Encoded_Then_RequiresNonnegativeLength(long length, bool accepted)
    {
        using var fixture = new CaptureFixture(); Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken));
        WorkerReceipt receipt = fixture.Receipt();
        receipt = receipt with { Artifacts = [receipt.Artifacts[0] with { Length = length }] };
        if (accepted) Assert.Equal(length, PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(receipt)).Artifacts[0].Length);
        else Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(receipt));
    }

    [Theory]
    [InlineData(63, false)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task Given_ReceiptArtifactHashLengthBoundary_When_Encoded_Then_RequiresExactSha256Length(int length, bool accepted)
    {
        using var fixture = new CaptureFixture(); Assert.Equal(0, await fixture.Run(TestContext.Current.CancellationToken));
        WorkerReceipt receipt = fixture.Receipt();
        receipt = receipt with { Artifacts = [receipt.Artifacts[0] with { Sha256 = new string('a', length) }] };
        if (accepted) Assert.Equal(length, PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(receipt)).Artifacts[0].Sha256.Length);
        else Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(receipt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("receipt")]
    [InlineData("note")]
    public async Task Given_CaptureHost_When_Finalizing_Then_ReceiptFollowsDisposalAndFailuresSuppressOrFailPublication(string? failure)
    {
        using var fixture = new CaptureFixture(); var publisher = new Publisher(fixture.Events, failure == "receipt");
        if (failure == "note") fixture.AfterCreate = _ => throw new IOException("synthetic uncertain note");
        string[] args = B10SweepLineOptionsTests.Configuration(fixture.Options).Select(pair => $"--{pair.Key}={pair.Value}").ToArray();
        int exit = await Program.RunAsync(args, services =>
        {
            services.RemoveAll<IHostedService>();
            foreach (ServiceDescriptor item in services.Where(item => item.ServiceType.Name is
                "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray()) services.Remove(item);
            services.RemoveAll<VerifierKafkaSessionFactory>(); services.AddSingleton(new VerifierKafkaSessionFactory(_ => fixture.Kafka.Session));
            services.RemoveAll<IVerifierArtifactStore>(); services.AddSingleton<IVerifierArtifactStore>(fixture);
            services.RemoveAll<ICompletionReceiptPublisher>(); services.AddSingleton<ICompletionReceiptPublisher>(publisher);
            services.AddSingleton<IHostedService>(_ => new HostCleanup(fixture.Events, failure));
        }, fixture.Kafka.Clock).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(failure is null ? 0 : 1, exit);
        Assert.Equal(failure is null or "receipt" ? 1 : 0, publisher.Calls);
        Assert.Equal(["admin-dispose", "consumer-dispose", "note", "stop", "dispose"], fixture.Events.Take(5));
        if (publisher.Calls == 1) Assert.Equal("receipt", fixture.Events[^1]);
    }

    private static string Utc(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);

    private sealed class CaptureFixture : IVerifierArtifactStore, IDisposable
    {
        internal CaptureFixture(string phase = "C0")
        {
            Options = B10SweepLineOptionsTests.CaptureOptions(phase); Options.CleanupTimeout = TimeSpan.FromMilliseconds(100); Options.BlobIoTimeout = TimeSpan.FromMilliseconds(100);
            Kafka.Poll = Kafka.Refresh; Kafka.Query = _ => new WatermarkOffsets(0, 0);
            Kafka.AdminDispose = () => Events.Add("admin-dispose"); Kafka.ConsumerDispose = () => Events.Add("consumer-dispose");
        }
        internal VerifierHostOptions Options { get; }
        internal KafkaSessionTestSupport Kafka { get; } = new();
        internal VerifierCompletionState Completion { get; } = new();
        internal List<string> Events { get; } = [];
        internal byte[]? Bytes { get; set; }
        internal int Creates { get; private set; }
        internal Action<CancellationToken>? AfterCreate { get; set; }
        internal TimeProvider? Clock { get; set; }
        internal Task<int> Run(CancellationToken token) => new B10SweepLineHandler(Microsoft.Extensions.Options.Options.Create(Options), Clock ?? Kafka.Clock,
            new VerifierKafkaSessionFactory(_ => Kafka.Session), this, Completion).ExecuteAsync(token);
        internal WorkerReceipt Receipt()
        {
            Completion.MarkReturned(Options, 0, CancellationToken.None, CancellationToken.None);
            Assert.True(Completion.TryGetBlobReceipt(out string container, out string path, out byte[] receipt));
            Assert.Equal(Options.VerifierContainer, container); Assert.Equal(PhasePaths.B10SweepLineCompletion(Options.RunId!, Options.Phase!), path);
            return PhaseWireCodec.ReadReceipt(receipt);
        }
        internal void AssertNoReceipt()
        {
            Completion.MarkReturned(Options, 1, CancellationToken.None, CancellationToken.None);
            Assert.False(Completion.TryGetBlobReceipt(out _, out _, out _));
        }
        public Task CreateAsync(string name, byte[] content, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Creates++; Events.Add("note");
            Assert.Equal(PhasePaths.SweepLine(Options.RunId!, Options.Phase!), name);
            if (Bytes is not null) throw new IOException("synthetic duplicate destination");
            Bytes = content; AfterCreate?.Invoke(token); return Task.CompletedTask;
        }
        public Task<byte[]?> ReadAsync(string name, CancellationToken token) => throw new InvalidOperationException("metadata writer cannot read artifacts");
        public Task<BlobListing> ListAsync(string prefix, CancellationToken token) => throw new InvalidOperationException("metadata writer cannot list artifacts");
        public void Dispose() => Kafka.Dispose();
    }

    private sealed class Publisher(List<string> events, bool fail) : ICompletionReceiptPublisher
    {
        internal int Calls { get; private set; }
        public Task PublishAsync(VerifierHostOptions options, string container, string name, byte[] bytes, CancellationToken token)
        {
            Calls++; events.Add("receipt"); Assert.Equal(PhasePaths.B10SweepLineCompletion(options.RunId!, options.Phase!), name);
            Assert.Equal("b10-sweep-line", PhaseWireCodec.ReadReceipt(bytes).Mode);
            return fail ? Task.FromException(new IOException("synthetic uncertain receipt")) : Task.CompletedTask;
        }
    }
    private sealed class HostCleanup(List<string> events, string? fail) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken token) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token) { events.Add("stop"); return fail == "stop" ? Task.FromException(new IOException("synthetic stop")) : Task.CompletedTask; }
        public void Dispose() { events.Add("dispose"); if (fail == "dispose") throw new IOException("synthetic disposal"); }
    }
    private sealed class DelayedClock(TimeProvider inner) : TimeProvider
    {
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new Timer();
        private sealed class Timer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
