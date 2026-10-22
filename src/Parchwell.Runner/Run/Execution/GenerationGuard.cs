using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed class GenerationGuard(ITerraformEnvironment environment, TimeProvider clock)
{
    public async Task<bool> DestroyAsync(EnvironmentGeneration expected, bool onlyExpired, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (onlyExpired && clock.GetUtcNow() <= expected.ExpiresAt) return false;
        EnvironmentGeneration? current = await environment.ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
        if (current is null) return true;
        EnsureSame(expected, current);
        DestroyPlan plan = await environment.PlanDestroyAsync(expected, cancellationToken).ConfigureAwait(false);
        EnsureSame(expected, plan.Generation);
        current = await environment.ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
        if (current is null) return true;
        EnsureSame(expected, current);
        await environment.ApplyDestroyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        if (await environment.ReadGenerationAsync(cancellationToken).ConfigureAwait(false) is not null)
            throw new InvalidDataException("destroy_state_not_empty");
        return true;
    }

    public static void EnsureSame(EnvironmentGeneration expected, EnvironmentGeneration actual)
    {
        if (expected != actual) throw new InvalidDataException("environment_generation_changed");
    }
}
