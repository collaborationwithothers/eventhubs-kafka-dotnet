using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Results;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class B10ScientificThreeJobLegacyReaderTests
{
    [Theory]
    [InlineData("C1")]
    public async Task Given_ProducedScientificStartLine_When_LegacySweepAndJudgeRead_Then_RejectStartLine(string phase)
    {
        using var authentication = new KafkaSessionTestSupport();
        var store = new Store();
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("verify");
        options.ScenarioId = "B10";
        options.ConfigurationId = options.Phase = phase;
        options.RunId = "b10-1-1";
        options.VerifierContainer = "verifier";
        options.B10SettleIntervalMilliseconds = 1000;
        options.MaxInputEntries = 100000;
        options.MaxTotalInputBytes = 4194304;
        options.MaxOutputBytes = 65536;
        options.MaxDiagnosticsPerCheck = 8;
        options.LoadGenImage = "synthetic/loadgen@sha256:" + new string('a', 64);
        options.VerifierImage = "synthetic/verifier@sha256:" + new string('b', 64);
        string commit = options.SourceCommit!;
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            var identity = new RunIdentity("B10", phase, "b10-1-1", "producer-0", "producer-0", "client-0");
            foreach (int role in new[] { 0, 1 })
            {
                string instance = $"producer-r{repeat}-{role}";
                var registered = identity with { InstanceId = instance, ExpectedInstanceId = instance, ClientId = instance };
                var registration = new ProducerRegistration(1, registered, phase, repeat,
                    role == 0 ? PhaseIdentity.JobName("b10-1-1", phase, repeat) : PhaseIdentity.RestartJobName("b10-1-1", phase, repeat),
                    instance, instance + "-uid", commit, role == 0 ? null : $"producer-r{repeat}-0");
                store.Objects[PhasePaths.Registration("b10-1-1", phase, repeat, instance)] = PhaseWireCodec.WriteRegistration(registration);
            }
        }
        // A coherent legacy intake pair makes the independently produced start line the rejecting predicate.
        VerifierResult result = B10OneJobC0CodecTests.Result("b10-1-1", phase, 2);
        var actualProducers = result.B10Intake!.Producers.Select((producer, role) =>
        {
            string instance = $"producer-r1-{role}";
            var identity = producer.Identity with { InstanceId = instance, ExpectedInstanceId = instance, ClientId = instance };
            return producer with { Identity = identity, PredecessorInstanceId = role == 0 ? null : "producer-r1-0", PublicationAcceptance = null };
        }).ToArray();
        result = result with { Identity = actualProducers[0].Identity, B10Intake = result.B10Intake with { Producers = actualProducers },
            B10ProducerObservations = null, B10WindowChecksComplete = false };
        var limits = new OutputLimits(65536, 8, 256);
        byte[] resultBytes = VerifierResultCodec.Write(result, limits);
        string resultName = PhasePaths.Result("b10-1-1", phase, 1);
        store.Objects[resultName] = resultBytes;
        var verifyReceipt = new WorkerReceipt(1, "verify", "B10", phase, "b10-1-1", phase, 1,
            PhaseIdentity.VerifyJobName("b10-1-1", phase, 1), "verifier-pod", result.Identity, commit, 1, true,
            "2026-11-13T00:00:00.0000000Z", "2026-11-13T00:00:01.0000000Z", [Reference(resultName, resultBytes)],
            null, new("pass", "pass", "pass"), result.B10Intake);
        store.Objects[PhasePaths.VerifyCompletion("b10-1-1", phase, 1)] = PhaseWireCodec.WriteReceipt(verifyReceipt);
        options.Repeat = 1;
        string job = PhaseIdentity.JobName("b10-1-1", phase, 1);
        var startIdentity = new RunIdentity("B10", phase, "b10-1-1", job, job, job);
        byte[] start = StartLineNoteCodec.Write(new(1, startIdentity, "document-events", [new(0, 0)],
            "2026-11-13T00:00:00.0000000Z", commit, "synthetic-managed", "synthetic-native", options.ExpectedProducerJobNames), limits);
        byte[] inventory = BrokerInventoryCodec.Write(startIdentity, [new Confluent.Kafka.BrokerMetadata(1, "broker-a", 9093)], commit, limits.MaxOutputBytes);
        string startName = PhasePaths.StartLine("b10-1-1", phase, 1);
        string inventoryName = PhasePaths.BrokerInventory("b10-1-1", phase, 1);
        store.Objects[startName] = start;
        store.Objects[inventoryName] = inventory;
        store.Objects[PhasePaths.StartLineCompletion("b10-1-1", phase, 1)] = PhaseWireCodec.WriteReceipt(new(1, "start-line", "B10", phase,
            "b10-1-1", phase, 1, PhaseIdentity.StartLineJobName("b10-1-1", phase, 1), "verifier-pod", startIdentity, commit, 0, true,
            "2026-11-13T00:00:00.0000000Z", "2026-11-13T00:00:00.0000000Z", [Reference(startName, start), Reference(inventoryName, inventory)], null, null));
        DateTimeOffset now = authentication.Clock.GetUtcNow();
        store.Objects[PhasePaths.SweepLine("b10-1-1", phase)] = B10SweepEvidenceCodec.WriteSweepLine(new(1, "B10", phase, "b10-1-1", phase,
            commit, "document-events", [new(0, 1)], now, now, now.AddSeconds(-1), 1000));
        options.Mode = "b10-sweep";
        options.Repeat = null;
        options.JobName = PhaseIdentity.B10SweepJobName("b10-1-1", phase);
        Assert.Equal(1, await new B10SweepHandler(Options.Create(options), authentication.Clock,
            new VerifierKafkaSessionFactory(_ => throw new InvalidOperationException("unexpected_kafka_session")), store, store)
            .ExecuteAsync(TestContext.Current.CancellationToken));
        B10Finalization finalization = B10SweepEvidenceCodec.ReadFinalization(store.Objects[PhasePaths.B10Finalization("b10-1-1", phase)]);
        Assert.Contains("b10_sweep_failed", finalization.Repeats[0].Reasons);
        Assert.Contains(startName, store.Reads);
        Assert.DoesNotContain(store.Reads, name => name.Contains("/publication/", StringComparison.Ordinal));
        options.Mode = "judge";
        options.JobName = PhaseIdentity.JudgeJobName("b10-1-1", phase);
        options.CandidateOutputName = PhasePaths.Candidate("b10-1-1", phase);
        Assert.Equal(1, await new JudgeHandler(Options.Create(options), store, store, authentication.Clock)
            .ExecuteAsync(TestContext.Current.CancellationToken));
        // A scientific run ID now reaches the final judge, which finds no authenticated final sweep for this legacy-only evidence.
        using JsonDocument candidate = JsonDocument.Parse(store.Objects[options.CandidateOutputName]);
        Assert.Contains(candidate.RootElement.GetProperty("reasons").EnumerateArray(),
            reason => reason.GetString() == "b10_final_projection_untrusted");
    }

    [Fact]
    public async Task Given_ActualOneProducerC0Output_When_LegacyJudgeReads_Then_RejectsIntakeCount()
    {
        using HostEvidenceBuilder fixture = B10OneJobC0VerifyTests.Fixture();
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out string receiptName, out byte[] receipt));
        var store = new Store();
        foreach ((string name, byte[] bytes) in fixture.VerifierObjects.Concat(fixture.LedgerObjects)) store.Objects[name] = bytes;
        store.Objects[receiptName] = receipt;
        for (int repeat = 2; repeat <= 5; repeat++)
        {
            string instance = $"earlier-producer-r{repeat}";
            var identity = new RunIdentity("B10", "C0", "b10-1-1", instance, instance, instance);
            store.Objects[PhasePaths.Registration("b10-1-1", "C0", repeat, instance)] = PhaseWireCodec.WriteRegistration(new(1, identity,
                "C0", repeat, PhaseIdentity.JobName("b10-1-1", "C0", repeat), instance, instance + "-uid", fixture.Options.SourceCommit!));
        }
        DateTimeOffset now = fixture.Kafka.Clock.GetUtcNow();
        fixture.Options.B10SettleIntervalMilliseconds = 1000;
        store.Objects[PhasePaths.SweepLine("b10-1-1", "C0")] = B10SweepEvidenceCodec.WriteSweepLine(new(1, "B10", "C0", "b10-1-1", "C0",
            fixture.Options.SourceCommit!, "document-events", [new(0, 1)], now, now, now.AddSeconds(-1), 1000));
        fixture.Options.Mode = "b10-sweep";
        fixture.Options.Repeat = null;
        fixture.Options.JobName = PhaseIdentity.B10SweepJobName("b10-1-1", "C0");
        Assert.Equal(1, await new B10SweepHandler(Options.Create(fixture.Options), fixture.Kafka.Clock,
            new VerifierKafkaSessionFactory(_ => throw new InvalidOperationException("unexpected_kafka_session")), store, store)
            .ExecuteAsync(TestContext.Current.CancellationToken));
        B10Finalization finalization = B10SweepEvidenceCodec.ReadFinalization(store.Objects[PhasePaths.B10Finalization("b10-1-1", "C0")]);
        Assert.Contains("b10_sweep_failed", finalization.Repeats[0].Reasons);
        Assert.Contains(PhasePaths.Result("b10-1-1", "C0", 1), store.Reads);
        Assert.DoesNotContain(PhasePaths.StartLine("b10-1-1", "C0", 1), store.Reads);
        fixture.Options.LoadGenImage = "synthetic/loadgen@sha256:" + new string('a', 64);
        fixture.Options.VerifierImage = "synthetic/verifier@sha256:" + new string('b', 64);
        fixture.Options.Mode = "judge";
        fixture.Options.CandidateOutputName = PhasePaths.Candidate("b10-1-1", "C0");
        fixture.Options.JobName = PhaseIdentity.JudgeJobName("b10-1-1", "C0");
        Assert.Equal(1, await new JudgeHandler(Options.Create(fixture.Options), store, store, fixture.Kafka.Clock)
            .ExecuteAsync(TestContext.Current.CancellationToken));
        // A scientific run ID now reaches the final judge, which finds no authenticated final sweep for this legacy-only evidence.
        using JsonDocument candidate = JsonDocument.Parse(store.Objects[fixture.Options.CandidateOutputName]);
        Assert.Contains(candidate.RootElement.GetProperty("reasons").EnumerateArray(),
            reason => reason.GetString() == "b10_final_projection_untrusted");
    }

    private static ArtifactReference Reference(string name, byte[] bytes) => new("verifier", name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private sealed class Store : IVerifierArtifactStore, IProducerArtifactStore
    {
        internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        internal List<string> Reads { get; } = [];
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
        {
            Reads.Add(name);
            return Task.FromResult(Objects.GetValueOrDefault(name));
        }
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(
            Objects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray(), 1, true));
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
        {
            Objects.Add(name, content);
            return Task.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
