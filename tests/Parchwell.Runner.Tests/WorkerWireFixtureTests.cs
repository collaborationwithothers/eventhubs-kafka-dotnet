using System.Security.Cryptography;
using Confluent.Kafka;
using Parchwell.Verifier.StartLine;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.ProbeGate;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;
using LoadGenIdentity = Parchwell.LoadGen.Run.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class WorkerWireFixtureTests
{
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    [Fact]
    public async Task Given_ActualPublishersAndCodecsWithLegacyReceipts_When_ProducingFixtures_Then_AllFixturesAreReadable()
    {
        string? supplied = Environment.GetEnvironmentVariable("PARCHWELL_WIRE_FIXTURE_DIRECTORY");
        string root = supplied ?? Path.Combine(Path.GetTempPath(), "parchwell-wire-" + Guid.NewGuid().ToString("N"));
        try
        {
            await ProduceAsync(root, TestContext.Current.CancellationToken);
        }
        finally
        {
            if (supplied is null && Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    internal static async Task ProduceAsync(string root, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        string commit = WireFixtures.BuiltSourceCommit();
        var files = new List<WireFile>();
        var probe = new List<VerifierResult>();
        try
        {
            foreach (string phase in new[] { "probe", "C0", "C3" })
                for (int ordinal = 1; ordinal <= 5; ordinal++)
                {
                    string run = "rehearsal-" + phase.ToLowerInvariant() + "-r" + ordinal;
                    string producer = run + "-pod";
                    var loadIdentity = new LoadGenIdentity("A01", phase == "probe" ? "C0" : phase, run, producer, producer, producer);
                    string relative = phase + "/r" + ordinal;
                    string work = Path.Combine(root, "producer-work", relative);
                    WorkerPublicationFixture.GoldenRun publication = await WorkerPublicationFixture.CreateAsync(work, loadIdentity, cancellationToken);
                    Assert.Equal("index.json", publication.Requests.Last().Name);
                    Assert.Equal(6, publication.Requests.Length);
                    Assert.All(publication.Requests, request => Assert.Equal("*", request.IfNoneMatch));
                    foreach ((string name, byte[] bytes) in publication.Payloads) await Save(relative + "/" + name, bytes);
                    var identity = new VerifierIdentity("A01", loadIdentity.ConfigurationId, run, producer, producer, producer);
                    // This real writer fixture has diagnostic evidence only. No successful oracle or zero counts are invented.
                    var result = new VerifierResult(1, identity, "1", new Dictionary<string, string> { ["fixture"] = "synthetic" },
                        commit, typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, VerifierResultStatus.Invalid,
                        [12], null, null, null, null, null, null, null,
                        phase == "probe" ? new(true, ordinal == 1, "sasl_ssl://broker.example.invalid:9093/1", 1, 1, ordinal == 1 ? 20001 : 20000) : null,
                        null, [], [new(12, "synthetic_codec_fixture", null, null, null, null)]);
                    byte[] resultBytes = VerifierResultCodec.Write(result, new(16777216, 100, 2048));
                    var readback = VerifierResultCodec.Read(resultBytes, relative + "/result.json",
                        new InputLimits(32, 16777216, 16777216, 65536, 100, 1000, 2048), 100);
                    Assert.True(readback.IsSuccess);
                    Assert.Equal(identity, readback.Value!.Identity);
                    Assert.Null(readback.Value.Counts);
                    Assert.Equal(VerifierResultStatus.Invalid, readback.Value.Status);
                    await Save(relative + "/result.json", resultBytes);
                    if (phase == "probe") probe.Add(result);
                    byte[] inventory = BrokerInventoryCodec.Write(identity,
                        [new BrokerMetadata(1, "broker.example.invalid", 9093)], commit, 65536);
                    await Save(relative + "/broker-inventory.json", inventory);
                    foreach (string mode in new[] { "loadgen", "start-line", "verify" })
                        await Save(relative + "/" + mode + "-receipt.txt", await WorkerReceipts.WriteAsync(mode, identity, commit));
                }
            var gateRule = new Parchwell.Scenarios.ProbeGate("C0", new("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer),
                Parchwell.Scenarios.ProbeOperator.GreaterThan, 20000, 5, ["C0", "C3"]);
            foreach (string variation in new[] { "pass", "nonqualifying", "unusable" })
            {
                VerifierResult[] results = probe.Select(result => variation switch
                {
                    "nonqualifying" => result with { ProbeObservation = result.ProbeObservation! with { Qualifying = false, WindowMaximum = 20000 } },
                    "unusable" => result with { ProbeObservation = result.ProbeObservation! with { Usable = false, Qualifying = false } },
                    _ => result
                }).ToArray();
                string prefix = variation == "pass" ? "probe" : "probe-" + variation;
                if (variation != "pass")
                    for (int index = 0; index < results.Length; index++)
                        await Save(prefix + "/r" + (index + 1) + "/result.json", VerifierResultCodec.Write(results[index], new(16777216, 100, 2048)));
                ProbeGateResult gate = ProbeGateEvaluator.Evaluate(new(gateRule, results.Select(result => result.Identity).ToArray(),
                    results.Select((result, index) => new ProbeGateInput(prefix + "/r" + (index + 1) + "/result.json", ProbeGateInputStatus.Readable, result)).ToArray()), commit);
                Assert.Equal(variation switch { "pass" => "pass", "nonqualifying" => "untestable_at_build_scale", _ => "inconclusive" }, gate.Outcome);
                await Save("gate-" + variation + ".json", ProbeGateResultCodec.Write(gate, 16777216));
            }
            await Save("gate-receipt.txt", await WorkerReceipts.WriteAsync("probe-gate", null, commit));
            await File.WriteAllBytesAsync(Path.Combine(root, "wire-manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new WireManifest(1, commit, files.ToArray()),
                WireOptions), cancellationToken);
            WireFixtures read = await WireFixtures.LoadAsync(root, cancellationToken);
            Assert.Equal(commit, read.SourceCommit);
        }
        finally
        {
            string work = Path.Combine(root, "producer-work");
            if (Directory.Exists(work)) Directory.Delete(work, true);
        }
        async Task Save(string path, byte[] bytes)
        {
            string destination = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
            files.Add(new(path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }
    }
}
