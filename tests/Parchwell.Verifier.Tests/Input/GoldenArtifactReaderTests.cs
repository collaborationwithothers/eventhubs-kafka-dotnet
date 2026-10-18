using System.Text;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Input;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class GoldenArtifactReaderTests
{
    [Fact]
    public void Given_GoldenArtifacts_When_IndependentReadersParse_Then_PreserveSchemaOneEvidence()
    {
        byte[] ledgerBytes = ReadGolden("ledger.jsonl");
        byte[] manifestBytes = ReadGolden("manifest.json");
        byte[] indexBytes = ReadGolden("index.json");
        byte[] statisticsBytes = ReadGolden("statistics.jsonl");
        byte[] lifecycleBytes = ReadGolden("request-lifecycle.jsonl");
        byte[] capabilitiesBytes = ReadGolden("capabilities.json");
        var limits = Limits();
        var budget = new InputBudget(limits);

        ParseResult<IReadOnlyList<LedgerRow>> ledger = LedgerReader.ReadRows(ledgerBytes, limits, budget);
        Assert.True(ledger.IsSuccess);
        Assert.Equal(2, ledger.Value!.Count);
        Assert.Equal("golden-run-001", ledger.Value[0].Identity.RunId);
        Assert.Equal(1, ledger.Value[0].SchemaVersion);
        Assert.Equal("A01", ledger.Value[0].Identity.ScenarioId);
        Assert.Equal("C0", ledger.Value[0].Identity.ConfigurationId);
        Assert.Equal("golden-run-001", ledger.Value[0].Identity.RunId);
        Assert.Equal("golden-instance-001", ledger.Value[0].Identity.InstanceId);
        Assert.Equal("golden-instance-001", ledger.Value[0].Identity.ExpectedInstanceId);
        Assert.Equal("golden-loadgen-client", ledger.Value[0].Identity.ClientId);
        Assert.Equal("golden-attempt-000", ledger.Value[0].Attempt.AttemptId);
        Assert.Equal(0L, ledger.Value[0].Attempt.Ordinal);
        Assert.Equal("tenant-golden-d0", ledger.Value[0].Attempt.Key);
        Assert.Equal("callback", ledger.Value[0].CompletionSource);
        Assert.True(ledger.Value[0].Delivered);
        Assert.Equal(2, ledger.Value[0].PersistenceStatus.Value);
        Assert.Equal(FieldPresence.Value, ledger.Value[0].PersistenceStatus.Presence);
        Assert.Equal(0.25, ledger.Value[0].DeliveryLatencyMs.Value);
        Assert.Equal(FieldPresence.Null, ledger.Value[0].SynchronousCallDurationMs.Presence);
        Assert.Equal("golden-attempt-001", ledger.Value[1].Attempt.AttemptId);
        Assert.Equal("synchronous_rejection", ledger.Value[1].CompletionSource);
        Assert.False(ledger.Value[1].Delivered);
        Assert.Equal(-184, ledger.Value[1].ErrorCode);
        Assert.Equal("Local_QueueFull", ledger.Value[1].ErrorName.Value);
        Assert.Equal(FieldPresence.Null, ledger.Value[1].PersistenceStatus.Presence);
        Assert.Equal(0.125, ledger.Value[1].SynchronousCallDurationMs.Value);

        ParseResult<SealedRunManifest> manifest = ManifestReader.Read(manifestBytes, limits, budget, 2);
        Assert.True(manifest.IsSuccess);
        Assert.Equal("golden-run-001", manifest.Value!.Identity.RunId);
        AssertIdentity(manifest.Value.Identity);
        Assert.Equal(1, manifest.Value.SchemaVersion);
        Assert.Equal("1", manifest.Value.GeneratorVersion);
        Assert.Equal(2L, manifest.Value.PlannedAttemptCount);
        Assert.Equal(2L, manifest.Value.AttemptCount);
        Assert.Equal(2, manifest.Value.Inventory.Count);
        Assert.Equal("golden-attempt-001", manifest.Value.Inventory[1].AttemptId);
        Assert.Equal("complete", manifest.Value.WorkerStatus);
        Assert.True(manifest.Value.BookkeepingComplete);
        Assert.Equal(FieldPresence.Null, manifest.Value.FirstUnexecutedOrdinal.Presence);
        Assert.Equal("2.16.0", manifest.Value.RuntimeVersions["confluent_kafka"]);
        Assert.Equal("statistics.jsonl", manifest.Value.EvidenceFiles["statistics.jsonl"]);
        Assert.Contains("broker_lifecycle_inventory_unproven", manifest.Value.GuardObservations);
        Assert.Contains("request_lifecycle_capture_unsupported", manifest.Value.GuardObservations);

        ParseResult<ArtifactIndex> index = ArtifactIndexReader.Read(indexBytes, limits, budget);
        Assert.True(index.IsSuccess);
        Assert.False(index.IsExecutionFailure);
        Assert.Equal("golden-run-001", index.Value!.Identity.RunId);
        AssertIdentity(index.Value.Identity);
        Assert.Equal(1, index.Value.SchemaVersion);
        Assert.Equal("complete", index.Value.WorkerStatus);
        Assert.True(index.Value.BookkeepingComplete);
        Assert.True(index.Value.WorkerComplete);
        Assert.True(index.Value.PublicationComplete);
        Assert.Equal(5, index.Value.Artifacts.Count);
        Assert.DoesNotContain(index.Value.Artifacts, artifact => artifact.Name == "index.json");
        Assert.Equal("ledger.jsonl", index.Value.Artifacts[0].Name);
        Assert.Equal(1467L, index.Value.Artifacts[0].Length);
        Assert.Equal("bf5fdb1cec62fe6b5976b64a6dbad1eb968201355babb03e8aa76dd7e1fb52b1", index.Value.Artifacts[0].Sha256);

        ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics = CapturedEvidenceReader.ReadStatistics(
            statisticsBytes, "statistics.jsonl", limits, budget);
        Assert.True(statistics.IsSuccess);
        Assert.Equal(2, statistics.Value!.Count);
        AssertIdentity(statistics.Value[0].Identity);
        Assert.Equal(1, statistics.Value[0].SchemaVersion);
        Assert.Equal("golden-statistics-session", statistics.Value[0].SessionId);
        Assert.Equal("2026-10-17T12:34:56.789+00:00", statistics.Value[0].ReceivedAt);
        Assert.Equal("golden-client#producer", statistics.Value[0].TypedSnapshot.Value!.Name);
        Assert.Equal(0L, statistics.Value[0].TypedSnapshot.Value!.Brokers[0].Throttle.Count);
        Assert.Equal(FieldPresence.Null, statistics.Value[0].TypedSnapshot.Value!.Brokers[0].Throttle.Value.Presence);
        Assert.StartsWith("{\n  \"name\":\"golden-client#producer\",", statistics.Value[0].RawJson);
        ParseResult<NativeClientReport> nativeFirst = NativeStatisticsReader.Read(
            Encoding.UTF8.GetBytes(statistics.Value[0].RawJson), "statistics.jsonl", limits, budget, 65536, 20);
        Assert.True(nativeFirst.IsSuccess);
        Assert.Equal(10_000_000L, nativeFirst.Value!.TimestampMicroseconds.Value);
        Assert.Equal(1_000_000L, nativeFirst.Value.AgeMicroseconds.Value);
        Assert.Equal("broker-a/1", nativeFirst.Value.Brokers.Value!["broker-a/1"].DictionaryKey);
        Assert.Equal("learned", nativeFirst.Value.Brokers.Value!["broker-a/1"].Source.Value);
        Assert.Equal(1L, nativeFirst.Value.Brokers.Value!["broker-a/1"].NodeId.Value);
        Assert.Equal("UP", nativeFirst.Value.Brokers.Value!["broker-a/1"].State.Value);
        Assert.Equal(0L, nativeFirst.Value.Brokers.Value["broker-a/1"].Throttle.Value!.Count.Value);
        Assert.Equal(0L, nativeFirst.Value.Brokers.Value!["broker-a/1"].Throttle.Value!.Maximum.Value);
        Assert.Equal(0L, nativeFirst.Value.Brokers.Value["broker-a/1"].RoundTripTime.Value!.Count.Value);
        Assert.Equal(0L, nativeFirst.Value.Brokers.Value!["broker-a/1"].RoundTripTime.Value!.P99.Value);
        Assert.Equal(FieldPresence.Missing, nativeFirst.Value.Brokers.Value["broker-a/1"].Throttle.Value!.P99.Presence);
        ParseResult<NativeClientReport> nativeSecond = NativeStatisticsReader.Read(
            Encoding.UTF8.GetBytes(statistics.Value[1].RawJson), "statistics.jsonl", limits, budget, 65536, 20);
        Assert.True(nativeSecond.IsSuccess);
        Assert.Equal(15_000_000L, nativeSecond.Value!.TimestampMicroseconds.Value);
        Assert.Equal(6_000_000L, nativeSecond.Value.AgeMicroseconds.Value);
        Assert.Equal(2L, nativeSecond.Value.Brokers.Value!["broker-a/1"].Throttle.Value!.Count.Value);
        Assert.Equal(17L, nativeSecond.Value.Brokers.Value!["broker-a/1"].Throttle.Value!.Maximum.Value);
        Assert.Equal(4L, nativeSecond.Value.Brokers.Value!["broker-a/1"].RoundTripTime.Value!.Count.Value);
        Assert.Equal(99_327L, nativeSecond.Value.Brokers.Value["broker-a/1"].RoundTripTime.Value!.P99.Value);

        ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle = CapturedEvidenceReader.ReadLifecycle(
            lifecycleBytes, "request-lifecycle.jsonl", limits, budget);
        Assert.True(lifecycle.IsSuccess);
        Assert.Equal(2, lifecycle.Value!.Count);
        NativeLog nativeLog = Assert.IsType<NativeLog>(lifecycle.Value[0]);
        AssertIdentity(nativeLog.Identity);
        Assert.Equal(1, nativeLog.SchemaVersion);
        Assert.Equal("managed_log_callback", nativeLog.SessionId);
        Assert.Equal(1L, nativeLog.ReceiptSequence);
        Assert.Equal(FieldPresence.Null, nativeLog.SourceEventTimestamp.Presence);
        Assert.Equal(FieldPresence.Null, nativeLog.CorrelationId.Presence);
        Assert.Equal(FieldPresence.Null, nativeLog.SourceOrder.Presence);
        Assert.Equal(FieldPresence.Null, nativeLog.Inference.Presence);
        Assert.Equal(3, nativeLog.KnownMissingFields.Count);
        Assert.Equal("Info", nativeLog.Level);
        Assert.Equal("BROKER", nativeLog.Facility);
        NativeError nativeError = Assert.IsType<NativeError>(lifecycle.Value[1]);
        Assert.Equal("managed_error_callback", nativeError.SessionId);
        Assert.Equal(2L, nativeError.ReceiptSequence);
        Assert.Equal(0, nativeError.ErrorCode);
        Assert.Equal("NoError", nativeError.ErrorName);
        Assert.Equal("Success", nativeError.Reason.Value);
        Assert.False(nativeError.IsError);
        Assert.False(nativeError.IsFatal);

        ParseResult<CapturedCapabilities> capabilities = CapturedEvidenceReader.ReadCapabilities(
            capabilitiesBytes, "capabilities.json", limits, budget);
        Assert.True(capabilities.IsSuccess);
        Assert.Equal("golden-run-001", capabilities.Value!.Identity.RunId);
        AssertIdentity(capabilities.Value.Identity);
        Assert.Equal(1, capabilities.Value.SchemaVersion);
        Assert.Equal(3, capabilities.Value.Artifacts.Count);
        Assert.False(capabilities.Value.RequestCapabilitySupported);
        Assert.True(capabilities.Value.GuardObservationsComplete);
        Assert.Equal(FieldPresence.Value, capabilities.Value.ResolvedConfiguration.Presence);
        Assert.True(capabilities.Value.ResolvedConfiguration.Value!.Settings.Count > 0);
        Assert.Equal("-1", capabilities.Value.ResolvedConfiguration.Value.Settings["acks"].Value);
        Assert.Equal(FieldPresence.Value, capabilities.Value.StatisticsCompletion.Presence);
        Assert.True(capabilities.Value.StatisticsCompletion.Value!.IsComplete);
        Assert.Equal(12_000_000L, capabilities.Value.StatisticsCompletion.Value.DrainBoundaryMicroseconds.Value);
        Assert.Empty(capabilities.Value.GuardObservations);
        Assert.Equal(28_731L, index.Value.Artifacts[4].Length);
    }

    private static byte[] ReadGolden(string file) => File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory, "fixtures", "verifier-v1", "captured-run", file));

    internal static InputLimits Limits() => new(64, 100_000, 1_000_000, 50_000, 100, 100_000, 2048);

    private static void AssertIdentity(Parchwell.Verifier.Common.Contracts.RunIdentity identity)
    {
        Assert.Equal("A01", identity.ScenarioId);
        Assert.Equal("C0", identity.ConfigurationId);
        Assert.Equal("golden-run-001", identity.RunId);
        Assert.Equal("golden-instance-001", identity.InstanceId);
        Assert.Equal("golden-instance-001", identity.ExpectedInstanceId);
        Assert.Equal("golden-loadgen-client", identity.ClientId);
    }
}
#pragma warning restore CA1707
