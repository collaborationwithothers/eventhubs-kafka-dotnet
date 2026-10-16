using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Publishes one fully reconciled worker result to its unique, create-only Blob path.</summary>
public sealed class BlobRunArtifactPublisher : IRunArtifactPublisher
{
    private const int BufferSize = 81920;
    private const string LedgerContainer = "ledger";
    private readonly BlobServiceClient _client;
    private readonly TimeProvider _clock;

    /// <summary>Creates a publisher from the SDK client and injected clock owned by the composition root.</summary>
    public BlobRunArtifactPublisher(BlobServiceClient client, TimeProvider clock)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ValidateEndpoint(client.Uri);
    }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public async Task PublishAsync(RunExecutionResult result, RunPublicationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        ValidateEndpoint(options.Endpoint);
        if (!SameEndpoint(new Uri(options.Endpoint, UriKind.Absolute), _client.Uri))
            throw new ArgumentException("publication_endpoint_does_not_match_blob_client", nameof(options));
        if (!StringComparer.Ordinal.Equals(options.Container, LedgerContainer))
            throw new ArgumentException("publication_container_must_be_ledger", nameof(options));
        ValidateSuccessfulResult(result);

        using var deadline = new CancellationTokenSource(options.UploadTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken token = linked.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            ImmutableArray<ArtifactSource> sources = await OpenAndHashSourcesAsync(result, options, token).ConfigureAwait(false);
            try
            {
                await PublishOpenedSourcesAsync(result, options, sources, token).ConfigureAwait(false);
            }
            finally
            {
                foreach (ArtifactSource source in sources) await source.Stream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("artifact_publication_timeout");
        }
    }

    internal async Task PublishOpenedSourcesAsync(RunExecutionResult result, RunPublicationOptions options,
        ImmutableArray<ArtifactSource> sources, CancellationToken cancellationToken)
    {
        var expectations = result.EvidenceExpectations
            ?? throw new InvalidDataException("publication_evidence_expectations_missing");
        ImmutableDictionary<string, byte[]> payloads = await ReadAndVerifySourcesAsync(sources,
            options.MaxArtifactBytes, cancellationToken).ConfigureAwait(false);
        RunManifest persistedManifest = RunArtifactIndexReader.ValidatePayloadSemantics(result.Manifest.Identity, expectations,
            payloads, options.MaxArtifactBytes, cancellationToken);
        ValidatePersistedManifest(result, payloads["manifest.json"], persistedManifest, options);

        string directory = Path.GetDirectoryName(sources[0].Path)!;
        string localIndexPath = Path.Combine(directory, "index.json");
        if (File.Exists(localIndexPath)) throw new IOException("publication_local_index_already_exists");

        long aggregate = sources.Sum(source => source.Length);
        byte[] indexBytes = SerializeIndex(result, sources);
        if (indexBytes.LongLength > options.MaxArtifactBytes)
            throw new InvalidDataException("publication_index_exceeds_artifact_byte_limit");
        try { aggregate = checked(aggregate + indexBytes.LongLength); }
        catch (OverflowException) { throw new InvalidDataException("publication_aggregate_byte_limit_exceeded"); }
        if (aggregate > options.MaxAggregateArtifactBytes)
            throw new InvalidDataException("publication_aggregate_byte_limit_exceeded");

        BlobContainerClient container = _client.GetBlobContainerClient(options.Container);
        string prefix = GetRunPrefix(result.Manifest.Identity);
        foreach (ArtifactSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UploadSourceAsync(container, prefix, source, options.MaxArtifactBytes, cancellationToken).ConfigureAwait(false);
            await VerifySourceUnchangedAsync(source, options.MaxArtifactBytes, cancellationToken).ConfigureAwait(false);
        }

        await SaveIndexCreateOnlyAsync(localIndexPath, indexBytes, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await using var indexStream = new MemoryStream(indexBytes, writable: false);
        BlobClient indexBlob = container.GetBlobClient(prefix + "/index.json");
        await indexBlob.UploadAsync(indexStream, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateSuccessfulResult(RunExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result.Manifest);
        ArgumentNullException.ThrowIfNull(result.ArtifactPaths);
        if (result.EvidenceExpectations is null)
            throw new InvalidDataException("publication_evidence_expectations_missing");
        _ = GetRunPrefix(result.Manifest.Identity);
        if (result.ExitCode != 0 || result.Manifest.WorkerStatus != "complete" || !result.Manifest.BookkeepingComplete)
            throw new InvalidDataException("publication_requires_complete_worker_and_bookkeeping");
        LedgerValidator.ValidateManifest(result.Manifest);
    }

    private static void ValidateOptions(RunPublicationOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxAggregateArtifactBytes);
        if (options.MaxArtifactBytes > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options), "publication_artifact_limit_exceeds_supported_reader_bound");
        if (options.UploadTimeout <= TimeSpan.Zero || options.UploadTimeout == Timeout.InfiniteTimeSpan ||
            options.UploadTimeout.TotalMilliseconds > uint.MaxValue - 1d)
            throw new ArgumentOutOfRangeException(nameof(options), "publication_timeout_must_be_positive_and_finite");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Container);
    }

    private static void ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException("publication_endpoint_must_be_absolute_https", nameof(endpoint));
        ValidateEndpoint(uri);
    }

    private static void ValidateEndpoint(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri || !StringComparer.OrdinalIgnoreCase.Equals(endpoint.Scheme, Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment) || endpoint.AbsolutePath != "/")
            throw new ArgumentException("publication_endpoint_must_be_https_service_root_without_credentials_or_query");
    }

    private static bool SameEndpoint(Uri left, Uri right)
        => Uri.Compare(left, right, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static string GetRunPrefix(RunIdentity identity)
    {
        identity.Validate();
        foreach (string segment in new[] { identity.ScenarioId, identity.ConfigurationId, identity.RunId, identity.InstanceId })
        {
            if (segment is "." or "..")
                throw new InvalidDataException("publication_identity_dot_segment_not_allowed");
        }
        return string.Join('/', Uri.EscapeDataString(identity.ScenarioId), Uri.EscapeDataString(identity.ConfigurationId),
            Uri.EscapeDataString(identity.RunId), Uri.EscapeDataString(identity.InstanceId));
    }

    internal static async Task<ImmutableArray<ArtifactSource>> OpenAndHashSourcesAsync(RunExecutionResult result,
        RunPublicationOptions options, CancellationToken cancellationToken)
    {
        var paths = RequiredPaths(result);
        string root = Path.GetDirectoryName(paths[0].Path)!;
        foreach (ArtifactSourcePath path in paths)
        {
            if (!string.Equals(root, Path.GetDirectoryName(path.Path), StringComparerForPaths))
                throw new InvalidDataException("publication_artifacts_must_share_run_directory");
            if (!StringComparer.Ordinal.Equals(Path.GetFileName(path.Path), path.Name))
                throw new InvalidDataException("publication_artifact_filename_mismatch:" + path.Name);
        }

        var sources = ImmutableArray.CreateBuilder<ArtifactSource>(paths.Length);
        long aggregate = 0;
        try
        {
            foreach (ArtifactSourcePath path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stream = new FileStream(path.Path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var source = new ArtifactSource(path.Name, path.Path, stream, stream.Length, string.Empty);
                sources.Add(source);
                if (source.Length > options.MaxArtifactBytes)
                    throw new InvalidDataException("publication_artifact_exceeds_byte_limit:" + source.Name);
                try { aggregate = checked(aggregate + source.Length); }
                catch (OverflowException) { throw new InvalidDataException("publication_aggregate_byte_limit_exceeded"); }
                if (aggregate > options.MaxAggregateArtifactBytes)
                    throw new InvalidDataException("publication_aggregate_byte_limit_exceeded");
            }

            for (int index = 0; index < sources.Count; index++)
            {
                ArtifactSource source = sources[index];
                string hash = await HashStreamAsync(source.Stream, options.MaxArtifactBytes, cancellationToken).ConfigureAwait(false);
                if (source.Stream.Length != source.Length) throw new InvalidDataException("publication_artifact_mutated_during_hash:" + source.Name);
                source.Stream.Position = 0;
                sources[index] = source with { Sha256 = hash };
            }
            return sources.ToImmutable();
        }
        catch
        {
            foreach (ArtifactSource source in sources) await source.Stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static ImmutableArray<ArtifactSourcePath> RequiredPaths(RunExecutionResult result)
    {
        var required = ImmutableArray.CreateBuilder<ArtifactSourcePath>(5);
        string[] evidenceNames = RunArtifactIndexReader.RequiredArtifactNames.Skip(2).ToArray();
        if (result.Manifest.EvidenceFiles.Count != evidenceNames.Length ||
            evidenceNames.Any(name => !result.Manifest.EvidenceFiles.TryGetValue(name, out string? declaredName) ||
                !StringComparer.Ordinal.Equals(declaredName, name)))
            throw new InvalidDataException("publication_manifest_evidence_file_set_invalid");
        string Get(string key) => result.ArtifactPaths.TryGetValue(key, out string? path) && !string.IsNullOrWhiteSpace(path)
            ? Path.GetFullPath(path) : throw new InvalidDataException("publication_artifact_path_missing:" + key);

        required.Add(new ArtifactSourcePath("ledger.jsonl", Get("ledger")));
        required.Add(new ArtifactSourcePath("manifest.json", Get("manifest")));
        foreach (string name in RunArtifactIndexReader.RequiredArtifactNames.Skip(2))
        {
            string path = Get(name);
            required.Add(new ArtifactSourcePath(name, path));
        }
        return required.ToImmutable();
    }

    private static async Task<ImmutableDictionary<string, byte[]>> ReadAndVerifySourcesAsync(
        ImmutableArray<ArtifactSource> sources, long maximumArtifactBytes, CancellationToken cancellationToken)
    {
        var payloads = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (ArtifactSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Length < 0 || source.Length > maximumArtifactBytes || source.Length > int.MaxValue)
                throw new InvalidDataException("publication_artifact_exceeds_byte_limit:" + source.Name);

            byte[] bytes = new byte[(int)source.Length];
            source.Stream.Position = 0;
            try
            {
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = await source.Stream.ReadAsync(bytes.AsMemory(offset), cancellationToken).ConfigureAwait(false);
                    if (read == 0) throw new InvalidDataException("publication_artifact_truncated_during_validation:" + source.Name);
                    offset += read;
                }
                byte[] overflowProbe = new byte[1];
                if (await source.Stream.ReadAsync(overflowProbe, cancellationToken).ConfigureAwait(false) != 0)
                    throw new InvalidDataException("publication_artifact_grew_during_validation:" + source.Name);
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (source.Stream.Length != source.Length || !StringComparer.Ordinal.Equals(hash, source.Sha256))
                    throw new InvalidDataException("publication_artifact_mutated_during_validation:" + source.Name);
                payloads.Add(source.Name, bytes);
            }
            finally
            {
                source.Stream.Position = 0;
            }
        }
        return payloads.ToImmutable();
    }

    private static void ValidatePersistedManifest(RunExecutionResult result, byte[] actual,
        RunManifest manifest, RunPublicationOptions options)
    {
        byte[] expected = LedgerValidator.SerializeManifest(result.Manifest, (int)options.MaxArtifactBytes);
        if (actual.LongLength != expected.LongLength + 1 || actual[^1] != (byte)'\n' ||
            !expected.AsSpan().SequenceEqual(actual.AsSpan(0, actual.Length - 1)) ||
            manifest.Identity != result.Manifest.Identity || manifest.WorkerStatus != "complete" || !manifest.BookkeepingComplete)
            throw new InvalidDataException("publication_persisted_manifest_mismatch");
    }

    private static async Task UploadSourceAsync(BlobContainerClient container, string prefix, ArtifactSource source,
        long maximumArtifactBytes, CancellationToken cancellationToken)
    {
        source.Stream.Position = 0;
        BlobClient blob = container.GetBlobClient(prefix + "/" + source.Name);
        await blob.UploadAsync(source.Stream, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (source.Stream.Length > maximumArtifactBytes)
            throw new InvalidDataException("publication_artifact_exceeds_byte_limit:" + source.Name);
    }

    private static async Task VerifySourceUnchangedAsync(ArtifactSource source, long maximumArtifactBytes,
        CancellationToken cancellationToken)
    {
        source.Stream.Position = 0;
        string hash = await HashStreamAsync(source.Stream, maximumArtifactBytes, cancellationToken).ConfigureAwait(false);
        if (source.Stream.Length != source.Length || !StringComparer.Ordinal.Equals(hash, source.Sha256))
            throw new InvalidDataException("publication_artifact_mutated_during_upload:" + source.Name);
        source.Stream.Position = 0;
    }

    private static async Task<string> HashStreamAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            try { total = checked(total + read); }
            catch (OverflowException) { throw new InvalidDataException("publication_artifact_exceeds_byte_limit"); }
            if (total > maximumBytes) throw new InvalidDataException("publication_artifact_exceeds_byte_limit");
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static byte[] SerializeIndex(RunExecutionResult result, ImmutableArray<ArtifactSource> sources)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", 1);
            writer.WritePropertyName("identity");
            writer.WriteStartObject();
            WriteIdentity(writer, result.Manifest.Identity);
            writer.WriteEndObject();
            writer.WriteString("worker_status", "complete");
            writer.WriteBoolean("bookkeeping_complete", true);
            writer.WriteBoolean("worker_complete", true);
            writer.WriteBoolean("publication_complete", true);
            writer.WritePropertyName("artifacts");
            writer.WriteStartArray();
            foreach (ArtifactSource source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString("name", source.Name);
                writer.WriteNumber("length", source.Length);
                writer.WriteString("sha256", source.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static void WriteIdentity(Utf8JsonWriter writer, RunIdentity identity)
    {
        writer.WriteString("scenario_id", identity.ScenarioId);
        writer.WriteString("configuration_id", identity.ConfigurationId);
        writer.WriteString("run_id", identity.RunId);
        writer.WriteString("instance_id", identity.InstanceId);
        writer.WriteString("expected_instance_id", identity.ExpectedInstanceId);
        writer.WriteString("client_id", identity.ClientId);
    }

    private static async Task SaveIndexCreateOnlyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static StringComparison StringComparerForPaths
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record ArtifactSourcePath(string Name, string Path);
    internal sealed record ArtifactSource(string Name, string Path, FileStream Stream, long Length, string Sha256);
}
