using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.Tests.A01;
using Xunit;

namespace Parchwell.Verifier.Tests.Results;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class VerifierResultCodecTests
{
    private static readonly OutputLimits OutputLimits = new(65_536, 8, 256);
    private static readonly string[] OutcomeCountNames = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly int[] FailedThreeAndEleven = [3, 11];

    [Fact]
    public void GivenWorkloadCountsMissingOrObservedZero_WhenRoundTripped_ThenPreservesAbsenceAndZero()
    {
        var absent=new WorkloadObservation(true,null,null,null,null,null,
            [new("ramp100",0,0,null,null,null)]);
        var zero=new WorkloadObservation(true,0,0,0,false,1,
            [new("ramp100",0,0,0,0,0)]);
        VerifierResult absentResult=RoundTrip(Result(VerifierResultStatus.Invalid) with { WorkloadObservation=absent });
        VerifierResult zeroResult=RoundTrip(Result(VerifierResultStatus.Invalid) with { WorkloadObservation=zero });
        Assert.Null(absentResult.WorkloadObservation!.PlannedAttempts);
        Assert.Null(absentResult.WorkloadObservation.Stages![0].InvokedAttempts);
        Assert.Equal(0,zeroResult.WorkloadObservation!.PlannedAttempts);
        Assert.Equal(0,zeroResult.WorkloadObservation.Stages![0].InvokedAttempts);
    }

    [Theory]
    [InlineData("\"planned_attempts\":-1")]
    [InlineData("\"invoked_attempts\":-1")]
    [InlineData("\"invoked_attempts\":9223372036854775808")]
    [InlineData("\"terminal_observations\":-1")]
    [InlineData("\"first_unexecuted_ordinal\":-1")]
    public void GivenInvalidWorkloadCounts_WhenRead_ThenRejects(string field)
    {
        ParseResult<VerifierResult> parsed=ReadProjection("\"workload_observation\":{\"synthetic\":true,"+field+"}");
        Assert.False(parsed.IsSuccess);
    }

    [Theory]
    [InlineData("ramp100",true)]
    [InlineData("",true)]
    [InlineData("ramp100,ramp300,ramp900",true)]
    [InlineData("ramp100,ramp300,ramp900,ramp1800",true)]
    [InlineData("ramp100,ramp300,ramp900,ramp1800,ramp100",false)]
    [InlineData("ramp100,ramp100",false)]
    [InlineData("unknown",false)]
    public void GivenWorkloadStageIds_WhenRead_ThenEnforcesFourUniqueKnownStages(string ids,bool accepted)
    {
        string stages=string.Join(',',ids.Length==0?[]:ids.Split(',').Select(id=>"{\"id\":\""+id+"\",\"offered_attempts_per_second\":0,\"duration_seconds\":0}"));
        ParseResult<VerifierResult> parsed=ReadProjection("\"workload_observation\":{\"synthetic\":true,\"stages\":["+stages+"]}");
        Assert.Equal(accepted,parsed.IsSuccess);
    }

    [Theory]
    [InlineData("\"unexpected\":true")]
    [InlineData("\"planned_attempts\":\"zero\"")]
    public void GivenMalformedOrUnknownWorkloadField_WhenRead_ThenRejects(string field)
    {
        ParseResult<VerifierResult> parsed=ReadProjection("\"workload_observation\":{\"synthetic\":true,"+field+"}");
        Assert.False(parsed.IsSuccess);
    }

    [Theory]
    [InlineData("offered_attempts_per_second",-1)]
    [InlineData("duration_seconds",-1)]
    [InlineData("invoked_attempts",-1)]
    [InlineData("delivered_observations",-1)]
    [InlineData("failed_observations",-1)]
    public void GivenNegativeWorkloadStageCount_WhenRead_ThenRejects(string field,long value)
    {
        string stage="\"id\":\"ramp100\",\"offered_attempts_per_second\":0,\"duration_seconds\":0";
        if(field is "offered_attempts_per_second" or "duration_seconds")
            stage=stage.Replace(field+"\":0",field+"\":"+value.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal);
        else stage+=",\""+field+"\":"+value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ParseResult<VerifierResult> parsed=ReadProjection("\"workload_observation\":{\"synthetic\":true,\"stages\":[{"+stage+"}]}");
        Assert.False(parsed.IsSuccess);
    }
    private static readonly int[] FailedOneAndTwo = [1, 2];
    private static readonly int[] FailedTwelve = [12];
    private static readonly int[] FailedTwo = [2];

