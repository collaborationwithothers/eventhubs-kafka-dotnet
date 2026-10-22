using System.Text.Json;

namespace Parchwell.Runner.Run.Contracts;

public sealed record WorkerIdentity(string ScenarioId, string ConfigurationId, string RunId,
    string InstanceId, string ExpectedInstanceId, string ClientId)
{
    public static WorkerIdentity Read(JsonElement value) => new(
        Text(value, "scenario_id"), Text(value, "configuration_id"), Text(value, "run_id"),
        Text(value, "instance_id"), Text(value, "expected_instance_id"), Text(value, "client_id"));

    public static string Text(JsonElement value, string name)
    {
        string? text = value.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(text) ? throw new InvalidDataException("missing_" + name) : text;
    }
}
