using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Theory]
    [InlineData("client_id", "another-client")]
    [InlineData("type", "consumer")]
    public async Task Given_StatisticsSnapshotWithWrongClientOrRole_When_Read_Then_RejectsContradictoryAttribution(string field, string value)
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "statistics.jsonl");
        JsonNode row = JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))!;
        row["typed_snapshot"]![field] = value;
        Assert.Equal("A01", row["identity"]!["scenario_id"]!.GetValue<string>());
        Assert.Equal("run-reader", row["identity"]!["run_id"]!.GetValue<string>());
        Assert.Equal(Identity().ClientId, row["identity"]!["client_id"]!.GetValue<string>());
        Assert.Equal(Identity().ClientId, row["statistics_identity"]!["client_id"]!.GetValue<string>());
        await File.WriteAllTextAsync(file, row.ToJsonString() + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("session_id", "stats-session-other")]
    [InlineData("native_name", "another-native-producer")]
    public async Task Given_StatisticsArtifactWithMixedSessionOrNativeName_When_Read_Then_RejectsMixedAttribution(string field, string value)
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "statistics.jsonl");
        string originalLine = (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).TrimEnd('\n');
        await File.WriteAllTextAsync(file, originalLine + "\n" + originalLine + "\n", TestContext.Current.CancellationToken);
        RunEvidenceReadResult consistent = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
        Assert.Equal(2, consistent.StatisticsRecordCount);

        JsonNode mixedRow = JsonNode.Parse(originalLine)!;
        if (field == "native_name")
        {
            mixedRow["typed_snapshot"]!["name"] = value;
        }
        else
        {
            mixedRow[field] = value;
        }
        Assert.Equal("run-reader", mixedRow["identity"]!["run_id"]!.GetValue<string>());
        Assert.Equal(Identity().ClientId, mixedRow["identity"]!["client_id"]!.GetValue<string>());
        await File.WriteAllTextAsync(file, originalLine + "\n" + mixedRow.ToJsonString() + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
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
    public async Task Given_CapabilityDocumentWithDuplicateTopLevelIdentityOrNestedRequirementProperty_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        string[] malformed =
        [
            original.Replace("\"is_faulted\":false", "\"is_faulted\":false,\"is_faulted\":true", StringComparison.Ordinal),
            original.Replace("\"scenario_id\":\"A01\"", "\"scenario_id\":\"A01\",\"scenario_id\":\"A01\"", StringComparison.Ordinal),
            original.Replace("\"status\":\"unsupported\"", "\"status\":\"unsupported\",\"status\":\"supported\"", StringComparison.Ordinal)
        ];

        foreach (string json in malformed)
        {
            Assert.NotEqual(original, json);
            await File.WriteAllTextAsync(file, json, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Given_ResolvedOverrideFromAnotherScenarioOrConfiguration_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        const string emptyResolved = "\"resolved_configuration\":null";
        const string resolved = "\"resolved_configuration\":{\"role\":0,\"settings\":{\"bootstrap.servers\":{\"value\":\"broker\",\"baseline\":{\"key\":\"bootstrap.servers\",\"value\":\"broker\",\"role\":0,\"disposition\":0,\"inventory_ids\":[\"fixture\"],\"sources\":[{\"url\":\"https://example.test/source\",\"version_or_date\":\"fixture\"}],\"selection_reason\":\"fixture\"},\"override\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"location\":{\"path\":\"scenario.json\",\"line\":1,\"column\":1}}}},\"omissions\":[]}";
        Assert.Contains(emptyResolved, original, StringComparison.Ordinal);
        string valid = original.Replace(emptyResolved, resolved, StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, valid, TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        foreach ((string expected, string actual) in new[]
        {
            ("\"override\":{\"scenario_id\":\"A01\"", "\"override\":{\"scenario_id\":\"A02\""),
            ("\"override\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\"", "\"override\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C2\"")
        })
        {
            string malformed = valid.Replace(expected, actual, StringComparison.Ordinal);
            Assert.NotEqual(valid, malformed);
            await File.WriteAllTextAsync(file, malformed, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Given_CompleteStatisticsWithDiagnosticsOrMissingBrokers_When_Read_Then_RejectsContradiction()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        string[] malformed =
        [
            original.Replace("\"diagnostics\":[],\"missing_brokers\":[]", "\"diagnostics\":[{\"code\":\"capture_loss\",\"broker\":null,\"field\":null}],\"missing_brokers\":[]", StringComparison.Ordinal),
            original.Replace("\"diagnostics\":[],\"missing_brokers\":[]", "\"diagnostics\":[],\"missing_brokers\":[\"broker-2\"]", StringComparison.Ordinal)
        ];

        foreach (string json in malformed)
        {
            Assert.NotEqual(original, json);
            await File.WriteAllTextAsync(file, json, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }
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
