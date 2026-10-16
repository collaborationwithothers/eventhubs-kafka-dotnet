using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Published files accepted only after the index and every named payload pass bounded validation.</summary>
public sealed record RunArtifactIndex(int SchemaVersion, RunIdentity Identity, string WorkerStatus,
    bool BookkeepingComplete, bool WorkerComplete, bool PublicationComplete,
    ImmutableArray<RunArtifactDescriptor> Artifacts)
{
    /// <summary>Exact UTF-8 byte length of the index document that was parsed.</summary>
    public long IndexLength { get; init; }
}

/// <summary>One closed local payload named by the publication index.</summary>
public sealed record RunArtifactDescriptor(string Name, long Length, string Sha256);

/// <summary>Strict bounded reader for the index-last artifact acceptance protocol.</summary>
public static class RunArtifactIndexReader
{
    private static readonly string[] ArtifactNames =
    [
        "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"
    ];

    private static readonly string[] IdentityNames =
    ["scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"];

    /// <summary>Parses one UTF-8 index, rejecting malformed, incomplete, duplicate or mismatched publication metadata.</summary>
    public static async Task<RunArtifactIndex> ReadAsync(Stream indexStream, RunIdentity expectedIdentity,
        long maximumIndexBytes, long maximumArtifactBytes, long maximumAggregateArtifactBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexStream);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        expectedIdentity.Validate();
        ValidateBudgets(maximumIndexBytes, maximumArtifactBytes, maximumAggregateArtifactBytes);

