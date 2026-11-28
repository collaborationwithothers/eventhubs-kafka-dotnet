// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed partial class VerifyInputCapacityTests
{
    [Trait("Tier", "Slow")]
    [Fact]
    public async Task Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_Then_PublishesExactCompleteCounts()
    {
        using var fixture = new HostEvidenceBuilder();
        string root = RepositoryRoot();
        using JsonDocument deployment = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/operations/a01-runtime-limits.json")));
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "docs/operations/a01-runtime-limits.json")).Build();
        configuration.GetSection("verifier_shared").Bind(fixture.Options);
        configuration.GetSection("verifier_verify").Bind(fixture.Options);
        fixture.Options.ConsumedMetadata!.Enabled = true;
        fixture.Options.ConsumedMetadata.MaxRowBytes = 1;
        fixture.Options.ConsumedMetadata.MaxChunkBytes = 1;
        fixture.Options.ConsumedMetadata.MaxBytes = 1;
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
        string run = fixture.Options.RunId!, phase = fixture.Options.Phase!;
        int repeat = fixture.Options.Repeat!.Value;
        string instance = fixture.Options.InstanceId!;
        string[] metadataReads =
        [
            PhasePaths.Registration(run, phase, repeat, instance),
            PhasePaths.StartLineCompletion(run, phase, repeat),
            PhasePaths.StartLine(run, phase, repeat),
            PhasePaths.BrokerInventory(run, phase, repeat),
            PhasePaths.ProducerCompletion(run, phase, repeat, instance)
        ];
        Assert.Equal(metadataReads.Concat(PublicationReadOrder.Select(name =>
            PhasePaths.Publication(run, phase, repeat, instance, name))), fixture.Reads);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);

        fixture.Options.MaxInputEntries = deployedCapacity;
        Assert.Equal(4194304, deployedCapacity);
        Assert.True(fixture.Inputs.Values.Sum(bytes => (long)bytes!.Length) + fixture.Note!.Length <= fixture.Options.MaxTotalInputBytes);
        Assert.All(fixture.Inputs.Values, bytes => Assert.True(bytes!.LongLength <= fixture.Options.MaxArtifactBytes));
        await AssertCompleteAsync(fixture, attempts);
        using JsonDocument capture = JsonDocument.Parse(Assert.Single(fixture.MetadataObjects).Value);
        JsonElement captureRoot = capture.RootElement;
        Assert.False(captureRoot.GetProperty("complete").GetBoolean());
        Assert.Equal(attempts, captureRoot.GetProperty("observed_rows").GetInt64());
        Assert.Equal(0, captureRoot.GetProperty("captured_rows").GetInt64());
        Assert.Equal(0, captureRoot.GetProperty("first_missing_ordinal").GetInt64());
        JsonElement gap = Assert.Single(captureRoot.GetProperty("gaps").EnumerateArray());
        Assert.Equal("row_limit", gap.GetProperty("code").GetString());
        Assert.Equal(0, gap.GetProperty("ordinal").GetInt64());
        Assert.Equal(deployment.RootElement.GetProperty("verifier_verify").GetProperty("MaxInventoryEntries").GetInt64(), fixture.Options.MaxInventoryEntries);
    }
}
#pragma warning restore CA1707
