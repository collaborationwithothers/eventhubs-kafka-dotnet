using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Parchwell.LoadGen.Run.Input;

/// <summary>Waits for the runner's bounded, independently identified producer release before host construction.</summary>
internal static class DeploymentAdmission
{
    internal const int MaxFileBytes = 65536;
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(180);
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    internal static async Task<IReadOnlyDictionary<string, string?>?> WaitAsync(IConfiguration configuration,
        TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        string? path = configuration["PARCHWELL_ADMISSION_FILE"];
        if (path is null) return null;
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string runId = Required(configuration, "LoadGen:RunId");
        string podName = Required(configuration, "PARCHWELL_POD_NAME");
        string podUid = Required(configuration, "PARCHWELL_POD_UID");
        string valuesPath = Required(configuration, "PARCHWELL_GENERATED_VALUES_FILE");
        using var deadline = new CancellationTokenSource(Deadline, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                byte[]? bytes = await ReadAvailableAsync(path, linked.Token).ConfigureAwait(false);
                if (bytes is not null && bytes.Length != 0)
                {
                    using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("deployment_admission_invalid");
                    if (root.TryGetProperty("release", out JsonElement release))
                    {
                        if (release.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                            throw new InvalidDataException("deployment_admission_release_invalid");
                        if (release.GetBoolean())
                        {
                            ValidateIdentity(root, runId, podName, podUid);
                            string[] names = root.GetProperty("expected_broker_names").EnumerateArray()
                                .Select(item => item.GetString() ?? string.Empty).ToArray();
                            if (names.Length == 0 || names.Any(name => string.IsNullOrWhiteSpace(name) || name.Contains(',', StringComparison.Ordinal)) ||
                                names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                                throw new InvalidDataException("deployment_admission_broker_inventory_invalid");
                            string hash = root.GetProperty("generated_values_sha256").GetString() ?? string.Empty;
                            await using FileStream values = File.OpenRead(valuesPath);
                            string actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(values, linked.Token).ConfigureAwait(false));
                            if (!StringComparer.Ordinal.Equals(hash, actualHash))
                                throw new InvalidDataException("deployment_admission_generated_values_hash_mismatch");
                            return new Dictionary<string, string?>(StringComparer.Ordinal)
                            {
                                ["LoadGen:ExpectedInstanceId"] = podName, ["LoadGen:ActualInstanceId"] = podName,
                                ["LoadGen:PodName"] = podName, ["LoadGen:ClientId"] = podName,
                                ["LoadGen:ExpectedBrokerNamesCsv"] = string.Join(',', names)
                            };
                        }
                    }
                }
                await Task.Delay(PollInterval, timeProvider, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("deployment_admission_deadline_exceeded");
        }
    }

    private static void ValidateIdentity(JsonElement root, string runId, string podName, string podUid)
    {
        if (root.GetProperty("schema_version").GetInt32() != 1 ||
            !StringComparer.Ordinal.Equals(root.GetProperty("run_id").GetString(), runId) ||
            !StringComparer.Ordinal.Equals(root.GetProperty("pod_name").GetString(), podName) ||
            !StringComparer.Ordinal.Equals(root.GetProperty("pod_uid").GetString(), podUid))
            throw new InvalidDataException("deployment_admission_identity_mismatch");
    }

    private static async Task<byte[]?> ReadAvailableAsync(string path, CancellationToken cancellationToken)
    {
        FileStream input;
        try { input = File.OpenRead(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        await using (input.ConfigureAwait(false))
        {
            if (input.Length > MaxFileBytes) throw new InvalidDataException("deployment_admission_file_too_large");
            byte[] buffer = new byte[MaxFileBytes + 1];
            int count = 0;
            while (count < buffer.Length)
            {
                int read = await input.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            if (count > MaxFileBytes) throw new InvalidDataException("deployment_admission_file_too_large");
            return buffer[..count];
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]! :
        throw new InvalidOperationException("deployment_admission_setting_missing:" + key);
}
