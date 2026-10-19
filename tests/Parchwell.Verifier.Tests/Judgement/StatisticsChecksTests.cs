using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Repository Given_When_Then naming convention.
public sealed class StatisticsChecksTests
{
    [Fact]
    public void Given_SyntheticBase_When_Evaluated_Then_AllStatisticsChecksPass()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        foreach (int check in Enumerable.Range(17, 10)) AssertCheck(request, check, true);
    }

    [Theory]
    [InlineData("statistics", "missing_input")]
    [InlineData("statistics", "malformed_json")]
    [InlineData("lifecycle", "missing_input")]
    [InlineData("lifecycle", "malformed_json")]
    [InlineData("capabilities", "missing_input")]
    [InlineData("capabilities", "malformed_json")]
    public void Given_CapturedArtifactUnavailable_When_Evaluated_Then_Check17Fails(string artifact, string code)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        var problem = new StructuralProblem(17, code, artifact, null, null, null);
        request = artifact switch
        {
            "statistics" => request with { Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(problem), Reports = [] },
            "lifecycle" => request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(problem) },
            _ => request with { Capabilities = ParseResult<CapturedCapabilities>.Failure(problem) }
        };
        AssertCheck(request, 17, false);
        Assert.Equal(code, StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 17).Diagnostics[0].Code);
        if (artifact == "statistics") AssertNotEvaluated(request, 18);
        if (artifact == "lifecycle") AssertCheck(request, 22, true);
        if (artifact == "capabilities") { AssertNotEvaluated(request, 19); AssertNotEvaluated(request, 25); }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Given_CaptureFaultFlag_When_Evaluated_Then_Check17UsesFlag(bool faulted, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = WithCapabilities(request, request.Capabilities.Value! with { IsFaulted = faulted });
        AssertCheck(request, 17, pass);
    }

    [Fact]
    public void Given_EmptyPresentStatisticsAndLifecycle_When_Evaluated_Then_OnlyReportsAreRequired()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), []);
        request = request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success([]) };
        AssertCheck(request, 17, true);
        AssertCheck(request, 18, false);
    }

    [Theory]
    [InlineData("role", false)]
    [InlineData("client", false)]
    [InlineData("both", false)]
    [InlineData("both", true)]
    public void Given_ForeignStatisticsLabels_When_Evaluated_Then_WholeArtifactStillPasses(string label, bool mixed)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        ReportAssociation[] reports = request.Reports.Select((report, index) => mixed && index == 0 ? report : report with
        {
            Envelope = report.Envelope with
            {
                StatisticsIdentity = report.Envelope.StatisticsIdentity with
                {
                    Role = label is "role" or "both" ? 999 : report.Envelope.StatisticsIdentity.Role,
                    ClientId = label is "client" or "both" ? "foreign-client" : report.Envelope.StatisticsIdentity.ClientId
                }
            }
        }).ToArray();
        request = WithReports(request, reports);
        foreach (int check in Enumerable.Range(18, 9)) AssertCheck(request, check, true);
        CheckAssessment identity = BookkeepingChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 3).Assessment;
        Assert.Equal(AssessmentDisposition.Pass, identity.Disposition);
        var count = BrokerCountRecorder.Record(request, StatisticsChecks.Evaluate(request).Select(result => result.Assessment).ToArray());
        Assert.Equal(1, count!.Maximum);
        Assert.Equal(1, count.FirstPostDrain);
        Assert.Equal("producer-1", count.ClientId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("boundary_missing")]
    [InlineData("boundary_null")]
    public void Given_IncompleteCompletion_When_Evaluated_Then_Check19Fails(string variation)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        Completion completion = request.Capabilities.Value!.StatisticsCompletion.Value!;
        ParsedField<Completion> value = variation switch
        {
            "missing" => ParsedField<Completion>.Missing(),
            "null" => ParsedField<Completion>.Null(),
            "false" => ParsedField<Completion>.FromValue(completion with { IsComplete = false }),
            "boundary_missing" => ParsedField<Completion>.FromValue(completion with { DrainBoundaryMicroseconds = ParsedField<long>.Missing() }),
            _ => ParsedField<Completion>.FromValue(completion with { DrainBoundaryMicroseconds = ParsedField<long>.Null() })
        };
        request = WithCapabilities(request, request.Capabilities.Value with { StatisticsCompletion = value });
        AssertCheck(request, 19, false);
        if (variation != "false") AssertNotEvaluated(request, 25);
    }

    [Fact]
    public void Given_ZeroBoundary_When_Evaluated_Then_BoundaryIsPresent()
    {
        RunJudgementRequest request = WithBoundary(RecordedEvidence.SyntheticRequest(), 0);
        AssertCheck(request, 19, true);
        AssertCheck(request, 25, true);
    }

    [Fact]
    public void Given_DiagnosticAndMalformedNative_When_Evaluated_Then_Check20RetainsBothFailures()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        ReportAssociation report = request.Reports[0];
        report = report with
        {
            Envelope = report.Envelope with { Diagnostics = [new("capture_problem", ParsedField<string>.Missing(), ParsedField<string>.FromValue("raw_json"))] },
            Native = ReadNative("{")
        };
        request = WithReports(request, [report, request.Reports[1], request.Reports[2]]);
        AssertCheck(request, 20, false);
        Assert.Equal(2, StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 20).Diagnostics.Count);
        Assert.Contains(StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 20).Diagnostics,
            diagnostic => diagnostic.Code == "malformed_json");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_DiagnosticOrMalformedNativeAlone_When_Evaluated_Then_Check20Fails(bool malformed)
    {
        ReportAssociation report = RecordedEvidence.Report(100, 50);
        report = malformed ? report with { Native = ReadNative("{") } : report with
        {
            Envelope = report.Envelope with { Diagnostics = [new("capture_problem", ParsedField<string>.Missing(), ParsedField<string>.Missing())] }
        };
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [report]), 20, false);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("nodeid")]
    [InlineData("source")]
    [InlineData("state")]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    [InlineData("disconnects")]
    [InlineData("throttle.cnt")]
    [InlineData("throttle.max")]
    [InlineData("rtt.cnt")]
    [InlineData("rtt.p99")]
    public void Given_RequiredNativeFieldOmitted_When_Evaluated_Then_Check21Fails(string field)
    {
        JsonNode node = JsonNode.Parse(NativeJson)!;
        RemoveField(node["brokers"]!["broker-a"]!, field);
        RunJudgementRequest request = WithNative(ReadNative(node.ToJsonString()));
        AssertCheck(request, 20, true);
        AssertCheck(request, 21, false);
        Assert.Contains(StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 21).Diagnostics,
            diagnostic => diagnostic.Code == "missing_required_field" && diagnostic.Field == "brokers.broker-a." + field);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("nodeid")]
    [InlineData("source")]
    [InlineData("state")]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    [InlineData("disconnects")]
    [InlineData("throttle")]
    [InlineData("rtt")]
    [InlineData("throttle.cnt")]
    [InlineData("throttle.max")]
    [InlineData("rtt.cnt")]
    [InlineData("rtt.p99")]
    public void Given_NativeFieldWrongKind_When_Evaluated_Then_Check20Fails(string field)
    {
        JsonNode node = JsonNode.Parse(NativeJson)!;
        JsonNode parent = node["brokers"]!["broker-a"]!;
        string[] parts = field.Split('.');
        if (parts.Length == 2) parent = parent[parts[0]]!;
        parent[parts[^1]] = true;
        AssertCheck(WithNative(ReadNative(node.ToJsonString())), 20, false);
    }

    [Fact]
    public void Given_OptionalNativeFieldsAbsentAndTypedDisagreement_When_Evaluated_Then_RawReportGoverns()
    {
        RunJudgementRequest request = WithNative(ReadNative(NativeJson));
        ReportAssociation report = request.Reports[0];
        report = report with { Envelope = report.Envelope with { TypedSnapshot = ParsedField<TypedSnapshot>.FromValue(
            new TypedSnapshot("different", "different", "consumer", -99, -99, -99, [])) } };
        request = WithReports(request, [report]);
        foreach (int check in Enumerable.Range(20, 7)) AssertCheck(request, check, true);
    }

    [Theory]
    [InlineData(99, 49, false)]
    [InlineData(100, 50, false)]
    [InlineData(101, 51, true)]
    [InlineData(101, 50, false)]
    [InlineData(101, 52, false)]
    public void Given_TimestampAndCreationBoundaries_When_Evaluated_Then_Check22UsesStrictOrderAndExactCreation(long ts, long age, bool pass) =>
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50), RecordedEvidence.Report(ts, age)]), 22, pass);

    [Theory]
    [InlineData(long.MinValue, long.MaxValue, long.MinValue + 1, long.MaxValue, false)]
    [InlineData(long.MinValue, 0, long.MaxValue, -1, false)]
    [InlineData(long.MaxValue - 1, long.MinValue, long.MaxValue, long.MinValue + 1, true)]
    public void Given_ExtremeNativeTimes_When_Evaluated_Then_Check22UsesInt128(long firstTs, long firstAge, long secondTs, long secondAge, bool pass) =>
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(firstTs, firstAge), RecordedEvidence.Report(secondTs, secondAge)]), 22, pass);

    [Fact]
    public void Given_ReversedCaptureOrder_When_Evaluated_Then_Check22DoesNotSort()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        AssertCheck(WithReports(request, request.Reports.Reverse().ToArray()), 22, false);
    }

    [Theory]
    [InlineData("req_timeouts", 4, false)] [InlineData("req_timeouts", 5, true)] [InlineData("req_timeouts", 6, true)]
    [InlineData("txretries", 4, false)] [InlineData("txretries", 5, true)] [InlineData("txretries", 6, true)]
    [InlineData("txerrs", 4, false)] [InlineData("txerrs", 5, true)] [InlineData("txerrs", 6, true)]
    [InlineData("rxerrs", 4, false)] [InlineData("rxerrs", 5, true)] [InlineData("rxerrs", 6, true)]
    [InlineData("disconnects", 4, false)] [InlineData("disconnects", 5, true)] [InlineData("disconnects", 6, true)]
    public void Given_EachCounterBoundary_When_Evaluated_Then_Check23RejectsOnlyDecrease(string field, long next, bool pass) =>
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, [Counter(RecordedEvidence.Broker(), field, 5)]),
            RecordedEvidence.Report(100, 50, [Counter(RecordedEvidence.Broker(), field, next)])]), 23, pass);

    [Theory]
    [InlineData(4, false)] [InlineData(5, true)] [InlineData(6, true)]
    public void Given_BrokerAbsentThenRenamed_When_Evaluated_Then_Check23PreservesDictionaryKeyHistory(long next, bool pass) =>
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, [Counter(RecordedEvidence.Broker(), "disconnects", 5)]),
            RecordedEvidence.Report(100, 50, []),
            RecordedEvidence.Report(110, 60, [Counter(RecordedEvidence.Broker() with { Name = ParsedField<string>.FromValue("renamed") }, "disconnects", next)])]), 23, pass);

    [Fact]
    public void Given_DifferentKeysWithSameNativeName_When_Evaluated_Then_HistoriesRemainIndependent()
    {
        NativeBrokerReport a = Counter(RecordedEvidence.Broker("broker-a"), "disconnects", 5);
        NativeBrokerReport b = Counter(RecordedEvidence.Broker("broker-b") with { Name = a.Name }, "disconnects", 1);
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(90, 40, [a, b]), RecordedEvidence.Report(100, 50, [a, b])]);
        request = request with { ExpectedBrokerNames = ["broker-a", "broker-b"] };
        foreach (int check in Enumerable.Range(23, 4)) AssertCheck(request, check, true);
    }

    [Theory]
    [InlineData("broker-a", false, "", true)]
    [InlineData("broker-z", false, "", false)]
    [InlineData("Broker-a", false, "", false)]
    [InlineData("broker-a", true, "", false)]
    [InlineData("broker-a", false, "broker-z", true)]
    [InlineData("broker-a", true, "broker-z", false)]
    [InlineData("", false, "broker-z", true)]
    public void Given_IndependentExpectedKeysAndMissingList_When_Evaluated_Then_Check24UsesOnlyExpectedDictionaryKeys(string expected, bool missingExpected, string extraMissing, bool pass)
    {
        NativeBrokerReport broker = RecordedEvidence.Broker() with { Name = ParsedField<string>.FromValue("broker-z") };
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [broker])]);
        request = request with { ExpectedBrokerNames = expected.Length == 0 ? [] : [expected] };
        var missing = new List<string>();
        if (missingExpected) missing.Add(expected);
        if (extraMissing.Length != 0) missing.Add(extraMissing);
        request = WithCompletion(request, request.Capabilities.Value!.StatisticsCompletion.Value! with { MissingBrokers = missing });
        AssertCheck(request, 24, pass);
    }

    [Fact]
    public void Given_ExpectedBrokerAbsentEverywhere_When_Evaluated_Then_Check24Fails()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [])]);
        AssertCheck(request, 24, false);
    }

    [Fact]
    public void Given_ExtraObservedKeyAndFalseObservedBrokerClaim_When_Evaluated_Then_IndependentExpectedKeysGovernCheck24()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50,
            [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        request = WithCapabilities(request, request.Capabilities.Value! with { ObservedBrokerNames = ["unrelated-claim"] });
        AssertCheck(request, 24, true);
    }

    [Theory]
    [InlineData(99, false)] [InlineData(100, true)] [InlineData(101, true)]
    public void Given_PostDrainBoundary_When_Evaluated_Then_Check25IncludesEquality(long timestamp, bool pass) =>
        AssertCheck(WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(timestamp, timestamp - 50)]), 25, pass);

    [Theory]
    [InlineData("late_add", false, true)]
    [InlineData("remove", false, false)]
    [InlineData("replace", false, false)]
    [InlineData("late_add_remove", false, true)]
    [InlineData("state", true, true)]
    [InlineData("native_name", true, true)]
    [InlineData("key", false, false)]
    [InlineData("internal", true, true)]
    [InlineData("configured", true, false)]
    [InlineData("logical", true, false)]
    public void Given_BrokerPopulationChanges_When_Evaluated_Then_CoverageAndPrefixStabilityStayDistinct(string variation, bool coverage, bool stable)
    {
        NativeBrokerReport a = RecordedEvidence.Broker("broker-a");
        NativeBrokerReport b = RecordedEvidence.Broker("broker-b");
        NativeBrokerReport[] first = variation == "remove" ? [a, b] : [a];
        NativeBrokerReport[] middle = variation switch
        {
            "remove" => [a], "replace" => [b], "key" => [b with { Name = a.Name }],
            "state" => [a with { State = ParsedField<string>.FromValue("DOWN") }],
            "native_name" => [a with { Name = ParsedField<string>.FromValue("different") }],
            "internal" => [a, b with { Source = ParsedField<string>.FromValue("internal") }],
            "configured" => [a, b with { Source = ParsedField<string>.FromValue("configured") }],
            "logical" => [a, b with { Source = ParsedField<string>.FromValue("logical") }], _ => [a]
        };
        var reports = new List<ReportAssociation> { RecordedEvidence.Report(90, 40, first), RecordedEvidence.Report(100, 50, middle) };
        if (variation is "late_add" or "late_add_remove") reports.Add(RecordedEvidence.Report(110, 60, [a, b]));
        if (variation == "late_add_remove") reports.Add(RecordedEvidence.Report(120, 70, [a]));
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), reports);
        AssertCheck(request, 25, coverage);
        AssertCheck(request, 26, stable);
    }

    [Fact]
    public void Given_EqualBoundaryReportMissingLaterBroker_When_Evaluated_Then_FirstQualifyingReportIsSelected()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40), RecordedEvidence.Report(100, 50),
            RecordedEvidence.Report(101, 51, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        AssertCheck(request, 25, false);
        Assert.Equal(2, StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 25).Diagnostics[0].Row);
        AssertCheck(request, 26, true);
    }

    [Fact]
    public void Given_ReadableCounterDecreaseAndUnreadableOtherReport_When_Evaluated_Then_WitnessedFailureWins()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, [Counter(RecordedEvidence.Broker(), "disconnects", 5)]),
            RecordedEvidence.Report(100, 50) with { Native = ReadNative("{") },
            RecordedEvidence.Report(110, 60, [Counter(RecordedEvidence.Broker(), "disconnects", 4)])]);
        AssertCheck(request, 20, false);
        AssertCheck(request, 23, false);
        AssertNotEvaluated(request, 22);
    }

    [Fact]
    public void Given_PersistedEnvelopesWithoutNativeAssociations_When_Evaluated_Then_StatisticsPredicatesRemainUnevaluated()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest() with { Reports = [] };
        AssertCheck(request, 18, true);
        foreach (int check in Enumerable.Range(20, 7)) AssertNotEvaluated(request, check);
    }

    [Fact]
    public void Given_UnreadableReportAfterSelectedBoundary_When_Evaluated_Then_PrefixStabilityAndKnownCoverageRemainAvailable()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = WithReports(request, [request.Reports[0], request.Reports[1], request.Reports[2] with { Native = ReadNative("{") }]);
        AssertCheck(request, 24, true);
        AssertCheck(request, 26, true);
        AssertNotEvaluated(request, 25);
    }

    [Fact]
    public void Given_BrokerChurnBeforeUnreadableTimestamp_When_Evaluated_Then_Check26RetainsWitnessedFailure()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40),
            RecordedEvidence.Report(95, 45, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")]),
            RecordedEvidence.Report(100, 50) with { Native = ReadNative("{") },
            RecordedEvidence.Report(110, 60, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        AssertCheck(request, 26, false);
        AssertNotEvaluated(request, 25);
        ResultDiagnostic diagnostic = StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == 26).Diagnostics[0];
        Assert.Equal(2, diagnostic.Row);
        Assert.Equal("brokers", diagnostic.Field);
    }

    [Fact]
    public void Given_StablePrefixAndChurnOnlyAfterUnreadableTimestamp_When_Evaluated_Then_Check26IsUnevaluated()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40), RecordedEvidence.Report(95, 45),
            RecordedEvidence.Report(100, 50) with { Native = ReadNative("{") },
            RecordedEvidence.Report(110, 60, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        AssertNotEvaluated(request, 26);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_MissingSourceAndDefiniteKeyAdditionOrRemoval_When_Evaluated_Then_Check26Fails(bool removal)
    {
        NativeBrokerReport a = RecordedEvidence.Broker();
        NativeBrokerReport b = RecordedEvidence.Broker("broker-b");
        NativeBrokerReport uncertain = a with { Source = ParsedField<string>.Missing() };
        NativeBrokerReport[] before = removal ? [a, b] : [a];
        NativeBrokerReport[] after = removal ? [uncertain] : [uncertain, b];
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, before), RecordedEvidence.Report(100, 50, after)]);
        AssertCheck(request, 21, false);
        AssertCheck(request, 26, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_OnlyAmbiguousSourceKeyAdditionOrRemoval_When_Evaluated_Then_Check26DoesNotInventAChange(bool removal)
    {
        NativeBrokerReport a = RecordedEvidence.Broker();
        NativeBrokerReport uncertain = RecordedEvidence.Broker("broker-b") with { Source = ParsedField<string>.Missing() };
        NativeBrokerReport[] before = removal ? [a, uncertain] : [a];
        NativeBrokerReport[] after = removal ? [a] : [a, uncertain];
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, before), RecordedEvidence.Report(100, 50, after)]);
        AssertCheck(request, 21, false);
        AssertNotEvaluated(request, 26);
    }

    [Fact]
    public void Given_PreBoundaryChurnWithoutPostDrainReport_When_Evaluated_Then_Check26RetainsWitnessedFailure()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40),
            RecordedEvidence.Report(95, 45, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        AssertCheck(request, 25, false);
        AssertCheck(request, 26, false);
    }

    [Fact]
    public void Given_ChurnWithoutDrainBoundary_When_Evaluated_Then_Check26HasNoDefinedPrefix()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40),
            RecordedEvidence.Report(100, 50, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        request = WithCompletion(request, request.Capabilities.Value!.StatisticsCompletion.Value! with
        {
            DrainBoundaryMicroseconds = ParsedField<long>.Missing()
        });
        AssertCheck(request, 19, false);
        AssertNotEvaluated(request, 26);
    }

    [Fact]
    public void Given_SyntheticUnknownTimestampWithDefiniteChurn_When_Evaluated_Then_UnknownEndpointReportIsIncluded()
    {
        ReportAssociation uncertain = RecordedEvidence.Report(100, 50, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")]);
        uncertain = uncertain with { Native = ParseResult<NativeClientReport>.Success(uncertain.Native.Value! with
        {
            TimestampMicroseconds = ParsedField<long>.Missing()
        }) };
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40), uncertain,
            RecordedEvidence.Report(110, 60, [RecordedEvidence.Broker(), RecordedEvidence.Broker("broker-b")])]);
        AssertCheck(request, 26, false);
        AssertNotEvaluated(request, 25);
    }

    private static readonly string NativeJson = """
        {"ts":100,"age":50,"brokers":{"broker-a":{"name":"broker-a","nodeid":1,"source":"learned","state":"UP","req_timeouts":0,"txretries":0,"txerrs":0,"rxerrs":0,"disconnects":0,"throttle":{"cnt":0,"max":0},"rtt":{"cnt":0,"p99":0}}}}
        """;

    private static ParseResult<NativeClientReport> ReadNative(string json) => NativeStatisticsReader.Read(
        Encoding.UTF8.GetBytes(json), "statistics.jsonl", RecordedEvidence.SmallLimits, new InputBudget(RecordedEvidence.SmallLimits), 4096, 16);

    private static void RemoveField(JsonNode node, string path)
    {
        string[] parts = path.Split('.');
        for (int index = 0; index < parts.Length - 1; index++) node = node[parts[index]]!;
        node.AsObject().Remove(parts[^1]);
    }

    private static RunJudgementRequest WithNative(ParseResult<NativeClientReport> native) =>
        WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50) with { Native = native }]);

    private static RunJudgementRequest WithReports(RunJudgementRequest request, IReadOnlyList<ReportAssociation> reports) =>
        request with { Reports = reports, Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(report => report.Envelope).ToArray()) };

    private static RunJudgementRequest WithCapabilities(RunJudgementRequest request, CapturedCapabilities capabilities) =>
        request with { Capabilities = ParseResult<CapturedCapabilities>.Success(capabilities) };

    private static RunJudgementRequest WithCompletion(RunJudgementRequest request, Completion completion) =>
        WithCapabilities(request, request.Capabilities.Value! with { StatisticsCompletion = ParsedField<Completion>.FromValue(completion) });

    private static RunJudgementRequest WithBoundary(RunJudgementRequest request, long boundary) =>
        WithCompletion(request, request.Capabilities.Value!.StatisticsCompletion.Value! with { DrainBoundaryMicroseconds = ParsedField<long>.FromValue(boundary) });

    private static NativeBrokerReport Counter(NativeBrokerReport broker, string field, long value) => field switch
    {
        "req_timeouts" => broker with { RequestTimeouts = ParsedField<long>.FromValue(value) },
        "txretries" => broker with { TransmissionRetries = ParsedField<long>.FromValue(value) },
        "txerrs" => broker with { TransmissionErrors = ParsedField<long>.FromValue(value) },
        "rxerrs" => broker with { ReceiveErrors = ParsedField<long>.FromValue(value) },
        _ => broker with { Disconnects = ParsedField<long>.FromValue(value) }
    };

    private static void AssertCheck(RunJudgementRequest request, int number, bool pass)
    {
        CheckAssessment assessment = StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == number).Assessment;
        Assert.Equal(number, assessment.CheckNumber);
        Assert.Equal(AssessmentEvaluation.Evaluated, assessment.Evaluation);
        Assert.Equal(pass ? AssessmentDisposition.Pass : AssessmentDisposition.Fail, assessment.Disposition);
        Assert.Equal(pass ? (AssessmentClassification?)null : AssessmentClassification.Invalid, assessment.Classification);
    }

    private static void AssertNotEvaluated(RunJudgementRequest request, int number)
    {
        CheckAssessment assessment = StatisticsChecks.Evaluate(request).Single(result => result.Assessment.CheckNumber == number).Assessment;
        Assert.Equal(number, assessment.CheckNumber);
        Assert.Equal(AssessmentEvaluation.NotEvaluated, assessment.Evaluation);
        Assert.Null(assessment.Disposition);
        Assert.Null(assessment.Classification);
    }
}
