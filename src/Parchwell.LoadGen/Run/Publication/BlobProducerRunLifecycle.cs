using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parchwell.LoadGen.Run.Execution;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Writes producer lifecycle records and bounded failure diagnostics to Blob Storage.</summary>
/// <param name="client">The configured Blob service client.</param>
/// <param name="clock">The time provider used for operation deadlines.</param>
public sealed class BlobProducerRunLifecycle(BlobServiceClient client, TimeProvider clock) : IProducerRunLifecycle
{
    private static readonly string[] PayloadNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private readonly BlobServiceClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken) =>
        UploadRecordAsync(context, options, context.RegistrationPath, CreateRecord(context, completion: false), options.CleanupTimeout, cancellationToken);

    /// <inheritdoc />
    public async Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(CreateRecord(context, completion: true));
        ValidateLifecycleRecordSize(bytes);
        await UploadBytesAsync(options, context.CompletionPath, bytes, options.CleanupTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
        ProducerLifecycleOptions options, CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        var snapshots = new List<(string Name, byte[] Bytes)>();
        var gaps = new List<string>();
        long aggregate = 0;
        bool aggregateLimitReached = false;
        foreach (string name in PayloadNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (aggregateLimitReached)
            {
                gaps.Add($"{name}:aggregate_limit");
                continue;
            }
            string path = Path.Combine(options.OutputRoot, name);
            if (result is not null)
            {
                if (result.ArtifactPaths.TryGetValue(name, out string? artifactPath) ||
                    result.ArtifactPaths.TryGetValue(Path.GetFileNameWithoutExtension(name), out artifactPath))
                {
                    if (artifactPath is null)
                    {
                        gaps.Add($"{name}:missing");
                        continue;
                    }
                    path = artifactPath;
                }
                else
                {
                    gaps.Add($"{name}:missing");
                    continue;
                }
            }
            long sourceLength;
            DateTime sourceWriteTime;
            try
            {
                using var deadline = new CancellationTokenSource(options.CleanupTimeout, _clock);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                sourceLength = input.Length;
                sourceWriteTime = File.GetLastWriteTimeUtc(path);
                if (sourceLength > options.MaxArtifactBytes || sourceLength > int.MaxValue)
                {
                    gaps.Add($"{name}:oversized");
                    continue;
                }
                byte[] bytes = new byte[(int)sourceLength];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = await input.ReadAsync(bytes.AsMemory(offset), linked.Token).ConfigureAwait(false);
                    if (read == 0) throw new IOException("diagnostic_snapshot_truncated");
                    offset += read;
                }
                byte[] extra = new byte[1];
                if (input.Length != bytes.LongLength || await input.ReadAsync(extra, linked.Token).ConfigureAwait(false) != 0 ||
                    File.GetLastWriteTimeUtc(path) != sourceWriteTime)
                {
                    gaps.Add($"{name}:changed");
                    continue;
                }
                aggregate = checked(aggregate + bytes.LongLength);
                if (aggregate > options.MaxAggregateArtifactBytes)
                {
                    gaps.Add($"{name}:aggregate_limit");
                    aggregateLimitReached = true;
                    continue;
                }
                snapshots.Add((name, bytes));
            }
            catch (FileNotFoundException)
            {
                gaps.Add($"{name}:missing");
            }
            catch (DirectoryNotFoundException)
            {
                gaps.Add($"{name}:missing");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                gaps.Add($"{name}:snapshot_timeout");
            }
            catch (Exception)
            {
                gaps.Add($"{name}:snapshot_failed");
            }
        }

        using var uploadDeadline = new CancellationTokenSource(options.UploadTimeout, _clock);
        using var uploadToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, uploadDeadline.Token);
        foreach ((string name, byte[] bytes) in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (uploadDeadline.IsCancellationRequested)
            {
                gaps.Add($"{name}:upload_deadline");
                continue;
            }
            try
            {
                await UploadBytesAsync(options, context.DiagnosticsPrefix + name, bytes,
                    options.UploadTimeout, uploadToken.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                gaps.Add($"{name}:upload_deadline");
            }
            catch (TimeoutException)
            {
                gaps.Add($"{name}:upload_timeout");
            }
            catch (Exception)
            {
                gaps.Add($"{name}:upload_failed");
            }
        }
        if (gaps.Count > 0) throw new DiagnosticPublicationException(gaps);
    }

    private async Task UploadRecordAsync(ProducerRunContext context, ProducerLifecycleOptions options, string path,
        object payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        context.Validate();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        ValidateLifecycleRecordSize(bytes);
        await UploadBytesAsync(options, path, bytes, timeout, cancellationToken).ConfigureAwait(false);
    }

    private void ValidateOptions(ProducerLifecycleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
            endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            !StringComparer.OrdinalIgnoreCase.Equals(endpoint.Host, _client.Uri.Host) || endpoint.Port != _client.Uri.Port)
            throw new ArgumentException("producer_lifecycle_endpoint_invalid", nameof(options));
        if (!StringComparer.Ordinal.Equals(options.Container, "ledger"))
            throw new ArgumentException("producer_lifecycle_container_invalid", nameof(options));
        if (options.CleanupTimeout <= TimeSpan.Zero || options.CleanupTimeout == Timeout.InfiniteTimeSpan ||
            options.UploadTimeout <= TimeSpan.Zero || options.UploadTimeout == Timeout.InfiniteTimeSpan ||
            options.CleanupTimeout.TotalMilliseconds > uint.MaxValue - 1d || options.UploadTimeout.TotalMilliseconds > uint.MaxValue - 1d)
            throw new ArgumentOutOfRangeException(nameof(options), "producer_lifecycle_timeout_must_be_positive_and_finite");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxAggregateArtifactBytes);
        if (options.MaxArtifactBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputRoot);
    }

    private async Task UploadBytesAsync(ProducerLifecycleOptions options, string path, byte[] bytes,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(timeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        BlobClient blob = _client.GetBlobContainerClient(options.Container).GetBlobClient(path);
        await using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            await blob.UploadAsync(stream, new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("producer_lifecycle_upload_timeout");
        }
    }

    private static void ValidateLifecycleRecordSize(byte[] bytes)
    {
        if (bytes.Length > 65536) throw new InvalidDataException("producer_lifecycle_record_too_large");
    }

    internal sealed class DiagnosticPublicationException(IReadOnlyCollection<string> gaps)
        : IOException("producer_diagnostics_incomplete:" + string.Join(',', gaps))
    {
        public IReadOnlyCollection<string> Gaps { get; } = gaps;
    }

    private static object CreateRecord(ProducerRunContext context, bool completion)
    {
        context.Validate();
        return completion
            ? new CompletionRecord(context)
            : new RegistrationRecord(context);
    }

    private sealed record RegistrationRecord([property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("identity")] object Identity,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("repeat_number")] int RepeatNumber,
        [property: JsonPropertyName("job_name")] string JobName,
        [property: JsonPropertyName("pod_name")] string PodName,
        [property: JsonPropertyName("pod_uid")] string PodUid,
        [property: JsonPropertyName("source_commit")] string SourceCommit)
    {
        public RegistrationRecord(ProducerRunContext c) : this(1, IdentityOf(c), c.Phase, c.RepeatNumber, c.JobName, c.PodName, c.PodUid, c.SourceCommit) { }
    }
    private sealed record CompletionRecord([property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("identity")] object Identity,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("repeat_number")] int RepeatNumber,
        [property: JsonPropertyName("job_name")] string JobName,
        [property: JsonPropertyName("pod_name")] string PodName,
        [property: JsonPropertyName("pod_uid")] string PodUid,
        [property: JsonPropertyName("source_commit")] string SourceCommit,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("handler_exit_code")] int HandlerExitCode,
        [property: JsonPropertyName("cleanup_completed")] bool CleanupCompleted)
    {
        public CompletionRecord(ProducerRunContext c) : this(1, IdentityOf(c), c.Phase, c.RepeatNumber, c.JobName, c.PodName, c.PodUid, c.SourceCommit, "loadgen", 0, true) { }
    }

    private static object IdentityOf(ProducerRunContext c) => new
    {
        scenario_id = c.Identity.ScenarioId, configuration_id = c.Identity.ConfigurationId, run_id = c.Identity.RunId,
        instance_id = c.Identity.InstanceId, expected_instance_id = c.Identity.ExpectedInstanceId, client_id = c.Identity.ClientId
    };
}
