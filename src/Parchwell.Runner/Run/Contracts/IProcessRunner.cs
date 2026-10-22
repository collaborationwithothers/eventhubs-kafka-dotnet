namespace Parchwell.Runner.Run.Contracts;

public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments,
    TimeSpan Timeout, int MaxOutputBytes = 16777216, string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut = false);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
