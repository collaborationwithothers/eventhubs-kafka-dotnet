// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Testing;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed partial class ProbeV2StatisticsBudgetTests
{
    [Fact]
    public async Task Given_RealNativeThirtyTwoPartitionSixtyFourBrokerReport_When_Judged_Then_OldBudgetFailsAndRepairedBudgetPreservesHash()
    {
        var options = ProbeV2Fixture.Options();
        var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        var row = evidence.Capture!.Repeats[0];
        using var cluster = new ProbeV2MockCluster(64);
        cluster.CreateTopic("document-events",32);
        string raw = await cluster.CaptureProducerStatisticsAsync("document-events",row.ClientId!,TestContext.Current.CancellationToken);
        byte[] bytes = Encoding.UTF8.GetBytes(raw);
        Assert.InRange(bytes.Length,32769,262144);
        using var report = JsonDocument.Parse(bytes);
        Assert.Equal(64,report.RootElement.GetProperty("brokers").EnumerateObject().Count());
        Assert.Equal(33,report.RootElement.GetProperty("topics").GetProperty("document-events").GetProperty("partitions").EnumerateObject().Count());
        row = row with { Statistics = [new(3,11,Convert.ToBase64String(bytes))] };
        var repaired = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal("complete_valid",repaired.Status);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)),repaired.StatisticsReference!.RawSha256);
        options.Limits.MaxStatisticsReportBytes = 32768;
        var old = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal("incomplete",old.Status);
        Assert.Contains("v2_capture_limit",old.Reasons);
        Assert.Contains("v2_statistics",old.Reasons);
        Assert.Null(old.BrokerCount);
    }
}
