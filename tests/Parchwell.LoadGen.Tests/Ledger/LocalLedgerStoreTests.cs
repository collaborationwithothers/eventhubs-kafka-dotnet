using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Ledger;
using Xunit;
#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

namespace Parchwell.LoadGen.Tests.Ledger;

public sealed class LocalLedgerStoreTests
{
    [Fact]
    public void Given_UnrepresentableDrainTimer_When_ValidatingOptions_Then_RejectsBeforeFileCreation()
    {
        using var directory = new TemporaryDirectory();
        var options = Paths(directory.Path).Options with { DrainTimeout = TimeSpan.MaxValue };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.False(Directory.Exists(directory.Path));
    }

    [Fact]
    public async Task Given_rows_and_manifest_When_drained_and_read_back_Then_exact_inventory_is_validated()
    {
        using var directory = new TemporaryDirectory();
        var paths = Paths(directory.Path);
        var row = LedgerValidatorTests.CallbackRow();
        await using (var store = new LocalLedgerStore(paths.Options))
        {
            Assert.True(store.TryAppend(row));
            await store.DrainAsync(TestContext.Current.CancellationToken);
            await store.SaveManifestAsync(LedgerValidatorTests.Manifest([row.Attempt]), TestContext.Current.CancellationToken);
        }

        var read = await LedgerValidator.ReadValidatedAsync(paths.Ledger, paths.Manifest, row.Identity, 2, 4096, 8192, 4096, TestContext.Current.CancellationToken);
        Assert.Equal([row], read);
    }

