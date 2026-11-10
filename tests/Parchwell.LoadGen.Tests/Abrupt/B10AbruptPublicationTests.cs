using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptPublicationTests
{
    private static readonly string[] FirstNames = ["registration.json", "journal.jsonl", "same_instance-pre-acceptance-report.json", "control-post-drain-report.json", "cross_restart-pre-acceptance-report.json", "abrupt-manifest.json", "termination.json"];

    [Fact]
    public async Task Given_ProducedFirstInstance_When_Published_Then_CohortFilesAreBoundAndFatalCoverageIsJournal()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            Assert.Equal(FirstNames.Order(StringComparer.Ordinal), Directory.GetFiles(Path.Combine(root, "first")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            Assert.Equal(B10AbruptPublisher.FirstMembers, FirstNames);
            Assert.Equal(7, Directory.GetFiles(Path.Combine(root, "successor")).Length);
            Dictionary<string, byte[]> files = Files(root);
            JsonElement manifest = B10AbruptWire.ReadEnvelope(files["abrupt-manifest.json"], "abrupt_manifest").GetProperty("data");
            var snapshot = B10AbruptJournal.Read(files["journal.jsonl"]);
            Assert.Empty(snapshot.Gaps);
            Assert.Equal(snapshot.Frames.Count, manifest.GetProperty("journal_records").GetInt64());
            B10AbruptPublisher.CheckDescriptor(manifest.GetProperty("journal"), "journal.jsonl", files);
            JsonElement[] descriptors = manifest.GetProperty("report_files").EnumerateArray().ToArray();
            Assert.Equal(B10AbruptPublisher.ReportMembers, descriptors.Select(entry => entry.GetProperty("name").GetString()));
            B10AbruptWire.Frame[] reports = snapshot.Frames.Where(frame => frame.Kind is "pre_acceptance_report" or "post_drain_report").ToArray();
            Assert.Equal(3, reports.Length);
            for (int index = 0; index < reports.Length; index++)
            {
                JsonElement entry = descriptors[index];
                string name = entry.GetProperty("name").GetString()!;
                B10AbruptPublisher.CheckDescriptor(entry, name, files);
                Assert.Equal(name, reports[index].Data.GetProperty("name").GetString());
                Assert.Equal(files[name].LongLength, reports[index].Data.GetProperty("length").GetInt64());
                Assert.Equal(B10AbruptPublisher.Hash(files[name]), reports[index].Data.GetProperty("sha256").GetString());
            }
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination").GetProperty("data");
            Assert.Equal(0, termination.GetProperty("native_error_count").GetInt64());
            Assert.Equal(0, termination.GetProperty("native_fatal_count").GetInt64());
            Assert.Equal("unknown_due_to_intended_kill", termination.GetProperty("native_tail").GetString());
            B10AbruptPublisher.CheckDescriptor(termination.GetProperty("manifest"), "abrupt-manifest.json", files);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("ledger_byte")]
    [InlineData("report_byte")]
    [InlineData("manifest_deleted")]
    [InlineData("report_length")]
    public async Task Given_CohortFileChangedAfterSeal_When_Publishing_Then_NothingBeyondTheRegistrationIsPublished(string change)
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Files(root); files.Remove("registration.json");
            if (change == "manifest_deleted") files.Remove("cross_restart-pre-acceptance-report.json");
            else
            {
                string name = change == "ledger_byte" ? "journal.jsonl" : "cross_restart-pre-acceptance-report.json";
                if (change == "report_length") files[name] = [.. files[name], 0];
                else { files[name] = (byte[])files[name].Clone(); files[name][0] ^= 1; }
            }
            var storage = new RecordingStorage(); var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
            var identity = Identity(root);
            await publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishFirstAsync(identity, files, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_publication_failed", failure.Message);
            Assert.Equal(["registration.json"], storage.Files.Keys.Select(Path.GetFileName));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Given_PredecessorBinding_When_CarryingACohortFile_Then_SuccessorRejectsReceiptChanged()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Files(root);
            JsonObject binding = Binding(files);
            Assert.Equal(["cross_host_timing", "first_pod", "release_utc", "runner_monotonic_ticks", "runner_utc", "start_line_binding", "termination"], binding.Select(entry => entry.Key).Order(StringComparer.Ordinal));
            var storage = Store(files); var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
            string path = Path.Combine(root, "binding.json");
            var options = B10AbruptWireProducerTests.TestOptions(root, "successor", path);
            await File.WriteAllTextAsync(path, binding.ToJsonString(), TestContext.Current.CancellationToken);
            Assert.NotNull(await publisher.LoadSelectionAsync(options, TestContext.Current.CancellationToken));
            binding["files"] = new JsonObject { ["timeout-ledger.jsonl"] = "{}" };
            await File.WriteAllTextAsync(path, binding.ToJsonString(), TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => publisher.LoadSelectionAsync(options, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_successor_binding_invalid:receipt_changed", failure.Message);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Given_NativeErrorsBeforeAndAfterTheKill_When_EventStreamIsDrained_Then_TerminationCountsThemFromTheJournal()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken, nativeErrors: true);
            var files = Files(root);
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination").GetProperty("data");
            Assert.Equal(3, termination.GetProperty("native_error_count").GetInt64());
            Assert.Equal(1, termination.GetProperty("native_fatal_count").GetInt64());
            Assert.Equal("unknown_due_to_intended_kill", termination.GetProperty("native_tail").GetString());
            var frames = B10AbruptJournal.Read(files["journal.jsonl"]).Frames;
            var native = frames.Where(frame => frame.Kind == "native_error").ToArray();
            Assert.Equal([1L, 2L, 3L], native.Select(frame => frame.Data.GetProperty("capture_sequence").GetInt64()));
            Assert.Equal([false, true, false], native.Select(frame => frame.Data.GetProperty("is_fatal").GetBoolean()));
            long intent = frames.Single(frame => frame.Kind == "kill_intent").Sequence;
            Assert.True(native[1].Sequence < intent);
            Assert.True(native[2].Sequence > intent);
            Assert.Equal(native.Length, termination.GetProperty("native_error_count").GetInt64());
            Assert.Equal(native.Count(frame => frame.Data.GetProperty("is_fatal").GetBoolean()), termination.GetProperty("native_fatal_count").GetInt64());
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Given_CohortFileUploads_When_Publishing_Then_EachAttemptIsOnStderrBeforeTheNextUpload()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Files(root); files.Remove("registration.json");
            var storage = new RecordingStorage(); var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
            var identity = Identity(root);
            await publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken);
            await publisher.PublishFirstAsync(identity, files, TestContext.Current.CancellationToken);
            Assert.Equal(FirstNames.SelectMany(name => new[] { "create:" + name, "read:" + name }), storage.Order);
            Assert.Equal(7, storage.Files.Count);
            Assert.Equal("create:termination.json", storage.Order[^2]);
            Assert.Equal("read:termination.json", storage.Order[^1]);
            Assert.All(files, file => Assert.Equal(file.Value, storage.Files[$"{identity.RunId}/C1/r1/b10-abrupt-diagnostic/{identity.PodName}/{file.Key}"]));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Given_SecondCohortUploadFails_When_Publishing_Then_ItsLineIsOnStderrBeforeTheAbortPathTouchesTheChild()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Files(root); files.Remove("registration.json");
            var storage = new RecordingStorage { FailMember = "same_instance-pre-acceptance-report.json" };
            var publisher = new B10AbruptPublisher(storage, TimeProvider.System); var identity = Identity(root);
            await publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<IOException>(() => publisher.PublishFirstAsync(identity, files, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
            Assert.Equal(["create:registration.json", "read:registration.json", "create:journal.jsonl", "read:journal.jsonl", "create:same_instance-pre-acceptance-report.json"], storage.Order);
            Assert.Equal(2, storage.Files.Count);
            Assert.DoesNotContain(storage.Files.Keys, name => name.EndsWith("termination.json", StringComparison.Ordinal));
            Assert.DoesNotContain(storage.Files.Keys, name => name.EndsWith("abrupt-manifest.json", StringComparison.Ordinal));
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("hash", "receipt_changed")]
    [InlineData("not-permitted", "receipt_changed")]
    [InlineData("kill-set", "selection_stale")]
    [InlineData("job", "job_mismatch")]
    public async Task Given_ChangedSuccessorBinding_When_Loaded_Then_FailsBeforeRegistration(string mutation, string detail)
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Files(root); JsonObject binding = Binding(files);
            switch (mutation)
            {
                case "hash":
                    string hash = binding["termination"]!["sha256"]!.GetValue<string>();
                    binding["termination"]!["sha256"] = (hash[0] == '0' ? "1" : "0") + hash[1..]; break;
                case "not-permitted":
                case "kill-set":
                    JsonObject receipt = JsonNode.Parse(files["termination.json"])!.AsObject();
                    if (mutation == "not-permitted") receipt["data"]!["successor_permitted"] = false;
                    else receipt["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("unknown");
                    files["termination.json"] = JsonSerializer.SerializeToUtf8Bytes(receipt);
                    binding["termination"] = JsonSerializer.SerializeToNode(B10AbruptPublisher.Describe("termination.json", files["termination.json"]), B10AbruptWire.JsonOptions); break;
                case "job": binding["first_pod"]!["job_name"] = "synthetic-abrupt-c1-r1-loadgen"; break;
            }
            string path = Path.Combine(root, "binding.json"); await File.WriteAllTextAsync(path, binding.ToJsonString(), TestContext.Current.CancellationToken);
            var storage = Store(files); var options = B10AbruptWireProducerTests.TestOptions(root, "successor", path);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => new B10AbruptPublisher(storage, TimeProvider.System).LoadSelectionAsync(options, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_successor_binding_invalid:" + detail, failure.Message);
            Assert.DoesNotContain(storage.Order, item => item.StartsWith("create:", StringComparison.Ordinal));
        }
        finally { Delete(root); }
    }

    internal static Dictionary<string, byte[]> Files(string root) => Directory.GetFiles(Path.Combine(root, "first")).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes, StringComparer.Ordinal);
    private static B10AbruptWire.Identity Identity(string root) => B10AbruptWire.ReadEnvelope(Files(root)["registration.json"], "registration").GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
    internal static JsonObject Binding(Dictionary<string, byte[]> files)
    {
        JsonElement receipt = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
        var first = receipt.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
        string release = B10AbruptJournal.Read(files["journal.jsonl"]).Frames.Single(frame => frame.Kind == "kill_intent").Data.GetProperty("release_utc").GetString()!;
        return JsonSerializer.SerializeToNode(new
        {
            first_pod = new { first.JobName, first.PodName, first.PodUid, first.ImageReference, first.SourceCommit, finished_utc = release },
            termination = B10AbruptPublisher.Describe("termination.json", files["termination.json"]), release_utc = release,
            runner_utc = release, runner_monotonic_ticks = 1L, cross_host_timing = "not_verified",
            start_line_binding = new { start_line = "{}", broker_inventory = "{}", completion = "{}" }
        }, B10AbruptWire.JsonOptions)!.AsObject();
    }
    private static RecordingStorage Store(Dictionary<string, byte[]> files)
    {
        var first = B10AbruptWire.ReadEnvelope(files["registration.json"], "registration").GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
        var storage = new RecordingStorage();
        foreach (var (name, bytes) in files) storage.Files.Add($"{first.RunId}/C1/r1/b10-abrupt-diagnostic/{first.PodName}/{name}", bytes);
        return storage;
    }
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "b10-publication-" + Guid.NewGuid().ToString("N"));
    private static void Delete(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed class RecordingStorage : IB10AbruptStorage
    {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        internal List<string> Order { get; } = [];
        internal string? FailMember { get; init; }
        public Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
        {
            string member = Path.GetFileName(name); Order.Add("create:" + member);
            if (member == FailMember) throw new IOException("synthetic_upload_failure");
            Files.Add(name, bytes); return Task.CompletedTask;
        }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken)
        { Order.Add("read:" + Path.GetFileName(name)); return Task.FromResult(Files[name]); }
    }
}
