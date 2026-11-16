using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Consumer;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707

public sealed class MalformedRecordPublicationTests
{
    [Theory]
    [InlineData("000001..jsonl")]
    [InlineData("000001\\.jsonl")]
    public async Task Given_UnsafeListedRecord_U4_When_Verified_Then_PublishesInvalidResultAndReceipt(string file)
    {
        using var host = new HostEvidenceBuilder(scenarioId: "C03", phase: "C0", scenarioSourceId: "A01");
        _ = host.Verify();
        VerifierHostOptions options = host.Options;
        var records = new ConsumerWireFixture();
        string prefix = PhaseIdentity.RepeatPrefix(options.RunId!, options.Phase!, 1);
        string group = ConsumerPlanValidation.GroupId(options);
        foreach (string role in new[] { "subject", "witness" })
        {
            JsonObject marker = ConsumerWireFixture.Registration(role);
            marker["run_id"] = options.RunId;
            marker["group_id"] = group;
            marker["effective_settings"]!["group.id"] = group;
            records.Set(role, "registration.json", marker);
            JsonObject receipt = ConsumerWireFixture.Completion(role, role == "witness" ? 2 : 1, 1, 0);
            receipt["run_id"] = options.RunId;
            if (role == "witness")
            {
                JsonObject commit = ConsumerWireFixture.Commit(role);
                commit["offsets"] = new JsonArray(ConsumerWireFixture.Offset(1, true));
                records.SetRows(role, [ConsumerWireFixture.Assignment(role, []), commit]);
            }
            records.Set(role, "completion.json", receipt);
        }
        ConsumerStartSnapshot snapshot = ConsumerArtifactCodec.ReadSnapshot(records.Blobs[ConsumerWireFixture.Prefix + "consumer-start.json"]);
        records.Blobs[ConsumerWireFixture.Prefix + "consumer-start.json"] = ConsumerArtifactCodec.WriteSnapshot(snapshot with { GroupId = group });
        Dictionary<string, byte[]> renamed = records.Blobs.ToDictionary(
            pair => pair.Key.Replace(ConsumerWireFixture.Prefix, prefix, StringComparison.Ordinal), pair => pair.Value, StringComparer.Ordinal);
        records.Blobs.Clear();
        foreach (var pair in renamed) records.Blobs.Add(pair.Key, pair.Value);
        string malformed = prefix + "consumer/subject/record/" + file;
        records.Blobs.Add(malformed, "invalid"u8.ToArray());
        var actualStore = new BlobProducerArtifactStore(new BlobContainerClient(new Uri("https://synthetic.invalid/consumer-record")), options.MaxArtifactBytes!.Value);
        await Assert.ThrowsAsync<ArgumentException>(async () => await actualStore.ReadBoundedAsync(malformed, options.MaxArtifactBytes.Value, TestContext.Current.CancellationToken));
        var guarded = new GuardedRecords(records, actualStore, malformed);
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes.Value,
            options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
            options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
        var settings = new Dictionary<string, string> { ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000" };
        var plan = new ConsumerVerificationPlan(null, 600000, settings, settings);
        int calls = 0;
        ConsumerOffsetReader offsets = (partitions, _, _) =>
        {
            calls++;
            return Task.FromResult(new ConsumerOffsetRead("success", partitions, host.Kafka.Clock.GetUtcNow(),
                host.Kafka.Clock.GetUtcNow(), 0, [new ConsumerObservedOffset(0, 1, 0)]));
        };
        var extension = new ConsumerRepeatExtension(options, plan,
            new ConsumerArtifactReader(guarded, limits, options.BlobIoTimeout!.Value, host.Kafka.Clock), records,
            offsets, host.Kafka.Clock, "synthetic:9092");
        var completion = new VerifierCompletionState();
        var handler = new VerifyRunHandler(Options.Create(options), host.Kafka.Clock,
            new Parchwell.Verifier.Common.Kafka.VerifierKafkaSessionFactory(_ => host.Kafka.Session), host, host,
            host.Loader, new RegisteredProducerConfigurationResolver(Options.Create(options)), completion, extension);

        Assert.Equal(1, await handler.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, guarded.UnsupportedReads);
        Assert.Equal(1, calls);
        Assert.Equal(1, host.Creates);
        using JsonDocument result = JsonDocument.Parse(host.Output!);
        Assert.Equal("complete_valid", result.RootElement.GetProperty("status").GetString());
        JsonElement consumer = result.RootElement.GetProperty("consumer");
        Assert.Equal("invalid", consumer.GetProperty("status").GetString());
        Assert.Equal(["consumer_record_invalid"], consumer.GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
        Assert.False(consumer.TryGetProperty("counts", out _));
        completion.MarkReturned(options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out string receiptName, out byte[] receiptBytes));
        Assert.Equal(PhasePaths.VerifyCompletion(options.RunId!, options.Phase!, 1), receiptName);
        Assert.Equal(1, PhaseWireCodec.ReadReceipt(receiptBytes).HandlerExitCode);
    }

    private sealed class GuardedRecords(ConsumerWireFixture records, BlobProducerArtifactStore actual, string malformed) : IProducerArtifactStore
    {
        internal int UnsupportedReads { get; private set; }
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => records.ListAsync(prefix, cancellationToken);
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => ReadBoundedAsync(name, long.MaxValue, cancellationToken);
        public Task<byte[]?> ReadBoundedAsync(string name, long maximum, CancellationToken cancellationToken)
        {
            if (name != malformed) return records.ReadBoundedAsync(name, maximum, cancellationToken);
            UnsupportedReads++;
            return actual.ReadBoundedAsync(name, maximum, cancellationToken);
        }
    }
}
