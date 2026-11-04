using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Input;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class RestartManifestReaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_SparseRestartPlan_When_ReadInAnyPropertyOrder_Then_PreservesEveryPlannedField(bool reverse)
    {
        JsonNode root = Fixture();
        SealedRunManifest manifest = Success(reverse ? ReverseProperties(root) : root);
        RestartManifest restart = Assert.IsType<RestartManifest>(manifest.Restart);
        Assert.Equal("first", restart.PredecessorInstanceId);
        Assert.Equal(2, restart.Planned.Count);
        Assert.Equal(new RestartPlannedEntry(0, "first-3", "t", "d0", "t-d0", 1, "ocr-started", 17), restart.Planned[0]);
        Assert.Equal(new RestartPlannedEntry(1, "first-8", "t", "d1", "t-d1", 4, "indexed", 41), restart.Planned[1]);
    }

    [Theory]
    [InlineData(0, 0, "complete", true, null)]
    [InlineData(1, 1, "complete", true, null)]
    [InlineData(2, 1, "incomplete", true, 1L)]
    [InlineData(2, 0, "failed", false, 0L)]
    [InlineData(2, 2, "incomplete", false, null)]
    [InlineData(2, 2, "failed", false, null)]
    public void Given_ValidInventoryPrefix_When_Read_Then_AcceptsCompletionState(
        int planned, int inventory, string status, bool bookkeeping, long? suffix)
    {
        JsonNode root = Sized(planned, inventory, status, bookkeeping, suffix);
        Assert.Equal(planned, Success(root).Restart!.Planned.Count);
    }

    [Theory]
    [InlineData(0, "uploaded")]
    [InlineData(1, "ocr-started")]
    [InlineData(2, "ocr-finished")]
    [InlineData(3, "classified")]
    [InlineData(4, "indexed")]
    public void Given_ValidSequenceEventPair_When_Read_Then_AcceptsAllFiveEvents(long sequence, string eventType)
    {
        JsonNode root = Sized(1, 1);
        SetLogical(root, 0, "sequence", JsonValue.Create(sequence));
        SetLogical(root, 0, "event_type", JsonValue.Create(eventType));
        Assert.Equal(sequence, Success(root).Restart!.Planned[0].Sequence);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void Given_NonnegativePlannedTime_When_Read_Then_PreservesIt(long time)
    {
        JsonNode root = Sized(1, 1);
        SetLogical(root, 0, "planned_microseconds", JsonValue.Create(time));
        Assert.Equal(time, Success(root).Restart!.Planned[0].PlannedMicroseconds);
    }

    [Fact]
    public void Given_InteriorSpacesInControlText_When_Read_Then_AcceptsProducerContract()
    {
        JsonNode root = Sized(1, 1);
        SetLogical(root, 0, "tenant_id", JsonValue.Create("t x"));
        SetLogical(root, 0, "key", JsonValue.Create("t x-d0"));
        Planned(root)[0]!["predecessor_attempt_id"] = "first 3";
        RestartManifest restart = Success(root).Restart!;
        Assert.Equal("t x", restart.Planned[0].TenantId);
        Assert.Equal("first 3", restart.Planned[0].PredecessorAttemptId);
    }

    [Theory]
    [InlineData(1, "ocr-started", true)]
    [InlineData(2, "ocr-finished", true)]
    [InlineData(3, "classified", false)]
    public void Given_LogicalSlotAtInt64Boundary_When_Read_Then_RejectsOnlyOverflow(long sequence, string eventType, bool accepted)
    {
        JsonNode root = Sized(1, 1);
        SetLogical(root, 0, "document_id", JsonValue.Create("d1844674407370955161"));
        SetLogical(root, 0, "key", JsonValue.Create("t-d1844674407370955161"));
        SetLogical(root, 0, "sequence", JsonValue.Create(sequence));
        SetLogical(root, 0, "event_type", JsonValue.Create(eventType));
        if (accepted) Assert.Equal(sequence, Success(root).Restart!.Planned[0].Sequence);
        else Reject(root, "invalid_restart_planned_entry", "restart.planned[0]");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("1")]
    [InlineData("true")]
    public void Given_NonObjectRestart_When_Read_Then_ReportsItsExactPath(string json)
    {
        JsonNode root = Fixture();
        root["restart"] = JsonNode.Parse(json);
        Reject(root, "wrong_json_kind", "restart");
    }

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public void Given_MissingRestartField_When_Read_Then_ReportsItsExactPath(string path)
    {
        JsonNode root = Fixture();
        (JsonObject parent, string name) = Parent(root, path);
        Assert.True(parent.Remove(name));
        Reject(root, "missing_property", path, single: false);
    }

    [Theory]
    [InlineData("restart.future")]
    [InlineData("restart.planned[0].future")]
    public void Given_UnknownRestartField_When_Read_Then_ReportsItsExactPath(string path)
    {
        JsonNode root = Fixture();
        (JsonObject parent, string name) = Parent(root, path);
        parent[name] = 1;
        Reject(root, "unknown_property", path);
    }

    [Theory]
    [MemberData(nameof(WrongKindCases))]
    public void Given_WrongKindRestartField_When_Read_Then_ReportsItsExactPath(string path, string json)
    {
        JsonNode root = Fixture();
        (JsonObject parent, string name) = Parent(root, path);
        parent[name] = JsonNode.Parse(json);
        Reject(root, "wrong_json_kind", path);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("1")]
    [InlineData("true")]
    public void Given_NonObjectPlannedEntry_When_Read_Then_ReportsItsArrayPosition(string json)
    {
        JsonNode root = Fixture();
        Planned(root)[0] = JsonNode.Parse(json);
        Reject(root, "expected_object", "restart.planned[0]");
    }

    [Theory]
    [InlineData("predecessor_instance_id", "\"predecessor_instance_id\":\"first\",", "restart.predecessor_instance_id")]
    [InlineData("ordinal", "\"ordinal\":0,", "restart.planned[0].ordinal")]
    public void Given_DuplicateRestartProperty_When_Read_Then_StrictJsonRejectsIt(string property, string token, string path)
    {
        // Insert into the literal restart section, preserving duplicate keys as text.
        int restartStart = FixtureJson.IndexOf("\"restart\"", StringComparison.Ordinal);
        int offset = FixtureJson.IndexOf(token, restartStart, StringComparison.Ordinal);
        Assert.True(offset >= 0, property);
        string duplicate = FixtureJson.Insert(offset, token);
        Reject(Read(duplicate), "duplicate_property", path);
    }

    [Theory]
    [InlineData("A01")]
    [InlineData("b10")]
    public void Given_OtherScenario_When_Read_Then_RejectsRestartManifest(string scenario)
    {
        JsonNode root = Fixture();
        root["identity"]!["scenario_id"] = scenario;
        Reject(root, "restart_manifest_requires_b10", "identity.scenario_id");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("first second")]
    [InlineData("first\t")]
    [InlineData("first\u0001")]
    [InlineData("second")]
    public void Given_InvalidPredecessor_When_Read_Then_RejectsIdentity(string predecessor)
    {
        JsonNode root = Fixture();
        root["restart"]!["predecessor_instance_id"] = predecessor;
        Reject(root, "restart_predecessor_invalid", "restart.predecessor_instance_id");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void Given_MismatchedPlannedCount_When_Read_Then_RejectsCount(long count)
    {
        JsonNode root = Fixture();
        root["planned_attempt_count"] = count;
        Reject(root, "restart_planned_count_mismatch", "planned_attempt_count");
    }

    [Theory]
    [MemberData(nameof(InvalidPlannedCases))]
    public void Given_InvalidPlannedEntry_When_Read_Then_ReportsFirstInvalidEntry(int index, string property, string json)
    {
        JsonNode root = Fixture();
        SetLogical(root, index, property, JsonNode.Parse(json));
        Reject(root, "invalid_restart_planned_entry", $"restart.planned[{index}]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_NonIncreasingLogicalSlots_When_Read_Then_RejectsSecondEntry(bool descending)
    {
        JsonNode root = Fixture();
        SetLogical(root, 1, "document_id", JsonValue.Create("d0"));
        SetLogical(root, 1, "key", JsonValue.Create("t-d0"));
        SetLogical(root, 1, "sequence", JsonValue.Create(descending ? 0 : 1));
        SetLogical(root, 1, "event_type", JsonValue.Create(descending ? "uploaded" : "ocr-started"));
        Reject(root, "invalid_restart_planned_entry", "restart.planned[1]");
    }

    [Theory]
    [InlineData("tenant_id", "\"other\"")]
    [InlineData("document_id", "\"d2\"")]
    [InlineData("key", "\"other-key\"")]
    [InlineData("sequence", "2")]
    [InlineData("event_type", "\"ocr-finished\"")]
    [InlineData("planned_microseconds", "18")]
    public void Given_InventoryLogicalFieldDiffers_When_Read_Then_RejectsPrefix(string property, string json)
    {
        JsonNode root = Fixture();
        Inventory(root)[0]![property] = JsonNode.Parse(json);
        Reject(root, "restart_inventory_mismatch", "inventory[0]");
    }

    [Fact]
    public void Given_InventoryLongerThanPlan_When_Read_Then_RejectsExtraEntry()
    {
        JsonNode root = Fixture();
        JsonNode extra = Inventory(root)[1]!.DeepClone();
        extra["ordinal"] = 2;
        extra["attempt_id"] = "second-2";
        Inventory(root).Add(extra);
        root["attempt_count"] = 3;
        Reject(root, "restart_inventory_mismatch", "inventory[2]");
    }

    [Fact]
    public void Given_SwappedInventoryLogicalFields_When_Read_Then_RejectsPrefix()
    {
        JsonNode root = Fixture();
        foreach (string property in LogicalFields)
        {
            JsonNode? first = Inventory(root)[0]![property]?.DeepClone();
            Inventory(root)[0]![property] = Inventory(root)[1]![property]?.DeepClone();
            Inventory(root)[1]![property] = first;
        }
        Reject(root, "restart_inventory_mismatch", "inventory[0]");
    }

    [Theory]
    [InlineData(2, 1, "incomplete", true, null)]
    [InlineData(2, 1, "incomplete", true, 0L)]
    [InlineData(2, 1, "incomplete", true, 2L)]
    [InlineData(2, 2, "complete", true, 0L)]
    [InlineData(2, 2, "complete", true, 2L)]
    [InlineData(2, 1, "complete", true, 1L)]
    [InlineData(2, 2, "complete", false, null)]
    [InlineData(0, 0, "complete", true, 0L)]
    public void Given_InvalidSuffixOrCompletion_When_Read_Then_RejectsCompletion(
        int planned, int inventory, string status, bool bookkeeping, long? suffix)
    {
        Reject(Sized(planned, inventory, status, bookkeeping, suffix),
            "manifest_unexecuted_suffix_mismatch", "first_unexecuted_ordinal");
    }

    [Fact]
    public void Given_MultipleSemanticProblems_When_Read_Then_ReportsOnlyEarliestCheck()
    {
        JsonNode root = Fixture();
        root["identity"]!["scenario_id"] = "A01";
        root["restart"]!["predecessor_instance_id"] = "second";
        root["planned_attempt_count"] = 1;
        Reject(root, "restart_manifest_requires_b10", "identity.scenario_id");
        root["identity"]!["scenario_id"] = "B10";
        Reject(root, "restart_predecessor_invalid", "restart.predecessor_instance_id");
    }

    [Fact]
    public void Given_StructuralAndSemanticProblems_When_Read_Then_DoesNotRunSemanticChecks()
    {
        JsonNode root = Fixture();
        root["identity"]!["scenario_id"] = "A01";
        root["restart"]!["predecessor_instance_id"] = null;
        Reject(root, "wrong_json_kind", "restart.predecessor_instance_id");
    }

    [Theory]
    [InlineData(9996, true)]
    [InlineData(9997, false)]
    public void Given_SharedEntryBudgetBoundary_When_Read_Then_CountsBothMaterializedLists(long consumed, bool accepted)
    {
        var budget = new InputBudget(Limits);
        budget.ConsumeEntries(consumed);
        if (accepted) Assert.True(ManifestReader.Read(Encoding.UTF8.GetBytes(FixtureJson), Limits, budget, 1000).IsSuccess);
        else
        {
            InputLimitExceededException exception = Assert.Throws<InputLimitExceededException>(() =>
                ManifestReader.Read(Encoding.UTF8.GetBytes(FixtureJson), Limits, budget, 1000));
            Assert.Equal("The combined verifier inputs exceed the configured entry limit.", exception.Message);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Given_PlannedArrayLimitBoundary_When_Read_Then_AppliesItsIndependentCap(long limit, bool accepted)
    {
        JsonNode root = Sized(2, 0, "incomplete", true, 0);
        if (accepted) Assert.True(Read(root, limit).IsSuccess);
        else
        {
            InputLimitExceededException exception = Assert.Throws<InputLimitExceededException>(() => Read(root, limit));
            Assert.Equal("manifest.json restart planned entries exceed the configured entry limit.", exception.Message);
        }
    }

    [Fact]
    public void Given_EmptyListsAndZeroInventoryLimit_When_Read_Then_AcceptsEmptyRestart()
    {
        Assert.True(Read(Sized(0, 0), 0).IsSuccess);
    }

    public static IEnumerable<object[]> RequiredFields()
    {
        yield return ["restart.predecessor_instance_id"];
        yield return ["restart.planned"];
        foreach (string property in PlannedFields) yield return [$"restart.planned[0].{property}"];
    }

    public static IEnumerable<object[]> WrongKindCases()
    {
        foreach (object[] row in RequiredFields())
        {
            string path = (string)row[0];
            yield return [path, "null"];
            yield return [path, "{}"];
            if (path is "restart.planned") continue;
            string property = path[(path.LastIndexOf('.') + 1)..];
            if (property is "ordinal" or "sequence" or "planned_microseconds")
            {
                yield return [path, "\"1\""];
                yield return [path, "1.5"];
                yield return [path, "9223372036854775808"];
            }
            else yield return [path, "1"];
        }
    }

    public static IEnumerable<object[]> InvalidPlannedCases()
    {
        yield return [0, "ordinal", "-1"];
        yield return [0, "ordinal", "1"];
        yield return [1, "ordinal", "0"];
        yield return [1, "ordinal", "2"];
        yield return [1, "predecessor_attempt_id", "\"first-3\""];
        foreach (string property in new[] { "predecessor_attempt_id", "tenant_id", "document_id", "key", "event_type" })
            foreach (string json in new[] { "\"\"", "\" \"", "\"bad\\u0001\"" })
                yield return [0, property, json];
        foreach (string document in new[] { "d00", "d-1", "d+1", "d 1", "D1", "d", "d9223372036854775808" })
            yield return [0, "document_id", JsonValue.Create(document)!.ToJsonString()];
        yield return [0, "planned_microseconds", "-1"];
        yield return [0, "sequence", "-1"];
        yield return [0, "sequence", "5"];
        yield return [0, "event_type", "\"indexed\""];
        yield return [0, "key", "\"wrong\""];
    }

    private static readonly string[] PlannedFields =
        ["ordinal", "predecessor_attempt_id", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds"];
    private static readonly string[] LogicalFields =
        ["tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds"];
    private static readonly InputLimits Limits = new(32, 1048576, 4194304, 65536, 1000, 10000, 512);

    private static JsonNode Fixture() => JsonNode.Parse(FixtureJson)!;
    private static JsonArray Planned(JsonNode root) => root["restart"]!["planned"]!.AsArray();
    private static JsonArray Inventory(JsonNode root) => root["inventory"]!.AsArray();

    private static JsonNode Sized(int planned, int inventory, string status = "complete", bool bookkeeping = true, long? suffix = null)
    {
        JsonNode root = Fixture();
        while (Planned(root).Count > planned) Planned(root).RemoveAt(Planned(root).Count - 1);
        while (Inventory(root).Count > inventory) Inventory(root).RemoveAt(Inventory(root).Count - 1);
        root["planned_attempt_count"] = planned;
        root["attempt_count"] = inventory;
        root["worker_status"] = status;
        root["bookkeeping_complete"] = bookkeeping;
        root["first_unexecuted_ordinal"] = suffix;
        return root;
    }

    private static void SetLogical(JsonNode root, int index, string property, JsonNode? value)
    {
        Planned(root)[index]![property] = value?.DeepClone();
        if (LogicalFields.Contains(property, StringComparer.Ordinal))
            Inventory(root)[index]![property] = value?.DeepClone();
    }

    private static (JsonObject Parent, string Name) Parent(JsonNode root, string path)
    {
        string property = path[(path.LastIndexOf('.') + 1)..];
        return (path.StartsWith("restart.planned[0].", StringComparison.Ordinal)
            ? Planned(root)[0]!.AsObject() : root["restart"]!.AsObject(), property);
    }

    private static SealedRunManifest Success(JsonNode root)
    {
        ParseResult<SealedRunManifest> result = Read(root);
        Assert.True(result.IsSuccess, string.Join(", ", result.Problems));
        return Assert.IsType<SealedRunManifest>(result.Value);
    }

    private static void Reject(JsonNode root, string code, string field, bool single = true) => Reject(Read(root), code, field, single);

    private static void Reject(ParseResult<SealedRunManifest> result, string code, string field, bool single = true)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        if (single) Assert.Single(result.Problems);
        Assert.Contains(result.Problems, problem => problem == new StructuralProblem(2, code, "manifest.json", null, field, null));
        Assert.All(result.Problems, problem =>
        {
            Assert.Equal(2, problem.CheckNumber);
            Assert.Equal("manifest.json", problem.Artifact);
        });
    }

    private static ParseResult<SealedRunManifest> Read(JsonNode root, long limit = 1000) => Read(root.ToJsonString(), limit);
    private static ParseResult<SealedRunManifest> Read(string json, long limit = 1000) =>
        ManifestReader.Read(Encoding.UTF8.GetBytes(json), Limits, new InputBudget(Limits), limit);

    private static JsonNode ReverseProperties(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach ((string key, JsonNode? value) in obj.Reverse()) result[key] = value is null ? null : ReverseProperties(value);
            return result;
        }
        if (node is JsonArray array)
        {
            var result = new JsonArray();
            foreach (JsonNode? value in array) result.Add(value is null ? null : ReverseProperties(value));
            return result;
        }
        return node.DeepClone();
    }

    private const string FixtureJson = """
        {
          "schema_version": 1,
          "identity": {"scenario_id":"B10","configuration_id":"C0","run_id":"r","instance_id":"second","expected_instance_id":"second","client_id":"second"},
          "generator_version":"1", "planned_attempt_count":2, "attempt_count":2,
          "inventory":[
            {"instance_id":"second","attempt_id":"second-0","ordinal":0,"tenant_id":"t","document_id":"d0","key":"t-d0","sequence":1,"event_type":"ocr-started","planned_microseconds":17,"invoked_microseconds":17},
            {"instance_id":"second","attempt_id":"second-1","ordinal":1,"tenant_id":"t","document_id":"d1","key":"t-d1","sequence":4,"event_type":"indexed","planned_microseconds":41,"invoked_microseconds":41}
          ],
          "timestamp_frequency":1000000, "start_timestamp":0,
          "worker_status":"complete", "bookkeeping_complete":true, "first_unexecuted_ordinal":null,
          "diagnostics":[], "runtime_versions":{}, "evidence_files":{}, "guard_observations":[],
          "restart":{"predecessor_instance_id":"first","planned":[
            {"ordinal":0,"predecessor_attempt_id":"first-3","tenant_id":"t","document_id":"d0","key":"t-d0","sequence":1,"event_type":"ocr-started","planned_microseconds":17},
            {"ordinal":1,"predecessor_attempt_id":"first-8","tenant_id":"t","document_id":"d1","key":"t-d1","sequence":4,"event_type":"indexed","planned_microseconds":41}
          ]}
        }
        """;
}
#pragma warning restore CA1707
