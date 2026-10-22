#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using Parchwell.Runner.Run.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Deployment;

public sealed class DeploymentPreflightTests
{
    [Fact]
    public async Task Given_MissingCommittedServiceTags_When_Preflight_Then_ItRejectsBeforeAnyProductionTransportCanBeConstructed()
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> runtime = fixture.Input("C0");
        runtime["synthetic"] = false;
        runtime["service_tags_file"] = Path.Combine(fixture.Directory, "missing-committed-service-tags.json");
        await Assert.ThrowsAsync<FileNotFoundException>(() => DeploymentPreflight.ValidateAsync(JsonSerializer.SerializeToElement(runtime), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_SyntheticRuntime_When_ProductionPreflight_Then_ItRejects()
    {
        using var fixture = new DeploymentFixture();
        await Assert.ThrowsAsync<InvalidDataException>(() => DeploymentPreflight.ValidateAsync(JsonSerializer.SerializeToElement(fixture.Input("C0")), TestContext.Current.CancellationToken));
    }
}
