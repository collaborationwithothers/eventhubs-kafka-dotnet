using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifierOutcomeComponentTests
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    [Fact]
    public Task Given_DownstreamLatency_When_ProduceFailsButDirectScanSeesAttempt_Then_ProvesRealPhantom() =>
        ProveAsync("phantom", SpikeFault.Latency, 0);

    [Fact]
    public Task Given_UpstreamTimeoutRemovedAfterFailedCallback_When_CompleteDirectScanHasNoTarget_Then_ProvesRealReportedFailure() =>
        ProveAsync("reported-failure-revised", SpikeFault.Timeout, 0);

    [Fact]
    public Task Given_DownstreamLatencyAndNativeRetries_When_SingleProduceIsRepeatedByClient_Then_ProvesRealDuplicate() =>
        ProveAsync("native-retry-duplicate-revised", SpikeFault.Latency, int.MaxValue);

    private static async Task ProveAsync(string recipe, SpikeFault fault, int retries)
    {
        var root = Environment.GetEnvironmentVariable("PARCHWELL_FIXTURE_PROOF_DIR")
            ?? Path.Combine(Path.GetTempPath(), $"verifier-outcome-proof-{Guid.NewGuid():N}");
        var selectedTrialText = Environment.GetEnvironmentVariable("PARCHWELL_FIXTURE_PROOF_TRIAL");
        var firstTrial = 1;
        var lastTrial = 5;
        if (selectedTrialText is not null)
        {
            firstTrial = int.Parse(selectedTrialText, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(firstTrial, 1, 5);
            lastTrial = firstTrial;
        }
        for (int trial = firstTrial; trial <= lastTrial; trial++)
        {
            var directory = Path.Combine(root, $"{recipe}-trial-{trial}");
            Directory.CreateDirectory(directory);
            if (await TrialAsync(recipe, fault, retries, trial, directory)) return;
        }
        Assert.Fail($"Recipe {recipe} did not yield its required witness in selected trials {firstTrial} through {lastTrial}. Evidence: {root}");
    }

    private static async Task<bool> TrialAsync(string recipe, SpikeFault fault, int retries, int trial, string directory)
    {
        using var workDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(workDeadline.Token, TestContext.Current.CancellationToken);
        var clock = TimeProvider.System;
        var started = clock.GetUtcNow();
        var id = Guid.NewGuid().ToString("N");
        var target = new AttemptMessage("document-events", Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"), Encoding.UTF8.GetBytes($"synthetic-key-{id}"), Encoding.UTF8.GetBytes("synthetic-payload"));
        var warmup = target with { AttemptId = Guid.NewGuid().ToString("N"), Key = Encoding.UTF8.GetBytes("synthetic-warmup") };
        // Duplicate-only amendment approved in issue comment 6260447919; other recipes stay frozen.
        var timings = recipe == "native-retry-duplicate-revised"
            ? new TimingSettings(MessageTimeoutMs: 30000) : new TimingSettings();
        LocalKafkaFixture? fixture = null;
        ProducerProbe? producer = null;
        DirectObserver? observer = null;
        ComponentRunHarness? fullRun = null;
        byte[]? fullResult = null;
        int? fullExitCode = null;
        PendingProduce? pending = null;
        ObservationScan? scan = null;
        ObservationScan? warmScan = null;
        var beforeRestoreScans = new List<ObservationScan>();
        string? nativeRetryEvent = null;
        DateTimeOffset? nativeRetryObserved = null;
        DateTimeOffset? targetObservedBeforeRestore = null;
        int targetLogStart = 0;
        InfrastructureDiagnostics? infrastructure = null;
        ProducerDiagnostics? diagnostics = null;
        OutcomeCounts? diagnosticCounts = null;
        string? faultState = null;
        string? removedFaultState = null;
        DateTimeOffset? callbackReceived = null;
        DateTimeOffset? toxicRemoved = null;
        DateTimeOffset? scanStarted = null;
        DateTimeOffset? workFinished = null;
        string[]? brokers = null;
        Exception? failure = null;
        bool success = false;
        bool cleanupSucceeded = false;
        try
        {
            fixture = new LocalKafkaFixture();
            await fixture.StartAsync(linked.Token);
            await fixture.ResetProxyAsync(id, linked.Token);
            await DirectObserver.CreateTopicAsync(fixture.ObserverBootstrapServers, target.Topic, linked.Token);
            observer = new DirectObserver(fixture.ObserverBootstrapServers, target.Topic, clock);
            producer = new ProducerProbe(fixture.ProducerBootstrapServers, new TrialConfiguration(retries, fault, trial), timings, clock);
            brokers = producer.MetadataBrokerEndpoints().ToArray();
            Assert.NotEmpty(brokers);
            Assert.All(brokers, endpoint => Assert.Equal(fixture.ProducerBootstrapServers, endpoint));
            var warmStart = observer.GetHighWatermark(linked.Token);
            var warmPending = producer.ProduceAttempt(warmup);
            Assert.True(warmPending.Accepted, warmPending.SynchronousError);
            var warmDelivery = await warmPending.Completion.WaitAsync(linked.Token);
            Assert.False(warmDelivery.IsError);
            await producer.DrainAsync(linked.Token);
            warmScan = await observer.ScanAsync(warmStart, warmup, linked.Token);
            Assert.True(warmScan.Complete, warmScan.Failure);
            Assert.Single(warmScan.Records);
            Assert.True(Matches(warmScan.Records[0], warmup));
            while (producer.SnapshotDiagnostics().Statistics.Count == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
            fullRun = new ComponentRunHarness(fixture.ObserverBootstrapServers, target.InstanceId);
            Assert.Equal(0, await fullRun.StartAsync(linked.Token));
            var start = observer.GetHighWatermark(linked.Token);
            faultState = await fixture.ApplyFaultAsync(fault, timings, linked.Token,
                recipe == "reported-failure-revised" ? "upstream" : "downstream");
            targetLogStart = producer.SnapshotDiagnostics().Logs.Count;
            pending = producer.ProduceAttempt(target); // One application call; native retries retain this identity.
            Assert.True(pending.Accepted, pending.SynchronousError);
            if (recipe == "native-retry-duplicate-revised")
            {
                // The unique topic has one outstanding target after a fully drained warmup.
                // Retain the native topic-specific action, not a global error or fabricated retry.
                while (nativeRetryEvent is null)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    nativeRetryEvent = producer.SnapshotDiagnostics().Logs.Skip(targetLogStart).FirstOrDefault(line =>
                        line.Contains(target.Topic, StringComparison.Ordinal)
                        && line.Contains("MSGSET", StringComparison.Ordinal)
                        && line.Contains("Retry,MsgPossiblyPersisted", StringComparison.Ordinal));
                    if (nativeRetryEvent is null)
                        await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
                }
                nativeRetryObserved = clock.GetUtcNow();
                while (targetObservedBeforeRestore is null)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var observedBeforeRestore = await observer.ScanAsync(start, target, linked.Token);
                    beforeRestoreScans.Add(observedBeforeRestore);
                    Assert.True(observedBeforeRestore.Complete, observedBeforeRestore.Failure);
                    Assert.Null(observedBeforeRestore.Failure);
                    if (observedBeforeRestore.Records.Any(record => Matches(record, target)))
                        targetObservedBeforeRestore = clock.GetUtcNow();
                    else
                        await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
                }
                removedFaultState = await fixture.RemoveFaultAsync(linked.Token);
                toxicRemoved = clock.GetUtcNow();
                Assert.True(nativeRetryObserved <= toxicRemoved && targetObservedBeforeRestore <= toxicRemoved);
            }
            var callback = await pending.Completion.WaitAsync(linked.Token);
            callbackReceived = clock.GetUtcNow();
            if (recipe == "reported-failure-revised")
            {
                Assert.True(callback.IsError);
                removedFaultState = await fixture.RemoveFaultAsync(linked.Token);
                toxicRemoved = clock.GetUtcNow();
            }
            await producer.DrainAsync(linked.Token);
            scanStarted = clock.GetUtcNow();
            scan = await observer.ScanAsync(start, target, linked.Token);
            Assert.True(scan.Complete, scan.Failure);
            Assert.Null(scan.Failure);
            Assert.Single(pending.Callbacks);
            var observed = scan.Records.Where(record => Matches(record, target)).ToArray();
            if (recipe == "reported-failure-revised")
            {
                Assert.True(callbackReceived <= toxicRemoved && toxicRemoved <= scanStarted);
                Assert.Equal(scan.Start, scan.End);
                Assert.Equal(scan.End, scan.Position);
                Assert.Empty(scan.Records);
            }
            // These projections retain the genuine callback/record identities and outcomes.
            // Other bookkeeping metadata is synthetic; no valid full-run result is claimed.
            var attempt = RecordedEvidence.Attempt(target.AttemptId, Encoding.UTF8.GetString(target.Key)) with { InstanceId = target.InstanceId };
            var ledger = RecordedEvidence.LedgerRow(attempt, !callback.IsError);
            var records = observed.Select(record => new ConsumedRecord(record.Topic, record.Partition, record.Offset,
                Encoding.UTF8.GetString(record.Key), record.InstanceId, record.AttemptId, 1, [])).ToArray();
            diagnosticCounts = OutcomeClassifier.Classify([ledger], records);
            diagnostics = producer.SnapshotDiagnostics();
            var actualRow = fullRun.Row(target.AttemptId, Encoding.UTF8.GetString(target.Key), !callback.IsError,
                (int)Enum.Parse<Confluent.Kafka.PersistenceStatus>(callback.PersistenceStatus));
            actualRow["error_code"] = (int)Enum.Parse<Confluent.Kafka.ErrorCode>(callback.ErrorCode);
            actualRow["error_name"] = callback.ErrorCode;
            await fullRun.SetAttemptsAsync(actualRow);
            ComponentEvidenceBuilder.ApplyMeasuredGuardCounters(fullRun, diagnostics);
            fullExitCode = await fullRun.VerifyAsync(linked.Token);
            Assert.Equal(1, fullExitCode.Value);
            VerifierWindowComponentTests.AssertFailed(fullRun,
                recipe == "native-retry-duplicate-revised" ? 15 : 12);
            fullResult = fullRun.Outputs[fullRun.Options.ResultOutputName!];
            var handlerDirectory = Path.Combine(directory, "synthetic-handler-inputs");
            Directory.CreateDirectory(handlerDirectory);
            foreach (var input in fullRun.Inputs)
                await File.WriteAllBytesAsync(Path.Combine(handlerDirectory, input.Key), input.Value, linked.Token);
            await File.WriteAllBytesAsync(Path.Combine(handlerDirectory, fullRun.Options.StartLineInputName!),
                fullRun.Outputs[fullRun.Options.StartLineInputName!], linked.Token);
            await File.WriteAllBytesAsync(Path.Combine(directory, "full-handler-result.json"), fullResult, linked.Token);
            success = recipe switch
            {
                "phantom" => callback.IsError && observed.Length >= 1 && diagnosticCounts.Phantom >= 1,
                "reported-failure-revised" => callback.IsError && observed.Length == 0 && diagnosticCounts.ReportedFailure >= 1,
                "native-retry-duplicate-revised" => observed.Length >= 2 && diagnosticCounts.Duplicate >= 1
                    && nativeRetryEvent is not null && targetObservedBeforeRestore is not null,
                _ => throw new InvalidOperationException("Unknown proof recipe.")
            };
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            workFinished = clock.GetUtcNow();
            diagnostics ??= producer?.SnapshotDiagnostics();
            using var diagnosticDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (fixture is not null)
            {
                try { infrastructure = await fixture.CaptureDiagnosticsAsync(diagnosticDeadline.Token); }
                catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            }
            using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await Task.Run(() => { observer?.Dispose(); producer?.Dispose(); fullRun?.Dispose(); }, CancellationToken.None).WaitAsync(cleanupDeadline.Token);
                if (fixture is not null) await fixture.DisposeAsync().AsTask().WaitAsync(cleanupDeadline.Token);
                cleanupSucceeded = true;
            }
            catch (Exception exception)
            {
                failure = failure is null ? exception : new AggregateException(failure, exception);
            }
            success &= cleanupSucceeded && failure is null;
            await File.WriteAllTextAsync(Path.Combine(directory, "proof.json"), JsonSerializer.Serialize(new
            {
                recipe, trial, retries, fault, timings, started, finished = clock.GetUtcNow(),
                kafkaImage = LocalKafkaFixture.KafkaImage, proxyImage = LocalKafkaFixture.ProxyImage,
                target, callbacks = pending?.Callbacks, accepted = pending?.Accepted, warmScan, scan, brokers,
                faultState, removedFaultState, callbackReceived, toxicRemoved, scanStarted, workFinished,
                targetLogStart, beforeRestoreScans, nativeRetryEvent, nativeRetryObserved, targetObservedBeforeRestore,
                workElapsedSeconds = (workFinished.Value - started).TotalSeconds,
                workBudgetExpired = workDeadline.IsCancellationRequested,
                diagnosticCounts, fullExitCode, fullResult = fullResult is null ? null : Encoding.UTF8.GetString(fullResult), diagnostics, infrastructure, failure = failure?.ToString(), cleanupSucceeded, success
            }, EvidenceJsonOptions), CancellationToken.None);
        }
        Assert.False(workDeadline.IsCancellationRequested, $"Work budget expired; no subsequent trial is permitted. Evidence: {directory}");
        Assert.True(cleanupSucceeded, $"Cleanup failed or was unknown; no subsequent trial is permitted. Evidence: {directory}");
        return success;
    }

    private static bool Matches(ConsumedObservation record, AttemptMessage target) =>
        record.InstanceId == target.InstanceId && record.AttemptId == target.AttemptId
        && record.Key.AsSpan().SequenceEqual(target.Key) && record.Payload.AsSpan().SequenceEqual(target.Payload)
        && record.Headers?.Count(header => header.Key == "sequence" && header.Value is not null
            && header.Value.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes("1"))) == 1;

}
