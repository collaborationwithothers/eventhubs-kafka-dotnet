using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunEvidenceReaderTests
{
    private static readonly JsonSerializerOptions ResolvedConfigurationJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private static readonly Lazy<Task<ImmutableDictionary<string, ResolvedRoleConfiguration>>> SelectedConfigurations =
        new(LoadSelectedConfigurationsAsync);

    [Fact]
    public async Task Given_CompletionBoundary_When_Read_Then_AcceptsNullableInt64AndRejectsInvalidValues()
    {
        RunIdentity identity = Identity();
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        string file = Path.Combine(path, "capabilities.json");
        JsonObject original = JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))!.AsObject();

        foreach (JsonNode? boundary in new JsonNode?[]
        {
            null,
            JsonValue.Create(long.MinValue),
            JsonValue.Create(long.MaxValue),
            JsonValue.Create(-1L)
        })
        {
            JsonObject valid = original.DeepClone().AsObject();
            valid["statistics_completion"]!["drain_boundary_microseconds"] = boundary?.DeepClone();
            await File.WriteAllTextAsync(file, valid.ToJsonString(), TestContext.Current.CancellationToken);
            _ = await ReadAsync(path, identity, TestContext.Current.CancellationToken);
        }

        JsonObject missing = original.DeepClone().AsObject();
        missing["statistics_completion"]!.AsObject().Remove("drain_boundary_microseconds");
        await File.WriteAllTextAsync(file, missing.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity, TestContext.Current.CancellationToken));

        foreach (JsonNode invalidBoundary in new JsonNode[]
        {
            JsonValue.Create("10")!,
            JsonValue.Create(10.5)!,
            JsonValue.Create(true)!,
            JsonNode.Parse("9223372036854775808")!,
            JsonNode.Parse("-9223372036854775809")!
        })
        {
            JsonObject invalid = original.DeepClone().AsObject();
            invalid["statistics_completion"]!["drain_boundary_microseconds"] = invalidBoundary.DeepClone();
            await File.WriteAllTextAsync(file, invalid.ToJsonString(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void Given_Utf8CharacterCrossesValidationChunk_When_Parsing_Then_AcceptsValidAndRejectsTruncatedEncoding()
    {
        string value = new string('a', 8185) + "😀" + new string('b', 8192);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"v\":\"" + value + "\"}\n");
        int count = RunEvidenceReader.ValidateJsonLines(bytes, bytes.Length, 1,
            row => Assert.Equal(value, row.GetProperty("v").GetString()), TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
        byte[] invalid = [.. bytes, 0xf0, 0x9f];
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            RunEvidenceReader.ValidateJsonLines(invalid, invalid.Length, 2, _ => { }, TestContext.Current.CancellationToken));
        Assert.Equal("evidence_json_invalid_utf8", failure.Message);
    }

    [Theory]
    [InlineData("{}\n{}\n")]
    [InlineData("{}\ninvalid-json\n")]
    [InlineData("{}\n")]
    public void Given_CancellationDuringEvidenceRowValidation_When_Parsing_Then_StopsBeforeAnotherRowOrSuccess(string jsonl)
    {
        using var cancellation = new CancellationTokenSource();
        int visited = 0;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(jsonl);
        OperationCanceledException failure = Assert.Throws<OperationCanceledException>(() =>
            RunEvidenceReader.ValidateJsonLines(bytes, 1024, 10, _ =>
            {
                visited++;
                cancellation.Cancel();
            }, cancellation.Token));
        Assert.Equal(1, visited);
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

    [Fact]
    public async Task Given_CanceledEvidenceValidation_When_ReadingBuffers_Then_PreservesCancellation()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        RunEvidenceExpectations expectations = await ExpectationsAsync(Identity());
        byte[] statistics = await File.ReadAllBytesAsync(Path.Combine(path, "statistics.jsonl"), TestContext.Current.CancellationToken);
        byte[] lifecycle = await File.ReadAllBytesAsync(Path.Combine(path, "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        byte[] capabilities = await File.ReadAllBytesAsync(Path.Combine(path, "capabilities.json"), TestContext.Current.CancellationToken);
        _ = RunEvidenceReader.ReadBytes(statistics, lifecycle, capabilities, Identity(), expectations, 256 * 1024, 64 * 1024, 16, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException failure = Assert.Throws<OperationCanceledException>(() =>
            RunEvidenceReader.ReadBytes(statistics, lifecycle, capabilities, Identity(), expectations,
                256 * 1024, 64 * 1024, 16, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

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
    public async Task Given_VersionOneCapabilityStatusesAreChanged_When_Read_Then_RejectsSupportedClaims()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        JsonNode supported = JsonNode.Parse(original)!;
        foreach (JsonNode? requirement in supported["request_lifecycle_requirements"]!.AsArray())
            requirement!["status"] = "supported";
        supported["request_capability_supported"] = true;
        await File.WriteAllTextAsync(file, supported.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode brokerLifecycle = JsonNode.Parse(original)!;
        JsonNode brokerRequirement = brokerLifecycle["request_lifecycle_requirements"]!.AsArray()
            .Single(requirement => requirement!["requirement"]!.GetValue<string>() == "broker_instance_lifecycle_inventory")!;
        brokerRequirement["status"] = "supported";
        await File.WriteAllTextAsync(file, brokerLifecycle.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_StatisticsArtifactWithWrongIdentity_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        RunIdentity wrong = Identity() with { RunId = "another-run" };

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, wrong, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_StatisticsWindowCountAndValueContradict_When_Read_Then_RejectsBothForms()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "statistics.jsonl");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        foreach (string windowName in new[] { "throttle", "round_trip_time" })
        {
            foreach ((int count, JsonNode? value) in new (int, JsonNode?)[]
            {
                (0, JsonValue.Create(20_001)), (1, null)
            })
            {
                JsonNode row = JsonNode.Parse(original)!;
                JsonNode window = row["typed_snapshot"]!["brokers"]![0]![windowName]!;
                window["count"] = count;
                window["value"] = value?.DeepClone();
                await File.WriteAllTextAsync(file, row.ToJsonString() + "\n", TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public async Task Given_CompletedStatisticsArtifactHasNoRows_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(path, "statistics.jsonl"), string.Empty, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_PositiveStatisticsGuardCounterIsMissingFromCompleteObservations_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string statisticsFile = Path.Combine(path, "statistics.jsonl");
        JsonNode firstStatistics = JsonNode.Parse(await File.ReadAllTextAsync(statisticsFile, TestContext.Current.CancellationToken))!;
        firstStatistics["typed_snapshot"]!["brokers"]![0]!["request_timeouts"] = 2;
        JsonNode secondStatistics = firstStatistics.DeepClone();
        secondStatistics["typed_snapshot"]!["timestamp_microseconds"] = 2;
        secondStatistics["typed_snapshot"]!["age_microseconds"] = 2;
        secondStatistics["typed_snapshot"]!["brokers"]![0]!["request_timeouts"] = 3;
        await File.WriteAllTextAsync(statisticsFile,
            firstStatistics.ToJsonString() + "\n" + secondStatistics.ToJsonString() + "\n", TestContext.Current.CancellationToken);

        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;
        capabilities["guard_observations"] = new JsonArray("native_error_callback_observed",
            "broker=broker-1;counter=req_timeouts;observed=3");
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        // Both snapshots advance and the retained counter value belongs to the later persisted row.
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        capabilities["guard_observations"] = new JsonArray("native_error_callback_observed");
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_CompleteGuardClaimsAreNotReconciledToPersistedCallbacksAndCounters_When_Read_Then_RejectsThem()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string statisticsFile = Path.Combine(path, "statistics.jsonl");
        JsonNode statistics = JsonNode.Parse(await File.ReadAllTextAsync(statisticsFile, TestContext.Current.CancellationToken))!;
        statistics["typed_snapshot"]!["brokers"]![0]!["request_timeouts"] = 2;
        await File.WriteAllTextAsync(statisticsFile, statistics.ToJsonString() + "\n", TestContext.Current.CancellationToken);

        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;
        capabilities["guard_observations"] = new JsonArray("native_error_callback_observed",
            "broker=broker-1;counter=req_timeouts;observed=2");
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        string validCounterObservation = "broker=broker-1;counter=req_timeouts;observed=2";
        string[][] malformedObservations =
        {
            ["native_error_callback_observed", validCounterObservation, "broker=broker-1;counter=req_timeouts;observed=999"],
            ["native_error_callback_observed", validCounterObservation, "broker=unpersisted;counter=txerrs;observed=9"],
            ["native_error_callback_observed", validCounterObservation, validCounterObservation]
        };
        foreach (string[] malformed in malformedObservations)
        {
            JsonArray malformedArray = [];
            foreach (string observation in malformed) malformedArray.Add(observation);
            capabilities["guard_observations"] = malformedArray;
            await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }

        // A faulted capture can retain a guard from a report that failed before it was persisted.
        statistics["typed_snapshot"]!["brokers"]![0]!["request_timeouts"] = 3;
        await File.WriteAllTextAsync(statisticsFile, statistics.ToJsonString() + "\n", TestContext.Current.CancellationToken);
        capabilities["guard_observations"] = new JsonArray("native_error_callback_observed",
            "broker=broker-1;counter=req_timeouts;observed=2",
            "broker=unpersisted;counter=txerrs;observed=9");
        capabilities["is_faulted"] = true;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_CompleteGuardClaimsNativeErrorHasNoPersistedErrorCallback_When_Read_Then_RejectsOrphan()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string lifecycleFile = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lines = await File.ReadAllLinesAsync(lifecycleFile, TestContext.Current.CancellationToken);
        int index = Array.FindIndex(lines, line => line.Contains("\"event_type\":\"native_error\"", StringComparison.Ordinal));
        Assert.True(index >= 0);
        JsonNode errorRow = JsonNode.Parse(lines[index])!;
        errorRow["is_error"] = false;
        errorRow["error_code"] = (int)ErrorCode.NoError;
        errorRow["error_name"] = ErrorCode.NoError.ToString();
        lines[index] = errorRow.ToJsonString();
        await File.WriteAllTextAsync(lifecycleFile, string.Join("\n", lines) + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ObservedBrokerNamesDisagreeWithStatisticsRows_When_Read_Then_RequiresConsistentSuccessfulCapture()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        JsonNode missing = JsonNode.Parse(original)!;
        missing["observed_broker_names"] = new JsonArray();
        await File.WriteAllTextAsync(file, missing.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode extra = JsonNode.Parse(original)!;
        extra["observed_broker_names"] = new JsonArray("broker-2");
        await File.WriteAllTextAsync(file, extra.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        // The writer records names before enqueueing the report, so a faulted capture may retain
        // a name for a report that never reached statistics.jsonl.
        JsonNode faultedExtra = JsonNode.Parse(original)!;
        faultedExtra["observed_broker_names"] = new JsonArray("broker-1", "broker-2");
        faultedExtra["is_faulted"] = true;
        await File.WriteAllTextAsync(file, faultedExtra.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_ErrorLifecycleGuardIsErased_When_Read_Then_RejectsOnlyCompleteErrorObservationLoss()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string lifecycleFile = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lifecycleLines = await File.ReadAllLinesAsync(lifecycleFile, TestContext.Current.CancellationToken);
        int errorIndex = Array.FindIndex(lifecycleLines,
            line => line.Contains("\"event_type\":\"native_error\"", StringComparison.Ordinal));
        Assert.True(errorIndex >= 0);
        JsonNode errorRow = JsonNode.Parse(lifecycleLines[errorIndex])!;
        Assert.True(errorRow["is_error"]!.GetValue<bool>());
        int originalErrorCode = errorRow["error_code"]!.GetValue<int>();
        string originalErrorName = errorRow["error_name"]!.GetValue<string>();

        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;
        capabilities["guard_observations"] = new JsonArray();
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        errorRow["is_error"] = false;
        errorRow["error_code"] = (int)ErrorCode.NoError;
        errorRow["error_name"] = ErrorCode.NoError.ToString();
        lifecycleLines[errorIndex] = errorRow.ToJsonString();
        await File.WriteAllTextAsync(lifecycleFile, string.Join("\n", lifecycleLines) + "\n", TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        errorRow["is_error"] = true;
        errorRow["error_code"] = originalErrorCode;
        errorRow["error_name"] = originalErrorName;
        lifecycleLines[errorIndex] = errorRow.ToJsonString();
        await File.WriteAllTextAsync(lifecycleFile, string.Join("\n", lifecycleLines) + "\n", TestContext.Current.CancellationToken);
        capabilities["guard_observations_complete"] = false;
        capabilities["is_faulted"] = true;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false, (int)ErrorCode.Local_TimedOut)]
    [InlineData(true, (int)ErrorCode.NoError)]
    public async Task Given_NativeErrorFlagContradictsCode_When_Read_Then_RejectsIt(bool isError, int errorCode)
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        int index = Array.FindIndex(lines, line => line.Contains("\"event_type\":\"native_error\"", StringComparison.Ordinal));
        Assert.True(index >= 0);
        JsonNode row = JsonNode.Parse(lines[index])!;
        row["is_error"] = isError;
        row["error_code"] = errorCode;
        row["error_name"] = ((ErrorCode)errorCode).ToString();
        lines[index] = row.ToJsonString();
        await File.WriteAllTextAsync(file, string.Join("\n", lines) + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ErrorCodeAndNameContradict_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        int index = Array.FindIndex(lines, line => line.Contains("\"event_type\":\"native_error\"", StringComparison.Ordinal));
        JsonNode row = JsonNode.Parse(lines[index])!;
        row["error_name"] = ErrorCode.NoError.ToString();
        lines[index] = row.ToJsonString();
        await File.WriteAllTextAsync(file, string.Join("\n", lines) + "\n", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_LifecycleReceiptSequenceIsDuplicated_When_Read_Then_RejectsItButAllowsConcurrentQueueOrderRegression()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Length);
        JsonNode first = JsonNode.Parse(lines[0])!;
        JsonNode second = JsonNode.Parse(lines[1])!;

        second["receipt_sequence"] = first["receipt_sequence"]!.GetValue<long>();
        await File.WriteAllTextAsync(file, first.ToJsonString() + "\n" + second.ToJsonString() + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        long secondSequence = JsonNode.Parse(lines[1])!["receipt_sequence"]!.GetValue<long>();
        first["receipt_sequence"] = secondSequence + 1;
        await File.WriteAllTextAsync(file, first.ToJsonString() + "\n" + lines[1] + "\n", TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_NativeLogHasInventedSourceMetadata_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "request-lifecycle.jsonl");
        string[] originalLines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        string nativeLog = originalLines.Single(line => line.Contains("\"event_type\":\"native_log\"", StringComparison.Ordinal));
        string[] malformed =
        [
            nativeLog.Replace("\"source_event_timestamp\":null", "\"source_event_timestamp\":\"1970-01-01T00:00:00+00:00\"", StringComparison.Ordinal),
            nativeLog.Replace("\"correlation_id\":null", "\"correlation_id\":\"request-1\"", StringComparison.Ordinal),
            nativeLog.Replace("\"source_order\":null", "\"source_order\":1", StringComparison.Ordinal),
            nativeLog.Replace("\"known_missing_fields\":[\"source_event_timestamp\",\"correlation_id\",\"source_order\"]", "\"known_missing_fields\":[\"source_event_timestamp\",\"correlation_id\"]", StringComparison.Ordinal),
            nativeLog.Replace("\"known_missing_fields\":[\"source_event_timestamp\",\"correlation_id\",\"source_order\"]", "\"known_missing_fields\":[\"source_event_timestamp\",\"correlation_id\",\"source_order\",\"extra\"]", StringComparison.Ordinal)
        ];

        foreach (string line in malformed)
        {
            Assert.NotEqual(nativeLog, line);
            string[] changedLines = originalLines.Select(existing => existing == nativeLog ? line : existing).ToArray();
            await File.WriteAllTextAsync(file, string.Join("\n", changedLines) + "\n", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }
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
        JsonNode secondValid = JsonNode.Parse(originalLine)!;
        secondValid["typed_snapshot"]!["timestamp_microseconds"] = 2;
        secondValid["typed_snapshot"]!["age_microseconds"] = 2;
        string secondValidLine = secondValid.ToJsonString();
        await File.WriteAllTextAsync(file, originalLine + "\n" + secondValidLine + "\n", TestContext.Current.CancellationToken);
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
        mixedRow["typed_snapshot"]!["timestamp_microseconds"] = 2;
        mixedRow["typed_snapshot"]!["age_microseconds"] = 2;
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
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadWithLimitsAsync(path, Identity(), 8, 7, 16, TestContext.Current.CancellationToken));
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
        RunIdentity identity = Identity() with { ConfigurationId = "C2" };
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, identity, TestContext.Current.CancellationToken);

        foreach ((string field, string value) in new[] { ("scenario_id", "A02"), ("configuration_id", "C0") })
        {
            JsonNode malformed = JsonNode.Parse(original)!;
            malformed["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["override"]![field] = value;
            string malformedJson = malformed.ToJsonString();
            Assert.NotEqual(original, malformedJson);
            await File.WriteAllTextAsync(file, malformedJson, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Given_C2SelectedOverrideValueIsChanged_When_Read_Then_RejectsIt()
    {
        RunIdentity identity = Identity() with { ConfigurationId = "C2" };
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        string file = Path.Combine(path, "capabilities.json");
        JsonNode original = JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))!;
        Assert.Equal("5000", original["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["value"]!.GetValue<string>());

        JsonNode malformed = original.DeepClone();
        malformed["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["value"] = "4000";
        string malformedJson = malformed.ToJsonString();
        Assert.NotEqual(original.ToJsonString(), malformedJson);
        await File.WriteAllTextAsync(file, malformedJson, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_C0SelectedConfigurationContainsInventedOverride_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        JsonNode original = JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken))!;
        Assert.Null(original["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["override"]);

        JsonNode malformed = original.DeepClone();
        malformed["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["override"] = JsonNode.Parse(
            "{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"location\":{\"path\":\"scenarios/A01/scenario.yaml\",\"line\":13,\"column\":7}}");
        malformed["resolved_configuration"]!["settings"]!["request.timeout.ms"]!["value"] = "5000";
        string malformedJson = malformed.ToJsonString();
        Assert.NotEqual(original.ToJsonString(), malformedJson);
        await File.WriteAllTextAsync(file, malformedJson, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_UnoverriddenResolvedSettingDiffersFromBaselineOrRole_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string valid = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(file, valid, TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);

        foreach ((string field, string value) in new[] { ("value", "another-broker"), ("role", "1") })
        {
            JsonNode malformed = JsonNode.Parse(valid)!;
            if (field == "value")
                malformed["resolved_configuration"]!["settings"]!["bootstrap.servers"]!["value"] = value;
            else
                malformed["resolved_configuration"]!["settings"]!["bootstrap.servers"]!["baseline"]!["role"] = 1;
            string malformedJson = malformed.ToJsonString();
            Assert.NotEqual(valid, malformedJson);
            await File.WriteAllTextAsync(file, malformedJson, TestContext.Current.CancellationToken);
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
    public async Task Given_CompleteStatisticsHasReportDiagnosticsOrUnparsedReport_When_Read_Then_RejectsContradiction()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string statisticsFile = Path.Combine(path, "statistics.jsonl");
        JsonNode validRow = JsonNode.Parse(await File.ReadAllTextAsync(statisticsFile, TestContext.Current.CancellationToken))!;

        JsonNode diagnosticRow = validRow.DeepClone();
        diagnosticRow["typed_snapshot"]!["timestamp_microseconds"] = 2;
        diagnosticRow["typed_snapshot"]!["age_microseconds"] = 2;
        diagnosticRow["diagnostics"] = new JsonArray
        {
            JsonNode.Parse("{\"code\":\"malformed_field\",\"broker\":null,\"field\":\"brokers\"}")
        };
        await File.WriteAllTextAsync(statisticsFile,
            validRow.ToJsonString() + "\n" + diagnosticRow.ToJsonString() + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;
        capabilities["statistics_completion"]!["is_complete"] = false;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
        capabilities["statistics_completion"]!["is_complete"] = true;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        // Parser failures have a null typed snapshot and a diagnostic. Rejecting the null snapshot
        // independently means erased diagnostics cannot make a complete artifact appear valid.
        JsonNode unparsedRow = validRow.DeepClone();
        unparsedRow["typed_snapshot"] = null;
        unparsedRow["diagnostics"] = new JsonArray();
        await File.WriteAllTextAsync(statisticsFile,
            validRow.ToJsonString() + "\n" + unparsedRow.ToJsonString() + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        capabilities["statistics_completion"]!["is_complete"] = false;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_CompleteStatisticsHidesNativeGenerationOrCounterContradictions_When_Read_Then_RejectsOnlyCompleteReceipt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string statisticsFile = Path.Combine(path, "statistics.jsonl");
        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode baseline = JsonNode.Parse(await File.ReadAllTextAsync(statisticsFile, TestContext.Current.CancellationToken))!;
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;

        async Task VerifyCompleteRejectedIncompleteAccepted(params JsonNode[] rows)
        {
            await File.WriteAllTextAsync(statisticsFile,
                string.Join("\n", rows.Select(row => row.ToJsonString())) + "\n", TestContext.Current.CancellationToken);
            capabilities["statistics_completion"]!["is_complete"] = true;
            await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

            capabilities["statistics_completion"]!["is_complete"] = false;
            await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
            _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
        }

        JsonNode duplicateTimestamp = baseline.DeepClone();
        await VerifyCompleteRejectedIncompleteAccepted(baseline.DeepClone(), duplicateTimestamp);
        capabilities["statistics_completion"] = null;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, Identity(), TestContext.Current.CancellationToken);
        capabilities["statistics_completion"] = new JsonObject
        {
            ["is_complete"] = false,
            ["diagnostics"] = new JsonArray(),
            ["missing_brokers"] = new JsonArray(),
            ["drain_boundary_microseconds"] = null
        };
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        JsonNode changedCreation = baseline.DeepClone();
        changedCreation["typed_snapshot"]!["timestamp_microseconds"] = 2;
        changedCreation["typed_snapshot"]!["age_microseconds"] = 0;
        await VerifyCompleteRejectedIncompleteAccepted(baseline.DeepClone(), changedCreation);

        foreach ((string field, string? guardCounter) in new (string, string?)[]
        {
            ("request_timeouts", "req_timeouts"),
            ("disconnects", null),
            ("transmission_retries", "txretries"),
            ("transmission_errors", "txerrs"),
            ("receive_errors", "rxerrs")
        })
        {
            JsonNode first = baseline.DeepClone();
            first["typed_snapshot"]!["brokers"]![0]![field] = 5;
            JsonNode second = baseline.DeepClone();
            second["typed_snapshot"]!["timestamp_microseconds"] = 2;
            second["typed_snapshot"]!["age_microseconds"] = 2;
            second["typed_snapshot"]!["brokers"]![0]![field] = 4;
            JsonArray guards = new("native_error_callback_observed");
            if (guardCounter is not null)
                guards.Add($"broker=broker-1;counter={guardCounter};observed=5");
            capabilities["guard_observations"] = guards;
            await VerifyCompleteRejectedIncompleteAccepted(first, second);
        }

        capabilities["guard_observations"] = new JsonArray("native_error_callback_observed");
        JsonNode duplicateBroker = baseline.DeepClone();
        duplicateBroker["typed_snapshot"]!["brokers"]!.AsArray().Add(
            duplicateBroker["typed_snapshot"]!["brokers"]![0]!.DeepClone());
        await VerifyCompleteRejectedIncompleteAccepted(duplicateBroker);
    }

    [Theory]
    [InlineData("native_log", "provenance", "invented")]
    [InlineData("native_log", "session_id", "invented")]
    [InlineData("native_log", "inference", "invented")]
    [InlineData("native_error", "provenance", "invented")]
    [InlineData("native_error", "session_id", "invented")]
    public async Task Given_NativeCallbackHasNoncanonicalAttribution_When_Read_Then_RejectsIt(
        string eventType, string field, string value)
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "request-lifecycle.jsonl");
        string[] lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        int index = Array.FindIndex(lines, line => line.Contains("\"event_type\":\"" + eventType + "\"", StringComparison.Ordinal));
        Assert.True(index >= 0);
        JsonNode row = JsonNode.Parse(lines[index])!;
        row[field] = value;
        lines[index] = row.ToJsonString();
        await File.WriteAllTextAsync(file, string.Join("\n", lines) + "\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_StatisticsCompletionDisagreesWithTrustedBrokerInventory_When_Read_Then_RejectsMissingExpectedBrokerButAllowsExtraObservedBroker()
    {
        RunIdentity identity = Identity();
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity,
            TestContext.Current.CancellationToken, expectedBrokerNames: ImmutableArray.Create("broker-2")));

        string capabilitiesFile = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesFile, TestContext.Current.CancellationToken))!;
        capabilities["statistics_completion"]!["is_complete"] = false;
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, identity,
            TestContext.Current.CancellationToken, expectedBrokerNames: ImmutableArray.Create("broker-2")));

        capabilities["statistics_completion"]!["missing_brokers"] = new JsonArray("broker-2");
        await File.WriteAllTextAsync(capabilitiesFile, capabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(path, identity, TestContext.Current.CancellationToken,
            expectedBrokerNames: ImmutableArray.Create("broker-2"));

        // Persisted extra brokers may legitimately be observed beyond the independently selected
        // inventory. They do not invalidate complete coverage of the expected broker set.
        string extraPath = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        string statisticsFile = Path.Combine(extraPath, "statistics.jsonl");
        JsonNode row = JsonNode.Parse(await File.ReadAllTextAsync(statisticsFile, TestContext.Current.CancellationToken))!;
        JsonNode extraBroker = row["typed_snapshot"]!["brokers"]![0]!.DeepClone();
        extraBroker["name"] = "broker-2";
        row["typed_snapshot"]!["brokers"]!.AsArray().Add(extraBroker);
        await File.WriteAllTextAsync(statisticsFile, row.ToJsonString() + "\n", TestContext.Current.CancellationToken);
        JsonNode extraCapabilities = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(extraPath, "capabilities.json"), TestContext.Current.CancellationToken))!;
        extraCapabilities["observed_broker_names"] = new JsonArray("broker-1", "broker-2");
        await File.WriteAllTextAsync(Path.Combine(extraPath, "capabilities.json"), extraCapabilities.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(extraPath, identity, TestContext.Current.CancellationToken,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));
    }

    [Fact]
    public async Task Given_ResolvedConfiguration_When_Validating_Then_RejectsNullAndUnknownNestedMembers()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string json = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        JsonNode nullConfiguration = JsonNode.Parse(json)!;
        nullConfiguration["resolved_configuration"] = null;
        await File.WriteAllTextAsync(file, nullConfiguration.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode emptyConfiguration = JsonNode.Parse(json)!;
        emptyConfiguration["resolved_configuration"] = JsonNode.Parse("{\"role\":0,\"settings\":{},\"omissions\":[]}");
        await File.WriteAllTextAsync(file, emptyConfiguration.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode incompleteConfiguration = JsonNode.Parse(json)!;
        incompleteConfiguration["resolved_configuration"] = await SerializeSelectedConfigurationAsync(Identity());
        incompleteConfiguration["resolved_configuration"]!["settings"]!.AsObject().Remove("bootstrap.servers");
        await File.WriteAllTextAsync(file, incompleteConfiguration.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode missingOmissions = JsonNode.Parse(json)!;
        missingOmissions["resolved_configuration"] = await SerializeSelectedConfigurationAsync(Identity());
        missingOmissions["resolved_configuration"]!["omissions"] = new JsonArray();
        await File.WriteAllTextAsync(file, missingOmissions.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        foreach (JsonNode? malformedOmission in new JsonNode?[] { JsonValue.Create("bad-omission"), null })
        {
            JsonNode invalidOmission = JsonNode.Parse(json)!;
            invalidOmission["resolved_configuration"]!["omissions"] = new JsonArray(malformedOmission?.DeepClone());
            await File.WriteAllTextAsync(file, invalidOmission.ToJsonString(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
        }

        JsonNode wrongRole = JsonNode.Parse(json)!;
        wrongRole["resolved_configuration"]!["role"] = 1;
        await File.WriteAllTextAsync(file, wrongRole.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));

        JsonNode unknownSetting = JsonNode.Parse(json)!;
        unknownSetting["resolved_configuration"]!["settings"]!["unknown"] = JsonNode.Parse("{}");
        await File.WriteAllTextAsync(file, unknownSetting.ToJsonString(), TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task Given_RequiredStatisticsCompletionIsErased_When_Read_Then_RejectsItWhileOptionalEvidenceRemainsOptional()
    {
        RunIdentity identity = Identity();
        string requiredPath = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity,
            requireStatisticsEvidence: true);
        string requiredFile = Path.Combine(requiredPath, "capabilities.json");
        JsonNode required = JsonNode.Parse(await File.ReadAllTextAsync(requiredFile, TestContext.Current.CancellationToken))!;
        required["statistics_completion"] = null;
        await File.WriteAllTextAsync(requiredFile, required.ToJsonString(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(requiredPath, identity,
            TestContext.Current.CancellationToken, requireStatisticsEvidence: true));

        string optionalPath = await CreateEvidenceAsync(TestContext.Current.CancellationToken, identity);
        string optionalFile = Path.Combine(optionalPath, "capabilities.json");
        JsonNode optional = JsonNode.Parse(await File.ReadAllTextAsync(optionalFile, TestContext.Current.CancellationToken))!;
        optional["statistics_completion"] = null;
        await File.WriteAllTextAsync(optionalFile, optional.ToJsonString(), TestContext.Current.CancellationToken);
        _ = await ReadAsync(optionalPath, identity, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_CanonicalRequestCapabilityClaimIsAltered_When_Read_Then_RejectsIt()
    {
        string path = await CreateEvidenceAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(path, "capabilities.json");
        string original = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        JsonNode capabilities = JsonNode.Parse(original)!;
        JsonNode claim = capabilities["request_lifecycle_requirements"]!.AsArray()
            .Single(requirement => requirement!["requirement"]!.GetValue<string>() == "request_identity")!;
        claim["source_facts"] = "unverified claim";
        string malformedJson = capabilities.ToJsonString();
        Assert.NotEqual(original, malformedJson);
        await File.WriteAllTextAsync(file, malformedJson, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(path, Identity(), TestContext.Current.CancellationToken));
    }

    private static async Task<string> CreateEvidenceAsync(CancellationToken cancellationToken,
        RunIdentity? identity = null, bool requireStatisticsEvidence = false)
    {
        RunIdentity runIdentity = identity ?? Identity();
        RunEvidenceExpectations expectations = await ExpectationsAsync(runIdentity, requireStatisticsEvidence);
        string path = Path.Combine(Path.GetTempPath(), "evidence-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        await using var session = new RunEvidenceSession(runIdentity,
            new RunEvidenceOptions(path, 8, 64 * 1024, 256 * 1024, 16, TimeSpan.FromSeconds(2)), TimeProvider.System,
            expectations);
        string rawWithNativeExtra = "{\"name\":\"native-producer\",\"vendor_extra\":{\"preserved\":true}}";
        await session.WriteAsync(Report(rawWithNativeExtra, runIdentity), cancellationToken);
        session.CaptureLog(new LogMessage("native-producer", SyslogLevel.Warning, "BROKER", "timed out"));
        session.CaptureError(new Error(ErrorCode.Local_TimedOut));
        await session.CompleteAsync(new KafkaStatisticsCompletion(true, ImmutableArray<KafkaStatisticsDiagnostic>.Empty, ImmutableArray<string>.Empty), cancellationToken);
        Assert.False(session.IsFaulted);
        return path;
    }

    internal static async Task<RunEvidenceExpectations> ExpectationsAsync(RunIdentity identity,
        bool requireStatisticsEvidence = false, ImmutableArray<string>? expectedBrokerNames = null)
    {
        ImmutableDictionary<string, ResolvedRoleConfiguration> configurations = StringComparer.Ordinal.Equals(identity.ClientId, Identity().ClientId)
            ? await SelectedConfigurations.Value
            : await LoadSelectedConfigurationsAsync(identity.ClientId);
        if (!configurations.TryGetValue(identity.ConfigurationId, out ResolvedRoleConfiguration? configuration))
            throw new InvalidOperationException("evidence_test_configuration_missing");
        return new RunEvidenceExpectations(configuration, requireStatisticsEvidence,
            expectedBrokerNames ?? ImmutableArray.Create("broker-1"));
    }

    private static async Task<JsonObject> SerializeSelectedConfigurationAsync(RunIdentity identity)
    {
        RunEvidenceExpectations expectations = await ExpectationsAsync(identity);
        return JsonSerializer.SerializeToNode(expectations.ResolvedConfiguration, ResolvedConfigurationJsonOptions)!.AsObject();
    }

    private static async Task<RunEvidenceReadResult> ReadAsync(string path, RunIdentity identity,
        CancellationToken cancellationToken, bool requireStatisticsEvidence = false,
        ImmutableArray<string>? expectedBrokerNames = null)
    {
        RunEvidenceExpectations expectations = await ExpectationsAsync(identity, requireStatisticsEvidence, expectedBrokerNames);
        return await RunEvidenceReader.ReadAsync(path, identity, expectations, 256 * 1024, 64 * 1024, 16,
            cancellationToken);
    }

    private static async Task<RunEvidenceReadResult> ReadWithLimitsAsync(string path, RunIdentity identity,
        long maximumFileBytes, int maximumRowBytes, int maximumDiagnostics, CancellationToken cancellationToken)
    {
        RunEvidenceExpectations expectations = await ExpectationsAsync(identity);
        return await RunEvidenceReader.ReadAsync(path, identity, expectations, maximumFileBytes, maximumRowBytes,
            maximumDiagnostics, cancellationToken);
    }

    private static Task<ImmutableDictionary<string, ResolvedRoleConfiguration>> LoadSelectedConfigurationsAsync() =>
        LoadSelectedConfigurationsAsync(Identity().ClientId);

    private static async Task<ImmutableDictionary<string, ResolvedRoleConfiguration>> LoadSelectedConfigurationsAsync(string clientId)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "reader-validation";
            options.ClientId = clientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        var loader = new ScenarioLoader(new ReaderScenarioSource(), Options.Create(new ScenarioLoadOptions
        {
            MaxDocumentCharacters = 32 * 1024,
            MaxNestingDepth = 32,
            MaxScalarCharacters = 8 * 1024,
            MaxParserEvents = 20_000
        }));
        LoadedScenario loaded = await loader.LoadAsync("A01");
        ResolvedScenario resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        return resolved.Configurations.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value.Roles[KafkaClientRole.LoadGenProducer], StringComparer.Ordinal);
    }

    private sealed class ReaderScenarioSource : IScenarioSource
    {
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string document = logicalPath switch
            {
                "scenarios/A01/scenario.yaml" => """
                    schema_version: 1
                    id: A01
                    baseline: Parchwell.Kafka
                    roles: [LoadGenProducer]
                    profile: profiles/reader.yaml
                    configurations:
                      C0: {}
                      C2:
                        LoadGenProducer:
                          request.timeout.ms: 5000
                    fault:
                      live: {kind: none}
                      component: {kind: none}
                    repeats: 5
                    gate: {kind: none}
                    measures:
                      - {id: ledger.delivery_latency_ms, role: LoadGenProducer}
                    """,
                "scenarios/profiles/reader.yaml" => """
                    schema_version: 1
                    id: reader
                    seed: -17
                    tenants: [{id: reader, weight: 1}]
                    documents_per_burst: 1
                    payloads:
                      uploaded: [{size_bytes: 4, weight: 1}]
                      ocr-started: [{size_bytes: 4, weight: 1}]
                      ocr-finished: [{size_bytes: 4, weight: 1}]
                      classified: [{size_bytes: 4, weight: 1}]
                      indexed: [{size_bytes: 4, weight: 1}]
                    stages: [{id: reader, offered_attempts_per_second: 1, duration_seconds: 1}]
                    """,
                _ => throw new FileNotFoundException("evidence_test_scenario_input_missing")
            };
            return ValueTask.FromResult<TextReader>(new StringReader(document));
        }
    }

    private static RunIdentity Identity() => new("A01", "C0", "run-reader", "instance-reader", "instance-reader", "loadgen-reader");

    private static KafkaStatisticsReport Report(string raw, RunIdentity identity) => new(
        new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer, identity.ClientId),
        "reader-session", DateTimeOffset.UnixEpoch, raw,
        new KafkaStatisticsSnapshot("native-producer", identity.ClientId, "producer", 1, 1, 1,
        [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, 0, 0, 0, 0, 0,
            new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]),
        ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
}
