using Azure.Storage.Blobs;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Parchwell.LoadGen.Run.Abrupt;

internal interface IB10AbruptStorage
{
    Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken);
}

internal sealed class B10AbruptBlobStorage(BlobServiceClient client) : IB10AbruptStorage
{
    public async Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        await client.GetBlobContainerClient("ledger").GetBlobClient(name).UploadAsync(stream, overwrite: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    public async Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken)
    {
        var response = await client.GetBlobContainerClient("ledger").GetBlobClient(name)
            .DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var stream = response.Value.Content;
        return await B10AbruptPublisher.ReadBoundedAsync(stream, maximumBytes, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class B10AbruptPublisher(IB10AbruptStorage storage, TimeProvider clock)
{
    internal static readonly string[] FirstMembers = ["registration.json", "journal.jsonl", "timeout-seal.json", "control-seal.json", "kill-selection.json", "abrupt-manifest.json", "termination.json"];
    internal static readonly string[] SuccessorMembers = ["registration.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "completion.json"];
    private readonly Dictionary<string, byte[]> _published = new(StringComparer.Ordinal);

    internal async Task<byte[]> RegisterDiagnosticAsync(B10AbruptWire.Identity identity, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity);
        byte[] bytes = B10AbruptWire.SerializeEnvelope("registration", identity, new { registered_utc = Utc(clock.GetUtcNow()) });
        await PublishAsync(identity, "registration.json", bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    internal async Task PublishFirstAsync(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> files, CancellationToken cancellationToken)
    {
        if (identity.PredecessorInstanceId is not null) throw PublicationFailed();
        RequireSet(files, FirstMembers.Where(name => name != "registration.json"));
        foreach (string name in FirstMembers.Skip(1))
            await PublishAsync(identity, name, files[name], cancellationToken).ConfigureAwait(false);
    }

    internal async Task PublishSuccessorAsync(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> payloads,
        JsonElement firstTermination, JsonElement selection, DateTimeOffset readyUtc, DateTimeOffset releaseUtc,
        object invocations, bool cleanupCompleted, CancellationToken cancellationToken)
    {
        if (identity.PredecessorInstanceId is null || !cleanupCompleted) throw PublicationFailed();
        RequireSet(payloads, SuccessorMembers.Skip(1).SkipLast(1));
        foreach (string name in SuccessorMembers.Skip(1).SkipLast(1))
            await PublishAsync(identity, name, payloads[name], cancellationToken).ConfigureAwait(false);
        if (!_published.TryGetValue("registration.json", out byte[]? registration)) throw PublicationFailed();
        var receipt = new
        {
            first_termination = Describe("termination.json", Encoding.UTF8.GetBytes(firstTermination.GetRawText())),
            selection = Describe("kill-selection.json", Encoding.UTF8.GetBytes(selection.GetRawText())),
            registration = Describe("registration.json", registration),
            artifacts = payloads.Select(pair => Describe(pair.Key, pair.Value)).ToArray(),
            ready_utc = Utc(readyUtc), release_utc = Utc(releaseUtc), invocations,
            cleanup_completed = cleanupCompleted, cross_host_timing = "not_verified"
        };
        await PublishAsync(identity, "completion.json", B10AbruptWire.SerializeEnvelope("successor_receipt", identity, receipt), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<SuccessorInput> LoadSelectionAsync(B10AbruptOptions options, CancellationToken cancellationToken)
    {
        try
        {
            byte[] binding = await ReadFileAsync(options.PredecessorBindingPath, 65536, cancellationToken).ConfigureAwait(false);
            JsonElement root = B10AbruptWire.Parse(binding);
            B10AbruptWire.ExactMembers(root, "files", "first_pod", "release_utc", "runner_utc", "runner_monotonic_ticks", "cross_host_timing", "start_line_binding");
            if (root.GetProperty("cross_host_timing").GetString() != "not_verified") throw Binding("selection_stale");
            JsonElement objects = root.GetProperty("files");
            if (!objects.TryGetProperty("termination.json", out _)) throw Binding("receipt_missing");
            string[] included = FirstMembers.Where(name => name != "journal.jsonl").ToArray();
            B10AbruptWire.ExactMembers(objects, included);
            var files = objects.EnumerateObject().ToDictionary(property => property.Name,
                property => Encoding.UTF8.GetBytes(property.Value.GetString() ?? throw Binding("receipt_missing")), StringComparer.Ordinal);
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
            var first = termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
            ValidateIdentity(first);
            if (first.RunId != options.RunId || first.ConfigurationId != options.ConfigurationId || first.Repeat != options.Repeat || first.Phase != options.Phase || first.ScenarioId != options.ScenarioId)
                throw Binding("predecessor_mismatch");
            if (first.SourceCommit != options.SourceCommit) throw Binding("source_mismatch");
            if (first.ImageReference != options.ImageReference) throw Binding("image_mismatch");
            if (first.PodUid == options.PodUid || first.PodName == options.PodName || first.InstanceId == options.PodName) throw Binding("pod_uid_reused");
            if (first.JobName != options.JobName.TrimEnd('2')) throw Binding("job_mismatch");
            JsonElement observed = root.GetProperty("first_pod");
            B10AbruptWire.ExactMembers(observed, "job_name", "pod_name", "pod_uid", "image_reference", "source_commit", "finished_utc");
            if (observed.GetProperty("job_name").GetString() != first.JobName) throw Binding("job_mismatch");
            if (observed.GetProperty("pod_uid").GetString() != first.PodUid || observed.GetProperty("pod_name").GetString() != first.PodName) throw Binding("predecessor_mismatch");
            if (observed.GetProperty("source_commit").GetString() != first.SourceCommit) throw Binding("source_mismatch");
            if (observed.GetProperty("image_reference").GetString() != first.ImageReference) throw Binding("image_mismatch");
            if (!B10AbruptWire.IsUtc(observed.GetProperty("finished_utc").GetString() ?? "")) throw Binding("predecessor_mismatch");
            JsonElement terminationData = termination.GetProperty("data");
            if (terminationData.GetProperty("mechanical_status").GetString() != "complete" || !terminationData.GetProperty("eof_complete").GetBoolean()) throw Binding("receipt_changed");
            CheckDescriptor(terminationData.GetProperty("manifest"), "abrupt-manifest.json", files);
            JsonElement manifest = B10AbruptWire.ReadEnvelope(files["abrupt-manifest.json"], "abrupt_manifest");
            JsonElement manifestData = manifest.GetProperty("data");
            foreach (var pair in new[] { ("registration", "registration.json"), ("timeout_seal", "timeout-seal.json"), ("control_seal", "control-seal.json"), ("selection", "kill-selection.json") })
                CheckDescriptor(manifestData.GetProperty(pair.Item1), pair.Item2, files);
            if (manifestData.GetProperty("gaps").GetArrayLength() != 0) throw Binding("receipt_changed");
            foreach (var file in files.Where(pair => pair.Key != "termination.json"))
            {
                JsonElement document = B10AbruptWire.Parse(file.Value);
                if (document.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != first) throw Binding("predecessor_mismatch");
            }
            JsonElement selection = B10AbruptWire.ReadEnvelope(files["kill-selection.json"], "selection");
            JsonElement data = selection.GetProperty("data");
            JsonElement checkpoint = data.GetProperty("checkpoint");
            byte[] checkpointBytes = Encoding.UTF8.GetBytes(checkpoint.GetRawText());
            _ = B10AbruptWire.ReadFrame(checkpointBytes);
            CheckDescriptor(data.GetProperty("checkpoint_descriptor"), "cross_restart-checkpoint.json",
                new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["cross_restart-checkpoint.json"] = checkpointBytes });
            DateTimeOffset release = DateTimeOffset.ParseExact(checkpoint.GetProperty("data").GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
            if (root.GetProperty("release_utc").GetString() != Utc(release) || data.GetProperty("entries").GetArrayLength() != 1) throw Binding("selection_stale");
            string[] selected = checkpoint.GetProperty("data").GetProperty("selected_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
            string[] candidates = data.GetProperty("candidate_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
            if (selected.Length != 1 || !candidates.SequenceEqual(selected, StringComparer.Ordinal) ||
                data.GetProperty("terminal_exclusions").GetArrayLength() != 0) throw Binding("selection_stale");
            foreach (JsonElement entry in data.GetProperty("entries").EnumerateArray())
            {
                B10AbruptWire.ExactMembers(entry, "attempt_id", "key", "sequence", "payload_length", "payload_sha256", "payload_base64");
                if (entry.GetProperty("attempt_id").GetString() != selected[0]) throw Binding("selection_stale");
                byte[] payload = Convert.FromBase64String(entry.GetProperty("payload_base64").GetString()!);
                if (payload.Length > 900000 || payload.Length != entry.GetProperty("payload_length").GetInt64() || Hash(payload) != entry.GetProperty("payload_sha256").GetString() ||
                    entry.GetProperty("key").GetString() != $"B10/{options.RunId}/C1/1/cross_restart" || entry.GetProperty("sequence").GetInt64() != 0) throw Binding("selection_stale");
            }
            return new(first, termination, selection, release);
        }
        catch (FileNotFoundException exception) { throw Binding("receipt_missing", exception); }
        catch (DirectoryNotFoundException exception) { throw Binding("receipt_missing", exception); }
        catch (InvalidDataException exception) when (!exception.Message.StartsWith("b10_abrupt_successor_binding_invalid", StringComparison.Ordinal))
        { throw Binding("receipt_changed", exception); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { throw Binding("receipt_changed", exception); }
    }

    private async Task PublishAsync(B10AbruptWire.Identity identity, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity);
        string[] allowed = identity.PredecessorInstanceId is null ? FirstMembers : SuccessorMembers;
        if (!allowed.Contains(name, StringComparer.Ordinal) || _published.ContainsKey(name) || bytes.Length > 268435456 ||
            _published.Values.Sum(value => (long)value.Length) + bytes.Length > 1073741824) throw PublicationFailed();
        string path = $"{identity.RunId}/C1/r1/b10-abrupt-diagnostic/{identity.PodName}/{name}";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await storage.CreateAsync(path, bytes, linked.Token).ConfigureAwait(false);
            byte[] actual = await storage.ReadAsync(path, bytes.Length, linked.Token).ConfigureAwait(false);
            if (!actual.AsSpan().SequenceEqual(bytes)) throw PublicationFailed();
            _published.Add(name, bytes);
        }
        catch (OperationCanceledException exception) { throw new IOException("b10_abrupt_publication_ambiguous", exception); }
        catch (Exception exception) when (exception is not InvalidDataException) { throw new IOException("b10_abrupt_publication_failed", exception); }
    }

    internal static B10AbruptWire.Descriptor Describe(string name, byte[] bytes) => new(name, bytes.LongLength, Hash(bytes));
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    internal static async Task<byte[]> ReadFileAsync(string path, int limit, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ReadBoundedAsync(stream, limit, cancellationToken).ConfigureAwait(false);
    }
    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[Math.Min(65536, limit + 1)];
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit + 1 - checked((int)output.Length))), cancellationToken).ConfigureAwait(false);
            if (read == 0) return output.ToArray();
            if (output.Length + read > limit) throw new InvalidDataException("b10_abrupt_publication_failed");
            output.Write(buffer, 0, read);
        }
    }
    internal static void CheckDescriptor(JsonElement descriptor, string name, IReadOnlyDictionary<string, byte[]> files)
    {
        B10AbruptWire.ExactMembers(descriptor, "name", "length", "sha256");
        if (descriptor.GetProperty("name").GetString() != name || !files.TryGetValue(name, out byte[]? bytes) ||
            descriptor.GetProperty("length").GetInt64() != bytes.Length || descriptor.GetProperty("sha256").GetString() != Hash(bytes)) throw Binding("receipt_changed");
    }
    private static void ValidateIdentity(B10AbruptWire.Identity identity)
    {
        B10AbruptWire.ValidateIdentity(B10AbruptWire.Element(identity));
        if (identity.InstanceId != identity.PodName || identity.JobName != $"{identity.RunId}-c1-r1-loadgen{(identity.PredecessorInstanceId is null ? "" : "2")}") throw PublicationFailed();
    }
    private static void RequireSet(IReadOnlyDictionary<string, byte[]> files, IEnumerable<string> expected)
    { if (!files.Keys.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw PublicationFailed(); }
    private static InvalidDataException PublicationFailed() => new("b10_abrupt_publication_failed");
    private static InvalidDataException Binding(string detail, Exception? inner = null) => new("b10_abrupt_successor_binding_invalid:" + detail, inner);
    internal sealed record SuccessorInput(B10AbruptWire.Identity FirstIdentity, JsonElement Termination, JsonElement Selection, DateTimeOffset ReleaseUtc);
}
