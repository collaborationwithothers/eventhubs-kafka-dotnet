#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using Xunit;

namespace Parchwell.ArchitectureTests.DeploymentContracts;

public sealed class ChartContractTests
{
    private static string RepositoryRoot =>
        Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT")
        ?? throw new InvalidOperationException("Missing repository root. Run ./scripts/verify-dotnet.sh.");

    [Fact]
    public void Given_ChartSchema_When_Read_Then_OnlyA01AndB04AndClosedRuntimeAreAccepted()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "charts/parchwell/values.schema.json")));
        JsonElement properties = schema.RootElement.GetProperty("properties");
        Assert.Collection(properties.GetProperty("scenario_id").GetProperty("enum").EnumerateArray(),
            value => Assert.Equal("A01", value.GetString()),
            value => Assert.Equal("B04", value.GetString()));
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        JsonElement runtime = properties.GetProperty("runtime");
        Assert.False(runtime.GetProperty("additionalProperties").GetBoolean());
        string?[] required = runtime.GetProperty("required").EnumerateArray().Select(property => property.GetString()).ToArray();
        JsonElement runtimeProperties = runtime.GetProperty("properties");
        Assert.DoesNotContain("service_tags", required);
        Assert.False(runtimeProperties.TryGetProperty("service_tags", out _));
        foreach (string endpoint in new[] { "eventhubs_ip", "blob_ip" })
        {
            Assert.Contains(endpoint, required);
            Assert.True(runtimeProperties.TryGetProperty(endpoint, out _));
        }
    }
}
