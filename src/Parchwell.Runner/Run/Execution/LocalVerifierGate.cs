using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed class LocalVerifierGate(ProbeGateRunner runner, JsonElement runtime) : IRunGate
{
    public Task<GateExecution> ExecuteAsync(IReadOnlyList<RepeatExecution> repeats, string sourceCommit,
        string outputDirectory, CancellationToken cancellationToken)
    {
        if (repeats.Count != 5) throw new InvalidDataException("gate_repeat_count");
        string output = Path.Combine(outputDirectory, "gate.json");
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(WorkerIdentity.Text(runtime, "limits_file")));
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(environment, limits.RootElement.GetProperty("verifier_shared"), "Verifier__");
        Add(environment, limits.RootElement.GetProperty("verifier_gate"), "Verifier__");
        Add(environment, limits.RootElement.GetProperty("scenario_parsing"), "Verifier__ScenarioLoadOptions__");
        environment["Verifier__Mode"] = "probe-gate"; environment["Verifier__ScenarioId"] = "A01";
        environment["Verifier__VerifierCommit"] = sourceCommit; environment["PARCHWELL_SOURCE_COMMIT"] = sourceCommit;
        environment["Verifier__ScenarioRoot"] = WorkerIdentity.Text(runtime, "scenario_root"); environment["Verifier__GateOutputPath"] = output;
        for (int i = 0; i < repeats.Count; i++)
        {
            string index = i.ToString(CultureInfo.InvariantCulture);
            environment["Verifier__RepeatResultPaths__" + index] = Path.Combine(outputDirectory, repeats[i].Identity.RunId, "result.json");
            WorkerIdentity id = repeats[i].Identity;
            string prefix = "Verifier__ExpectedRepeats__" + index + "__";
            environment[prefix + "ScenarioId"] = id.ScenarioId; environment[prefix + "ConfigurationId"] = id.ConfigurationId;
            environment[prefix + "RunId"] = id.RunId; environment[prefix + "InstanceId"] = id.InstanceId;
            environment[prefix + "ExpectedInstanceId"] = id.ExpectedInstanceId; environment[prefix + "ClientId"] = id.ClientId;
        }
        string[] arguments = runtime.TryGetProperty("verifier_arguments", out JsonElement args)
            ? args.EnumerateArray().Select(item => item.GetString() ?? throw new InvalidDataException("verifier_arguments")).ToArray() : [];
        return runner.ExecuteAsync(WorkerIdentity.Text(runtime, "verifier_executable"), arguments, environment, output, sourceCommit,
            repeats.Select(item => item.Identity).ToArray(), cancellationToken);
    }

    private static void Add(Dictionary<string, string> values, JsonElement section, string prefix)
    {
        foreach (JsonProperty property in section.EnumerateObject())
            values.Add(prefix + property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText());
    }
}
