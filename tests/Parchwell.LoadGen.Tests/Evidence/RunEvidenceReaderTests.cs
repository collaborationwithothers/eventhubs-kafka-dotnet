using System.Collections.Immutable;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunEvidenceReaderTests
{
    [Fact]
    public async Task Given_ValidNegativeCapabilityArtifactsAndNativeExtraFields_When_Read_Then_ReturnsArtifactCountsWithoutPromotingCapability()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        RunEvidenceReadResult result = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.StatisticsRecordCount);
        Assert.Equal(2, result.LifecycleRecordCount);
        Assert.False(result.IsFaulted);
        Assert.False(result.RequestCapabilitySupported);
        Assert.Equal(true, result.StatisticsComplete);
        Assert.Contains("unsupported", result.CapabilityStatuses);
        Assert.Contains("unproven", result.CapabilityStatuses);
        Assert.DoesNotContain("supported", result.CapabilityStatuses);
    }

    [Fact]
    public async Task Given_StatisticsArtifactWithWrongIdentity_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        RunIdentity wrong = Identity() with { RunId = "another-run" };

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, wrong, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_StatisticsRowWithUnknownOrDuplicateEnvelopeProperty_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "statistics.jsonl");
        string line = (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).TrimEnd('\n');
        await File.WriteAllTextAsync(file, line.Replace("{\"schema_version\":", "{\"unexpected\":true,\"schema_version\":", StringComparison.Ordinal) + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        await File.WriteAllTextAsync(file, line.Replace("{\"schema_version\":", "{\"schema_version\":1,\"schema_version\":", StringComparison.Ordinal) + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_TruncatedJsonlOrOverLimitFile_When_Read_Then_RejectsBeforeParsing()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string statistics = Path.Combine(path, "statistics.jsonl");
        string line = (await File.ReadAllTextAsync(statistics, TestContext.Current.CancellationToken)).TrimEnd('\n');
        await File.WriteAllTextAsync(statistics, line, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        await File.WriteAllTextAsync(statistics, line + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => RunEvidenceReader.ReadAsync(path, Identity(), 8, 7, 16, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_CapabilityDocumentWithUnknownProperty_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        string mutated = json.Insert(1, "\"unexpected\":true,");
        await File.WriteAllTextAsync(file, mutated, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_OptionalResolvedConfiguration_When_Validating_Then_AcceptsKnownShapeAndRejectsUnknownNestedMembers()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        const string noResolvedConfiguration = "\"resolved_configuration\":null";
        Assert.Contains(noResolvedConfiguration, json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, json.Replace(noResolvedConfiguration,
            "\"resolved_configuration\":{\"role\":0,\"settings\":{},\"omissions\":[]}", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        string withResolvedConfiguration = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(file, withResolvedConfiguration.Replace("\"settings\":{}", "\"settings\":{\"unknown\":{}}", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_NestedTypedSnapshotWithUnknownProperty_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "statistics.jsonl");
        string line = (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).TrimEnd('\n');
        string mutated = line.Replace("\"name\":\"native-producer\"", "\"name\":\"native-producer\",\"unknown\":1", StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, mutated + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        const string brokerArray = "\"brokers\":[{\"name\":\"broker-1\",\"node_id\":1,\"state\":\"UP\",\"state_age_microseconds\":1,\"request_timeouts\":0,\"disconnects\":0,\"transmission_retries\":0,\"transmission_errors\":0,\"receive_errors\":0,\"throttle\":{\"count\":0,\"value\":null},\"round_trip_time\":{\"count\":0,\"value\":null}}]";
        string nullCollection = line.Replace(brokerArray, "\"brokers\":null", StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, nullCollection + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    private static async Task<string> CreateEvidenceAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(Path.GetTempPath(), "evidence-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        await using var session = new RunEvidenceSession(Identity(),
            new RunEvidenceOptions(path, 8, 64 * 1024, 256 * 1024, 16, TimeSpan.FromSeconds(2)), TimeProvider.System);
        string rawWithNativeExtra = "{\"name\":\"native-producer\",\"vendor_extra\":{\"preserved\":true}}";
        await session.WriteAsync(Report(rawWithNativeExtra), cancellationToken);
        session.CaptureLog(new LogMessage("native-producer", SyslogLevel.Warning, "BROKER", "timed out"));
        session.CaptureError(new Error(ErrorCode.Local_TimedOut));
        await session.CompleteAsync(new KafkaStatisticsCompletion(true, ImmutableArray<KafkaStatisticsDiagnostic>.Empty, ImmutableArray<string>.Empty), cancellationToken);
        Assert.False(session.IsFaulted);
        return path;
    }

    private static Task<RunEvidenceReadResult> ReadAsync(string path, RunIdentity identity, CancellationToken cancellationToken) =>
        RunEvidenceReader.ReadAsync(path, identity, 256 * 1024, 64 * 1024, 16, cancellationToken);

    private static RunIdentity Identity() => new("A01", "C0", "run-reader", "instance-reader", "instance-reader", "loadgen-reader");

    private static KafkaStatisticsReport Report(string raw) => new(
        new KafkaStatisticsIdentity("A01", "run-reader", KafkaClientRole.LoadGenProducer, "loadgen-reader"),
        "reader-session", DateTimeOffset.UnixEpoch, raw,
        new KafkaStatisticsSnapshot("native-producer", "loadgen-reader", "producer", 1, 1, 1,
        [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, 0, 0, 0, 0, 0,
            new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]),
        ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
}
