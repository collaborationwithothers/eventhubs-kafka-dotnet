using System.Text;
using Parchwell.LoadGen.IdleConnectionCapture;
using Parchwell.LoadGen.Tests.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.Integration;

#pragma warning disable CA1707
// These isolated filesystem cases establish local Linux no-replace behavior, not live capture completeness.
public sealed class PacketSummaryFileIntegrationTests
{
    private static readonly byte[] Sentinel = Encoding.UTF8.GetBytes("DO_NOT_REPLACE_334");
    private static readonly byte[] EightBytes = [1, 2, 3, 4, 5, 6, 7, 8];

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_EmptyOrTwoEvents_When_RealWriterCompletes_Then_AtomicallyPublishesExactJsonAndDisposePreservesBytes(bool populated)
    {
        InDirectory(directory =>
        {
            if (AssertUnsupportedPlatform(directory)) return;
            string partial = PartialPath(directory);
            string final = FinalPath(directory);
            using var writer = new PacketSummaryFileWriter(directory, PacketSummaryFileTestData.Identity());
            Assert.Equal(PacketSummaryWriterState.Active, writer.State);
            Assert.True(File.Exists(partial));
            Assert.False(File.Exists(final));
            if (populated)
            {
                Assert.True(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
                Assert.True(writer.TryAppend(PacketSummaryFileTestData.SecondEvent));
            }
            Assert.True(File.Exists(partial));
            Assert.False(File.Exists(final));
            Assert.True(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
            Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
            Assert.Null(writer.FailureCode);
            Assert.False(writer.CleanupFailed);
            Assert.False(File.Exists(partial));
            Assert.True(File.Exists(final));
            byte[] bytes = File.ReadAllBytes(final);
            PacketSummaryFileTestData.AssertJson(bytes, populated);
            Assert.Equal(bytes.LongLength, writer.BytesWritten);
            Assert.Equal(populated ? 2 : 0, writer.EventCount);
            writer.Dispose();
            writer.Dispose();
            Assert.Equal(bytes, File.ReadAllBytes(final));
            Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
            Assert.Null(writer.FailureCode);
        });
    }

    [Theory]
    [InlineData("file")] [InlineData("directory")] [InlineData("symlink")]
    public void Given_PreexistingPartialEntry_When_Constructing_Then_OpenFailurePreservesEntryAndSentinel(string kind)
    {
        InDirectory(directory =>
        {
            if (AssertUnsupportedPlatform(directory)) return;
            string partial = PartialPath(directory);
            string sentinel = Path.Combine(directory, "sentinel.bin");
            File.WriteAllBytes(sentinel, Sentinel);
            switch (kind)
            {
                case "file": File.WriteAllBytes(partial, Sentinel); break;
                case "directory": Directory.CreateDirectory(partial); break;
                default: File.CreateSymbolicLink(partial, sentinel); break;
            }
            using var writer = new PacketSummaryFileWriter(directory, PacketSummaryFileTestData.Identity());
            Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
            Assert.Equal("packet_summary_open_failed", writer.FailureCode);
            Assert.Equal(0, writer.BytesWritten);
            Assert.Equal(0, writer.EventCount);
            Assert.False(writer.CleanupFailed);
            Assert.False(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
            Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
            writer.Dispose();
            writer.Dispose();
            Assert.False(File.Exists(FinalPath(directory)));
            Assert.Equal(Sentinel, File.ReadAllBytes(sentinel));
            if (kind == "directory") Assert.True(Directory.Exists(partial));
            else Assert.Equal(Sentinel, File.ReadAllBytes(partial));
            if (kind == "symlink") Assert.Equal(sentinel, new FileInfo(partial).LinkTarget);
        });
    }

    [Theory]
    [InlineData("file", false)] [InlineData("file", true)]
    [InlineData("directory", false)] [InlineData("dangling-symlink", false)]
    public void Given_OccupiedFinalName_When_Completing_Then_NativeNoReplacePreservesDestinationAndDeletesOwnedPartial(string kind, bool createAfterConstruction)
    {
        InDirectory(directory =>
        {
            if (AssertUnsupportedPlatform(directory)) return;
            string final = FinalPath(directory);
            string missingTarget = Path.Combine(directory, "missing-target.bin");
            if (!createAfterConstruction) CreateDestination(final, missingTarget, kind);
            using var writer = new PacketSummaryFileWriter(directory, PacketSummaryFileTestData.Identity());
            Assert.Equal(PacketSummaryWriterState.Active, writer.State);
            Assert.True(File.Exists(PartialPath(directory)));
            if (createAfterConstruction) CreateDestination(final, missingTarget, kind);
            Assert.True(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
            Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
            Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
            Assert.Equal("packet_summary_finalize_failed", writer.FailureCode);
            Assert.False(writer.CleanupFailed);
            Assert.False(File.Exists(PartialPath(directory)));
            Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
            Assert.False(writer.TryAppend(PacketSummaryFileTestData.SecondEvent));
            writer.Dispose();
            writer.Dispose();
            if (kind == "file") Assert.Equal(Sentinel, File.ReadAllBytes(final));
            else if (kind == "directory") Assert.True(Directory.Exists(final));
            else
            {
                Assert.Equal(missingTarget, new FileInfo(final).LinkTarget);
                Assert.False(File.Exists(missingTarget));
                Assert.False(Directory.Exists(missingTarget));
            }
            Assert.False(File.Exists(PartialPath(directory)));
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_ActiveStagingFile_When_AbandonedOrAppendFails_Then_DeletesPartialAndNeverCreatesFinal(bool invalidAppend)
    {
        InDirectory(directory =>
        {
            if (AssertUnsupportedPlatform(directory)) return;
            using var writer = new PacketSummaryFileWriter(directory, PacketSummaryFileTestData.Identity());
            Assert.True(File.Exists(PartialPath(directory)));
            if (invalidAppend)
                Assert.False(writer.TryAppend(new PacketSummaryEvent(0, PacketDirection.Outbound, 0, TcpControlFlags.Syn)));
            else
                writer.Dispose();
            Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
            Assert.Equal(invalidAppend ? "packet_event_invalid" : "packet_summary_abandoned", writer.FailureCode);
            Assert.False(writer.CleanupFailed);
            Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
            writer.Dispose();
            writer.Dispose();
            Assert.False(File.Exists(PartialPath(directory)));
            Assert.False(File.Exists(FinalPath(directory)));
        });
    }

    [Fact]
    public void Given_RealFileAndEightByteGuard_When_NinthByteIsRejected_Then_FileNeverExceedsEightBytes()
    {
        InDirectory(directory =>
        {
            if (AssertUnsupportedPlatform(directory)) return;
            string path = Path.Combine(directory, "bounded.bin");
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var stream = new BoundedPacketSummaryStream(file, 8))
            {
                stream.Write(EightBytes.AsSpan(0, 7));
                Assert.Equal(7, file.Length);
                stream.Write(EightBytes, 7, 1);
                Assert.Equal(8, file.Length);
                Assert.Throws<IOException>(() => stream.Write(EightBytes.AsSpan(0, 1)));
                Assert.Throws<IOException>(() => stream.Write(EightBytes, 0, 1));
                file.Flush(flushToDisk: true);
                Assert.Equal(8, file.Length);
                Assert.Equal(8, stream.Length);
            }
            Assert.Equal(8, new FileInfo(path).Length);
            Assert.Equal(EightBytes, File.ReadAllBytes(path));
            File.Delete(path);
            Assert.False(File.Exists(path));
        });
    }

    private static bool AssertUnsupportedPlatform(string directory)
    {
        var factory = new PacketSummaryFileFactory();
        Assert.Equal(OperatingSystem.IsLinux(), factory.IsSupported);
        if (OperatingSystem.IsLinux()) return false;
        using var writer = new PacketSummaryFileWriter(directory, PacketSummaryFileTestData.Identity());
        Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
        Assert.Equal("packet_summary_platform_unsupported", writer.FailureCode);
        Assert.Equal(0, writer.BytesWritten);
        Assert.Equal(0, writer.EventCount);
        Assert.False(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
        Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        writer.Dispose();
        Assert.False(writer.CleanupFailed);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
        return true;
    }

    private static void CreateDestination(string final, string missingTarget, string kind)
    {
        switch (kind)
        {
            case "file": File.WriteAllBytes(final, Sentinel); break;
            case "directory": Directory.CreateDirectory(final); break;
            default: File.CreateSymbolicLink(final, missingTarget); break;
        }
    }

    private static string PartialPath(string directory) => Path.Combine(directory, "packet-summary.json.partial");
    private static string FinalPath(string directory) => Path.Combine(directory, "packet-summary.json");

    private static void InDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "packet-summary-334-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            // Only this case's fixed entries live here; there is no recovery scan or cleanup retry.
            Directory.Delete(directory, recursive: true);
        }
    }
}
#pragma warning restore CA1707
