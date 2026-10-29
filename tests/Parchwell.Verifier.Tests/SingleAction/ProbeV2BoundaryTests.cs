using System.Globalization;
using System.Text;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707

namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2BoundaryTests
{
    [Theory]
    [InlineData("partition",-1)][InlineData("partition",0)][InlineData("partition",1)]
    [InlineData("offset",-1)][InlineData("offset",0)][InlineData("offset",1)]
    public void Given_RequestedLocationBoundary_When_Checked_Then_NegativeCoordinatesAreMalformed(string field,int value)
    {
        var (options,evidence,row) = Fixture("read");
        var request = field == "partition" ? row.Requested! with { Partition = value } : row.Requested! with { Offset = value };
        var record = value < 0 ? row.ReadObservation.Record! : field == "partition" ? row.ReadObservation.Record! with { Partition = value } : row.ReadObservation.Record! with { Offset = value };
        var result = Judge(options,evidence,row with { Requested = request,ReadObservation = row.ReadObservation with { Record = record } });
        Assert.Equal(value < 0 ? "invalid" : "complete_valid",result.Status);
        Assert.Equal(value < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0,"invalid")][InlineData(127,"complete_valid")][InlineData(128,"complete_valid")][InlineData(129,"incomplete")]
    public void Given_SettingsKeyCount_When_Checked_Then_ExhaustionIsIncompleteAndEmptyIsNotFidelity(int count,string status)
    {
        var (options,evidence,row) = Fixture();
        var settings = Enumerable.Range(0,count).ToDictionary(i => "k" + i.ToString(CultureInfo.InvariantCulture),_ => "v",StringComparer.Ordinal);
        var result = Judge(options,evidence,row with { Settings = settings },count == 0 ? row.Settings : settings);
        Assert.Equal(status,result.Status);
        Assert.Equal(count > 128,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(count == 0,result.Reasons.Contains("v2_configuration",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(16383)][InlineData(16384)][InlineData(16385)]
    public void Given_Utf8SettingsBytes_When_Checked_Then_CombinedKeyAndValueByteLimitApplies(int bytes)
    {
        var (options,evidence,row) = Fixture();
        var settings = new Dictionary<string,string>(StringComparer.Ordinal) { ["é"] = Utf8(bytes - 2) };
        Assert.Equal(bytes,settings.Sum(x => Encoding.UTF8.GetByteCount(x.Key) + Encoding.UTF8.GetByteCount(x.Value)));
        AssertLimit(Judge(options,evidence,row with { Settings = settings },settings),bytes > 16384);
    }

    [Theory]
    [InlineData(0)][InlineData(99)][InlineData(100)][InlineData(101)]
    public void Given_DiagnosticCount_When_Checked_Then_EmptyIsCompleteAndCapturedGapsAreIncomplete(int count)
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { Diagnostics = Enumerable.Repeat("capture_limit",count).ToArray() });
        AssertLimit(result,count != 0);
    }

    [Theory]
    [InlineData(0)][InlineData(2047)][InlineData(2048)][InlineData(2049)]
    public void Given_Utf8DiagnosticBytes_When_Checked_Then_CapturedGapNeverBecomesMalformed(int bytes)
    {
        var (options,evidence,row) = Fixture();
        string diagnostic = Utf8(bytes);
        Assert.Equal(bytes,Encoding.UTF8.GetByteCount(diagnostic));
        AssertLimit(Judge(options,evidence,row with { Diagnostics = [diagnostic] }),true);
    }

    [Theory]
    [InlineData(0)][InlineData(99)][InlineData(100)][InlineData(101)]
    public void Given_JobTotalDiagnostics_When_Checked_Then_LimitSpansAllFiveRepeats(int count)
    {
        var (options,evidence,_) = Fixture();
        var rows = evidence.Capture!.Repeats.Select((row,index) => row with
        { Diagnostics = Enumerable.Repeat("capture_limit",count / 5 + (index < count % 5 ? 1 : 0)).ToArray() }).ToArray();
        var problems = SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence with { Capture = evidence.Capture with { Repeats = rows } });
        Assert.Equal(count > 100 ? ["v2_capture_limit"] : Array.Empty<string>(),problems);
    }

    [Theory]
    [InlineData("key",0)][InlineData("key",4095)][InlineData("key",4096)][InlineData("key",4097)]
    [InlineData("value",0)][InlineData("value",4095)][InlineData("value",4096)][InlineData("value",4097)]
    public void Given_RecordPayloadBytes_When_Checked_Then_OnlyExceededRawByteLimitIsIncomplete(string field,int bytes)
    {
        var (options,evidence,row) = Fixture("read");
        string encoded = Convert.ToBase64String(new byte[bytes]);
        var record = row.ReadObservation.Record!;
        record = field == "key" ? record with { KeyBase64 = encoded } : record with { ValueBase64 = encoded };
        AssertLimit(Judge(options,evidence,row with { ReadObservation = row.ReadObservation with { Record = record } }),bytes > 4096);
    }

    [Theory]
    [InlineData("requested_key")][InlineData("requested_value")][InlineData("record_key")][InlineData("record_value")]
    public void Given_OversizedPayload_When_Aggregated_Then_ExhaustionCannotBecomeValidOrFalseMalformed(string field)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options);
        string stage = field.StartsWith("requested",StringComparison.Ordinal) ? "action" : "read";
        var value = evidence[("C0",stage)]; var row = value.Capture!.Repeats[0];
        string encoded = Convert.ToBase64String(new byte[4097]);
        row = field switch
        {
            "requested_key" => row with { Requested = row.Requested! with { KeyBase64 = encoded } },
            "requested_value" => row with { Requested = row.Requested! with { ValueBase64 = encoded } },
            "record_key" => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { KeyBase64 = encoded } } },
            _ => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = encoded } } }
        };
        evidence[("C0",stage)] = value with { Capture = value.Capture with { Repeats = [row,..value.Capture.Repeats.Skip(1)] } };
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("incomplete",result.Status); Assert.Null(result.Deterministic);
        Assert.Contains("v2_capture_limit",result.Repeats[0].Reasons);
        Assert.DoesNotContain("v2_wire",result.Repeats[0].Reasons);
        Assert.DoesNotContain("v2_configuration",result.Repeats[0].Reasons);
        Assert.DoesNotContain("v2_location",result.Repeats[0].Reasons);
    }

    [Theory]
    [InlineData("requested")][InlineData("record")][InlineData("statistics")]
    public void Given_MalformedBase64_When_Checked_Then_RemainsInvalidWire(string field)
    {
        var (options,evidence,row) = Fixture("read");
        row = field switch
        {
            "requested" => row with { Requested = row.Requested! with { KeyBase64 = "!" } },
            "record" => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { KeyBase64 = "!" } } },
            _ => row with { Statistics = [row.Statistics[0] with { RawBase64 = "!" }] }
        };
        var result = Judge(options,evidence,row);
        Assert.Equal("invalid",result.Status); Assert.Contains("v2_wire",result.Reasons); Assert.DoesNotContain("v2_capture_limit",result.Reasons);
    }

    [Theory]
    [InlineData(-1,"invalid")][InlineData(0,"complete_valid")][InlineData(1,"complete_valid")]
    [InlineData(4095,"complete_valid")][InlineData(4096,"complete_valid")][InlineData(4097,"incomplete")]
    public void Given_RecordsSeen_When_Checked_Then_NegativeIsMalformedAndExhaustionIsIncomplete(int count,string status)
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { ReadObservation = new(count,0,null) });
        Assert.Equal(status,result.Status);
        Assert.Equal(count > 4096,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(count < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(-1,"invalid")][InlineData(0,"complete_valid")][InlineData(1,"complete_valid")]
    public void Given_EofCount_When_Checked_Then_OnlyNonnegativeCountsAreWellFormed(int count,string status)
    {
        var (options,evidence,row) = Fixture();
        Assert.Equal(status,Judge(options,evidence,row with { ReadObservation = new(0,count,null) }).Status);
    }

    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,true)]
    [InlineData(19999,true)][InlineData(20000,true)][InlineData(20001,false)]
    public void Given_PostterminalStatisticsTime_When_Checked_Then_BothWindowEdgesAreInclusive(long delta,bool valid)
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { Statistics = [row.Statistics[0] with { ObservedElapsedMs = row.ElapsedMs + delta }] });
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(valid ? Array.Empty<string>() : ["v2_statistics"],result.Reasons);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)]
    public void Given_StatisticsReportCount_When_Checked_Then_OnlyOneIsCompleteAndExcessIsCaptureLimit(int count)
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { Statistics = Enumerable.Repeat(row.Statistics[0],count).ToArray() });
        Assert.Equal(count == 1 ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(count > 1,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(count != 1,result.Reasons.Contains("v2_statistics",StringComparer.Ordinal));
        Assert.DoesNotContain("v2_wire",result.Reasons);
    }

    [Theory]
    [InlineData("callback",0)][InlineData("callback",1023)][InlineData("callback",1024)][InlineData("callback",1025)]
    [InlineData("terminal",0)][InlineData("terminal",1023)][InlineData("terminal",1024)][InlineData("terminal",1025)]
    [InlineData("delivery",0)][InlineData("delivery",1023)][InlineData("delivery",1024)][InlineData("delivery",1025)]
    public void Given_Utf8ErrorReasonBytes_When_Checked_Then_EachErrorLocationUsesTheSameCaptureLimit(string location,int bytes)
    {
        var (options,evidence,row) = Fixture();
        var error = new ProbeV2Error(-1,false,"local",Utf8(bytes),10,1);
        row = location switch
        {
            "callback" => row with { ClientErrors = [error] },
            "terminal" => row with { Result = "error",Error = error,Delivery = null },
            _ => row with { Result = "error",Error = error,Delivery = row.Delivery! with { Error = error } }
        };
        AssertLimit(Judge(options,evidence,row),bytes > 1024);
    }

    [Theory]
    [InlineData("code")][InlineData("sequence")][InlineData("observed")][InlineData("origin")]
    public void Given_MalformedErrorAndExceededCount_When_Checked_Then_IndependentShapeFailureStaysInvalid(string field)
    {
        var (options,evidence,row) = Fixture(); var error = new ProbeV2Error(-1,false,"local","",0,0);
        error = field switch { "code" => error with { Code = 0 },"sequence" => error with { Sequence = -1 },"observed" => error with { ObservedElapsedMs = -1 },_ => error with { Origin = "" } };
        var result = Judge(options,evidence,row with { ClientErrors = Enumerable.Repeat(error,9).ToArray() });
        Assert.Equal("invalid",result.Status); Assert.Contains("v2_wire",result.Reasons); Assert.Contains("v2_capture_limit",result.Reasons);
    }

    [Theory]
    [InlineData("statistics")][InlineData("metadata")][InlineData("errors")][InlineData("omissions")]
    public void Given_EmptyInput_When_Checked_Then_RequiredAndOptionalEvidenceRemainDistinct(string field)
    {
        var (options,evidence,row) = Fixture();
        row = field switch { "statistics" => row with { Statistics = [] },"metadata" => row with { MetadataPartitions = [] },"errors" => row with { ClientErrors = [] },_ => row with { OmittedKeys = [] } };
        var result = Judge(options,evidence,row);
        Assert.Equal(field == "statistics" ? "incomplete" : field == "metadata" ? "invalid" : "complete_valid",result.Status);
        Assert.DoesNotContain("v2_capture_limit",result.Reasons);
    }

    [Theory]
    [InlineData(0,"invalid")][InlineData(1,"complete_valid")][InlineData(2,"complete_valid")]
    public void Given_MatchingRecordCount_When_Checked_Then_RecordRequiresAtLeastOneSeen(int count,string status)
    {
        var (options,evidence,row) = Fixture("read");
        Assert.Equal(status,Judge(options,evidence,row with { ReadObservation = row.ReadObservation with { RecordsSeen = count } }).Status);
    }

    [Theory]
    [InlineData("time",-1)][InlineData("time",0)][InlineData("time",1)]
    [InlineData("ts",-1)][InlineData("ts",0)][InlineData("ts",1)]
    public void Given_NativeStatisticIntegers_When_Checked_Then_NonnegativeValuesAreRequired(string field,long value)
    {
        var (options,evidence,row) = Fixture();
        row = row with { TerminalUnixMs = 0,Statistics = [ProbeV2Fixture.Statistic(row.ClientId!,row.Action,field == "time" ? value : 0,11,ts: field == "ts" ? value : 0)] };
        var result = Judge(options,evidence,row);
        Assert.Equal(value < 0 ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(value < 0 ? ["v2_statistics"] : Array.Empty<string>(),result.Reasons);
    }

    [Theory]
    [InlineData(0,"invalid")][InlineData(1,"invalid")][InlineData(2,"complete_valid")]
    public void Given_StatisticsSequenceAroundTerminal_When_Checked_Then_ReportMustFollowTerminal(long sequence,string status)
    {
        var (options,evidence,row) = Fixture();
        Assert.Equal(status,Judge(options,evidence,row with { Statistics = [row.Statistics[0] with { Sequence = sequence }] }).Status);
    }

    [Theory]
    [InlineData(null)][InlineData("")]
    public void Given_NullOrEmptyRecordValue_When_Checked_Then_TombstoneAndEmptyRemainDistinct(string? value)
    {
        var (options,evidence,row) = Fixture("read");
        var result = Judge(options,evidence,row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = value } } });
        Assert.Equal("complete_valid",result.Status);
        Assert.Equal(value is null ? "null" : "empty",result.SemanticOutcome.ValueKind);
    }

    [Fact]
    public void Given_EmptyNativeStatistics_When_Checked_Then_MissingMeasurementIsIncomplete()
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { Statistics = [row.Statistics[0] with { RawBase64 = "" }] });
        Assert.Equal("incomplete",result.Status); Assert.Equal(["v2_statistics"],result.Reasons);
    }

    [Theory]
    [InlineData(0)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Given_RepeatCollectionCount_When_Checked_Then_ExactlyFiveOrderedSlotsAreRequired(int count)
    {
        var (options,evidence,_) = Fixture();
        var rows = Enumerable.Range(1,count).Select(i => ProbeV2Fixture.Row(options,"C0","action",i)).ToArray();
        var result = SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence with { Capture = evidence.Capture! with { Repeats = rows } });
        Assert.Equal(count == 5 ? Array.Empty<string>() : ["v2_wire"],result);
    }

    [Theory]
    [InlineData("observed",-1,false)][InlineData("observed",0,false)][InlineData("observed",1,false)]
    [InlineData("sequence",-1,false)][InlineData("sequence",0,false)][InlineData("sequence",1,false)]
    [InlineData("observed",-1,true)][InlineData("observed",0,true)][InlineData("observed",1,true)]
    [InlineData("sequence",-1,true)][InlineData("sequence",0,true)][InlineData("sequence",1,true)]
    public void Given_OuterStatisticsNumericFields_When_Checked_Then_NegativesAreMalformedEvenWithoutTerminal(string field,long value,bool terminal)
    {
        var (options,evidence,row) = Fixture();
        if (!terminal) row = row with { Terminal = false,TerminalUnixMs = null,Result = null };
        var report = row.Statistics[0];
        report = field == "observed" ? report with { ObservedElapsedMs = value } : report with { Sequence = value };
        var result = Judge(options,evidence,row with { Statistics = [report] });
        Assert.Equal(value < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
        Assert.Equal(value < 0 || terminal && field == "sequence" ? "invalid" : "incomplete",result.Status);
        Assert.DoesNotContain("v2_capture_limit",result.Reasons);
        if (!terminal || field == "observed") Assert.Contains("v2_statistics",result.Reasons);
    }

    private static string Utf8(int bytes) => new string('é',bytes / 2) + (bytes % 2 == 0 ? "" : "x");
    private static (ProbeV2Options Options,ProbeV2Evidence Evidence,ProbeV2Repeat Row) Fixture(string stage = "action")
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0",stage);
        return (options,evidence,evidence.Capture!.Repeats[0]);
    }
    private static ProbeV2RepeatProjection Judge(ProbeV2Options options,ProbeV2Evidence evidence,ProbeV2Repeat row,IReadOnlyDictionary<string,string>? expected = null) =>
        SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,expected ?? row.Settings,[]);
    private static void AssertLimit(ProbeV2RepeatProjection result,bool limited)
    {
        Assert.Equal(limited ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(limited ? ["v2_capture_limit"] : Array.Empty<string>(),result.Reasons);
    }
}
