#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Contracts;

public sealed class ExecutionBudgetsTests
{
    public static IEnumerable<object[]> PositiveBoundaries()
    {
        JsonObject execution = ReadLimits()["execution"]!.AsObject();
        foreach ((string name, JsonNode? value) in execution)
        {
            if (name is "phase_count" or "repeats_per_phase" or "replacement_repeats" or "item_limit_gbp" or "phase_seconds" ||
                value is not JsonValue number || !number.TryGetValue<long>(out _)) continue;
            foreach (int adjacent in new[] { -1, 0, 1 }) yield return [name, adjacent];
        }
    }

    [Theory]
    [MemberData(nameof(PositiveBoundaries))]
    public void Given_ExecutionBoundAroundZero_When_Read_Then_OnlyPositiveValueIsAccepted(string name, int adjacent)
    {
        JsonObject limits = ReadLimits();
        limits["execution"]![name] = adjacent;
        JsonElement input = JsonSerializer.SerializeToElement(limits);
        if (adjacent > 0) Assert.Equal(adjacent, ExecutionBudgets.Read(input).Number(name));
        else Assert.Equal("limits_positive_required", Assert.Throws<InvalidDataException>(() => ExecutionBudgets.Read(input)).Message);
    }

    [Theory]
    [InlineData(2099, false)]
    [InlineData(2100, false)]
    [InlineData(2101, true)]
    public void Given_PhaseDeadlineAroundCleanupReserve_When_Read_Then_TimeMustRemainForExecution(int seconds, bool accepted)
    {
        JsonObject limits = ReadLimits();
        JsonNode execution = limits["execution"]!;
        Assert.Equal(2100, execution["apply_seconds"]!.GetValue<int>() + execution["export_seconds"]!.GetValue<int>() + execution["destroy_seconds"]!.GetValue<int>());
        execution["phase_seconds"] = seconds;
        JsonElement input = JsonSerializer.SerializeToElement(limits);
        if (accepted) Assert.Equal(TimeSpan.FromSeconds(seconds), ExecutionBudgets.Read(input).Seconds("phase_seconds"));
        else Assert.Equal("phase_cleanup_reserve", Assert.Throws<InvalidDataException>(() => ExecutionBudgets.Read(input)).Message);
    }

    private static JsonObject ReadLimits() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")))!.AsObject();
}
