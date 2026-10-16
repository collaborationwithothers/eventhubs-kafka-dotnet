using System.Collections.Immutable;
using System.Reflection;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Tests.Evidence;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RunExecutorCaptureFailureTests
{
    [Fact]
    public async Task Given_CaptureOverflowDuringFirstInvocation_When_Executing_Then_StopsAdmissionsAndRetainsReconciledDiagnosticArtifacts()
    {
        string directory = Path.Combine(Path.GetTempPath(), "loadgen-capture-fault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = new RunIdentity("A01", "C0", "capture-run", "pod-1", "pod-1", "pod-1");
            RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity);
            await using var evidence = new RunEvidenceSession(identity,
                new RunEvidenceOptions(directory, 8, 4096, 128 * 1024, 16, TimeSpan.FromSeconds(2)), TimeProvider.System,
                expectations);
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(
                Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"),
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, CaptureFaultProducer>();
            var proxy = (CaptureFaultProducer)(object)producer;
            proxy.Evidence = evidence;
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, evidence: evidence);
            string[] events = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];
            var profile = new WorkloadProfile(1, "synthetic-capture-fault", 1,
                [new TenantWeight("tenant", 1)], 1,
                events.ToImmutableDictionary(name => name, _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal),
                [new LoadStage("two", 2, 1)], new SourceLocation("synthetic-test-fixture"));
            var options = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), 4096, 64 * 1024, 64 * 1024, directory, "document-events", [], false);

            RunExecutionResult result = await executor.ExecuteAsync(profile, identity, options, TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(1, proxy.ProduceCount);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Equal(1, result.Manifest.FirstUnexecutedOrdinal);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains("evidence_capture_faulted", result.Manifest.Diagnostics);
            Assert.True(File.Exists(Path.Combine(directory, "capabilities.json")));
            Assert.All(result.Manifest.EvidenceFiles, pair => Assert.Equal(pair.Key, pair.Value));
            Assert.All(result.ArtifactPaths, pair => Assert.True(Path.IsPathFullyQualified(pair.Value)));
            var rows = await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), identity, 8, 4096, 64 * 1024, 64 * 1024,
                TestContext.Current.CancellationToken);
            Assert.Single(rows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public class CaptureFaultProducer : DispatchProxy
    {
        public RunEvidenceSession? Evidence { get; set; }
        public int ProduceCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted
                });
                Evidence!.CaptureLog(new LogMessage("producer", SyslogLevel.Warning, "synthetic", new string('x', 4096)));
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
#pragma warning restore CA1707
