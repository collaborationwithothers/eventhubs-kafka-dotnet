using Parchwell.LoadGen.Run.Execution;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Publishes a completed worker run's validated artifacts to its configured destination.</summary>
public interface IRunArtifactPublisher
{
    /// <summary>Whether the production destination and publisher are configured for this worker.</summary>
    bool IsAvailable { get; }

    /// <summary>Publishes every required artifact within the explicit destination and byte budgets.</summary>
    Task PublishAsync(RunExecutionResult result, RunPublicationOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Required Blob destination and finite upload limits for one publication.</summary>
public sealed record RunPublicationOptions(string Endpoint, string Container, TimeSpan UploadTimeout,
    long MaxArtifactBytes, long MaxAggregateArtifactBytes);

/// <summary>Explicit unavailable fallback for compositions without a publication adapter.</summary>
public sealed class UnavailableRunArtifactPublisher : IRunArtifactPublisher
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public Task PublishAsync(RunExecutionResult result, RunPublicationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("artifact_publication_unavailable");
    }
}
