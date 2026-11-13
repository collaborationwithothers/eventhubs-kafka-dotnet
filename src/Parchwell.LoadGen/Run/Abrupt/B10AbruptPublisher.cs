using Azure.Storage.Blobs;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;

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

internal sealed class B10AbruptPublisher(IB10AbruptStorage storage, TimeProvider clock, string purpose = "diagnostic_trial")
{
    internal static readonly string[] ReportMembers = ["same_instance-pre-acceptance-report.json", "control-post-drain-report.json", "cross_restart-pre-acceptance-report.json"];
    internal static readonly string[] FirstMembers = ["registration.json", "journal.jsonl", .. ReportMembers, "abrupt-manifest.json", "termination.json"];
    internal static readonly string[] SuccessorMembers = ["registration.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "completion.json"];
    private static readonly TimeSpan UploadBudget = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, byte[]> _published = new(StringComparer.Ordinal);

    internal async Task<byte[]> RegisterDiagnosticAsync(B10AbruptWire.Identity identity, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity);
        byte[] bytes = B10AbruptWire.SerializeEnvelope("registration", identity, new { registered_utc = Utc(clock.GetUtcNow()) }, purpose);
        await PublishAsync(identity, "registration.json", bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    internal async Task PublishFirstAsync(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> files, CancellationToken cancellationToken,
        B10AbruptTimeline? timeline = null)
    {
        if (identity.PredecessorInstanceId is not null || !files.TryGetValue("journal.jsonl", out byte[]? journalBytes) ||
            !files.TryGetValue("abrupt-manifest.json", out byte[]? manifestBytes) || !files.TryGetValue("termination.json", out byte[]? terminationBytes))
            throw PublicationFailed();
        var journal = B10AbruptJournal.Read(journalBytes, identity, purpose);
        var reports = journal.Frames.Where(frame => frame.Kind is "pre_acceptance_report" or "post_drain_report").ToArray();
        string[] reportNames = reports.Select(frame => frame.Data.GetProperty("name").GetString()!).ToArray();
        RequireSet(files, ["journal.jsonl", .. reportNames.Distinct(StringComparer.Ordinal), "abrupt-manifest.json", "termination.json"]);
        JsonElement manifest;
        JsonElement termination;
        try
        {
            manifest = B10AbruptWire.ReadEnvelope(manifestBytes, "abrupt_manifest", purpose, identity.Repeat);
            termination = B10AbruptWire.ReadEnvelope(terminationBytes, "termination", purpose, identity.Repeat);
            if (manifest.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != identity ||
                termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != identity) throw PublicationFailed();
            JsonElement terminationData = termination.GetProperty("data");
            if (!NativeCountsMatch(terminationData, journal.Frames)) throw PublicationFailed();
            JsonElement data = manifest.GetProperty("data");
            CheckDescriptor(data.GetProperty("journal"), "journal.jsonl", files);
            CheckDescriptor(termination.GetProperty("data").GetProperty("manifest"), "abrupt-manifest.json", files);
            if (data.GetProperty("journal_records").GetInt64() != journal.Frames.Count ||
                data.GetProperty("report_files").GetArrayLength() != reports.Length) throw PublicationFailed();
            for (int i = 0; i < reports.Length; i++)
            {
                string name = reportNames[i];
                if (!ReportMembers.Contains(name, StringComparer.Ordinal) || !files.TryGetValue(name, out byte[]? bytes) ||
                    reports[i].Data.GetProperty("length").GetInt64() != bytes.LongLength ||
                    reports[i].Data.GetProperty("sha256").GetString() != Hash(bytes)) throw PublicationFailed();
                CheckDescriptor(data.GetProperty("report_files")[i], name, files);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw PublicationFailed(exception); }
        await PublishAsync(identity, "journal.jsonl", journalBytes, cancellationToken).ConfigureAwait(false);
        foreach (string name in reportNames.Distinct(StringComparer.Ordinal))
            await PublishAsync(identity, name, files[name], cancellationToken).ConfigureAwait(false);
        await PublishAsync(identity, "abrupt-manifest.json", manifestBytes, cancellationToken).ConfigureAwait(false);
        await PublishAsync(identity, "termination.json", terminationBytes, cancellationToken).ConfigureAwait(false);
        timeline?.Mark(B10AbruptStage.PublicationComplete);
    }

    internal async Task PublishSuccessorAsync(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> payloads,
        B10AbruptWire.Descriptor firstTermination, B10AbruptWire.Descriptor journal, IReadOnlyList<string> killUnconfirmedAttemptIds,
        DateTimeOffset readyUtc, DateTimeOffset releaseUtc, object invocations, bool cleanupCompleted, CancellationToken cancellationToken)
    {
        if (identity.PredecessorInstanceId is null || !cleanupCompleted || killUnconfirmedAttemptIds.Count == 0) throw PublicationFailed();
        RequireSet(payloads, SuccessorMembers.Skip(1).SkipLast(1));
        foreach (string name in SuccessorMembers.Skip(1).SkipLast(1))
            await PublishAsync(identity, name, payloads[name], cancellationToken).ConfigureAwait(false);
        if (!_published.TryGetValue("registration.json", out byte[]? registration)) throw PublicationFailed();
        var receipt = new
        {
            first_termination = firstTermination,
            journal, kill_unconfirmed_attempt_ids = killUnconfirmedAttemptIds,
            registration = Describe("registration.json", registration),
            artifacts = SuccessorMembers.Skip(1).SkipLast(1).Select(name => Describe(name, payloads[name])).ToArray(),
            ready_utc = Utc(readyUtc), release_utc = Utc(releaseUtc), invocations,
            cleanup_completed = cleanupCompleted, cross_host_timing = "not_verified"
        };
        await PublishAsync(identity, "completion.json", B10AbruptWire.SerializeEnvelope("successor_receipt", identity, receipt, purpose), cancellationToken).ConfigureAwait(false);
    }

    internal async Task<SuccessorInput> LoadSelectionAsync(B10AbruptOptions options, CancellationToken cancellationToken)
    {
        try
        {
            JsonElement root = B10AbruptWire.Parse(await ReadFileAsync(options.PredecessorBindingPath, 65536, cancellationToken).ConfigureAwait(false));
            B10AbruptWire.ExactMembers(root, "first_pod", "termination", "release_utc", "runner_utc", "runner_monotonic_ticks", "cross_host_timing", "start_line_binding");
            if (root.GetProperty("cross_host_timing").GetString() != "not_verified" ||
                !B10AbruptWire.IsUtc(root.GetProperty("runner_utc").GetString() ?? "")) throw Binding("selection_stale");
            _ = B10AbruptWire.Nonnegative(root.GetProperty("runner_monotonic_ticks"));
            JsonElement startLine = root.GetProperty("start_line_binding");
            B10AbruptWire.ExactMembers(startLine, "start_line", "broker_inventory", "completion");
            foreach (var member in startLine.EnumerateObject())
                if (member.Value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(member.Value.GetString())) throw Binding("receipt_changed");
            JsonElement observed = root.GetProperty("first_pod");
            B10AbruptWire.ExactMembers(observed, "job_name", "pod_name", "pod_uid", "image_reference", "source_commit", "finished_utc");
            if (observed.GetProperty("job_name").GetString() != $"{options.RunId}-c1-r{options.Repeat.ToString(CultureInfo.InvariantCulture)}-loadgen-restart") throw Binding("job_mismatch");
            string podName = observed.GetProperty("pod_name").GetString() ?? throw Binding("predecessor_mismatch");
            if (string.IsNullOrWhiteSpace(podName) || podName.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) ||
                podName.Contains('/', StringComparison.Ordinal) || podName.Contains('\\') || podName is "." or "..") throw Binding("predecessor_mismatch");
            string prefix = B10AbruptWire.ArtifactPrefix(options.Identity() with { PodName = podName }, options.Purpose);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            files.Add("termination.json", await ReadPublishedAsync(prefix + "termination.json", 65536, cancellationToken).ConfigureAwait(false));
            CheckDescriptor(root.GetProperty("termination"), "termination.json", files);
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination", options.Purpose, options.Repeat);
            var first = termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
            if (first.RunId != options.RunId || first.ConfigurationId != options.ConfigurationId || first.Repeat != options.Repeat || first.Phase != options.Phase || first.ScenarioId != options.ScenarioId || first.PredecessorInstanceId is not null)
                throw Binding("predecessor_mismatch");
            if (first.SourceCommit != options.SourceCommit) throw Binding("source_mismatch");
            if (first.ImageReference != options.ImageReference) throw Binding("image_mismatch");
            if (first.PodUid == options.PodUid || first.PodName == options.PodName || first.InstanceId == options.PodName) throw Binding("pod_uid_reused");
            if (first.JobName != $"{options.RunId}-c1-r{options.Repeat.ToString(CultureInfo.InvariantCulture)}-loadgen-restart") throw Binding("job_mismatch");
            ValidateIdentity(first);
            if (observed.GetProperty("pod_uid").GetString() != first.PodUid || podName != first.PodName) throw Binding("predecessor_mismatch");
            if (observed.GetProperty("source_commit").GetString() != first.SourceCommit) throw Binding("source_mismatch");
            if (observed.GetProperty("image_reference").GetString() != first.ImageReference) throw Binding("image_mismatch");
            if (!B10AbruptWire.IsUtc(observed.GetProperty("finished_utc").GetString() ?? "")) throw Binding("predecessor_mismatch");
            JsonElement terminationData = termination.GetProperty("data");
            if (terminationData.GetProperty("successor_permitted").ValueKind != JsonValueKind.True ||
                terminationData.GetProperty("termination").GetString() != "intended_self_kill") throw Binding("receipt_changed");
            files.Add("abrupt-manifest.json", await ReadPublishedAsync(prefix + "abrupt-manifest.json", 65536, cancellationToken).ConfigureAwait(false));
            CheckDescriptor(terminationData.GetProperty("manifest"), "abrupt-manifest.json", files);
            JsonElement manifest = B10AbruptWire.ReadEnvelope(files["abrupt-manifest.json"], "abrupt_manifest", options.Purpose, options.Repeat);
            if (manifest.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions) != first) throw Binding("predecessor_mismatch");
            JsonElement manifestData = manifest.GetProperty("data");
            files.Add("journal.jsonl", await ReadPublishedAsync(prefix + "journal.jsonl", 16777216, cancellationToken).ConfigureAwait(false));
            CheckDescriptor(manifestData.GetProperty("journal"), "journal.jsonl", files);
            var snapshot = B10AbruptJournal.Read(files["journal.jsonl"], first, options.Purpose);
            if (snapshot.Gaps.Count != 0 || snapshot.Failures.Count != 0 || manifestData.GetProperty("gaps").GetArrayLength() != 0 ||
                manifestData.GetProperty("journal_records").GetInt64() != snapshot.Frames.Count) throw Binding("receipt_changed");
            B10AbruptWire.Frame[] reportRecords = snapshot.Frames.Where(frame => frame.Kind is "pre_acceptance_report" or "post_drain_report").ToArray();
            JsonElement reportFiles = manifestData.GetProperty("report_files");
            if (reportFiles.GetArrayLength() != reportRecords.Length) throw Binding("receipt_changed");
            for (int i = 0; i < reportRecords.Length; i++)
            {
                JsonElement descriptor = reportFiles[i];
                JsonElement recorded = reportRecords[i].Data;
                if (descriptor.GetProperty("name").GetString() != recorded.GetProperty("name").GetString() ||
                    descriptor.GetProperty("length").GetInt64() != recorded.GetProperty("length").GetInt64() ||
                    descriptor.GetProperty("sha256").GetString() != recorded.GetProperty("sha256").GetString()) throw Binding("receipt_changed");
            }
            if (!NativeCountsMatch(terminationData, snapshot.Frames)) throw Binding("receipt_changed");
            string[] selected = terminationData.GetProperty("kill_unconfirmed_attempt_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
            if (selected.Length == 0 || !snapshot.KillUnconfirmedAttemptIds.SequenceEqual(selected, StringComparer.Ordinal)) throw Binding("selection_stale");
            B10AbruptWire.Frame? intent = snapshot.Frames.SingleOrDefault(frame => frame.Kind == "kill_intent");
            if (intent is null || root.GetProperty("release_utc").GetString() != intent.Data.GetProperty("release_utc").GetString()) throw Binding("selection_stale");
            DateTimeOffset release = DateTimeOffset.ParseExact(intent.Data.GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
            JsonElement entries = B10AbruptWire.Element(selected.Select(id => snapshot.Frames.Single(frame => frame.Kind == "prepared" &&
                frame.Data.GetProperty("attempt_id").GetString() == id).Data).ToArray());
            return new(first, termination, Describe("termination.json", files["termination.json"]), Describe("journal.jsonl", files["journal.jsonl"]), entries, release, selected);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404) { throw Binding("receipt_missing", exception); }
        catch (Azure.RequestFailedException exception) { throw Binding("receipt_changed", exception); }
        catch (FileNotFoundException exception) { throw Binding("receipt_missing", exception); }
        catch (DirectoryNotFoundException exception) { throw Binding("receipt_missing", exception); }
        catch (InvalidDataException exception) when (!exception.Message.StartsWith("b10_abrupt_successor_binding_invalid", StringComparison.Ordinal))
        { throw Binding("receipt_changed", exception); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { throw Binding("receipt_changed", exception); }
    }

    private static bool NativeCountsMatch(JsonElement terminationData, IReadOnlyList<B10AbruptWire.Frame> frames) =>
        terminationData.GetProperty("native_error_count").GetInt64() == frames.LongCount(frame => frame.Kind == "native_error") &&
        terminationData.GetProperty("native_fatal_count").GetInt64() == frames.LongCount(frame => frame.Kind == "native_error" && frame.Data.GetProperty("is_fatal").GetBoolean());

    private async Task<byte[]> ReadPublishedAsync(string name, int limit, CancellationToken cancellationToken)
    {
        byte[] bytes = await storage.ReadAsync(name, limit, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > limit) throw Binding("receipt_changed");
        return bytes;
    }

    private async Task PublishAsync(B10AbruptWire.Identity identity, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        ValidateIdentity(identity);
        string[] allowed = identity.PredecessorInstanceId is null ? FirstMembers : SuccessorMembers;
        if (_published.ContainsKey(name)) throw B10AbruptDiagnostics.Attach(new IOException("b10_abrupt_publication_ambiguous"));
        if (!allowed.Contains(name, StringComparer.Ordinal) || bytes.Length > 268435456 ||
            _published.Values.Sum(value => (long)value.Length) + bytes.Length > 1073741824) throw PublicationFailed();
        string path = B10AbruptWire.ArtifactPrefix(identity, purpose) + name;
        using var deadline = new CancellationTokenSource(UploadBudget, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await storage.CreateAsync(path, bytes, linked.Token).ConfigureAwait(false);
        }
        // Create exceptions do not establish whether any SDK attempt took effect.
        catch (Exception exception) { throw B10AbruptDiagnostics.Attach(new IOException("b10_abrupt_publication_ambiguous", exception)); }
        byte[] actual;
        try { actual = await storage.ReadAsync(path, bytes.Length, linked.Token).ConfigureAwait(false); }
        catch (InvalidDataException exception) when (exception.Message == "b10_abrupt_publication_failed") { throw; }
        catch (Exception exception) { throw B10AbruptDiagnostics.Attach(new IOException("b10_abrupt_publication_ambiguous", exception)); }
        if (!actual.AsSpan().SequenceEqual(bytes)) throw PublicationFailed();
        _published.Add(name, bytes);
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
            if (output.Length + read > limit) throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_publication_failed"));
            output.Write(buffer, 0, read);
        }
    }
    internal static void CheckDescriptor(JsonElement descriptor, string name, IReadOnlyDictionary<string, byte[]> files)
    {
        B10AbruptWire.ExactMembers(descriptor, "name", "length", "sha256");
        if (descriptor.GetProperty("name").GetString() != name || !files.TryGetValue(name, out byte[]? bytes) ||
            descriptor.GetProperty("length").GetInt64() != bytes.Length || descriptor.GetProperty("sha256").GetString() != Hash(bytes)) throw Binding("receipt_changed");
    }
    private void ValidateIdentity(B10AbruptWire.Identity identity)
    {
        B10AbruptWire.ValidateIdentity(B10AbruptWire.Element(identity), purpose);
        if (identity.InstanceId != identity.PodName || identity.JobName != $"{identity.RunId}-c1-r{identity.Repeat.ToString(CultureInfo.InvariantCulture)}-loadgen-{(identity.PredecessorInstanceId is null ? "restart" : "successor")}") throw PublicationFailed();
    }
    private static void RequireSet(IReadOnlyDictionary<string, byte[]> files, IEnumerable<string> expected)
    { if (!files.Keys.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw PublicationFailed(); }
    private static InvalidDataException PublicationFailed(Exception? inner = null, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_publication_failed", inner), [], file, member, line);
    private static InvalidDataException Binding(string detail, Exception? inner = null, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_successor_binding_invalid:" + detail, inner), [B10AbruptDiagnostics.Fact(B10AbruptFact.SuccessorDetail, B10AbruptDiagnostics.SuccessorDetailCode(detail))], file, member, line);
    internal sealed record SuccessorInput(B10AbruptWire.Identity FirstIdentity, JsonElement Termination, B10AbruptWire.Descriptor FirstTermination,
        B10AbruptWire.Descriptor Journal,
        JsonElement Entries, DateTimeOffset ReleaseUtc, IReadOnlyList<string> KillUnconfirmedAttemptIds);
}
