using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.LoadGen.Tests.SingleActionV2;

public sealed class ProbeV2StatisticsBudgetTests
{
    [Theory]
    [InlineData(0)][InlineData(262143)][InlineData(262144)][InlineData(262145)]
    public void Given_StatisticsByteBoundary_When_Captured_Then_OnlyExcessIsRejectedWithSpecificSafeSize(int bytes)
    {
        var capture = Terminal();
        string raw = new('s', bytes);
        capture.Statistics(raw);
        var row = Snapshot(capture);
        Assert.Equal(bytes > 262144, capture.Limit);
        if (bytes <= 262144)
            Assert.Equal(raw, Encoding.UTF8.GetString(Convert.FromBase64String(Assert.Single(row.Statistics).RawBase64)));
        else
        {
            Assert.Empty(row.Statistics);
            Assert.Equal(["capture_limit", "capture_rejected:MaxStatisticsReportBytes:observed=262145:allowed=262144"], row.Diagnostics);
            Assert.Equal("success", row.Result);
            Assert.Equal(9, row.Delivery!.Offset);
            capture.Statistics("later-small-report");
            Assert.Empty(Snapshot(capture).Statistics);
        }
    }

    [Fact]
    public void Given_MultibyteStatistics_When_Captured_Then_BoundCountsUtf8Bytes()
    {
        var capture = Terminal();
        capture.Statistics(new string('é', 131073));
        Assert.Contains("capture_rejected:MaxStatisticsReportBytes:observed=262146:allowed=262144", Snapshot(capture).Diagnostics);
    }

    [Fact]
    public void Given_InvalidUtf16_When_EncodingStatistics_Then_RejectionIsDistinctFromSizeAndRetainsNoPayload()
    {
        var capture = Terminal();
        capture.Statistics("private-synthetic-identity\ud800");
        Assert.Equal(["capture_limit", "capture_rejected:StatisticsUtf8"], Snapshot(capture).Diagnostics);
        Assert.Empty(Snapshot(capture).Statistics);
    }

    [Fact]
    public void Given_ExhaustedSharedDiagnostics_When_ReportIsRejected_Then_FirstSpecificCauseSurvivesWithinBudget()
    {
        var budget = new ProbeV2DiagnosticBudget(100);
        var first = Terminal(budget);
        for (int index = 0; index < 100; index++) first.Diagnostic("synthetic-" + index);
        var rows = new List<ProbeV2Repeat>();
        for (int index = 0; index < 5; index++)
        {
            var capture = Terminal(budget);
            capture.Statistics(new string('x', 262145));
            capture.Error(new Error(ErrorCode.Local_Transport, new string('x', 1025)));
            rows.Add(Snapshot(capture));
        }
        Assert.Equal(100, Snapshot(first).Diagnostics.Count + rows.Sum(row => row.Diagnostics.Count));
        Assert.All(rows, row => Assert.Equal(["capture_limit", "capture_rejected:MaxStatisticsReportBytes:observed=262145:allowed=262144"], row.Diagnostics));
    }

    [Theory]
    [InlineData(2097151)][InlineData(2097152)][InlineData(2097153)]
    public void Given_SerializedCaptureBoundary_When_Encoded_Then_ExcessCarriesNumericRejectionEvidence(int size)
    {
        int emptySize = ProbeV2Wire.Encode("", int.MaxValue).Length;
        string value = new('x', size - emptySize);
        if (size <= 2097152) Assert.Equal(size, ProbeV2Wire.Encode(value, 2097152).Length);
        else
        {
            var rejection = Assert.Throws<InvalidDataException>(() => ProbeV2Wire.Encode(value, 2097152));
            Assert.Equal(size, rejection.Data["observed_bytes"]);
            Assert.Equal(2097152, rejection.Data["allowed_bytes"]);
        }
    }

    private static ProbeV2Capture Terminal(ProbeV2DiagnosticBudget? budget = null)
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), new TestClock(), 0, 330000, budget);
        capture.Delivery(new() { Topic = "document-events", Partition = 30, Offset = 9, Status = PersistenceStatus.Persisted, Error = new(ErrorCode.NoError) });
        return capture;
    }
    private static ProbeV2Repeat Snapshot(ProbeV2Capture capture) => capture.Snapshot(1,"pod-r1",ProbeV2Stage.For("B08","C0","action"),new Dictionary<string,string>(),[],Enumerable.Range(0,32).ToArray(),new("document-events","automatic",null,null,"aw==","dg=="));
}
