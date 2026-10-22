using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Rehearsal;
using Xunit;

namespace Parchwell.Runner.Tests;

#pragma warning disable CA1707 // Repository test naming convention.

// Synthetic zero-filled files exercise transfer budgets, without representing worker evidence.
public sealed class WireFixtureBoundsTests
{
    [Theory]
    [InlineData(1048575, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public async Task Given_SyntheticManifestByteBoundary_When_Loading_Then_OnlyBoundedManifestIsAccepted(int manifestBytes, bool accepted)
    {
        using var fixture = new SyntheticWireDirectory();
        WireFile file = await fixture.AddZeroFileAsync("synthetic.bin", 1);
        await fixture.WriteManifestAsync([file], manifestBytes);

        if (accepted)
        {
            WireFixtures loaded = await WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken);
            Assert.Equal(WireFixtures.BuiltSourceCommit(), loaded.SourceCommit);
            Assert.Equal(new byte[] { 0 }, loaded.Get(file.Path));
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken));
            Assert.Equal("wire_manifest_limit", error.Message);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task Given_SyntheticManifestCountBoundary_When_Loading_Then_OnlyCountsWithinBothBoundsAreAccepted(int fileCount, bool accepted)
    {
        using var fixture = new SyntheticWireDirectory();
        var files = new List<WireFile>();
        for (int index = 0; index < fileCount; index++)
            files.Add(await fixture.AddZeroFileAsync("synthetic-" + index.ToString(CultureInfo.InvariantCulture) + ".bin", 1));
        await fixture.WriteManifestAsync([.. files]);

        if (accepted)
        {
            WireFixtures loaded = await WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken);
            foreach (WireFile file in files)
                Assert.Equal(new byte[] { 0 }, loaded.Get(file.Path));
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken));
            Assert.Equal("wire_manifest_source_or_count", error.Message);
        }
    }

    [Theory]
    [InlineData(16777215, true)]
    [InlineData(16777216, true)]
    [InlineData(16777217, false)]
    public async Task Given_SyntheticFileByteBoundary_When_Loading_Then_OnlyBoundedFileIsAccepted(int fileBytes, bool accepted)
    {
        using var fixture = new SyntheticWireDirectory();
        WireFile file = await fixture.AddZeroFileAsync("synthetic.bin", fileBytes);
        await fixture.WriteManifestAsync([file]);

        if (accepted)
        {
            WireFixtures loaded = await WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken);
            Assert.Equal(fileBytes, loaded.Get(file.Path).Length);
            Assert.Equal(file.Sha256, Convert.ToHexStringLower(SHA256.HashData(loaded.Get(file.Path))));
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken));
            Assert.Equal("wire_fixture_size", error.Message);
        }
    }

    [Theory]
    [InlineData(268435455, true)]
    [InlineData(268435456, true)]
    [InlineData(268435457, false)]
    public async Task Given_SyntheticAggregateByteBoundary_When_Loading_Then_OnlyBoundedTotalIsAccepted(int totalBytes, bool accepted)
    {
        using var fixture = new SyntheticWireDirectory();
        var files = new List<WireFile>();
        int remaining = totalBytes;
        while (remaining > 0)
        {
            int length = Math.Min(16777216, remaining);
            files.Add(await fixture.AddZeroFileAsync("synthetic-" + files.Count.ToString(CultureInfo.InvariantCulture) + ".bin", length));
            remaining -= length;
        }
        Assert.Equal(totalBytes, files.Sum(file => file.SizeBytes));
        Assert.All(files, file => Assert.InRange(file.SizeBytes, 1, 16777216));
        await fixture.WriteManifestAsync([.. files]);

        if (accepted)
        {
            WireFixtures loaded = await WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken);
            Assert.Equal(totalBytes, files.Sum(file => loaded.Get(file.Path).LongLength));
            foreach (WireFile file in files)
                Assert.Equal(file.SizeBytes, loaded.Get(file.Path).LongLength);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => WireFixtures.LoadAsync(fixture.Root, TestContext.Current.CancellationToken));
            Assert.Equal("wire_fixture_total_limit", error.Message);
        }
    }

    private sealed class SyntheticWireDirectory : IDisposable
    {
        private static readonly JsonSerializerOptions ManifestOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        private readonly Dictionary<int, string> zeroHashes = [];
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "synthetic-wire-bounds-" + Guid.NewGuid().ToString("N"));

        public SyntheticWireDirectory() => Directory.CreateDirectory(Root);

        public async Task<WireFile> AddZeroFileAsync(string relativePath, int length)
        {
            string path = Path.Combine(Root, relativePath);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.SetLength(length);
            if (!zeroHashes.TryGetValue(length, out string? hash))
            {
                await using var input = File.OpenRead(path);
                hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, TestContext.Current.CancellationToken));
                zeroHashes.Add(length, hash);
            }
            return new(relativePath, length, hash);
        }

        public async Task WriteManifestAsync(WireFile[] files, int? exactBytes = null)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new WireManifest(1, WireFixtures.BuiltSourceCommit(), files),
                ManifestOptions);
            byte[] bytes = json;
            if (exactBytes is int length)
            {
                Assert.True(json.Length <= length);
                bytes = new byte[length];
                Array.Fill(bytes, (byte)' ');
                json.CopyTo(bytes, 0);
            }
            string path = Path.Combine(Root, "wire-manifest.json");
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            Assert.Equal(exactBytes ?? json.Length, new FileInfo(path).Length);
        }

        public void Dispose() => Directory.Delete(Root, true);
    }
}
