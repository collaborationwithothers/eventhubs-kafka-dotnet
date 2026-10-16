using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Ledger;

/// <summary>A non-waiting append boundary for terminal producer observations.</summary>
public interface ILedgerStore : IAsyncDisposable
{
    /// <summary>True once any capacity, validation, serialization or file error has permanently faulted capture.</summary>
    bool IsFaulted { get; }

    /// <summary>The first permanent capture or writer error, when one has occurred.</summary>
    Exception? Failure { get; }

    /// <summary>Attempts to queue one row without waiting for capacity or file I/O.</summary>
    bool TryAppend(LedgerRow row);

    /// <summary>Stops appends and drains the writer within its configured budget.</summary>
    Task DrainAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the independent manifest directly, bypassing the row queue and writer.</summary>
    Task SaveManifestAsync(RunManifest manifest, CancellationToken cancellationToken = default);

    /// <summary>Drains rows and saves the manifest as a convenience when no later evidence must be added.</summary>
    async Task CompleteAsync(RunManifest manifest, CancellationToken cancellationToken = default)
    {
        await DrainAsync(cancellationToken).ConfigureAwait(false);
        await SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
    }
}
