using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Parchwell.Verifier.StartLine;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

public sealed class RepeatAdmissionDeadlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_SlowStartLineOrRefresh_When_AdmissionDeadlineExpires_Then_TheSame180SecondBudgetCancelsBeforeProducerExecution(bool slowRefresh)
    {
        string directory = Path.Combine(Path.GetTempPath(), "admission-deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var clock = new AdmissionClock();
            var cluster = new WaitingCluster(slowRefresh);
            var executor = new RepeatExecutor(cluster, clock);
            var context = new RepeatContext("C0", 1, "synthetic-run", new string('a', 40), directory);
            Task<RepeatExecution> execution = executor.ExecuteAsync(context, (_, token) =>
            {
                Assert.Equal(cluster.AdmissionToken, token);
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
            await cluster.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal([TimeSpan.FromSeconds(1500), TimeSpan.FromSeconds(180)], clock.Deadlines.Select(timer => timer.Due));
            Assert.NotEqual(cluster.WatchToken, cluster.AdmissionToken);

            clock.Deadlines[1].Fire();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.True(cluster.AdmissionToken.IsCancellationRequested);
            Assert.False(cluster.WatchToken.IsCancellationRequested);
            Assert.Equal(slowRefresh ? 1 : 0, cluster.ReleaseCalls);
            Assert.False(cluster.ProducerWaited);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class WaitingCluster(bool slowRefresh) : IRunCluster
    {
        internal TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken WatchToken { get; private set; }
        internal CancellationToken AdmissionToken { get; private set; }
        internal int ReleaseCalls { get; private set; }
        internal bool ProducerWaited { get; private set; }
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken)
        { WatchToken = cancellationToken; return Task.FromResult(new PodAdmission(context.RunId, "initial")); }
        public Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken)
        { AdmissionToken = cancellationToken; return Task.CompletedTask; }
        public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
        {
            Assert.Equal(AdmissionToken, cancellationToken);
            var pod = new ProducerPod("synthetic-pod", "synthetic-uid");
            admission.Observe(new("observed", "ADDED", pod));
            return Task.FromResult(pod);
        }
        public Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        { Assert.Equal(AdmissionToken, cancellationToken); return Task.FromResult(new string('a', 64)); }
        public async Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        {
            Assert.Equal(AdmissionToken, cancellationToken);
            if (!slowRefresh) await WaitAsync(cancellationToken);
            var worker = new VerifierIdentity(identity.ScenarioId, identity.ConfigurationId, identity.RunId,
                identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId);
            byte[] inventory = BrokerInventoryCodec.Write(worker, [new BrokerMetadata(1, "broker.example.invalid", 9093)], context.SourceCommit, 16777216);
            string receipt = System.Text.Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("start-line", worker, context.SourceCommit));
            return new(new(new(0, receipt, "")), inventory);
        }
        public async Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken)
        {
            Assert.Equal(AdmissionToken, cancellationToken);
            ReleaseCalls++;
            await WaitAsync(cancellationToken);
        }
        private async Task WaitAsync(CancellationToken token)
        {
            Waiting.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        public Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken)
        { ProducerWaited = true; throw new InvalidOperationException("producer_must_not_start"); }
        public Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("publication_must_not_start");
        public Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken) => throw new InvalidOperationException("verify_must_not_start");
        public Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AdmissionClock : TimeProvider
    {
        internal List<AdmissionTimer> Deadlines { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new AdmissionTimer(callback, state, dueTime);
            Deadlines.Add(timer);
            return timer;
        }
    }
    private sealed class AdmissionTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        internal TimeSpan Due { get; } = due;
        internal void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
#pragma warning restore CA1707
