using Confluent.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.VerifyRun.B10Final;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
// Start-line authentication precedes end-watermark capture; CanAssign cannot stand in for check 7.
public sealed class B10FinalInputOptionsTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public async Task Given_ThreePartitions_When_RepeatOptionsReachTheStartReader_Then_OnlyTheConfiguredPartitionLimitDecidesAuthentication(int maximum, bool accepted)
    {
        const string run = "b10-1-1", commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string utc = "2026-11-12T00:00:00.0000000+00:00";
        string job = PhaseIdentity.JobName(run, "C1", 1);
        var identity = new RunIdentity("B10", "C1", run, job, job, job);
        var note = new StartLineNote(1, identity, "document-events", [new(0, 0), new(1, 0), new(2, 0)], utc, commit,
            typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, [job, job + "-restart", job + "-successor"]);
        string start = PhasePaths.StartLine(run, "C1", 1), inventory = PhasePaths.BrokerInventory(run, "C1", 1);
        var objects = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [start] = StartLineNoteCodec.Write(note, new OutputLimits(16384, 100, 1024)),
            [inventory] = BrokerInventoryCodec.Write(identity, [new BrokerMetadata(1, "broker-a", 9093)], commit, 16384)
        };
        objects[PhasePaths.StartLineCompletion(run, "C1", 1)] = PhaseWireCodec.WriteReceipt(new WorkerReceipt(1, "start-line", "B10", "C1", run, "C1", 1,
            PhaseIdentity.StartLineJobName(run, "C1", 1), "synthetic-start", identity, commit, 0, true, utc, utc,
            [B10FinalSweepHandler.Reference("verifier", start, objects[start]), B10FinalSweepHandler.Reference("verifier", inventory, objects[inventory])], null, null));
        var options = new VerifierHostOptions
        {
            RunId = run, ScenarioId = "B10", ConfigurationId = "C1", Phase = "C1", VerifierCommit = commit, SourceCommit = commit,
            MaxPartitions = maximum, MaxArtifactBytes = 16777216, MaxTotalInputBytes = 16777216, MaxInputDepth = 32,
            MaxRowBytes = 65536, MaxRowsPerArtifact = 10000, MaxInputEntries = 100000, MaxDiagnosticBytes = 1024,
            MaxOutputBytes = 16384, MaxDiagnosticsPerCheck = 100
        };
        var limits = new InputLimits(32, 16777216, 16777216, 65536, 10000, 100000, 1024);
        var reader = new B10FinalRepeatEvidence(options, limits, new InputBudget(limits),
            (name, _) => Task.FromResult(objects.GetValueOrDefault(name)),
            (_, _, _) => Task.FromResult<byte[]?>(null), (_, _) => Task.FromResult(new BlobListing([], 1, true)));
        VerifierHostOptions repeatOptions = reader.RepeatOptions(1);
        Assert.Equal(maximum, repeatOptions.MaxPartitions);
        Assert.Equal(100000, repeatOptions.MaxInventoryEntries);
        Assert.Equal(16777216, repeatOptions.MaxNativeReportBytes);
        Assert.Equal(100000, repeatOptions.MaxBrokerEntries);
        var input = new B10FinalInput(1);
        await reader.ReadAsync(input, TestContext.Current.CancellationToken);
        Assert.True(input.Started);
        Assert.Equal(accepted, input.Start is not null);
        Assert.Equal(accepted, input.Starts is not null);
        if (accepted) Assert.Equal(3, input.Starts!.Count);
    }
}
#pragma warning restore CA1707
