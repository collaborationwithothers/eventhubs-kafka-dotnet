#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests;

public sealed class ChartContractTests
{
    [Fact]
    public void Given_ChartSchema_When_Read_Then_OnlyV1A01AndClosedRuntimeAreAccepted()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "charts/parchwell/values.schema.json")));
        JsonElement properties = schema.RootElement.GetProperty("properties");
        Assert.Equal(1, properties.GetProperty("contract_version").GetProperty("const").GetInt32());
        Assert.Equal("A01", properties.GetProperty("scenario_id").GetProperty("const").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.False(properties.GetProperty("runtime").GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Given_NetworkPolicyTemplate_When_Inspected_Then_DnsSelectorsAreCombinedAndNoWildcardExists()
    {
        string template = File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "charts/parchwell/templates/networkpolicies.yaml"));
        Assert.Contains("namespaceSelector:", template, StringComparison.Ordinal);
        Assert.Contains("          podSelector:", template, StringComparison.Ordinal);
        Assert.DoesNotContain("- podSelector:", template, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0/0", template, StringComparison.Ordinal);
        Assert.DoesNotContain("::/0", template, StringComparison.Ordinal);
    }
}
