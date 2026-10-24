using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifyInputCapacityTests
{
    [Fact]
    public async Task Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_Then_PublishesExactCompleteCounts()
    {
        using var fixture = new HostEvidenceBuilder();
        string root = RepositoryRoot();
        using JsonDocument deployment = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/operations/a01-runtime-limits.json")));
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "docs/operations/a01-runtime-limits.json")).Build();
        configuration.GetSection("verifier_shared").Bind(fixture.Options);
        configuration.GetSection("verifier_verify").Bind(fixture.Options);
        var scenario = await fixture.Loader.LoadAsync("A01", TestContext.Current.CancellationToken);
        int attempts = checked((int)scenario.Profile.Stages.Sum(stage => (long)stage.DurationSeconds * stage.OfferedAttemptsPerSecond));
        Assert.Equal(186000, attempts);
        Assert.Equal(attempts, fixture.Options.MaxInventoryEntries);
        Populate(fixture, attempts);
        long deployedCapacity = fixture.Options.MaxInputEntries!.Value;

        // Reproduce the live failure when inventory size is reused as the aggregate budget.
        fixture.Options.MaxInputEntries = fixture.Options.MaxInventoryEntries;
        InputLimitExceededException error = await Assert.ThrowsAsync<InputLimitExceededException>(() =>
            fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("The combined verifier inputs exceed the configured entry limit.", error.Message);
        Assert.Equal(["index.json", "ledger.jsonl"], fixture.Reads);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);

        fixture.Options.MaxInputEntries = deployedCapacity;
        Assert.Equal(4194304, deployedCapacity);
        Assert.True(fixture.Inputs.Values.Sum(bytes => (long)bytes!.Length) + fixture.Note!.Length <= fixture.Options.MaxTotalInputBytes);
        Assert.All(fixture.Inputs.Values, bytes => Assert.True(bytes!.LongLength <= fixture.Options.MaxArtifactBytes));
        await AssertCompleteAsync(fixture, attempts);
        Assert.Equal(deployment.RootElement.GetProperty("verifier_verify").GetProperty("MaxInventoryEntries").GetInt64(), fixture.Options.MaxInventoryEntries);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Given_InventoryBelowAtOrAboveItsIndependentCap_When_Verifying_Then_InputCapacityDoesNotBypassInventoryBound(int count, bool accepted)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.MaxInventoryEntries = 2;
        Populate(fixture, count);
        if (accepted) await AssertCompleteAsync(fixture, count);
        else
        {
            InputLimitExceededException error = await Assert.ThrowsAsync<InputLimitExceededException>(() =>
                fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Contains("inventory exceeds", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, fixture.Creates);
            Assert.Null(fixture.Output);
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Given_ParserCapacityBelowAtOrAboveLargestInput_When_Verifying_Then_IndependentEntryLimitRemainsEnforced(int adjustment, bool accepted)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.MaxInventoryEntries = 1;
        long largest = fixture.Inputs.Values.Where(bytes => bytes is { Length: > 0 })
            .SelectMany(bytes => Encoding.UTF8.GetString(bytes!).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Append(Encoding.UTF8.GetString(fixture.Note!))
            .Select(CountDocumentEntries).Max();
        fixture.Options.MaxInputEntries = largest + adjustment;
        if (accepted) await AssertCompleteAsync(fixture, 1);
        else
        {
            await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, fixture.Creates);
            Assert.Null(fixture.Output);
        }
    }

    private static async Task AssertCompleteAsync(HostEvidenceBuilder fixture, long attempts)
    {
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument result = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("complete_valid", result.RootElement.GetProperty("status").GetString());
        Assert.Empty(result.RootElement.GetProperty("failed_checks").EnumerateArray());
        Assert.Equal(attempts, result.RootElement.GetProperty("ok").GetInt64());
        foreach (string count in new[] { "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.Equal(0, result.RootElement.GetProperty(count).GetInt64());
        Assert.Equal(1, fixture.Creates);
        Assert.Equal("create", fixture.Events[^1]);
    }

    private static void Populate(HostEvidenceBuilder fixture, int count)
    {
        JsonObject row = fixture.Json("ledger.jsonl"), manifest = fixture.Json("manifest.json");
        JsonObject attempt = row["attempt"]!.AsObject();
        using (var ledger = new MemoryStream())
        {
            for (int i = 0; i < count; i++)
            {
                SetAttempt(attempt, i);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(row);
                ledger.Write(bytes);
                ledger.WriteByte((byte)'\n');
            }
            fixture.Inputs["ledger.jsonl"] = ledger.ToArray();
        }
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach ((string name, JsonNode? value) in manifest)
                {
                    writer.WritePropertyName(name);
                    if (name is "planned_attempt_count" or "attempt_count") writer.WriteNumberValue(count);
                    else if (name == "inventory")
                    {
                        writer.WriteStartArray();
                        for (int i = 0; i < count; i++)
                        {
                            SetAttempt(attempt, i);
                            attempt.WriteTo(writer);
                        }
                        writer.WriteEndArray();
                    }
                    else if (value is null) writer.WriteNullValue();
                    else value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            fixture.Inputs["manifest.json"] = stream.ToArray();
        }
        JsonObject index = fixture.Json("index.json");
        foreach (JsonNode? artifact in index["artifacts"]!.AsArray())
        {
            byte[] bytes = fixture.Inputs[artifact!["name"]!.GetValue<string>()]!;
            artifact["length"] = bytes.LongLength;
            artifact["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        fixture.Set("index.json", index);
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, count);
        long position = 0;
        fixture.Kafka.Position = _ => new Offset(position);
        fixture.ConsumeRecord = () =>
        {
            var record = HostEvidenceBuilder.Record();
            record.Offset = position;
            record.Message.Headers = new Headers
            {
                { "instance-id", Encoding.UTF8.GetBytes(fixture.Options.InstanceId!) },
                { "attempt-id", Encoding.UTF8.GetBytes(AttemptId(position)) },
                { "sequence", Encoding.UTF8.GetBytes(position.ToString(CultureInfo.InvariantCulture)) }
            };
            position++;
            return record;
        };
    }

    private static string AttemptId(long ordinal) => "synthetic-capacity-" + ordinal.ToString(CultureInfo.InvariantCulture);

    private static void SetAttempt(JsonObject attempt, int ordinal)
    {
        attempt["attempt_id"] = AttemptId(ordinal);
        attempt["ordinal"] = ordinal;
        attempt["sequence"] = ordinal;
    }

    private static long CountDocumentEntries(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Count(document.RootElement);

        static long Count(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().Sum(property => 1 + Count(property.Value)),
            JsonValueKind.Array => value.EnumerateArray().Sum(element => 1 + Count(element)),
            _ => 0
        };
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root is unavailable.");
    }
}
#pragma warning restore CA1707
