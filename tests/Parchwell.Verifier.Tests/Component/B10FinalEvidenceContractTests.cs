using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.B10Final;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Trait("Tier", "Docker")]
public sealed class B10FinalEvidenceContractTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    [Theory]
    [InlineData("key")]
    [InlineData("unconfirmed_count")]
    [InlineData("resend_invoked")]
    [InlineData("unreported_attempts")]
    [InlineData("unknown-arm")]
    [InlineData("unknown-member")]
    [InlineData("malformed-member")]
    [InlineData("malformed-first-instance")]
    public async Task Given_ReboundA3ArmFailure_When_RealFinalSweepRuns_Then_E17RejectsOnlyRestart(string defect)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            JsonObject arm = root["data"]!["arms"]![1]!.AsObject();
            if (defect == "key") arm["key"] = "wrong-key";
            else if (defect is "unconfirmed_count" or "resend_invoked" or "unreported_attempts") arm[defect] = 2;
            else if (defect == "unknown-arm") arm["unknown"] = true;
            else if (defect == "unknown-member") arm["members"]![0]!["unknown"] = true;
            else if (defect == "malformed-first-instance") root["data"]!["first_instance_id"] = new JsonObject();
            else arm["members"]![0]!["sequence"] = "4";
        });
        Assert.Equal(0, await run.RunAsync());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject item = run.Repeat(repeat);
            Assert.Equal("complete_valid", item["status"]!.GetValue<string>());
            Assert.True(item["classification_final"]!.GetValue<bool>());
            Assert.Empty(item["reasons"]!.AsArray());
            Assert.Equal(1, item["timeout"]!["ok"]!.GetValue<long>());
            foreach (string field in new[] { "loss", "phantom", "reported_failure", "duplicate", "reorder" })
                Assert.Equal(0, item["timeout"]![field]!.GetValue<long>());
            JsonNode restart = item["restart"]!;
            if (repeat == 3)
            {
                Assert.Equal("ineligible", restart["eligibility"]!.GetValue<string>());
                Assert.Equal(["b10_restart_input_unavailable"], restart["eligibility_reasons"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());
                Assert.Null(restart["arms"]);
                Assert.Equal("unavailable", restart["comparison"]!.GetValue<string>());
            }
            else
            {
                Assert.Equal("eligible", restart["eligibility"]!.GetValue<string>());
                Assert.Empty(restart["eligibility_reasons"]!.AsArray());
                Assert.NotNull(restart["arms"]);
            }
        }
    }

    [Fact]
    public async Task Given_FiveRepeatBuilderAndRealPublisherExports_When_ComparingWireKinds_Then_AllMemberNameSetsMatch_T0()
    {
        Dictionary<string, byte[]> abrupt = await B10AbruptVerifyEvidence.ExportAsync();
        Dictionary<string, byte[]> ordinary = await B10AbruptVerifyEvidence.ExportTimeoutAsync();
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            B10AbruptVerifyEvidence built = B10FinalSweepHarness.ScientificEvidence(repeat);
            Dictionary<string, JsonObject> expected = Shapes(abrupt), actual = Shapes(built.Files);
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (string kind in expected.Keys)
                AssertMembers(expected[kind], actual[kind]);
            foreach ((string name, byte[] bytes) in ordinary)
            {
                byte[] fixture = built.Files["timeout/" + name["producer/pod-first/".Length..]];
                string[] producedRows = Rows(bytes), fixtureRows = Rows(fixture);
                if (producedRows.Length == 0) { Assert.Empty(fixtureRows); continue; }
                Assert.NotEmpty(fixtureRows);
                foreach (string row in fixtureRows) AssertMembers(JsonNode.Parse(producedRows[0]), JsonNode.Parse(row));
                if (name.EndsWith("manifest.json", StringComparison.Ordinal))
                    Assert.Equal(Names(JsonNode.Parse(bytes)!["runtime_versions"]!.AsObject()), Names(JsonNode.Parse(fixture)!["runtime_versions"]!.AsObject()));
            }
        }
    }

    [Fact]
    public async Task Given_EmptyAndLargeValuesWithSameIdentity_When_Drained_Then_RetainedRecordsContainNoPayloadBytes_T39()
    {
        await kafka.ResetAsync(); using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files);
        await run.ProduceAsync(0, 0, key: "retained", value: []);
        await run.ProduceAsync(0, 1, key: "retained", value: new byte[65536]);
        using var consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
        var window = new RunWindow([], new Dictionary<int, long> { [0] = 2 }, [new PartitionWindow(0, 0, 2)], [], true);
        WindowDrainResult result = WindowConsumer.Drain(consumer, window, TimeProvider.System, TimeProvider.System.GetTimestamp(),
            TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(50), new ConsumedRecordBudget(2, 100000), new HashSet<string>(StringComparer.Ordinal) { "retained" }, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Records.Count);
        ConsumedRecord first = result.Records[0], second = result.Records[1];
        Assert.Equal(first.Problems, second.Problems);
        Assert.Equal(first with { Offset = second.Offset, ValueSha256 = second.ValueSha256 }, second with { Problems = first.Problems });
        Assert.NotEqual(first.ValueSha256, second.ValueSha256);
        Assert.Equal(B10AbruptVerifyEvidence.Hash([]), first.ValueSha256);
        Assert.Equal(B10AbruptVerifyEvidence.Hash(new byte[65536]), second.ValueSha256);
        Assert.DoesNotContain(typeof(ConsumedRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), property => property.PropertyType == typeof(byte[]));
        Assert.DoesNotContain(typeof(ConsumedRecord).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), field => field.FieldType == typeof(byte[]));
    }

    private static string[] Rows(byte[] bytes) => Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    private static string[] Names(JsonObject node) => node.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
    // Nulls and empty arrays expose no child members. Map keys carry data rather than wire field names.
    private static void AssertMembers(JsonNode? expected, JsonNode? actual, string? member = null)
    {
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            if (member is "journal_counts" or "brokers" or "topics" or "partitions" or "settings" or "evidence_files" or "runtime_versions" or "omissions")
            {
                foreach (JsonNode? item in actualObject.Select(pair => pair.Value))
                    foreach (JsonNode? produced in expectedObject.Select(pair => pair.Value)) AssertMembers(produced, item);
            }
            else
            {
                Assert.Equal(Names(expectedObject), Names(actualObject));
                foreach (string name in expectedObject.Select(pair => pair.Key)) AssertMembers(expectedObject[name], actualObject[name], name);
            }
        }
        else if (expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            foreach (JsonNode? item in actualArray)
                foreach (JsonNode? produced in expectedArray) AssertMembers(produced, item);
        }
    }

    private static Dictionary<string, JsonObject> Shapes(Dictionary<string, byte[]> files)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach ((string name, byte[] bytes) in files)
        {
            if (name is not ("first/journal.jsonl" or "first/registration.json" or "first/abrupt-manifest.json" or "first/termination.json" or "successor/registration.json" or "successor/manifest.json" or "successor/completion.json")) continue;
            foreach (string row in Rows(bytes))
            {
                JsonObject root = JsonNode.Parse(row)!.AsObject();
                string kind = root["kind"]!.GetValue<string>();
                if (result.TryGetValue(kind, out JsonObject? previous)) AssertMembers(previous, root);
                else result.Add(kind, root);
            }
        }
        return result;
    }
}

