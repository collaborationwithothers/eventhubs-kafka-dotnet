using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Rehearsal;

public sealed record RehearsalScenario(RunLedger Ledger, RehearsalEnvironment Environment, PhaseExecutor Executor);

/// <summary>DI-registered composition factory. Its graph contains no external process or Azure transport.</summary>
public sealed class RehearsalScenarioFactory(TimeProvider clock)
{
    public RehearsalScenario Create(WireFixtures fixtures, string? failure, string gateVariant, List<string> trace)
    {
        var store = new RehearsalArtifactStore(failure, trace);
        var ledger = new RunLedger(store, clock);
        var environment = new RehearsalEnvironment(failure, trace);
        var cluster = new RehearsalCluster(fixtures, failure, gateVariant, trace);
        var executor = new PhaseExecutor(ledger, environment, new RepeatExecutor(cluster, clock),
            new EvidenceExporter(store, clock), new GenerationGuard(environment, clock), clock);
        return new(ledger, environment, executor);
    }
}
