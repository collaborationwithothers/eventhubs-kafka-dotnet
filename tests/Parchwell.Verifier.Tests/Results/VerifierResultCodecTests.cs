using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Results;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class VerifierResultCodecTests
{
    private static readonly OutputLimits OutputLimits = new(65_536, 8, 256);
    private static readonly string[] OutcomeCountNames = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly int[] FailedThreeAndEleven = [3, 11];
    private static readonly int[] FailedOneAndTwo = [1, 2];
    private static readonly int[] FailedTwelve = [12];

    [Theory]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public void Given_InvalidOrIncompleteResult_When_Serialized_Then_AllSixOutcomeKeysAreAbsent(string statusText)
    {
        VerifierResultStatus status = Status(statusText);
        VerifierResult result = Result(status, counts: new OutcomeCounts(11, 12, 13, 14, 15, 16));

        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(result, OutputLimits));

        foreach (string name in OutcomeCountNames)
        {
            Assert.False(json.RootElement.TryGetProperty(name, out _));
        }
    }

    [Fact]
    public void Given_CompleteValidResult_When_Serialized_Then_AllSixOutcomeKeysArePresent()
    {
        VerifierResult result = Result(VerifierResultStatus.CompleteValid, counts: new OutcomeCounts(11, 12, 13, 14, 15, 16));

        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(result, OutputLimits));
        JsonElement root = json.RootElement;

        Assert.Equal(11, root.GetProperty("ok").GetInt64());
        Assert.Equal(12, root.GetProperty("loss").GetInt64());
        Assert.Equal(13, root.GetProperty("phantom").GetInt64());
        Assert.Equal(14, root.GetProperty("reported_failure").GetInt64());
        Assert.Equal(15, root.GetProperty("duplicate").GetInt64());
        Assert.Equal(16, root.GetProperty("reorder").GetInt64());
    }

    [Fact]
    public void Given_CompleteValidResultWithObservedZeros_When_Serialized_Then_AllSixZeroCountsRemainPresent()
    {
        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(
            Result(VerifierResultStatus.CompleteValid, counts: new OutcomeCounts(0, 0, 0, 0, 0, 0)), OutputLimits));

        foreach (string name in OutcomeCountNames)
        {
            Assert.True(json.RootElement.TryGetProperty(name, out JsonElement count));
            Assert.Equal(0, count.GetInt64());
        }
    }

    [Fact]
    public void Given_CompleteValidResultWithoutCounts_When_Serialized_Then_ThrowsCodecError()
    {
        VerifierResult result = Result(VerifierResultStatus.CompleteValid);

        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result, OutputLimits));
    }

    [Fact]
    public void Given_FailedAndUnevaluatedAssessments_When_RoundTripped_Then_PreservesTheirDistinctStates()
    {
        var notEvaluated = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null);
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            failedChecks: [3, 11],
            fidelity: new ConfigurationFidelityResult(notEvaluated, []));

        VerifierResult roundTripped = RoundTrip(result);

        Assert.Equal(FailedThreeAndEleven, roundTripped.FailedChecks);
        Assert.Equal(VerifierResultStatus.Invalid, roundTripped.Status);
        Assert.Equal(AssessmentEvaluation.NotEvaluated, roundTripped.ConfigurationFidelity!.Assessment.Evaluation);
        Assert.Null(roundTripped.ConfigurationFidelity.Assessment.Disposition);

        var failedAssessment = new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", 2, "setting", "mismatch", "false");
        VerifierResult failedRoundTrip = RoundTrip(Result(
            VerifierResultStatus.Invalid,
            failedChecks: [3, 11],
            fidelity: new ConfigurationFidelityResult(failedAssessment, [])));
        Assert.Equal(AssessmentEvaluation.Evaluated, failedRoundTrip.ConfigurationFidelity!.Assessment.Evaluation);
        Assert.Equal<AssessmentDisposition?>(AssessmentDisposition.Fail, failedRoundTrip.ConfigurationFidelity.Assessment.Disposition);
        Assert.Equal<AssessmentClassification?>(AssessmentClassification.Invalid, failedRoundTrip.ConfigurationFidelity.Assessment.Classification);
    }

    [Theory]
    [InlineData("complete_valid")]
    [InlineData("invalid")]
    public void Given_CompletedDrain_When_RoundTripped_Then_EmitsOnlyDrainTimeInMilliseconds(string statusText)
    {
        VerifierResultStatus status = Status(statusText);
        VerifierResult result = Result(status, counts: status == VerifierResultStatus.CompleteValid ? new OutcomeCounts(0, 0, 0, 0, 0, 0) : null,
            drainTimeMilliseconds: 1250.9m);

        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        VerifierResult roundTripped = Read(bytes);

        Assert.Equal(1250, json.RootElement.GetProperty("drain_time").GetInt64());
        Assert.False(json.RootElement.TryGetProperty("drain_elapsed_at_stop", out _));
        Assert.Equal(1250m, roundTripped.DrainTimeMilliseconds);
    }

    [Fact]
    public void Given_SafetyStopDuration_When_RoundTripped_Then_EmitsOnlyLabelledLowerBound()
    {
        byte[] bytes = VerifierResultCodec.Write(Result(VerifierResultStatus.Incomplete, drainElapsedAtStopMilliseconds: 600000.9m), OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement root = json.RootElement;

        Assert.Equal(600000, root.GetProperty("drain_elapsed_at_stop").GetInt64());
        Assert.True(root.GetProperty("drain_elapsed_at_stop_lower_bound").GetBoolean());
        Assert.False(root.TryGetProperty("drain_time", out _));
        VerifierResult roundTripped = Read(bytes);
        Assert.Equal(600000m, roundTripped.DrainElapsedAtStopMilliseconds);
        Assert.Null(roundTripped.DrainTimeMilliseconds);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public void Given_PreBoundaryOrOutOfRangeOrIoTermination_When_Serialized_Then_BothDrainFieldsAreAbsent(string statusText)
    {
        VerifierResultStatus status = Status(statusText);
        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(Result(status), OutputLimits));

        Assert.False(json.RootElement.TryGetProperty("drain_time", out _));
        Assert.False(json.RootElement.TryGetProperty("drain_elapsed_at_stop", out _));
        Assert.False(json.RootElement.TryGetProperty("drain_elapsed_at_stop_lower_bound", out _));
    }

    [Fact]
    public void Given_HashAndBoundedDiagnostics_When_Serialized_Then_EmitsOnlyResultProjection()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            failedChecks: [1, 2],
            hashes: [new JudgedFileHash("ledger.jsonl", "bf5fdb1cec62fe6b5976b64a6dbad1eb968201355babb03e8aa76dd7e1fb52b1")],
            diagnostics: [new ResultDiagnostic(1, "bad_row", "ledger.jsonl", 3, "delivered", "false")]);
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        string text = Encoding.UTF8.GetString(bytes);

        Assert.Equal("bf5fdb1cec62fe6b5976b64a6dbad1eb968201355babb03e8aa76dd7e1fb52b1",
            json.RootElement.GetProperty("judged_file_hashes")[0].GetProperty("sha256").GetString());
        Assert.Equal("loadgen-4.2.0", json.RootElement.GetProperty("generator_version").GetString());
        Assert.Equal("10.0.0", json.RootElement.GetProperty("runtime_versions").GetProperty("runtime").GetString());
        Assert.Equal("verifier-commit", json.RootElement.GetProperty("verifier_commit").GetString());
        Assert.Equal("2.16.0", json.RootElement.GetProperty("verifier_confluent_kafka_version").GetString());
        Assert.Equal(FailedOneAndTwo, Read(bytes).FailedChecks);
        Assert.DoesNotContain("raw_json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("typed_snapshot", text, StringComparison.Ordinal);
        Assert.DoesNotContain("request_lifecycle_requirements", text, StringComparison.Ordinal);
        Assert.DoesNotContain("index_hash", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NullableDiagnosticFields_When_RoundTripped_Then_EmitsAndReadsExplicitNulls()
    {
        const string json = "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"},\"generator_version\":\"loadgen-4.2.0\",\"runtime_versions\":{\"runtime\":\"10.0.0\"},\"verifier_commit\":\"verifier-commit\",\"verifier_confluent_kafka_version\":\"2.16.0\",\"verifier_librdkafka_version\":\"2.16.0\",\"status\":\"invalid\",\"failed_checks\":[],\"judged_file_hashes\":[],\"diagnostics\":[{\"check\":1,\"code\":\"diagnostic\",\"artifact\":null,\"row\":null,\"field\":null,\"snippet\":null}]}";
        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(
            Encoding.UTF8.GetBytes(json), "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);
        Assert.True(parsed.IsSuccess);
        ResultDiagnostic diagnostic = parsed.Value!.Diagnostics[0];
        Assert.Null(diagnostic.Artifact);
        Assert.Null(diagnostic.Row);
        Assert.Null(diagnostic.Field);
        Assert.Null(diagnostic.Snippet);

        using JsonDocument written = JsonDocument.Parse(VerifierResultCodec.Write(parsed.Value, OutputLimits));
        JsonElement writtenDiagnostic = written.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(JsonValueKind.Null, writtenDiagnostic.GetProperty("artifact").ValueKind);
        Assert.Equal(JsonValueKind.Null, writtenDiagnostic.GetProperty("row").ValueKind);
        Assert.Equal(JsonValueKind.Null, writtenDiagnostic.GetProperty("field").ValueKind);
        Assert.Equal(JsonValueKind.Null, writtenDiagnostic.GetProperty("snippet").ValueKind);
    }

    [Fact]
    public void Given_WrongKindNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem()
    {
        const string json = "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"},\"generator_version\":\"loadgen-4.2.0\",\"runtime_versions\":{\"runtime\":\"10.0.0\"},\"verifier_commit\":\"verifier-commit\",\"verifier_confluent_kafka_version\":\"2.16.0\",\"verifier_librdkafka_version\":\"2.16.0\",\"status\":\"invalid\",\"failed_checks\":[],\"judged_file_hashes\":[],\"diagnostics\":[{\"check\":1,\"code\":\"diagnostic\",\"artifact\":17,\"row\":null,\"field\":null,\"snippet\":null}]}";

        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(
            Encoding.UTF8.GetBytes(json), "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);

        Assert.False(parsed.IsSuccess);
        Assert.Equal("diagnostics[0].artifact", parsed.Problems[0].Field);
    }

    [Fact]
    public void Given_MissingNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem()
    {
        const string json = "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"},\"generator_version\":\"loadgen-4.2.0\",\"runtime_versions\":{\"runtime\":\"10.0.0\"},\"verifier_commit\":\"verifier-commit\",\"verifier_confluent_kafka_version\":\"2.16.0\",\"verifier_librdkafka_version\":\"2.16.0\",\"status\":\"invalid\",\"failed_checks\":[],\"judged_file_hashes\":[],\"diagnostics\":[{\"check\":1,\"code\":\"diagnostic\",\"artifact\":null,\"row\":null,\"field\":null}]}";

        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(
            Encoding.UTF8.GetBytes(json), "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);

        Assert.False(parsed.IsSuccess);
        Assert.Equal("diagnostics[0].snippet", parsed.Problems[0].Field);
    }

    [Fact]
    public void Given_StartLineNote_When_RoundTripped_Then_PreservesOffsetsWithoutSelfHash()
    {
        var note = new StartLineNote(
            1,
            Identity,
            "document-events",
            [new PartitionHigh(0, 7), new PartitionHigh(1, 0)],
            "2026-10-17T12:34:56.789+00:00",
            "verifier-sha",
            "2.16.0",
            "2.16.0");
        byte[] bytes = StartLineNoteCodec.Write(note, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        ParseResult<StartLineNote> parsed = StartLineNoteCodec.Read(bytes, "start-line-note", RecordedEvidence.SmallLimits, maxPartitions: 16);

        Assert.True(parsed.IsSuccess);
        Assert.Equal("document-events", json.RootElement.GetProperty("topic").GetString());
        Assert.Equal("2026-10-17T12:34:56.789+00:00", parsed.Value!.CapturedAt);
        Assert.Equal(7, parsed.Value.PartitionHighs[0].High);
        Assert.Equal(0, parsed.Value.PartitionHighs[1].High);
        Assert.False(json.RootElement.TryGetProperty("sha256", out _));
    }

    [Fact]
    public void Given_MalformedStartLineNote_When_Read_Then_ReturnsStructuralProblemForFutureCheckSeven()
    {
        byte[] malformed = Encoding.UTF8.GetBytes(
            "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"r\",\"instance_id\":\"i\",\"expected_instance_id\":\"i\",\"client_id\":\"c\"},\"topic\":\"document-events\",\"partition_highs\":[{\"partition\":0}],\"captured_at\":\"t\",\"verifier_commit\":\"v\",\"confluent_kafka_version\":\"2.16.0\",\"librdkafka_version\":\"2.16.0\"}");

        ParseResult<StartLineNote> parsed = StartLineNoteCodec.Read(malformed, "start-line-note", RecordedEvidence.SmallLimits, maxPartitions: 16);

        Assert.False(parsed.IsSuccess);
        Assert.Equal(7, parsed.Problems[0].CheckNumber);
        Assert.Equal("partition_highs[0].high", parsed.Problems[0].Field);
    }

    [Fact]
    public void Given_InvalidButUsableProbeProjection_When_RoundTripped_Then_PreservesIndependentEvidence()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            failedChecks: [12],
            probe: new ProbeRepeatObservation(true, false, "broker-1", 10, 1, 0),
            counts: new OutcomeCounts(11, 12, 13, 14, 15, 16));

        VerifierResult roundTripped = RoundTrip(result);

        Assert.Equal(VerifierResultStatus.Invalid, roundTripped.Status);
        Assert.Equal(FailedTwelve, roundTripped.FailedChecks);
        Assert.True(roundTripped.ProbeObservation!.Usable);
        Assert.False(roundTripped.ProbeObservation.Qualifying);
        Assert.Equal(0, roundTripped.ProbeObservation.WindowMaximum);
        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(result, OutputLimits));
        Assert.False(json.RootElement.TryGetProperty("ok", out _));
    }

    [Fact]
    public void Given_BrokerCountProjection_When_RoundTripped_Then_PreservesOnlySuppliedMeasurements()
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid) with
        {
            BrokerCount = new BrokerConnectionCount("producer", "producer-1", 9, 8, true)
        };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement broker = json.RootElement.GetProperty("broker_count");

        Assert.Equal("producer", broker.GetProperty("role").GetString());
        Assert.Equal("producer-1", broker.GetProperty("client_id").GetString());
        Assert.Equal(9, broker.GetProperty("maximum").GetInt64());
        Assert.Equal(8, broker.GetProperty("first_post_drain").GetInt64());
        Assert.True(broker.GetProperty("lower_bound").GetBoolean());
        Assert.Equal(9, Read(bytes).BrokerCount!.Maximum);

        byte[] absentMeasurement = VerifierResultCodec.Write(result with
        {
            BrokerCount = new BrokerConnectionCount("producer", "producer-1", null, null, true)
        }, OutputLimits);
        using JsonDocument absentJson = JsonDocument.Parse(absentMeasurement);
        Assert.False(absentJson.RootElement.GetProperty("broker_count").TryGetProperty("maximum", out _));
        Assert.False(absentJson.RootElement.GetProperty("broker_count").TryGetProperty("first_post_drain", out _));

        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result with
        {
            BrokerCount = new BrokerConnectionCount("producer", "producer-1", 9, 8, false)
        }, OutputLimits));
    }

    [Fact]
    public void Given_BrokerCountLowerBoundFalse_When_Read_Then_ReturnsStructuralProblem()
    {
        const string json = "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"},\"generator_version\":\"loadgen-4.2.0\",\"runtime_versions\":{\"runtime\":\"10.0.0\"},\"verifier_commit\":\"verifier-commit\",\"verifier_confluent_kafka_version\":\"2.16.0\",\"verifier_librdkafka_version\":\"2.16.0\",\"status\":\"invalid\",\"failed_checks\":[],\"judged_file_hashes\":[],\"diagnostics\":[],\"broker_count\":{\"role\":\"producer\",\"client_id\":\"producer-1\",\"lower_bound\":false}}";

        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(
            Encoding.UTF8.GetBytes(json), "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);

        Assert.False(parsed.IsSuccess);
        Assert.Equal("broker_count.lower_bound", parsed.Problems[0].Field);
    }

    [Fact]
    public void Given_ConfigurationFidelityDifferences_When_RoundTripped_Then_EmitsBoundedSnakeCaseProjection()
    {
        var assessment = new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", 2, "settings.enable.idempotence", "setting_mismatch", "false");
        VerifierResult result = Result(VerifierResultStatus.Invalid, fidelity: new ConfigurationFidelityResult(
            assessment,
            [new SettingDifference("enable.idempotence", "true", "false"), new SettingDifference("acks", null, "all")]));
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement fidelity = json.RootElement.GetProperty("configuration_fidelity");
        JsonElement actualAssessment = fidelity.GetProperty("assessment");

        Assert.Equal("evaluated", actualAssessment.GetProperty("evaluation").GetString());
        Assert.Equal("fail", actualAssessment.GetProperty("disposition").GetString());
        Assert.Equal("invalid", actualAssessment.GetProperty("classification").GetString());
        Assert.Equal("enable.idempotence", fidelity.GetProperty("differences")[0].GetProperty("setting").GetString());
        Assert.Equal("false", fidelity.GetProperty("differences")[0].GetProperty("actual").GetString());
        Assert.False(fidelity.GetProperty("differences")[1].TryGetProperty("expected", out _));
        Assert.Equal(AssessmentDisposition.Fail, Read(bytes).ConfigurationFidelity!.Assessment.Disposition);
    }

    [Fact]
    public void Given_A01RecordedEvidence_When_RoundTripped_Then_PreservesSelectedInt64FieldsAndOmissions()
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid) with
        {
            A01RecordedEvidence = new A01RecordedEvidence(
                [-184],
                [new A01WindowEvidence("broker-1", 10, 9, 2, 1, 17, 99327),
                    new A01WindowEvidence("broker-2", 11, 10, null, null, null, null)])
        };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement evidence = json.RootElement.GetProperty("a01_recorded_evidence");
        JsonElement window = evidence.GetProperty("windows")[0];
        JsonElement absentWindow = evidence.GetProperty("windows")[1];

        Assert.Equal(-184, evidence.GetProperty("delivery_error_codes")[0].GetInt32());
        Assert.Equal(17, window.GetProperty("throttle_maximum_ms").GetInt64());
        Assert.Equal(99327, window.GetProperty("round_trip_p99_us").GetInt64());
        Assert.False(absentWindow.TryGetProperty("throttle_maximum_ms", out _));
        Assert.False(absentWindow.TryGetProperty("round_trip_p99_us", out _));
        Assert.Equal(99327, Read(bytes).A01RecordedEvidence!.Windows[0].RoundTripP99Microseconds);
    }

    [Fact]
    public void Given_OutputExceedsConfiguredBytes_When_Serialized_Then_ThrowsExecutionLimit()
    {
        var smallLimit = new OutputLimits(32, 8, 256);

        Assert.Throws<InputLimitExceededException>(() => StartLineNoteCodec.Write(
            new StartLineNote(1, Identity, "document-events", [], "captured", "commit", "2.16.0", "2.16.0"), smallLimit));
        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Write(
            Result(VerifierResultStatus.Invalid), smallLimit));
    }

    [Fact]
    public void Given_UnicodeDiagnosticExceedsUtf8Limit_When_Serialized_Then_ThrowsExecutionLimit()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            diagnostics: [new ResultDiagnostic(1, "bad_row", "ledger", null, null, "ééé")]);
        var limits = new OutputLimits(65_536, 8, 5);

        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Write(result, limits));
    }

    [Fact]
    public void Given_NegativeFractionalDuration_When_Serialized_Then_TruncatesTowardZero()
    {
        byte[] bytes = VerifierResultCodec.Write(Result(VerifierResultStatus.Invalid, drainTimeMilliseconds: -1250.9m), OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        VerifierResult roundTripped = Read(bytes);

        Assert.Equal(-1250, json.RootElement.GetProperty("drain_time").GetInt64());
        Assert.Equal(-1250m, roundTripped.DrainTimeMilliseconds);
    }

    [Fact]
    public void Given_ResultExceedsAggregateEntryLimit_When_Read_Then_ThrowsExecutionLimit()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            diagnostics:
            [
                new ResultDiagnostic(1, "bad_row", "ledger", null, null, null),
                new ResultDiagnostic(2, "bad_manifest", "manifest", null, null, null)
            ]);
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        InputLimits limits = RecordedEvidence.SmallLimits with { MaxEntries = 1 };

        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Read(bytes, "result.json", limits, maxDiagnosticsPerCheck: 8));
    }

    [Fact]
    public void Given_DiagnosticsForDifferentChecks_When_Read_Then_JsonlRowLimitDoesNotApply()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            diagnostics:
            [
                new ResultDiagnostic(1, "bad_row", "ledger", null, null, null),
                new ResultDiagnostic(2, "bad_manifest", "manifest", null, null, null)
            ]);
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        InputLimits limits = RecordedEvidence.SmallLimits with { MaxRowsPerArtifact = 1 };

        VerifierResult parsed = Read(bytes, limits, maxDiagnosticsPerCheck: 1);

        Assert.Equal(2, parsed.Diagnostics.Count);
    }

    [Fact]
    public void Given_UnicodeDiagnosticExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit()
    {
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            diagnostics: [new ResultDiagnostic(1, "bad_row", "ledger", null, null, "ééé")]);
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        InputLimits limits = RecordedEvidence.SmallLimits with { MaxDiagnosticBytes = 5 };

        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Read(bytes, "result.json", limits, maxDiagnosticsPerCheck: 8));
    }

    [Fact]
    public void Given_AssessmentSnippetExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit()
    {
        var assessment = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, "ééé");
        VerifierResult result = Result(VerifierResultStatus.Invalid, fidelity: new ConfigurationFidelityResult(assessment, []));
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        InputLimits limits = RecordedEvidence.SmallLimits with { MaxDiagnosticBytes = 5 };

        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Read(bytes, "result.json", limits, maxDiagnosticsPerCheck: 8));
    }

    [Fact]
    public void Given_DiagnosticAndAssessmentExceedPerCheckLimit_When_Read_Then_ThrowsExecutionLimit()
    {
        var assessment = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null);
        VerifierResult result = Result(
            VerifierResultStatus.Invalid,
            fidelity: new ConfigurationFidelityResult(assessment, []),
            diagnostics: [new ResultDiagnostic(27, "fidelity_observation", "capabilities", null, null, null)]);
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);

        Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Read(
            bytes, "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 1));
    }

    [Fact]
    public void Given_PartitionCountExceedsConfiguredMaximum_When_ReadStartLine_Then_ThrowsExecutionLimit()
    {
        var note = new StartLineNote(1, Identity, "document-events", [new PartitionHigh(0, 7), new PartitionHigh(1, 0)],
            "captured", "commit", "2.16.0", "2.16.0");
        byte[] bytes = StartLineNoteCodec.Write(note, OutputLimits);

        Assert.Throws<InputLimitExceededException>(() => StartLineNoteCodec.Read(
            bytes, "start-line-note", RecordedEvidence.SmallLimits, maxPartitions: 1));
    }

    private static VerifierResult Result(
        VerifierResultStatus status,
        OutcomeCounts? counts = null,
        IReadOnlyList<int>? failedChecks = null,
        IReadOnlyList<JudgedFileHash>? hashes = null,
        IReadOnlyList<ResultDiagnostic>? diagnostics = null,
        decimal? drainTimeMilliseconds = null,
        decimal? drainElapsedAtStopMilliseconds = null,
        ConfigurationFidelityResult? fidelity = null,
        ProbeRepeatObservation? probe = null) => new(
            1,
            Identity,
            "loadgen-4.2.0",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["runtime"] = "10.0.0" },
            "verifier-commit",
            "2.16.0",
            "2.16.0",
            status,
            failedChecks ?? [],
            null,
            null,
            drainTimeMilliseconds,
            drainElapsedAtStopMilliseconds,
            counts,
            null,
            fidelity,
            probe,
            null,
            hashes ?? [],
            diagnostics ?? []);

    private static VerifierResult Read(byte[] bytes, InputLimits? limits = null, int maxDiagnosticsPerCheck = 8)
    {
        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(
            bytes, "result.json", limits ?? RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck);
        Assert.True(parsed.IsSuccess);
        return parsed.Value!;
    }

    private static VerifierResult RoundTrip(VerifierResult result) => Read(VerifierResultCodec.Write(result, OutputLimits));

    private static VerifierResultStatus Status(string value) => value switch
    {
        "complete_valid" => VerifierResultStatus.CompleteValid,
        "invalid" => VerifierResultStatus.Invalid,
        "incomplete" => VerifierResultStatus.Incomplete,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static RunIdentity Identity { get; } = new("A01", "C0", "run-1", "instance-1", "instance-1", "producer-1");
}
