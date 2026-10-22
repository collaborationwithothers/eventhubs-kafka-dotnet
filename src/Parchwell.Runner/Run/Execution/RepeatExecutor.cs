using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed record RepeatExecution(WorkerIdentity Identity, AcceptedWorkerOutcome Outcome,
    ProducerPublication Publication, byte[] Result, IReadOnlyList<PodWatchEvent> PodHistory)
{
    public PodAdmission? Admission { get; init; }
}

public sealed class RepeatExecutor(IRunCluster cluster, TimeProvider clock, ExecutionBudgets? budgets = null)
{
    private readonly Dictionary<string, PodAdmission> admissions = new(StringComparer.Ordinal);

    public Task PreflightAsync(System.Text.Json.JsonElement outputs, CancellationToken cancellationToken) => cluster.PreflightAsync(outputs, cancellationToken);

    public async Task<RepeatExecution> ExecuteAsync(RepeatContext context,
        Func<WorkerIdentity, CancellationToken, Task> recordAdmission, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(budgets?.Seconds("repeat_seconds") ?? TimeSpan.FromSeconds(1500), clock);
        using var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken token = work.Token;
        PodAdmission admission = await cluster.EstablishProducerWatchAsync(context, token).ConfigureAwait(false);
        admissions.Add(context.RunId, admission);
        await cluster.CreateProducerAsync(context, token).ConfigureAwait(false);
        ProducerPod pod = await cluster.ObserveProducerAsync(context, admission, token).ConfigureAwait(false);
        if (pod != admission.Producer) throw new InvalidDataException("independent_producer_identity");
        var identity = new WorkerIdentity("A01", context.Phase == "probe" ? "C0" : context.Phase,
            context.RunId, pod.Name, pod.Name, pod.Name);
        await recordAdmission(identity, token).ConfigureAwait(false);
        string comparisonHash = await cluster.GenerateComparisonAsync(context, identity, token).ConfigureAwait(false);
        StartLineExecution start = await cluster.StartLineAsync(context, identity, token).ConfigureAwait(false);
        await PersistWorkerAsync(context, "start-line", start.Worker, token).ConfigureAwait(false);
        if (start.Worker.Process.ExitCode != 0 || !WorkerOutcome.HasReceipt(start.Worker.Process, "start-line", identity, context.SourceCommit))
            throw new InvalidDataException("start_line_execution");
        await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, "broker-inventory.json"), start.Inventory, token).ConfigureAwait(false);
        IReadOnlyList<string> brokers = BrokerInventory.Read(start.Inventory, identity, context.SourceCommit);
        await cluster.ReleaseAsync(context, admission.Release(brokers, comparisonHash), token).ConfigureAwait(false);
        WorkerExecution producer = await cluster.WaitProducerAsync(context, token).ConfigureAwait(false);
        await PersistWorkerAsync(context, "loadgen", producer, token).ConfigureAwait(false);
        if (producer.Process.ExitCode != 0 || !WorkerOutcome.HasReceipt(producer.Process, "loadgen", identity, context.SourceCommit))
            throw new InvalidDataException("loadgen_execution");
        ProducerPublication publication = await cluster.DownloadPublicationAsync(context, token).ConfigureAwait(false);
        PublicationAcceptance.Validate(publication, identity);
        foreach ((string name, byte[] bytes) in publication.Objects)
            await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, name), bytes, token).ConfigureAwait(false);
        WorkerExecution verifier = await cluster.VerifyAsync(context, identity, brokers, token).ConfigureAwait(false);
        await PersistWorkerAsync(context, "verify", verifier, token).ConfigureAwait(false);
        _ = admission.Producer;
        byte[] result = verifier.Result ?? [];
        AcceptedWorkerOutcome outcome = WorkerOutcome.Reconcile(verifier.Process, result, identity, context.SourceCommit);
        if (!outcome.ExecutionCompleted) throw new InvalidDataException(outcome.Reason);
        return new(identity, outcome, publication, result, admission.Events.ToArray()) { Admission = admission };
    }

    public async Task PreserveFailureAsync(RepeatContext context, CancellationToken cancellationToken, CancellationToken retentionToken)
    {
        try { await cluster.PreserveAvailableArtifactsAsync(context, cancellationToken).ConfigureAwait(false); }
        finally
        {
            if (admissions.TryGetValue(context.RunId, out PodAdmission? admission))
            {
                try { await cluster.CompleteProducerWatchAsync(context, admission, cancellationToken).ConfigureAwait(false); }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException) { }
                await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, "pod-history.json"),
                    System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(admission.Events), retentionToken).ConfigureAwait(false);
            }
        }
    }

    public async Task ObserveThroughExportAsync(RepeatContext context, RepeatExecution repeat, CancellationToken cancellationToken)
    {
        PodAdmission admission = repeat.Admission ?? throw new InvalidDataException("producer_watch_receipt_missing");
        await cluster.CompleteProducerWatchAsync(context, admission, cancellationToken).ConfigureAwait(false);
        _ = admission.Producer;
        await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, "pod-history.json"),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(admission.Events), cancellationToken).ConfigureAwait(false);
    }

    private static async Task PersistWorkerAsync(RepeatContext context, string mode, WorkerExecution worker, CancellationToken token)
    {
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, mode + "-stdout.log"), worker.Process.StandardOutput, token).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, mode + "-termination.json"),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { exit_code = worker.Process.ExitCode, timed_out = worker.Process.TimedOut }), token).ConfigureAwait(false);
        if (worker.Result is not null)
            await File.WriteAllBytesAsync(Path.Combine(context.OutputDirectory, mode == "start-line" ? "start-line.json" : "result.json"), worker.Result, token).ConfigureAwait(false);
    }
}
