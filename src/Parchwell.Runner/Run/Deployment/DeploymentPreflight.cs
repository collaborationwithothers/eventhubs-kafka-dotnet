using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Scenarios;

namespace Parchwell.Runner.Run.Deployment;

/// <summary>Checks local deployment prerequisites before any production transport may apply.</summary>
public sealed class DeploymentPreflight(ScenarioLoader loader)
{
    public const string OfficialHelmArchiveSha256 = "0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b";
    private const int DocumentBytes = 16777216;
    private const int ToolBytes = 67108864;

    /// <summary>Validates the private input needed to redact the phase's public candidate.</summary>
    public static void ValidatePublicCandidateInputs(JsonElement runtime)
    {
        if (!runtime.TryGetProperty("subscription_guid", out JsonElement subscription) ||
            subscription.ValueKind != JsonValueKind.String || subscription.GetString() is not { Length: 36 } subscriptionGuid ||
            !Guid.TryParseExact(subscriptionGuid, "D", out _))
            throw new InvalidDataException("subscription_redaction_input_invalid");
    }

    public async Task ValidateAsync(JsonElement runtime, CancellationToken cancellationToken = default)
    {
        ValidatePublicCandidateInputs(runtime);
        if (runtime.GetProperty("schema_version").GetInt32() != 1 ||
            (runtime.TryGetProperty("synthetic", out JsonElement synthetic) && synthetic.GetBoolean()))
            throw new InvalidDataException("production_runtime_must_not_be_synthetic");
        string source = Text(runtime, "source_commit");
        if (source.Length != 40 || source.Any(c => !char.IsAsciiHexDigitLower(c))) throw new InvalidDataException("source_commit_invalid");
        foreach (string role in new[] { "loadgen", "verifier" })
        {
            JsonElement image = runtime.GetProperty("images").GetProperty(role);
            string digest = Text(image, "digest");
            if (Text(image, "source_commit") != source || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
                digest.Length != 71 || digest[7..].Any(c => !char.IsAsciiHexDigitLower(c)))
                throw new InvalidDataException("immutable_image_receipt_mismatch");
        }
        using JsonDocument tags = JsonDocument.Parse(await ReadAsync(Text(runtime, "service_tags_file"), DocumentBytes, cancellationToken).ConfigureAwait(false));
        ServiceTagData.Validate(tags.RootElement);
        if (tags.RootElement.TryGetProperty("synthetic", out JsonElement tagSynthetic) && tagSynthetic.GetBoolean())
            throw new InvalidDataException("production_service_tags_must_not_be_synthetic");
        using JsonDocument limits = JsonDocument.Parse(await ReadAsync(Text(runtime, "limits_file"), 1048576, cancellationToken).ConfigureAwait(false));
        if (limits.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("limits_schema_invalid");
        string[] sections = ["scenario_parsing", "loadgen", "verifier_shared", "verifier_kafka", "verifier_start_line", "verifier_verify", "verifier_gate", "execution"];
        foreach (string section in sections)
        {
            JsonElement value = limits.RootElement.GetProperty(section);
            if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Any()) throw new InvalidDataException("limits_section_required");
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number && (!property.Value.TryGetInt64(out long number) || number < 0 || (number == 0 && property.Name != "replacement_repeats")))
                    throw new InvalidDataException("execution_limit_invalid");
                if (property.Name.EndsWith("Timeout", StringComparison.Ordinal) || property.Name is "ProcessLifetime" or "SafetyStop" or "PollSlice")
                    if (!TimeSpan.TryParse(property.Value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out TimeSpan timeout) || timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                        throw new InvalidDataException("execution_duration_invalid");
            }
        }
        await loader.LoadAsync("A01", cancellationToken).ConfigureAwait(false);
        await VerifyHelmAsync(Text(runtime, "helm_archive_file"), Text(runtime, "helm_binary_file"), cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyHelmAsync(string archivePath, string binaryPath, CancellationToken cancellationToken)
    {
        byte[] archive = await ReadAsync(archivePath, ToolBytes, cancellationToken).ConfigureAwait(false);
        if (Convert.ToHexStringLower(SHA256.HashData(archive)) != OfficialHelmArchiveSha256)
            throw new InvalidDataException("official_helm_archive_checksum_mismatch");
        byte[] binary = await ReadAsync(binaryPath, ToolBytes, cancellationToken).ConfigureAwait(false);
        using var compressed = new MemoryStream(archive, writable: false);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        int entries = 0, matching = 0;
        while (await tar.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false) is { } entry)
        {
            if (++entries > 16) throw new InvalidDataException("helm_archive_entry_limit");
            if (entry.Name != "linux-amd64/helm") continue;
            if (++matching != 1 || entry.Length != binary.LongLength || entry.DataStream is null || entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                throw new InvalidDataException("official_helm_binary_invalid");
            byte[] expected = await SHA256.HashDataAsync(entry.DataStream, cancellationToken).ConfigureAwait(false);
            if (!expected.AsSpan().SequenceEqual(SHA256.HashData(binary))) throw new InvalidDataException("installed_helm_binary_mismatch");
        }
        if (matching != 1) throw new InvalidDataException("official_helm_binary_missing");
    }

    private static async Task<byte[]> ReadAsync(string path, int cap, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > cap) throw new InvalidDataException("preflight_input_exceeds_limit");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > cap) throw new InvalidDataException("preflight_input_exceeds_limit");
        return bytes;
    }
    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString() is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text)
        ? text : throw new InvalidDataException("preflight_required_input_missing");
}
