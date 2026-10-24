using System.Text;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

/// <summary>Formats safe Runner failures and opt-in unredacted command diagnostics.</summary>
internal static class FailureDiagnostics
{
    private static readonly AsyncLocal<string?> CurrentStage = new();

    internal static IDisposable UseStage(string stage)
    {
        string? previous = CurrentStage.Value;
        CurrentStage.Value = stage;
        return new StageScope(previous);
    }

    public static void Write(Exception exception, TextWriter error, string? stage = null)
    {
        stage ??= CurrentStage.Value;
        bool debug = DebugEnabled();
        var line = new StringBuilder("runner_execution_failed ");
        if (stage is not null) line.Append(stage).Append(' ');
        bool first = true;
        foreach (Exception entry in Traverse(exception))
        {
            if (!first) line.Append(" -> ");
            first = false;
            line.Append(entry.GetType().Name);
            string message = entry.Message;
            if (message.Length > 0 && message.All(character => character is >= 'a' and <= 'z' or '_' or ':'))
                line.Append(' ').Append(message);
        }
        foreach (Exception entry in Traverse(exception))
            if (entry.Data["runner_terraform_command"] is string command && command is "init" or "plan" or "apply"
                && entry.Data["runner_terraform_cancellation"] is string reason
                && reason is "caller_cancellation" or "apply_deadline" or "command_deadline" or "cancellation_unknown")
            {
                line.Append(" terraform_command=").Append(command).Append(" cancellation=").Append(reason);
                if (entry.Data["runner_terraform_cancellation_evidence"] is string evidence && evidence is "saved" or "unavailable")
                    line.Append(" cancellation_evidence=").Append(evidence);
            }
        error.WriteLine(line.ToString());
        if (!debug) return;

        if (stage is not null) error.WriteLine($"runner_debug_exception {stage}");
        error.WriteLine(exception.ToString());
        foreach (Exception entry in Traverse(exception))
        {
            if (entry.Data["runner_worker_mode"] is string mode)
            {
                string context = $"scenario_id={entry.Data["runner_worker_scenario_id"]} run_id={entry.Data["runner_worker_run_id"]}";
                error.WriteLine($"runner_debug_worker {mode} exit_code={entry.Data["runner_worker_exit_code"]} timed_out={entry.Data["runner_worker_timed_out"]} {context}");
                foreach (string stream in new[] { "stdout", "stderr" })
                    if (entry.Data["runner_worker_" + stream + "_tail"] is string { Length: > 0 } tail)
                    {
                        error.WriteLine($"runner_debug_worker {mode} {stream} {context}");
                        error.WriteLine(tail);
                    }
            }
            if (entry.Data["runner_export_path"] is string path)
                error.WriteLine($"runner_debug_export path={path} write_outcome={entry.Data["runner_export_write_outcome"]} expected_sha256={entry.Data["runner_export_expected_sha256"]} actual_sha256={entry.Data["runner_export_actual_sha256"]}");
            if (entry.Data["runner_command_tool"] is not string tool) continue;
            WriteTail("stdout", "runner_command_stdout_tail");
            WriteTail("stderr", "runner_command_stderr_tail");

            void WriteTail(string stream, string key)
            {
                if (entry.Data[key] is not string { Length: > 0 } tail) return;
                error.WriteLine(stage is null
                    ? $"runner_debug_command {tool} {stream}"
                    : $"runner_debug_command {stage} {tool} {stream}");
                error.WriteLine(tail);
            }
        }
    }

    public static T WithCommandOutput<T>(T exception, string tool, ProcessResult result) where T : Exception
    {
        if (DebugEnabled() && tool is "terraform" or "az" or "kubectl" or "helm")
        {
            exception.Data["runner_command_tool"] = tool;
            exception.Data["runner_command_stdout_tail"] = Tail(result.StandardOutput);
            exception.Data["runner_command_stderr_tail"] = Tail(result.StandardError);
        }
        return exception;
    }

    internal static T WithWorkerOutput<T>(T exception, string mode, ProcessResult result, WorkerIdentity identity) where T : Exception
    {
        if (DebugEnabled() && mode is "start-line" or "loadgen" or "verify")
        {
            exception.Data["runner_worker_mode"] = mode;
            exception.Data["runner_worker_scenario_id"] = identity.ScenarioId;
            exception.Data["runner_worker_run_id"] = identity.RunId;
            exception.Data["runner_worker_exit_code"] = result.ExitCode;
            exception.Data["runner_worker_timed_out"] = result.TimedOut;
            exception.Data["runner_worker_stdout_tail"] = Tail(result.StandardOutput);
            exception.Data["runner_worker_stderr_tail"] = Tail(result.StandardError);
        }
        return exception;
    }

    internal static T WithExportConflict<T>(T exception, string path, ArtifactWriteOutcome outcome,
        string expectedSha256, string? actualSha256) where T : Exception
    {
        if (DebugEnabled())
        {
            exception.Data["runner_export_path"] = path;
            exception.Data["runner_export_write_outcome"] = outcome;
            exception.Data["runner_export_expected_sha256"] = expectedSha256;
            exception.Data["runner_export_actual_sha256"] = actualSha256 ?? "missing";
        }
        return exception;
    }

    private static bool DebugEnabled() => string.Equals(
        Environment.GetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES"), "true", StringComparison.Ordinal);

    private static string Tail(string stream)
    {
        using var reader = new StringReader(stream);
        var lines = new Queue<string>();
        while (reader.ReadLine() is string line)
        {
            if (lines.Count == 50) lines.Dequeue();
            lines.Enqueue(line);
        }
        return string.Join('\n', lines);
    }

    private static IEnumerable<Exception> Traverse(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.TryPop(out Exception? entry))
        {
            yield return entry;
            if (entry is AggregateException aggregate)
            {
                for (int index = aggregate.InnerExceptions.Count - 1; index >= 0; index--)
                    pending.Push(aggregate.InnerExceptions[index]);
            }
            else if (entry.InnerException is Exception inner) pending.Push(inner);
        }
    }

    private sealed class StageScope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            CurrentStage.Value = previous;
            _disposed = true;
        }
    }
}
