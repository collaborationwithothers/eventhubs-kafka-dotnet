using System.Text;
using Parchwell.Scenarios;

namespace Parchwell.Runner.Run.Deployment;

public sealed class DeploymentScenarioSource(string root) : IScenarioSource
{
    public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (logicalPath is not ("scenarios/A01/scenario.yaml" or "scenarios/profiles/saturation-ramp.yaml"))
            throw new InvalidDataException("deployment_scenario_path_not_allowed");
        string current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("deployment_scenario_symlink");
        foreach (string segment in logicalPath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("deployment_scenario_symlink");
        }
        return ValueTask.FromResult<TextReader>(new StreamReader(current, new UTF8Encoding(false, true), false));
    }
}
