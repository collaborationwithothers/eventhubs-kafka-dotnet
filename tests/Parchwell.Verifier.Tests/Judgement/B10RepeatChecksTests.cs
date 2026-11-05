using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.

/// <summary>Protects ADR-021's scoped exceptions and independent retry/restart reconciliation.</summary>
public sealed class B10RepeatChecksTests
{
    private static readonly string[] Instances = ["first", "second"];
    private static readonly string[] Counters = ["req_timeouts", "txretries", "txerrs", "rxerrs"];
    private static readonly string[] CompletionSources = ["callback", "synchronous_rejection", "Callback"];
    private static readonly bool[] DeliveryStates = [false, true];
    private static readonly int[] ErrorCodes = [-193, -192, -191];
    private static readonly string[] PersistenceStates = ["missing", "null", "-1", "0", "1", "2"];
    private static readonly long[] CounterValues = [-1, 0, 1];
    private static readonly string[] BrokerNames = ["broker-a", "broker-a;counter=txerrs;observed=99"];
    private static readonly OutputLimits Limits = new(262_144, 16, 256);

    public static IEnumerable<object[]> TimeoutCases() =>
        from source in CompletionSources
        from delivered in DeliveryStates
        from error in ErrorCodes
        from persistence in PersistenceStates
        select new object[] { source, delivered, error, persistence,
            source == "callback" && !delivered && error == -192 && persistence is "0" or "1" };

    [Theory]
    [MemberData(nameof(TimeoutCases))]
    public void Given_TimeoutConjunction_When_Checked_Then_EveryClauseMustMatch(
        string source, bool delivered, int error, string persistence, bool permitted)
    {
        LedgerRow row = Row(0, "a") with
        {
            CompletionSource = source, Delivered = delivered, ErrorCode = error,
            PersistenceStatus = Persistence(persistence)
        };
        Assert.Equal(permitted, B10RepeatChecks.IsPermittedTimeout(row));
        RunJudgementRequest request = Request(0, [row]);
        IReadOnlyList<CheckResult> checks = LateWriteChecks.Evaluate(request);
        AssertState(checks, 12, source != "callback" || delivered || permitted);
        AssertState(checks, 13, source != "callback" || (persistence is not "missing" and not "null" and not "1") || permitted);
    }

    [Theory]
    [InlineData("B10", 0, true, true)] [InlineData("B10", 1, true, true)]
    [InlineData("A01", 0, false, true)] [InlineData("A01", 1, false, false)]
    [InlineData("b10", 0, false, true)] [InlineData("b10", 1, false, false)]
    public void Given_IdenticalTimeoutAcrossScenarioIds_When_GuardsRun_Then_OnlyExactB10ReceivesException(
        string scenario, int persistence, bool twelve, bool thirteen)
    {
        RunJudgementRequest request = Request(0, [Timeout(0, "a") with { PersistenceStatus = ParsedField<int>.FromValue(persistence) }]);
        request = request with { Identity = request.Identity with { ScenarioId = scenario } };
        AssertState(LateWriteChecks.Evaluate(request), 12, twelve);
        AssertState(LateWriteChecks.Evaluate(request), 13, thirteen);
    }

    [Theory]
    [InlineData("async_delivery_failure_observed", "backed", true)]
    [InlineData("possibly_persisted_observed", "backed", true)]
    [InlineData("async_delivery_failure_observed", "empty", false)]
    [InlineData("possibly_persisted_observed", "empty", false)]
    [InlineData("async_delivery_failure_observed", "missing", false)]
    [InlineData("possibly_persisted_observed", "missing", false)]
    [InlineData("async_delivery_failure_observed", "mixed", false)]
    [InlineData("possibly_persisted_observed", "mixed", false)]
    [InlineData("possibly_persisted_observed", "status-zero", false)]
    [InlineData("possibly_persisted_observed", "status-null", false)]
    [InlineData("possibly_persisted_observed", "status-missing", false)]
    [InlineData("async_delivery_failure_observed", "sync", false)]
    [InlineData("possibly_persisted_observed", "sync", false)]
    [InlineData("unknown", "backed", false)]
    [InlineData("ASYNC_DELIVERY_FAILURE_OBSERVED", "backed", false)]
    public void Given_RowGuardMarker_When_Checked_Then_OnlyPresentEntirelyPermittedBackingPasses(string marker, string variation, bool permitted)
    {
        LedgerRow row = Timeout(0, "a");
        LedgerRow[] rows = variation switch
        {
            "empty" => [],
            "mixed" => [row, Row(0, "b") with { Delivered = false, PersistenceStatus = ParsedField<int>.FromValue(1) }],
            "status-zero" => [row with { PersistenceStatus = ParsedField<int>.FromValue(0) }],
            "status-null" => [row with { PersistenceStatus = ParsedField<int>.Null() }],
            "status-missing" => [row with { PersistenceStatus = ParsedField<int>.Missing() }],
            "sync" => [row with { CompletionSource = "synchronous_rejection" }],
            _ => [row]
        };
        RunJudgementRequest request = WithMarkers(Request(0, rows), [marker]);
        if (variation == "missing") request = request with { Ledger = Missing<IReadOnlyList<LedgerRow>>(1) };
        Assert.Equal(permitted, B10RepeatChecks.IsPermittedGuardMarker(marker, request));
        AssertState(LateWriteChecks.Evaluate(request), 14, permitted);
    }

