using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed record GateExecution(ProcessResult Process, byte[] Bytes);
public sealed record ProbeGateDecision(string Outcome, bool AllowsFullPhases);

public sealed class ProbeGateRunner(IProcessRunner process)
{
    public async Task<GateExecution> ExecuteAsync(string verifierExecutable,
        IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment,
        string outputPath, string sourceCommit, IReadOnlyList<WorkerIdentity> expected, CancellationToken cancellationToken)
    {
        ProcessResult execution = await process.RunAsync(new(verifierExecutable, arguments, TimeSpan.FromSeconds(90),
            Environment: environment), cancellationToken).ConfigureAwait(false);
        byte[] bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
        _ = Inspect(new(execution, bytes), sourceCommit, expected);
        return new(execution, bytes);
    }

    public static ProbeGateDecision Inspect(GateExecution gate, string sourceCommit, IReadOnlyList<WorkerIdentity> expected)
    {
        if (gate.Process.ExitCode != 0 || !WorkerOutcome.HasReceipt(gate.Process, "probe-gate", null, sourceCommit))
            throw new InvalidDataException("gate_execution");
        if (gate.Bytes.Length > 16777216 || expected.Count != 5 || expected.Select(item => item.RunId).Distinct(StringComparer.Ordinal).Count() != 5)
            throw new InvalidDataException("gate_repeat_count");
        using JsonDocument document = JsonDocument.Parse(gate.Bytes);
        JsonElement root = document.RootElement;
        string outcome = WorkerIdentity.Text(root, "outcome");
        if (root.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Text(root, "scenario_id") != "A01" ||
            WorkerIdentity.Text(root, "verifier_commit") != sourceCommit || outcome is not ("pass" or "untestable_at_build_scale" or "inconclusive"))
            throw new InvalidDataException("gate_identity");
        JsonElement repeats = root.GetProperty("repeats");
        // Root schema errors may yield an empty inconclusive set. They never authorize full phases.
        if (outcome != "pass") return new(outcome, false);
        if (repeats.GetArrayLength() != 5) throw new InvalidDataException("gate_repeat_count");
        var seen = new HashSet<WorkerIdentity>();
        foreach (JsonElement repeat in repeats.EnumerateArray())
        {
            WorkerIdentity identity = WorkerIdentity.Read(repeat.GetProperty("identity"));
            if (!expected.Contains(identity) || !seen.Add(identity) || !repeat.GetProperty("usable").GetBoolean())
                throw new InvalidDataException("gate_repeat_identity");
        }
        return new(outcome, true);
    }
}
