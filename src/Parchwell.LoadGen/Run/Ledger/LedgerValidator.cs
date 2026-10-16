using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Ledger;

/// <summary>Strict version 1 JSON codecs and in-memory bookkeeping reconciliation.</summary>
public static class LedgerValidator
{
    private static readonly string[] LifecycleEvents = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 32
    };

    private static readonly string[] RowProperties = ["schema_version", "identity", "attempt", "completion_source", "delivered", "error_code", "error_name", "persistence_status", "persistence_provenance", "completed_microseconds", "delivery_latency_ms", "synchronous_call_duration_ms"];
    private static readonly string[] IdentityProperties = ["scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"];
    private static readonly string[] AttemptProperties = ["instance_id", "attempt_id", "ordinal", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds", "invoked_microseconds"];
    private static readonly string[] ManifestProperties = ["schema_version", "identity", "generator_version", "planned_attempt_count", "attempt_count", "inventory", "timestamp_frequency", "start_timestamp", "worker_status", "bookkeeping_complete", "first_unexecuted_ordinal", "diagnostics", "runtime_versions", "evidence_files", "guard_observations"];

    /// <summary>Serializes a validated row as one compact UTF-8 JSON value.</summary>
    public static byte[] SerializeRow(LedgerRow row)
    {
        ValidateRow(row);
        return JsonSerializer.SerializeToUtf8Bytes(row, JsonOptions);
    }

    /// <summary>Serializes a row without allowing the JSON writer to grow beyond the supplied byte budget.</summary>
    public static byte[] SerializeRow(LedgerRow row, int maximumBytes)
    {
        ValidateRow(row);
        return SerializeBounded(row, maximumBytes);
    }

    /// <summary>Parses one strict row, rejecting duplicate, missing, unknown or malformed properties.</summary>
    public static LedgerRow DeserializeRow(ReadOnlySpan<byte> utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateProperties(document.RootElement);
        ValidateObject(document.RootElement, RowProperties);
        ValidateObjectProperty(document.RootElement, "identity", IdentityProperties);
        ValidateObjectProperty(document.RootElement, "attempt", AttemptProperties);
        var row = document.RootElement.Deserialize<LedgerRow>(JsonOptions) ?? throw new JsonException("null_ledger_row");
        ValidateRow(row);
        return row;
    }

    /// <summary>Serializes a complete independent manifest as UTF-8 JSON.</summary>
    public static byte[] SerializeManifest(RunManifest manifest)
    {
        ValidateManifest(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
    }

    /// <summary>Serializes a manifest without allowing the JSON writer to grow beyond the supplied byte budget.</summary>
    public static byte[] SerializeManifest(RunManifest manifest, int maximumBytes)
    {
        ValidateManifest(manifest);
        return SerializeBounded(manifest, maximumBytes);
    }

    /// <summary>Parses a strict manifest, including every nested inventory entry.</summary>
    public static RunManifest DeserializeManifest(ReadOnlySpan<byte> utf8Json)
        => DeserializeManifest(utf8Json, int.MaxValue);

    /// <summary>Parses a manifest only when its declared inventory fits the caller's entry budget.</summary>
    public static RunManifest DeserializeManifest(ReadOnlySpan<byte> utf8Json, int maximumInventoryEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumInventoryEntries);
        using var document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateProperties(document.RootElement);
        ValidateObject(document.RootElement, ManifestProperties);
        ValidateObjectProperty(document.RootElement, "identity", IdentityProperties);
        if (!document.RootElement.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
            throw new JsonException("manifest_inventory_must_be_array");
        if (inventory.GetArrayLength() > maximumInventoryEntries) throw new InvalidDataException("manifest_inventory_row_limit_exceeded");
        foreach (var item in inventory.EnumerateArray()) ValidateObject(item, AttemptProperties);
        var manifest = document.RootElement.Deserialize<RunManifest>(JsonOptions) ?? throw new JsonException("null_manifest");
        ValidateManifest(manifest);
        return manifest;
    }

    /// <summary>Reads both persisted files under explicit byte, row and JSON-line bounds, then reconciles them.</summary>
    public static async Task<ImmutableArray<LedgerRow>> ReadValidatedAsync(string ledgerPath, string manifestPath,
        RunIdentity expectedIdentity, int maximumRows, int maximumRowBytes, long maximumLedgerBytes,
        int maximumManifestBytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        expectedIdentity.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRowBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLedgerBytes);
        if (maximumLedgerBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumLedgerBytes), "ledger_readback_limit_exceeds_supported_memory_bound");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumManifestBytes);

        var manifestBytes = await ReadBoundedAsync(manifestPath, maximumManifestBytes, cancellationToken).ConfigureAwait(false);
        var manifest = DeserializeManifest(StripOneFinalLf(manifestBytes), maximumRows);

        var ledgerBytes = await ReadBoundedAsync(ledgerPath, maximumLedgerBytes, cancellationToken).ConfigureAwait(false);
        var rows = new List<LedgerRow>(Math.Min(manifest.Inventory.Length, maximumRows));
        var lineStart = 0;
        for (var index = 0; index < ledgerBytes.Length; index++)
        {
            if (ledgerBytes[index] != (byte)'\n') continue;
            var lineLength = index - lineStart;
            if (lineLength <= 0 || lineLength > maximumRowBytes) throw new InvalidDataException("invalid_ledger_line_length");
            if (rows.Count >= maximumRows) throw new InvalidDataException("ledger_row_limit_exceeded");
            rows.Add(DeserializeRow(ledgerBytes.AsSpan(lineStart, lineLength)));
            lineStart = index + 1;
        }
        if (lineStart != ledgerBytes.Length) throw new InvalidDataException("truncated_ledger_final_line");
        return Validate(rows, manifest, expectedIdentity);
    }

    /// <summary>Checks one terminal row's schema, identity, provenance and timing shape.</summary>
    public static void ValidateRow(LedgerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.SchemaVersion != 1 || row.Identity is null || row.Attempt is null)
            throw new InvalidDataException("invalid_ledger_schema_or_required_object");
        row.Identity.Validate();
        ValidateAttempt(row.Attempt, row.Identity);
        if (!string.Equals(row.Identity.InstanceId, row.Attempt.InstanceId, StringComparison.Ordinal) ||
            row.CompletedMicroseconds < row.Attempt.InvokedMicroseconds || string.IsNullOrWhiteSpace(row.CompletionSource) ||
            string.IsNullOrWhiteSpace(row.PersistenceProvenance))
            throw new InvalidDataException("invalid_ledger_identity_or_range");
        if (row.DeliveryLatencyMs is < 0 || row.SynchronousCallDurationMs is < 0 ||
            (row.DeliveryLatencyMs.HasValue && !double.IsFinite(row.DeliveryLatencyMs.Value)) ||
            (row.SynchronousCallDurationMs.HasValue && !double.IsFinite(row.SynchronousCallDurationMs.Value)))
            throw new InvalidDataException("invalid_ledger_timing");
        var callback = string.Equals(row.CompletionSource, "callback", StringComparison.Ordinal);
        var synchronous = string.Equals(row.CompletionSource, "synchronous_rejection", StringComparison.Ordinal);
        if ((!callback && !synchronous) || (callback && (!row.DeliveryLatencyMs.HasValue || row.SynchronousCallDurationMs.HasValue)) ||
            (synchronous && (row.DeliveryLatencyMs.HasValue || row.SynchronousCallDurationMs is null || row.PersistenceStatus.HasValue || row.PersistenceProvenance != "pre_enqueue")) ||
            (callback && row.PersistenceProvenance is not ("native_callback" or "missing")))
            throw new InvalidDataException("invalid_ledger_completion_provenance");
        if (row.PersistenceStatus is < 0 or > 2 ||
            (row.PersistenceProvenance == "native_callback" && row.PersistenceStatus is null) ||
            (row.PersistenceProvenance == "missing" && row.PersistenceStatus.HasValue) ||
            (synchronous && row.Delivered))
            throw new InvalidDataException("invalid_persistence_or_delivery_status");
        if (row.ErrorName is not null) ValidateMetadataText(row.ErrorName, "error_name");
        if (row.Delivered != (row.ErrorCode == 0)) throw new InvalidDataException("delivered_error_code_mismatch");
    }

    /// <summary>Checks manifest counts and inventory uniqueness without consulting ledger rows.</summary>
    public static void ValidateManifest(RunManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != 1 || manifest.Identity is null || manifest.Inventory.IsDefault ||
            manifest.Diagnostics.IsDefault || manifest.RuntimeVersions is null || manifest.EvidenceFiles is null || manifest.GuardObservations.IsDefault)
            throw new InvalidDataException("invalid_manifest_required_field");
        manifest.Identity.Validate();
        ValidateMetadataText(manifest.GeneratorVersion, "generator_version");
        ValidateMetadataText(manifest.WorkerStatus, "worker_status");
        foreach (var diagnostic in manifest.Diagnostics) ValidateMetadataText(diagnostic, "diagnostic");
        foreach (var observation in manifest.GuardObservations) ValidateMetadataText(observation, "guard_observation");
        foreach (var (key, value) in manifest.RuntimeVersions)
        {
            ValidateMetadataText(key, "runtime_version_name");
            ValidateMetadataText(value, "runtime_version");
        }
        foreach (var (key, value) in manifest.EvidenceFiles)
        {
            ValidateMetadataText(key, "evidence_file_name");
            ValidateMetadataText(value, "evidence_file_path");
        }
        if (manifest.GeneratorVersion != "1" || manifest.WorkerStatus is not ("complete" or "incomplete" or "failed") ||
            manifest.PlannedAttemptCount < 0 || manifest.AttemptCount != manifest.Inventory.Length ||
            manifest.TimestampFrequency <= 0 || manifest.StartTimestamp < 0 ||
            manifest.FirstUnexecutedOrdinal is < 0 || manifest.FirstUnexecutedOrdinal >= manifest.PlannedAttemptCount && manifest.FirstUnexecutedOrdinal.HasValue)
            throw new InvalidDataException("invalid_manifest_range_or_count");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var ordinals = new HashSet<long>();
        var logicalAttempts = new Dictionary<long, AttemptEntry>();
        long expectedOrdinal = 0;
        long scheduleProgress = 0;
        foreach (var entry in manifest.Inventory)
        {
            ValidateAttempt(entry, manifest.Identity);
            if (entry.Ordinal != expectedOrdinal++ || !ids.Add(entry.AttemptId) || !ordinals.Add(entry.Ordinal))
                throw new InvalidDataException("duplicate_manifest_inventory_entry");
            var logicalSlot = GetLogicalScheduleOrdinal(entry);
            if (logicalSlot >= manifest.PlannedAttemptCount)
                throw new InvalidDataException("attempt_exceeds_planned_schedule");
            if (logicalSlot == scheduleProgress)
            {
                logicalAttempts.Add(logicalSlot, entry);
                scheduleProgress = checked(scheduleProgress + 1);
            }
            else if (logicalSlot > scheduleProgress || !logicalAttempts.TryGetValue(logicalSlot, out var firstAttempt) ||
                !SameLogicalAttempt(firstAttempt, entry))
            {
                throw new InvalidDataException("invalid_retry_or_skipped_schedule_slot");
            }
        }
        var expectedSuffix = scheduleProgress < manifest.PlannedAttemptCount ? scheduleProgress : (long?)null;
        if (manifest.FirstUnexecutedOrdinal != expectedSuffix ||
            (manifest.WorkerStatus == "complete" &&
                (expectedSuffix.HasValue || !manifest.BookkeepingComplete || manifest.PlannedAttemptCount == 0)))
            throw new InvalidDataException("manifest_unexecuted_suffix_mismatch");
    }

    /// <summary>Requires exact inventory-to-row agreement, including the entire immutable entry and run identity.</summary>
    public static ImmutableArray<LedgerRow> Validate(IEnumerable<LedgerRow> rows, RunManifest manifest, RunIdentity expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ValidateManifest(manifest);
        expectedIdentity.Validate();
        if (manifest.Identity != expectedIdentity) throw new InvalidDataException("manifest_run_identity_mismatch");
        var expected = manifest.Inventory.ToDictionary(entry => entry.AttemptId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validated = ImmutableArray.CreateBuilder<LedgerRow>();
        foreach (var row in rows)
        {
            ValidateRow(row);
            if (row.Identity != expectedIdentity || !expected.TryGetValue(row.Attempt.AttemptId, out var entry) ||
                entry != row.Attempt || !seen.Add(row.Attempt.AttemptId))
                throw new InvalidDataException("ledger_inventory_mismatch_or_duplicate");
            validated.Add(row);
        }
        if (seen.Count != expected.Count) throw new InvalidDataException("ledger_inventory_missing_rows");
        return validated.ToImmutable();
    }

    internal static void ValidateAttempt(AttemptEntry entry, RunIdentity identity)
    {
        if (entry is null || !IsSafeControlText(entry.InstanceId) || !IsSafeControlText(entry.AttemptId) ||
            !IsSafeControlText(entry.TenantId) || !IsSafeControlText(entry.DocumentId) || !IsSafeControlText(entry.Key) ||
            !IsSafeControlText(entry.EventType) || !string.Equals(entry.InstanceId, identity.InstanceId, StringComparison.Ordinal) ||
            entry.Ordinal < 0 || entry.Sequence < 0 || entry.PlannedMicroseconds < 0 || entry.InvokedMicroseconds < entry.PlannedMicroseconds)
            throw new InvalidDataException("invalid_attempt_entry");

        if (entry.Sequence >= LifecycleEvents.Length ||
            !string.Equals(entry.EventType, LifecycleEvents[(int)entry.Sequence], StringComparison.Ordinal) ||
            !IsCanonicalDocumentId(entry.DocumentId) ||
            !string.Equals(entry.Key, string.Concat(entry.TenantId, "-", entry.DocumentId), StringComparison.Ordinal))
            throw new InvalidDataException("attempt_workload_identity_mismatch");
    }

    internal static long GetLogicalScheduleOrdinal(AttemptEntry entry)
    {
        var documentOrdinal = long.Parse(entry.DocumentId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);
        try
        {
            return checked(documentOrdinal * LifecycleEvents.Length + entry.Sequence);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("logical_schedule_ordinal_overflow", exception);
        }
    }

    internal static bool SameLogicalAttempt(AttemptEntry first, AttemptEntry retry) =>
        string.Equals(first.TenantId, retry.TenantId, StringComparison.Ordinal) &&
        string.Equals(first.DocumentId, retry.DocumentId, StringComparison.Ordinal) &&
        string.Equals(first.Key, retry.Key, StringComparison.Ordinal) && first.Sequence == retry.Sequence &&
        first.PlannedMicroseconds == retry.PlannedMicroseconds &&
        string.Equals(first.EventType, retry.EventType, StringComparison.Ordinal);

    private static bool IsCanonicalDocumentId(string documentId)
    {
        if (documentId.Length < 2 || documentId[0] != 'd' ||
            !long.TryParse(documentId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) || ordinal < 0)
            return false;
        return string.Equals(documentId, string.Concat("d", ordinal.ToString(CultureInfo.InvariantCulture)), StringComparison.Ordinal);
    }

    private static void ValidateMetadataText(string? value, string fieldName)
    {
        if (!IsSafeControlText(value)) throw new InvalidDataException($"invalid_manifest_text:{fieldName}");
    }

    private static bool IsSafeControlText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    private static void ValidateObjectProperty(JsonElement parent, string name, string[] expected)
    {
        if (!parent.TryGetProperty(name, out var element)) throw new JsonException($"missing_property:{name}");
        ValidateObject(element, expected);
    }

    private static void ValidateObject(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("object_required");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new JsonException($"duplicate_property:{property.Name}");
            if (!expected.Contains(property.Name, StringComparer.Ordinal)) throw new JsonException($"unknown_property:{property.Name}");
        }
        foreach (var property in expected)
            if (!names.Contains(property)) throw new JsonException($"missing_property:{property}");
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"duplicate_property:{property.Name}");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }

    private static byte[] SerializeBounded<T>(T value, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        using var stream = new BoundedMemoryStream(maximumBytes);
        JsonSerializer.Serialize(stream, value, JsonOptions);
        return stream.ToArray();
    }

    private static ReadOnlySpan<byte> StripOneFinalLf(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n') throw new InvalidDataException("manifest_missing_final_lf");
        return bytes.AsSpan(0, bytes.Length - 1);
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, long maximumBytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > maximumBytes || stream.Length > int.MaxValue)
            throw new InvalidDataException("persisted_artifact_byte_limit_exceeded");
        var result = new byte[(int)stream.Length];
        var offset = 0;
        while (offset < result.Length)
        {
            var read = await stream.ReadAsync(result.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("persisted_artifact_truncated_during_read");
            offset += read;
        }
        if (await stream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
            throw new InvalidDataException("persisted_artifact_grew_during_read");
        return result;
    }

    private sealed class BoundedMemoryStream(int maximumBytes) : Stream
    {
        private byte[] buffer = new byte[Math.Min(maximumBytes, 256)];
        private int length;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] destination, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] source, int offset, int count) => Write(source.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> source)
        {
            EnsureCapacity(source.Length);
            source.CopyTo(buffer.AsSpan(length));
            length += source.Length;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(source.Span);
            return ValueTask.CompletedTask;
        }
        public override Task WriteAsync(byte[] source, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(source.AsMemory(offset, count), cancellationToken).AsTask();
        public byte[] ToArray() => buffer.AsSpan(0, length).ToArray();
        private void EnsureCapacity(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            var required = checked(length + count);
            if (required > maximumBytes)
                throw new InvalidDataException("serialized_artifact_byte_limit_exceeded");
            if (required <= buffer.Length) return;
            var doubled = buffer.Length > maximumBytes / 2 ? maximumBytes : buffer.Length * 2;
            Array.Resize(ref buffer, Math.Min(maximumBytes, Math.Max(required, doubled)));
        }
    }
}
