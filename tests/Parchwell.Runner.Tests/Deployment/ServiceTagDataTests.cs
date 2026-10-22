#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Parchwell.Runner.Run.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Deployment;

public sealed class ServiceTagDataTests
{
    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("192.0.2.1/33")]
    [InlineData("not-a-cidr")]
    public void Given_InvalidOrUnrestrictedPrefix_When_Validated_Then_Fails(string prefix)
    {
        using var fixture = new DeploymentFixture();
        string path = Path.Combine(fixture.Directory, "tags.json");
        string json = File.ReadAllText(path).Replace("192.0.2.0/24", prefix, StringComparison.Ordinal);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => ServiceTagData.Validate(doc.RootElement));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void Given_PrefixCountBoundary_When_Validated_Then_TheEntireSetIsAcceptedOrRejected(int count, bool accepted)
    {
        using var fixture = new DeploymentFixture();
        JsonNode data = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Directory, "tags.json")))!;
        string[] prefixes = Enumerable.Range(0, count).Select(index => "2001:db8::" + index.ToString("x", CultureInfo.InvariantCulture) + "/128").Order(StringComparer.Ordinal).ToArray();
        // The all-zero address has the canonical compressed representation.
        if (count > 0) prefixes = prefixes.Select(prefix => prefix == "2001:db8::0/128" ? "2001:db8::/128" : prefix).Order(StringComparer.Ordinal).ToArray();
        data["tags"]!["AzureActiveDirectory"]!["address_prefixes"] = JsonSerializer.SerializeToNode(prefixes);
        using JsonDocument document = JsonDocument.Parse(data.ToJsonString());
        if (accepted) ServiceTagData.Validate(document.RootElement);
        else Assert.Throws<InvalidDataException>(() => ServiceTagData.Validate(document.RootElement));
    }

    [Fact]
    public void Given_CommittedDataUnavailable_When_Validated_Then_NoSyntheticFallbackExists()
    {
        using JsonDocument doc = JsonDocument.Parse("{\"schema_version\":1,\"source_url\":\"https://download.microsoft.com/missing.json\",\"retrieved_utc\":\"2026-10-22T00:00:00Z\",\"source_sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"change_number\":1,\"tags\":{\"AzureActiveDirectory\":{\"change_number\":1,\"address_prefixes\":[]},\"AzureMonitor\":{\"change_number\":1,\"address_prefixes\":[]}}}");
        Assert.Throws<InvalidDataException>(() => ServiceTagData.Validate(doc.RootElement));
    }
}
