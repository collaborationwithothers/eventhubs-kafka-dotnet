using System.Text;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.B10Final;
using Parchwell.Verifier.VerifyRun.Configuration;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10FinalSweepUnitTests
{
    [Theory]
    [InlineData("A01", "C0", "C0", "b10-1-1", 60000)]
    [InlineData("B10", "C2", "C2", "b10-1-1", 60000)]
    [InlineData("B10", "C0", "C1", "b10-1-1", 60000)]
    [InlineData("B10", "C1", "C0", "b10-1-1", 60000)]
    [InlineData("B10", "C0", "C0", "b10d-1-1", 60000)]
    [InlineData("B10", "C0", "C0", "b10-1-1", 59999)]
    [InlineData("B10", "C0", "C0", "b10-1-1", 60001)]
    public async Task Given_InvalidAdmission_When_Executed_Then_NoEvidenceIsRead(string scenario, string phase, string configuration, string run, long settle)
    {
        using var fixture = new Fixture();
        fixture.Options.ScenarioId = scenario;
        fixture.Options.Phase = phase;
        fixture.Options.ConfigurationId = configuration;
        fixture.Options.RunId = run;
        fixture.Options.B10SettleIntervalMilliseconds = settle;
        Assert.Equal(1, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Store.Reads);
        Assert.Empty(fixture.Store.Objects);
        Assert.Equal(0, fixture.Sessions);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_ValidSweepAndNoStarts_When_Executed_Then_PublishesIncompleteRepeatsAndBoundReceipt(string phase)
    {
        using var fixture = new Fixture(phase);
        byte[] sweep = fixture.AddSweep();
        Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        byte[] output = fixture.Store.Objects[PhasePaths.B10FinalSweep("b10-1-1", phase)];
        B10FinalProjection projection = B10FinalProjectionCodec.Read(output);
        Assert.True(projection.Data.ScanComplete);
        Assert.Equal(B10FinalSweepHandler.Reference("verifier", PhasePaths.SweepLine("b10-1-1", phase), sweep), projection.Data.SweepLine);
        Assert.Equal(Enumerable.Range(1, 5), projection.Data.Repeats.Select(repeat => repeat.Repeat));
        Assert.All(projection.Data.Repeats, repeat =>
        {
            Assert.Equal("incomplete", repeat.Status);
            Assert.False(repeat.ClassificationFinal);
            Assert.Null(repeat.Timeout);
            Assert.Equal(["b10_final_repeat_not_started"], repeat.Reasons);
        });
        Assert.Equal(0, fixture.Sessions);
        fixture.Completion.MarkReturned(fixture.Options, 0, CancellationToken.None, CancellationToken.None);
        Assert.True(fixture.Completion.TryGetBlobReceipt(out string container, out string name, out byte[] bytes));
        Assert.Equal("verifier", container);
        Assert.Equal(PhasePaths.B10FinalSweepCompletion("b10-1-1", phase), name);
        var receipt = PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal("b10-final-sweep", receipt.Mode);
        Assert.True(receipt.CleanupCompleted);
        Assert.Equal(B10FinalSweepHandler.Reference("verifier", PhasePaths.B10FinalSweep("b10-1-1", phase), output), Assert.Single(receipt.Artifacts));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("wrong-source")]
    [InlineData("future")]
    public async Task Given_UntrustedSweep_When_Executed_Then_PublishesInvalidWithoutScanning(string fault)
    {
        using var fixture = new Fixture();
        if (fault != "missing") fixture.AddSweep(fault);
        Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        B10FinalProjection projection = B10FinalProjectionCodec.Read(fixture.Store.Objects[PhasePaths.B10FinalSweep("b10-1-1", "C0")]);
        Assert.False(projection.Data.ScanComplete);
        Assert.Null(projection.Data.SweepLine);
        Assert.All(projection.Data.Repeats, repeat =>
        {
            Assert.Equal("invalid", repeat.Status);
            Assert.False(repeat.ClassificationFinal);
            Assert.Null(repeat.Timeout);
            Assert.Equal(["b10_final_sweep_line_invalid"], repeat.Reasons);
        });
        Assert.Equal(0, fixture.Sessions);
    }

    [Fact]
    public async Task Given_UnreadableStartReceiptAndValidSweep_When_Executed_Then_AllRepeatsAreInvalid()
    {
        using var fixture = new Fixture();
        fixture.AddSweep();
        fixture.Store.Objects[PhasePaths.StartLineCompletion("b10-1-1", "C0", 1)] = Encoding.UTF8.GetBytes("{}");
        Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        B10FinalProjection projection = B10FinalProjectionCodec.Read(fixture.Store.Objects[PhasePaths.B10FinalSweep("b10-1-1", "C0")]);
        Assert.False(projection.Data.ScanComplete);
        Assert.All(projection.Data.Repeats, repeat => Assert.Equal(["b10_final_start_line_invalid"], repeat.Reasons));
        Assert.Equal(0, fixture.Sessions);
    }

    [Fact]
    public async Task Given_OutputReadBackDiffers_When_Executed_Then_NoCompletionReceiptIsAvailable()
    {
        using var fixture = new Fixture();
        fixture.AddSweep();
        fixture.Store.CorruptOutputRead = true;
        Assert.Equal(1, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        fixture.Completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _, out _, out _));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HostEvidenceBuilder evidence;
        internal Fixture(string phase = "C0")
        {
            evidence = new HostEvidenceBuilder(scenarioId: "B10", phase: phase, runId: "b10-1-1");
            Options = evidence.Options;
            Options.Mode = "b10-final-sweep";
            Options.Phase = phase;
            Options.ConfigurationId = phase;
            Options.JobName = PhaseIdentity.B10FinalSweepJobName(Options.RunId!, phase);
            Options.VerifierImage = "synthetic/verifier@sha256:" + new string('b', 64);
            Options.B10SettleIntervalMilliseconds = 60000;
        }
        internal VerifierHostOptions Options { get; }
        internal Store Store { get; } = new();
        internal VerifierCompletionState Completion { get; } = new();
        internal int Sessions { get; private set; }
        internal B10FinalSweepHandler Handler() => new(Microsoft.Extensions.Options.Options.Create(Options), evidence.Kafka.Clock,
            new VerifierKafkaSessionFactory(_ => { Sessions++; throw new InvalidOperationException("Unexpected Kafka scan."); }),
            Store, Store, evidence.Loader, new RegisteredProducerConfigurationResolver(Microsoft.Extensions.Options.Options.Create(Options)), Completion);
        internal byte[] AddSweep(string fault = "none")
        {
            DateTimeOffset now = evidence.Kafka.Clock.GetUtcNow().AddSeconds(fault == "future" ? 60 : -1);
            byte[] bytes = fault == "malformed" ? Encoding.UTF8.GetBytes("{}") : B10SweepEvidenceCodec.WriteSweepLine(new(
                1, "B10", Options.ConfigurationId!, Options.RunId!, Options.Phase!, fault == "wrong-source" ? new string('c', 40) : Options.SourceCommit!,
                "document-events", [new(0, 0)], now, now.AddSeconds(-1), now.AddSeconds(-60), 60000));
            Store.Objects[PhasePaths.SweepLine(Options.RunId!, Options.Phase!)] = bytes;
            return bytes;
        }
        public void Dispose() => evidence.Dispose();
    }

    private sealed class Store : IVerifierArtifactStore, IProducerArtifactStore
    {
        internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        internal List<string> Reads { get; } = [];
        internal bool CorruptOutputRead { get; set; }
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
        {
            Reads.Add(name);
            return Task.FromResult(CorruptOutputRead && name.EndsWith("b10-final-sweep.json", StringComparison.Ordinal)
                ? (byte[]?)[1] : Objects.GetValueOrDefault(name));
        }
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
            Task.FromResult(new BlobListing(Objects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, true));
        public async Task CreateStreamAsync(string name, Stream content, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            await CreateAsync(name, copy.ToArray(), cancellationToken);
        }
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
        {
            Objects.Add(name, content);
            return Task.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
