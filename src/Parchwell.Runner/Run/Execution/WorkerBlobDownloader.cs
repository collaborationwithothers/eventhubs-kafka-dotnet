using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

/// <summary>Downloads original worker bytes with an ETag and an independently checked byte ceiling.</summary>
internal static class WorkerBlobDownloader
{
    internal static async Task<long> DownloadAsync(IProcessRunner process, string account, string container,
        string name, string destination, long maximumBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult properties = await RunAsync(["storage", "blob", "show", "--account-name", account, "--container-name", container, "--name", name,
            "--auth-mode", "login", "--query", "properties", "--only-show-errors", "-o", "json"]).ConfigureAwait(false);
        using JsonDocument detail = JsonDocument.Parse(properties.StandardOutput);
        string etag = WorkerIdentity.Text(detail.RootElement, "etag");
        if (!detail.RootElement.GetProperty("contentLength").TryGetInt64(out long length) || length < 0 || length > maximumBytes)
            throw new InvalidDataException("download_artifact_limit");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await RunAsync(["storage", "blob", "download", "--account-name", account, "--container-name", container, "--name", name,
            "--file", destination, "--auth-mode", "login", "--if-match", etag, "--overwrite", "false", "--only-show-errors", "-o", "none"]).ConfigureAwait(false);
        if (new FileInfo(destination).Length != length) throw new InvalidDataException("download_artifact_limit");
        return length;

        async Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments)
        {
            ProcessResult result = await process.RunAsync(new("az", arguments, timeout), cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.TimedOut) throw FailureDiagnostics.WithCommandOutput(new IOException("worker_blob_read_failed"), "az", result);
            return result;
        }
    }
}