        byte[] bytes = await ReadBoundedAsync(indexStream, maximumIndexBytes,
            "publication_index_exceeds_byte_limit", cancellationToken).ConfigureAwait(false);
        if (bytes.LongLength > maximumArtifactBytes) throw Invalid("publication_index_exceeds_artifact_byte_limit");
        using JsonDocument document = JsonDocument.Parse(bytes,
            new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        RejectDuplicateProperties(document.RootElement);
        JsonElement root = document.RootElement;
        RequireExactProperties(root, "schema_version", "identity", "worker_status", "bookkeeping_complete",
            "worker_complete", "publication_complete", "artifacts");

        int schemaVersion = RequireInt32(Get(root, "schema_version"), "schema_version");
        if (schemaVersion != 1) throw Invalid("publication_index_schema_unsupported");
        RunIdentity identity = ParseIdentity(Get(root, "identity"));
        if (identity != expectedIdentity) throw Invalid("publication_index_identity_mismatch");
        string workerStatus = RequireString(Get(root, "worker_status"), "worker_status");
        bool bookkeepingComplete = RequireBoolean(Get(root, "bookkeeping_complete"), "bookkeeping_complete");
        bool workerComplete = RequireBoolean(Get(root, "worker_complete"), "worker_complete");
        bool publicationComplete = RequireBoolean(Get(root, "publication_complete"), "publication_complete");
        if (!StringComparer.Ordinal.Equals(workerStatus, "complete") || !bookkeepingComplete || !workerComplete || !publicationComplete)
            throw Invalid("publication_index_incomplete");

        JsonElement artifactsElement = Get(root, "artifacts");
        if (artifactsElement.ValueKind != JsonValueKind.Array || artifactsElement.GetArrayLength() != ArtifactNames.Length)
            throw Invalid("publication_index_artifact_set_invalid");
        var artifacts = ImmutableArray.CreateBuilder<RunArtifactDescriptor>(ArtifactNames.Length);
        long aggregate = bytes.LongLength;
        int index = 0;
        foreach (JsonElement item in artifactsElement.EnumerateArray())
        {
            RequireExactProperties(item, "name", "length", "sha256");
            string name = RequireString(Get(item, "name"), "artifact_name");
            if (!StringComparer.Ordinal.Equals(name, ArtifactNames[index]))
                throw Invalid("publication_index_artifact_order_or_name_invalid");
            long length = RequireNonnegativeInt64(Get(item, "length"), "artifact_length");
            if (length > maximumArtifactBytes) throw Invalid("publication_artifact_exceeds_byte_limit");
            string hash = RequireString(Get(item, "sha256"), "artifact_sha256");
            if (hash.Length != 64 || hash.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
                throw Invalid("publication_artifact_sha256_invalid");
            try { aggregate = checked(aggregate + length); }
            catch (OverflowException) { throw Invalid("publication_aggregate_byte_limit_exceeded"); }
            if (aggregate > maximumAggregateArtifactBytes) throw Invalid("publication_aggregate_byte_limit_exceeded");
            artifacts.Add(new RunArtifactDescriptor(name, length, hash));
            index++;
        }

        return new RunArtifactIndex(schemaVersion, identity, workerStatus, bookkeepingComplete, workerComplete,
            publicationComplete, artifacts.ToImmutable()) { IndexLength = bytes.LongLength };
    }

    /// <summary>Requires a successful observed runner exit, exact prefix listing, bounded payload hashes, and valid ledger, manifest, and evidence content.</summary>
    /// <remarks>The caller must supply the runner's observed exit code, independently selected evidence expectations, and enumerate every member beneath the run prefix before calling. Partial listings are not accepted.</remarks>
    public static async Task ValidateArtifactSetAsync(RunArtifactIndex index, int? observedWorkerExitCode,
        RunEvidenceExpectations expectations, IReadOnlyCollection<string> observedPrefixMemberNames,
        Func<string, CancellationToken, ValueTask<Stream>> openArtifact, long maximumArtifactBytes,
        long maximumAggregateArtifactBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(observedPrefixMemberNames);
        ArgumentNullException.ThrowIfNull(openArtifact);
        if (observedWorkerExitCode != 0) throw Invalid("publication_worker_exit_code_invalid");
        index.Identity.Validate();
        ValidateBudgets(index.IndexLength, maximumArtifactBytes, maximumAggregateArtifactBytes);
        if (index.IndexLength > maximumArtifactBytes) throw Invalid("publication_index_exceeds_artifact_byte_limit");
        if (index.SchemaVersion != 1 || index.WorkerStatus != "complete" || !index.BookkeepingComplete ||
            !index.WorkerComplete || !index.PublicationComplete || index.Artifacts.IsDefault ||
            index.Artifacts.Length != ArtifactNames.Length)
            throw Invalid("publication_index_incomplete_or_malformed");

        string[] expectedMembers = [.. ArtifactNames, "index.json"];
        if (observedPrefixMemberNames.Count != expectedMembers.Length)
            throw Invalid("publication_prefix_member_set_incomplete");
        var expectedMemberSet = expectedMembers.ToHashSet(StringComparer.Ordinal);
        var observedMemberSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? member in observedPrefixMemberNames)
        {
            if (member is null || !expectedMemberSet.Contains(member) || !observedMemberSet.Add(member))
                throw Invalid("publication_prefix_member_set_invalid");
        }
        if (observedMemberSet.Count != expectedMemberSet.Count)
            throw Invalid("publication_prefix_member_set_incomplete");

        var payloads = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        long aggregate = index.IndexLength;
        for (int i = 0; i < ArtifactNames.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunArtifactDescriptor descriptor = index.Artifacts[i];
            if (!StringComparer.Ordinal.Equals(descriptor.Name, ArtifactNames[i]) || descriptor.Length < 0 ||
                descriptor.Length > maximumArtifactBytes || !IsSha256(descriptor.Sha256))
                throw Invalid("publication_index_artifact_set_invalid");
            try { aggregate = checked(aggregate + descriptor.Length); }
            catch (OverflowException) { throw Invalid("publication_aggregate_byte_limit_exceeded"); }
            if (aggregate > maximumAggregateArtifactBytes) throw Invalid("publication_aggregate_byte_limit_exceeded");

            await using Stream stream = await openArtifact(descriptor.Name, cancellationToken).ConfigureAwait(false)
                ?? throw Invalid("publication_artifact_missing:" + descriptor.Name);
            byte[] bytes = await ReadBoundedAsync(stream, descriptor.Length,
                "publication_artifact_exceeds_byte_limit", cancellationToken).ConfigureAwait(false);
            long length = bytes.LongLength;
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            payloads.Add(descriptor.Name, bytes);
            if (length != descriptor.Length || !StringComparer.Ordinal.Equals(hash, descriptor.Sha256))
                throw Invalid("publication_artifact_content_mismatch:" + descriptor.Name);
        }

        _ = ValidatePayloadSemantics(index.Identity, expectations, payloads.ToImmutable(), maximumArtifactBytes,
            cancellationToken);
    }

    /// <summary>Reconciles bounded payload bytes against caller-supplied evidence expectations for local publication and index readback.</summary>
    internal static RunManifest ValidatePayloadSemantics(RunIdentity identity, RunEvidenceExpectations expectations,
        IReadOnlyDictionary<string, byte[]> payloads, long maximumArtifactBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(payloads);
        identity.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumArtifactBytes, (long)int.MaxValue);
        if (payloads.Count != ArtifactNames.Length || ArtifactNames.Any(name => !payloads.ContainsKey(name)) ||
            payloads.Any(pair => pair.Value is null || pair.Value.LongLength > maximumArtifactBytes))
            throw Invalid("publication_artifact_set_incomplete_or_over_budget");

