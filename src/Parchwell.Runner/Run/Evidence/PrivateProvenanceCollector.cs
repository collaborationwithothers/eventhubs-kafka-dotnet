using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Evidence;

public sealed record PrivateProvenanceCapture(bool Complete, byte[]? CostBytes);

/// <summary>Stages only named private provenance inputs before the immutable phase manifest.</summary>
public static class PrivateProvenanceCollector
{
    public const int MaxDocumentBytes = 1_048_576;
    public const int MaxReceiptBytes = 16 * 1024 * 1024;
    private static readonly string[] ReceiptPaths =
    [
        "image-receipts.json", "tools/azure-cli.json", "tools/kubectl.json",
        "tools/kubelogin-version.txt", "tools/kubelogin-convert-help.txt", "tools/aks-get-credentials-help.txt",
        "tools/terraform.json", "tools/blob-upload-help.txt", "tools/blob-download-help.txt", "tools/helm.json"
    ];

    public static async Task<PrivateExportReceipt> ExportAvailableAsync(JsonElement runtime, byte[] runtimeBytes,
        string outputDirectory, string environmentRunId, EvidenceExporter exporter, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(300), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        PrivateProvenanceCapture capture = await CollectAsync(runtime, runtimeBytes, outputDirectory, deadline.Token).ConfigureAwait(false);
        EvidenceFile[] files = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(outputDirectory, path).Replace('\\', '/').StartsWith("private-store/", StringComparison.Ordinal))
            .Take(EvidenceExporter.MaxEntries + 1)
            .Select(path => new EvidenceFile(Path.GetRelativePath(outputDirectory, path).Replace('\\', '/'), path)).ToArray();
        return (await exporter.ExportAsync(environmentRunId, files, deadline.Token).ConfigureAwait(false)) with { Complete = capture.Complete };
    }

    public static async Task<PrivateProvenanceCapture> CollectAsync(JsonElement runtime, byte[] runtimeBytes,
        string outputDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeBytes);
        if (runtimeBytes.Length > MaxDocumentBytes) throw new InvalidDataException("private_runtime_input_limit");
        string directory = Path.Combine(outputDirectory, "private-provenance");
        Directory.CreateDirectory(directory);
        var missing = new List<string>();
        var invalid = new List<string>();
        byte[]? cost = null;
        await CaptureAsync("runtime.json", () => Task.FromResult(runtimeBytes)).ConfigureAwait(false);
        await CaptureAsync("cost-input.json", async () =>
        {
            cost = await ReadBoundedAsync(WorkerIdentity.Text(runtime, "cost_file"), MaxDocumentBytes, cancellationToken).ConfigureAwait(false);
            return cost;
        }).ConfigureAwait(false);
        string? source = runtime.TryGetProperty("private_provenance_directory", out JsonElement location) ? location.GetString() : null;
        string imageLog = WorkerIdentity.Text(runtime, "phase") == "probe" ? "image-publication.log" : "image-reference-readback.log";
        foreach (string path in ReceiptPaths.Append(imageLog))
        {
            await CaptureAsync(path, () => string.IsNullOrWhiteSpace(source)
                ? Task.FromException<byte[]>(new FileNotFoundException())
                : ReadBoundedAsync(Path.Combine(source, path), MaxReceiptBytes, cancellationToken)).ConfigureAwait(false);
        }
        bool complete = missing.Count == 0 && invalid.Count == 0;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, "status.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { schema_version = 1, complete, missing, invalid }, LedgerJson.Options), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FailureDiagnostics.Write(exception, Console.Error);
            // A missing status receipt cannot prevent preservation of the available originals.
            complete = false;
        }
        return new(complete, cost);

        async Task CaptureAsync(string relative, Func<Task<byte[]>> read)
        {
            try
            {
                byte[] bytes = await read().ConfigureAwait(false);
                string target = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    byte[] retained = await ReadBoundedAsync(target, MaxReceiptBytes, cancellationToken).ConfigureAwait(false);
                    if (!bytes.AsSpan().SequenceEqual(retained)) throw new InvalidDataException("private_provenance_changed");
                }
                else
                {
                    await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await file.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (FileNotFoundException exception) { FailureDiagnostics.Write(exception, Console.Error); missing.Add(relative); }
            catch (DirectoryNotFoundException exception) { FailureDiagnostics.Write(exception, Console.Error); missing.Add(relative); }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException)
            { FailureDiagnostics.Write(exception, Console.Error); invalid.Add(relative); }
        }
    }

    public static async Task<byte[]> ReadBoundedAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null) throw new InvalidDataException("private_provenance_link");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = file.Length;
        if (length > maximumBytes) throw new InvalidDataException("private_provenance_byte_limit");
        byte[] bytes = new byte[checked((int)length)];
        await file.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        byte[] extra = new byte[1];
        if (await file.ReadAsync(extra, cancellationToken).ConfigureAwait(false) != 0) throw new InvalidDataException("private_provenance_changed");
        return bytes;
    }
}
