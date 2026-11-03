using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
// The independent file guard must reject before forwarding, even when its inner stream partially fails.
public sealed class PacketSummaryFileBoundaryTests
{
    private static readonly byte[] NineBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_EightByteLimit_When_WritingSevenThenEightThenNine_Then_RejectsNinthBeforeForwardingAndStaysFaulted(bool span)
    {
        using var inner = new ObservedStream();
        using var stream = new BoundedPacketSummaryStream(inner, 8);
        Write(stream, 7, span);
        Assert.Equal(7, stream.Length);
        Assert.Equal(7, inner.Length);
        Write(stream, 1, span);
        Assert.Equal(8, stream.Length);
        Assert.Equal(8, stream.Position);
        Assert.Equal(8, inner.Length);
        Assert.Equal(2, inner.WriteCalls);
        Assert.Throws<IOException>(() => Write(stream, 1, span));
        Assert.Throws<IOException>(() => Write(stream, 0, !span));
        Assert.Throws<IOException>(() => Write(stream, 1, !span));
        Assert.Equal(8, stream.Length);
        Assert.Equal(8, inner.Length);
        Assert.Equal(2, inner.WriteCalls);
        Assert.False(stream.CanWrite);
    }

    [Theory]
    [InlineData(7, false)] [InlineData(8, false)] [InlineData(9, false)]
    [InlineData(7, true)] [InlineData(8, true)] [InlineData(9, true)]
    public void Given_EightByteLimit_When_WritingSingleBoundaryBuffer_Then_AcceptsOnlyWithinLimit(int count, bool span)
    {
        using var inner = new ObservedStream();
        using var stream = new BoundedPacketSummaryStream(inner, 8);
        if (count <= 8)
        {
            Write(stream, count, span);
            Assert.Equal(count, inner.Length);
            Assert.Equal(count, stream.Length);
            Assert.Equal(1, inner.WriteCalls);
            Assert.True(stream.CanWrite);
        }
        else
        {
            Assert.Throws<IOException>(() => Write(stream, count, span));
            Assert.Throws<IOException>(() => Write(stream, 1, !span));
            Assert.Equal(0, inner.Length);
            Assert.Equal(0, stream.Length);
            Assert.Equal(0, inner.WriteCalls);
            Assert.False(stream.CanWrite);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_InnerPartialWriteFailure_When_Writing_Then_CountsOnlySuccessfulCallsAndNeverForwardsAgain(bool span)
    {
        using var inner = new ObservedStream { FailCall = 2 };
        using var stream = new BoundedPacketSummaryStream(inner, 8);
        Write(stream, 2, span);
        Assert.Throws<IOException>(() => Write(stream, 3, span));
        Assert.Equal(3, inner.Length);
        Assert.Equal(2, stream.Length);
        Assert.Equal(2, stream.Position);
        Assert.Throws<IOException>(() => Write(stream, 1, !span));
        Assert.Throws<IOException>(() => Write(stream, 0, span));
        Assert.Equal(2, inner.WriteCalls);
        Assert.Equal(3, inner.Length);
        Assert.Equal(2, stream.Length);
        Assert.False(stream.CanWrite);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(20971520, true)] [InlineData(20971521, false)]
    public void Given_TestCapBoundary_When_Constructing_Then_RequiresPositiveCapNoGreaterThanProduction(long maximumBytes, bool valid)
    {
        using var inner = new MemoryStream();
        if (valid)
        {
            using var stream = new BoundedPacketSummaryStream(inner, maximumBytes);
            Write(stream, 1, true);
            Assert.Equal(1, stream.Length);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedPacketSummaryStream(inner, maximumBytes));
            Assert.Equal(0, inner.Length);
        }
    }

    [Fact]
    public void Given_WriteOnlyGuard_When_ReadingSeekingOrChangingLength_Then_RejectsWithoutChangingInnerStream()
    {
        using var inner = new MemoryStream();
        using var stream = new BoundedPacketSummaryStream(inner, 8);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Read(NineBytes, 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
        Assert.Throws<NotSupportedException>(() => stream.Position = 1);
        Assert.Equal(0, inner.Length);
    }

    [Fact]
    public void Given_FileGuardSmallerThanPrefix_When_WrapperStarts_Then_CoreWriteFailureCleansOwnedStaging()
    {
        using var factory = new GuardedFactory();
        using var writer = new PacketSummaryFileWriter(PacketSummaryFileTestData.Directory, PacketSummaryFileTestData.Identity(), factory);
        Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
        Assert.Equal("packet_summary_write_failed", writer.FailureCode);
        Assert.Equal(0, writer.BytesWritten);
        Assert.Equal(0, factory.Inner.Length);
        Assert.Equal("create,close,delete", factory.Trace);
        Assert.False(writer.TryAppend(PacketSummaryFileTestData.FirstEvent));
        Assert.False(writer.TryComplete(PacketSummaryFileTestData.EndedUtc));
        writer.Dispose();
        Assert.Equal("create,close,delete", factory.Trace);
    }

    private static void Write(BoundedPacketSummaryStream stream, int count, bool span)
    {
        if (span) stream.Write(NineBytes.AsSpan(0, count));
        else stream.Write(NineBytes, 0, count);
    }

    private sealed class ObservedStream : MemoryStream
    {
        public int WriteCalls { get; private set; }
        public int FailCall { get; init; }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteCalls++;
            if (WriteCalls == FailCall)
            {
                base.Write(buffer[..1]);
                throw new IOException("synthetic partial write");
            }
            base.Write(buffer);
        }
    }

    private sealed class GuardedFactory : IPacketSummaryFileFactory, IPacketSummaryStagingFile, IDisposable
    {
        private readonly List<string> _operations = [];
        public GuardedFactory() => Content = new BoundedPacketSummaryStream(Inner, 8);
        public MemoryStream Inner { get; } = new();
        public Stream Content { get; }
        public bool IsSupported => true;
        public string Trace => string.Join(',', _operations);
        public IPacketSummaryStagingFile Create(string directory)
        {
            Assert.Equal(PacketSummaryFileTestData.Directory, directory);
            _operations.Add("create");
            return this;
        }
        public void FlushToDisk() => _operations.Add("flush");
        public void Close() => _operations.Add("close");
        public void FinalizeFile() => _operations.Add("finalize");
        public void DeletePartial() => _operations.Add("delete");
        public void Dispose()
        {
            Content.Dispose();
            Inner.Dispose();
        }
    }
}
#pragma warning restore CA1707
