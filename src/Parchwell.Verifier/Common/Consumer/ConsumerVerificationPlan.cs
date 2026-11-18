namespace Parchwell.Verifier.Common.Consumer;

/// <summary>
/// What the verifier expects of one consumer pair. <paramref name="WindowMs"/> and <paramref name="SlowMessageMode"/> are set together,
/// for a scenario whose subject processes one slow message, and absent for every other plan.
/// </summary>
internal sealed record ConsumerVerificationPlan(int? KillCount, int CompletionDeadlineMs,
    IReadOnlyDictionary<string, string> SubjectSettings, IReadOnlyDictionary<string, string> WitnessSettings,
    int? WindowMs = null, string? SlowMessageMode = null);

internal interface IConsumerVerificationPlanSource
{
    bool TryGetPlan(string scenarioId, string configurationId, out ConsumerVerificationPlan? plan);
}

internal static class ConsumerPlanValidation
{
    internal static bool IsValid(ConsumerVerificationPlan? plan, VerifierHostOptions options)
    {
        if (plan is null || plan.KillCount is < 1 or > 1000000 || plan.CompletionDeadlineMs is < 1000 or > 3600000 ||
            plan.SubjectSettings is null || plan.WitnessSettings is null) return false;
        if (plan.WindowMs.HasValue != (plan.SlowMessageMode is not null) || plan.WindowMs is < 1 ||
            plan.SlowMessageMode is not (null or "inline" or "pause")) return false;
        string[] reserved = ["bootstrap.servers", "client.id", "group.id"];
        if (reserved.Any(key => plan.SubjectSettings.ContainsKey(key) || plan.WitnessSettings.ContainsKey(key)) ||
            !plan.SubjectSettings.TryGetValue("isolation.level", out string? subject) ||
            !plan.WitnessSettings.TryGetValue("isolation.level", out string? witness) || subject != witness) return false;
        TimeSpan?[] budgets = [options.CleanupTimeout, options.IoTimeout, options.TokenTimeout,
            options.MetadataTimeout, options.WatermarkTimeout, options.BlobIoTimeout, options.SafetyStop];
        return budgets.All(value => value.HasValue) && VerifierHostOptionsValidator.TrySumBudgetTicks(
            budgets.Select(value => value!.Value).Append(TimeSpan.FromMilliseconds(plan.CompletionDeadlineMs)), out long ticks) &&
            options.ProcessLifetime?.Ticks > ticks;
    }

    internal static string GroupId(VerifierHostOptions options) => $"{options.RunId}-{options.Phase!.ToLowerInvariant()}-r{options.Repeat}-consumers";
    internal static bool IsConsumerScenario(string? scenario) => scenario is "C01" or "C03" or "C10";
}
