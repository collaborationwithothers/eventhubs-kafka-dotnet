#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Globalization;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

// Synthetic sparse files exercise metadata guards without transferring phase-sized payloads.
public sealed class EvidenceExportBoundaryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public EvidenceExportBoundaryTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task Given_EntryCountBoundary_When_Exported_Then_AcceptsOnlyWithinLimit(int count, bool allowed)
    {
        var token = TestContext.Current.CancellationToken;
        string path = Path.Combine(directory, "synthetic.bin");
        byte[] original = [1, 2, 255];
        await File.WriteAllBytesAsync(path, original, token);
        EvidenceFile[] files = Enumerable.Range(0, count)
            .Select(index => new EvidenceFile("synthetic/" + index.ToString("D3", CultureInfo.InvariantCulture) + ".bin", path)).ToArray();
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        if (!allowed)
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("synthetic-env", files, token));
            Assert.Equal("export_entry_limit", exception.Message);
            Assert.Empty(store.Writes);
            return;
        }
        var receipt = await exporter.ExportAsync("synthetic-env", files, token);
        Assert.True(receipt.Complete);
        Assert.Equal(count, receipt.VerifiedFileCount);
        Assert.Equal((long)count * original.Length, receipt.VerifiedBytes);
        Assert.Equal(count + 1, store.Writes.Count);
        Assert.Equal(receipt.ManifestPath, store.Writes[^1]);
        Assert.Equal(original, await exporter.ReadVerifiedFileAsync("synthetic-env", files[^1].RelativePath, receipt, token));
    }

    [Theory]
    [InlineData(268_435_455, true)]
    [InlineData(268_435_456, true)]
    [InlineData(268_435_457, false)]
    public async Task Given_FileByteBoundary_When_PreflightValidated_Then_OnlyAcceptedSizesReachTinyFirstWrite(long size, bool allowed)
    {
        string tiny = await TinyFileAsync();
        string sparse = SparseFile("synthetic-file.bin", size);
        await AssertPreflightAsync([new("a-synthetic-tiny.bin", tiny), new("z-synthetic-file.bin", sparse)], allowed);
    }

    [Theory]
    [InlineData(8_589_934_591, true)]
    [InlineData(8_589_934_592, true)]
    [InlineData(8_589_934_593, false)]
    public async Task Given_PhaseByteBoundary_When_PreflightValidated_Then_OnlyAcceptedTotalsReachTinyFirstWrite(long total, bool allowed)
    {
        string tiny = await TinyFileAsync();
        string full = SparseFile("synthetic-full.bin", EvidenceExporter.MaxFileBytes);
        const int fullEntries = 31;
        long remainder = total - 1 - (long)fullEntries * EvidenceExporter.MaxFileBytes;
        string tail = SparseFile("synthetic-tail.bin", remainder);
        EvidenceFile[] files = [new("a-synthetic-tiny.bin", tiny),
            .. Enumerable.Range(0, fullEntries).Select(index => new EvidenceFile("synthetic/" + index.ToString("D2", CultureInfo.InvariantCulture), full)),
            new("z-synthetic-tail.bin", tail)];
        Assert.Equal(total, files.Sum(file => new FileInfo(file.LocalPath).Length));
        await AssertPreflightAsync(files, allowed);
    }

    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public async Task Given_ActualCostByteBoundary_When_Uploaded_Then_AcceptsOnlyWithinLimit(int size, bool allowed)
    {
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        byte[] synthetic = new byte[size];
        synthetic[^1] = 255;
        var token = TestContext.Current.CancellationToken;
        if (!allowed)
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.UploadActualCostAsync("synthetic-env", synthetic, token));
            Assert.Equal("actual_cost_record_limit", exception.Message);
            Assert.Empty(store.Writes);
            return;
        }
        var receipt = await exporter.UploadActualCostAsync("synthetic-env", synthetic, token);
        Assert.Equal(size, receipt.SizeBytes);
        Assert.Equal(receipt.Path, Assert.Single(store.Writes));
        Assert.Equal(synthetic, store.Objects[("evidence", receipt.Path)].Bytes);
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public async Task Given_GateByteBoundary_When_UploadedAndRead_Then_AcceptsOnlyWithinLimit(int size, bool allowed)
    {
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        byte[] synthetic = new byte[size];
        synthetic[^1] = 255;
        var token = TestContext.Current.CancellationToken;
        if (!allowed)
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.UploadGateAsync("synthetic-env", synthetic, token));
            Assert.Equal("gate_byte_limit", exception.Message);
            Assert.Empty(store.Writes);
            return;
        }
        var receipt = await exporter.UploadGateAsync("synthetic-env", synthetic, token);
        Assert.Equal(size, receipt.SizeBytes);
        Assert.Equal(receipt.Path, Assert.Single(store.Writes));
        Assert.Equal(synthetic, await exporter.ReadGateAsync("synthetic-env", receipt.Sha256, token));
    }

    public void Dispose() => Directory.Delete(directory, true);

    private async Task<string> TinyFileAsync()
    {
        string path = Path.Combine(directory, "synthetic-tiny.bin");
        await File.WriteAllBytesAsync(path, new byte[] { 42 }, TestContext.Current.CancellationToken);
        return path;
    }

    private string SparseFile(string name, long size)
    {
        string path = Path.Combine(directory, name);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        file.SetLength(size);
        return path;
    }

    private static async Task AssertPreflightAsync(EvidenceFile[] files, bool allowed)
    {
        var store = new StopAtFirstWriteStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        if (allowed)
        {
            // Reaching this deliberate transport stop proves metadata admission, not a full large transfer.
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExportAsync("synthetic-env", files, TestContext.Current.CancellationToken));
            Assert.Equal("synthetic_first_write_reached", exception.Message);
            Assert.Equal(1, store.WriteCount);
            Assert.Equal("A01/synthetic-env/a-synthetic-tiny.bin", store.FirstPath);
            Assert.Equal(new byte[] { 42 }, store.FirstBytes);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("synthetic-env", files, TestContext.Current.CancellationToken));
            Assert.Equal("export_byte_limit", exception.Message);
            Assert.Equal(0, store.WriteCount);
        }
    }

    private sealed class StopAtFirstWriteStore : IArtifactStore
    {
        public int WriteCount { get; private set; }
        public string? FirstPath { get; private set; }
        public byte[]? FirstBytes { get; private set; }

        public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synthetic_unexpected_read");

        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes,
            string? expectedETag, bool createOnly, CancellationToken cancellationToken)
        {
            WriteCount++;
            FirstPath = path;
            FirstBytes = bytes.ToArray();
            throw new InvalidOperationException("synthetic_first_write_reached");
        }
    }
}
