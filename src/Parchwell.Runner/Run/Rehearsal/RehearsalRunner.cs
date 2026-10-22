using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Rehearsal;

public sealed class RehearsalRunner(TimeProvider clock, RehearsalScenarioFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public async Task<int> ExecuteAsync(string fixtureDirectory, string outputDirectory, CancellationToken cancellationToken)
    {
        WireFixtures fixtures = await WireFixtures.LoadAsync(fixtureDirectory, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(outputDirectory);
        ScenarioReport happy = await RunAsync("happy", null, "pass", false).ConfigureAwait(false);
        Require(happy.Phases == 3 && happy.ProbeRepeats == 5 && happy.FullC0Repeats == 5 && happy.FullC3Repeats == 5 && happy.Applies == 3 && happy.Destroys == 3 && happy.Result == "passed", "happy_totals");
        var reports = new List<ScenarioReport> { happy };
        foreach (string variant in new[] { "nonqualifying", "unusable" })
        {
            ScenarioReport stopped = await RunAsync(variant, null, variant, false).ConfigureAwait(false);
            Require(stopped.Phases == 1 && stopped.ProbeRepeats == 5 && stopped.FullC0Repeats == 0 && stopped.Applies == 1 && stopped.Destroys == 1, variant);
            reports.Add(stopped);
        }
        ScenarioReport over = await RunAsync("cost_over_limit", null, "pass", true).ConfigureAwait(false);
        Require(over.Applies == 0 && over.Destroys == 0, "cost_before_apply"); reports.Add(over);
        string[] failures = ["apply", "outputs", "preflight", "watch", "producer_create", "admission", "comparison", "start_line", "release", "loadgen", "publication", "verify", "watch_complete", "extra_producer", "export", "phase_export", "cleanup_receipt", "gate", "destroy_plan", "destroy"];
        foreach (string failure in failures)
        {
            ScenarioReport stopped = await RunAsync("failure_" + failure, failure, "pass", false).ConfigureAwait(false);
            Require(stopped.Result == "failed" && stopped.Applies == 1 && stopped.FullC0Repeats == 0, failure + "_stops");
            Require(stopped.DestroyAttempts == 1, failure + "_cleanup_attempt");
            Require(Array.IndexOf(stopped.Trace, "export_started") >= 0 && Array.IndexOf(stopped.Trace, "export_started") < Array.IndexOf(stopped.Trace, "destroy_plan"), failure + "_export_before_destroy");
            reports.Add(stopped);
        }
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "rehearsal-cases.json"), JsonSerializer.SerializeToUtf8Bytes(reports, JsonOptions), cancellationToken).ConfigureAwait(false);
        var summary = new { phases = happy.Phases, probe_repeats = happy.ProbeRepeats, full_C0_repeats = happy.FullC0Repeats,
            full_C3_repeats = happy.FullC3Repeats, applies = happy.Applies, destroys = happy.Destroys,
            azure_calls = 0, replacement_repeats = 0, result = happy.Result, negative_cases = reports.Count - 1 };
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "summary.json"), JsonSerializer.SerializeToUtf8Bytes(summary, JsonOptions), cancellationToken).ConfigureAwait(false);
        return 0;

        async Task<ScenarioReport> RunAsync(string name, string? failure, string gateVariant, bool overLimit)
        {
            string root = Path.Combine(outputDirectory, name); Directory.CreateDirectory(root);
            var trace = new List<string>();
            RehearsalScenario scenario = factory.Create(fixtures, failure, gateVariant, trace);
            RunLedger ledger = scenario.Ledger;
            RehearsalEnvironment environment = scenario.Environment;
            PhaseExecutor executor = scenario.Executor;
            var results = new List<PhaseExecution>();
            string result = "passed";
            try
            {
                foreach (string phase in RunLedger.PhaseNames)
                {
                    string env = "rehearsal-" + phase.ToLowerInvariant();
                    PhaseExecution executed = await executor.ExecuteAsync(new(phase, env, fixtures.SourceCommit, Path.Combine(root, phase), Costs(overLimit), phase == "probe"), cancellationToken).ConfigureAwait(false);
                    results.Add(executed);
                    if (executed.Result != "completed") { result = "failed"; break; }
                    if (phase == "probe")
                    {
                        byte[] bytes = fixtures.Get("gate-" + gateVariant + ".json");
                        var gate = new GateExecution(new(0, failure == "gate" ? "" : Encoding.UTF8.GetString(fixtures.Get("gate-receipt.txt")), ""), bytes);
                        ProbeGateDecision decision = ProbeGateRunner.Inspect(gate, fixtures.SourceCommit, executed.Repeats.Select(item => item.Identity).ToArray());
                        await ledger.RecordGateAsync(new(decision.Outcome, Convert.ToHexStringLower(SHA256.HashData(bytes)), fixtures.SourceCommit), cancellationToken).ConfigureAwait(false);
                        if (!decision.AllowsFullPhases) { result = "blocked_by_gate"; break; }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or InvalidDataException or OperationCanceledException) { result = "failed"; }
            int repeatCount(string phase) => results.Where(item => item.Phase == phase).Sum(item => item.Repeats.Count);
            return new(name, results.Count, repeatCount("probe"), repeatCount("C0"), repeatCount("C3"),
                environment.Applies, environment.Destroys, environment.Trace.Count(item => item == "destroy_plan"), result, trace.ToArray());
        }
    }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException("rehearsal_assertion_" + code); }
    private CostInput Costs(bool overLimit) => new("GBP", "A01", overLimit ? 14m : 0m, ["https://example.invalid/synthetic-baseline"],
        RunLedger.PhaseNames.ToDictionary(name => name, _ => new PhaseEstimate(2m, 1m), StringComparer.Ordinal), 1m,
        [new("https://example.invalid/synthetic-estimate", clock.GetUtcNow(), "synthetic phase", 1m, 3m)]);
    private sealed record ScenarioReport(string Name, int Phases, int ProbeRepeats, int FullC0Repeats, int FullC3Repeats,
        int Applies, int Destroys, int DestroyAttempts, string Result, string[] Trace);
}
