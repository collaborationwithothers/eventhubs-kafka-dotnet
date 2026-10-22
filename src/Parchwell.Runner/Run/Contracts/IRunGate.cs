using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Contracts;

// External executable transport: evaluation remains exclusively in the verifier.
public interface IRunGate
{
    Task<GateExecution> ExecuteAsync(IReadOnlyList<RepeatExecution> repeats, string sourceCommit,
        string outputDirectory, CancellationToken cancellationToken);
}
