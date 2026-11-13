using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Trait("Tier", "Docker")]
public sealed class B10FinalEvidenceContractTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
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
#pragma warning restore CA1707