    [Theory]
    [InlineData("req_timeouts", 1)] [InlineData("req_timeouts", 2)]
    [InlineData("txretries", 1)] [InlineData("txretries", 2)]
    public void Given_ExactCapturedCounterMarker_When_Checked_Then_EmbeddedSeparatorsRemainPartOfName(string counter, long value)
    {
        foreach (string name in BrokerNames)
        {
            NativeBrokerReport broker = Counter(RecordedEvidence.Broker(), counter, ParsedField<long>.FromValue(value)) with { Name = ParsedField<string>.FromValue(name) };
            string marker = FormattableString.Invariant($"broker={name};counter={counter};observed={value}");
            RunJudgementRequest request = WithMarkers(WithReports(Request(0, []), [RecordedEvidence.Report(100, 50, [broker])]), [marker]);
            Assert.True(B10RepeatChecks.IsPermittedGuardMarker(marker, request));
            AssertState(LateWriteChecks.Evaluate(request), 14, true);
        }
    }

    [Theory]
    [InlineData("zero")] [InlineData("negative")] [InlineData("wrong-value")]
    [InlineData("other-broker")] [InlineData("txerrs")] [InlineData("rxerrs")]
    [InlineData("prefix")] [InlineData("suffix")] [InlineData("missing-counter")]
    [InlineData("null-counter")] [InlineData("missing-name")] [InlineData("null-name")]
    [InlineData("missing-native")] [InlineData("missing-brokers")] [InlineData("null-brokers")]
    [InlineData("no-reports")] [InlineData("case")]
    public void Given_CounterMarkerWithoutExactReadableBacking_When_Checked_Then_FourteenFails(string variation)
    {
        string counter = variation is "txerrs" or "rxerrs" ? variation : "req_timeouts";
        long value = variation == "zero" ? 0 : variation == "negative" ? -1 : 1;
        NativeBrokerReport broker = Counter(RecordedEvidence.Broker(), counter, ParsedField<long>.FromValue(value));
        if (variation is "missing-counter" or "null-counter") broker = Counter(broker, counter, variation == "missing-counter" ? ParsedField<long>.Missing() : ParsedField<long>.Null());
        if (variation is "missing-name" or "null-name") broker = broker with { Name = variation == "missing-name" ? ParsedField<string>.Missing() : ParsedField<string>.Null() };
        ReportAssociation report = RecordedEvidence.Report(100, 50, [broker]);
        if (variation == "missing-native") report = report with { Native = Missing<NativeClientReport>(21) };
        if (variation is "missing-brokers" or "null-brokers") report = report with { Native = ParseResult<NativeClientReport>.Success(report.Native.Value! with
        { Brokers = variation == "missing-brokers" ? ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Missing() : ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Null() }) };
        string marker = FormattableString.Invariant($"broker=broker-a;counter={counter};observed={value}");
        marker = variation switch
        {
            "wrong-value" => "broker=broker-a;counter=req_timeouts;observed=2",
            "other-broker" => "broker=broker-b;counter=req_timeouts;observed=1",
            "prefix" => "broker=broker;counter=req_timeouts;observed=1",
            "suffix" => marker + ";extra=1",
            "case" => "broker=broker-a;counter=REQ_TIMEOUTS;observed=1",
            _ => marker
        };
        RunJudgementRequest request = WithMarkers(WithReports(Request(0, []), variation == "no-reports" ? [] : [report]), [marker]);
        Assert.False(B10RepeatChecks.IsPermittedGuardMarker(marker, request));
        AssertState(LateWriteChecks.Evaluate(request), 14, false);
    }

    [Theory]
    [InlineData("B10", true)] [InlineData("A01", false)] [InlineData("b10", false)]
    public void Given_BackedMarkersAndIncompleteCapture_When_GuardsRun_Then_CompletenessRemainsIndependent(string scenario, bool markerPermitted)
    {
        RunJudgementRequest request = WithMarkers(Request(0, [Timeout(0, "a")]), ["async_delivery_failure_observed"]);
        request = request with { Identity = request.Identity with { ScenarioId = scenario } };
        AssertState(LateWriteChecks.Evaluate(request), 14, markerPermitted);
        request = request with { Capabilities = ParseResult<CapturedCapabilities>.Success(request.Capabilities.Value! with { GuardObservationsComplete = false }) };
        AssertState(LateWriteChecks.Evaluate(request), 14, false);
    }

    public static IEnumerable<object[]> CounterCases() =>
        from counter in Counters
        from value in CounterValues
        select new object[] { counter, value, counter is "req_timeouts" or "txretries" || value == 0 };