[Trait("Tier", "Unit")]
public sealed class B10FinalRestartAgreementTests
{
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Given_JournalDerivedCountBelowAtOrAboveAuthority_When_Reauthenticated_Then_OnlyExactCountsPass()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        for (int index = 0; index < 2; index++)
        {
            foreach (string field in new[] { "unconfirmed_count", "resend_invoked", "unreported_attempts" })
            {
                long expected = data["arms"]![index]![field]!.GetValue<long>();
                foreach (long value in new[] { expected - 1, expected, expected + 1 })
                {
                    JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![index]![field] = value;
                    Assert.Equal(value == expected, Accepts(input, changed));
                }
                JsonObject unknown = data.DeepClone().AsObject(); unknown["arms"]![index]![field] = null;
                Assert.False(Accepts(input, unknown));
            }
        }
    }

    [Fact]
    public async Task Given_ArmOrUnconfirmedMemberCountBelowAtOrAboveAuthority_When_Reauthenticated_Then_OnlyExactSetsPass()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        foreach (int count in new[] { 0, 1, 2, 3 })
        {
            JsonObject changed = data.DeepClone().AsObject();
            changed["arms"] = new JsonArray(Enumerable.Range(0, count).Select(index => data["arms"]![index % 2]!.DeepClone()).ToArray());
            Assert.Equal(count == 2, Accepts(input, changed));
        }
        for (int arm = 0; arm < 2; arm++)
        {
            foreach (int count in new[] { 0, 1, 2 })
            {
                JsonObject changed = data.DeepClone().AsObject();
                changed["arms"]![arm]!["members"] = new JsonArray(Enumerable.Range(0, count).Select(_ => data["arms"]![arm]!["members"]![0]!.DeepClone()).ToArray());
                Assert.Equal(count == 1, Accepts(input, changed));
            }
        }
        JsonObject reversed = data.DeepClone().AsObject();
        reversed["arms"] = new JsonArray(data["arms"]![1]!.DeepClone(), data["arms"]![0]!.DeepClone());
        Assert.False(Accepts(input, reversed));
        JsonObject absent = data.DeepClone().AsObject(); absent["arms"] = null;
        Assert.False(Accepts(input, absent));
        Assert.True(Accepts(new B10FinalInput(1), absent));
        Assert.False(Accepts(new B10FinalInput(1), data));
    }

    [Fact]
    public async Task Given_ChangedArmOrMemberMetadata_When_Reauthenticated_Then_JournalIdentityAndTerminalsMustAgree()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        for (int index = 0; index < 2; index++)
        {
            foreach (string field in new[] { "arm", "key" })
            {
                JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![index]![field] = "wrong";
                Assert.False(Accepts(input, changed));
            }
            foreach (string field in new[] { "original_attempt_id", "resend_attempt_id", "payload_sha256" })
            {
                JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![index]!["members"]![0]![field] = "wrong";
                Assert.False(Accepts(input, changed));
            }
            foreach (long sequence in new long[] { 3, 4, 5 })
            {
                JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![index]!["members"]![0]!["sequence"] = sequence;
                Assert.Equal(sequence == 4, Accepts(input, changed));
            }
            foreach (string field in new[] { "delivered", "error_code", "persistence_status" })
            {
                JsonObject changed = data.DeepClone().AsObject(); JsonNode terminal = changed["arms"]![index]!["members"]![0]!["resend_terminal"]!;
                if (field == "delivered") terminal[field] = false;
                else terminal[field] = terminal[field]!.GetValue<int>() + 1;
                Assert.False(Accepts(input, changed));
            }
            foreach (string field in new[] { "resend_attempt_id", "resend_terminal" })
            {
                JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![index]!["members"]![0]![field] = null;
                Assert.False(Accepts(input, changed));
            }
        }
    }

    [Fact]
    public async Task Given_UnknownOrMissingMemberAtAnyNestedLevel_When_ReadingA3Arms_Then_ClosedShapeRejects()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        Assert.True(Accepts(input, data));
        foreach (string path in ObjectPaths(data["arms"]!, "arms"))
        {
            JsonObject unknown = data.DeepClone().AsObject(); At(unknown, path).AsObject()["unknown"] = true;
            Assert.False(Accepts(input, unknown));
            foreach (string field in At(data, path).AsObject().Select(pair => pair.Key))
            {
                JsonObject missing = data.DeepClone().AsObject(); At(missing, path).AsObject().Remove(field);
                Assert.False(Accepts(input, missing));
            }
        }
    }

    [Fact]
    public async Task Given_WrongNestedTypes_When_ReadingA3Arms_Then_EveryExistingFieldRetainsItsWireType()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        foreach (string path in ObjectPaths(data["arms"]!, "arms"))
        {
            foreach (string field in At(data, path).AsObject().Select(pair => pair.Key))
            {
                JsonObject changed = data.DeepClone().AsObject();
                At(changed, path)[field] = new JsonObject();
                Assert.False(Accepts(input, changed));
            }
        }
        for (int arm = 0; arm < 2; arm++)
        {
            foreach (string field in new[] { "originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends" })
            {
                foreach (long value in new long[] { -1, 0, 1, long.MaxValue })
                {
                    JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![arm]![field] = value;
                    Assert.Equal(value >= 0, Accepts(input, changed));
                }
                JsonObject unknown = data.DeepClone().AsObject(); unknown["arms"]![arm]![field] = null;
                Assert.True(Accepts(input, unknown));
            }
            foreach (string field in new[] { "copy_count", "original_copies", "resend_copies", "other_copies", "pair_count" })
            {
                foreach (long value in new long[] { -1, 0, 1, long.MaxValue })
                {
                    JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![arm]!["members"]![0]![field] = value;
                    Assert.Equal(value >= 0, Accepts(input, changed));
                }
                JsonObject unknown = data.DeepClone().AsObject(); unknown["arms"]![arm]!["members"]![0]![field] = null;
                Assert.True(Accepts(input, unknown));
            }
        }
    }

    [Fact]
    public async Task Given_NonIntegerOrOverflowingNestedNumber_When_ReadingA3Arms_Then_RejectsWithoutCoercion()
    {
        (B10FinalInput input, JsonObject data) = await EvidenceAsync();
        foreach (string path in ObjectPaths(data["arms"]!, "arms"))
        {
            foreach ((string field, JsonNode? value) in At(data, path).AsObject())
            {
                if (value is not JsonValue scalar || !scalar.TryGetValue<long>(out _)) continue;
                foreach (string json in new[] { "true", "\"1\"", "1.5", "9223372036854775808" })
                {
                    JsonObject changed = data.DeepClone().AsObject(); At(changed, path)[field] = JsonNode.Parse(json);
                    Assert.False(Accepts(input, changed));
                }
            }
        }
    }

    [Fact]
    public async Task Given_NestedNumericAndRoleBoundaries_When_ReadingA3Arms_Then_ExistingScalarDomainsArePreserved()
    {
        (_, JsonObject data) = await EvidenceAsync();
        foreach (string path in ObjectPaths(data["arms"]!, "arms"))
        {
            JsonObject node = At(data, path).AsObject();
            foreach ((string field, JsonNode? scalar) in node)
            {
                if (scalar is not JsonValue value || !value.TryGetValue<long>(out _)) continue;
                bool signed = field is "error_code" or "persistence_status";
                long maximum = field is "partition" or "error_code" or "persistence_status" ? int.MaxValue : long.MaxValue;
                foreach (long number in new[] { -1, 0, 1, maximum })
                {
                    JsonObject changed = data.DeepClone().AsObject(); At(changed, path)[field] = number;
                    Assert.Equal(signed || number >= 0, ShapeAccepts(changed));
                }
                if (maximum == int.MaxValue)
                {
                    foreach (long number in new[] { (long)int.MinValue - 1, (long)int.MaxValue + 1 })
                    {
                        JsonObject changed = data.DeepClone().AsObject(); At(changed, path)[field] = number;
                        Assert.False(ShapeAccepts(changed));
                    }
                }
            }
            if (!node.ContainsKey("role")) continue;
            foreach (string role in new[] { "original", "resend", "other", "unknown" })
            {
                JsonObject changed = data.DeepClone().AsObject(); At(changed, path)["role"] = role;
                Assert.Equal(role != "unknown", ShapeAccepts(changed));
            }
            foreach (string field in new[] { "instance_id", "attempt_id" })
            {
                foreach (string? header in new string?[] { null, "" })
                {
                    JsonObject changed = data.DeepClone().AsObject(); At(changed, path)[field] = header;
                    Assert.True(ShapeAccepts(changed));
                }
            }
        }
    }

    [Fact]
    public async Task Given_NoAuthenticatedSuccessor_When_Reauthenticated_Then_CrossRestartInvocationAndMemberMetadataRemainUnknown()
    {
        (B10FinalInput input, _) = await EvidenceAsync(); input.Successor = null;
        var data = new JsonObject { ["arms"] = JsonSerializer.SerializeToNode(AbruptClassification.Evaluate(input.First!, null, [], true, 100), WireOptions) };
        Assert.True(Accepts(input, data));
        foreach (long count in new long[] { 0, 1, 2 })
        {
            JsonObject changed = data.DeepClone().AsObject(); changed["arms"]![1]!["resend_invoked"] = count;
            Assert.False(Accepts(input, changed));
        }
        JsonObject resend = data.DeepClone().AsObject(); resend["arms"]![1]!["members"]![0]!["resend_attempt_id"] = "r4";
        Assert.False(Accepts(input, resend));
        JsonObject terminal = data.DeepClone().AsObject(); terminal["arms"]![1]!["members"]![0]!["resend_terminal"] = new JsonObject
        { ["delivered"] = true, ["error_code"] = 0, ["persistence_status"] = 2 };
        Assert.False(Accepts(input, terminal));
    }

    private static IEnumerable<string> ObjectPaths(JsonNode node, string path)
    {
        if (node is JsonObject obj)
        {
            yield return path;
            foreach ((string name, JsonNode? child) in obj)
                if (child is not null)
                    foreach (string nested in ObjectPaths(child, path + "/" + name)) yield return nested;
        }
        else if (node is JsonArray array)
            for (int index = 0; index < array.Count; index++)
                if (array[index] is { } child)
                    foreach (string nested in ObjectPaths(child, path + "/" + index)) yield return nested;
    }

    private static JsonNode At(JsonObject data, string path)
    {
        JsonNode node = data;
        foreach (string part in path.Split('/')) node = node is JsonArray array ? array[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)]! : node[part]!;
        return node;
    }

    private static bool Accepts(B10FinalInput input, JsonObject data)
    {
        JsonElement parsed = AbruptEvidenceCodec.Parse(Encoding.UTF8.GetBytes(data.ToJsonString()));
        try
        {
            B10FinalRepeatEvidence.ValidateArms(parsed.GetProperty("arms"));
            return B10FinalRepeatEvidence.SameMembers(input, parsed);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or OverflowException) { return false; }
    }

    private static bool ShapeAccepts(JsonObject data)
    {
        try
        {
            B10FinalRepeatEvidence.ValidateArms(AbruptEvidenceCodec.Parse(Encoding.UTF8.GetBytes(data.ToJsonString())).GetProperty("arms"));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or OverflowException) { return false; }
    }

    private static async Task<(B10FinalInput Input, JsonObject Data)> EvidenceAsync()
    {
        var source = new B10AbruptVerifyEvidence();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string name, byte[] bytes) in source.Files)
        {
            if (name.StartsWith("first/", StringComparison.Ordinal)) files[B10AbruptVerifyEvidence.First + "/" + name[6..]] = bytes;
            else if (name.StartsWith("successor/", StringComparison.Ordinal)) files[B10AbruptVerifyEvidence.Second + "/" + name[10..]] = bytes;
        }
        var options = new VerifierHostOptions { RunId = "synthetic", Repeat = 1, VerifierCommit = B10AbruptVerifyEvidence.Commit, LoadGenImage = B10AbruptVerifyEvidence.Image };
        var reader = new AbruptEvidenceReader(options, (name, _, _) => Task.FromResult(files.GetValueOrDefault(name)));
        string[] Names(string pod) => files.Keys.Where(name => name.StartsWith(pod + "/", StringComparison.Ordinal)).ToArray();
        AbruptFirstEvidence first = await reader.ReadFirstAsync("", B10AbruptVerifyEvidence.First, "uid-first", Names(B10AbruptVerifyEvidence.First), TestContext.Current.CancellationToken);
        AbruptSuccessorEvidence successor = await reader.ReadSuccessorAsync("", B10AbruptVerifyEvidence.Second, first, Names(B10AbruptVerifyEvidence.Second), TestContext.Current.CancellationToken);
        Assert.False(first.Invalid); Assert.False(successor.Invalid);
        var records = new List<ConsumedRecord>();
        foreach ((string arm, string original, string resend, string instance) in new[]
        {
            ("same_instance", "s4", "t4", first.Pod), ("cross_restart", "c4", "r4", successor.Pod)
        })
        {
            AbruptAttempt attempt = first.Attempts[original]; Assert.Equal(arm, attempt.Arm);
            records.Add(new("document-events", 0, records.Count, attempt.Key, first.Pod, original, attempt.Sequence, [], attempt.PayloadSha256));
            records.Add(new("document-events", 0, records.Count, attempt.Key, instance, resend, attempt.Sequence, [], attempt.PayloadSha256));
        }
        var data = new JsonObject { ["arms"] = JsonSerializer.SerializeToNode(AbruptClassification.Evaluate(first, successor, records, true, 100), WireOptions) };
        var input = new B10FinalInput(1) { First = first, Successor = successor };
        Assert.True(Accepts(input, data));
        return (input, data);
    }
}
#pragma warning restore CA1707
