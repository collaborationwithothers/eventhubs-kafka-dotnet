using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Input;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class StrictParserTests
{
    [Theory]
    [MemberData(nameof(RequiredPropertyCases))]
    public void Given_RequiredPropertyMissing_When_Read_Then_RoutesProblemToOwningCheck(
        string artifact, string path, int owner, string expectedField)
    {
        byte[] input = Mutate(artifact, path, remove: true);
        IReadOnlyList<StructuralProblem> problems = Parse(artifact, input);
        Assert.Contains(problems, problem => problem.CheckNumber == (owner == 0 ? null : owner) && problem.Field == expectedField);
    }

    [Theory]
    [MemberData(nameof(RequiredPropertyCases))]
    public void Given_RequiredPropertyHasWrongJsonKind_When_Read_Then_RoutesProblemToOwningCheck(
        string artifact, string path, int owner, string expectedField)
    {
        byte[] input = Mutate(artifact, path, wrongKind: true);
        IReadOnlyList<StructuralProblem> problems = Parse(artifact, input);
        Assert.Contains(problems, problem => problem.CheckNumber == (owner == 0 ? null : owner) && problem.Field == expectedField);
    }

    [Fact]
    public void Given_MissingOrNullCompletionReceipt_When_CapabilitiesAreRead_Then_PreservesPresenceAndFalseValues()
    {
        JsonNode absent = JsonNode.Parse(ReadGolden("capabilities"))!;
        absent.AsObject().Remove("statistics_completion");
        ParseResult<CapturedCapabilities> missing = CapturedEvidenceReader.ReadCapabilities(
            Serialize(absent), "capabilities.json", Limits(), new InputBudget(Limits()));
        Assert.True(missing.IsSuccess);
        Assert.Equal(FieldPresence.Missing, missing.Value!.StatisticsCompletion.Presence);

        JsonNode explicitNull = JsonNode.Parse(ReadGolden("capabilities"))!;
        explicitNull["statistics_completion"] = null;
        ParseResult<CapturedCapabilities> nulled = CapturedEvidenceReader.ReadCapabilities(
            Serialize(explicitNull), "capabilities.json", Limits(), new InputBudget(Limits()));
        Assert.True(nulled.IsSuccess);
        Assert.Equal(FieldPresence.Null, nulled.Value!.StatisticsCompletion.Presence);

        JsonNode completionMissingBoundary = JsonNode.Parse(ReadGolden("capabilities"))!;
        completionMissingBoundary["statistics_completion"]!.AsObject().Remove("drain_boundary_microseconds");
        ParseResult<CapturedCapabilities> boundaryMissing = CapturedEvidenceReader.ReadCapabilities(
            Serialize(completionMissingBoundary), "capabilities.json", Limits(), new InputBudget(Limits()));
        Assert.True(boundaryMissing.IsSuccess);
        Assert.Equal(FieldPresence.Missing, boundaryMissing.Value!.StatisticsCompletion.Value!.DrainBoundaryMicroseconds.Presence);

        JsonNode completionNullBoundary = JsonNode.Parse(ReadGolden("capabilities"))!;
        completionNullBoundary["statistics_completion"]!["drain_boundary_microseconds"] = null;
        completionNullBoundary["statistics_completion"]!["is_complete"] = false;
        ParseResult<CapturedCapabilities> boundaryNull = CapturedEvidenceReader.ReadCapabilities(
            Serialize(completionNullBoundary), "capabilities.json", Limits(), new InputBudget(Limits()));
        Assert.True(boundaryNull.IsSuccess);
        Assert.Equal(FieldPresence.Null, boundaryNull.Value!.StatisticsCompletion.Value!.DrainBoundaryMicroseconds.Presence);
        Assert.False(boundaryNull.Value.StatisticsCompletion.Value.IsComplete);

        foreach (long boundary in new[] { -1L, long.MinValue, long.MaxValue })
        {
            JsonNode boundaryValue = JsonNode.Parse(ReadGolden("capabilities"))!;
            boundaryValue["statistics_completion"]!["drain_boundary_microseconds"] = boundary;
            ParseResult<CapturedCapabilities> parsed = CapturedEvidenceReader.ReadCapabilities(
                Serialize(boundaryValue), "capabilities.json", Limits(), new InputBudget(Limits()));
            Assert.True(parsed.IsSuccess);
            Assert.Equal(boundary, parsed.Value!.StatisticsCompletion.Value!.DrainBoundaryMicroseconds.Value);
        }
    }

    [Fact]
    public void Given_NullCallbackPersistenceAndNullableLifecycleReason_When_Read_Then_PreservesNullWithoutValidityDecision()
    {
        JsonNode ledger = JsonNode.Parse(FirstJsonLine(ReadGolden("ledger")))!;
        ledger["persistence_status"] = null;
        ledger["delivered"] = false;
        ParseResult<IReadOnlyList<LedgerRow>> ledgerResult = LedgerReader.ReadRows(
            JsonLine(Serialize(ledger)), Limits(), new InputBudget(Limits()));
        Assert.True(ledgerResult.IsSuccess);
        Assert.False(ledgerResult.Value![0].Delivered);
        Assert.Equal(FieldPresence.Null, ledgerResult.Value[0].PersistenceStatus.Presence);

        JsonNode error = JsonNode.Parse(SecondJsonLine(ReadGolden("lifecycle")))!;
        error["reason"] = null;
        ParseResult<IReadOnlyList<LifecycleEvent>> errorResult = CapturedEvidenceReader.ReadLifecycle(
            JsonLine(Serialize(error)), "request-lifecycle.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(errorResult.IsSuccess);
        Assert.Equal(FieldPresence.Null, Assert.IsType<NativeError>(errorResult.Value![0]).Reason.Presence);
    }

    [Fact]
    public void Given_OnlyHarnessOwnedScalarValues_When_Read_Then_UsesExactWriterSetsAndKeepsOrdinaryStringsFree()
    {
        foreach (string completionSource in new[] { "callback", "synchronous_rejection" })
        {
            JsonNode ledger = JsonNode.Parse(FirstJsonLine(ReadGolden("ledger")))!;
            ledger["completion_source"] = completionSource;
            ledger["identity"]!["scenario_id"] = "";
            ParseResult<IReadOnlyList<LedgerRow>> result = LedgerReader.ReadRows(
                JsonLine(Serialize(ledger)), Limits(), new InputBudget(Limits()));
            Assert.True(result.IsSuccess);
            Assert.Equal(completionSource, result.Value![0].CompletionSource);
            Assert.Equal("", result.Value[0].Identity.ScenarioId);
        }

        foreach (string provenance in new[] { "native_callback", "missing", "pre_enqueue" })
        {
            JsonNode ledger = JsonNode.Parse(FirstJsonLine(ReadGolden("ledger")))!;
            ledger["persistence_provenance"] = provenance;
            Assert.True(LedgerReader.ReadRows(JsonLine(Serialize(ledger)), Limits(), new InputBudget(Limits())).IsSuccess);
        }

        foreach (string workerStatus in new[] { "complete", "incomplete", "failed" })
        {
            JsonNode manifest = JsonNode.Parse(ReadGolden("manifest"))!;
            manifest["worker_status"] = workerStatus;
            Assert.True(ManifestReader.Read(Serialize(manifest), Limits(), new InputBudget(Limits()), 2).IsSuccess);
        }

        JsonNode manifestWithMapKeys = JsonNode.Parse(ReadGolden("manifest"))!;
        manifestWithMapKeys["runtime_versions"]!["future-client"] = "";
        manifestWithMapKeys["evidence_files"]!["future-evidence"] = "uninterpreted";
        Assert.True(ManifestReader.Read(Serialize(manifestWithMapKeys), Limits(), new InputBudget(Limits()), 2).IsSuccess);

        JsonNode manifestWithNegativeTimes = JsonNode.Parse(ReadGolden("manifest"))!;
        manifestWithNegativeTimes["timestamp_frequency"] = -1;
        manifestWithNegativeTimes["start_timestamp"] = long.MinValue;
        Assert.True(ManifestReader.Read(Serialize(manifestWithNegativeTimes), Limits(), new InputBudget(Limits()), 2).IsSuccess);

        JsonNode index = JsonNode.Parse(ReadGolden("index"))!;
        index["artifacts"]![0]!["length"] = -1;
        index["artifacts"]![0]!["sha256"] = "not-a-digest-check";
        Assert.True(ArtifactIndexReader.Read(Serialize(index), Limits(), new InputBudget(Limits())).IsSuccess);

        JsonNode capabilities = WithCapabilitiesExtras();
        capabilities["resolved_configuration"]!["settings"]!["acks"]!["baseline"]!["role"] = int.MinValue;
        capabilities["resolved_configuration"]!["settings"]!["acks"]!["baseline"]!["disposition"] = int.MaxValue;
        Assert.True(CapturedEvidenceReader.ReadCapabilities(Serialize(capabilities), "capabilities.json", Limits(), new InputBudget(Limits())).IsSuccess);
    }

    [Fact]
    public void Given_ReorderedPropertiesAndUninterpretedEvidence_When_Read_Then_PreservesDataWithoutSemanticGates()
    {
        JsonNode reordered = ReverseProperties(JsonNode.Parse(FirstJsonLine(ReadGolden("ledger")))!);
        ParseResult<IReadOnlyList<LedgerRow>> reorderedLedger = LedgerReader.ReadRows(
            JsonLine(Serialize(reordered)), Limits(), new InputBudget(Limits()));
        Assert.True(reorderedLedger.IsSuccess);
        Assert.Equal("golden-run-001", reorderedLedger.Value![0].Identity.RunId);
        Assert.Equal("golden-attempt-000", reorderedLedger.Value[0].Attempt.AttemptId);
        JsonNode reorderedManifest = ReverseProperties(JsonNode.Parse(ReadGolden("manifest"))!);
        ParseResult<SealedRunManifest> manifestProjection = ManifestReader.Read(Serialize(reorderedManifest), Limits(), new InputBudget(Limits()), 2);
        Assert.True(manifestProjection.IsSuccess);
        Assert.Equal("golden-run-001", manifestProjection.Value!.Identity.RunId);
        Assert.Equal(2L, manifestProjection.Value.AttemptCount);

        JsonNode statistics = JsonNode.Parse(SecondJsonLine(ReadGolden("statistics")))!;
        statistics["statistics_identity"]!["role"] = int.MaxValue;
        statistics["statistics_identity"]!["client_id"] = "independent-client-id";
        statistics["typed_snapshot"]!["brokers"]![0]!["throttle"]!["count"] = 999;
        statistics["typed_snapshot"]!["brokers"]![0]!["throttle"]!["value"] = null;
        ParseResult<IReadOnlyList<StatisticsEnvelope>> typed = CapturedEvidenceReader.ReadStatistics(
            JsonLine(Serialize(statistics)), "statistics.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(typed.IsSuccess);
        Assert.Equal(int.MaxValue, typed.Value![0].StatisticsIdentity.Role);
        Assert.Equal("independent-client-id", typed.Value[0].StatisticsIdentity.ClientId);
        Assert.Equal(999L, typed.Value[0].TypedSnapshot.Value!.Brokers[0].Throttle.Count);
        Assert.Equal(FieldPresence.Null, typed.Value[0].TypedSnapshot.Value!.Brokers[0].Throttle.Value.Presence);
        Assert.NotNull(typed.Value[0].RawJson);
        ParseResult<NativeClientReport> independentRaw = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(typed.Value[0].RawJson),
            "statistics.jsonl", Limits(), new InputBudget(Limits()), 65_536, 20);
        Assert.NotNull(independentRaw.Value);
        Assert.NotNull(independentRaw.Value.Brokers.Value);
        Assert.Equal(17L, independentRaw.Value!.Brokers.Value!["broker-a/1"].Throttle.Value!.Maximum.Value);

        JsonNode typedThrottleValue = JsonNode.Parse(SecondJsonLine(ReadGolden("statistics")))!;
        typedThrottleValue["typed_snapshot"]!["brokers"]![0]!["throttle"]!["value"] = 999;
        ParseResult<IReadOnlyList<StatisticsEnvelope>> typedValue = CapturedEvidenceReader.ReadStatistics(
            JsonLine(Serialize(typedThrottleValue)), "statistics.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(typedValue.IsSuccess);
        Assert.Equal(999L, typedValue.Value![0].TypedSnapshot.Value!.Brokers[0].Throttle.Value.Value);
        ParseResult<NativeClientReport> rawWithTypedValue = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(typedValue.Value[0].RawJson),
            "statistics.jsonl", Limits(), new InputBudget(Limits()), 65_536, 20);
        Assert.Equal(17L, rawWithTypedValue.Value!.Brokers.Value!["broker-a/1"].Throttle.Value!.Maximum.Value);

        JsonNode typedSnapshotNull = JsonNode.Parse(SecondJsonLine(ReadGolden("statistics")))!;
        typedSnapshotNull["typed_snapshot"] = null;
        ParseResult<IReadOnlyList<StatisticsEnvelope>> nullTyped = CapturedEvidenceReader.ReadStatistics(
            JsonLine(Serialize(typedSnapshotNull)), "statistics.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(nullTyped.IsSuccess);
        Assert.Equal(FieldPresence.Null, nullTyped.Value![0].TypedSnapshot.Presence);
        ParseResult<NativeClientReport> rawFromNullTyped = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(nullTyped.Value[0].RawJson),
            "statistics.jsonl", Limits(), new InputBudget(Limits()), 65_536, 20);
        Assert.Equal(17L, rawFromNullTyped.Value!.Brokers.Value!["broker-a/1"].Throttle.Value!.Maximum.Value);

        JsonNode manifest = JsonNode.Parse(ReadGolden("manifest"))!;
        manifest["guard_observations"]!.AsArray().Add("future-guard-observation");
        Assert.Contains("future-guard-observation", ManifestReader.Read(Serialize(manifest), Limits(), new InputBudget(Limits()), 2).Value!.GuardObservations);
        JsonNode index = JsonNode.Parse(ReadGolden("index"))!;
        index["worker_complete"] = false;
        index["publication_complete"] = false;
        Assert.False(ArtifactIndexReader.Read(Serialize(index), Limits(), new InputBudget(Limits())).Value!.WorkerComplete);
        Assert.False(ArtifactIndexReader.Read(Serialize(index), Limits(), new InputBudget(Limits())).Value!.PublicationComplete);

        byte[] first = JsonLine(Encoding.UTF8.GetBytes(FirstJsonLine(ReadGolden("lifecycle"))));
        byte[] duplicateRows = first.Concat(first).ToArray();
        ParseResult<IReadOnlyList<LifecycleEvent>> duplicated = CapturedEvidenceReader.ReadLifecycle(
            duplicateRows, "request-lifecycle.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(duplicated.IsSuccess);
        Assert.Equal(2, duplicated.Value!.Count);
        byte[] ledger = ReadGolden("ledger");
        string[] ledgerLines = Encoding.UTF8.GetString(ledger).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        ParseResult<IReadOnlyList<LedgerRow>> duplicateAttempts = LedgerReader.ReadRows(
            JsonLine(Encoding.UTF8.GetBytes(ledgerLines[0] + "\n" + ledgerLines[0])), Limits(), new InputBudget(Limits()));
        Assert.True(duplicateAttempts.IsSuccess);
        Assert.Equal(2, duplicateAttempts.Value!.Count);

        JsonNode faulted = JsonNode.Parse(ReadGolden("capabilities"))!;
        faulted["is_faulted"] = true;
        Assert.True(CapturedEvidenceReader.ReadCapabilities(Serialize(faulted), "capabilities.json", Limits(), new InputBudget(Limits())).Value!.IsFaulted);

        JsonNode syntheticGap = JsonNode.Parse(FirstJsonLine(ReadGolden("statistics")))!;
        syntheticGap["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "synthetic_gap", ["broker"] = null, ["field"] = null });
        ParseResult<IReadOnlyList<StatisticsEnvelope>> gap = CapturedEvidenceReader.ReadStatistics(
            JsonLine(Serialize(syntheticGap)), "statistics.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(gap.IsSuccess);
        Assert.Equal("synthetic_gap", gap.Value![0].Diagnostics[0].Code);
        Assert.Equal(FieldPresence.Null, gap.Value[0].Diagnostics[0].Broker.Presence);

        ParseResult<NativeClientReport> ten = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes("{\"ts\":10,\"age\":1,\"brokers\":{}}"), "statistics.jsonl", Limits(), new InputBudget(Limits()), 4096, 1);
        ParseResult<NativeClientReport> nine = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes("{\"ts\":9,\"age\":1,\"brokers\":{}}"), "statistics.jsonl", Limits(), new InputBudget(Limits()), 4096, 1);
        Assert.Equal(10L, ten.Value!.TimestampMicroseconds.Value);
        Assert.Equal(9L, nine.Value!.TimestampMicroseconds.Value);
    }

    [Fact]
    public void Given_UnsupportedHarnessScalarsOrSchema_When_Read_Then_RoutesStructuralProblem()
    {
        foreach ((string artifact, string path, JsonNode value, string field) in new[]
        {
            ("ledger", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("manifest", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("index", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("statistics", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("lifecycle", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("capabilities", "schema_version", JsonValue.Create(2)!, "schema_version"),
            ("ledger", "completion_source", JsonValue.Create("future-source")!, "completion_source"),
            ("ledger", "persistence_provenance", JsonValue.Create("future-provenance")!, "persistence_provenance"),
            ("manifest", "worker_status", JsonValue.Create("future-status")!, "worker_status"),
            ("index", "worker_status", JsonValue.Create("incomplete")!, "worker_status")
        })
        {
            byte[] input = MutateTo(artifact, path, value);
            IReadOnlyList<StructuralProblem> problems = Parse(artifact, input);
            int? owner = artifact switch { "ledger" => 1, "manifest" => 2, "index" => null, _ => 17 };
            Assert.Contains(problems, p => p.CheckNumber == owner && p.Field == field);
        }

        foreach (string value in new[] { "\"1\"", "1.5", "true", "{}", "9223372036854775808" })
        {
            JsonNode replacement = JsonNode.Parse("{\"value\":" + value + "}")!["value"]!.DeepClone();
            byte[] input = MutateTo("ledger", "attempt.planned_microseconds", replacement);
            Assert.Contains(Parse("ledger", input), p => p.CheckNumber == 1 && p.Field == "attempt.planned_microseconds");
            JsonNode completion = JsonNode.Parse(ReadGolden("capabilities"))!;
            string original = completion["statistics_completion"]!["drain_boundary_microseconds"]!.ToJsonString();
            string changed = Encoding.UTF8.GetString(Serialize(completion)).Replace(original, value, StringComparison.Ordinal);
            Assert.Contains(CapturedEvidenceReader.ReadCapabilities(Encoding.UTF8.GetBytes(changed), "capabilities.json", Limits(), new InputBudget(Limits())).Problems,
                p => p.CheckNumber == 17 && p.Field == "statistics_completion.drain_boundary_microseconds");
        }
    }

    [Theory]
    [MemberData(nameof(StartLineRequiredPropertyCases))]
    public void Given_StartLineRequiredPropertyInvalid_When_Read_Then_RoutesProblemToCheckSeven(string path, bool wrongKind)
    {
        JsonNode root = JsonNode.Parse(StartLineFixture)!;
        JsonNode? current = Navigate(root, path, out JsonNode? parent, out string property);
        if (wrongKind) Assign(parent, property, current is JsonObject or JsonArray ? JsonValue.Create("wrong-kind")! : new JsonObject());
        else if (parent is JsonObject obj) obj.Remove(property);
        else if (parent is JsonArray array) array.RemoveAt(int.Parse(property, System.Globalization.CultureInfo.InvariantCulture));
        ParseResult<Parchwell.Verifier.Common.Window.StartLineNote> result = Parchwell.Verifier.Common.Window.StartLineNoteCodec.Read(
            Serialize(root), "start-line-note.json", Limits(), 4);
        Assert.Contains(result.Problems, p => p.CheckNumber == 7 && p.Field == path);
    }

    public static IEnumerable<object[]> StartLineRequiredPropertyCases()
    {
        foreach (string field in new[] { "schema_version", "identity", "topic", "partition_highs", "captured_at", "verifier_commit", "confluent_kafka_version", "librdkafka_version" })
            foreach (bool wrongKind in new[] { false, true }) yield return [field, wrongKind];
        foreach (string field in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            foreach (bool wrongKind in new[] { false, true }) yield return ["identity." + field, wrongKind];
        foreach (string field in new[] { "partition", "high" })
            foreach (bool wrongKind in new[] { false, true }) yield return ["partition_highs[0]." + field, wrongKind];
    }

    private const string StartLineFixture = "{\"schema_version\":1,\"identity\":{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"R0\",\"instance_id\":\"I0\",\"expected_instance_id\":\"I0\",\"client_id\":\"L0\"},\"topic\":\"topic-a\",\"partition_highs\":[{\"partition\":0,\"high\":10}],\"captured_at\":\"2026-10-18T00:00:00Z\",\"verifier_commit\":\"abc\",\"confluent_kafka_version\":\"2.16.0\",\"librdkafka_version\":\"2.16.0\"}";

    [Fact]
    public void Given_UnknownHarnessField_When_EnvelopeIsRead_Then_RejectsItAtEachClosedShape()
    {
        foreach ((string artifact, string path, int check, string field) in new[]
        {
            ("ledger", "unexpected", 1, "unexpected"),
            ("ledger", "identity.future", 1, "identity.future"),
            ("manifest", "inventory[0].future", 2, "inventory[0].future"),
            ("index", "artifacts[0].future", 0, "artifacts[0].future"),
            ("statistics", "typed_snapshot.brokers[0].future", 17, "typed_snapshot.brokers[0].future"),
            ("lifecycle", "future", 17, "future"),
            ("lifecycle-error", "future", 17, "future"),
            ("capabilities", "resolved_configuration.settings.acks.baseline.future", 17, "resolved_configuration.settings.acks.baseline.future")
        })
        {
            byte[] input = AddProperty(artifact, path, JsonValue.Create("free string"));
            IReadOnlyList<StructuralProblem> problems = Parse(artifact, input);
            Assert.Contains(problems, p => p.CheckNumber == (check == 0 ? null : check) && p.Field == field);
        }

        JsonNode manifestDataMap = JsonNode.Parse(ReadGolden("manifest"))!;
        manifestDataMap["runtime_versions"]!["future.runtime.key"] = "allowed";
        Assert.True(ManifestReader.Read(Serialize(manifestDataMap), Limits(), new InputBudget(Limits()), 2).IsSuccess);
        JsonNode resolvedSettingMap = JsonNode.Parse(ReadGolden("capabilities"))!;
        resolvedSettingMap["resolved_configuration"]!["settings"]!["future.setting.key"] = resolvedSettingMap["resolved_configuration"]!["settings"]!["acks"]!.DeepClone();
        Assert.True(CapturedEvidenceReader.ReadCapabilities(Serialize(resolvedSettingMap), "capabilities.json", Limits(), new InputBudget(Limits())).IsSuccess);

        var distinctClosedParents = RequiredPropertyCases()
            .Select(row => (Artifact: (string)row[0], Parent: ParentPath((string)row[1]), Check: (int)row[2]))
            .Distinct()
            .ToArray();
        foreach ((string artifact, string parentPath, int owner) in distinctClosedParents)
        {
            JsonNode root = JsonNode.Parse(ReadArtifactJson(artifact))!;
            AddSyntheticNodes(artifact, root);
            JsonNode? parent = parentPath == "$" ? root : Navigate(root, parentPath, out _, out _);
            Assert.IsType<JsonObject>(parent).Add("future_parent_probe_7f4b", JsonValue.Create("free"));
            byte[] input = EncodeInput(artifact, Encoding.UTF8.GetString(Serialize(root)));
            string expected = parentPath == "$" ? "future_parent_probe_7f4b" : parentPath + ".future_parent_probe_7f4b";
            Assert.Contains(Parse(artifact, input), p => p.CheckNumber == (owner == 0 ? null : owner) && p.Field == expected);
        }

        JsonNode noteUnknown = JsonNode.Parse(StartLineFixture)!;
        noteUnknown["partition_highs"]![0]!["future"] = 1;
        Assert.Contains(Parchwell.Verifier.Common.Window.StartLineNoteCodec.Read(Serialize(noteUnknown), "start-line-note.json", Limits(), 4).Problems,
            p => p.CheckNumber == 7 && p.Field == "partition_highs[0].future");
        JsonNode noteSchema = JsonNode.Parse(StartLineFixture)!;
        noteSchema["schema_version"] = 2;
        Assert.Contains(Parchwell.Verifier.Common.Window.StartLineNoteCodec.Read(Serialize(noteSchema), "start-line-note.json", Limits(), 4).Problems,
            p => p.CheckNumber == 7 && p.Field == "schema_version" && p.Code == "unsupported_schema_version");
    }

    [Theory]
    [MemberData(nameof(StartLineContainerCases))]
    public void Given_StartLineContainerHasUnknownOrDuplicateProperty_When_Read_Then_RoutesToCheckSeven(string path, bool duplicate)
    {
        JsonNode root = JsonNode.Parse(StartLineFixture)!;
        JsonNode? parent = path == "$" ? root : Navigate(root, path, out _, out _);
        JsonObject target = Assert.IsType<JsonObject>(parent);
        const string field = "future_note_field_7f4b";
        target.Add(field, JsonValue.Create("unconstrained"));
        if (duplicate)
        {
            target.Add("__note_duplicate_sentinel_7f4b__", JsonValue.Create("unconstrained"));
            string source = Encoding.UTF8.GetString(Serialize(root));
            const string sentinel = "\"__note_duplicate_sentinel_7f4b__\":";
            int position = source.IndexOf(sentinel, StringComparison.Ordinal);
            Assert.True(position >= 0);
            Assert.Equal(position, source.LastIndexOf(sentinel, StringComparison.Ordinal));
            source = source.Remove(position, sentinel.Length).Insert(position, $"\"{field}\":");
            string expected = path == "$" ? field : path + "." + field;
            IReadOnlyList<StructuralProblem> problems = Parchwell.Verifier.Common.Window.StartLineNoteCodec.Read(
                Encoding.UTF8.GetBytes(source), "start-line-note.json", Limits(), 4).Problems;
            Assert.Contains(problems, problem => problem.CheckNumber == 7 && problem.Code == "duplicate_property" && problem.Field == expected);
        }
        else
        {
            IReadOnlyList<StructuralProblem> problems = Parchwell.Verifier.Common.Window.StartLineNoteCodec.Read(
                Serialize(root), "start-line-note.json", Limits(), 4).Problems;
            string expected = path == "$" ? field : path + "." + field;
            Assert.Contains(problems, problem => problem.CheckNumber == 7 && problem.Field == expected);
        }
    }

    public static IEnumerable<object[]> StartLineContainerCases()
    {
        foreach (string path in new[] { "$", "identity", "partition_highs[0]" })
            foreach (bool duplicate in new[] { false, true }) yield return [path, duplicate];
    }

    private static string ParentPath(string path)
    {
        int lastDot = path.LastIndexOf('.');
        return lastDot < 0 ? "$" : path[..lastDot];
    }

    [Theory]
    [MemberData(nameof(RequiredPropertyCases))]
    public void Given_DuplicatePropertyAtNestedDepth_When_Read_Then_ReportsDuplicateToOwner(string artifact, string path, int check, string field)
    {
        JsonNode root = JsonNode.Parse(ReadArtifactJson(artifact))!;
        AddSyntheticNodes(artifact, root);
        JsonNode? original = Navigate(root, path, out JsonNode? parent, out string property);
        Assert.True(ContainsProperty(parent!, property));
        JsonObject exactParent = Assert.IsType<JsonObject>(parent);
        const string sentinel = "__duplicate_test_sentinel_7f4b__";
        exactParent.Add(sentinel, original?.DeepClone());
        string source = Encoding.UTF8.GetString(Serialize(root));
        string sentinelToken = $"\"{sentinel}\":";
        int sentinelIndex = source.IndexOf(sentinelToken, StringComparison.Ordinal);
        Assert.True(sentinelIndex >= 0);
        Assert.Equal(sentinelIndex, source.LastIndexOf(sentinelToken, StringComparison.Ordinal));
        string duplicate = source.Remove(sentinelIndex, sentinelToken.Length).Insert(sentinelIndex, $"\"{property}\":");
        IReadOnlyList<StructuralProblem> problems = Parse(artifact, EncodeInput(artifact, duplicate));
        Assert.Contains(problems, p => p.CheckNumber == (check == 0 ? null : check) && p.Code == "duplicate_property" && p.Field == field);
    }

    [Fact]
    public void Given_InvalidUtf8TruncatedOrBadJsonlFraming_When_Read_Then_RoutesEnvelopeProblem()
    {
        byte[][] malformed =
        [
            [0xc3, 0x28, 0x0a],
            Encoding.UTF8.GetBytes("{\"schema_version\":"),
            Encoding.UTF8.GetBytes("\n"),
            Encoding.UTF8.GetBytes("{\"schema_version\":1}\n{\"schema_version\":1"),
            Encoding.UTF8.GetBytes("{\"schema_version\":1,}\n"),
            Encoding.UTF8.GetBytes("//comment\n")
        ];
        foreach (byte[] bytes in malformed)
        {
            IReadOnlyList<StructuralProblem> problems = Parse("ledger", bytes);
            Assert.NotEmpty(problems);
            Assert.All(problems, p => Assert.Equal(1, p.CheckNumber));
        }

        byte[] extraRoot = Encoding.UTF8.GetBytes(FirstJsonLine(ReadGolden("ledger")) + "{}\n");
        Assert.Contains(Parse("ledger", extraRoot), p => p.CheckNumber == 1 && p.Code == "malformed_json");
        Assert.True(ManifestReader.Read(ReadGolden("manifest"), Limits(), new InputBudget(Limits()), 2).IsSuccess);
        byte[] manifestWithLf = ReadGolden("manifest").Concat(new byte[] { (byte)'\n' }).ToArray();
        Assert.True(ManifestReader.Read(manifestWithLf, Limits(), new InputBudget(Limits()), 2).IsSuccess);
    }

    [Fact]
    public void Given_NativeRawReportHasIndependentShape_When_Read_Then_AllowsExtraFieldsAndRoutesNativeProblems()
    {
        const string report = "{\"ordinary_top\":true,\"ts\":-1,\"age\":long.MaxValue,\"time\":1,\"brokers\":{\"dictionary-key\":{\"name\":\"different-name\",\"nodeid\":2147483648,\"source\":\"free-source\",\"state\":\"free-state\",\"req_timeouts\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"disconnects\":0,\"throttle\":{\"cnt\":0,\"max\":0,\"p99\":{}},\"rtt\":{\"cnt\":0,\"p99\":0,\"max\":{}},\"extra_broker\":\"accepted\"}}}";
        string literal = report.Replace("long.MaxValue", long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        InputLimits limits = Limits();
        ParseResult<NativeClientReport> parsed = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(literal), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(-1L, parsed.Value!.TimestampMicroseconds.Value);
        Assert.Equal(long.MaxValue, parsed.Value.AgeMicroseconds.Value);
        Assert.Equal(2_147_483_648L, parsed.Value.Brokers.Value!["dictionary-key"].NodeId.Value);
        Assert.Equal("dictionary-key", parsed.Value.Brokers.Value["dictionary-key"].DictionaryKey);
        Assert.Equal("different-name", parsed.Value.Brokers.Value["dictionary-key"].Name.Value);
        Assert.Equal("free-source", parsed.Value.Brokers.Value["dictionary-key"].Source.Value);

        string optionalTopFieldsAbsent = "{\"ts\":1,\"age\":2,\"brokers\":{}}";
        Assert.True(NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(optionalTopFieldsAbsent), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4).IsSuccess);

        string missingBrokerField = "{\"ts\":1,\"age\":2,\"brokers\":{\"b\":{}}}";
        ParseResult<NativeClientReport> missing = NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(missingBrokerField), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4);
        Assert.NotNull(missing.Value);
        Assert.Contains(missing.Problems, p => p.CheckNumber == 21 && p.Field == "brokers.b.name");
        Assert.Contains(missing.Problems, p => p.CheckNumber == 21 && p.Field == "brokers.b.throttle.cnt");
        Assert.Contains(missing.Problems, p => p.CheckNumber == 21 && p.Field == "brokers.b.throttle.max");
        Assert.Contains(missing.Problems, p => p.CheckNumber == 21 && p.Field == "brokers.b.rtt.p99");
        NativeBrokerReport missingBroker = missing.Value!.Brokers.Value!["b"];
        Assert.Equal(FieldPresence.Missing, missingBroker.Name.Presence);
        Assert.Equal(FieldPresence.Missing, missingBroker.NodeId.Presence);
        Assert.Equal(FieldPresence.Missing, missingBroker.Throttle.Presence);
        Assert.Equal(FieldPresence.Missing, missingBroker.RoundTripTime.Presence);

        foreach (string path in new[]
        {
            "name", "nodeid", "source", "state", "req_timeouts", "txretries", "txerrs", "rxerrs", "disconnects",
            "throttle.cnt", "throttle.max", "rtt.cnt", "rtt.p99"
        })
        {
            JsonNode variant = JsonNode.Parse(ValidNativeReport())!;
            RemoveNativePath(variant["brokers"]!["b"]!, path);
            ParseResult<NativeClientReport> partial = NativeStatisticsReader.Read(
                Encoding.UTF8.GetBytes(variant.ToJsonString()), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4);
            Assert.NotNull(partial.Value);
            Assert.Contains(partial.Problems, p => p.CheckNumber == 21 && p.Field == "brokers.b." + path);
        }

        foreach (string field in new[] { "ts", "age", "brokers" })
        {
            JsonNode variant = JsonNode.Parse(ValidNativeReport())!;
            variant.AsObject().Remove(field);
            ParseResult<NativeClientReport> absent = NativeStatisticsReader.Read(
                Encoding.UTF8.GetBytes(variant.ToJsonString()), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4);
            Assert.Null(absent.Value);
            Assert.Contains(absent.Problems, p => p.CheckNumber == 20 && p.Field == field);
        }

        foreach (string path in new[] { "ts", "age", "brokers.b.nodeid", "brokers.b.throttle.max", "brokers.b.rtt.p99" })
        {
            JsonNode variant = JsonNode.Parse(ValidNativeReport())!;
            SetNativeWrongKind(variant, path);
            ParseResult<NativeClientReport> wrong = NativeStatisticsReader.Read(
                Encoding.UTF8.GetBytes(variant.ToJsonString()), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4);
            Assert.Contains(wrong.Problems, p => p.CheckNumber == 20 && p.Field == path);
        }

        string wrongBrokerKind = "{\"ts\":1,\"age\":2,\"brokers\":{\"b\":{\"nodeid\":\"1\"}}}";
        Assert.Contains(NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(wrongBrokerKind), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4).Problems,
            p => p.CheckNumber == 20 && p.Field == "brokers.b.nodeid");

        foreach (string invalid in new[] { "{", "{\"ts\":\"1\",\"age\":2,\"brokers\":{}}", "{\"ts\":1,\"age\":2}", "{\"ts\":1,\"ts\":2,\"age\":3,\"brokers\":{}}" })
        {
            Assert.Contains(NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(invalid), "statistics.jsonl", limits, new InputBudget(limits), 4096, 4).Problems,
                p => p.CheckNumber == 20);
        }
    }

    [Fact]
    public void Given_MalformedRawJsonInValidStatisticsEnvelope_When_Read_Then_EnvelopeAndNativeProblemsStaySeparate()
    {
        JsonNode stats = JsonNode.Parse(FirstJsonLine(ReadGolden("statistics")))!;
        stats["raw_json"] = "{";
        ParseResult<IReadOnlyList<StatisticsEnvelope>> envelope = CapturedEvidenceReader.ReadStatistics(
            JsonLine(Serialize(stats)), "statistics.jsonl", Limits(), new InputBudget(Limits()));
        Assert.True(envelope.IsSuccess);
        ParseResult<NativeClientReport> native = NativeStatisticsReader.Read(
            Encoding.UTF8.GetBytes(envelope.Value![0].RawJson), "statistics.jsonl", Limits(), new InputBudget(Limits()), 4096, 4);
        Assert.Contains(native.Problems, p => p.CheckNumber == 20);
    }

    [Fact]
    public void Given_InputExceedsConfiguredLimits_When_Read_Then_ThrowsExecutionLimitWithoutCheckRouting()
    {
        byte[] ledger = ReadGolden("ledger");
        Assert.Equal(1467, ledger.Length);
        InputLimits exactArtifact = new(64, ledger.Length, 10_000, 50_000, 2, 100_000, 2048);
        Assert.True(LedgerReader.ReadRows(ledger, exactArtifact, new InputBudget(exactArtifact)).IsSuccess);
        InputLimits exactEntries = exactArtifact with { MaxEntries = 28 };
        Assert.True(LedgerReader.ReadRows(ledger, exactEntries, new InputBudget(exactEntries)).IsSuccess);
        InputLimits oneEntry = exactEntries with { MaxEntries = 27 };
        Assert.Throws<InputLimitExceededException>(() => LedgerReader.ReadRows(ledger, oneEntry, new InputBudget(oneEntry)));
        InputLimits overArtifact = exactArtifact with { MaxArtifactBytes = 1467 };
        byte[] ledgerAt1468Bytes = ledger.Concat(new byte[] { (byte)'\n' }).ToArray();
        Assert.Throws<InputLimitExceededException>(() => LedgerReader.ReadRows(ledgerAt1468Bytes, overArtifact, new InputBudget(overArtifact)));

        string[] ledgerRows = Encoding.UTF8.GetString(ledger).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        byte[] threeLedgerRows = JsonLine(Encoding.UTF8.GetBytes(string.Join("\n", ledgerRows.Concat(new[] { ledgerRows[0] }))));
        Assert.Equal(2, LedgerReader.ReadRows(ledger, exactArtifact, new InputBudget(exactArtifact)).Value!.Count);
        InputLimits rowCountLimits = exactArtifact with { MaxArtifactBytes = 10_000 };
        Assert.Throws<InputLimitExceededException>(() => LedgerReader.ReadRows(threeLedgerRows, rowCountLimits, new InputBudget(rowCountLimits)));

        int firstNewline = Array.IndexOf(ledger, (byte)'\n');
        int secondNewline = Array.IndexOf(ledger, (byte)'\n', firstNewline + 1);
        int firstRowBytes = firstNewline;
        int secondRowBytes = secondNewline - firstNewline - 1;
        int maxRowBytes = Math.Max(firstRowBytes, secondRowBytes);
        InputLimits exactRowBytes = exactArtifact with { MaxRowBytes = maxRowBytes };
        Assert.True(LedgerReader.ReadRows(ledger, exactRowBytes, new InputBudget(exactRowBytes)).IsSuccess);
        string firstLine = Encoding.UTF8.GetString(ledger, 0, firstRowBytes);
        string secondLine = Encoding.UTF8.GetString(ledger, firstNewline + 1, secondRowBytes);
        byte[] oneByteOverRow = JsonLine(Encoding.UTF8.GetBytes(firstLine + new string(' ', maxRowBytes - firstRowBytes + 1) + "\n" + secondLine));
        InputLimits overRowBytes = exactRowBytes with { MaxArtifactBytes = 10_000, MaxRowBytes = maxRowBytes };
        Assert.Throws<InputLimitExceededException>(() => LedgerReader.ReadRows(oneByteOverRow, overRowBytes, new InputBudget(overRowBytes)));

        InputLimits inventoryLimits = Limits();
        Assert.Equal(2, ManifestReader.Read(ReadGolden("manifest"), inventoryLimits, new InputBudget(inventoryLimits), 2).Value!.Inventory.Count);
        JsonNode threeInventoryManifest = JsonNode.Parse(ReadGolden("manifest"))!;
        threeInventoryManifest["inventory"]!.AsArray().Add(threeInventoryManifest["inventory"]![0]!.DeepClone());
        Assert.Throws<InputLimitExceededException>(() => ManifestReader.Read(
            Serialize(threeInventoryManifest), inventoryLimits, new InputBudget(inventoryLimits), 2));
        Assert.Throws<InputLimitExceededException>(() => ManifestReader.Read(
            ReadGolden("manifest"), inventoryLimits, new InputBudget(inventoryLimits), 1));

        const string brokerValue = "{\"name\":\"b\",\"nodeid\":1,\"source\":\"learned\",\"state\":\"UP\",\"req_timeouts\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"disconnects\":0,\"throttle\":{\"cnt\":0,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}";
        string oneBrokerReport = "{\"ts\":1,\"age\":2,\"brokers\":{\"a\":" + brokerValue + "}}";
        string twoBrokerReport = "{\"ts\":1,\"age\":2,\"brokers\":{\"a\":" + brokerValue + ",\"b\":" + brokerValue + "}}";
        InputLimits brokerLimits = Limits();
        Assert.True(NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(oneBrokerReport), "statistics.jsonl", brokerLimits, new InputBudget(brokerLimits), 4096, 1).IsSuccess);
        Assert.Throws<InputLimitExceededException>(() => NativeStatisticsReader.Read(
            Encoding.UTF8.GetBytes(twoBrokerReport), "statistics.jsonl", brokerLimits, new InputBudget(brokerLimits), 4096, 1));

        InputLimits oneRowOnly = exactArtifact with { MaxRowsPerArtifact = 1 };
        Assert.Throws<InputLimitExceededException>(() => LedgerReader.ReadRows(ledger, oneRowOnly, new InputBudget(oneRowOnly)));
        InputLimits depthOne = Limits() with { MaxDepth = 1 };
        Assert.True(StrictJson.ParseDocument("{}"u8.ToArray(), "input", depthOne, 1).IsSuccess);
        Assert.Throws<InputLimitExceededException>(() => StrictJson.ParseDocument("{\"x\":{}}"u8.ToArray(), "input", depthOne, 1));
        InputLimits depthTwo = depthOne with { MaxDepth = 2 };
        Assert.True(StrictJson.ParseDocument("{\"x\":{}}"u8.ToArray(), "input", depthTwo, 1).IsSuccess);
        InputLimits depthFour = Limits() with { MaxDepth = 4 };
        Assert.True(StrictJson.ParseDocument("{\"a\":{\"b\":{\"c\":{}}}}"u8.ToArray(), "input", depthFour, 1).IsSuccess);
        Assert.Throws<InputLimitExceededException>(() => StrictJson.ParseDocument("{\"a\":{\"b\":{\"c\":{\"d\":{}}}}}"u8.ToArray(), "input", depthFour, 1));
        InputLimits aggregate = Limits() with { MaxTotalInputBytes = ledger.Length };
        var budget = new InputBudget(aggregate);
        Assert.True(LedgerReader.ReadRows(ledger, aggregate, budget).IsSuccess);
        Assert.Throws<InputLimitExceededException>(() => ArtifactIndexReader.Read(ReadGolden("index"), aggregate, budget));
        InputLimits rawLimit = Limits();
        byte[] raw = Encoding.UTF8.GetBytes("{\"ts\":1,\"age\":2,\"brokers\":{}}");
        Assert.True(NativeStatisticsReader.Read(raw, "statistics.jsonl", rawLimit, new InputBudget(rawLimit), raw.Length, 1).IsSuccess);
        Assert.Throws<InputLimitExceededException>(() => NativeStatisticsReader.Read(raw, "statistics.jsonl", rawLimit, new InputBudget(rawLimit), raw.Length - 1, 1));
    }

    [Fact]
    public void Given_MissingInputSource_When_Read_Then_RoutesAbsenceToItsOwner()
    {
        InputLimits limits = Limits();
        Assert.Contains(LedgerReader.ReadRows(null, limits, new InputBudget(limits)).Problems, p => p.CheckNumber == 1 && p.Code == "missing_input");
        Assert.Contains(ManifestReader.Read(null, limits, new InputBudget(limits), 2).Problems, p => p.CheckNumber == 2 && p.Code == "missing_input");
        Assert.Contains(ArtifactIndexReader.Read(null, limits, new InputBudget(limits)).Problems, p => p.CheckNumber is null && p.Code == "missing_input");
        Assert.Contains(CapturedEvidenceReader.ReadStatistics(null, "statistics.jsonl", limits, new InputBudget(limits)).Problems,
            p => p.CheckNumber == 17 && p.Code == "missing_input");
        Assert.Contains(CapturedEvidenceReader.ReadLifecycle(null, "request-lifecycle.jsonl", limits, new InputBudget(limits)).Problems,
            p => p.CheckNumber == 17 && p.Code == "missing_input");
        Assert.Contains(CapturedEvidenceReader.ReadCapabilities(null, "capabilities.json", limits, new InputBudget(limits)).Problems,
            p => p.CheckNumber == 17 && p.Code == "missing_input");
    }

    public static IEnumerable<object[]> RequiredPropertyCases()
    {
        foreach (string path in new[] { "schema_version", "identity", "attempt", "completion_source", "delivered", "error_code", "error_name", "persistence_status", "persistence_provenance", "completed_microseconds", "delivery_latency_ms", "synchronous_call_duration_ms" })
            yield return ["ledger", path, 1, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["ledger", "identity." + path, 1, "identity." + path];
        foreach (string path in AttemptFieldNames()) yield return ["ledger", "attempt." + path, 1, "attempt." + path];

        foreach (string path in new[] { "schema_version", "identity", "generator_version", "planned_attempt_count", "attempt_count", "inventory", "timestamp_frequency", "start_timestamp", "worker_status", "bookkeeping_complete", "first_unexecuted_ordinal", "diagnostics", "runtime_versions", "evidence_files", "guard_observations" })
            yield return ["manifest", path, 2, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["manifest", "identity." + path, 2, "identity." + path];
        foreach (string path in AttemptFieldNames()) yield return ["manifest", "inventory[0]." + path, 2, "inventory[0]." + path];

        foreach (string path in new[] { "schema_version", "identity", "worker_status", "bookkeeping_complete", "worker_complete", "publication_complete", "artifacts" })
            yield return ["index", path, 0, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["index", "identity." + path, 0, "identity." + path];
        foreach (string path in new[] { "name", "length", "sha256" }) yield return ["index", "artifacts[0]." + path, 0, "artifacts[0]." + path];

        foreach (string path in new[] { "schema_version", "identity", "statistics_identity", "session_id", "received_at", "receipt_time", "raw_json", "typed_snapshot", "diagnostics" })
            yield return ["statistics", path, 17, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["statistics", "identity." + path, 17, "identity." + path];
        foreach (string path in new[] { "scenario_id", "run_id", "role", "client_id" }) yield return ["statistics", "statistics_identity." + path, 17, "statistics_identity." + path];
        foreach (string path in new[] { "name", "client_id", "type", "timestamp_microseconds", "time_seconds", "age_microseconds", "brokers" })
            yield return ["statistics", "typed_snapshot." + path, 17, "typed_snapshot." + path];
        foreach (string path in new[] { "name", "node_id", "state", "state_age_microseconds", "request_timeouts", "disconnects", "transmission_retries", "transmission_errors", "receive_errors", "throttle", "round_trip_time" })
            yield return ["statistics", "typed_snapshot.brokers[0]." + path, 17, "typed_snapshot.brokers[0]." + path];
        foreach (string countPath in new[] { "throttle", "round_trip_time" })
            foreach (string path in new[] { "count", "value" }) yield return ["statistics", $"typed_snapshot.brokers[0].{countPath}.{path}", 17, $"typed_snapshot.brokers[0].{countPath}.{path}"];
        foreach (string path in new[] { "code", "broker", "field" }) yield return ["statistics-diagnostic", "diagnostics[0]." + path, 17, "diagnostics[0]." + path];

        foreach (string path in new[] { "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "source_event_timestamp", "correlation_id", "source_order", "known_missing_fields", "inference", "event_type", "native_name", "level", "facility", "message", "provenance" })
            yield return ["lifecycle", path, 17, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["lifecycle", "identity." + path, 17, "identity." + path];
        foreach (string path in new[] { "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "event_type", "error_code", "error_name", "reason", "is_error", "is_fatal", "provenance" })
            yield return ["lifecycle-error", path, 17, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["lifecycle-error", "identity." + path, 17, "identity." + path];

        foreach (string path in new[] { "schema_version", "identity", "artifacts", "resolved_configuration", "request_lifecycle_requirements", "request_capability_supported", "observed_broker_names", "guard_observations", "guard_observations_complete", "session_diagnostics", "is_faulted" })
            yield return ["capabilities", path, 17, path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" })
            yield return ["capabilities", "identity." + path, 17, "identity." + path];
        foreach (string path in new[] { "role", "settings", "omissions" }) yield return ["capabilities", "resolved_configuration." + path, 17, "resolved_configuration." + path];
        foreach (string path in new[] { "value", "baseline", "override" }) yield return ["capabilities", "resolved_configuration.settings.acks." + path, 17, "resolved_configuration.settings.acks." + path];
        foreach (string path in new[] { "key", "value", "role", "disposition", "inventory_ids", "sources", "selection_reason" }) yield return ["capabilities", "resolved_configuration.settings.acks.baseline." + path, 17, "resolved_configuration.settings.acks.baseline." + path];
        foreach (string path in new[] { "key", "value", "role", "disposition", "inventory_ids", "sources", "selection_reason" }) yield return ["capabilities", "resolved_configuration.omissions[0]." + path, 17, "resolved_configuration.omissions[0]." + path];
        foreach (string path in new[] { "url", "version_or_date" }) yield return ["capabilities", "resolved_configuration.settings.acks.baseline.sources[0]." + path, 17, "resolved_configuration.settings.acks.baseline.sources[0]." + path];
        foreach (string path in new[] { "requirement", "status", "pinned_version", "source_urls", "source_facts", "inference", "local_test_evidence" }) yield return ["capabilities", "request_lifecycle_requirements[0]." + path, 17, "request_lifecycle_requirements[0]." + path];
        foreach (string path in new[] { "is_complete", "diagnostics", "missing_brokers" }) yield return ["capabilities", "statistics_completion." + path, 17, "statistics_completion." + path];
        foreach (string path in new[] { "code", "broker", "field" }) yield return ["capabilities-diagnostic", "statistics_completion.diagnostics[0]." + path, 17, "statistics_completion.diagnostics[0]." + path];
        foreach (string path in new[] { "scenario_id", "configuration_id", "location" }) yield return ["capabilities-override", "resolved_configuration.settings.acks.override." + path, 17, "resolved_configuration.settings.acks.override." + path];
        foreach (string path in new[] { "path", "line", "column" }) yield return ["capabilities-override", "resolved_configuration.settings.acks.override.location." + path, 17, "resolved_configuration.settings.acks.override.location." + path];
    }

    private static IEnumerable<string> AttemptFieldNames() =>
        ["instance_id", "attempt_id", "ordinal", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds", "invoked_microseconds"];

    private static IReadOnlyList<StructuralProblem> Parse(string artifact, byte[] input)
    {
        InputLimits limits = Limits();
        var budget = new InputBudget(limits);
        return artifact switch
        {
            "ledger" => LedgerReader.ReadRows(input, limits, budget).Problems,
            "manifest" => ManifestReader.Read(input, limits, budget, 100).Problems,
            "index" => ArtifactIndexReader.Read(input, limits, budget).Problems,
            "statistics" => CapturedEvidenceReader.ReadStatistics(input, "statistics.jsonl", limits, budget).Problems,
            "lifecycle" => CapturedEvidenceReader.ReadLifecycle(input, "request-lifecycle.jsonl", limits, budget).Problems,
            "capabilities" => CapturedEvidenceReader.ReadCapabilities(input, "capabilities.json", limits, budget).Problems,
            "statistics-diagnostic" => CapturedEvidenceReader.ReadStatistics(input, "statistics.jsonl", limits, budget).Problems,
            "lifecycle-error" => CapturedEvidenceReader.ReadLifecycle(input, "request-lifecycle.jsonl", limits, budget).Problems,
            "capabilities-diagnostic" => CapturedEvidenceReader.ReadCapabilities(input, "capabilities.json", limits, budget).Problems,
            "capabilities-override" => CapturedEvidenceReader.ReadCapabilities(input, "capabilities.json", limits, budget).Problems,
            _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unknown test artifact.")
        };
    }

    private static byte[] Mutate(string artifact, string path, bool remove = false, bool wrongKind = false)
    {
        JsonNode root = JsonNode.Parse(ReadArtifactJson(artifact))!;
        if (artifact == "statistics-diagnostic")
            root["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "diagnostic", ["broker"] = null, ["field"] = null });
        else if (artifact == "capabilities-diagnostic")
            root["statistics_completion"]!["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "diagnostic", ["broker"] = null, ["field"] = null });
        else if (artifact == "capabilities-override")
            root["resolved_configuration"]!["settings"]!["acks"]!["override"] = new JsonObject
            {
                ["scenario_id"] = "A01",
                ["configuration_id"] = "C0",
                ["location"] = new JsonObject { ["path"] = "scenario.yaml", ["line"] = 1, ["column"] = 1 }
            };

        JsonNode? current = Navigate(root, path, out JsonNode? parent, out string last);
        Assert.True(ContainsProperty(parent!, last));
        if (remove)
        {
            if (parent is JsonObject objectParent) objectParent.Remove(last);
            else if (parent is JsonArray arrayParent) arrayParent.RemoveAt(int.Parse(last, System.Globalization.CultureInfo.InvariantCulture));
        }
        else if (wrongKind)
        {
            JsonNode replacement = current is JsonObject or JsonArray || path.EndsWith(".override", StringComparison.Ordinal)
                ? JsonValue.Create("wrong-kind")! : new JsonObject();
            Assign(parent, last, replacement);
        }
        return EncodeInput(artifact, Encoding.UTF8.GetString(Serialize(root)));
    }

    private static void AddSyntheticNodes(string artifact, JsonNode root)
    {
        if (artifact == "statistics-diagnostic")
            root["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "diagnostic", ["broker"] = null, ["field"] = null });
        else if (artifact == "capabilities-diagnostic")
            root["statistics_completion"]!["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "diagnostic", ["broker"] = null, ["field"] = null });
        else if (artifact == "capabilities-override")
            root["resolved_configuration"]!["settings"]!["acks"]!["override"] = new JsonObject
            {
                ["scenario_id"] = "A01", ["configuration_id"] = "C0",
                ["location"] = new JsonObject { ["path"] = "scenario.yaml", ["line"] = 1, ["column"] = 1 }
            };
    }

    private static bool ContainsProperty(JsonNode node, string property) => node switch
    {
        JsonObject obj => obj.ContainsKey(property),
        JsonArray array => int.TryParse(property, out int index) && index >= 0 && index < array.Count,
        _ => false
    };

    private static byte[] AddProperty(string artifact, string path, JsonNode? value)
    {
        JsonNode root = JsonNode.Parse(ReadArtifactJson(artifact))!;
        string[] parts = Parts(path);
        JsonNode? parent = root;
        foreach (string part in parts[..^1]) parent = Child(parent!, part);
        Assert.IsType<JsonObject>(parent).Add(parts[^1], value?.DeepClone());
        return EncodeInput(artifact, Encoding.UTF8.GetString(Serialize(root)));
    }

    private static byte[] MutateTo(string artifact, string path, JsonNode value)
    {
        JsonNode root = JsonNode.Parse(ReadArtifactJson(artifact))!;
        JsonNode? current = Navigate(root, path, out JsonNode? parent, out string property);
        Assert.True(ContainsProperty(parent!, property));
        Assign(parent, property, value.DeepClone());
        return EncodeInput(artifact, Encoding.UTF8.GetString(Serialize(root)));
    }

    private static JsonNode WithCapabilitiesExtras()
    {
        JsonNode root = JsonNode.Parse(ReadGolden("capabilities"))!;
        root["statistics_completion"]!["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "diag", ["broker"] = null, ["field"] = "" });
        root["resolved_configuration"]!["settings"]!["acks"]!["override"] = new JsonObject
        {
            ["scenario_id"] = "",
            ["configuration_id"] = "",
            ["location"] = new JsonObject { ["path"] = "", ["line"] = null, ["column"] = null }
        };
        return root;
    }

    private static JsonNode ReverseProperties(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            var reordered = new JsonObject();
            foreach ((string key, JsonNode? value) in objectNode.Reverse())
                reordered[key] = value is null ? null : ReverseProperties(value);
            return reordered;
        }
        if (node is JsonArray arrayNode)
        {
            var reordered = new JsonArray();
            foreach (JsonNode? value in arrayNode)
                reordered.Add(value is null ? null : ReverseProperties(value));
            return reordered;
        }
        return node.DeepClone();
    }

    private static string ValidNativeReport() =>
        "{\"ts\":1,\"age\":2,\"brokers\":{\"b\":{\"name\":\"b\",\"nodeid\":1,\"source\":\"learned\",\"state\":\"UP\",\"req_timeouts\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"disconnects\":0,\"throttle\":{\"cnt\":0,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}}}";

    private static void RemoveNativePath(JsonNode node, string path)
    {
        string[] parts = path.Split('.');
        JsonNode? parent = node;
        foreach (string part in parts[..^1]) parent = parent![part];
        Assert.IsType<JsonObject>(parent).Remove(parts[^1]);
    }

    private static void SetNativeWrongKind(JsonNode root, string path)
    {
        string[] parts = path.Split('.');
        JsonNode? parent = root;
        foreach (string part in parts[..^1]) parent = parent![part];
        JsonNode? current = parent![parts[^1]];
        if (current is JsonObject or JsonArray) parent.AsObject()[parts[^1]] = "wrong-kind";
        else parent.AsObject()[parts[^1]] = new JsonObject();
    }

    private static JsonNode? Navigate(JsonNode root, string path, out JsonNode? parent, out string last)
    {
        string[] parts = Parts(path);
        parent = null;
        JsonNode? current = root;
        foreach (string part in parts)
        {
            parent = current;
            current = Child(current!, part);
        }
        last = parts[^1];
        return current;
    }

    private static JsonNode? Child(JsonNode node, string part) => node switch
    {
        JsonObject obj => obj[part],
        JsonArray array => array[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)],
        _ => throw new InvalidOperationException($"Cannot navigate through {node.GetType().Name}.")
    };

    private static void Assign(JsonNode? parent, string property, JsonNode value)
    {
        if (parent is JsonObject obj) obj[property] = value;
        else if (parent is JsonArray array) array[int.Parse(property, System.Globalization.CultureInfo.InvariantCulture)] = value;
        else throw new InvalidOperationException("A test path has no object or array parent.");
    }

    private static string[] Parts(string path) => Regex.Matches(path, @"[^.\[\]]+|\[(\d+)\]")
        .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Value)
        .ToArray();

    private static string ReadArtifactJson(string artifact) => artifact switch
    {
        "ledger" => FirstJsonLine(ReadGolden("ledger")),
        "manifest" => Encoding.UTF8.GetString(ReadGolden("manifest")),
        "index" => Encoding.UTF8.GetString(ReadGolden("index")),
        "statistics" or "statistics-diagnostic" => FirstJsonLine(ReadGolden("statistics")),
        "lifecycle" => FirstJsonLine(ReadGolden("lifecycle")),
        "lifecycle-error" => SecondJsonLine(ReadGolden("lifecycle")),
        "capabilities" or "capabilities-diagnostic" or "capabilities-override" => Encoding.UTF8.GetString(ReadGolden("capabilities")),
        _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unknown test artifact.")
    };

    private static string DecodeInput(string artifact) => Encoding.UTF8.GetString(EncodeInput(artifact, ReadArtifactJson(artifact)));
    private static byte[] EncodeInput(string artifact, string json) => artifact is "ledger" or "statistics" or "lifecycle" or "statistics-diagnostic" or "lifecycle-error"
        ? JsonLine(Encoding.UTF8.GetBytes(json))
        : Encoding.UTF8.GetBytes(json);

    private static string FirstJsonLine(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)'\n');
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
    }

    private static string SecondJsonLine(byte[] bytes)
    {
        int first = Array.IndexOf(bytes, (byte)'\n');
        int next = Array.IndexOf(bytes, (byte)'\n', first + 1);
        return Encoding.UTF8.GetString(bytes, first + 1, next < 0 ? bytes.Length - first - 1 : next - first - 1);
    }

    private static byte[] ReadGolden(string artifact) => File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory, "fixtures", "verifier-v1", "captured-run", artifact switch
        {
            "ledger" => "ledger.jsonl",
            "manifest" => "manifest.json",
            "index" => "index.json",
            "statistics" => "statistics.jsonl",
            "lifecycle" => "request-lifecycle.jsonl",
            "capabilities" => "capabilities.json",
            _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unknown golden artifact.")
        }));

    private static byte[] Serialize(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    private static byte[] JsonLine(byte[] bytes) => bytes.Concat(new byte[] { (byte)'\n' }).ToArray();
    private static InputLimits Limits() => GoldenArtifactReaderTests.Limits();
}
#pragma warning restore CA1707
