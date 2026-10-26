using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioPhasesTests
{
    [Theory]
    [InlineData("A01", "probe", "C0")]
    [InlineData("A01", "C0", "C0")]
    [InlineData("A01", "C3", "C3")]
    [InlineData("B04", "C0", "C0")]
    [InlineData("B04", "C1", "C1")]
    public void Given_ExecutableScenarioPhase_When_LookingUpConfiguration_Then_ReturnsExactMapping(
        string scenarioId, string phase, string expectedConfiguration)
    {
        Assert.True(ScenarioPhases.TryGetConfiguration(scenarioId, phase, out var configuration));
        Assert.Equal(expectedConfiguration, configuration);
    }

    [Theory]
    [InlineData("A01", "C1")]
    [InlineData("B04", "probe")]
    [InlineData("B04", "C3")]
    [InlineData("A02", "C0")]
    [InlineData(null, "C0")]
    [InlineData("B04", null)]
    [InlineData("b04", "C0")]
    public void Given_UnknownScenarioPhasePair_When_LookingUpConfiguration_Then_ReturnsFalse(string? scenarioId, string? phase)
    {
        Assert.False(ScenarioPhases.TryGetConfiguration(scenarioId, phase, out var configuration));
        Assert.Null(configuration);
    }
}
