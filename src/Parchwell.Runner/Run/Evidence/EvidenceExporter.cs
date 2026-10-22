using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Run.Evidence;

public sealed record EvidenceFile(string RelativePath, string LocalPath);
public sealed record EvidenceManifestEntry(string Path, long SizeBytes, string Sha256);
public sealed record EvidenceManifest(int SchemaVersion, string EnvironmentRunId, EvidenceManifestEntry[] Files);
public sealed record EvidenceObjectReceipt(string Path, string Sha256, long SizeBytes);

/// <summary>Private original-byte export with create-only objects and a manifest committed last.</summary>
public sealed class EvidenceExporter(IArtifactStore store, TimeProvider clock)
{
    public const long MaxPhaseBytes = 8L * 1024 * 1024 * 1024;
    public const int MaxEntries = 256;
    public const int MaxFileBytes = 268_435_456;
    public const int MaxManifestBytes = 1_048_576;

    public Task<PrivateExportReceipt> ExportAsync(string environmentRunId, IReadOnlyList<EvidenceFile> files,
        CancellationToken cancellationToken = default) => ExportCoreAsync(environmentRunId, Prefix(environmentRunId), "manifest.json", files, cancellationToken);

    public Task<PrivateExportReceipt> ExportRepeatAsync(string environmentRunId, string runId, IReadOnlyList<EvidenceFile> files,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        ValidateRunId(runId);
        if (!Enumerable.Range(1, 5).Any(ordinal => runId == environmentRunId + "-r" + ordinal))
            throw new InvalidDataException("repeat_export_identity_invalid");
        return ExportCoreAsync(environmentRunId, Prefix(environmentRunId) + runId + "/", "_private-export-manifest.json", files, cancellationToken);
    }

    public async Task<PrivateExportReceipt> ExportRecoveryAsync(string environmentRunId, string captureSha256,
        IReadOnlyList<EvidenceFile> files, CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        if (captureSha256.Length != 64 || captureSha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidDataException("recovery_capture_hash_invalid");
        var receipt = await ExportCoreAsync(environmentRunId,
            Prefix(environmentRunId) + "recovery/" + captureSha256.ToLowerInvariant() + "/", "_private-export-manifest.json", files, cancellationToken).ConfigureAwait(false);
        // A recovered snapshot cannot establish historical Pod/watch continuity.
        return receipt with { Complete = false };
    }

    public async Task<EvidenceObjectReceipt> UploadActualCostAsync(string environmentRunId, byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        if (bytes.Length > 1_048_576) throw new InvalidDataException("actual_cost_record_limit");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return await PutVerifiedAsync(Prefix(environmentRunId) + "cost/" + hash + ".json", bytes, deadline.Token).ConfigureAwait(false);
    }

    private async Task<PrivateExportReceipt> ExportCoreAsync(string environmentRunId, string prefix, string manifestFileName, IReadOnlyList<EvidenceFile> files,
        CancellationToken cancellationToken)
    {
        ValidateRunId(environmentRunId);
        if (files.Count > MaxEntries) throw new InvalidDataException("export_entry_limit");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(300), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var file in files)
        {
            ValidateRelativePath(file.RelativePath);
            if ((file.RelativePath == manifestFileName || file.RelativePath.StartsWith("cleanup/", StringComparison.Ordinal)) || !paths.Add(file.RelativePath))
                throw new InvalidDataException("export_path_conflict");
            var info = new FileInfo(file.LocalPath);
            if (!info.Exists || info.LinkTarget is not null) throw new InvalidDataException("export_file_invalid");
            total = checked(total + info.Length);
            if (info.Length > MaxFileBytes || total > MaxPhaseBytes) throw new InvalidDataException("export_byte_limit");
        }
        var entries = new List<EvidenceManifestEntry>();
        long uploadedTotal = 0;
        foreach (var file in files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
        {
            var length = new FileInfo(file.LocalPath).Length;
            if (length > MaxFileBytes || length < 0) throw new InvalidDataException("export_byte_limit");
            var bytes = await File.ReadAllBytesAsync(file.LocalPath, deadline.Token).ConfigureAwait(false);
            uploadedTotal = checked(uploadedTotal + bytes.LongLength);
            if (bytes.Length != length || uploadedTotal > MaxPhaseBytes) throw new InvalidDataException("export_file_changed_or_byte_limit");
            var receipt = await PutVerifiedAsync(prefix + file.RelativePath, bytes, deadline.Token).ConfigureAwait(false);
            entries.Add(new EvidenceManifestEntry(file.RelativePath, receipt.SizeBytes, receipt.Sha256));
        }
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new EvidenceManifest(1, environmentRunId, entries.ToArray()), LedgerJson.Options);
        if (manifest.Length > MaxManifestBytes) throw new InvalidDataException("export_manifest_limit");
        var committed = await PutVerifiedAsync(prefix + manifestFileName, manifest, deadline.Token).ConfigureAwait(false);
        return new PrivateExportReceipt(committed.Path, committed.Sha256, entries.Count, entries.Sum(e => e.SizeBytes));
    }

