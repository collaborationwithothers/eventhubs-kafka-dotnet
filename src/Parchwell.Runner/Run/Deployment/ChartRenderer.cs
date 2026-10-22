using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Deployment;

public sealed class ChartRenderer(IProcessRunner process)
{
    public async Task<string> RenderAsync(DeploymentFiles files, string chartDirectory, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ProcessResult version = await process.RunAsync(new ProcessRequest(files.HelmBinaryPath, ["version", "--short"], TimeSpan.FromSeconds(60)), cancellationToken).ConfigureAwait(false);
        if (version.ExitCode != 0 || version.TimedOut || !version.StandardOutput.StartsWith("v4.3.1+", StringComparison.Ordinal))
            throw new InvalidDataException("verified_helm_v4_3_1_required");
        ProcessResult lint = await process.RunAsync(new ProcessRequest(files.HelmBinaryPath,
            ["lint", chartDirectory, "-f", files.GeneratedValuesPath, "-f", files.RuntimeValuesPath], TimeSpan.FromSeconds(60)), cancellationToken).ConfigureAwait(false);
        if (lint.ExitCode != 0 || lint.TimedOut) throw new InvalidDataException("chart_lint_failed");
        ProcessResult render = await process.RunAsync(new ProcessRequest(files.HelmBinaryPath,
            ["template", "parchwell", chartDirectory, "-f", files.GeneratedValuesPath, "-f", files.RuntimeValuesPath], TimeSpan.FromSeconds(60)), cancellationToken).ConfigureAwait(false);
        if (render.ExitCode != 0 || render.TimedOut || string.IsNullOrWhiteSpace(render.StandardOutput))
            throw new InvalidDataException("chart_render_failed");
        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, "rendered.yaml");
        await File.WriteAllTextAsync(path, render.StandardOutput, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