    [Theory]
    [MemberData(nameof(CounterCases))]
    public void Given_B10CountersAroundZero_When_GuardsRun_Then_OnlyTimeoutAndRetryCountersAreExempt(string counter, long value, bool pass)
    {
        RunJudgementRequest request = WithReports(Request(0, []), [RecordedEvidence.Report(100, 50,
            [Counter(RecordedEvidence.Broker(), counter, ParsedField<long>.FromValue(value))])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, pass);
    }

    [Theory]
    [InlineData("req_timeouts", false)] [InlineData("req_timeouts", true)]
    [InlineData("txretries", false)] [InlineData("txretries", true)]
    [InlineData("txerrs", false)] [InlineData("txerrs", true)]
    [InlineData("rxerrs", false)] [InlineData("rxerrs", true)]
    public void Given_MissingOrNullCounter_When_GuardsRun_Then_PermittedCountersStillRequirePresence(string counter, bool isNull)
    {
        RunJudgementRequest request = WithReports(Request(0, []), [RecordedEvidence.Report(100, 50,
            [Counter(RecordedEvidence.Broker(), counter, isNull ? ParsedField<long>.Null() : ParsedField<long>.Missing())])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, null);
        request = WithReports(request, [RecordedEvidence.Report(100, 50,
            [Counter(RecordedEvidence.Broker() with { TransmissionErrors = ParsedField<long>.FromValue(1), ReceiveErrors = ParsedField<long>.FromValue(1) },
                counter, isNull ? ParsedField<long>.Null() : ParsedField<long>.Missing())])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, false);
    }

    [Theory]
    [InlineData("req_timeouts", true)] [InlineData("txretries", true)]
    [InlineData("txerrs", false)] [InlineData("rxerrs", false)]
    public void Given_EarlierCounterNonzeroThenZero_When_Checked_Then_HistoryFailureRemainsIndependent(string counter, bool guardPasses)
    {
        RunJudgementRequest request = WithReports(Request(0, []),
        [RecordedEvidence.Report(90, 40, [Counter(RecordedEvidence.Broker(), counter, ParsedField<long>.FromValue(1))]), RecordedEvidence.Report(100, 50)]);
        AssertState(LateWriteChecks.Evaluate(request), 15, guardPasses);
        AssertState(StatisticsChecks.Evaluate(request), 23, false);
    }

    [Theory]
    [InlineData("learned", "UP")] [InlineData("internal", "UP")]
    [InlineData("unknown", "UP")] [InlineData("learned", "DOWN")]
    public void Given_ForbiddenCounterInLateNonqualifyingReport_When_Checked_Then_FifteenStillFails(string source, string state)
    {
        RunJudgementRequest request = Request(0, []);
        request = WithReports(request, [request.Reports[0], request.Reports[1], RecordedEvidence.Report(110, 60,
            [RecordedEvidence.Broker(source: source, state: state) with { ReceiveErrors = ParsedField<long>.FromValue(1) }])]);
        AssertState(LateWriteChecks.Evaluate(request), 15, false);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_PermittedTimeoutWithNativeError_When_Checked_Then_SixteenStillUsesIsError(bool error)
    {
        RunJudgementRequest request = Request(0, [Timeout(0, "a")]);
        request = request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success(
            [new NativeError(1, request.Identity, "synthetic", 0, "synthetic", 99, "error", ParsedField<string>.Null(), error, false, "synthetic")]) };
        AssertState(LateWriteChecks.Evaluate(request), 12, true);
        AssertState(LateWriteChecks.Evaluate(request), 16, !error);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_PresentEmptyOrMissingLedger_When_Checked_Then_AbsenceNeverGrantsMarkerExemption(bool missing)
    {
        RunJudgementRequest request = Request(0, []);
        if (missing) request = request with { Ledger = Missing<IReadOnlyList<LedgerRow>>(1) };
        AssertState(LateWriteChecks.Evaluate(request), 12, missing ? null : true);
        AssertState(LateWriteChecks.Evaluate(request), 13, missing ? null : true);
        Assert.False(B10RepeatChecks.IsPermittedGuardMarker("async_delivery_failure_observed", request));
        Assert.False(B10RepeatChecks.IsPermittedGuardMarker("possibly_persisted_observed", request));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, true)]
    public void Given_ThreeSamePairInvocations_When_Reconciled_Then_ImmediatelyPreviousRowControlsRetry(bool middleTimeout, bool valid)
    {
        LedgerRow a = Timeout(0, "a");
        LedgerRow b = (middleTimeout ? Timeout(0, "b", 1) : Row(0, "b", 1));
        LedgerRow c = Row(0, "c", 2);
        RunJudgementRequest first = Request(0, [a, b, c]);
        B10RepeatJudgement result = Evaluate(first, Request(1, []));
        Assert.Equal(valid ? VerifierResultStatus.Incomplete : VerifierResultStatus.Invalid, result.Judgement.Status);
        if (valid) { Assert.Empty(result.Judgement.FailedChecks); Assert.NotNull(result.InWindowCounts); }
        else AssertRetryFailure(result, 0, 3);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void Given_SuccessFollowedBySamePairWithinEitherInstance_When_Reconciled_Then_RetryIsInvalid(int instance)
    {
        RunJudgementRequest first = Request(0, instance == 0 ? [Row(0, "a"), Row(0, "b", 1)] : []);
        RunJudgementRequest second = Request(1, instance == 1 ? [Row(1, "a"), Row(1, "b", 1)] : []);
        AssertRetryFailure(Evaluate(first, second), instance, 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_CrossInstanceRetryAndReversedTimestamps_When_Reconciled_Then_BoundInstanceOrderControls(bool timeout)
    {
        LedgerRow firstRow = (timeout ? Timeout(0, "a") : Row(0, "a")) with { CompletedMicroseconds = 10_000 };
        firstRow = firstRow with { Attempt = firstRow.Attempt with { InvokedMicroseconds = 10_000 } };
        LedgerRow secondRow = Row(1, "b") with { CompletedMicroseconds = 1, Attempt = Attempt(1, "b") with { InvokedMicroseconds = 1 } };
        RunJudgementRequest first = Request(0, [firstRow]);
        RunJudgementRequest second = Request(1, [secondRow], timeout ? [Planned(firstRow)] : []);
        B10RepeatJudgement result = Evaluate(first, second);
        if (!timeout) AssertRetryFailure(result, 1, 1);
        else
        {
            Assert.Empty(result.Judgement.FailedChecks);
            Assert.Equal(new OutcomeCounts(0, 1, 0, 1, 0, 0), result.InWindowCounts);
            Assert.Null(result.Judgement.Counts);
        }
    }

    [Fact]
    public void Given_SynchronousRejectionBeforeRepeatedPair_When_Reconciled_Then_ItCannotAuthorizeRetry()
    {
        LedgerRow rejection = Row(0, "a") with { CompletionSource = "synchronous_rejection", Delivered = false,
            ErrorCode = -192, PersistenceStatus = ParsedField<int>.Null(), PersistenceProvenance = "pre_enqueue" };
        AssertRetryFailure(Evaluate(Request(0, [rejection]), Request(1, [Row(1, "b")])), 1, 1);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    [InlineData(4, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(7, false)]
    public void Given_UnavailableAssessmentAroundBookkeepingRange_When_Reconciled_Then_OnlyChecksOneThroughSixDisableJoins(int check, bool unavailable)
    {
        RunJudgementResult previous = check == 0 ? Result() with { Assessments = Result().Assessments.Append(Unavailable(0)).ToArray() }
            : WithAssessment(Result(), Unavailable(check));
        B10RepeatJudgement result = Evaluate(Request(0, [Row(0, "a"), Row(0, "b", 1)]), Request(1, []), previous);
        Assert.Equal(!unavailable, result.Judgement.Diagnostics.Any(d => d.Code == "b10_retry_requires_permitted_timeout"));
        Assert.Null(result.InWindowCounts);
    }

    [Theory]
    [InlineData("key")] [InlineData("sequence")]
    public void Given_DifferentKeyOrSequence_When_Reconciled_Then_NoRetryPredecessorIsRequired(string different)
    {
        LedgerRow[] rows = different == "sequence" ? [Row(0, "a"), Row(0, "b", 1, sequence: 1)] :
            [Row(0, "a0"), Row(0, "a1", 1, sequence: 1), Row(0, "a2", 2, sequence: 2),
                Row(0, "a3", 3, sequence: 3), Row(0, "a4", 4, sequence: 4), Row(0, "b", 5, document: 1)];
        B10RepeatJudgement result = Evaluate(Request(0, rows), Request(1, []));
        Assert.Empty(result.Judgement.FailedChecks);
        Assert.NotNull(result.InWindowCounts);
    }

    [Fact]
    public void Given_ShuffledLedgerRows_When_Reconciled_Then_SealedInventoryControlsAndDiagnosticRetainsOriginalRow()
    {
        LedgerRow a = Timeout(0, "a"); LedgerRow b = Row(0, "b", 1); LedgerRow c = Row(0, "c", 2);
        RunJudgementRequest ordered = Request(0, [a, b, c]);
        RunJudgementRequest shuffled = ordered with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([c, a, b]) };
        B10RepeatJudgement ordinary = Evaluate(ordered, Request(1, []));
        B10RepeatJudgement reordered = Evaluate(shuffled, Request(1, []));
        AssertRetryFailure(ordinary, 0, 3);
        AssertRetryFailure(reordered, 0, 1);
        Assert.Equal(ordinary.Judgement.Status, reordered.Judgement.Status);
        Assert.Equal(ordinary.Judgement.FailedChecks, reordered.Judgement.FailedChecks);
    }

    [Theory]
    [InlineData("ledger")] [InlineData("manifest")] [InlineData("duplicate-ledger")]
    [InlineData("duplicate-inventory")] [InlineData("missing-assessment")]
    [InlineData("failed-assessment")]
    public void Given_UnavailableOrMalformedBookkeeping_When_Reconciled_Then_JoinsAreUnavailableAndNeverThrow(string variation)
    {
        RunJudgementRequest first = Request(0, [Timeout(0, "a")]);
        RunJudgementResult prior = Result();
        switch (variation)
        {
            case "ledger": first = first with { Ledger = Missing<IReadOnlyList<LedgerRow>>(1) }; prior = WithAssessment(prior, Unavailable(1)); break;
            case "manifest": first = first with { Manifest = Missing<SealedRunManifest>(2) }; prior = WithAssessment(prior, Unavailable(2)); break;
            case "duplicate-ledger": first = first with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([Timeout(0, "a"), Timeout(0, "a")]) }; prior = WithAssessment(prior, RecordedEvidence.WindowFail(1, AssessmentClassification.Invalid)); break;
            case "duplicate-inventory": first = first with { Manifest = ParseResult<SealedRunManifest>.Success(first.Manifest.Value! with { Inventory = [first.Manifest.Value!.Inventory[0], first.Manifest.Value!.Inventory[0]] }) }; prior = WithAssessment(prior, RecordedEvidence.WindowFail(2, AssessmentClassification.Invalid)); break;
            case "missing-assessment": prior = prior with { Assessments = prior.Assessments.Where(a => a.CheckNumber != 6).ToArray() }; break;
            case "failed-assessment": prior = WithAssessment(prior, RecordedEvidence.WindowFail(6, AssessmentClassification.Invalid)); break;
        }
        B10RepeatJudgement result = Evaluate(first, Request(1, []), prior);
        Assert.Null(result.InWindowCounts);
        Assert.DoesNotContain(result.Judgement.Diagnostics, d => d.Code == "restart_plan_mismatch" || d.Code == "b10_retry_requires_permitted_timeout");
    }

    [Theory]
    [InlineData(0, 0, true)] [InlineData(0, 1, false)]
    [InlineData(1, 0, false)] [InlineData(1, 1, true)] [InlineData(1, 2, false)]
    [InlineData(2, 1, false)] [InlineData(2, 2, true)] [InlineData(2, 3, false)]
    public void Given_RestartSelectionCountsAroundExpected_When_Reconciled_Then_EntirePlanMustMatch(int expectedCount, int actualCount, bool valid)
    {
        LedgerRow[] rows = Enumerable.Range(0, expectedCount).Select(index => Timeout(0, $"a{index}", index, document: index)).ToArray();
        RestartPlannedEntry[] plan = Enumerable.Range(0, actualCount).Select(index => Planned(Timeout(0, $"a{index}", index, document: index), index)).ToArray();
        B10RepeatJudgement result = Evaluate(Request(0, rows), Request(1, [], plan));
        if (valid) { Assert.Empty(result.Judgement.FailedChecks); Assert.NotNull(result.InWindowCounts); }
        else AssertPlanFailure(result);
    }

    [Theory]
    [InlineData("ordinal")] [InlineData("predecessor")]
    [InlineData("tenant")] [InlineData("document")] [InlineData("key")]
    [InlineData("sequence")] [InlineData("event")]
    [InlineData("planned-below")] [InlineData("planned-above")]
    public void Given_OneCopiedRestartFieldMismatch_When_Reconciled_Then_CheckFourRejects(string field)
    {
        LedgerRow row = Timeout(0, "a") with { Attempt = Attempt(0, "a") with { PlannedMicroseconds = 1 } };
        RestartPlannedEntry entry = Planned(row);
        entry = field switch
        {
            "ordinal" => entry with { Ordinal = 1 }, "predecessor" => entry with { PredecessorAttemptId = "other" },
            "tenant" => entry with { TenantId = "other" }, "document" => entry with { DocumentId = "d1" },
            "key" => entry with { Key = "other" }, "sequence" => entry with { Sequence = 1 },
            "event" => entry with { EventType = "downloaded" },
            "planned-below" => entry with { PlannedMicroseconds = 0 }, _ => entry with { PlannedMicroseconds = 2 }
        };
        AssertPlanFailure(Evaluate(Request(0, [row]), Request(1, [], [entry])));
    }

    [Theory]
    [InlineData("d0", -1, "uploaded", "t0-d0", false)]
    [InlineData("d0", 0, "uploaded", "t0-d0", true)]
    [InlineData("d0", 1, "ocr-started", "t0-d0", true)]
    [InlineData("d0", 2, "ocr-finished", "t0-d0", true)]
    [InlineData("d0", 3, "classified", "t0-d0", true)]
    [InlineData("d0", 4, "indexed", "t0-d0", true)]
    [InlineData("d0", 5, "indexed", "t0-d0", false)]
    [InlineData("d0", 0, "indexed", "t0-d0", false)]
    [InlineData("d0", 0, "Uploaded", "t0-d0", false)]
    [InlineData("", 0, "uploaded", "t0-", false)]
    [InlineData("d", 0, "uploaded", "t0-d", false)]
    [InlineData("D0", 0, "uploaded", "t0-D0", false)]
    [InlineData("x0", 0, "uploaded", "t0-x0", false)]
    [InlineData("d-1", 0, "uploaded", "t0-d-1", false)]
    [InlineData("d+1", 0, "uploaded", "t0-d+1", false)]
    [InlineData("d 1", 0, "uploaded", "t0-d 1", false)]
    [InlineData("d01", 0, "uploaded", "t0-d01", false)]
    [InlineData("d0", 0, "uploaded", "t0:d0", false)]
    [InlineData("d1", 0, "uploaded", "t0-d1", true)]
    [InlineData("d9223372036854775808", 0, "uploaded", "t0-d9223372036854775808", false)]
    [InlineData("d1844674407370955161", 1, "ocr-started", "t0-d1844674407370955161", true)]
    [InlineData("d1844674407370955161", 2, "ocr-finished", "t0-d1844674407370955161", true)]
    [InlineData("d1844674407370955161", 3, "classified", "t0-d1844674407370955161", false)]
    [InlineData("d1844674407370955162", 0, "uploaded", "t0-d1844674407370955162", false)]
    public void Given_LogicalSlotShapeAndOverflowBoundaries_When_Recomputed_Then_OnlyCanonicalCheckedSlotsPass(
        string document, long sequence, string eventType, string key, bool valid)
    {
        LedgerRow row = Timeout(0, "a");
        row = row with { Attempt = row.Attempt with { DocumentId = document, Sequence = sequence, EventType = eventType, Key = key } };
        B10RepeatJudgement result = Evaluate(Request(0, [row]), Request(1, [], [Planned(row)]));
        if (valid) { Assert.Empty(result.Judgement.FailedChecks); Assert.NotNull(result.InWindowCounts); }
        else AssertPlanFailure(result);
    }

    [Fact]
    public void Given_InventorySlotsOutOfOrder_When_Recomputed_Then_PlannedEntriesAreSortedByLogicalSlot()
    {
        LedgerRow laterSlot = Timeout(0, "a", document: 1);
        LedgerRow earlierSlot = Timeout(0, "b", 1);
        B10RepeatJudgement result = Evaluate(Request(0, [laterSlot, earlierSlot]),
            Request(1, [], [Planned(earlierSlot), Planned(laterSlot, 1)]));
        Assert.Empty(result.Judgement.FailedChecks);
        Assert.NotNull(result.InWindowCounts);
    }

    [Fact]
    public void Given_ReversedTypedRestartList_When_Reconciled_Then_OrderMismatchIsCheckFour()
    {
        LedgerRow a = Timeout(0, "a"); LedgerRow b = Timeout(0, "b", 1, document: 1);
        AssertPlanFailure(Evaluate(Request(0, [a, b]), Request(1, [], [Planned(b, 1), Planned(a)])));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Given_LatestAttemptSupersedesEarlierAttempt_When_RestartSelected_Then_OnlyLatestTimeoutIsResent(bool latestTimeout)
    {
        LedgerRow earlier = latestTimeout ? Row(0, "a") : Timeout(0, "a");
        LedgerRow latest = latestTimeout ? Timeout(0, "b", 1) : Row(0, "b", 1);
        RunJudgementRequest first = Request(0, [earlier, latest]);
        // The successful-then-timeout case independently violates retry policy; selection must still require b.
        B10RepeatJudgement exact = Evaluate(first, Request(1, [], latestTimeout ? [Planned(latest)] : []));
        Assert.DoesNotContain(exact.Judgement.Diagnostics, d => d.Code == "restart_plan_mismatch");
        B10RepeatJudgement wrong = Evaluate(first, Request(1, [], [Planned(earlier)]));
        AssertPlanFailure(wrong);
        if (!latestTimeout) Assert.Empty(exact.Judgement.FailedChecks);
    }

    [Theory]
    [InlineData("first-scenario")] [InlineData("scenario")] [InlineData("configuration")] [InlineData("run")]
    [InlineData("predecessor")] [InlineData("first-restart")]
    [InlineData("second-restart-absent")] [InlineData("worker")]
    public void Given_RestartSharedOrRoleIdentityMismatch_When_Reconciled_Then_CheckFourRejects(string variation)
    {
        RunJudgementRequest first = Request(0, []); RunJudgementRequest second = Request(1, []);
        SealedRunManifest manifest = second.Manifest.Value!;
        switch (variation)
        {
            case "first-scenario": first = first with { Manifest = ParseResult<SealedRunManifest>.Success(first.Manifest.Value! with { Identity = first.Identity with { ScenarioId = "b10" } }) }; break;
            case "scenario": manifest = manifest with { Identity = manifest.Identity with { ScenarioId = "A01" } }; break;
            case "configuration": manifest = manifest with { Identity = manifest.Identity with { ConfigurationId = "C1" } }; break;
            case "run": manifest = manifest with { Identity = manifest.Identity with { RunId = "other" } }; break;
            case "predecessor": manifest = manifest with { Restart = manifest.Restart! with { PredecessorInstanceId = "other" } }; break;
            case "second-restart-absent": manifest = manifest with { Restart = null }; break;
            case "first-restart": first = first with { Manifest = ParseResult<SealedRunManifest>.Success(first.Manifest.Value! with { Restart = new("other", []) }) }; break;
            case "worker": first = first with { Manifest = ParseResult<SealedRunManifest>.Success(first.Manifest.Value! with { WorkerStatus = "partial" }) }; break;
        }
        second = second with { Manifest = ParseResult<SealedRunManifest>.Success(manifest) };
        B10RepeatJudgement result = Evaluate(first, second);
        Assert.Equal(VerifierResultStatus.Invalid, result.Judgement.Status);
        Assert.Contains(4, result.Judgement.FailedChecks);
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "restart_identity_mismatch" && d.Check == 4);
        Assert.Null(result.InWindowCounts);
    }

    [Fact]
    public void Given_PlanMismatchAndIndependentGuardStatisticsFailures_When_Reconciled_Then_AllFailuresSurvive()
    {
        LedgerRow row = Timeout(0, "a");
        RunJudgementResult first = WithAssessment(Result(), RecordedEvidence.WindowFail(15, AssessmentClassification.Invalid)) with
        { Status = VerifierResultStatus.Invalid, FailedChecks = [15], Diagnostics = [new(15, "check_15_failed", "statistics.jsonl", 1, "txerrs", null)] };
        RunJudgementResult second = WithAssessment(Result(), RecordedEvidence.WindowFail(23, AssessmentClassification.Invalid)) with
        { Status = VerifierResultStatus.Invalid, FailedChecks = [23], Diagnostics = [new(23, "check_23_failed", "statistics.jsonl", 2, "counter", null)] };
        B10RepeatJudgement result = Evaluate(Request(0, [row]), Request(1, []), first, second);
        Assert.Equal([4, 15, 23], result.Judgement.FailedChecks);
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "restart_plan_mismatch" && d.Artifact == "producer/second/manifest.json");
        Assert.Contains(result.Judgement.Diagnostics, d => d.Check == 15 && d.Artifact == "producer/first/statistics.jsonl");
        Assert.Contains(result.Judgement.Diagnostics, d => d.Check == 23 && d.Artifact == "producer/second/statistics.jsonl");
        Assert.Null(result.InWindowCounts);
    }

    [Fact]
    public void Given_CombinedOutcomeOnlyFixture_When_Classified_Then_CrossInstanceDuplicateAndReordersAreCounted()
    {
        LedgerRow a = Timeout(0, "a", key: "k"); LedgerRow b = Row(0, "b", 1, sequence: 1, key: "k");
        LedgerRow c = Row(0, "c", 2, key: "z"); LedgerRow d = Row(1, "d", key: "k"); LedgerRow e = Timeout(1, "e", 1, key: "q");
        ConsumedRecord[] records = [Record(b, 0), Record(a, 1), Record(d, 2)];
        Assert.Equal(new OutcomeCounts(2, 1, 1, 1, 1, 2), OutcomeClassifier.Classify([a, b, c, d, e], records));
    }

    [Fact]
    public void Given_SameBareAttemptIdInBothInstances_When_Combined_Then_OnlyOwnLedgerJoinsAndSequenceTiesDoNotReorder()
    {
        LedgerRow a = Timeout(0, "same"); LedgerRow b = Row(1, "same");
        RunJudgementRequest first = Request(0, [a]) with { Records = [Record(a, 0)] };
        RunJudgementRequest second = Request(1, [b], [Planned(a)]) with { Records = [Record(b, 1)] };
        B10RepeatJudgement result = Evaluate(first, second);
        Assert.Equal(new OutcomeCounts(1, 0, 1, 0, 1, 0), result.InWindowCounts);
        second = second with { Records = [] };
        Assert.Equal(new OutcomeCounts(0, 1, 1, 0, 0, 0), Evaluate(first, second).InWindowCounts);
    }

    [Fact]
    public void Given_ValidEmptyPair_When_Reconciled_Then_InternalCountsAreSixZerosAndPublicationIsNonfinal()
    {
        B10RepeatJudgement result = Evaluate(Request(0, []), Request(1, []));
        Assert.Equal(new OutcomeCounts(0, 0, 0, 0, 0, 0), result.InWindowCounts);
        Assert.Equal(VerifierResultStatus.Incomplete, result.Judgement.Status);
        Assert.Null(result.Judgement.Counts);
        Assert.Empty(result.Judgement.FailedChecks);
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "b10_intake_only");
    }

    [Theory]
    [InlineData("Invalid", "CompleteValid", "Invalid")]
    [InlineData("Incomplete", "CompleteValid", "Incomplete")]
    [InlineData("CompleteValid", "Invalid", "Invalid")]
    [InlineData("CompleteValid", "Incomplete", "Incomplete")]
    [InlineData("Invalid", "Incomplete", "Invalid")]
    [InlineData("Incomplete", "Invalid", "Invalid")]
    public void Given_PublicationAdjustedStatusWithoutNumberedFailure_When_Reconciled_Then_PublicationRejectionStaysSticky(
        string firstStatus, string secondStatus, string expected)
    {
        RunJudgementResult first = Result() with { Status = Enum.Parse<VerifierResultStatus>(firstStatus), Diagnostics = [new(0, "publication_first", null, null, null, null)] };
        RunJudgementResult second = Result() with { Status = Enum.Parse<VerifierResultStatus>(secondStatus), Diagnostics = [new(0, "publication_second", null, null, null, null)] };
        B10RepeatJudgement result = Evaluate(Request(0, []), Request(1, []), first, second);
        Assert.Equal(Enum.Parse<VerifierResultStatus>(expected), result.Judgement.Status);
        Assert.Null(result.InWindowCounts);
        Assert.Null(result.Judgement.Counts);
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "publication_first");
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "publication_second");
    }

    [Theory]
    [InlineData("incomplete")] [InlineData("unavailable")] [InlineData("invalid")]
    public void Given_ExistingCheckFourOutcome_When_Merged_Then_BookkeepingGateRetainsOutcomeAndOldDiagnostic(string existing)
    {
        CheckAssessment check = existing == "unavailable" ? Unavailable(4) : RecordedEvidence.WindowFail(4,
            existing == "invalid" ? AssessmentClassification.Invalid : AssessmentClassification.Incomplete);
        RunJudgementResult first = WithAssessment(Result(), check) with
        {
            Status = existing == "invalid" ? VerifierResultStatus.Invalid : existing == "incomplete" ? VerifierResultStatus.Incomplete : null,
            FailedChecks = existing == "unavailable" ? [] : [4], Diagnostics = [new(4, "existing_four", "manifest.json", null, "old", null)]
        };
        B10RepeatJudgement result = Evaluate(Request(0, [Row(0, "a"), Row(0, "b", 1)]), Request(1, []), first);
        Assert.Equal(existing == "invalid" ? VerifierResultStatus.Invalid : VerifierResultStatus.Incomplete, result.Judgement.Status);
        Assert.Equal(existing == "unavailable" ? AssessmentEvaluation.NotEvaluated : AssessmentEvaluation.Evaluated,
            result.Judgement.Assessments.Single(a => a.CheckNumber == 4).Evaluation);
        if (existing == "unavailable") Assert.Empty(result.Judgement.FailedChecks);
        else Assert.Equal(4, Assert.Single(result.Judgement.FailedChecks));
        Assert.Contains(result.Judgement.Diagnostics, d => d.Code == "existing_four");
        Assert.DoesNotContain(result.Judgement.Diagnostics, d => d.Code == "b10_retry_requires_permitted_timeout");
        Assert.Null(result.InWindowCounts);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Given_CappedRetryDiagnosticsAndSharedWindow_When_Reconciled_Then_CapNeverAuthorizesCountsOrDropsFailedNumbers(int cap)
    {
        LedgerRow[] rows = [Row(0, "a"), Row(0, "b", 1), Row(0, "c", 2), Row(0, "d", 3)];
        RunJudgementResult prior = Result() with
        {
            FailedChecks = [10, 27], Status = VerifierResultStatus.Incomplete,
            Diagnostics = [new(4, "old_four", "manifest.json", null, "old", "\u00e9\u00e9a"), new(10, "window", "window", null, null, null)],
            ConfigurationFidelity = new(RecordedEvidence.WindowFail(27) with { Snippet = "\u00e9\u00e9a" }, [])
        };
        RunJudgementResult second = Result() with { Diagnostics = [new(10, "window", "window", null, null, null)], FailedChecks = [27],
            ConfigurationFidelity = new(RecordedEvidence.WindowFail(27) with { Snippet = "\u00e9\u00e9a" }, []) };
        B10RepeatJudgement result = Evaluate(Request(0, rows), Request(1, []), prior, second, Limits with { MaxDiagnosticsPerCheck = cap, MaxDiagnosticBytes = 4 });
        Assert.Equal(VerifierResultStatus.Invalid, result.Judgement.Status);
        Assert.Equal([4, 10, 27], result.Judgement.FailedChecks);
        Assert.Equal(cap, result.Judgement.Diagnostics.Count(d => d.Check == 4));
        Assert.Equal("\u00e9\u00e9", result.Judgement.Diagnostics.Single(d => d.Code == "old_four").Snippet);
        Assert.Single(result.Judgement.Diagnostics, d => d.Check == 10);
        Assert.True(result.Judgement.Diagnostics.Count(d => d.Check == 27) + 1 <= cap);
        Assert.Null(result.InWindowCounts);
    }

    private static B10RepeatJudgement Evaluate(RunJudgementRequest first, RunJudgementRequest second,
        RunJudgementResult? firstResult = null, RunJudgementResult? secondResult = null, OutputLimits? limits = null) =>
        B10RepeatChecks.Evaluate(first, second, firstResult ?? Result(), secondResult ?? Result(), Instances, limits ?? Limits);

    private static RunJudgementResult Result() => new(VerifierResultStatus.CompleteValid, [],
        Enumerable.Range(1, 27).Select(RecordedEvidence.WindowPass).ToArray(), new(0, 0, 0, 0, 0, 0), null,
        new(RecordedEvidence.WindowPass(27), []), []);

    private static RunJudgementResult WithAssessment(RunJudgementResult result, CheckAssessment assessment) => result with
    { Assessments = result.Assessments.Select(a => a.CheckNumber == assessment.CheckNumber ? assessment : a).ToArray() };

    private static CheckAssessment Unavailable(int check) => new(check, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null);

    private static RunJudgementRequest Request(int instance, LedgerRow[] rows, IReadOnlyList<RestartPlannedEntry>? plan = null)
    {
        RunJudgementRequest original = RecordedEvidence.SyntheticRequest();
        RunIdentity identity = Identity(instance);
        return original with
        {
            Identity = identity, Records = [],
            Index = ParseResult<ArtifactIndex>.Success(original.Index.Value! with { Identity = identity }),
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(rows),
            Manifest = ParseResult<SealedRunManifest>.Success(original.Manifest.Value! with
            {
                Identity = identity, PlannedAttemptCount = rows.Length, AttemptCount = rows.Length,
                Inventory = rows.Select(r => r.Attempt).ToArray(),
                Restart = instance == 1 ? new(Instances[0], plan ?? []) : null
            }),
            Capabilities = ParseResult<CapturedCapabilities>.Success(original.Capabilities.Value! with { Identity = identity })
        };
    }

    private static RunIdentity Identity(int instance) => new("B10", "C0", "run-1", Instances[instance], Instances[instance], $"producer-{instance}");

    private static AttemptEntry Attempt(int instance, string id, long ordinal = 0, long sequence = 0, int document = 0, string? key = null) =>
        new(Instances[instance], id, ordinal, "t0", $"d{document}", key ?? $"t0-d{document}", sequence, sequence switch { 0 => "uploaded", 1 => "ocr-started", 2 => "ocr-finished", 3 => "classified", 4 => "indexed", _ => "uploaded" }, 0, ordinal);

    private static LedgerRow Row(int instance, string id, long ordinal = 0, long sequence = 0, int document = 0, string? key = null) =>
        RecordedEvidence.LedgerRow(Attempt(instance, id, ordinal, sequence, document, key)) with { Identity = Identity(instance) };

    private static LedgerRow Timeout(int instance, string id, long ordinal = 0, long sequence = 0, int document = 0, string? key = null) =>
        Row(instance, id, ordinal, sequence, document, key) with { Delivered = false, ErrorCode = -192, PersistenceStatus = ParsedField<int>.FromValue(1) };

    private static RestartPlannedEntry Planned(LedgerRow row, long ordinal = 0) => new(ordinal, row.Attempt.AttemptId,
        row.Attempt.TenantId, row.Attempt.DocumentId, row.Attempt.Key, row.Attempt.Sequence, row.Attempt.EventType, row.Attempt.PlannedMicroseconds);

    private static ConsumedRecord Record(LedgerRow row, long offset) => new("topic", 0, offset, row.Attempt.Key, row.Attempt.InstanceId, row.Attempt.AttemptId, row.Attempt.Sequence, []);

    private static ParsedField<int> Persistence(string value) => value switch
    {
        "missing" => ParsedField<int>.Missing(), "null" => ParsedField<int>.Null(),
        _ => ParsedField<int>.FromValue(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
    };

    private static ParseResult<T> Missing<T>(int check) => ParseResult<T>.Failure(new StructuralProblem(check, "missing_input", "synthetic", null, null, null));

    private static RunJudgementRequest WithMarkers(RunJudgementRequest request, IReadOnlyList<string> markers) => request with
    { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { GuardObservations = markers }) };

    private static RunJudgementRequest WithReports(RunJudgementRequest request, IReadOnlyList<ReportAssociation> reports) => request with
    { Reports = reports, Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(r => r.Envelope).ToArray()) };

    private static NativeBrokerReport Counter(NativeBrokerReport broker, string counter, ParsedField<long> value) => counter switch
    {
        "req_timeouts" => broker with { RequestTimeouts = value }, "txretries" => broker with { TransmissionRetries = value },
        "txerrs" => broker with { TransmissionErrors = value }, "rxerrs" => broker with { ReceiveErrors = value },
        _ => throw new ArgumentOutOfRangeException(nameof(counter))
    };

    private static void AssertState(IReadOnlyList<CheckResult> results, int number, bool? pass)
    {
        CheckAssessment assessment = Assert.Single(results, r => r.Assessment.CheckNumber == number).Assessment;
        Assert.Equal(pass is null ? AssessmentEvaluation.NotEvaluated : AssessmentEvaluation.Evaluated, assessment.Evaluation);
        Assert.Equal(pass is null ? null : pass.Value ? AssessmentDisposition.Pass : AssessmentDisposition.Fail, assessment.Disposition);
        Assert.Equal(pass == false ? AssessmentClassification.Invalid : null, assessment.Classification);
    }

    private static void AssertRetryFailure(B10RepeatJudgement result, int instance, long row)
    {
        Assert.Equal(VerifierResultStatus.Invalid, result.Judgement.Status);
        Assert.Contains(4, result.Judgement.FailedChecks);
        Assert.Contains(new ResultDiagnostic(4, "b10_retry_requires_permitted_timeout", $"producer/{Instances[instance]}/ledger.jsonl", row, "attempt", null), result.Judgement.Diagnostics);
        Assert.Null(result.InWindowCounts);
        Assert.Null(result.Judgement.Counts);
    }

    private static void AssertPlanFailure(B10RepeatJudgement result)
    {
        Assert.Equal(VerifierResultStatus.Invalid, result.Judgement.Status);
        Assert.Contains(4, result.Judgement.FailedChecks);
        Assert.Contains(new ResultDiagnostic(4, "restart_plan_mismatch", "producer/second/manifest.json", null, "restart.planned", null), result.Judgement.Diagnostics);
        Assert.Null(result.InWindowCounts);
        Assert.Null(result.Judgement.Counts);
    }
}
