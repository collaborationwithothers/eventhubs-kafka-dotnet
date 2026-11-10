using System.Globalization;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
/// <summary>
/// The first instance publishes the eight cohort files the verifier must reconcile on its own (PR3a, T1 to T3, T10 and T14).
/// Every case runs the real supervisor, publisher, ledger store and file storage on produced bytes.
/// </summary>
public sealed class B10AbruptPublicationTests
{
    private static readonly string[] FirstNames =
    [
        "registration.json", "journal.jsonl", "timeout-seal.json", "control-seal.json", "timeout-manifest.json", "timeout-ledger.jsonl",
        "timeout-post-drain-report.json", "control-manifest.json", "control-ledger.jsonl", "control-post-drain-report.json",
        "same_instance-pre-acceptance-report.json", "cross_restart-pre-acceptance-report.json", "kill-selection.json",
        "abrupt-manifest.json", "termination.json"
    ];

    // T1: each cohort file's published bytes are the bytes the seals and checkpoints bind, and the fatal coverage fields are the new literals.
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
            Dictionary<string, byte[]> files = FirstNames.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(root, "first", name)), StringComparer.Ordinal);

            JsonElement manifest = B10AbruptWire.ReadEnvelope(files["abrupt-manifest.json"], "abrupt_manifest").GetProperty("data");
            JsonElement[] cohort = manifest.GetProperty("cohort_files").EnumerateArray().ToArray();
            Assert.Equal(B10AbruptPublisher.CohortMembers, cohort.Select(entry => entry.GetProperty("name").GetString()));
            foreach (JsonElement entry in cohort)
                B10AbruptPublisher.CheckDescriptor(entry, entry.GetProperty("name").GetString()!, files);

            JsonElement timeout = B10AbruptWire.ReadEnvelope(files["timeout-seal.json"], "cohort_seal").GetProperty("data");
            JsonElement control = B10AbruptWire.ReadEnvelope(files["control-seal.json"], "cohort_seal").GetProperty("data");
            // Binding is by role: a seal descriptor carries the producer's local name, the published member its own.
            Assert.Equal(Describe(files["timeout-manifest.json"]), Bound(timeout, "inventory"));
            Assert.Equal(Describe(files["timeout-ledger.jsonl"]), Bound(timeout, "ledger"));
            Assert.Equal(Describe(files["timeout-post-drain-report.json"]), Bound(timeout, "post_drain_statistics"));
            Assert.Equal(Describe(files["control-manifest.json"]), Bound(control, "inventory"));
            Assert.Equal(Describe(files["control-ledger.jsonl"]), Bound(control, "ledger"));
            Assert.Equal(Describe(files["control-post-drain-report.json"]), Bound(control, "post_drain_statistics"));
            foreach (string arm in new[] { "same_instance", "cross_restart" })
            {
                JsonElement checkpoint = ReadJournal(files["journal.jsonl"]).Single(frame => frame.GetProperty("kind").GetString() == "checkpoint"
                    && frame.GetProperty("data").GetProperty("arm").GetString() == arm).GetProperty("data").GetProperty("pre_acceptance_report");
                Assert.Equal(Describe(files[arm + "-pre-acceptance-report.json"]), (checkpoint.GetProperty("length").GetInt64(), checkpoint.GetProperty("sha256").GetString()));
            }

            foreach (JsonElement seal in new[] { timeout, control })
            {
                Assert.Equal("journal", seal.GetProperty("fatal_capture_status").GetString());
                Assert.Equal(0, seal.GetProperty("native_error_count").GetInt64());
            }
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination").GetProperty("data");
            Assert.Equal("journal_through_eof", termination.GetProperty("fatal_capture_status").GetString());
            Assert.Equal(0, termination.GetProperty("native_error_count").GetInt64());
            Assert.Equal(0, termination.GetProperty("native_fatal_count").GetInt64());
            Assert.Equal("unknown_due_to_intended_kill", termination.GetProperty("native_tail").GetString());
            B10AbruptPublisher.CheckDescriptor(termination.GetProperty("manifest"), "abrupt-manifest.json", files);
        }
        finally { Delete(root); }
    }

    // T2: a cohort file that changes between its seal and publication stops publication before any member beyond the registration is written.
    [Theory]
    [InlineData("ledger_byte")]
    [InlineData("report_byte")]
    [InlineData("manifest_deleted")]
    public async Task Given_CohortFileChangedAfterSeal_When_Publishing_Then_NothingBeyondTheRegistrationIsPublished(string change)
    {
        string root = NewRoot();
        try
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken,
                atKill: work =>
                {
                    // The kill request comes after both seals were loaded and the kill checkpoint was written, and before publication.
                    string ledger = Path.Combine(work, "timeout", "ledger.jsonl");
                    string report = Path.Combine(work, "abrupt-child", "cross_restart-pre-acceptance-report.json");
                    if (change == "ledger_byte") Flip(ledger);
                    else if (change == "report_byte") Flip(report);
                    else File.Delete(Path.Combine(work, "control", "manifest.json"));
                }));
            Assert.Equal("b10_abrupt_publication_failed", failure.Message);
            Assert.Equal(["registration.json"], Directory.GetFiles(Path.Combine(root, "first")).Select(Path.GetFileName));
        }
        finally { Delete(root); }
    }

    // T3, a regression guard: the successor's mounted binding keeps its six names, and a cohort file in it is a changed receipt.
    [Fact]
    public async Task Given_PredecessorBinding_When_CarryingACohortFile_Then_SuccessorRejectsReceiptChanged()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            Assert.Equal(["abrupt-manifest.json", "control-seal.json", "kill-selection.json", "registration.json", "termination.json", "timeout-seal.json"],
                B10AbruptPublisher.BindingMembers.Order(StringComparer.Ordinal));
            Dictionary<string, byte[]> files = FirstNames.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(root, "first", name)), StringComparer.Ordinal);
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
            var first = termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
            JsonElement selection = B10AbruptWire.ReadEnvelope(files["kill-selection.json"], "selection");
            DateTimeOffset release = DateTimeOffset.ParseExact(selection.GetProperty("data").GetProperty("checkpoint").GetProperty("data").GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
            string path = Path.Combine(root, "binding.json");
            foreach (bool withCohortFile in new[] { false, true })
            {
                var members = files.Where(pair => B10AbruptPublisher.BindingMembers.Contains(pair.Key) || (withCohortFile && pair.Key == "timeout-ledger.jsonl"))
                    .ToDictionary(pair => pair.Key, pair => System.Text.Encoding.UTF8.GetString(pair.Value), StringComparer.Ordinal);
                await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(new
                {
                    files = members, first_pod = new { first.JobName, first.PodName, first.PodUid, first.ImageReference, first.SourceCommit, finished_utc = B10AbruptPublisher.Utc(release) },
                    release_utc = B10AbruptPublisher.Utc(release), runner_utc = B10AbruptPublisher.Utc(release), runner_monotonic_ticks = 1L,
                    cross_host_timing = "not_verified", start_line_binding = new { start_line = "{}", broker_inventory = "{}", completion = "{}" }
                }, B10AbruptWire.JsonOptions), TestContext.Current.CancellationToken);
                B10AbruptOptions options = B10AbruptWireProducerTests.TestOptions(Path.Combine(root, "work"), "successor", path);
                if (withCohortFile)
                {
                    var failure = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptPublisher.LoadSelectionAsync(options, TestContext.Current.CancellationToken));
                    Assert.Equal("b10_abrupt_successor_binding_invalid:receipt_changed", failure.Message);
                }
                else Assert.NotNull(await B10AbruptPublisher.LoadSelectionAsync(options, TestContext.Current.CancellationToken));
            }
        }
        finally { Delete(root); }
    }

    // T10: native errors journaled before the kill request, in the kill arm's stop drain and after the kill request are all counted by the termination receipt.
    [Fact]
    public async Task Given_NativeErrorsBeforeAndAfterTheKill_When_EventStreamIsDrained_Then_TerminationCountsThemFromTheJournal()
    {
        string root = NewRoot();
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken, nativeErrors: true);
            JsonElement termination = B10AbruptWire.ReadEnvelope(File.ReadAllBytes(Path.Combine(root, "first", "termination.json")), "termination").GetProperty("data");
            Assert.Equal(3, termination.GetProperty("native_error_count").GetInt64());
            Assert.Equal(1, termination.GetProperty("native_fatal_count").GetInt64());
            Assert.Equal("journal_through_eof", termination.GetProperty("fatal_capture_status").GetString());
            Assert.Equal("unknown_due_to_intended_kill", termination.GetProperty("native_tail").GetString());
            JsonElement[] frames = ReadJournal(File.ReadAllBytes(Path.Combine(root, "first", "journal.jsonl"))).ToArray();
            JsonElement[] native = frames.Where(frame => frame.GetProperty("kind").GetString() == "native_error").ToArray();
            Assert.Equal([1L, 2L, 3L], native.Select(frame => frame.GetProperty("data").GetProperty("capture_sequence").GetInt64()));
            Assert.Equal([false, true, false], native.Select(frame => frame.GetProperty("data").GetProperty("is_fatal").GetBoolean()));
            long killSequence = frames.Single(frame => frame.GetProperty("kind").GetString() == "signal" && frame.GetProperty("data").GetProperty("requested_signal").GetInt32() == 9
                && frame.GetProperty("data").GetProperty("return_value").ValueKind == JsonValueKind.Number).GetProperty("sequence").GetInt64();
            Assert.True(native[1].GetProperty("sequence").GetInt64() < killSequence);
            Assert.True(native[2].GetProperty("sequence").GetInt64() > killSequence);
            // The seals were built before the second and third callbacks, so each records the one callback that preceded it.
            foreach (string name in new[] { "timeout-seal.json", "control-seal.json" })
                Assert.Equal(1, B10AbruptWire.ReadEnvelope(File.ReadAllBytes(Path.Combine(root, "first", name)), "cohort_seal").GetProperty("data").GetProperty("native_error_count").GetInt64());
        }
        finally { Delete(root); }
    }

    // T14 (R12): one line per cohort-file upload attempt is on stderr before the next upload starts and before a failure is reported.
    [Fact]
    public async Task Given_CohortFileUploads_When_Publishing_Then_EachAttemptIsOnStderrBeforeTheNextUpload()
    {
        string root = NewRoot();
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            var gate = new object();
            string[] seen = new string[8];
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken, supervisorErrors: stderr,
                publicationStorage: context => new CohortUploadStorage(context, 250, failAtIndex: null, seen, gate));
            JsonElement[] lines = CohortLines(stderr.ToString());
            Assert.Equal(8, lines.Length);
            for (int index = 0; index < 8; index++)
            {
                JsonElement facts = lines[index].GetProperty("facts");
                Assert.Equal(["name_index", "file_length", "elapsed_milliseconds", "budget_milliseconds", "upload_succeeded"],
                    facts.EnumerateObject().Select(property => property.Name));
                Assert.Equal(index, facts.GetProperty("name_index").GetInt64());
                Assert.Equal(new FileInfo(Path.Combine(root, "first", B10AbruptPublisher.CohortMembers[index])).Length, facts.GetProperty("file_length").GetInt64());
                Assert.Equal(250, facts.GetProperty("elapsed_milliseconds").GetInt64());
                Assert.Equal(30000, facts.GetProperty("budget_milliseconds").GetInt64());
                Assert.Equal(1, facts.GetProperty("upload_succeeded").GetInt64());
            }
            // Inside the create call for the file at index i + 1 the line for index i is already written; a held line would not be.
            for (int index = 1; index < 8; index++) Assert.Contains($"\"name_index\":{index - 1},", seen[index]);
            string text = stderr.ToString();
            Assert.True(text.IndexOf("\"stage\":\"selection_finalized\"", StringComparison.Ordinal) < text.IndexOf("\"stage\":\"cohort_file_uploaded\"", StringComparison.Ordinal));
            Assert.True(text.LastIndexOf("\"stage\":\"cohort_file_uploaded\"", StringComparison.Ordinal) < text.IndexOf("\"stage\":\"publication_complete\"", StringComparison.Ordinal));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task Given_SecondCohortUploadFails_When_Publishing_Then_ItsLineIsOnStderrBeforeTheAbortPathTouchesTheChild()
    {
        string root = NewRoot();
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            string[] seen = new string[8];
            var failure = await Assert.ThrowsAsync<IOException>(() => B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken, supervisorErrors: stderr,
                publicationStorage: context => new CohortUploadStorage(context, 250, failAtIndex: 1, seen, new object())));
            Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
            JsonElement[] lines = CohortLines(stderr.ToString());
            Assert.Equal(2, lines.Length);
            Assert.Equal(1, lines[0].GetProperty("facts").GetProperty("upload_succeeded").GetInt64());
            JsonElement failed = lines[1].GetProperty("facts");
            Assert.Equal(1, failed.GetProperty("name_index").GetInt64());
            Assert.Equal(0, failed.GetProperty("upload_succeeded").GetInt64());
            Assert.Equal(30000, failed.GetProperty("elapsed_milliseconds").GetInt64());
            // The failure line was already written when the abort path first read the child's exited state, which precedes any cleanup call.
            Assert.NotNull(seen[7]);
            Assert.Contains("\"name_index\":1,", seen[7]);
            Assert.Contains("\"upload_succeeded\":0", seen[7]);
        }
        finally { Delete(root); }
    }

    private static JsonElement[] CohortLines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.StartsWith("b10_abrupt_stage ", StringComparison.Ordinal))
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line["b10_abrupt_stage ".Length..]))
        .Where(entry => entry.GetProperty("stage").GetString() == "cohort_file_uploaded").ToArray();

    private static IEnumerable<JsonElement> ReadJournal(byte[] journal) => System.Text.Encoding.UTF8.GetString(journal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonSerializer.Deserialize<JsonElement>(line));

    private static (long, string) Describe(byte[] bytes) => (bytes.LongLength, B10AbruptPublisher.Hash(bytes));

    private static (long, string?) Bound(JsonElement seal, string member) =>
        (seal.GetProperty(member).GetProperty("length").GetInt64(), seal.GetProperty(member).GetProperty("sha256").GetString());

    private static void Flip(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[0] ^= 1;
        File.WriteAllBytes(path, bytes);
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "b10-publication-" + Guid.NewGuid().ToString("N"));
    private static void Delete(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }

    /// <summary>
    /// Wraps the real file storage. Each cohort-file create advances the harness clock by a fixed amount, snapshots stderr as the
    /// file starts, and can fail one upload after advancing the clock to the budget.
    /// </summary>
    private sealed class CohortUploadStorage : IB10AbruptStorage
    {
        private readonly B10AbruptWireProducerTests.PublicationContext context;
        private readonly int milliseconds;
        private readonly int? failAtIndex;
        private readonly string[] seen;
        private readonly object gate;

        internal CohortUploadStorage(B10AbruptWireProducerTests.PublicationContext context, int milliseconds, int? failAtIndex, string[] seen, object gate)
        {
            this.context = context; this.milliseconds = milliseconds; this.failAtIndex = failAtIndex; this.seen = seen; this.gate = gate;
        }

        public async Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
        {
            string member = Path.GetFileName(name);
            // The clock is manual only while the cohort files are published: the supervisor's pump and the successor's chain wait on real timers.
            if (member == "control-seal.json") context.Clock.EnterManual();
            int index = Array.IndexOf(B10AbruptPublisher.CohortMembers, member);
            if (index < 0)
            {
                await context.Inner.CreateAsync(name, bytes, cancellationToken);
                if (member == "termination.json") context.Clock.ExitManual();
                return;
            }
            lock (gate) { if (index > 0) seen[index] = context.Stderr.ToString()!; }
            if (failAtIndex == index)
            {
                context.Clock.Advance(TimeSpan.FromMilliseconds(30000));
                context.Process.ExitedRead = () =>
                {
                    // The first read of the child's exited state after the throw: no cleanup call has happened yet.
                    context.Process.ExitedRead = null;
                    lock (gate) seen[7] = context.Stderr.ToString()!;
                };
                throw new IOException("synthetic_upload_failure");
            }
            context.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            await context.Inner.CreateAsync(name, bytes, cancellationToken);
        }

        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) => context.Inner.ReadAsync(name, maximumBytes, cancellationToken);
    }
}
