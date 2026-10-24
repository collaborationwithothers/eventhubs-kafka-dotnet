using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707

/// <summary>Asserts the published diagnostic contract using independently authored synthetic input changes.</summary>
public sealed class VerifyRunDiagnosticTests
{
    [Theory]
    [InlineData("scenario_id")]
    [InlineData("configuration_id")]
    [InlineData("run_id")]
    [InlineData("instance_id")]
    [InlineData("expected_instance_id")]
    [InlineData("client_id")]
    public async Task Given_ParseableForeignStartIdentity_When_Verifying_Then_PublishesSemanticSevenWithStartLineAttribution(string field)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject note = Note(fixture);
        note["identity"]![field] = "synthetic-foreign";
        fixture.Note = Encoding.UTF8.GetBytes(note.ToJsonString());
        await AssertPublishedFailure(fixture, "invalid", 7, "check_7_failed", "start-line", "identity");
    }

    [Theory]
    [InlineData("topic", "topic")]
    [InlineData("empty", "partition_highs")]
    [InlineData("duplicate", "partition_highs")]
    [InlineData("negative", "partition_highs")]
    public async Task Given_ParseableInvalidStartBoundary_When_Verifying_Then_PublishesSemanticSevenWithExactField(string kind, string field)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject note = Note(fixture);
        if (kind == "topic") note["topic"] = "synthetic-other-topic";
        if (kind == "empty") note["partition_highs"] = new JsonArray();
        if (kind == "duplicate") note["partition_highs"]!.AsArray().Add(note["partition_highs"]![0]!.DeepClone());
        if (kind == "negative") note["partition_highs"]![0]!["high"] = -1;
        fixture.Note = Encoding.UTF8.GetBytes(note.ToJsonString());
        await AssertPublishedFailure(fixture, "invalid", 7, "check_7_failed", "start-line", field);
    }

    [Theory]
    [InlineData("missing", "missing_input", null)]
    [InlineData("malformed", "malformed_json", null)]
    [InlineData("schema", "unsupported_schema_version", "schema_version")]
    [InlineData("kind", "wrong_json_kind", "partition_highs[0].high")]
    public async Task Given_StartNoteParserFailure_When_Verifying_Then_PreservesParserCodePathAndNullableMetadata(string kind, string code, string? field)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject note = Note(fixture);
        if (kind == "schema") note["schema_version"] = 2;
        if (kind == "kind") note["partition_highs"]![0]!["high"] = "synthetic-wrong-kind";
        fixture.Note = kind switch
        {
            "missing" => null,
            "malformed" => Encoding.UTF8.GetBytes("{synthetic malformed note"),
            _ => Encoding.UTF8.GetBytes(note.ToJsonString())
        };
        fixture.OmitStartLineNote = kind == "missing";
        await AssertPublishedFailure(fixture, "invalid", 7, code, fixture.Options.StartLineInputName!, field);
    }

    [Theory]
    [InlineData(false, 8, "check_8_failed")]
    [InlineData(true, 9, "check_9_failed")]
    public async Task Given_MissingEndPartitionOrRegressingEnd_When_Verifying_Then_PublishesTopicDiagnosticAndNoCounts(bool regression, int check, string code)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject note = Note(fixture);
        if (regression) note["partition_highs"]![0]!["high"] = 2;
        else note["partition_highs"]!.AsArray().Add(new JsonObject { ["partition"] = 1, ["high"] = 0 });
        fixture.Note = Encoding.UTF8.GetBytes(note.ToJsonString());
        await AssertPublishedFailure(fixture, "invalid", check, code, "document-events", "partition_highs");
    }

    [Theory]
    [InlineData(ErrorCode.Local_AutoOffsetReset)]
    [InlineData(ErrorCode.OffsetOutOfRange)]
    public async Task Given_NativeAssignedOffsetOutOfRange_When_Verifying_Then_PublishesTopicNineWithNullField(ErrorCode error)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.Assign = _ => throw new KafkaException(new Error(error));
        await AssertPublishedFailure(fixture, "invalid", 9, "check_9_failed", "document-events", null);
    }

    [Fact]
    public async Task Given_SafetyStop_When_Verifying_Then_PublishesTopicTenWithNullFieldAndStopDuration()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.ConsumeRecord = null;
        fixture.Kafka.Poll = () =>
        {
            if (fixture.Kafka.Authentication.InstalledTokenExpiresOn is null) fixture.Kafka.Refresh();
            else fixture.Kafka.Clock.Advance(1000);
        };
        await AssertPublishedFailure(fixture, "incomplete", 10, "check_10_failed", "document-events", null);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(1000, output.RootElement.GetProperty("drain_elapsed_at_stop").GetInt64());
        Assert.True(output.RootElement.GetProperty("drain_elapsed_at_stop_lower_bound").GetBoolean());
        Assert.False(output.RootElement.TryGetProperty("drain_time", out _));
    }

    [Theory]
    [InlineData("token")]
    [InlineData("metadata")]
    [InlineData("watermark")]
    [InlineData("empty-metadata")]
    public async Task Given_QueryFailure_When_Verifying_Then_PublishesTopicElevenWithNullFieldWithoutInventedBoundaries(string phase)
    {
        using var fixture = new HostEvidenceBuilder();
        if (phase == "token") fixture.Kafka.Poll = () => fixture.Kafka.Clock.Advance(1000);
        if (phase == "metadata") fixture.Kafka.ReadMetadata = () => throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
        if (phase == "watermark") fixture.Kafka.Query = _ => throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
        if (phase == "empty-metadata") fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata();
        await AssertPublishedFailure(fixture, "incomplete", 11, "check_11_failed", "document-events", null);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.False(output.RootElement.TryGetProperty("end_line", out _));
        Assert.False(output.RootElement.TryGetProperty("drain_time", out _));
        Assert.False(output.RootElement.TryGetProperty("drain_elapsed_at_stop", out _));
    }

    private static JsonObject Note(HostEvidenceBuilder fixture) => JsonNode.Parse(fixture.Note!)!.AsObject();

    private static async Task AssertPublishedFailure(HostEvidenceBuilder fixture, string status, int check,
        string code, string artifact, string? field)
    {
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Creates);
        Assert.Equal(fixture.Options.ResultOutputName, fixture.OutputName);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement root = output.RootElement;
        Assert.Equal(status, root.GetProperty("status").GetString());
        Assert.Equal(new[] { check }, root.GetProperty("failed_checks").EnumerateArray().Select(item => item.GetInt32()));
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.False(root.TryGetProperty(count, out _));
        JsonElement diagnostic = Assert.Single(root.GetProperty("diagnostics").EnumerateArray(),
            item => item.GetProperty("check").GetInt32() == check);
        Assert.Equal(6, diagnostic.EnumerateObject().Count());
        Assert.Equal(check, diagnostic.GetProperty("check").GetInt32());
        Assert.Equal(code, diagnostic.GetProperty("code").GetString());
        Assert.Equal(artifact, diagnostic.GetProperty("artifact").GetString());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("row").ValueKind);
        if (field is null) Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("field").ValueKind);
        else Assert.Equal(field, diagnostic.GetProperty("field").GetString());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("snippet").ValueKind);
    }
}

#pragma warning restore CA1707
