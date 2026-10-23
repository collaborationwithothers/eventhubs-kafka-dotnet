using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed record AcceptedWorkerOutcome(bool ExecutionCompleted, bool CountsAccepted, string Status,
    IReadOnlyDictionary<string, long>? Counts, string Reason);

public static class WorkerOutcome
{
    public const string ReceiptPrefix = "PARCHWELL_COMPLETION ";
    private static readonly string[] CountNames = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];

    public static bool HasReceipt(ProcessResult process, string mode, WorkerIdentity? identity, string sourceCommit)
    {
        if (process.TimedOut || process.ExitCode is not (0 or 1)) return false;
        string[] lines = process.StandardOutput.Split('\n').Where(line => line.StartsWith(ReceiptPrefix, StringComparison.Ordinal)).ToArray();
        if (lines.Length != 1 || System.Text.Encoding.UTF8.GetByteCount(lines[0][ReceiptPrefix.Length..]) > 65536) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(lines[0][ReceiptPrefix.Length..]);
            JsonElement receipt = document.RootElement;
            return receipt.GetProperty("schema_version").GetInt32() == 1 &&
                WorkerIdentity.Text(receipt, "mode") == mode &&
                WorkerIdentity.Text(receipt, "source_commit") == sourceCommit &&
                receipt.GetProperty("cleanup_completed").GetBoolean() &&
                receipt.GetProperty("handler_exit_code").GetInt32() == process.ExitCode &&
                (identity is null ? receipt.GetProperty("identity").GetProperty("scenario_id").GetString() == "A01" : WorkerIdentity.Read(receipt.GetProperty("identity")) == identity);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException)
        { FailureDiagnostics.Write(exception, Console.Error); return false; }
    }

    public static AcceptedWorkerOutcome Reconcile(ProcessResult process, ReadOnlyMemory<byte> result,
        WorkerIdentity identity, string sourceCommit)
    {
        if (!HasReceipt(process, "verify", identity, sourceCommit)) return Rejected("completion_receipt_or_exit");
        try
        {
            if (result.Length > 16777216) return Rejected("result_limit");
            using JsonDocument document = JsonDocument.Parse(result, new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            if (root.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Read(root.GetProperty("identity")) != identity ||
                WorkerIdentity.Text(root, "verifier_commit") != sourceCommit) return Rejected("result_identity");
            string status = WorkerIdentity.Text(root, "status");
            if (status == "complete_valid" && process.ExitCode == 0)
            {
                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (string name in CountNames)
                {
                    long count = root.GetProperty(name).GetInt64();
                    if (count < 0) return Rejected("invalid_counts");
                    counts.Add(name, count);
                }
                return new(true, true, status, counts, "complete_valid");
            }
            if (status is "invalid" or "incomplete" && process.ExitCode == 1 && !CountNames.Any(name => root.TryGetProperty(name, out _)))
                return new(true, false, status, null, status);
            return Rejected("result_exit_disagreement");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException)
        { FailureDiagnostics.Write(exception, Console.Error); return Rejected("malformed_result"); }
    }

    private static AcceptedWorkerOutcome Rejected(string reason) => new(false, false, "execution_failure", null, reason);
}
