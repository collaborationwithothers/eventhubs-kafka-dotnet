using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.LoadGen.Run.Evidence;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RequestCapabilityCatalogueTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void Given_CanonicalClaims_When_Matching_Then_ContainsUniquePinnedRequirements()
    {
        Assert.Equal(7, RequestCapabilityCatalogue.Claims.Length);
        Assert.Equal(RequestCapabilityCatalogue.Claims.Length,
            RequestCapabilityCatalogue.Claims.Select(claim => claim.Requirement).Distinct(StringComparer.Ordinal).Count());

        foreach (RequestCapabilityClaim claim in RequestCapabilityCatalogue.Claims)
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(claim, JsonOptions));
            Assert.True(RequestCapabilityCatalogue.Matches(document.RootElement));
        }
    }

    [Theory]
    [InlineData("source_urls")]
    [InlineData("source_facts")]
    [InlineData("inference")]
    public void Given_SameStatusClaimWithCorruptedProvenance_When_Matching_Then_Rejects(string field)
    {
        RequestCapabilityClaim claim = RequestCapabilityCatalogue.Claims[0];
        string json = JsonSerializer.Serialize(claim, JsonOptions);
        JsonObject candidate = JsonNode.Parse(json)!.AsObject();
        switch (field)
        {
            case "source_urls":
                candidate[field] = new JsonArray("https://example.invalid/v2.16.0");
                break;
            case "source_facts":
                candidate[field] = "An unrelated source says something different.";
                break;
            case "inference":
                candidate[field] = "The substituted inference claims request attribution is complete.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field));
        }

        using JsonDocument corrupted = JsonDocument.Parse(candidate.ToJsonString());
        Assert.Equal("unsupported", corrupted.RootElement.GetProperty("status").GetString());
        Assert.False(RequestCapabilityCatalogue.Matches(corrupted.RootElement));
    }
}
