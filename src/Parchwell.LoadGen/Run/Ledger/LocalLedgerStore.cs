using System.Threading.Channels;
using System.IO;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Ledger;

/// <summary>Bounded, non-waiting append queue backed by one asynchronous local-file writer.</summary>
public sealed class LocalLedgerStore : ILedgerStore
{
    private static readonly byte[] Newline = [(byte)'\n'];
    private readonly LedgerStoreOptions options;
    private readonly Func<Stream>? writerStreamFactory;
    private readonly Channel<LedgerRow> queue;
    private readonly CancellationTokenSource writerCancellation = new();
    private readonly Task writerTask;
    private Exception? failure;
    private int draining;
    private int disposed;
    private long writtenBytes;

    /// <summary>Creates the local ledger and starts its single background writer.</summary>
    public LocalLedgerStore(LedgerStoreOptions options)
        : this(options, null)
    {
    }

    internal LocalLedgerStore(LedgerStoreOptions options, Func<Stream>? writerStreamFactory)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.writerStreamFactory = writerStreamFactory;
        options.Validate();
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.LedgerPath));
        if (directory is not null) Directory.CreateDirectory(directory);
        queue = Channel.CreateBounded<LedgerRow>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        writerTask = WriteRowsAsync();
    }

    /// <inheritdoc />
    public bool IsFaulted => Failure is not null;

    /// <inheritdoc />
    public Exception? Failure => Volatile.Read(ref failure);

    /// <inheritdoc />
    public bool TryAppend(LedgerRow row)
    {
        if (IsFaulted || Volatile.Read(ref draining) != 0) return false;
        if (row is null)
        {
            SetFailure(new ArgumentNullException(nameof(row)));
            return false;
        }
        if (queue.Writer.TryWrite(row)) return true;
        SetFailure(new InvalidOperationException("ledger_queue_full_or_closed"));
        return false;
    }

    /// <inheritdoc />
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref draining, 1) == 0) queue.Writer.TryComplete();
        using var deadline = new CancellationTokenSource(options.DrainTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await writerTask.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            SetFailure(new TimeoutException("ledger_writer_drain_timeout", ex));
            writerCancellation.Cancel();
            throw Failure!;
        }
        catch (OperationCanceledException ex)
        {
            SetFailure(ex);
            writerCancellation.Cancel();
            throw;
        }
        catch (Exception ex) when (Failure is not null)
        {
            throw new IOException("ledger_writer_failed", Failure ?? ex);
        }
        if (Failure is not null) throw new IOException("ledger_writer_failed", Failure);
    }

    /// <inheritdoc />
    public async Task SaveManifestAsync(RunManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (options.MaximumManifestBytes <= 1) throw new InvalidDataException("manifest_byte_limit_exceeded");
        var bytes = LedgerValidator.SerializeManifest(manifest, options.MaximumManifestBytes - 1);
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.ManifestPath));
        if (directory is not null) Directory.CreateDirectory(directory);
        await using var stream = new FileStream(options.ManifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(Newline, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Drains the row writer and persists the supplied independent manifest.</summary>
    public async Task CompleteAsync(RunManifest manifest, CancellationToken cancellationToken = default)
    {
        await DrainAsync(cancellationToken).ConfigureAwait(false);
        await SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try
        {
            if (Volatile.Read(ref draining) == 0) await DrainAsync().ConfigureAwait(false);
        }
        finally { writerCancellation.Dispose(); }
    }

    private async Task WriteRowsAsync()
    {
        try
        {
            await using var stream = writerStreamFactory?.Invoke() ?? new FileStream(options.LedgerPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await foreach (var ledgerRow in queue.Reader.ReadAllAsync(writerCancellation.Token).ConfigureAwait(false))
            {
                LedgerValidator.ValidateRow(ledgerRow);
                var row = LedgerValidator.SerializeRow(ledgerRow, options.MaximumRowBytes);
                var nextSize = checked(Interlocked.Read(ref writtenBytes) + row.Length + 1);
                if (nextSize > options.MaximumLedgerBytes) throw new InvalidDataException("ledger_byte_limit_exceeded");
                await stream.WriteAsync(row, writerCancellation.Token).ConfigureAwait(false);
                await stream.WriteAsync(Newline, writerCancellation.Token).ConfigureAwait(false);
                Interlocked.Exchange(ref writtenBytes, nextSize);
            }
            await stream.FlushAsync(writerCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetFailure(ex);
            queue.Writer.TryComplete(ex);
            throw;
        }
    }

    private void SetFailure(Exception exception)
    {
        Interlocked.CompareExchange(ref failure, exception, null);
    }
}
