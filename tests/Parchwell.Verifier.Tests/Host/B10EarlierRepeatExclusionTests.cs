using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10EarlierRepeatExclusionTests
{
    [Theory]
    [InlineData("first", true)]
    [InlineData("second", true)]
    [InlineData("later", false)]
    [InlineData("unknown", false)]
    [InlineData("missing", false)]
    [InlineData("duplicate", false)]
    [InlineData("other-phase", false)]
    public async Task Given_ForeignOriginalWindowRecord_When_VerifyingLaterRepeat_Then_OnlyUnambiguousEarlierOwnershipIsExcluded(string kind, bool eligible)
    {
        using HostEvidenceBuilder earlier = B10SweepFixture.CreateRepeat(1);
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(2);
        CopyRegistrations(earlier, fixture);
        string instance = kind switch
        {
            "first" or "duplicate" => "pod-first", "second" => "pod-second",
            "later" => "pod-first-r3", "other-phase" => "pod-c1", _ => "unknown"
        };
        ConsumeResult<byte[], byte[]> own = Record(fixture.Options.InstanceId!, 0);
        ConsumeResult<byte[], byte[]> foreign = Record(instance, 1);
        if (kind == "missing") foreign.Message.Headers.Remove("instance-id");
        if (kind == "duplicate") foreign.Message.Headers.Add("instance-id", Encoding.UTF8.GetBytes(instance));
        if (kind == "other-phase")
        {
            using HostEvidenceBuilder other = B10SweepFixture.CreateRepeat(1, "C1");
            CopyRegistrations(other, fixture);
        }
        int position = 0;
        ConsumeResult<byte[], byte[]>[] records = [own, foreign];
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, records.Length);
        fixture.Kafka.Position = _ => new Offset(position);
        fixture.ConsumeRecord = () => position < records.Length ? records[position++] : null;
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument result = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(eligible ? "incomplete" : "invalid", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(eligible, result.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
        Assert.False(result.RootElement.GetProperty("classification_final").GetBoolean());
        if (!eligible) Assert.Contains(result.RootElement.GetProperty("failed_checks").EnumerateArray(), value => value.GetInt32() is 3 or 6);
    }

    [Theory]
    [InlineData("records", -1)]
    [InlineData("records", 0)]
    [InlineData("records", 1)]
    [InlineData("bytes", -1)]
    [InlineData("bytes", 0)]
    [InlineData("bytes", 1)]
    public async Task Given_ExcludedEarlierRecord_When_ApplyingRawBudget_Then_ItConsumesTheSameBudget(string budget, int delta)
    {
        using HostEvidenceBuilder earlier = B10SweepFixture.CreateRepeat(1);
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(2);
        CopyRegistrations(earlier, fixture);
        ConsumeResult<byte[], byte[]>[] records = [Record(fixture.Options.InstanceId!, 0), Record("pod-first", 1)];
        int position = 0;
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, records.Length);
        fixture.Kafka.Position = _ => new Offset(position);
        fixture.ConsumeRecord = () => position < records.Length ? records[position++] : null;
        long bytes = records.Sum(record => (long)record.Message.Key.Length + record.Message.Value.Length +
            record.Message.Headers.Sum(header => (long)Encoding.UTF8.GetByteCount(header.Key) + header.GetValueBytes().Length));
        if (budget == "records") fixture.Options.MaxConsumedRecords = records.Length + delta;
        else fixture.Options.MaxConsumedBytes = bytes + delta;
        if (delta < 0)
        {
            await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Empty(fixture.CreateNames);
        }
        else
        {
            Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            using JsonDocument result = JsonDocument.Parse(fixture.Output!);
            Assert.True(result.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("source")]
    [InlineData("job")]
    [InlineData("predecessor")]
    [InlineData("path")]
    [InlineData("same-instance")]
    public async Task Given_UntrustedEarlierRegistration_When_VerifyingLaterRepeat_Then_ItCannotAuthorizeExclusion(string mutation)
    {
        using HostEvidenceBuilder earlier = B10SweepFixture.CreateRepeat(1);
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(2);
        CopyRegistrations(earlier, fixture);
        string name = PhasePaths.Registration(fixture.Options.RunId!, "C0", 1, "pod-first");
        if (mutation == "missing") fixture.RemoveLedgerObject(name);
        else
        {
            JsonObject registration = JsonNode.Parse(fixture.LedgerObjects[name])!.AsObject();
            switch (mutation)
            {
                case "source": registration["source_commit"] = new string('b', 40); break;
                case "job": registration["job_name"] = PhaseIdentity.JobName(fixture.Options.RunId!, "C0", 2); break;
                case "predecessor": registration["predecessor_instance_id"] = "unexpected"; break;
                case "path": fixture.RemoveLedgerObject(name); name = name.Replace("/pod-first/", "/different/", StringComparison.Ordinal); break;
                case "same-instance":
                    foreach (string field in new[] { "instance_id", "expected_instance_id", "client_id" })
                        registration["identity"]![field] = fixture.Options.InstanceId;
                    registration["pod_name"] = fixture.Options.InstanceId;
                    fixture.RemoveLedgerObject(name);
                    name = PhasePaths.Registration(fixture.Options.RunId!, "C0", 1, fixture.Options.InstanceId!);
                    string second = PhasePaths.Registration(fixture.Options.RunId!, "C0", 1, "pod-second");
                    JsonObject secondRegistration = JsonNode.Parse(fixture.LedgerObjects[second])!.AsObject();
                    secondRegistration["predecessor_instance_id"] = fixture.Options.InstanceId;
                    fixture.AddLedgerObject(second, Encoding.UTF8.GetBytes(secondRegistration.ToJsonString()));
                    break;
            }
            fixture.AddLedgerObject(name, Encoding.UTF8.GetBytes(registration.ToJsonString()));
        }
        if (mutation is "job" or "predecessor")
            await Assert.ThrowsAsync<FormatException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Empty(fixture.CreateNames);
    }

    [Fact]
    public async Task Given_FirstRepeat_When_Verifying_Then_NoEarlierRegistrationIsRead()
    {
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(1);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(fixture.Reads, name => name.Contains("/r0/", StringComparison.Ordinal));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.True(output.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
    }

    [Fact]
    public async Task Given_NoEarlierRepeats_When_ReadingOwnership_Then_StorageIsNotTouched()
    {
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(1);
        Assert.Empty(await B10PhaseRegistrationReader.ReadAsync(fixture.Options, 0, new UnexpectedStore(),
            new InputBudget(Limits(1, 1)), (_, _) => throw new InvalidOperationException("unexpected read"),
            fixture.Kafka.Clock, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 17)]
    public async Task Given_IncompleteOrOverLimitEarlierListing_When_ReadingOwnership_Then_FailsClosed(bool complete, int pages)
    {
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(1);
        var store = new ListingStore(fixture, complete, pages);
        await Assert.ThrowsAsync<InvalidOperationException>(() => B10PhaseRegistrationReader.ReadAsync(fixture.Options, 1, store,
            new InputBudget(Limits(1_048_576, 8192)), Read, fixture.Kafka.Clock, TestContext.Current.CancellationToken));
        Task<byte[]> Read(string name, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(fixture.LedgerObjects[name]); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_SharedRegistrationByteBoundary_When_ReadingOwnership_Then_ExactLimitPasses(int delta)
    {
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(1);
        string prefix = PhasePaths.ProducersPrefix(fixture.Options.RunId!, "C0", 1);
        BlobListing listing = await fixture.ListAsync(prefix, TestContext.Current.CancellationToken);
        long bytes = listing.Names.Sum(name => (long)Encoding.UTF8.GetByteCount(name)) + listing.Names
            .Where(name => name.EndsWith("/registration.json", StringComparison.Ordinal)).Sum(name => fixture.LedgerObjects[name].LongLength);
        Task<IReadOnlyList<IReadOnlyList<ProducerRegistration>>> Read() => B10PhaseRegistrationReader.ReadAsync(fixture.Options, 1, fixture,
            new InputBudget(Limits(bytes + delta, 8192)), (name, _) => Task.FromResult(fixture.LedgerObjects[name]), fixture.Kafka.Clock,
            TestContext.Current.CancellationToken);
        if (delta < 0) await Assert.ThrowsAsync<InputLimitExceededException>(Read);
        else Assert.Equal(2, Assert.Single(await Read()).Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_SharedRegistrationEntryBoundary_When_ReadingOwnership_Then_ExactLimitPasses(int delta)
    {
        using HostEvidenceBuilder fixture = B10SweepFixture.CreateRepeat(1);
        BlobListing listing = await fixture.ListAsync(PhasePaths.ProducersPrefix(fixture.Options.RunId!, "C0", 1), TestContext.Current.CancellationToken);
        Task<IReadOnlyList<IReadOnlyList<ProducerRegistration>>> Read() => B10PhaseRegistrationReader.ReadAsync(fixture.Options, 1, fixture,
            new InputBudget(Limits(1_048_576, listing.Names.Count + delta)), (name, _) => Task.FromResult(fixture.LedgerObjects[name]), fixture.Kafka.Clock,
            TestContext.Current.CancellationToken);
        if (delta < 0) await Assert.ThrowsAsync<InputLimitExceededException>(Read);
        else Assert.Equal(2, Assert.Single(await Read()).Count);
    }

    private static InputLimits Limits(long bytes, long entries) => new(64, 131072, bytes, 32768, 100, entries, 256);
    private static void CopyRegistrations(HostEvidenceBuilder source, HostEvidenceBuilder destination)
    {
        foreach ((string name, byte[] bytes) in source.LedgerObjects.Where(item => item.Key.EndsWith("/registration.json", StringComparison.Ordinal)))
            destination.AddLedgerObject(name, bytes);
    }
    private static ConsumeResult<byte[], byte[]> Record(string instance, long offset)
    {
        ConsumeResult<byte[], byte[]> record = HostEvidenceBuilder.Record();
        record.Offset = offset;
        record.Message.Headers.Remove("instance-id");
        record.Message.Headers.Add("instance-id", Encoding.UTF8.GetBytes(instance));
        return record;
    }
    private sealed class UnexpectedStore : IProducerArtifactStore
    {
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => throw new InvalidOperationException("unexpected list");
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => throw new InvalidOperationException("unexpected read");
    }
    private sealed class ListingStore(HostEvidenceBuilder fixture, bool complete, int pages) : IProducerArtifactStore
    {
        public async Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
            (await fixture.ListAsync(prefix, cancellationToken)) with { IsComplete = complete, Pages = pages };
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(fixture.LedgerObjects.GetValueOrDefault(name));
    }
}
#pragma warning restore CA1707
