using System.Text;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class C3HandlerTests
{
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Given_CoherentC3Artifacts_When_RealHandlerRuns_Then_RecordsDiagnosticsWithoutProbeOrChangedCounts()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.ConfigurationId = "C3";
        fixture.Options.Phase = "C3";
        fixture.Options.ExpectedInstanceId = "synthetic-c5-c3-r1-loadgen";
        fixture.Options.JobName = "synthetic-c5-c3-r1-verify";
        fixture.Options.RepeatPrefix = "synthetic-c5/C3/r1/";
        fixture.Options.PhasePrefix = "synthetic-c5/C3/";
        fixture.Options.StartLineInputName = "synthetic-c5/C3/r1/start-line.json";
        fixture.Options.StartLineOutputName = fixture.Options.StartLineInputName;
        fixture.Options.BrokerInventoryOutputName = "synthetic-c5/C3/r1/broker-inventory.json";
        fixture.Options.ResultOutputName = "synthetic-c5/C3/r1/result.json";
        fixture.Options.ArtifactPrefix = "synthetic-c5/C3/r1/producer/";
        foreach (string name in fixture.Inputs.Keys.ToArray())
            fixture.Inputs[name] = C3Bytes(fixture.Inputs[name]!);
        fixture.Note = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(C3Bytes(fixture.Note!))
            .Replace("synthetic-c5-c0-r1-loadgen", "synthetic-c5-c3-r1-loadgen", StringComparison.Ordinal));
        var capabilities = fixture.Json("capabilities.json");
        var loaded = await fixture.Loader.LoadAsync("A01", TestContext.Current.CancellationToken);
        capabilities["resolved_configuration"] = JsonSerializer.SerializeToNode(
            fixture.Resolver.Resolve(loaded).Configurations["C3"].Roles[KafkaClientRole.LoadGenProducer], WireOptions);
        fixture.Set("capabilities.json", capabilities);
        int exitCode = await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.True(exitCode == 0, Encoding.UTF8.GetString(fixture.Output ?? []));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        Assert.Equal("C3", result.GetProperty("identity").GetProperty("configuration_id").GetString());
        Assert.Equal("complete_valid", result.GetProperty("status").GetString());
        Assert.Equal(1, result.GetProperty("ok").GetInt64());
        Assert.False(result.TryGetProperty("probe_observation", out _));
        JsonElement recorded = result.GetProperty("a01_recorded_evidence");
        Assert.Empty(recorded.GetProperty("delivery_error_codes").EnumerateArray());
        Assert.Equal(3, recorded.GetProperty("windows").GetArrayLength());
        Assert.Equal(["admin-dispose", "consumer-dispose", "create"], fixture.Events.TakeLast(3));
    }

    private static byte[] C3Bytes(byte[] bytes) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)
        .Replace("\"C0\"", "\"C3\"", StringComparison.Ordinal));
}
#pragma warning restore CA1707
