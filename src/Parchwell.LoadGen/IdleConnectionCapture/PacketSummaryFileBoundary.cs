namespace Parchwell.LoadGen.IdleConnectionCapture;

/// <summary>Creates the single owned staging file for a packet-summary writer.</summary>
internal interface IPacketSummaryFileFactory
{
    bool IsSupported { get; }
    IPacketSummaryStagingFile Create(string directory);
}

/// <summary>Separates file ownership and finalization from the non-owning JSON writer.</summary>
internal interface IPacketSummaryStagingFile
{
    Stream Content { get; }
    void FlushToDisk();
    void Close();
    void FinalizeFile();
    void DeletePartial();
}

internal sealed class PacketSummaryFileFactory : IPacketSummaryFileFactory
{
    public bool IsSupported => OperatingSystem.IsLinux();

    public IPacketSummaryStagingFile Create(string directory)
    {
        if (!IsSupported) throw new PlatformNotSupportedException();
        string partialPath = Path.Combine(directory, "packet-summary.json.partial");
        string finalPath = Path.Combine(directory, "packet-summary.json");
        var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.SequentialScan);
        try
        {
            return new PacketSummaryStagingFile(file, partialPath, finalPath);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

#pragma warning disable CA1001 // Ownership is released by the explicit Close operation in the slice's boundary.
    private sealed class PacketSummaryStagingFile(FileStream file, string partialPath, string finalPath)
        : IPacketSummaryStagingFile
    {
        public Stream Content { get; } = new BoundedPacketSummaryStream(file);
        public void FlushToDisk() => file.Flush(flushToDisk: true);
        public void Close() => file.Dispose();
        public void FinalizeFile() => LinuxPacketSummaryRename.MoveWithoutReplace(partialPath, finalPath);
        public void DeletePartial() => File.Delete(partialPath);
    }
#pragma warning restore CA1001
}

/// <summary>Checks the file-size ceiling before forwarding synchronous writes to a non-owned stream.</summary>
internal sealed class BoundedPacketSummaryStream : Stream
{
#pragma warning disable CA2213 // The staging-file boundary, not this non-owning guard, closes the inner stream.
    private readonly Stream _inner;
#pragma warning restore CA2213
    private readonly long _maximumBytes;
    private long _written;
    private bool _faulted;

    internal BoundedPacketSummaryStream(Stream inner, long maximumBytes = PacketSummaryBudget.MaximumSummaryBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, PacketSummaryBudget.MaximumSummaryBytes);
        _inner = inner;
        _maximumBytes = maximumBytes;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_faulted && _inner.CanWrite;
    public override long Length => _written;
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_faulted) throw new IOException("packet_summary_write_failed");
        if (buffer.Length > _maximumBytes - _written)
        {
            _faulted = true;
            throw new IOException("packet_summary_write_failed");
        }
        try
        {
            _inner.Write(buffer);
            _written += buffer.Length;
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