        byte[] ledgerBytes = payloads["ledger.jsonl"];
        byte[] manifestBytes = payloads["manifest.json"];
        byte[] statisticsBytes = payloads["statistics.jsonl"];
        byte[] lifecycleBytes = payloads["request-lifecycle.jsonl"];
        byte[] capabilityBytes = payloads["capabilities.json"];
        if (manifestBytes.Length == 0 || manifestBytes[^1] != (byte)'\n')
            throw Invalid("publication_manifest_missing_final_lf");
        RunManifest manifest = LedgerValidator.DeserializeManifest(manifestBytes.AsSpan(0, manifestBytes.Length - 1), int.MaxValue);
        if (manifest.Identity != identity || manifest.WorkerStatus != "complete" || !manifest.BookkeepingComplete)
            throw Invalid("publication_manifest_identity_or_status_mismatch");
        string[] evidenceNames = ArtifactNames.Skip(2).ToArray();
        if (manifest.EvidenceFiles.Count != evidenceNames.Length ||
            evidenceNames.Any(name => !manifest.EvidenceFiles.TryGetValue(name, out string? value) ||
                !StringComparer.Ordinal.Equals(value, name)))
            throw Invalid("publication_manifest_evidence_file_set_invalid");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<LedgerRow> rows = ParseLedgerRows(ledgerBytes, manifest.Inventory.Length, maximumArtifactBytes, cancellationToken);
            _ = LedgerValidator.Validate(rows, manifest, identity);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or OverflowException)
        {
            throw Invalid("publication_ledger_manifest_reconciliation_failed");
        }

        RunEvidenceReadResult evidence;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            int maximumRowBytes = (int)maximumArtifactBytes;
            evidence = RunEvidenceReader.ReadBytes(statisticsBytes, lifecycleBytes, capabilityBytes, identity,
                expectations,
                maximumArtifactBytes, maximumRowBytes, int.MaxValue);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or DecoderFallbackException)
        {
            throw Invalid("publication_evidence_validation_failed");
        }
        if (evidence.IsFaulted) throw Invalid("publication_evidence_capture_faulted");
        return manifest;
    }

    private static ImmutableArray<LedgerRow> ParseLedgerRows(byte[] bytes, int maximumRows, long maximumRowBytes,
        CancellationToken cancellationToken)
    {
        var rows = new List<LedgerRow>(maximumRows);
        int lineStart = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\n') continue;
            cancellationToken.ThrowIfCancellationRequested();
            int lineLength = index - lineStart;
            if (lineLength <= 0 || lineLength > maximumRowBytes)
                throw Invalid("publication_ledger_line_length_invalid");
            if (rows.Count >= maximumRows) throw Invalid("publication_ledger_row_count_exceeded");
            rows.Add(LedgerValidator.DeserializeRow(bytes.AsSpan(lineStart, lineLength)));
            lineStart = index + 1;
        }
        if (lineStart != bytes.Length) throw Invalid("publication_ledger_final_line_truncated");
        return rows.ToImmutableArray();
    }

    internal static IReadOnlyList<string> RequiredArtifactNames => ArtifactNames;

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, string limitError,
        CancellationToken cancellationToken)
    {
        if (maximumBytes > int.MaxValue) maximumBytes = int.MaxValue;
        using var output = new MemoryStream((int)Math.Min(maximumBytes, 81920));
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maximumBytes + 1 - output.Length)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumBytes) throw Invalid(limitError);
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static RunIdentity ParseIdentity(JsonElement element)
    {
        RequireExactProperties(element, IdentityNames);
        var identity = new RunIdentity(RequireString(Get(element, "scenario_id"), "scenario_id"),
            RequireString(Get(element, "configuration_id"), "configuration_id"), RequireString(Get(element, "run_id"), "run_id"),
            RequireString(Get(element, "instance_id"), "instance_id"), RequireString(Get(element, "expected_instance_id"), "expected_instance_id"),
            RequireString(Get(element, "client_id"), "client_id"));
        identity.Validate();
        return identity;
    }

    private static void ValidateBudgets(long maximumIndexBytes, long maximumArtifactBytes, long maximumAggregateArtifactBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIndexBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAggregateArtifactBytes);
        if (maximumIndexBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumIndexBytes), "Index materialization limit must fit in an Int32-sized buffer.");
        if (maximumArtifactBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumArtifactBytes), "Artifact materialization limit must fit in an Int32-sized buffer.");
    }

    private static bool IsSha256(string value) => value is { Length: 64 } &&
        value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

    private static JsonElement Get(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) ? value : throw Invalid("publication_required_property_missing:" + name);

    private static void RequireExactProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid("publication_object_required");
        var expected = names.ToHashSet(StringComparer.Ordinal);
        foreach (string name in names)
            if (!element.TryGetProperty(name, out _)) throw Invalid("publication_required_property_missing:" + name);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!expected.Contains(property.Name)) throw Invalid("publication_property_unknown:" + property.Name);
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid("publication_duplicate_property");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }

    private static int RequireInt32(JsonElement element, string field)
        => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value) ? value : throw Invalid("publication_int32_invalid:" + field);

    private static long RequireNonnegativeInt64(JsonElement element, string field)
        => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out long value) && value >= 0
            ? value : throw Invalid("publication_nonnegative_int64_invalid:" + field);

    private static string RequireString(JsonElement element, string field)
        => element.ValueKind == JsonValueKind.String && element.GetString() is { } value
            ? value : throw Invalid("publication_string_invalid:" + field);

    private static bool RequireBoolean(JsonElement element, string field)
        => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Invalid("publication_boolean_invalid:" + field)
        };

    private static InvalidDataException Invalid(string message) => new(message);
}
