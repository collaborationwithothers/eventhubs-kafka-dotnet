using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed record AksTarget(string Subscription, string ResourceGroup, string Name);

public static partial class AksKubeconfigBinding
{
    public static AksTarget ReadTarget(JsonElement outputs)
    {
        JsonElement aks = outputs.GetProperty("aks");
        if (aks.TryGetProperty("value", out JsonElement wrapped)) aks = wrapped;
        string name = WorkerIdentity.Text(aks, "name");
        string[] parts = WorkerIdentity.Text(aks, "id").Split('/');
        if (parts.Length != 9 || parts[0].Length != 0 || !parts[1].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(parts[2], "D", out _) || !parts[3].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[4]) || parts[4].Length > 90 || parts[4].Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '\\' or '?' or '#') ||
            !parts[5].Equals("providers", StringComparison.OrdinalIgnoreCase) ||
            !parts[6].Equals("Microsoft.ContainerService", StringComparison.OrdinalIgnoreCase) ||
            !parts[7].Equals("managedClusters", StringComparison.OrdinalIgnoreCase) ||
            !ClusterName().IsMatch(name) || !parts[8].Equals(name, StringComparison.Ordinal))
            throw new InvalidDataException("aks_output_identity_invalid");
        return new(parts[2], parts[4], name);
    }

    public static Task<IReadOnlyDictionary<string, string>> BindAsync(IProcessRunner process,
        JsonElement outputs, string privateKubeconfigPath, TimeSpan timeout, CancellationToken cancellationToken) =>
        BindCoreAsync(process, outputs, privateKubeconfigPath, timeout, true, cancellationToken);

    public static Task<IReadOnlyDictionary<string, string>> BindReadOnlyAsync(IProcessRunner process,
        JsonElement outputs, string privateKubeconfigPath, TimeSpan timeout, CancellationToken cancellationToken) =>
        BindCoreAsync(process, outputs, privateKubeconfigPath, timeout, false, cancellationToken);

    private static async Task<IReadOnlyDictionary<string, string>> BindCoreAsync(IProcessRunner process,
        JsonElement outputs, string privateKubeconfigPath, TimeSpan timeout, bool ensureNamespace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        AksTarget target = ReadTarget(outputs);
        if (!Path.IsPathFullyQualified(privateKubeconfigPath) || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(60))
            throw new InvalidDataException("private_kubeconfig_path_or_timeout_invalid");
        string path = Path.GetFullPath(privateKubeconfigPath);
        string directory = Path.GetDirectoryName(path) ?? throw new InvalidDataException("private_kubeconfig_directory_missing");
        if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(directory).LinkTarget is not null)
            throw new InvalidDataException("private_kubeconfig_symlink_rejected");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Delete(path);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["KUBECONFIG"] = path };
        await RunAsync("az", ["aks", "get-credentials", "--subscription", target.Subscription,
            "--resource-group", target.ResourceGroup, "--name", target.Name, "--file", path,
            "--overwrite-existing", "--format", "exec", "--only-show-errors"], environment).ConfigureAwait(false);
        if (!File.Exists(path)) throw new IOException("private_kubeconfig_not_created");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        ProcessResult version = await RunAsync("kubelogin", ["--version"], environment).ConfigureAwait(false);
        ProcessResult help = await RunAsync("kubelogin", ["convert-kubeconfig", "--help"], environment).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "kubelogin-version.txt"), version.StandardOutput, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "kubelogin-convert-help.txt"), help.StandardOutput, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(version.StandardOutput) || !help.StandardOutput.Contains("azurecli", StringComparison.Ordinal) ||
            !help.StandardOutput.Contains("--login", StringComparison.Ordinal)) throw new InvalidDataException("kubelogin_azurecli_prerequisite_missing");
        await RunAsync("kubelogin", ["convert-kubeconfig", "-l", "azurecli", "--subscription", target.Subscription], environment).ConfigureAwait(false);
        if (ensureNamespace)
        {
        string namespacePath = Path.Combine(directory, "namespace.json");
        await File.WriteAllBytesAsync(namespacePath, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "v1", kind = "Namespace", metadata = new { name = "parchwell" }
        }), cancellationToken).ConfigureAwait(false);
        await RunAsync("kubectl", ["apply", "-f", namespacePath], environment).ConfigureAwait(false);
        }
        return environment;

        async Task<ProcessResult> RunAsync(string tool, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> overlay)
        {
            ProcessResult result = await process.RunAsync(new(tool, arguments, timeout, Environment: overlay), cancellationToken).ConfigureAwait(false);
            if (result.TimedOut || result.ExitCode != 0) throw FailureDiagnostics.WithCommandOutput(new IOException("aks_context_binding_failed"), tool, result);
            return result;
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ClusterName();
}
