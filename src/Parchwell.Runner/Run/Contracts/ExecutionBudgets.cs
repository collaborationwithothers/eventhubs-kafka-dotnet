using System.Text.Json;

namespace Parchwell.Runner.Run.Contracts;

/// <summary>Execution ceilings read from the retained runtime limits document.</summary>
public sealed class ExecutionBudgets
{
    private readonly JsonElement values;
    private ExecutionBudgets(JsonElement values) { this.values = values; }
    public int Number(string name) => values.GetProperty(name).GetInt32();
    public TimeSpan Seconds(string name) => TimeSpan.FromSeconds(Number(name));
    public static ExecutionBudgets Read(JsonElement limits)
    {
        if (limits.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("limits_schema");
        JsonElement execution = limits.GetProperty("execution");
        foreach (JsonProperty property in execution.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.Number && property.Name != "replacement_repeats" &&
                (!property.Value.TryGetDecimal(out decimal number) || number <= 0)) throw new InvalidDataException("limits_positive_required");
        if (execution.GetProperty("phase_count").GetInt32() != 3 || execution.GetProperty("repeats_per_phase").GetInt32() != 5 ||
            execution.GetProperty("replacement_repeats").GetInt32() != 0 || execution.GetProperty("item_limit_gbp").GetDecimal() != 15m)
            throw new InvalidDataException("experiment_cardinality_or_cost_changed");
        foreach (string required in new[] { "repeat_seconds", "phase_seconds", "apply_seconds", "destroy_seconds", "export_seconds", "api_seconds",
            "loadgen_job_seconds", "verify_job_seconds", "start_line_job_seconds", "gate_job_seconds", "kubernetes_poll_seconds", "admission_seconds", "watch_events" })
            if (execution.GetProperty(required).GetInt32() <= 0) throw new InvalidDataException("execution_budget_missing");
        if (execution.GetProperty("phase_seconds").GetInt32() <= checked(execution.GetProperty("apply_seconds").GetInt32() +
            execution.GetProperty("destroy_seconds").GetInt32() + execution.GetProperty("export_seconds").GetInt32())) throw new InvalidDataException("phase_cleanup_reserve");
        return new(execution.Clone());
    }
}
