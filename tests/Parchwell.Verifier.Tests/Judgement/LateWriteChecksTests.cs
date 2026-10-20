using System.Text;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
/// <summary>Protects every independent late-write guard, including partially readable native reports.</summary>
public sealed class LateWriteChecksTests
{
    private static readonly string[] GuardCounters = ["req_timeouts", "txretries", "txerrs", "rxerrs"];
    [Theory]
    [InlineData("callback", false, 0, false)]
    [InlineData("callback", false, 9, false)]
    [InlineData("callback", true, 0, true)]
    [InlineData("callback", true, 9, true)]
    [InlineData("synchronous_rejection", false, 9, true)]
    public void Given_LedgerCompletion_When_Evaluated_Then_OnlyUndeliveredCallbacksFailTwelve(
        string source, bool delivered, int errorCode, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(
            [RecordedEvidence.LedgerRow(delivered: delivered, source: source) with { ErrorCode = errorCode }]) };
        AssertState(LateWriteChecks.Evaluate(request), 12, pass);
        Assert.Equal(pass ? [] : new[] { new ResultDiagnostic(12, "check_12_failed", "ledger.jsonl", 1, "delivered", null) },
            LateWriteChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 12).Diagnostics);
    }

    [Theory]
    [InlineData(null, "callback", false)]
    [InlineData(0, "callback", true)]
    [InlineData(1, "callback", false)]
    [InlineData(2, "callback", true)]
    [InlineData(-1, "callback", true)]
    [InlineData(int.MinValue, "callback", true)]
    [InlineData(int.MaxValue, "callback", true)]
    [InlineData(null, "synchronous_rejection", true)]
    public void Given_CallbackPersistenceValue_When_Evaluated_Then_OnlyNullOrPossiblyPersistedFailsThirteen(
        int? status, string source, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        LedgerRow row = RecordedEvidence.LedgerRow(source: source) with
        {
            PersistenceStatus = status is null ? ParsedField<int>.Null() : ParsedField<int>.FromValue(status.Value)
        };
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([row]) };
        AssertState(LateWriteChecks.Evaluate(request), 13, pass);
        Assert.Equal(pass ? [] : new[] { new ResultDiagnostic(13, "check_13_failed", "ledger.jsonl", 1, "persistence_status", null) },
            LateWriteChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 13).Diagnostics);
    }

    [Fact]
    public void Given_SyntheticSuccessfulDtoWithMissingPersistence_When_Evaluated_Then_ThirteenFails()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(
            [RecordedEvidence.LedgerRow() with { PersistenceStatus = ParsedField<int>.Missing() }]) };
        AssertState(LateWriteChecks.Evaluate(request), 13, false);
        Assert.Equal([new ResultDiagnostic(13, "check_13_failed", "ledger.jsonl", 1, "persistence_status", null)],
            LateWriteChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 13).Diagnostics);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_LiteralLedgerPersistenceOmittedOrNull_When_ParsedAndEvaluated_Then_OwningChecksRemainDistinct(bool omitted)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        string json = """
            {"schema_version":1,"identity":{"scenario_id":"A01","configuration_id":"C0","run_id":"run-1","instance_id":"instance-1","expected_instance_id":"instance-1","client_id":"producer-1"},"attempt":{"instance_id":"instance-1","attempt_id":"a1","ordinal":0,"tenant_id":"synthetic","document_id":"synthetic","key":"k","sequence":1,"event_type":"uploaded","planned_microseconds":0,"invoked_microseconds":0},"completion_source":"callback","delivered":true,"error_code":0,"error_name":null,"persistence_status":null,"persistence_provenance":"native_callback","completed_microseconds":1,"delivery_latency_ms":null,"synchronous_call_duration_ms":null}
            """;
        if (omitted) json = json.Replace("\"persistence_status\":null,", "", StringComparison.Ordinal);
        request = request with { Ledger = LedgerReader.ReadRows(Encoding.UTF8.GetBytes(json + "\n"),
            RecordedEvidence.SmallLimits, new(RecordedEvidence.SmallLimits)) };
        AssertState(BookkeepingChecks.Evaluate(request), 1, !omitted);
        AssertState(LateWriteChecks.Evaluate(request), 13, omitted ? null : false);
        Assert.Equal(omitted, request.Ledger.Value is null);
        if (omitted) Assert.Contains(request.Ledger.Problems, p => p.Field == "persistence_status" && p.CheckNumber == 1);
    }

    public static IEnumerable<object[]> MarkerCases()
    {
        string[] variants = ["", "broker_lifecycle_inventory_unproven", "request_lifecycle_capture_unsupported",
            "broker_lifecycle_inventory_unproven|request_lifecycle_capture_unsupported", "unknown",
            "broker_lifecycle_inventory_unproven|unknown", "BROKER_LIFECYCLE_INVENTORY_UNPROVEN"];
        foreach (string markers in variants)
            foreach (bool complete in new[] { false, true })
                yield return [markers, complete, complete && !markers.Contains("unknown", StringComparison.Ordinal) && !markers.Contains("BROKER", StringComparison.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(MarkerCases))]
    public void Given_ManifestMarkersAndCompleteness_When_Evaluated_Then_OnlyStandingMarkersAndTrueCompletenessPass(
        string markers, bool complete, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with
        {
            Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with
            {
                GuardObservations = markers.Length == 0 ? [] : markers.Split('|')
            }),
            Capabilities = ParseResult<CapturedCapabilities>.Success(request.Capabilities.Value! with
            {
                GuardObservationsComplete = complete, GuardObservations = ["unknown_capabilities_marker"]
            })
        };
        AssertState(LateWriteChecks.Evaluate(request), 14, pass);
    }

    public static IEnumerable<object[]> CounterCases() =>
        from counter in GuardCounters
        from value in new long[] { -1, 0, 1 }
        select new object[] { counter, value, value == 0 };

    [Fact]
    public void Given_IncompleteGuardCapture_When_Evaluated_Then_DiagnosticNamesCapabilitiesField()
    {
        // Synthetic evidence isolates the capabilities completeness guard.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Capabilities = ParseResult<CapturedCapabilities>.Success(
            request.Capabilities.Value! with { GuardObservationsComplete = false }) };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(request);
        AssertState(results, 14, false);
        Assert.Equal([new ResultDiagnostic(14, "check_14_failed", "capabilities.json", null, "guard_observations_complete", null)],
            results.Single(r => r.Assessment.CheckNumber == 14).Diagnostics);
    }

    [Fact]
    public void Given_UnknownSecondManifestMarker_When_Evaluated_Then_DiagnosticNamesItsIndex()
    {
        // Synthetic evidence places an unknown marker after an accepted marker.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with
        {
            GuardObservations = ["broker_lifecycle_inventory_unproven", "synthetic-unknown"]
        }) };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(request);
        AssertState(results, 14, false);
        Assert.Equal([new ResultDiagnostic(14, "check_14_failed", "manifest.json", null, "guard_observations[1]", null)],
            results.Single(r => r.Assessment.CheckNumber == 14).Diagnostics);
    }

    [Theory]
    [InlineData("native_null")]
    [InlineData("brokers_missing")]
    [InlineData("brokers_null")]
    [InlineData("brokers_value_null")]
    public void Given_UnavailableNativeBrokerEvidence_When_Evaluated_Then_CounterGuardIsNotEvaluated(string variation)
    {
        // Synthetic direct-call DTOs include a Value/null map unavailable from normal parsing.
        ReportAssociation report = RecordedEvidence.Report(100, 50);
        NativeClientReport native = report.Native.Value!;
        report = report with { Native = variation == "native_null"
            ? ParseResult<NativeClientReport>.Failure(new StructuralProblem(21, "missing_input", "native-statistics", null, null, null))
            : ParseResult<NativeClientReport>.Success(native with
            {
                Brokers = variation switch
                {
                    "brokers_missing" => ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Missing(),
                    "brokers_null" => ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Null(),
                    _ => new ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>(FieldPresence.Value, null)
                }
            }) };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(WithReports(RecordedEvidence.SyntheticRequest(), [report]));
        AssertState(results, 15, null);
        Assert.Empty(results.Single(r => r.Assessment.CheckNumber == 15).Diagnostics);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("capabilities")]
    public void Given_OneMarkerPrerequisiteMissing_When_Evaluated_Then_FourteenIsNotEvaluated(string artifact)
    {
        // Synthetic evidence removes one prerequisite while keeping the other benign.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = artifact == "manifest"
            ? request with { Manifest = ParseResult<SealedRunManifest>.Failure(
                new StructuralProblem(2, "missing_input", "manifest.json", null, null, null)) }
            : request with { Capabilities = ParseResult<CapturedCapabilities>.Failure(
                new StructuralProblem(17, "missing_input", "capabilities.json", null, null, null)) };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(request);
        AssertState(results, 14, null);
        Assert.Empty(results.Single(r => r.Assessment.CheckNumber == 14).Diagnostics);
    }

    [Theory]
    [MemberData(nameof(CounterCases))]
    public void Given_GuardCounterBelowAtOrAboveZero_When_Evaluated_Then_OnlyZeroPasses(string counter, long value, bool pass)
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(),
            [RecordedEvidence.Report(100, 50, [WithCounter(RecordedEvidence.Broker(), counter, value)])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, pass);
        string field = counter switch
        {
            "req_timeouts" => "brokers.broker-a.req_timeouts",
            "txretries" => "brokers.broker-a.txretries",
            "txerrs" => "brokers.broker-a.txerrs",
            _ => "brokers.broker-a.rxerrs"
        };
        Assert.Equal(pass ? [] : new[] { new ResultDiagnostic(15, "check_15_failed", "statistics.jsonl", 1, field, null) },
            LateWriteChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 15).Diagnostics);
    }

    [Theory]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    public void Given_NonzeroFirstCounterFollowedByZero_When_Evaluated_Then_LateGuardAndResetBothFail(string counter)
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(),
        [
            RecordedEvidence.Report(90, 40, [WithCounter(RecordedEvidence.Broker(), counter, 1)]),
            RecordedEvidence.Report(100, 50)
        ]);
        AssertState(LateWriteChecks.Evaluate(request), 15, false);
        AssertState(StatisticsChecks.Evaluate(request), 23, false);
    }

    [Theory]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    public void Given_ConstantNegativeGuardCounter_When_Evaluated_Then_LateGuardFailsWithoutCounterReset(string counter)
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(),
        [
            RecordedEvidence.Report(90, 40, [WithCounter(RecordedEvidence.Broker(), counter, -1)]),
            RecordedEvidence.Report(100, 50, [WithCounter(RecordedEvidence.Broker(), counter, -1)]),
            RecordedEvidence.Report(110, 60, [WithCounter(RecordedEvidence.Broker(), counter, -1)])
        ]);
        AssertState(LateWriteChecks.Evaluate(request), 15, false);
        AssertState(StatisticsChecks.Evaluate(request), 23, true);
    }

    [Theory]
    [InlineData("learned", "UP")]
    [InlineData("internal", "UP")]
    [InlineData("unknown", "UP")]
    [InlineData("learned", "DOWN")]
    public void Given_NonzeroCounterOnlyAfterPostDrain_When_Evaluated_Then_AllBrokerSourcesAndStatesAreScanned(string source, string state)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = WithReports(request,
        [
            request.Reports[0], request.Reports[1],
            RecordedEvidence.Report(110, 60, [WithCounter(RecordedEvidence.Broker(source: source, state: state), "txerrs", 1)])
        ]);
        AssertState(LateWriteChecks.Evaluate(request), 15, false);
    }

    [Fact]
    public void Given_DisconnectCounterPositiveAlone_When_Evaluated_Then_LateCounterGuardPasses()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(),
            [RecordedEvidence.Report(100, 50, [RecordedEvidence.Broker() with { Disconnects = ParsedField<long>.FromValue(1) }])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, true);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Given_NonzeroCounterOnForeignStatisticsLabels_When_Evaluated_Then_ReportStillFailsGuard(bool role, bool client)
    {
        ReportAssociation report = RecordedEvidence.Report(100, 50, [WithCounter(RecordedEvidence.Broker(), "txerrs", 1)]);
        report = report with { Envelope = report.Envelope with { StatisticsIdentity = report.Envelope.StatisticsIdentity with
        {
            Role = role ? 99 : report.Envelope.StatisticsIdentity.Role,
            ClientId = client ? "foreign" : report.Envelope.StatisticsIdentity.ClientId
        } } };
        AssertState(LateWriteChecks.Evaluate(WithReports(RecordedEvidence.SyntheticRequest(), [report])), 15, false);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void Given_NativeErrorFlags_When_Evaluated_Then_OnlyIsErrorControlsSixteen(bool error, bool fatal, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success(
            [new NativeError(1, request.Identity, "synthetic", 0, "synthetic", 99, "error", ParsedField<string>.Null(), error, fatal, "synthetic")]) };
        AssertState(LateWriteChecks.Evaluate(request), 16, pass);
        Assert.Equal(pass ? [] : new[] { new ResultDiagnostic(16, "check_16_failed", "request-lifecycle.jsonl", 1, "is_error", null) },
            LateWriteChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 16).Diagnostics);
    }

    [Fact]
    public void Given_NativeLogWithErrorSeverityAndText_When_Evaluated_Then_SixteenPasses()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success(
            [new NativeLog(1, request.Identity, "synthetic", 0, "synthetic", ParsedField<string>.Null(),
                ParsedField<string>.Null(), ParsedField<long>.Null(), [], ParsedField<string>.Null(),
                "native", "error", "error", "error", "synthetic")]) };
        AssertState(LateWriteChecks.Evaluate(request), 16, true);
    }

    [Fact]
    public void Given_AllGuardInputsMissing_When_Evaluated_Then_GuardsAreNotEvaluated()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest() with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(1, "missing_input", "ledger.jsonl", null, null, null)),
            Manifest = ParseResult<SealedRunManifest>.Failure(new StructuralProblem(2, "missing_input", "manifest.json", null, null, null)),
            Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(new StructuralProblem(17, "missing_input", "statistics.jsonl", null, null, null)),
            Capabilities = ParseResult<CapturedCapabilities>.Failure(new StructuralProblem(17, "missing_input", "capabilities.json", null, null, null)),
            Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(new StructuralProblem(17, "missing_input", "request-lifecycle.jsonl", null, null, null)),
            Reports = []
        };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(request);
        foreach (int check in Enumerable.Range(12, 5)) AssertState(results, check, null);
    }

    [Fact]
    public void Given_PresentEmptyLedgerReportsAndLifecycle_When_Evaluated_Then_GuardsPassTheirEmptyPredicates()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), []) with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([]), Records = []
        };
        IReadOnlyList<CheckResult> results = LateWriteChecks.Evaluate(request);
        foreach (int check in Enumerable.Range(12, 5)) AssertState(results, check, true);
        request = WithReports(request, [RecordedEvidence.Report(100, 50, [])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, true);
    }

    [Fact]
    public void Given_UnknownManifestMarkerAndMissingCapabilities_When_Evaluated_Then_WitnessedFailureWins()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with
        {
            Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { GuardObservations = ["unknown"] }),
            Capabilities = ParseResult<CapturedCapabilities>.Failure(new StructuralProblem(17, "missing_input", "capabilities.json", null, null, null))
        };
        AssertState(LateWriteChecks.Evaluate(request), 14, false);
    }

    [Fact]
    public void Given_IncompleteNativeReportWithReadableNonzeroCounter_When_Evaluated_Then_WitnessedFailureWins()
    {
        NativeBrokerReport broker = RecordedEvidence.Broker() with
        {
            RequestTimeouts = ParsedField<long>.Missing(), TransmissionErrors = ParsedField<long>.FromValue(1)
        };
        ReportAssociation report = RecordedEvidence.Report(100, 50, [broker]);
        report = report with { Native = new(report.Native.Value,
            [new(21, "missing_property", "native-statistics", null, "brokers.broker-a.req_timeouts", null)]) };
        AssertState(LateWriteChecks.Evaluate(WithReports(RecordedEvidence.SyntheticRequest(), [report])), 15, false);
    }

    [Fact]
    public void Given_ZeroCountersWithMissingRequiredCounter_When_Evaluated_Then_FifteenIsNotEvaluated()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(),
            [RecordedEvidence.Report(100, 50, [RecordedEvidence.Broker() with { ReceiveErrors = ParsedField<long>.Missing() }])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, null);
    }

    private static NativeBrokerReport WithCounter(NativeBrokerReport broker, string counter, long value) => counter switch
    {
        "req_timeouts" => broker with { RequestTimeouts = ParsedField<long>.FromValue(value) },
        "txretries" => broker with { TransmissionRetries = ParsedField<long>.FromValue(value) },
        "txerrs" => broker with { TransmissionErrors = ParsedField<long>.FromValue(value) },
        "rxerrs" => broker with { ReceiveErrors = ParsedField<long>.FromValue(value) },
        _ => throw new ArgumentOutOfRangeException(nameof(counter))
    };

    private static RunJudgementRequest WithReports(RunJudgementRequest request, IReadOnlyList<ReportAssociation> reports) => request with
    {
        Reports = reports,
        Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(r => r.Envelope).ToArray())
    };

    private static void AssertState(IReadOnlyList<CheckResult> results, int check, bool? pass)
    {
        CheckAssessment assessment = Assert.Single(results, r => r.Assessment.CheckNumber == check).Assessment;
        Assert.Equal(check, assessment.CheckNumber);
        Assert.Equal(pass is null ? AssessmentEvaluation.NotEvaluated : AssessmentEvaluation.Evaluated, assessment.Evaluation);
        Assert.Equal(pass is null ? null : pass.Value ? AssessmentDisposition.Pass : AssessmentDisposition.Fail, assessment.Disposition);
        Assert.Equal(pass == false ? AssessmentClassification.Invalid : null, assessment.Classification);
    }
}
