using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Results;

/// <summary>Reads and writes the bounded schema-one verifier result projection.</summary>
internal static class VerifierResultCodec
{
    private static readonly string[] CountFields = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];

    private static readonly IReadOnlySet<string> RequiredRootFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema_version", "identity", "verifier_commit",
        "verifier_confluent_kafka_version", "verifier_librdkafka_version", "status", "failed_checks",
        "judged_file_hashes", "diagnostics"
    };

    private static readonly IReadOnlySet<string> OptionalRootFields = BuildOptionalRootFields();

    private static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"
    };

    private static readonly IReadOnlySet<string> AssessmentFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "check_number", "evaluation", "disposition", "classification", "artifact", "row", "field", "code", "snippet"
    };

    /// <summary>Writes an explicit projection and rejects output or diagnostic limit excess.</summary>
    internal static byte[] Write(VerifierResult result, OutputLimits limits)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(limits);
        if (result.SchemaVersion != 1)
            throw new ArgumentException("Verifier results must use schema version 1.", nameof(result));
        ValidateForWrite(result, limits);

        using var stream = new BoundedMemoryStream(limits.MaxOutputBytes);
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", result.SchemaVersion);
            WriteIdentity(writer, result.Identity);
            if (result.GeneratorVersion is not null) writer.WriteString("generator_version", result.GeneratorVersion);
            if (result.RuntimeVersions is not null) WriteStringMap(writer, "runtime_versions", result.RuntimeVersions);
            writer.WriteString("verifier_commit", result.VerifierCommit);
            writer.WriteString("verifier_confluent_kafka_version", result.VerifierConfluentKafkaVersion);
            writer.WriteString("verifier_librdkafka_version", result.VerifierLibrdkafkaVersion);
            writer.WriteString("status", StatusName(result.Status));
            WriteIntArray(writer, "failed_checks", result.FailedChecks);

            if (result.StartLine is not null) WriteWatermarks(writer, "start_line", result.StartLine);
            if (result.EndLine is not null) WriteWatermarks(writer, "end_line", result.EndLine);
            if (result.DrainTimeMilliseconds is decimal drainTime) writer.WriteNumber("drain_time", TruncateMilliseconds(drainTime));
            if (result.DrainElapsedAtStopMilliseconds is decimal elapsed)
            {
                writer.WriteNumber("drain_elapsed_at_stop", TruncateMilliseconds(elapsed));
                writer.WriteBoolean("drain_elapsed_at_stop_lower_bound", true);
            }

            if (result.Status == VerifierResultStatus.CompleteValid)
            {
                OutcomeCounts counts = result.Counts!;
                writer.WriteNumber("ok", counts.Ok);
                writer.WriteNumber("loss", counts.Loss);
                writer.WriteNumber("phantom", counts.Phantom);
                writer.WriteNumber("reported_failure", counts.ReportedFailure);
                writer.WriteNumber("duplicate", counts.Duplicate);
                writer.WriteNumber("reorder", counts.Reorder);
            }

            if (result.BrokerCount is not null) WriteBrokerCount(writer, result.BrokerCount);
            if (result.ConfigurationFidelity is not null) WriteConfigurationFidelity(writer, result.ConfigurationFidelity);
            if (result.ProbeObservation is not null) WriteProbeObservation(writer, result.ProbeObservation);
            if (result.A01RecordedEvidence is not null) WriteA01RecordedEvidence(writer, result.A01RecordedEvidence);
            if (result.WorkloadObservation is not null) WriteWorkloadObservation(writer, result.WorkloadObservation);
            WriteJudgedHashes(writer, result.JudgedFileHashes);
            WriteDiagnostics(writer, result.Diagnostics);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>Reads a strict verifier result without judging the status it declares.</summary>
    internal static ParseResult<VerifierResult> Read(
        ReadOnlyMemory<byte> utf8,
        string artifact,
        InputLimits limits,
        int maxDiagnosticsPerCheck)
    {
        ParseResult<JsonDocument> document = StrictJson.ParseDocument(utf8, artifact, limits, checkNumber: null);
        if (!document.IsSuccess) return ParseResult<VerifierResult>.Failure(document.Problems);

        using JsonDocument parsed = document.Value!;
        JsonElement root = parsed.RootElement;
        IReadOnlySet<string> allowedRoot = new HashSet<string>(RequiredRootFields, StringComparer.Ordinal);
        var expandedAllowed = new HashSet<string>(allowedRoot, StringComparer.Ordinal);
        expandedAllowed.UnionWith(OptionalRootFields);
        IReadOnlyList<StructuralProblem> rootProblems = StrictJson.ValidateProperties(
            root, "$", RequiredRootFields, expandedAllowed, artifact, checkNumber: null);
        if (rootProblems.Count > 0) return ParseResult<VerifierResult>.Failure(rootProblems);

        if (!TryInt32(root, "schema_version", out int schemaVersion)) return Failure(artifact, "schema_version", "wrong_json_kind");
        if (schemaVersion != 1) return Failure(artifact, "schema_version", "unsupported_schema_version");
        if (!TryIdentity(root, artifact, out RunIdentity? identity, out StructuralProblem? identityProblem))
            return ParseResult<VerifierResult>.Failure(identityProblem!);
        string? generatorVersion = null;
        if (root.TryGetProperty("generator_version", out _) && !TryString(root, "generator_version", out generatorVersion))
            return Failure(artifact, "generator_version", "wrong_json_kind");
        IReadOnlyDictionary<string, string>? runtimeVersions = null;
        if (root.TryGetProperty("runtime_versions", out _) && !TryStringMap(root, "runtime_versions", artifact, out runtimeVersions, out StructuralProblem? mapProblem))
            return ParseResult<VerifierResult>.Failure(mapProblem!);
        if (!TryString(root, "verifier_commit", out string? verifierCommit)) return Failure(artifact, "verifier_commit", "wrong_json_kind");
        if (!TryString(root, "verifier_confluent_kafka_version", out string? verifierKafkaVersion)) return Failure(artifact, "verifier_confluent_kafka_version", "wrong_json_kind");
        if (!TryString(root, "verifier_librdkafka_version", out string? verifierLibrdkafkaVersion)) return Failure(artifact, "verifier_librdkafka_version", "wrong_json_kind");
        if (!TryString(root, "status", out string? statusText) || !TryStatus(statusText!, out VerifierResultStatus status))
            return Failure(artifact, "status", "invalid_enum_value");
        if (!TryIntArray(root, "failed_checks", artifact, out IReadOnlyList<int>? failedChecks, out StructuralProblem? failedChecksProblem))
            return ParseResult<VerifierResult>.Failure(failedChecksProblem!);

        if (!TryOptionalWatermarks(root, "start_line", artifact, out IReadOnlyList<WatermarkOffset>? startLine, out StructuralProblem? startProblem))
            return ParseResult<VerifierResult>.Failure(startProblem!);
        if (!TryOptionalWatermarks(root, "end_line", artifact, out IReadOnlyList<WatermarkOffset>? endLine, out StructuralProblem? endProblem))
            return ParseResult<VerifierResult>.Failure(endProblem!);

        decimal? drainTime = null;
        decimal? drainElapsed = null;
        bool hasDrainTime = root.TryGetProperty("drain_time", out JsonElement drainTimeElement);
        bool hasDrainElapsed = root.TryGetProperty("drain_elapsed_at_stop", out JsonElement drainElapsedElement);
        bool hasLowerBound = root.TryGetProperty("drain_elapsed_at_stop_lower_bound", out JsonElement lowerBoundElement);
        if (hasDrainTime && hasDrainElapsed) return Failure(artifact, "drain_time", "mutually_exclusive_fields");
        if (hasDrainTime)
        {
            if (!StrictJson.TryGetInt64(drainTimeElement, out long milliseconds)) return Failure(artifact, "drain_time", "wrong_json_kind");
            drainTime = milliseconds;
        }

        if (hasDrainElapsed)
        {
            if (!StrictJson.TryGetInt64(drainElapsedElement, out long milliseconds)) return Failure(artifact, "drain_elapsed_at_stop", "wrong_json_kind");
            if (!hasLowerBound || lowerBoundElement.ValueKind != JsonValueKind.True)
                return Failure(artifact, "drain_elapsed_at_stop_lower_bound", "expected_true");
            drainElapsed = milliseconds;
        }
        else if (hasLowerBound)
        {
            return Failure(artifact, "drain_elapsed_at_stop_lower_bound", "orphaned_lower_bound_marker");
        }

        bool anyCount = CountFields.Any(name => root.TryGetProperty(name, out _));
        OutcomeCounts? counts = null;
        if (status == VerifierResultStatus.CompleteValid)
        {
            if (!anyCount || !TryCounts(root, out counts)) return Failure(artifact, "ok", "missing_or_invalid_counts");
        }
        else if (anyCount)
        {
            return Failure(artifact, "ok", "counts_not_allowed_for_status");
        }

        if (!TryOptionalObject(root, "broker_count", artifact, ParseBrokerCount, out BrokerConnectionCount? brokerCount, out StructuralProblem? brokerProblem))
            return ParseResult<VerifierResult>.Failure(brokerProblem!);
        if (!TryOptionalObject(root, "configuration_fidelity", artifact, ParseConfigurationFidelity, out ConfigurationFidelityResult? fidelity, out StructuralProblem? fidelityProblem))
            return ParseResult<VerifierResult>.Failure(fidelityProblem!);
        if (fidelity is not null) CheckAssessmentInputLimits(fidelity.Assessment, limits.MaxDiagnosticBytes);
        if (!TryOptionalObject(root, "probe_observation", artifact, ParseProbeObservation, out ProbeRepeatObservation? probe, out StructuralProblem? probeProblem))
            return ParseResult<VerifierResult>.Failure(probeProblem!);
        if (!TryOptionalObject(root, "a01_recorded_evidence", artifact, ParseA01Evidence, out A01RecordedEvidence? a01, out StructuralProblem? a01Problem))
            return ParseResult<VerifierResult>.Failure(a01Problem!);
        if (!TryOptionalObject(root, "workload_observation", artifact, ParseWorkloadObservation, out WorkloadObservation? workload, out StructuralProblem? workloadProblem))
            return ParseResult<VerifierResult>.Failure(workloadProblem!);
        if (!TryHashes(root, artifact, out IReadOnlyList<JudgedFileHash>? hashes, out StructuralProblem? hashesProblem))
            return ParseResult<VerifierResult>.Failure(hashesProblem!);
        if (!TryDiagnostics(root, artifact, limits.MaxDiagnosticBytes, out IReadOnlyList<ResultDiagnostic>? diagnostics, out StructuralProblem? diagnosticsProblem))
            return ParseResult<VerifierResult>.Failure(diagnosticsProblem!);
        ValidateInputDiagnostics(diagnostics!, fidelity?.Assessment, maxDiagnosticsPerCheck);

        return ParseResult<VerifierResult>.Success(new VerifierResult(
            schemaVersion, identity!, generatorVersion!, runtimeVersions!, verifierCommit!, verifierKafkaVersion!, verifierLibrdkafkaVersion!,
            status, failedChecks!, startLine, endLine, drainTime, drainElapsed, counts, brokerCount, fidelity, probe, a01, hashes!, diagnostics!, workload));
    }

    private static void ValidateForWrite(VerifierResult result, OutputLimits limits)
    {
        ArgumentNullException.ThrowIfNull(result.Identity);
        ArgumentNullException.ThrowIfNull(result.FailedChecks);
        ArgumentNullException.ThrowIfNull(result.JudgedFileHashes);
        ArgumentNullException.ThrowIfNull(result.Diagnostics);
        if (result.Status == VerifierResultStatus.CompleteValid && result.Counts is null)
            throw new ArgumentException("A complete_valid result requires all six supplied outcome counts.", nameof(result));
        if (result.DrainTimeMilliseconds is not null && result.DrainElapsedAtStopMilliseconds is not null)
            throw new ArgumentException("drain_time and drain_elapsed_at_stop are mutually exclusive.", nameof(result));
        if (result.BrokerCount is not null && !result.BrokerCount.LowerBound)
            throw new ArgumentException("A broker_count projection requires lower_bound=true.", nameof(result));

        var perCheck = new Dictionary<int, int>();
        foreach (ResultDiagnostic diagnostic in result.Diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);
            CountDiagnostic(diagnostic.Check, perCheck, limits);
            CheckDiagnosticBytes(diagnostic.Snippet, limits);
        }

        if (result.ConfigurationFidelity is not null)
        {
            CheckAssessment assessment = result.ConfigurationFidelity.Assessment;
            ArgumentNullException.ThrowIfNull(assessment);
            if (assessment.Evaluation == AssessmentEvaluation.NotEvaluated &&
                (assessment.Disposition is not null || assessment.Classification is not null))
                throw new ArgumentException("An unevaluated assessment cannot have a disposition or classification.", nameof(result));
            CountDiagnostic(assessment.CheckNumber, perCheck, limits);
            CheckDiagnosticBytes(assessment.Snippet, limits);
        }

        foreach (KeyValuePair<int, int> item in perCheck)
        {
            if (item.Value > limits.MaxDiagnosticsPerCheck)
                throw new InputLimitExceededException("The verifier result exceeds the configured diagnostics per check limit.");
        }
    }

    private static void CountDiagnostic(int check, Dictionary<int, int> counts, OutputLimits limits)
    {
        int next = counts.TryGetValue(check, out int current) ? current + 1 : 1;
        counts[check] = next;
        if (next > limits.MaxDiagnosticsPerCheck)
            throw new InputLimitExceededException("The verifier result exceeds the configured diagnostics per check limit.");
    }

    private static void CheckDiagnosticBytes(string? value, OutputLimits limits)
    {
        if (value is not null && Encoding.UTF8.GetByteCount(value) > limits.MaxDiagnosticBytes)
            throw new InputLimitExceededException("A verifier result diagnostic exceeds the configured byte limit.");
    }

    private static void WriteIdentity(Utf8JsonWriter writer, RunIdentity identity)
    {
        writer.WritePropertyName("identity");
        writer.WriteStartObject();
        writer.WriteString("scenario_id", identity.ScenarioId);
        writer.WriteString("configuration_id", identity.ConfigurationId);
        writer.WriteString("run_id", identity.RunId);
        writer.WriteString("instance_id", identity.InstanceId);
        writer.WriteString("expected_instance_id", identity.ExpectedInstanceId);
        writer.WriteString("client_id", identity.ClientId);
        writer.WriteEndObject();
    }

    private static void WriteStringMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach (KeyValuePair<string, string> value in values)
        {
            writer.WriteString(value.Key, value.Value);
        }
        writer.WriteEndObject();
    }

    private static void WriteIntArray(Utf8JsonWriter writer, string name, IReadOnlyList<int> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (int value in values) writer.WriteNumberValue(value);
        writer.WriteEndArray();
    }

    private static void WriteWatermarks(Utf8JsonWriter writer, string name, IReadOnlyList<WatermarkOffset> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (WatermarkOffset value in values)
        {
            writer.WriteStartObject();
            writer.WriteNumber("partition", value.Partition);
            writer.WriteNumber("offset", value.Offset);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteBrokerCount(Utf8JsonWriter writer, BrokerConnectionCount value)
    {
        writer.WritePropertyName("broker_count");
        writer.WriteStartObject();
        writer.WriteString("role", value.Role);
        writer.WriteString("client_id", value.ClientId);
        if (value.Maximum is long maximum) writer.WriteNumber("maximum", maximum);
        if (value.FirstPostDrain is long first) writer.WriteNumber("first_post_drain", first);
        writer.WriteBoolean("lower_bound", true);
        writer.WriteEndObject();
    }

    private static void WriteConfigurationFidelity(Utf8JsonWriter writer, ConfigurationFidelityResult value)
    {
        writer.WritePropertyName("configuration_fidelity");
        writer.WriteStartObject();
        writer.WritePropertyName("assessment");
        WriteAssessment(writer, value.Assessment);
        writer.WritePropertyName("differences");
        writer.WriteStartArray();
        foreach (SettingDifference difference in value.Differences)
        {
            writer.WriteStartObject();
            writer.WriteString("setting", difference.Setting);
            if (difference.Expected is not null) writer.WriteString("expected", difference.Expected);
            if (difference.Actual is not null) writer.WriteString("actual", difference.Actual);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteAssessment(Utf8JsonWriter writer, CheckAssessment assessment)
    {
        writer.WriteStartObject();
        writer.WriteNumber("check_number", assessment.CheckNumber);
        writer.WriteString("evaluation", assessment.Evaluation == AssessmentEvaluation.NotEvaluated ? "not_evaluated" : "evaluated");
        if (assessment.Disposition is AssessmentDisposition disposition)
            writer.WriteString("disposition", disposition == AssessmentDisposition.Pass ? "pass" : "fail");
        if (assessment.Classification is AssessmentClassification classification)
            writer.WriteString("classification", classification == AssessmentClassification.Invalid ? "invalid" : "incomplete");
        WriteOptionalString(writer, "artifact", assessment.Artifact);
        if (assessment.Row is long row) writer.WriteNumber("row", row);
        WriteOptionalString(writer, "field", assessment.Field);
        WriteOptionalString(writer, "code", assessment.Code);
        WriteOptionalString(writer, "snippet", assessment.Snippet);
        writer.WriteEndObject();
    }

    private static void WriteProbeObservation(Utf8JsonWriter writer, ProbeRepeatObservation value)
    {
        writer.WritePropertyName("probe_observation");
        writer.WriteStartObject();
        writer.WriteBoolean("usable", value.Usable);
        writer.WriteBoolean("qualifying", value.Qualifying);
        WriteOptionalString(writer, "broker", value.Broker);
        if (value.ReportIndex is long index) writer.WriteNumber("report_index", index);
        if (value.WindowCount is long count) writer.WriteNumber("window_count", count);
        if (value.WindowMaximum is long maximum) writer.WriteNumber("window_maximum", maximum);
        writer.WriteEndObject();
    }

    private static void WriteA01RecordedEvidence(Utf8JsonWriter writer, A01RecordedEvidence value)
    {
        writer.WritePropertyName("a01_recorded_evidence");
        writer.WriteStartObject();
        if (value.DeliveryErrorCodes is not null) WriteIntArray(writer, "delivery_error_codes", value.DeliveryErrorCodes);
        if (value.Windows is not null)
        {
            writer.WritePropertyName("windows");
            writer.WriteStartArray();
            foreach (A01WindowEvidence window in value.Windows)
            {
                writer.WriteStartObject();
                writer.WriteString("broker", window.Broker);
                writer.WriteNumber("report_index", window.ReportIndex);
                writer.WriteNumber("native_timestamp", window.NativeTimestamp);
                if (window.RequestTimeoutsIncrement is long timeouts) writer.WriteNumber("request_timeouts_increment", timeouts);
                if (window.DisconnectsIncrement is long disconnects) writer.WriteNumber("disconnects_increment", disconnects);
                if (window.ThrottleMaximumMilliseconds is long throttle) writer.WriteNumber("throttle_maximum_ms", throttle);
                if (window.RoundTripP99Microseconds is long rtt) writer.WriteNumber("round_trip_p99_us", rtt);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteWorkloadObservation(Utf8JsonWriter writer, WorkloadObservation value)
    {
        writer.WritePropertyName("workload_observation"); writer.WriteStartObject(); writer.WriteBoolean("synthetic", value.Synthetic);
        if(value.PlannedAttempts is long planned) writer.WriteNumber("planned_attempts",planned);
        if(value.InvokedAttempts is long invoked) writer.WriteNumber("invoked_attempts",invoked);
        if(value.TerminalObservations is long terminal) writer.WriteNumber("terminal_observations",terminal);
        if(value.FixedProfileComplete is bool complete) writer.WriteBoolean("fixed_profile_complete",complete);
        if(value.FirstUnexecutedOrdinal is long first) writer.WriteNumber("first_unexecuted_ordinal",first);
        if(value.Stages is not null){writer.WritePropertyName("stages");writer.WriteStartArray();foreach(var stage in value.Stages){writer.WriteStartObject();writer.WriteString("id",stage.Id);writer.WriteNumber("offered_attempts_per_second",stage.OfferedAttemptsPerSecond);writer.WriteNumber("duration_seconds",stage.DurationSeconds);if(stage.InvokedAttempts is long si)writer.WriteNumber("invoked_attempts",si);if(stage.DeliveredObservations is long sd)writer.WriteNumber("delivered_observations",sd);if(stage.FailedObservations is long sf)writer.WriteNumber("failed_observations",sf);writer.WriteEndObject();}writer.WriteEndArray();}
        writer.WriteEndObject();
    }

    private static bool ParseWorkloadObservation(JsonElement e,string path,string artifact,out WorkloadObservation? value,out StructuralProblem? problem)
    {
        value=null;problem=null;
        if(!Validate(e,path,Set("synthetic"),Set("synthetic","planned_attempts","invoked_attempts","terminal_observations","fixed_profile_complete","first_unexecuted_ordinal","stages"),artifact,out problem))return false;
        if(!TryBool(e,"synthetic",out bool synthetic)){problem=Problem(artifact,path+".synthetic","wrong_json_kind");return false;}
        if(!TryOptionalInt64(e,"planned_attempts",artifact,path,out long? planned,out problem)||!TryOptionalInt64(e,"invoked_attempts",artifact,path,out long? invoked,out problem)||!TryOptionalInt64(e,"terminal_observations",artifact,path,out long? terminal,out problem)||!TryOptionalInt64(e,"first_unexecuted_ordinal",artifact,path,out long? first,out problem))return false;
        bool? complete=null;if(e.TryGetProperty("fixed_profile_complete",out var completeElement)){if(completeElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)){problem=Problem(artifact,path+".fixed_profile_complete","wrong_json_kind");return false;}complete=completeElement.GetBoolean();}
        IReadOnlyList<WorkloadStageObservation>? stages=null;
        if(e.TryGetProperty("stages",out var array))
        {
            if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength()>4){problem=Problem(artifact,path+".stages","wrong_json_kind");return false;}
            var parsed=new List<WorkloadStageObservation>();var ids=new HashSet<string>(StringComparer.Ordinal);
            string[] allowedIds=["ramp100","ramp300","ramp900","ramp1800"];
            int index=0;foreach(JsonElement item in array.EnumerateArray())
            {
                string itemPath=$"{path}.stages[{index++}]";
                if(!Validate(item,itemPath,Set("id","offered_attempts_per_second","duration_seconds"),Set("id","offered_attempts_per_second","duration_seconds","invoked_attempts","delivered_observations","failed_observations"),artifact,out problem))return false;
                if(!TryString(item,"id",out string? id)||!allowedIds.Contains(id,StringComparer.Ordinal)||!ids.Add(id!)){problem=Problem(artifact,itemPath+".id","invalid_value");return false;}
                if(!TryInt64(item,"offered_attempts_per_second",out long offered)||offered<0||!TryInt64(item,"duration_seconds",out long duration)||duration<0){problem=Problem(artifact,itemPath,"invalid_value");return false;}
                if(!TryOptionalInt64(item,"invoked_attempts",artifact,itemPath,out long? si,out problem)||!TryOptionalInt64(item,"delivered_observations",artifact,itemPath,out long? sd,out problem)||!TryOptionalInt64(item,"failed_observations",artifact,itemPath,out long? sf,out problem))return false;
                parsed.Add(new(id!,offered,duration,si,sd,sf));
            }
            stages=parsed;
        }
        if(new[]{planned,invoked,terminal,first}.Any(x=>x<0)){problem=Problem(artifact,path,"negative_value");return false;}
        value=new(synthetic,planned,invoked,terminal,complete,first,stages);return true;
    }

    private static void WriteJudgedHashes(Utf8JsonWriter writer, IReadOnlyList<JudgedFileHash> values)
    {
        writer.WritePropertyName("judged_file_hashes");
        writer.WriteStartArray();
        foreach (JudgedFileHash value in values)
        {
            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WriteString("sha256", value.Sha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteDiagnostics(Utf8JsonWriter writer, IReadOnlyList<ResultDiagnostic> values)
    {
        writer.WritePropertyName("diagnostics");
        writer.WriteStartArray();
        foreach (ResultDiagnostic value in values)
        {
            writer.WriteStartObject();
            writer.WriteNumber("check", value.Check);
            writer.WriteString("code", value.Code);
            WriteNullableString(writer, "artifact", value.Artifact);
            if (value.Row is long row) writer.WriteNumber("row", row); else writer.WriteNull("row");
            WriteNullableString(writer, "field", value.Field);
            WriteNullableString(writer, "snippet", value.Snippet);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static bool TryIdentity(JsonElement root, string artifact, out RunIdentity? identity, out StructuralProblem? problem)
    {
        identity = null;
        problem = null;
        JsonElement value = root.GetProperty("identity");
        IReadOnlyList<StructuralProblem> properties = StrictJson.ValidateProperties(value, "identity", IdentityFields, IdentityFields, artifact, checkNumber: null);
        if (properties.Count > 0) { problem = properties[0]; return false; }
        if (!TryString(value, "scenario_id", out string? scenarioId)) { problem = Problem(artifact, "identity.scenario_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "configuration_id", out string? configurationId)) { problem = Problem(artifact, "identity.configuration_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "run_id", out string? runId)) { problem = Problem(artifact, "identity.run_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "instance_id", out string? instanceId)) { problem = Problem(artifact, "identity.instance_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "expected_instance_id", out string? expectedInstanceId)) { problem = Problem(artifact, "identity.expected_instance_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "client_id", out string? clientId)) { problem = Problem(artifact, "identity.client_id", "wrong_json_kind"); return false; }
        identity = new RunIdentity(scenarioId!, configurationId!, runId!, instanceId!, expectedInstanceId!, clientId!);
        return true;

    }

    private static bool TryStringMap(JsonElement root, string name, string artifact, out IReadOnlyDictionary<string, string>? values, out StructuralProblem? problem)
    {
        values = null;
        problem = null;
        JsonElement element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Object) { problem = Problem(artifact, name, "wrong_json_kind"); return false; }
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!StrictJson.TryGetString(property.Value, out string value))
            {
                problem = Problem(artifact, name + "." + property.Name, "wrong_json_kind");
                return false;
            }
            map.Add(property.Name, value);
        }
        values = map;
        return true;
    }

    private static bool TryIntArray(JsonElement root, string name, string artifact, out IReadOnlyList<int>? values, out StructuralProblem? problem)
    {
        values = null;
        problem = null;
        JsonElement element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, name, "wrong_json_kind"); return false; }
        var list = new List<int>(element.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (!StrictJson.TryGetInt32(item, out int value)) { problem = Problem(artifact, $"{name}[{index}]", "wrong_json_kind"); return false; }
            list.Add(value);
            index++;
        }
        values = list;
        return true;
    }

    private static bool TryOptionalWatermarks(JsonElement root, string name, string artifact, out IReadOnlyList<WatermarkOffset>? values, out StructuralProblem? problem)
    {
        values = null;
        problem = null;
        if (!root.TryGetProperty(name, out JsonElement array)) return true;
        if (array.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, name, "wrong_json_kind"); return false; }
        var list = new List<WatermarkOffset>(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string path = $"{name}[{index}]";
            if (!HasExactProperties(item, path, Set("partition", "offset"), artifact, out problem)) return false;
            if (!TryInt32(item, "partition", out int partition)) { problem = Problem(artifact, path + ".partition", "wrong_json_kind"); return false; }
            if (!TryInt64(item, "offset", out long offset)) { problem = Problem(artifact, path + ".offset", "wrong_json_kind"); return false; }
            list.Add(new WatermarkOffset(partition, offset));
            index++;
        }
        values = list;
        return true;
    }

    private static bool TryCounts(JsonElement root, out OutcomeCounts? counts)
    {
        counts = null;
        if (!TryInt64(root, "ok", out long ok) || !TryInt64(root, "loss", out long loss) ||
            !TryInt64(root, "phantom", out long phantom) || !TryInt64(root, "reported_failure", out long reportedFailure) ||
            !TryInt64(root, "duplicate", out long duplicate) || !TryInt64(root, "reorder", out long reorder)) return false;
        counts = new OutcomeCounts(ok, loss, phantom, reportedFailure, duplicate, reorder);
        return true;
    }

    private delegate bool ObjectParser<T>(JsonElement element, string path, string artifact, out T? value, out StructuralProblem? problem);

    private static bool TryOptionalObject<T>(JsonElement root, string name, string artifact, ObjectParser<T> parser, out T? value, out StructuralProblem? problem)
        where T : class
    {
        value = null;
        problem = null;
        if (!root.TryGetProperty(name, out JsonElement element)) return true;
        if (element.ValueKind != JsonValueKind.Object) { problem = Problem(artifact, name, "wrong_json_kind"); return false; }
        return parser(element, name, artifact, out value, out problem);
    }

    private static bool ParseBrokerCount(JsonElement element, string path, string artifact, out BrokerConnectionCount? value, out StructuralProblem? problem)
    {
        value = null;
        IReadOnlySet<string> allowed = Set("role", "client_id", "maximum", "first_post_drain", "lower_bound");
        if (!Validate(element, path, Set("role", "client_id", "lower_bound"), allowed, artifact, out problem)) return false;
        if (!TryString(element, "role", out string? role)) { problem = Problem(artifact, path + ".role", "wrong_json_kind"); return false; }
        if (!TryString(element, "client_id", out string? clientId)) { problem = Problem(artifact, path + ".client_id", "wrong_json_kind"); return false; }
        if (!TryBool(element, "lower_bound", out bool lowerBound)) { problem = Problem(artifact, path + ".lower_bound", "wrong_json_kind"); return false; }
        if (!lowerBound) { problem = Problem(artifact, path + ".lower_bound", "expected_true"); return false; }
        if (!TryOptionalInt64(element, "maximum", artifact, path, out long? maximum, out problem)) return false;
        if (!TryOptionalInt64(element, "first_post_drain", artifact, path, out long? first, out problem)) return false;
        value = new BrokerConnectionCount(role!, clientId!, maximum, first, lowerBound);
        return true;
    }

    private static bool ParseConfigurationFidelity(JsonElement element, string path, string artifact, out ConfigurationFidelityResult? value, out StructuralProblem? problem)
    {
        value = null;
        if (!Validate(element, path, Set("assessment", "differences"), Set("assessment", "differences"), artifact, out problem)) return false;
        if (!TryParseAssessment(element.GetProperty("assessment"), path + ".assessment", artifact, out CheckAssessment? assessment, out problem)) return false;
        JsonElement array = element.GetProperty("differences");
        if (array.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, path + ".differences", "wrong_json_kind"); return false; }
        var differences = new List<SettingDifference>(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string itemPath = $"{path}.differences[{index}]";
            if (!Validate(item, itemPath, Set("setting"), Set("setting", "expected", "actual"), artifact, out problem)) return false;
            if (!TryString(item, "setting", out string? setting)) { problem = Problem(artifact, $"{path}.differences[{index}].setting", "wrong_json_kind"); return false; }
            if (!TryOptionalString(item, "expected", artifact, itemPath, out string? expected, out problem)) return false;
            if (!TryOptionalString(item, "actual", artifact, itemPath, out string? actual, out problem)) return false;
            differences.Add(new SettingDifference(setting!, expected, actual));
            index++;
        }
        value = new ConfigurationFidelityResult(assessment!, differences);
        return true;
    }

    private static bool TryParseAssessment(JsonElement element, string path, string artifact, out CheckAssessment? value, out StructuralProblem? problem)
    {
        value = null;
        if (!Validate(element, path, Set("check_number", "evaluation"), AssessmentFields, artifact, out problem)) return false;
        if (!TryInt32(element, "check_number", out int check)) { problem = Problem(artifact, path + ".check_number", "invalid_or_wrong_json_value"); return false; }
        if (!TryString(element, "evaluation", out string? evaluationText)) { problem = Problem(artifact, path + ".evaluation", "invalid_or_wrong_json_value"); return false; }
        AssessmentEvaluation evaluation;
        if (evaluationText == "not_evaluated") evaluation = AssessmentEvaluation.NotEvaluated;
        else if (evaluationText == "evaluated") evaluation = AssessmentEvaluation.Evaluated;
        else { problem = Problem(artifact, path + ".evaluation", "invalid_or_wrong_json_value"); return false; }
        if (!TryOptionalEnum(element, "disposition", "pass", "fail", out AssessmentDisposition? disposition)) { problem = Problem(artifact, path + ".disposition", "invalid_or_wrong_json_value"); return false; }
        if (!TryOptionalEnum(element, "classification", "invalid", "incomplete", out AssessmentClassification? classification)) { problem = Problem(artifact, path + ".classification", "invalid_or_wrong_json_value"); return false; }
        if (evaluation == AssessmentEvaluation.NotEvaluated && (disposition is not null || classification is not null))
        {
            problem = Problem(artifact, path, "unevaluated_assessment_has_outcome");
            return false;
        }
        if (!TryOptionalString(element, "artifact", artifact, path, out string? artifactName, out problem)) return false;
        if (!TryOptionalInt64(element, "row", artifact, path, out long? row, out problem)) return false;
        if (!TryOptionalString(element, "field", artifact, path, out string? field, out problem)) return false;
        if (!TryOptionalString(element, "code", artifact, path, out string? code, out problem)) return false;
        if (!TryOptionalString(element, "snippet", artifact, path, out string? snippet, out problem)) return false;
        value = new CheckAssessment(check, evaluation, disposition, classification, artifactName, row, field, code, snippet);
        return true;
    }

    private static bool ParseProbeObservation(JsonElement element, string path, string artifact, out ProbeRepeatObservation? value, out StructuralProblem? problem)
    {
        value = null;
        if (!Validate(element, path, Set("usable", "qualifying"), Set("usable", "qualifying", "broker", "report_index", "window_count", "window_maximum"), artifact, out problem)) return false;
        if (!TryBool(element, "usable", out bool usable)) { problem = Problem(artifact, path + ".usable", "wrong_json_kind"); return false; }
        if (!TryBool(element, "qualifying", out bool qualifying)) { problem = Problem(artifact, path + ".qualifying", "wrong_json_kind"); return false; }
        if (!TryOptionalString(element, "broker", artifact, path, out string? broker, out problem)) return false;
        if (!TryOptionalInt64(element, "report_index", artifact, path, out long? index, out problem)) return false;
        if (!TryOptionalInt64(element, "window_count", artifact, path, out long? count, out problem)) return false;
        if (!TryOptionalInt64(element, "window_maximum", artifact, path, out long? maximum, out problem)) return false;
        value = new ProbeRepeatObservation(usable, qualifying, broker, index, count, maximum);
        return true;
    }

    private static bool ParseA01Evidence(JsonElement element, string path, string artifact, out A01RecordedEvidence? value, out StructuralProblem? problem)
    {
        value = null;
        if (!Validate(element, path, Set(), Set("delivery_error_codes", "windows"), artifact, out problem)) return false;
        IReadOnlyList<int>? codes = null;
        if (element.TryGetProperty("delivery_error_codes", out _) &&
            !TryIntArray(element, "delivery_error_codes", artifact, out codes, out problem)) return false;
        if (!element.TryGetProperty("windows", out JsonElement array))
        {
            value = new A01RecordedEvidence(codes, null);
            return true;
        }
        if (array.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, path + ".windows", "wrong_json_kind"); return false; }
        var windows = new List<A01WindowEvidence>(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string itemPath = $"{path}.windows[{index}]";
            IReadOnlySet<string> required = Set("broker", "report_index", "native_timestamp");
            IReadOnlySet<string> allowed = Set("broker", "report_index", "native_timestamp", "request_timeouts_increment", "disconnects_increment", "throttle_maximum_ms", "round_trip_p99_us");
            if (!Validate(item, itemPath, Set(), allowed, artifact, out problem) ||
                !Validate(item, itemPath, required, allowed, artifact, out problem)) return false;
            if (!TryString(item, "broker", out string? broker)) { problem = Problem(artifact, $"{itemPath}.broker", "wrong_json_kind"); return false; }
            if (!TryInt64(item, "report_index", out long reportIndex)) { problem = Problem(artifact, $"{itemPath}.report_index", "wrong_json_kind"); return false; }
            if (!TryInt64(item, "native_timestamp", out long nativeTimestamp)) { problem = Problem(artifact, $"{itemPath}.native_timestamp", "wrong_json_kind"); return false; }
            if (!TryOptionalInt64(item, "request_timeouts_increment", artifact, itemPath, out long? timeouts, out problem)) return false;
            if (!TryOptionalInt64(item, "disconnects_increment", artifact, itemPath, out long? disconnects, out problem)) return false;
            if (!TryOptionalInt64(item, "throttle_maximum_ms", artifact, itemPath, out long? throttle, out problem)) return false;
            if (!TryOptionalInt64(item, "round_trip_p99_us", artifact, itemPath, out long? rtt, out problem)) return false;
            windows.Add(new A01WindowEvidence(broker!, reportIndex, nativeTimestamp, timeouts, disconnects, throttle, rtt));
            index++;
        }
        value = new A01RecordedEvidence(codes, windows);
        return true;
    }

    private static bool TryHashes(JsonElement root, string artifact, out IReadOnlyList<JudgedFileHash>? values, out StructuralProblem? problem)
    {
        values = null;
        problem = null;
        JsonElement array = root.GetProperty("judged_file_hashes");
        if (array.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, "judged_file_hashes", "wrong_json_kind"); return false; }
        var list = new List<JudgedFileHash>(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string path = $"judged_file_hashes[{index}]";
            if (!Validate(item, path, Set("name", "sha256"), Set("name", "sha256"), artifact, out problem)) return false;
            if (!TryString(item, "name", out string? name) || !TryString(item, "sha256", out string? digest))
            { problem = Problem(artifact, path, "wrong_json_kind"); return false; }
            list.Add(new JudgedFileHash(name!, digest!));
            index++;
        }
        values = list;
        return true;
    }

    private static bool TryDiagnostics(JsonElement root, string artifact, int maxDiagnosticBytes, out IReadOnlyList<ResultDiagnostic>? values, out StructuralProblem? problem)
    {
        values = null;
        problem = null;
        JsonElement array = root.GetProperty("diagnostics");
        if (array.ValueKind != JsonValueKind.Array) { problem = Problem(artifact, "diagnostics", "wrong_json_kind"); return false; }
        var list = new List<ResultDiagnostic>(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string path = $"diagnostics[{index}]";
            IReadOnlySet<string> diagnosticFields = Set("check", "code", "artifact", "row", "field", "snippet");
            if (!Validate(item, path, diagnosticFields, diagnosticFields, artifact, out problem)) return false;
            if (!TryInt32(item, "check", out int check) || !TryString(item, "code", out string? code))
            { problem = Problem(artifact, path, "wrong_json_kind"); return false; }
            if (!TryNullableString(item, "artifact", artifact, path, out string? artifactName, out problem)) return false;
            if (!TryNullableInt64(item, "row", artifact, path, out long? row, out problem)) return false;
            if (!TryNullableString(item, "field", artifact, path, out string? field, out problem)) return false;
            if (!TryNullableString(item, "snippet", artifact, path, out string? snippet, out problem)) return false;
            if (snippet is not null && Encoding.UTF8.GetByteCount(snippet) > maxDiagnosticBytes)
                throw new InputLimitExceededException($"{artifact} diagnostic {index} exceeds the configured diagnostic byte limit.");
            list.Add(new ResultDiagnostic(check, code!, artifactName!, row, field, snippet));
            index++;
        }
        values = list;
        return true;
    }

    private static void CheckAssessmentInputLimits(CheckAssessment assessment, int maxDiagnosticBytes)
    {
        if (assessment.Snippet is not null && Encoding.UTF8.GetByteCount(assessment.Snippet) > maxDiagnosticBytes)
            throw new InputLimitExceededException("A configuration fidelity assessment exceeds the configured diagnostic byte limit.");
    }

    private static void ValidateInputDiagnostics(
        IReadOnlyList<ResultDiagnostic> diagnostics,
        CheckAssessment? assessment,
        int maxDiagnosticsPerCheck)
    {
        var perCheck = new Dictionary<int, int>();
        foreach (ResultDiagnostic diagnostic in diagnostics)
        {
            CountInputDiagnostic(diagnostic.Check, perCheck, maxDiagnosticsPerCheck);
        }

        if (assessment is not null)
        {
            CountInputDiagnostic(assessment.CheckNumber, perCheck, maxDiagnosticsPerCheck);
        }
    }

    private static void CountInputDiagnostic(int check, Dictionary<int, int> perCheck, int maxDiagnosticsPerCheck)
    {
        int count = perCheck.TryGetValue(check, out int current) ? current + 1 : 1;
        perCheck[check] = count;
        if (count > maxDiagnosticsPerCheck)
            throw new InputLimitExceededException("The verifier result exceeds the configured diagnostics per check limit.");
    }

    private static bool Validate(JsonElement element, string path, IReadOnlySet<string> required, IReadOnlySet<string> allowed, string artifact, out StructuralProblem? problem)
    {
        IReadOnlyList<StructuralProblem> problems = StrictJson.ValidateProperties(element, path, required, allowed, artifact, checkNumber: null);
        problem = problems.Count == 0 ? null : problems[0];
        return problem is null;
    }

    private static bool HasExactProperties(JsonElement element, string path, IReadOnlySet<string> names, string artifact, out StructuralProblem? problem) =>
        Validate(element, path, names, names, artifact, out problem);

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static bool TryString(JsonElement parent, string name, out string? value)
    {
        if (parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetString(element, out string parsed))
        { value = parsed; return true; }
        value = null;
        return false;
    }

    private static bool TryInt32(JsonElement parent, string name, out int value) =>
        parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetInt32(element, out value) ? true : Fail(out value);
    private static bool TryInt64(JsonElement parent, string name, out long value) =>
        parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetInt64(element, out value) ? true : Fail(out value);
    private static bool TryBool(JsonElement parent, string name, out bool value)
    {
        if (parent.TryGetProperty(name, out JsonElement element) && (element.ValueKind is JsonValueKind.True or JsonValueKind.False))
        { value = element.GetBoolean(); return true; }
        value = false;
        return false;
    }

    private static bool TryOptionalString(JsonElement parent, string name, string artifact, string path, out string? value, out StructuralProblem? problem)
    {
        problem = null;
        if (!parent.TryGetProperty(name, out JsonElement element)) { value = null; return true; }
        if (StrictJson.TryGetString(element, out string parsed)) { value = parsed; return true; }
        value = null; problem = Problem(artifact, path + "." + name, "wrong_json_kind"); return false;
    }

    private static bool TryOptionalInt64(JsonElement parent, string name, string artifact, string path, out long? value, out StructuralProblem? problem)
    {
        problem = null;
        if (!parent.TryGetProperty(name, out JsonElement element)) { value = null; return true; }
        if (StrictJson.TryGetInt64(element, out long parsed)) { value = parsed; return true; }
        value = null; problem = Problem(artifact, path + "." + name, "wrong_json_kind"); return false;
    }

    private static bool TryNullableString(JsonElement parent, string name, string artifact, string path, out string? value, out StructuralProblem? problem)
    {
        problem = null;
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            value = null;
            problem = Problem(artifact, path + "." + name, "missing_property");
            return false;
        }
        if (element.ValueKind == JsonValueKind.Null) { value = null; return true; }
        if (StrictJson.TryGetString(element, out string parsed)) { value = parsed; return true; }
        value = null; problem = Problem(artifact, path + "." + name, "wrong_json_kind"); return false;
    }

    private static bool TryNullableInt64(JsonElement parent, string name, string artifact, string path, out long? value, out StructuralProblem? problem)
    {
        problem = null;
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            value = null;
            problem = Problem(artifact, path + "." + name, "missing_property");
            return false;
        }
        if (element.ValueKind == JsonValueKind.Null) { value = null; return true; }
        if (StrictJson.TryGetInt64(element, out long parsed)) { value = parsed; return true; }
        value = null; problem = Problem(artifact, path + "." + name, "wrong_json_kind"); return false;
    }

    private static bool TryOptionalEnum<T>(JsonElement element, string name, string first, string second, out T? value) where T : struct, Enum
    {
        value = null;
        if (!element.TryGetProperty(name, out JsonElement property)) return true;
        if (property.ValueKind != JsonValueKind.String) return false;
        string? text = property.GetString();
        if (typeof(T) == typeof(AssessmentDisposition))
        {
            if (text == first) { value = (T)(object)AssessmentDisposition.Pass; return true; }
            if (text == second) { value = (T)(object)AssessmentDisposition.Fail; return true; }
        }
        else if (typeof(T) == typeof(AssessmentClassification))
        {
            if (text == first) { value = (T)(object)AssessmentClassification.Invalid; return true; }
            if (text == second) { value = (T)(object)AssessmentClassification.Incomplete; return true; }
        }
        return false;
    }

    private static bool TryStatus(string value, out VerifierResultStatus status)
    {
        switch (value)
        {
            case "complete_valid": status = VerifierResultStatus.CompleteValid; return true;
            case "invalid": status = VerifierResultStatus.Invalid; return true;
            case "incomplete": status = VerifierResultStatus.Incomplete; return true;
            default: status = default; return false;
        }
    }

    private static string StatusName(VerifierResultStatus status) => status switch
    {
        VerifierResultStatus.CompleteValid => "complete_valid",
        VerifierResultStatus.Invalid => "invalid",
        VerifierResultStatus.Incomplete => "incomplete",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static ParseResult<VerifierResult> Failure(string artifact, string field, string code) =>
        ParseResult<VerifierResult>.Failure(Problem(artifact, field, code));
    private static StructuralProblem Problem(string artifact, string field, string code) => new(null, code, artifact, null, field, null);
    private static bool Fail(out int value) { value = 0; return false; }
    private static bool Fail(out long value) { value = 0; return false; }

    private static long TruncateMilliseconds(decimal milliseconds)
    {
        decimal truncated = decimal.Truncate(milliseconds);
        if (truncated < long.MinValue || truncated > long.MaxValue)
            throw new ArgumentException("A result duration cannot be represented as an Int64 millisecond value.");
        return decimal.ToInt64(truncated);
    }

    private static HashSet<string> BuildOptionalRootFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal)
        {
            "generator_version", "runtime_versions", "start_line", "end_line", "drain_time", "drain_elapsed_at_stop", "drain_elapsed_at_stop_lower_bound",
            "broker_count", "configuration_fidelity", "probe_observation", "a01_recorded_evidence", "workload_observation"
        };
        fields.UnionWith(CountFields);
        return fields;
    }

    private sealed class BoundedMemoryStream(long maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacityFor(1);
            base.WriteByte(value);
        }

        private void EnsureCapacityFor(int count)
        {
            if (count < 0 || Length > maximumBytes - count)
                throw new InputLimitExceededException("The verifier result exceeds the configured output byte limit.");
        }
    }
}
