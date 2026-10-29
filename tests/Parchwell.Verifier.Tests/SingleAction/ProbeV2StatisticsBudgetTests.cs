using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Testing;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2StatisticsBudgetTests
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

    [Theory]
    [InlineData(0)][InlineData(262143)][InlineData(262144)][InlineData(262145)]
    public void Given_ReportByteBoundary_When_Judged_Then_EmptyIsUnusableAndOnlyOversizeIsLimit(int size)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        string raw = size == 0 ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64));
        if (size != 0) raw += new string(' ',size - Encoding.UTF8.GetByteCount(raw));
        row = row with { Statistics = [row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) }] };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(size == 0 || size > 262144 ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(size > 262144,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(size == 0 || size > 262144,result.Reasons.Contains("v2_statistics",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("identity")][InlineData("sequence")][InlineData("time")][InlineData("wire")]
    public void Given_LargeReportWithIndependentDefect_When_Judged_Then_IncreasedBudgetDoesNotMaskExistingChecks(string defect)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        string raw = Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64));
        raw += new string(' ',131072 - Encoding.UTF8.GetByteCount(raw));
        var statistic = row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) };
        row = row with { Statistics = [statistic] };
        row = defect switch
        {
            "identity" => row with { ClientId = "unrelated-client" },
            "sequence" => row with { Statistics = [statistic with { Sequence = -1 }] },
            "time" => row with { TerminalUnixMs = 1001 },
            _ => row with { Statistics = [statistic with { RawBase64 = "not base64!" }] }
        };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(defect == "time" ? "incomplete" : "invalid",result.Status);
        Assert.Contains(defect == "identity" ? "v2_identity" : defect == "time" ? "v2_statistics" : "v2_wire",result.Reasons);
    }
}
