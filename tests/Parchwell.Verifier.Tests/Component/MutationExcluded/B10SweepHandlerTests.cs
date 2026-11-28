// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707

public sealed partial class B10SweepHandlerTests
{
    [Theory]
    [InlineData("C0","complete")] [InlineData("C1","complete")]
    [InlineData("C0","record-count-minus-one")] [InlineData("C1","record-count-minus-one")]
    [InlineData("C0","actual-byte-count-minus-one")] [InlineData("C1","actual-byte-count-minus-one")]
    public async Task Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(
        string phase,string boundary)
    {
        using PhaseFixture fixture=await PhaseFixture.Create(phase,fullPhaseVolume:true);
        Dictionary<string,byte[]> originals=fixture.Verifier.Where(p=>p.Key.EndsWith("/result.json",StringComparison.Ordinal))
            .ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        long expectedBytes=fixture.ExpectedCapacityConsumedBytes();
        Assert.Equal(212505000,expectedBytes);
        Assert.Equal(9300000,fixture.Options.MaxConsumedRecords);
        Assert.Equal(429496729600,fixture.Options.MaxConsumedBytes);
        Assert.True(expectedBytes<fixture.Options.MaxConsumedBytes!.Value);
        if(boundary=="record-count-minus-one")fixture.Options.MaxConsumedRecords=2324999;
        if(boundary=="actual-byte-count-minus-one")fixture.Options.MaxConsumedBytes=expectedBytes-1;

        int exit=await fixture.Run(TestContext.Current.CancellationToken);

        Assert.Contains(fixture.MetadataObjects.Where(p=>p.Key.EndsWith("capture.json",StringComparison.Ordinal)), p=>
        {
            using JsonDocument capture=JsonDocument.Parse(p.Value);
            return !capture.RootElement.GetProperty("complete").GetBoolean() &&
                capture.RootElement.GetProperty("gaps").EnumerateArray().Any(g=>g.GetProperty("code").GetString()=="row_limit");
        });
        Assert.Equal(2325000,fixture.RecordsRead);
        Assert.Equal(expectedBytes,fixture.ConsumedBytes);
        Assert.Equal(5,fixture.Assignments);
        Assert.Equal(["admin-dispose","consumer-dispose","create"],fixture.Events);
        Assert.Equal(PhasePaths.B10Finalization(fixture.Options.RunId!,phase),Assert.Single(fixture.Created));
        foreach((string path,byte[] bytes) in originals)Assert.Equal(bytes,fixture.Verifier[path]);
        B10Finalization finalization=fixture.Finalization();
        if(boundary=="complete")
        {
            Assert.Equal(0,exit);
            Assert.True(finalization.ScanComplete);
            Assert.Equal(B10SweepEvidence.ResidualRisk,finalization.ResidualRisk);
            Assert.Equal(Enumerable.Range(1,5),finalization.Repeats.Select(r=>r.Repeat));
            Assert.All(finalization.Repeats,r=>
            {
                Assert.True(r.ClassificationFinal);
                Assert.Equal(VerifierResultStatus.CompleteValid,r.Status);
                Assert.Equal(new OutcomeCounts(0,0,1,1,92999,0),r.Counts);
            });
        }
        else
        {
            Assert.Equal(1,exit);
            AssertFailedScan(finalization);
        }
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
        fixture.Completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(fixture.Completion.TryGetBlobReceipt(out string container,out string name,out byte[] receiptBytes));
        Assert.Equal("verifier",container);
        Assert.Equal(PhasePaths.B10SweepCompletion(fixture.Options.RunId!,phase),name);
        WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(receiptBytes);
        Assert.Equal("b10-sweep",receipt.Mode);
        Assert.True(receipt.CleanupCompleted);
        Assert.Equal(exit,receipt.HandlerExitCode);
        Assert.Equal(Reference(fixture.Created[0],fixture.Verifier[fixture.Created[0]]),Assert.Single(receipt.Artifacts));
        TestContext.Current.TestOutputHelper?.WriteLine($"Synthetic full-phase volume: unique_tail_records=465000; reads={fixture.RecordsRead}; bytes={fixture.ConsumedBytes}; boundary={boundary}");
    }
}
