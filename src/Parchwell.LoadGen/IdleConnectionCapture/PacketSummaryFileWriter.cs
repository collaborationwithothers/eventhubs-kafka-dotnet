namespace Parchwell.LoadGen.IdleConnectionCapture;

/// <summary>Owns a bounded local packet-summary staging file until finalization or cleanup.</summary>
internal sealed class PacketSummaryFileWriter : IDisposable
{
    private readonly IPacketSummaryStagingFile? _staging;
    private readonly PacketSummaryStreamWriter? _core;
    private bool _closeAttempted;
    private bool _deleteAttempted;
    private bool _ownsPartial;

    public PacketSummaryFileWriter(string directory, PacketSummaryIdentity identity)
        : this(directory, identity, new PacketSummaryFileFactory()) { }

    internal PacketSummaryFileWriter(string directory, PacketSummaryIdentity identity,
        IPacketSummaryFileFactory factory, PacketSummaryBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(factory);
        if (!PacketSummaryEncoding.IsValidIdentity(identity))
        {
            Fault("packet_identity_invalid");
            return;
        }
        if (string.IsNullOrEmpty(directory) || directory.Contains('\0', StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(directory))
        {
            Fault("packet_summary_path_invalid");
            return;
        }
        if (!factory.IsSupported)
        {
            Fault("packet_summary_platform_unsupported");
            return;
        }
        try
        {
            _staging = factory.Create(directory);
            _ownsPartial = true;
        }
#pragma warning disable CA1031 // Boundary exceptions, including cancellation, expose only a fixed stage code.
        catch (Exception)
#pragma warning restore CA1031
        {
            Fault("packet_summary_open_failed");
            return;
        }
        try
        {
            _core = new PacketSummaryStreamWriter(_staging.Content, identity, budget ?? new PacketSummaryBudget());
        }
#pragma warning disable CA1031 // A failing content stream must not expose filesystem diagnostics.
        catch (Exception)
#pragma warning restore CA1031
        {
            Fault("packet_summary_write_failed");
            return;
        }
        if (_core.State == PacketSummaryWriterState.Faulted) Fault(_core.FailureCode!);
    }

    public PacketSummaryWriterState State { get; private set; }
    public string? FailureCode { get; private set; }
    public bool CleanupFailed { get; private set; }
    public int EventCount => _core?.EventCount ?? 0;
    public long BytesWritten => _core?.BytesWritten ?? 0;

    public bool TryAppend(PacketSummaryEvent value)
    {
        if (!RequireActive()) return false;
        return _core!.TryAppend(value) || Fault(_core.FailureCode!);
    }

    public bool TryComplete(DateTimeOffset endedUtc)
    {
        if (!RequireActive()) return false;
        if (!_core!.TryComplete(endedUtc)) return Fault(_core.FailureCode!);
        try
        {
            _staging!.FlushToDisk();
        }
#pragma warning disable CA1031 // Finalization stops at the first boundary failure without diagnostic disclosure.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Fault("packet_summary_flush_failed");
        }
        _closeAttempted = true;
        try
        {
            _staging!.Close();
        }
#pragma warning disable CA1031 // A failed close is recorded and never retried, including during cleanup.
        catch (Exception)
#pragma warning restore CA1031
        {
            CleanupFailed = true;
            return Fault("packet_summary_close_failed");
        }
        try
        {
            _staging!.FinalizeFile();
        }
#pragma warning disable CA1031 // Native and filesystem failures expose only the fixed finalization code.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Fault("packet_summary_finalize_failed");
        }
        // The old partial name is no longer ours; later calls must not delete a new entry there.
        _ownsPartial = false;
        State = PacketSummaryWriterState.Completed;
        return true;
    }

    public void Dispose()
    {
        if (State == PacketSummaryWriterState.Active) Fault("packet_summary_abandoned");
    }

    private bool RequireActive() => State == PacketSummaryWriterState.Active ||
        (State != PacketSummaryWriterState.Faulted && Fault("packet_summary_state_invalid"));

    private bool Fault(string failure)
    {
        State = PacketSummaryWriterState.Faulted;
        FailureCode ??= failure;
        Cleanup();
        return false;
    }

    private void Cleanup()
    {
        if (!_ownsPartial) return;
        if (!_closeAttempted)
        {
            _closeAttempted = true;
            try
            {
                _staging!.Close();
            }
#pragma warning disable CA1031 // Cleanup is one attempt and cannot replace the original failure code.
            catch (Exception)
#pragma warning restore CA1031
            {
                CleanupFailed = true;
            }
        }
        if (_deleteAttempted) return;
        _deleteAttempted = true;
        try
        {
            _staging!.DeletePartial();
        }
#pragma warning disable CA1031 // Cleanup failures are reported only by the bounded flag, never exception text.
        catch (Exception)
#pragma warning restore CA1031
        {
            CleanupFailed = true;
        }
    }
}
