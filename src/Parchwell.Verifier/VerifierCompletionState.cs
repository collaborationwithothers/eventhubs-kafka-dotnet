using System.Text;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier;

/// <summary>Tracks normal handler return separately from the handler's validity exit code.</summary>
internal sealed class VerifierCompletionState
{
    private int _state;
    private Completion? _completion;
    private PendingBlobReceipt? _blobReceipt;

    internal void MarkStopped() => Interlocked.CompareExchange(ref _state, 2, 0);

    internal void MarkReturned(VerifierHostOptions options, int exitCode,
        CancellationToken stoppingToken, CancellationToken applicationStopping)
    {
        if (options.Mode == "probe-gate")
        {
            _completion = new(options.Mode, options.ScenarioId!, null, options.VerifierCommit!, exitCode);
        }
        Interlocked.CompareExchange(ref _state, 1, 0);
        if (stoppingToken.IsCancellationRequested || applicationStopping.IsCancellationRequested)
            Interlocked.Exchange(ref _state, 2);
    }

    internal async Task WriteReceiptAsync(TextWriter output)
    {
        if (Volatile.Read(ref _state) != 1 || _completion is not { } completed || completed.Mode != "probe-gate") return;
        byte[] bytes = DeploymentJson.Write(65_536, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", 1);
            writer.WriteString("mode", completed.Mode);
            if (completed.Identity is { } identity) DeploymentJson.WriteIdentity(writer, identity);
            else
            {
                writer.WritePropertyName("identity");
                writer.WriteStartObject();
                writer.WriteString("scenario_id", completed.ScenarioId);
                writer.WriteEndObject();
            }
            writer.WriteString("source_commit", completed.SourceCommit);
            writer.WriteNumber("handler_exit_code", completed.ExitCode);
            writer.WriteBoolean("cleanup_completed", true);
            writer.WriteEndObject();
        });
        await output.WriteLineAsync("PARCHWELL_COMPLETION " + Encoding.UTF8.GetString(bytes)).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    internal void SetPendingBlobReceipt(string container, string name, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);
        if (Volatile.Read(ref _state) != 0) throw new InvalidOperationException("Completion has already been decided.");
        _blobReceipt = new(container,name,bytes);
    }

    internal bool TryGetBlobReceipt(out string container,out string name,out byte[] bytes)
    {
        PendingBlobReceipt? receipt=Volatile.Read(ref _state)==1?_blobReceipt:null;
        container=receipt?.Container??string.Empty;name=receipt?.Name??string.Empty;bytes=receipt?.Bytes??[];
        return receipt is not null;
    }

    internal void MarkFailed(){_blobReceipt=null;Interlocked.Exchange(ref _state,2);}

    private sealed record Completion(string Mode, string ScenarioId, RunIdentity? Identity, string SourceCommit, int ExitCode);
    private sealed record PendingBlobReceipt(string Container,string Name,byte[] Bytes);
}
