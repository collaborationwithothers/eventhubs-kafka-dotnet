using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Cost;

/// <summary>
/// Uses the existing OIDC login for conditional Blob operations without key lookup or Runner-initiated write retries.
/// Azure CLI's SDK may retry conditional requests within the process deadline.
/// </summary>
public sealed partial class AzureCliRunStore : IArtifactStore
{
    private static readonly string[] BlobCommand = ["storage", "blob"];
    private readonly IProcessRunner process;
    private readonly string account;
    private readonly string privateDirectory;
    private readonly TimeProvider clock;

    public AzureCliRunStore(IProcessRunner process, string accountUrl, string privateDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!Uri.TryCreate(accountUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 ||
            !AccountHost().IsMatch(uri.Host)) throw new InvalidDataException("run_storage_url_invalid");
        this.process = process;
        account = uri.Host.Split('.')[0];
        this.privateDirectory = privateDirectory;
        clock = timeProvider ?? TimeProvider.System;
    }

    public async Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
    {
        ValidatePath(container, path);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var properties = await ExecuteAsync(["show", "--container-name", container, "--name", path, "--output", "json"], deadline.Token).ConfigureAwait(false);
        if (properties.ExitCode != 0 && properties.StandardError.Contains("BlobNotFound", StringComparison.Ordinal)) return null;
        RequireSuccess(properties);
        using var json = JsonDocument.Parse(properties.StandardOutput);
        var detail = json.RootElement.GetProperty("properties");
        var etag = detail.GetProperty("etag").GetString();
        var length = detail.GetProperty("contentLength").GetInt64();
        if (string.IsNullOrWhiteSpace(etag) || length < 0 || length > maxBytes) throw new InvalidDataException("private_blob_size_or_etag_invalid");
        var file = NewPrivateFile();
        try
        {
            var downloaded = await ExecuteAsync(["download", "--container-name", container, "--name", path,
                "--file", file, "--if-match", etag, "--no-progress", "--output", "none"], deadline.Token).ConfigureAwait(false);
            RequireSuccess(downloaded);
            var info = new FileInfo(file);
            if (info.Length != length || info.Length > maxBytes) throw new InvalidDataException("private_blob_length_mismatch");
            return new StoredArtifact(await File.ReadAllBytesAsync(file, deadline.Token).ConfigureAwait(false), etag);
        }
        finally { File.Delete(file); }
    }

    public async Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes,
        string? expectedETag, bool createOnly, CancellationToken cancellationToken)
    {
        ValidatePath(container, path);
        if (createOnly == (expectedETag is not null)) throw new InvalidOperationException("conditional_write_required");
        var file = NewPrivateFile();
        try
        {
            await File.WriteAllBytesAsync(file, bytes.ToArray(), cancellationToken).ConfigureAwait(false);
            var arguments = new List<string> { "upload", "--container-name", container, "--name", path, "--file", file,
                "--type", "block", "--no-progress", "--output", "none" };
            if (createOnly) arguments.AddRange(["--if-none-match", "*", "--overwrite", "false"]);
            else arguments.AddRange(["--overwrite", "true", "--if-match", expectedETag!]);
            ProcessResult result;
            try { result = await ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException exception) { FailureDiagnostics.Write(exception, Console.Error); return ArtifactWriteOutcome.Uncertain; }
            catch (IOException exception) { FailureDiagnostics.Write(exception, Console.Error); return ArtifactWriteOutcome.Uncertain; }
            if (result.ExitCode == 0 && !result.TimedOut) return ArtifactWriteOutcome.Stored;
            if (result.StandardError.Contains("ConditionNotMet", StringComparison.Ordinal) ||
                result.StandardError.Contains("BlobAlreadyExists", StringComparison.Ordinal) ||
                result.StandardError.Contains("412", StringComparison.Ordinal)) return ArtifactWriteOutcome.Conflict;
            return ArtifactWriteOutcome.Uncertain;
        }
        finally { File.Delete(file); }
    }

    public Task<string> VerifyCliAsync(CancellationToken cancellationToken) => VerifyCliCoreAsync(null, cancellationToken);

    public Task<string> VerifyCliAsync(string receiptDirectory, CancellationToken cancellationToken) => VerifyCliCoreAsync(receiptDirectory, cancellationToken);

    private async Task<string> VerifyCliCoreAsync(string? receiptDirectory, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        cancellationToken = deadline.Token;
        if (receiptDirectory is not null) Directory.CreateDirectory(receiptDirectory);
        var version = await process.RunAsync(new ProcessRequest("az", ["version", "--output", "json"], TimeSpan.FromSeconds(60), 1_048_576), cancellationToken).ConfigureAwait(false);
        RequireSuccess(version);
        if (receiptDirectory is not null)
            await File.WriteAllTextAsync(Path.Combine(receiptDirectory, "version.json"), version.StandardOutput, cancellationToken).ConfigureAwait(false);
        foreach (var (command, flags) in new[]
        {
            ("upload", new[] { "--auth-mode", "--if-match", "--if-none-match", "--overwrite", "--type" }),
            ("download", new[] { "--auth-mode", "--if-match", "--file" })
        })
        {
            var help = await process.RunAsync(new ProcessRequest("az", ["storage", "blob", command, "--help"], TimeSpan.FromSeconds(60), 1_048_576), cancellationToken).ConfigureAwait(false);
            RequireSuccess(help);
            if (receiptDirectory is not null)
                await File.WriteAllTextAsync(Path.Combine(receiptDirectory, "blob-" + command + "-help.txt"), help.StandardOutput, cancellationToken).ConfigureAwait(false);
            if (flags.Any(flag => !help.StandardOutput.Contains(flag, StringComparison.Ordinal)))
                throw new InvalidOperationException("azure_cli_conditional_options_unavailable");
        }
        return version.StandardOutput;
    }

    private Task<ProcessResult> ExecuteAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) => process.RunAsync(
        new ProcessRequest("az", BlobCommand.Concat(arguments).Concat(["--account-name", account, "--auth-mode", "login", "--only-show-errors"]).ToArray(),
            TimeSpan.FromSeconds(60), 1_048_576), cancellationToken);

    private string NewPrivateFile()
    {
        Directory.CreateDirectory(privateDirectory);
        var path = Path.Combine(privateDirectory, "blob-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static void RequireSuccess(ProcessResult result)
    {
        if (result.TimedOut || result.ExitCode != 0) throw FailureDiagnostics.WithCommandOutput(new IOException("private_storage_cli_failed"), "az", result);
    }

    private static void ValidatePath(string container, string path)
    {
        if (container is not ("ledger" or "evidence") || string.IsNullOrWhiteSpace(path) || path.Length > 1024 ||
            path.StartsWith('/') || path.Split('/').Any(part => part is "" or "." or "..") || path.Any(char.IsControl) || path.Contains('\\'))
            throw new InvalidDataException("private_blob_path_invalid");
    }
    [GeneratedRegex("^[a-z0-9]{3,24}\\.blob\\.core\\.windows\\.net$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountHost();
}
