using System.Text.Json;

namespace Parchwell.Runner.Run.Contracts;

public sealed record EnvironmentGeneration(string RunId, DateTimeOffset ExpiresAt, string Lineage, long Serial);
public sealed record DestroyPlan(string Path, EnvironmentGeneration Generation);

public interface ITerraformEnvironment
{
    Task<EnvironmentGeneration?> ReadGenerationAsync(CancellationToken cancellationToken);
    Task ApplyAsync(string environmentRunId, DateTimeOffset startedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<JsonElement> ReadOutputsAsync(CancellationToken cancellationToken);
    Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken);
    Task<DestroyPlan> PlanDestroyAsync(EnvironmentGeneration expected, CancellationToken cancellationToken);
    Task ApplyDestroyPlanAsync(DestroyPlan plan, CancellationToken cancellationToken);
}
