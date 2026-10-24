namespace Parchwell.Runner.Run.Execution;

internal sealed record ProcessCancellationCapture(string Reason, string StandardOutput, string StandardError);
