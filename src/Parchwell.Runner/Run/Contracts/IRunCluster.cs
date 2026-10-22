using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Contracts;

public sealed record RepeatContext(string Phase, int Ordinal, string RunId, string SourceCommit, string OutputDirectory);
public sealed record WorkerExecution(ProcessResult Process, byte[]? Result = null);
public sealed record ProducerPublication(IReadOnlyDictionary<string, byte[]> Objects);
public sealed record StartLineExecution(WorkerExecution Worker, byte[] Inventory);

public interface IRunCluster
{
    Task PreflightAsync(System.Text.Json.JsonElement outputs, CancellationToken cancellationToken);
    Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken);
    Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken);
    Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken);
    Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken);
    Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken);
    Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken);
    Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken);
    Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken);
    Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken);
    Task PreserveObservedWorkersAsync(RepeatContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken);
    Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken);
}
