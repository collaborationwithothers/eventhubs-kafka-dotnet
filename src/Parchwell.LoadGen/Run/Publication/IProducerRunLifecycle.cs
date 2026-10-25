using Parchwell.LoadGen.Run.Execution;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Persists LoadGen's registration, failed-run diagnostics and last-action completion record.</summary>
public interface IProducerRunLifecycle
{
    /// <summary>Creates the registration object before native producer construction.</summary>
    Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken);

    /// <summary>Uploads bounded diagnostic snapshots without changing the run result.</summary>
    Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
        ProducerLifecycleOptions options, CancellationToken cancellationToken);

    /// <summary>Writes the create-only receipt after all other worker cleanup succeeds.</summary>
    Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options,
        CancellationToken cancellationToken);
}

/// <summary>Blob destination and finite lifecycle artifact limits for one worker invocation.</summary>
/// <param name="Endpoint">The HTTPS Blob service endpoint.</param>
/// <param name="Container">The destination container.</param>
/// <param name="CleanupTimeout">The per-operation cleanup deadline.</param>
/// <param name="UploadTimeout">The diagnostic upload deadline.</param>
/// <param name="MaxArtifactBytes">The maximum size of one diagnostic object.</param>
/// <param name="MaxAggregateArtifactBytes">The maximum combined diagnostic snapshot size.</param>
/// <param name="OutputRoot">The known local artifact directory.</param>
public sealed record ProducerLifecycleOptions(string Endpoint, string Container, TimeSpan CleanupTimeout,
    TimeSpan UploadTimeout, long MaxArtifactBytes, long MaxAggregateArtifactBytes, string OutputRoot);