    [Fact]
    public void Given_MissingManifestProvenance_When_RoundTripped_Then_SchemaOneOmitsBothFields()
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid, failedChecks: [2]) with
        {
            GeneratorVersion = null,
            RuntimeVersions = null
        };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("generator_version", out _));
        Assert.False(json.RootElement.TryGetProperty("runtime_versions", out _));
        VerifierResult parsed = Read(bytes);
        Assert.Null(parsed.GeneratorVersion);
        Assert.Null(parsed.RuntimeVersions);
        Assert.Equal(FailedTwo, parsed.FailedChecks);
    }

    [Fact]
    public void Given_ReadableEmptyProducerProvenance_When_RoundTripped_Then_PreservesPresentEmptyValues()
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid) with
        {
            GeneratorVersion = "",
            RuntimeVersions = new Dictionary<string, string>(StringComparer.Ordinal)
        };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        Assert.Equal("", json.RootElement.GetProperty("generator_version").GetString());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("runtime_versions").ValueKind);
        VerifierResult parsed = Read(bytes);
        Assert.Equal("", parsed.GeneratorVersion);
        Assert.Empty(parsed.RuntimeVersions!);
    }

    [Theory]
    [InlineData("generator_version", "null")]
    [InlineData("generator_version", "123")]
    [InlineData("runtime_versions", "null")]
    [InlineData("runtime_versions", "[]")]
    [InlineData("runtime_versions", "{\"runtime\":null}")]
    public void Given_PresentMalformedProducerProvenance_When_Read_Then_RejectsWithoutNumberedCheck(string property, string literal)
    {
        byte[] original = VerifierResultCodec.Write(Result(VerifierResultStatus.Invalid), OutputLimits);
        var node = System.Text.Json.Nodes.JsonNode.Parse(original)!;
        node[property] = System.Text.Json.Nodes.JsonNode.Parse(literal);
        var parsed = VerifierResultCodec.Read(Encoding.UTF8.GetBytes(node.ToJsonString()), "result.json",
            RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);
        Assert.False(parsed.IsSuccess);
        Assert.All(parsed.Problems, problem => Assert.Null(problem.CheckNumber));
    }

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

        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement root = json.RootElement;

        Assert.Equal(11, root.GetProperty("ok").GetInt64());
        Assert.Equal(12, root.GetProperty("loss").GetInt64());
        Assert.Equal(13, root.GetProperty("phantom").GetInt64());
        Assert.Equal(14, root.GetProperty("reported_failure").GetInt64());
        Assert.Equal(15, root.GetProperty("duplicate").GetInt64());
        Assert.Equal(16, root.GetProperty("reorder").GetInt64());

        OutcomeCounts roundTripped = Read(bytes).Counts!;
        Assert.Equal(11, roundTripped.Ok);
        Assert.Equal(12, roundTripped.Loss);
        Assert.Equal(13, roundTripped.Phantom);
        Assert.Equal(14, roundTripped.ReportedFailure);
        Assert.Equal(15, roundTripped.Duplicate);
        Assert.Equal(16, roundTripped.Reorder);
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
        Assert.False(json.RootElement.TryGetProperty("drain_elapsed_at_stop_lower_bound", out _));
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
        JsonElement diagnostic = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(1, diagnostic.GetProperty("check").GetInt32());
        Assert.Equal("bad_row", diagnostic.GetProperty("code").GetString());
        Assert.Equal("ledger.jsonl", diagnostic.GetProperty("artifact").GetString());
        Assert.Equal(3, diagnostic.GetProperty("row").GetInt64());
        Assert.Equal("delivered", diagnostic.GetProperty("field").GetString());
        Assert.Equal("false", diagnostic.GetProperty("snippet").GetString());
        Assert.Equal(FailedOneAndTwo, Read(bytes).FailedChecks);
        Assert.DoesNotContain("raw_json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("typed_snapshot", text, StringComparison.Ordinal);
        Assert.DoesNotContain("request_lifecycle_requirements", text, StringComparison.Ordinal);
        Assert.DoesNotContain("index_hash", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ProducerAndVerifierVersionsDiffer_When_RoundTripped_Then_CopiesEachSourceSeparately()
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid) with
        {
            GeneratorVersion = "producer-generator-4.2.0",
            RuntimeVersions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dotnet"] = "producer-dotnet-10.0.0",
                ["confluent_kafka"] = "producer-managed-3.8.0",
                ["librdkafka"] = "producer-native-2.4.0",
                ["workload_generator"] = "producer-workload-4.2.0"
            },
            VerifierCommit = "verifier-commit-abc",
            VerifierConfluentKafkaVersion = "verifier-managed-2.16.0",
            VerifierLibrdkafkaVersion = "verifier-native-2.16.0"
        };

        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement root = json.RootElement;

        Assert.Equal("producer-generator-4.2.0", root.GetProperty("generator_version").GetString());
        Assert.Equal("producer-dotnet-10.0.0", root.GetProperty("runtime_versions").GetProperty("dotnet").GetString());
        Assert.Equal("producer-managed-3.8.0", root.GetProperty("runtime_versions").GetProperty("confluent_kafka").GetString());
        Assert.Equal("producer-native-2.4.0", root.GetProperty("runtime_versions").GetProperty("librdkafka").GetString());
        Assert.Equal("producer-workload-4.2.0", root.GetProperty("runtime_versions").GetProperty("workload_generator").GetString());
        Assert.Equal("verifier-commit-abc", root.GetProperty("verifier_commit").GetString());
        Assert.Equal("verifier-managed-2.16.0", root.GetProperty("verifier_confluent_kafka_version").GetString());
        Assert.Equal("verifier-native-2.16.0", root.GetProperty("verifier_librdkafka_version").GetString());

        VerifierResult roundTripped = Read(bytes);
        Assert.Equal("producer-generator-4.2.0", roundTripped.GeneratorVersion);
        Assert.Equal(result.RuntimeVersions, roundTripped.RuntimeVersions);
        Assert.Equal("verifier-commit-abc", roundTripped.VerifierCommit);
        Assert.Equal("verifier-managed-2.16.0", roundTripped.VerifierConfluentKafkaVersion);
        Assert.Equal("verifier-native-2.16.0", roundTripped.VerifierLibrdkafkaVersion);
    }

    [Fact]
    public void Given_AbsentAndObservedEmptyBoundaries_When_RoundTripped_Then_PreservesTheDistinction()
    {
        byte[] absentBytes = VerifierResultCodec.Write(Result(VerifierResultStatus.Invalid), OutputLimits);
        using JsonDocument absentJson = JsonDocument.Parse(absentBytes);
        VerifierResult absent = Read(absentBytes);

        Assert.False(absentJson.RootElement.TryGetProperty("start_line", out _));
        Assert.False(absentJson.RootElement.TryGetProperty("end_line", out _));
        Assert.Null(absent.StartLine);
        Assert.Null(absent.EndLine);

        VerifierResult observedEmptyResult = Result(VerifierResultStatus.Invalid) with
        {
            StartLine = [],
            EndLine = []
        };
        byte[] emptyBytes = VerifierResultCodec.Write(observedEmptyResult, OutputLimits);
        using JsonDocument emptyJson = JsonDocument.Parse(emptyBytes);
        VerifierResult observedEmpty = Read(emptyBytes);

        Assert.Equal(JsonValueKind.Array, emptyJson.RootElement.GetProperty("start_line").ValueKind);
        Assert.Equal(0, emptyJson.RootElement.GetProperty("start_line").GetArrayLength());
        Assert.Equal(JsonValueKind.Array, emptyJson.RootElement.GetProperty("end_line").ValueKind);
        Assert.Equal(0, emptyJson.RootElement.GetProperty("end_line").GetArrayLength());
        Assert.NotNull(observedEmpty.StartLine);
        Assert.Empty(observedEmpty.StartLine!);
        Assert.NotNull(observedEmpty.EndLine);
        Assert.Empty(observedEmpty.EndLine!);

        VerifierResult observedResult = Result(VerifierResultStatus.Invalid) with
        {
            StartLine = [new WatermarkOffset(0, 7), new WatermarkOffset(1, 0)],
            EndLine = [new WatermarkOffset(0, 9)]
        };
        byte[] observedBytes = VerifierResultCodec.Write(observedResult, OutputLimits);
        using JsonDocument observedJson = JsonDocument.Parse(observedBytes);
        JsonElement start = observedJson.RootElement.GetProperty("start_line");
        JsonElement end = observedJson.RootElement.GetProperty("end_line");
        Assert.Equal(2, start.GetArrayLength());
        Assert.Equal(0, start[0].GetProperty("partition").GetInt32());
        Assert.Equal(7, start[0].GetProperty("offset").GetInt64());
        Assert.Equal(1, start[1].GetProperty("partition").GetInt32());
        Assert.Equal(0, start[1].GetProperty("offset").GetInt64());
        Assert.Equal(1, end.GetArrayLength());
        Assert.Equal(0, end[0].GetProperty("partition").GetInt32());
        Assert.Equal(9, end[0].GetProperty("offset").GetInt64());
        VerifierResult observed = Read(observedBytes);
        Assert.Equal(2, observed.StartLine!.Count);
        Assert.Equal(new WatermarkOffset(0, 7), observed.StartLine[0]);
        Assert.Equal(new WatermarkOffset(1, 0), observed.StartLine[1]);
        Assert.Equal(new WatermarkOffset(0, 9), Assert.Single(observed.EndLine!));
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
        using JsonDocument json = JsonDocument.Parse(VerifierResultCodec.Write(result, OutputLimits));
        JsonElement probe = json.RootElement.GetProperty("probe_observation");
        Assert.True(probe.GetProperty("usable").GetBoolean());
        Assert.False(probe.GetProperty("qualifying").GetBoolean());
        Assert.Equal("broker-1", probe.GetProperty("broker").GetString());
        Assert.Equal(10, probe.GetProperty("report_index").GetInt64());
        Assert.Equal(1, probe.GetProperty("window_count").GetInt64());
        Assert.Equal(0, probe.GetProperty("window_maximum").GetInt64());
        Assert.True(roundTripped.ProbeObservation!.Usable);
        Assert.False(roundTripped.ProbeObservation.Qualifying);
        Assert.Equal("broker-1", roundTripped.ProbeObservation.Broker);
        Assert.Equal(10, roundTripped.ProbeObservation.ReportIndex);
        Assert.Equal(1, roundTripped.ProbeObservation.WindowCount);
        Assert.Equal(0, roundTripped.ProbeObservation.WindowMaximum);
        Assert.False(json.RootElement.TryGetProperty("ok", out _));

        VerifierResult withoutMeasurements = Result(VerifierResultStatus.Invalid) with
        {
            ProbeObservation = new ProbeRepeatObservation(false, false, null, null, null, null)
        };
        byte[] absentBytes = VerifierResultCodec.Write(withoutMeasurements, OutputLimits);
        using JsonDocument absentJson = JsonDocument.Parse(absentBytes);
        JsonElement absentProbe = absentJson.RootElement.GetProperty("probe_observation");
        Assert.False(absentProbe.TryGetProperty("broker", out _));
        Assert.False(absentProbe.TryGetProperty("report_index", out _));
        Assert.False(absentProbe.TryGetProperty("window_count", out _));
        Assert.False(absentProbe.TryGetProperty("window_maximum", out _));
        ProbeRepeatObservation readAbsent = Read(absentBytes).ProbeObservation!;
        Assert.Null(readAbsent.Broker);
        Assert.Null(readAbsent.ReportIndex);
        Assert.Null(readAbsent.WindowCount);
        Assert.Null(readAbsent.WindowMaximum);
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
        BrokerConnectionCount readBroker = Read(bytes).BrokerCount!;
        Assert.Equal("producer", readBroker.Role);
        Assert.Equal("producer-1", readBroker.ClientId);
        Assert.Equal(9, readBroker.Maximum);
        Assert.Equal(8, readBroker.FirstPostDrain);
        Assert.True(readBroker.LowerBound);

        byte[] absentMeasurement = VerifierResultCodec.Write(result with
        {
            BrokerCount = new BrokerConnectionCount("producer", "producer-1", null, null, true)
        }, OutputLimits);
        using JsonDocument absentJson = JsonDocument.Parse(absentMeasurement);
        Assert.False(absentJson.RootElement.GetProperty("broker_count").TryGetProperty("maximum", out _));
        Assert.False(absentJson.RootElement.GetProperty("broker_count").TryGetProperty("first_post_drain", out _));
        BrokerConnectionCount readAbsentBroker = Read(absentMeasurement).BrokerCount!;
        Assert.Null(readAbsentBroker.Maximum);
        Assert.Null(readAbsentBroker.FirstPostDrain);

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
        Assert.Equal("true", fidelity.GetProperty("differences")[0].GetProperty("expected").GetString());
        Assert.Equal("false", fidelity.GetProperty("differences")[0].GetProperty("actual").GetString());
        Assert.False(fidelity.GetProperty("differences")[1].TryGetProperty("expected", out _));
        Assert.Equal(AssessmentDisposition.Fail, Read(bytes).ConfigurationFidelity!.Assessment.Disposition);
        ConfigurationFidelityResult readFidelity = Read(bytes).ConfigurationFidelity!;
        Assert.Equal(AssessmentEvaluation.Evaluated, readFidelity.Assessment.Evaluation);
        Assert.Equal(AssessmentDisposition.Fail, readFidelity.Assessment.Disposition);
        Assert.Equal(AssessmentClassification.Invalid, readFidelity.Assessment.Classification);
        Assert.Equal("capabilities.json", readFidelity.Assessment.Artifact);
        Assert.Equal(2, readFidelity.Assessment.Row);
        Assert.Equal("settings.enable.idempotence", readFidelity.Assessment.Field);
        Assert.Equal("setting_mismatch", readFidelity.Assessment.Code);
        Assert.Equal("false", readFidelity.Assessment.Snippet);
        Assert.Equal("enable.idempotence", readFidelity.Differences[0].Setting);
        Assert.Equal("true", readFidelity.Differences[0].Expected);
        Assert.Equal("false", readFidelity.Differences[0].Actual);
        Assert.Equal("acks", readFidelity.Differences[1].Setting);
        Assert.Null(readFidelity.Differences[1].Expected);
        Assert.Equal("all", readFidelity.Differences[1].Actual);
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
        Assert.Equal("broker-1", window.GetProperty("broker").GetString());
        Assert.Equal(10, window.GetProperty("report_index").GetInt64());
        Assert.Equal(9, window.GetProperty("native_timestamp").GetInt64());
        Assert.Equal(2, window.GetProperty("request_timeouts_increment").GetInt64());
        Assert.Equal(1, window.GetProperty("disconnects_increment").GetInt64());
        Assert.Equal(17, window.GetProperty("throttle_maximum_ms").GetInt64());
        Assert.Equal(99327, window.GetProperty("round_trip_p99_us").GetInt64());
        Assert.Equal("broker-2", absentWindow.GetProperty("broker").GetString());
        Assert.Equal(11, absentWindow.GetProperty("report_index").GetInt64());
        Assert.Equal(10, absentWindow.GetProperty("native_timestamp").GetInt64());
        Assert.False(absentWindow.TryGetProperty("request_timeouts_increment", out _));
        Assert.False(absentWindow.TryGetProperty("disconnects_increment", out _));
        Assert.False(absentWindow.TryGetProperty("throttle_maximum_ms", out _));
        Assert.False(absentWindow.TryGetProperty("round_trip_p99_us", out _));
        Assert.Equal(99327, Read(bytes).A01RecordedEvidence!.Windows![0].RoundTripP99Microseconds);
        A01RecordedEvidence readEvidence = Read(bytes).A01RecordedEvidence!;
        Assert.Equal(-184, readEvidence.DeliveryErrorCodes![0]);
        Assert.Equal("broker-1", readEvidence.Windows![0].Broker);
        Assert.Equal(10, readEvidence.Windows![0].ReportIndex);
        Assert.Equal(9, readEvidence.Windows![0].NativeTimestamp);
        Assert.Equal(2, readEvidence.Windows![0].RequestTimeoutsIncrement);
        Assert.Equal(1, readEvidence.Windows![0].DisconnectsIncrement);
        Assert.Equal(17, readEvidence.Windows![0].ThrottleMaximumMilliseconds);
        Assert.Equal(99327, readEvidence.Windows![0].RoundTripP99Microseconds);
        Assert.Null(readEvidence.Windows![1].RequestTimeoutsIncrement);
        Assert.Null(readEvidence.Windows![1].DisconnectsIncrement);
        Assert.Null(readEvidence.Windows![1].ThrottleMaximumMilliseconds);
        Assert.Null(readEvidence.Windows![1].RoundTripP99Microseconds);
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

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "invalid")]
    [InlineData(null, "incomplete")]
    [InlineData("pass", null)]
    [InlineData("pass", "invalid")]
    [InlineData("pass", "incomplete")]
    [InlineData("fail", null)]
    [InlineData("fail", "invalid")]
    [InlineData("fail", "incomplete")]
    public void Given_UnevaluatedAssessmentOutcomeMatrix_When_ReadOrWritten_Then_OnlyAbsentOutcomesAreAccepted(
        string? dispositionText, string? classificationText)
    {
        AssessmentDisposition? disposition = dispositionText is null ? null : dispositionText == "pass" ? AssessmentDisposition.Pass : AssessmentDisposition.Fail;
        AssessmentClassification? classification = classificationText is null ? null : classificationText == "invalid" ? AssessmentClassification.Invalid : AssessmentClassification.Incomplete;
        var assessment = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, disposition, classification,
            null, null, null, null, null);
        VerifierResult result = Result(VerifierResultStatus.Invalid, fidelity: new ConfigurationFidelityResult(assessment, []));
        string outcomes = (disposition is null ? "" : "\"disposition\":\"" + (disposition == AssessmentDisposition.Pass ? "pass" : "fail") + "\",") +
            (classification is null ? "" : "\"classification\":\"" + (classification == AssessmentClassification.Invalid ? "invalid" : "incomplete") + "\",");
        string json = """
            {"schema_version":1,"identity":{"scenario_id":"A01","configuration_id":"C0","run_id":"run-1",
            "instance_id":"instance-1","expected_instance_id":"instance-1","client_id":"producer-1"},
            "generator_version":"synthetic","runtime_versions":{},"verifier_commit":"synthetic",
            "verifier_confluent_kafka_version":"synthetic","verifier_librdkafka_version":"synthetic",
            "status":"invalid","failed_checks":[],"judged_file_hashes":[],"diagnostics":[],
            "configuration_fidelity":{"assessment":{
            """ + outcomes + """
            "check_number":27,"evaluation":"not_evaluated"},"differences":[]}}
            """;
        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json),
            "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);
        if (disposition is null && classification is null)
        {
            Assert.Equal(assessment, RoundTrip(result).ConfigurationFidelity!.Assessment);
            Assert.True(parsed.IsSuccess);
            Assert.Equal(assessment, parsed.Value!.ConfigurationFidelity!.Assessment);
            return;
        }

        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result, OutputLimits));
        Assert.False(parsed.IsSuccess);
        StructuralProblem problem = Assert.Single(parsed.Problems);
        Assert.Null(problem.CheckNumber);
        Assert.Equal("unevaluated_assessment_has_outcome", problem.Code);
        Assert.Equal("configuration_fidelity.assessment", problem.Field);
    }

    [Theory]
    [InlineData("disposition")]
    [InlineData("classification")]
    public void Given_UnevaluatedAssessmentWithExplicitNullOutcome_When_Read_Then_StrictOptionalEnumRejectsNull(string field)
    {
        const string prefix = """
            {"schema_version":1,"identity":{"scenario_id":"A01","configuration_id":"C0","run_id":"run-1",
            "instance_id":"instance-1","expected_instance_id":"instance-1","client_id":"producer-1"},
            "generator_version":"synthetic","runtime_versions":{},"verifier_commit":"synthetic",
            "verifier_confluent_kafka_version":"synthetic","verifier_librdkafka_version":"synthetic",
            "status":"invalid","failed_checks":[],"judged_file_hashes":[],"diagnostics":[],
            "configuration_fidelity":{"assessment":{"check_number":27,"evaluation":"not_evaluated",
            """;
        string json = prefix + "\"" + field + "\":null},\"differences\":[]}}";
        ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json),
            "result.json", RecordedEvidence.SmallLimits, maxDiagnosticsPerCheck: 8);
        Assert.False(parsed.IsSuccess);
        StructuralProblem problem = Assert.Single(parsed.Problems);
        Assert.Null(problem.CheckNumber);
        Assert.Equal("invalid_or_wrong_json_value", problem.Code);
        Assert.Equal("configuration_fidelity.assessment." + field, problem.Field);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("abcd", true)]
    [InlineData("abcde", false)]
    [InlineData("\u00e9\u00e9", true)]
    [InlineData("\u00e9\u00e9a", false)]
    public void Given_SnippetByteBoundaries_When_ReadOrWritten_Then_BothDiagnosticLocationsEnforceOnlySnippetBytes(
        string? snippet, bool accepted)
    {
        foreach (bool nested in new[] { false, true })
        {
            var assessment = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, null, null,
                "capabilities.json", null, "delivery_latency_ms", "bad_row", snippet);
            VerifierResult result = nested
                ? Result(VerifierResultStatus.Invalid, fidelity: new ConfigurationFidelityResult(assessment, []))
                : Result(VerifierResultStatus.Invalid,
                    diagnostics: [new ResultDiagnostic(1, "bad_row", "ledger.jsonl", null, "delivery_latency_ms", snippet)]);
            var outputLimits = new OutputLimits(65_536, 8, 4);
            InputLimits inputLimits = RecordedEvidence.SmallLimits with { MaxDiagnosticBytes = 4 };
            byte[] generousBytes = VerifierResultCodec.Write(result, OutputLimits);
            if (!accepted)
            {
                Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Write(result, outputLimits));
                Assert.Throws<InputLimitExceededException>(() => VerifierResultCodec.Read(generousBytes,
                    "result.json", inputLimits, maxDiagnosticsPerCheck: 8));
                continue;
            }

            byte[] bytes = VerifierResultCodec.Write(result, outputLimits);
            VerifierResult roundTripped = Read(bytes, inputLimits);
            Assert.Equal(snippet, nested ? roundTripped.ConfigurationFidelity!.Assessment.Snippet : roundTripped.Diagnostics[0].Snippet);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_OneByteSnippetBudget_When_ReadOrWritten_Then_MetadataIsRetainedExactly(bool nested)
    {
        var assessment = new CheckAssessment(27, AssessmentEvaluation.NotEvaluated, null, null,
            "ledger.jsonl", null, "delivery_latency_ms", "bad_row", "x");
        var diagnostic = new ResultDiagnostic(1, "bad_row", "ledger.jsonl", null, "delivery_latency_ms", "x");
        VerifierResult result = nested
            ? Result(VerifierResultStatus.Invalid, fidelity: new ConfigurationFidelityResult(assessment, []))
            : Result(VerifierResultStatus.Invalid, diagnostics: [diagnostic]);
        byte[] bytes = VerifierResultCodec.Write(result, new OutputLimits(65_536, 8, 1));
        VerifierResult roundTripped = Read(bytes, RecordedEvidence.SmallLimits with { MaxDiagnosticBytes = 1 });
        if (nested) Assert.Equal(assessment, roundTripped.ConfigurationFidelity!.Assessment);
        else Assert.Equal(diagnostic, Assert.Single(roundTripped.Diagnostics));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_OptionalA01Collections_When_RoundTripped_Then_AbsentAndEmptyStayIndependent(bool codesPresent, bool windowsPresent)
    {
        VerifierResult result = Result(VerifierResultStatus.Invalid) with
        { A01RecordedEvidence = new(codesPresent ? [] : null, windowsPresent ? [] : null) };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement evidence = json.RootElement.GetProperty("a01_recorded_evidence");
        Assert.Equal(codesPresent, evidence.TryGetProperty("delivery_error_codes", out _));
        Assert.Equal(windowsPresent, evidence.TryGetProperty("windows", out _));
        A01RecordedEvidence read = Read(bytes).A01RecordedEvidence!;
        if (codesPresent) Assert.Empty(read.DeliveryErrorCodes!); else Assert.Null(read.DeliveryErrorCodes);
        if (windowsPresent) Assert.Empty(read.Windows!); else Assert.Null(read.Windows);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_LegacyReportTimestamp_When_Read_Then_UnknownPropertyIsRejected(bool window, bool alsoCurrent)
    {
        string reference = alsoCurrent ? "\"report_index\":10,\"report_timestamp\":10" : "\"report_timestamp\":10";
        string projection = window ? "\"a01_recorded_evidence\":{\"windows\":[{\"broker\":\"broker-1\",\"native_timestamp\":9," + reference + "}]}" :
            "\"probe_observation\":{\"usable\":true,\"qualifying\":false," + reference + "}";
        ParseResult<VerifierResult> parsed = ReadProjection(projection);
        Assert.False(parsed.IsSuccess);
        StructuralProblem problem = Assert.Single(parsed.Problems);
        Assert.Equal("unknown_property", problem.Code);
        Assert.EndsWith(".report_timestamp", problem.Field, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "10", true)]
    [InlineData(true, "10", true)]
    [InlineData(false, "null", false)]
    [InlineData(true, "null", false)]
    [InlineData(false, "\"10\"", false)]
    [InlineData(true, "\"10\"", false)]
    [InlineData(false, "1.5", false)]
    [InlineData(true, "1.5", false)]
    public void Given_ReportIndexKinds_When_Read_Then_OnlyInt64IsAccepted(bool window, string value, bool accepted)
    {
        string projection = window ? "\"a01_recorded_evidence\":{\"windows\":[{\"broker\":\"broker-1\",\"native_timestamp\":9,\"report_index\":" + value + "}]}" :
            "\"probe_observation\":{\"usable\":true,\"qualifying\":false,\"report_index\":" + value + "}";
        ParseResult<VerifierResult> parsed = ReadProjection(projection);
        Assert.Equal(accepted, parsed.IsSuccess);
        if (accepted)
            Assert.Equal(10, window ? parsed.Value!.A01RecordedEvidence!.Windows![0].ReportIndex : parsed.Value!.ProbeObservation!.ReportIndex);
        else Assert.Equal("wrong_json_kind", Assert.Single(parsed.Problems).Code);
    }

    [Theory]
    [InlineData("delivery_error_codes", "null")]
    [InlineData("delivery_error_codes", "{}")]
    [InlineData("delivery_error_codes", "[2147483648]")]
    [InlineData("windows", "null")]
    [InlineData("windows", "{}")]
    public void Given_MalformedOptionalCollection_When_Read_Then_ItIsRejected(string name, string value)
    {
        Assert.False(ReadProjection("\"a01_recorded_evidence\":{\"" + name + "\":" + value + "}").IsSuccess);
    }

    [Fact]
    public void Given_ProductionRecorderEvidence_When_RoundTripped_Then_InvalidUsableAndOverflowAbsenceArePreserved()
    {
        var request = A01EvidenceRecorderTests.Request([
            A01EvidenceRecorderTests.Report(90, A01EvidenceRecorderTests.Broker(timeouts: long.MinValue, maximum: 20_001, rttCount: 0)),
            A01EvidenceRecorderTests.Report(100, A01EvidenceRecorderTests.Broker(timeouts: long.MaxValue, maximum: 0, p99: 0))]);
        var judgement = new RunJudgementResult(VerifierResultStatus.Invalid, [12],
            Enumerable.Range(1, 27).Select(check => check == 12 ? RecordedEvidence.WindowFail(check, AssessmentClassification.Invalid) : RecordedEvidence.WindowPass(check)).ToArray(),
            null, null, new(RecordedEvidence.WindowPass(27), []), []);
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(request, judgement);
        VerifierResult result = Result(VerifierResultStatus.Invalid, failedChecks: [12]) with
        { ProbeObservation = recording.ProbeObservation, A01RecordedEvidence = recording.RecordedEvidence, Diagnostics = recording.Diagnostics };
        byte[] bytes = VerifierResultCodec.Write(result, OutputLimits);
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement windows = json.RootElement.GetProperty("a01_recorded_evidence").GetProperty("windows");
        Assert.False(windows[0].TryGetProperty("round_trip_p99_us", out _));
        Assert.False(windows[1].TryGetProperty("request_timeouts_increment", out _));
        Assert.Equal(0, windows[1].GetProperty("throttle_maximum_ms").GetInt64());
        Assert.Equal(0, windows[1].GetProperty("round_trip_p99_us").GetInt64());
        VerifierResult read = Read(bytes);
        Assert.Equal(VerifierResultStatus.Invalid, read.Status); Assert.Equal(FailedTwelve, read.FailedChecks); Assert.Null(read.Counts);
        Assert.Equal(new ProbeRepeatObservation(true, true, "broker-a", 0, 1, 20_001), read.ProbeObservation);
        Assert.Null(read.A01RecordedEvidence!.Windows![1].RequestTimeoutsIncrement);
        Assert.Equal(recording.Diagnostics.Single(), read.Diagnostics.Single());
    }

    private static ParseResult<VerifierResult> ReadProjection(string projection)
    {
        string root = Encoding.UTF8.GetString(VerifierResultCodec.Write(Result(VerifierResultStatus.Invalid), OutputLimits));
        return VerifierResultCodec.Read(Encoding.UTF8.GetBytes(root[..^1] + "," + projection + "}"), "result.json", RecordedEvidence.SmallLimits, 8);
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