    public async Task<EvidenceObjectReceipt> UploadCleanupReceiptAsync(string environmentRunId, DestroyReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        if (receipt.EnvironmentRunId != environmentRunId || receipt.CompletedAt == default)
            throw new InvalidDataException("cleanup_generation_invalid");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, LedgerJson.Options);
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        return await PutVerifiedAsync(Prefix(environmentRunId) + "cleanup/" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".json", bytes, deadline.Token).ConfigureAwait(false);
    }

    public Task<byte[]> ReadVerifiedFileAsync(string environmentRunId, string relativePath, PrivateExportReceipt receipt,
        CancellationToken cancellationToken = default) => ReadVerifiedFileAsync(environmentRunId, relativePath, receipt, MaxFileBytes, cancellationToken);

    public async Task<byte[]> ReadVerifiedFileAsync(string environmentRunId, string relativePath, PrivateExportReceipt receipt,
        int maxBytes, CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        ValidateRelativePath(relativePath);
        if (receipt.ManifestPath != Prefix(environmentRunId) + "manifest.json" || !receipt.Complete)
            throw new InvalidDataException("private_manifest_identity_invalid");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var stored = await store.ReadAsync("evidence", receipt.ManifestPath, MaxManifestBytes, deadline.Token).ConfigureAwait(false)
            ?? throw new InvalidDataException("private_manifest_missing");
        if (Convert.ToHexStringLower(SHA256.HashData(stored.Bytes)) != receipt.ManifestSha256)
            throw new InvalidDataException("private_manifest_hash_mismatch");
        var manifest = JsonSerializer.Deserialize<EvidenceManifest>(stored.Bytes, LedgerJson.Options)
            ?? throw new InvalidDataException("private_manifest_invalid");
        if (manifest.SchemaVersion != 1 || manifest.EnvironmentRunId != environmentRunId || manifest.Files.Length > MaxEntries ||
            manifest.Files.Length != receipt.VerifiedFileCount || manifest.Files.Sum(file => file.SizeBytes) != receipt.VerifiedBytes ||
            manifest.Files.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count() != manifest.Files.Length)
            throw new InvalidDataException("private_manifest_invalid");
        var selected = manifest.Files.SingleOrDefault(file => file.Path == relativePath)
            ?? throw new InvalidDataException("private_manifest_file_missing");
        if (maxBytes is < 0 or > MaxFileBytes || selected.SizeBytes < 0 || selected.SizeBytes > maxBytes)
            throw new InvalidDataException("private_manifest_file_size_invalid");
        var content = await store.ReadAsync("evidence", Prefix(environmentRunId) + relativePath, (int)selected.SizeBytes, deadline.Token).ConfigureAwait(false)
            ?? throw new InvalidDataException("private_evidence_file_missing");
        if (content.Bytes.LongLength != selected.SizeBytes || Convert.ToHexStringLower(SHA256.HashData(content.Bytes)) != selected.Sha256)
            throw new InvalidDataException("private_evidence_file_hash_mismatch");
        return content.Bytes;
    }

    public async Task<EvidenceObjectReceipt> UploadGateAsync(string environmentRunId, byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        if (bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("gate_byte_limit");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return await PutVerifiedAsync(Prefix(environmentRunId) + "gate/" + hash + ".json", bytes, deadline.Token).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadGateAsync(string environmentRunId, string sha256,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(environmentRunId);
        if (sha256.Length != 64 || sha256.Any(c => !char.IsAsciiHexDigit(c))) throw new InvalidDataException("gate_hash_invalid");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var stored = await store.ReadAsync("evidence", Prefix(environmentRunId) + "gate/" + sha256.ToLowerInvariant() + ".json",
            16 * 1024 * 1024, deadline.Token).ConfigureAwait(false) ?? throw new InvalidDataException("private_gate_missing");
        if (stored.Bytes.Length > 16 * 1024 * 1024 || !Convert.ToHexStringLower(SHA256.HashData(stored.Bytes)).Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("private_gate_hash_mismatch");
        return stored.Bytes;
    }

    private async Task<EvidenceObjectReceipt> PutVerifiedAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var outcome = await store.WriteAsync("evidence", path, bytes, null, true, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(attempt), clock, cancellationToken).ConfigureAwait(false);
            try
            {
                var readback = await store.ReadAsync("evidence", path, bytes.Length, cancellationToken).ConfigureAwait(false);
                if (readback is not null && readback.Bytes.AsSpan().SequenceEqual(bytes))
                    return new EvidenceObjectReceipt(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength);
                if (outcome != ArtifactWriteOutcome.Uncertain) break;
            }
            catch (IOException) when (outcome == ArtifactWriteOutcome.Uncertain && attempt < 2) { }
        }
        throw new InvalidDataException("private_export_readback_conflict");
    }

    private static string Prefix(string runId) => "A01/" + runId + "/";
    private static void ValidateRunId(string value)
    {
        if (value.Length is < 1 or > 80 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new InvalidDataException("export_run_id_invalid");
    }
    private static void ValidateRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Contains('\\') ||
            value.Split('/').Any(p => p is "" or "." or "..") || value.Any(char.IsControl))
            throw new InvalidDataException("export_relative_path_invalid");
    }
}