    [Fact]
    public async Task Given_missing_manifest_When_read_back_Then_run_is_rejected()
    {
        using var directory = new TemporaryDirectory();
        var paths = Paths(directory.Path);
        await using (var store = new LocalLedgerStore(paths.Options))
        {
            Assert.True(store.TryAppend(LedgerValidatorTests.CallbackRow()));
            await store.DrainAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<FileNotFoundException>(() => LedgerValidator.ReadValidatedAsync(paths.Ledger, paths.Manifest,
            LedgerValidatorTests.CallbackRow().Identity, 2, 4096, 8192, 4096, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_duplicate_persisted_row_When_read_back_Then_duplicate_is_rejected()
    {
        using var directory = new TemporaryDirectory();
        var paths = Paths(directory.Path);
        var row = LedgerValidatorTests.CallbackRow();
        await using (var store = new LocalLedgerStore(paths.Options))
        {
            Assert.True(store.TryAppend(row));
            await store.DrainAsync(TestContext.Current.CancellationToken);
            await store.SaveManifestAsync(LedgerValidatorTests.Manifest([row.Attempt]), TestContext.Current.CancellationToken);
        }
        var line = await File.ReadAllTextAsync(paths.Ledger, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(paths.Ledger, line, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => LedgerValidator.ReadValidatedAsync(paths.Ledger, paths.Manifest,
            row.Identity, 3, 4096, 8192, 4096, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_truncated_jsonl_When_read_back_Then_partial_last_row_is_rejected()
    {
        using var directory = new TemporaryDirectory();
        var paths = Paths(directory.Path);
        var row = LedgerValidatorTests.CallbackRow();
        await using (var store = new LocalLedgerStore(paths.Options))
        {
            Assert.True(store.TryAppend(row));
            await store.DrainAsync(TestContext.Current.CancellationToken);
            await store.SaveManifestAsync(LedgerValidatorTests.Manifest([row.Attempt]), TestContext.Current.CancellationToken);
        }
        var bytes = await File.ReadAllBytesAsync(paths.Ledger, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(paths.Ledger, bytes[..^1], TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => LedgerValidator.ReadValidatedAsync(paths.Ledger, paths.Manifest,
            row.Identity, 2, 4096, 8192, 4096, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_invalid_append_When_called_on_callback_path_Then_it_returns_false_and_fault_sticks()
    {
        using var directory = new TemporaryDirectory();
        await using var store = new LocalLedgerStore(Paths(directory.Path).Options);

        Assert.False(store.TryAppend(null!));
        Assert.True(store.IsFaulted);
        Assert.False(store.TryAppend(LedgerValidatorTests.CallbackRow()));
        await Assert.ThrowsAsync<IOException>(() => store.DrainAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_file_write_exceeds_budget_When_drained_Then_manifest_can_still_be_saved_independently()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        var ledger = System.IO.Path.Combine(directory.Path, "ledger.jsonl");
        var manifest = System.IO.Path.Combine(directory.Path, "manifest.json");
        var paths = (Ledger: ledger, Manifest: manifest,
            Options: new LedgerStoreOptions(ledger, manifest, 4, 4096, 8, 4096, TimeSpan.FromSeconds(3)));
        var row = LedgerValidatorTests.CallbackRow();
        await using var store = new LocalLedgerStore(paths.Options);
        Assert.True(store.TryAppend(row));
        var failure = await Assert.ThrowsAsync<IOException>(() => store.DrainAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.True(store.IsFaulted);

        await store.SaveManifestAsync(LedgerValidatorTests.Manifest([row.Attempt]) with
        {
            BookkeepingComplete = false,
            WorkerStatus = "failed",
            Diagnostics = ["synthetic_writer_byte_limit"]
        }, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(paths.Manifest));
    }

    [Fact]
    public async Task Given_row_exceeds_serialization_budget_When_written_Then_failure_is_latched_before_file_append()
    {
        using var directory = new TemporaryDirectory();
        var ledger = System.IO.Path.Combine(directory.Path, "ledger.jsonl");
        var manifest = System.IO.Path.Combine(directory.Path, "manifest.json");
        var options = new LedgerStoreOptions(ledger, manifest, 2, 64, 8192, 4096, TimeSpan.FromSeconds(3));
        await using var store = new LocalLedgerStore(options);
        Assert.True(store.TryAppend(LedgerValidatorTests.CallbackRow()));

        var error = await Assert.ThrowsAsync<IOException>(() => store.DrainAsync(TestContext.Current.CancellationToken));

        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.True(store.IsFaulted);
        Assert.Equal(0, new FileInfo(ledger).Length);
    }

    [Fact]
    public async Task Given_blocked_writer_and_full_queue_When_append_is_attempted_Then_capture_failure_sticks_and_missing_rows_are_detected()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        var ledger = System.IO.Path.Combine(directory.Path, "ledger.jsonl");
        var manifestPath = System.IO.Path.Combine(directory.Path, "manifest.json");
        var options = new LedgerStoreOptions(ledger, manifestPath, 1, 4096, 8192, 4096, TimeSpan.FromSeconds(3));
        var gate = new BlockingWriteStream(ledger);
        await using var store = new LocalLedgerStore(options, () => gate);
        var first = LedgerValidatorTests.CallbackRow();
        var second = first with { Attempt = first.Attempt with { AttemptId = "attempt-2", Ordinal = 1 } };
        var third = first with { Attempt = first.Attempt with { AttemptId = "attempt-3", Ordinal = 2 } };

        Assert.True(store.TryAppend(first));
        await gate.WriteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(store.TryAppend(second));
        Assert.False(store.TryAppend(third));
        Assert.True(store.IsFaulted);
        gate.Release();
        await Assert.ThrowsAsync<IOException>(() => store.DrainAsync(TestContext.Current.CancellationToken));
        Assert.False(store.TryAppend(first));
        await store.SaveManifestAsync(LedgerValidatorTests.Manifest([first.Attempt, second.Attempt, third.Attempt]) with
        {
            BookkeepingComplete = false,
            WorkerStatus = "failed",
            Diagnostics = ["synthetic_queue_full"]
        }, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => LedgerValidator.ReadValidatedAsync(ledger, manifestPath,
            first.Identity, 4, 4096, 8192, 4096, TestContext.Current.CancellationToken));
    }

    private static (string Ledger, string Manifest, LedgerStoreOptions Options) Paths(string directory)
    {
        var ledger = System.IO.Path.Combine(directory, "ledger.jsonl");
        var manifest = System.IO.Path.Combine(directory, "manifest.json");
        return (ledger, manifest, new LedgerStoreOptions(ledger, manifest, 8, 4096, 8192, 4096, TimeSpan.FromSeconds(3)));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"loadgen-ledger-{Guid.NewGuid():N}");
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }

    private sealed class BlockingWriteStream : Stream
    {
        private readonly FileStream inner;
        private readonly TaskCompletionSource writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BlockingWriteStream(string path) => inner = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        public TaskCompletionSource WriteStarted => writeStarted;
        public void Release() => release.TrySetResult();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            await inner.WriteAsync(buffer, cancellationToken);
        }
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
