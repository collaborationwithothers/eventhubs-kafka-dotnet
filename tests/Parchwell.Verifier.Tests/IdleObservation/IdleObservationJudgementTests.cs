using System.Text;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.IdleObservation;
using Xunit;
using Broker = (string Key, long NodeId, string Source, string State, long TxIdle, long RxIdle, long Connects, long Disconnects, long Timeouts, long Metadata);
using static Parchwell.Verifier.Tests.IdleObservation.IdleFixture;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.IdleObservation;

public sealed class IdleObservationJudgementTests
{
    private static Broker Closing(long elapsed, Broker broker, long closedFrom, long beforeTx, long beforeRx, long beforeAt) =>
        elapsed == beforeAt ? broker with { TxIdle = beforeTx, RxIdle = beforeRx } : elapsed >= closedFrom ? broker with { Disconnects = 1 } : broker;

    [Fact]
    public void Given_DefaultReports_When_Judged_Then_ConnectionIsKeptAndEveryCountIsZero()
    {
        var result = JudgeOne(Repeat());
        Assert.Equal("complete_valid", result.Status);
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(new IdleWindows(0, 0, 0, 0, 0, null, null, 0, 0, 0, 0, false), result.Windows);
        Assert.Equal(2, result.MaximumUpBrokers);
    }

    [Theory]
    [InlineData(225000000L, 230000000L, "closed_while_idle", 1L, 0L, 225000L)]
    [InlineData(224999999L, 230000000L, "kept", 0L, 1L, 224999L)]
    [InlineData(300000000L, 224999999L, "kept", 0L, 1L, 224999L)]
    [InlineData(-1L, 225000000L, "closed_while_idle", 1L, 0L, 225000L)]
    [InlineData(224999999L, -1L, "kept", 0L, 1L, 224999L)]
    [InlineData(-1L, -1L, "kept", 0L, 1L, 0L)]
    public void Given_IdleDisconnect_When_QuietPeriodIsRead_Then_SmallerReadingDecidesCause(long tx, long rx, string expected, long timed, long other, long quiet)
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => Closing(elapsed, broker, 255000, tx, rx, 240000))));
        Assert.Equal("complete_valid", result.Status);
        Assert.Equal(expected, result.ConnectionClass);
        Assert.Equal(timed, result.Windows!.IdleTimedDisconnects);
        Assert.Equal(other, result.Windows.IdleOtherCauseDisconnects);
        Assert.Equal(253000, result.Windows.FirstCloseAfterIdleMs);
        Assert.Equal(quiet, result.Windows.FirstCloseQuietMs);
    }

    [Fact]
    public void Given_DisconnectAfterIdleEnd_When_QuietPeriodIsLong_Then_ConnectionIsStaleOnResume()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => Closing(elapsed, broker, 435000, 405000000, 405000000, 420000))));
        Assert.Equal("stale_on_resume", result.ConnectionClass);
        Assert.Equal(1, result.Windows!.ResumeIdleTimedDisconnects);
        Assert.Null(result.Windows.FirstCloseAfterIdleMs);
    }

    [Fact]
    public void Given_ResumeDisconnectWithShortQuietPeriod_When_Judged_Then_ItIsOtherCauseAndTimeoutIsRecorded()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed >= 435000 ? broker with { Disconnects = 1, Timeouts = 1 } : broker)));
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(1, result.Windows!.ResumeOtherCauseDisconnects);
        Assert.Equal(1, result.Windows.ResumeRequestTimeouts);
    }

    private static readonly long[] ResumeTimes = [.. Enumerable.Range(1, 33).Select(index => index * 15000L)];

    private static IdleRepeat ResumeRepeat(SingleActionStatistic[] statistics) => Repeat(statistics) with { Second = Produce(2, 422000, 480000) };

    [Theory]
    [InlineData(435000L, 435000L, 435000L, "timed_out_on_resume", true)]
    [InlineData(435000L, 450000L, 465000L, "timed_out_on_resume", true)]
    [InlineData(450000L, 450000L, 480000L, "timed_out_on_resume", true)]
    [InlineData(435000L, 465000L, 450000L, "kept", false)]
    [InlineData(450000L, 435000L, 465000L, "kept", false)]
    [InlineData(435000L, 435000L, 495000L, "kept", false)]
    [InlineData(420000L, 435000L, 435000L, "kept", false)]
    [InlineData(435000L, long.MaxValue, 435000L, "kept", false)]
    [InlineData(435000L, 435000L, long.MaxValue, "kept", false)]
    [InlineData(long.MaxValue, 435000L, 435000L, "kept", false)]
    public void Given_ResumeTimeoutDisconnectAndConnect_When_ReportOrderVaries_Then_OnlyThatOrderOnOneKeyIsTimedOutOnResume(
        long timeoutFrom, long disconnectFrom, long connectFrom, string expected, bool sequence)
    {
        var result = JudgeOne(ResumeRepeat(Node8Series((elapsed, broker) => broker with
        {
            Timeouts = elapsed >= timeoutFrom ? 1 : 0,
            Disconnects = elapsed >= disconnectFrom ? 1 : 0,
            Connects = elapsed >= connectFrom ? 2 : 1
        }, ResumeTimes)));
        Assert.Equal("complete_valid", result.Status);
        Assert.Equal(expected, result.ConnectionClass);
        Assert.Equal(sequence, result.Windows!.ResumeTimeoutReconnect);
        Assert.Equal(0, result.Windows.ResumeIdleTimedDisconnects);
    }

    [Fact]
    public void Given_TimeoutOnOneKeyAndReconnectOnAnother_When_Judged_Then_ConnectionIsKept()
    {
        var result = JudgeOne(ResumeRepeat(Series(elapsed => elapsed >= 435000
            ? [Bootstrap() with { Disconnects = 1, Connects = 2 }, Node8() with { Timeouts = 1 }]
            : [Bootstrap(), Node8()], ResumeTimes)));
        Assert.Equal("kept", result.ConnectionClass);
        Assert.False(result.Windows!.ResumeTimeoutReconnect);
        Assert.Equal((1L, 1L, 1L), (result.Windows.ResumeOtherCauseDisconnects, result.Windows.ResumeConnects, result.Windows.ResumeRequestTimeouts));
    }

    [Fact]
    public void Given_KeyFirstSeenInResumeWindowWithTimeoutDisconnectAndConnect_When_Judged_Then_AbsentCountsAsZero()
    {
        var result = JudgeOne(ResumeRepeat(Series(elapsed => elapsed >= 435000
            ? [Bootstrap(), Node8(), ("n:9093/9", 9, "learned", "UP", 1000000, 1000000, 1, 1, 1, 0)]
            : [Bootstrap(), Node8()], ResumeTimes)));
        Assert.Equal("timed_out_on_resume", result.ConnectionClass);
        Assert.True(result.Windows!.ResumeTimeoutReconnect);
    }

    [Fact]
    public void Given_ResumeIdleTimedDisconnectWithTimeoutAndReconnect_When_Judged_Then_StaleOnResumeTakesPrecedence()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed == 420000 ? broker with { TxIdle = 405000000, RxIdle = 405000000 }
            : elapsed >= 435000 ? broker with { Timeouts = 1, Disconnects = 1, Connects = 2 } : broker)));
        Assert.Equal("stale_on_resume", result.ConnectionClass);
        Assert.True(result.Windows!.ResumeTimeoutReconnect);
        Assert.Equal(1, result.Windows.ResumeIdleTimedDisconnects);
    }

    [Fact]
    public void Given_IdleTimedCloseAndResumeTimeoutSequence_When_Judged_Then_ClosedWhileIdleTakesPrecedence()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed == 240000 ? broker with { TxIdle = 230000000, RxIdle = 230000000 }
            : elapsed >= 435000 ? broker with { Disconnects = 2, Connects = 2, Timeouts = 1 } : elapsed >= 255000 ? broker with { Disconnects = 1 } : broker)));
        Assert.Equal("closed_while_idle", result.ConnectionClass);
        Assert.True(result.Windows!.ResumeTimeoutReconnect);
    }

    [Fact]
    public void Given_BrokerKeyFirstSeenWithDisconnect_When_Judged_Then_QuietPeriodIsZeroAndCauseIsOther()
    {
        var result = JudgeOne(Repeat(Series(elapsed => elapsed >= 255000
            ? [Bootstrap(), Node8(), ("n:9093/9", 9, "learned", "UP", 1000000, 1000000, 1, 1, 0, 0)]
            : [Bootstrap(), Node8()])));
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(1, result.Windows!.IdleOtherCauseDisconnects);
        Assert.Equal(0, result.Windows.FirstCloseQuietMs);
    }

    [Fact]
    public void Given_ClientCloseShape_When_QuietPeriodIsBelowIdleTimedThreshold_Then_ItIsOtherCauseWithItsQuietPeriod()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => Closing(elapsed, broker, 195000, 163000000, 170000000, 180000))));
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(193000, result.Windows!.FirstCloseAfterIdleMs);
        Assert.Equal(163000, result.Windows.FirstCloseQuietMs);
    }

    [Fact]
    public void Given_ConnectIncrease_When_NoDisconnect_Then_IdleConnectsCountsItAndConnectionIsKept()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed >= 255000 ? broker with { Connects = 2 } : broker)));
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(1, result.Windows!.IdleConnects);
    }

    [Fact]
    public void Given_MetadataRequests_When_Judged_Then_IdleWindowDeltaIsCounted()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed >= 405000 ? broker with { Metadata = 2 } : elapsed >= 300000 ? broker with { Metadata = 1 } : broker)));
        Assert.Equal(2, result.Windows!.IdleMetadataRequests);
    }

    [Fact]
    public void Given_DecreasingCounter_When_Judged_Then_RepeatIsInvalid()
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => elapsed is >= 255000 and < 270000 ? broker with { Disconnects = 1 } : broker)));
        Assert.Equal("invalid", result.Status);
        Assert.Contains("idle_counter_decreased", result.Reasons);
    }

    private static Broker WithCounter(Broker broker, string counter, long value) => counter switch
    {
        "connects" => broker with { Connects = value },
        "disconnects" => broker with { Disconnects = value },
        "timeouts" => broker with { Timeouts = value },
        _ => broker with { Metadata = value }
    };

    [Theory]
    [InlineData("connects", -1L, "invalid")]
    [InlineData("connects", 0L, "complete_valid")]
    [InlineData("connects", 1L, "complete_valid")]
    [InlineData("disconnects", -1L, "invalid")]
    [InlineData("disconnects", 0L, "complete_valid")]
    [InlineData("disconnects", 1L, "complete_valid")]
    [InlineData("timeouts", -1L, "invalid")]
    [InlineData("timeouts", 0L, "complete_valid")]
    [InlineData("timeouts", 1L, "complete_valid")]
    [InlineData("metadata", -1L, "invalid")]
    [InlineData("metadata", 0L, "complete_valid")]
    [InlineData("metadata", 1L, "complete_valid")]
    public void Given_EachCounter_When_ItChangesByOne_Then_OnlyADecreaseIsInvalid(string counter, long change, string status)
    {
        var result = JudgeOne(Repeat(Node8Series((elapsed, broker) => WithCounter(broker, counter, elapsed >= 255000 ? 5 + change : 5))));
        Assert.Equal(status, result.Status);
        string[] reasons = status == "invalid" ? ["idle_counter_decreased"] : [];
        Assert.Equal(reasons, result.Reasons);
    }

    [Theory]
    [InlineData(22000L, true)]
    [InlineData(22001L, false)]
    public void Given_FirstReportTime_When_Judged_Then_StartReferenceSlackIs20000Ms(long first, bool complete)
    {
        long[] times = [first, .. DefaultTimes.Skip(1)];
        AssertReference(JudgeOne(Repeat(Series(times: times))), complete);
    }

    [Theory]
    [InlineData(402000L, true)]
    [InlineData(401999L, false)]
    public void Given_LastReportBeforeEnd_When_Judged_Then_EndReferenceSlackIs20000Ms(long last, bool complete)
    {
        long[] times = [.. DefaultTimes.Where(time => time <= 390000), last, 435000];
        AssertReference(JudgeOne(Repeat(Series(times: times))), complete);
    }

    [Theory]
    [InlineData(405000L, false)]
    [InlineData(405001L, true)]
    [InlineData(420000L, true)]
    [InlineData(420001L, true)]
    [InlineData(435000L, true)]
    [InlineData(435001L, false)]
    public void Given_SecondTerminalTime_When_Judged_Then_ResumeReportMustNotPrecedeLastIdleReport(long terminal, bool complete)
    {
        IdleRepeat repeat = Repeat();
        AssertReference(JudgeOne(repeat with { Second = repeat.Second! with { TerminalElapsedMs = terminal } }), complete);
    }

    [Fact]
    public void Given_NoReportAtOrAfterSecondTerminal_When_Judged_Then_ReferenceIsMissing()
    {
        AssertReference(JudgeOne(Repeat(Series(times: DefaultTimes.Where(time => time <= 420000)))), false);
    }

    [Fact]
    public void Given_NoStatisticsReportsAndSuccessfulFirstProduce_When_Judged_Then_ReferenceIsMissing()
    {
        var result = JudgeOne(Repeat([]));
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(["idle_reference_missing"], result.Reasons);
        Assert.Null(result.ConnectionClass);
        Assert.Null(result.Windows);
        Assert.Equal(0, result.MaximumUpBrokers);
        Assert.Empty(result.Reports);
    }

    [Fact]
    public void Given_ReportsWithEmptyBrokersObject_When_Judged_Then_ConnectionIsKeptWithNoUpBrokers()
    {
        var result = JudgeOne(Repeat(Series(_ => [])));
        Assert.Equal("complete_valid", result.Status);
        Assert.Empty(result.Reasons);
        Assert.Equal("kept", result.ConnectionClass);
        Assert.Equal(new IdleWindows(0, 0, 0, 0, 0, null, null, 0, 0, 0, 0, false), result.Windows);
        Assert.Equal(0, result.MaximumUpBrokers);
        Assert.Equal(DefaultTimes.Length, result.Reports.Length);
    }

    private static void AssertReference(IdleRepeatResult result, bool complete)
    {
        if (complete) { Assert.Equal("complete_valid", result.Status); return; }
        Assert.Equal("incomplete", result.Status);
        Assert.Contains("idle_reference_missing", result.Reasons);
    }

    private static string Default8Json() => BrokersJson(Bootstrap(), Node8());

    public static TheoryData<string> BadShapes => new()
    {
        { Default8Json().Replace(",\"Metadata\":0", "", StringComparison.Ordinal) },
        { Default8Json().Replace("\"rxidle\":1000000,", "", StringComparison.Ordinal) },
        { Default8Json().Replace("\"txidle\":1000000", "\"txidle\":-2", StringComparison.Ordinal) },
        { Default8Json().Replace("\"rxidle\":1000000", "\"rxidle\":-2", StringComparison.Ordinal) },
        { Default8Json().Replace("\"connects\":1", "\"connects\":\"1\"", StringComparison.Ordinal) },
    };

    [Theory, MemberData(nameof(BadShapes))]
    public void Given_MalformedReport_When_Judged_Then_RepeatIsInvalidForShape(string json)
    {
        SingleActionStatistic[] statistics = Series();
        statistics[0] = Raw(1, statistics[0].ObservedElapsedMs, json);
        var result = JudgeOne(Repeat(statistics));
        Assert.Equal("invalid", result.Status);
        Assert.Contains("idle_statistics_shape", result.Reasons);
    }

    [Theory]
    [InlineData("base64")]
    [InlineData("utf8")]
    [InlineData("brokers")]
    public void Given_UndecodableReport_When_Judged_Then_RepeatIsInvalidForShape(string kind)
    {
        SingleActionStatistic[] statistics = Series();
        statistics[0] = kind switch
        {
            "base64" => new(1, statistics[0].ObservedElapsedMs, "!!"),
            "utf8" => new(1, statistics[0].ObservedElapsedMs, Convert.ToBase64String([0xFF, 0xFE, 0xFD])),
            _ => Report(1, statistics[0].ObservedElapsedMs, [.. Enumerable.Range(0, 65).Select(index => ($"h:9093/{index}", (long)index, "learned", "UP", 1000000L, 1000000L, 1L, 0L, 0L, 0L))])
        };
        var result = JudgeOne(Repeat(statistics), maxBrokers: 64);
        Assert.Equal("invalid", result.Status);
        Assert.Contains("idle_statistics_shape", result.Reasons);
    }

    [Fact]
    public void Given_SixtyFourBrokers_When_Judged_Then_ShapeIsAccepted()
    {
        SingleActionStatistic[] statistics = Series();
        statistics[0] = Report(1, statistics[0].ObservedElapsedMs, [.. Enumerable.Range(0, 64).Select(index => ($"h:9093/{index}", (long)index, "learned", "UP", 1000000L, 1000000L, 1L, 0L, 0L, 0L))]);
        Assert.DoesNotContain("idle_statistics_shape", JudgeOne(Repeat(statistics), maxBrokers: 64).Reasons);
    }

    [Fact]
    public void Given_OversizedReport_When_Parsed_Then_ShapeIsRejected()
    {
        SingleActionStatistic statistic = Raw(1, 15000, Default8Json());
        Assert.Throws<FormatException>(() => IdleObservationJudgement.ParseReport(statistic, 10, 64));
    }

    [Fact]
    public void Given_FixtureReport_When_SizeIsMeasured_Then_ItIs358Bytes()
    {
        Assert.Equal(358, Encoding.UTF8.GetByteCount(Default8Json()));
    }

    [Theory]
    [InlineData(357, false)]
    [InlineData(358, true)]
    [InlineData(359, true)]
    public void Given_ReportSizeAroundLimit_When_Parsed_Then_OnlyAReportAboveTheLimitIsRejected(int maxBytes, bool accepted)
    {
        SingleActionStatistic statistic = Raw(1, 15000, Default8Json());
        if (accepted) Assert.Equal(2, IdleObservationJudgement.ParseReport(statistic, maxBytes, 64).Brokers.Count);
        else Assert.Throws<FormatException>(() => IdleObservationJudgement.ParseReport(statistic, maxBytes, 64));
    }

    [Theory]
    [InlineData(357, "invalid")]
    [InlineData(358, "complete_valid")]
    [InlineData(359, "complete_valid")]
    public void Given_ReportSizeAroundLimit_When_RepeatIsJudged_Then_TheJudgeParameterSetsTheLimit(int maxReportBytes, string status)
    {
        var result = IdleObservationJudgement.JudgeRepeat(Repeat(), 1, "p", DefaultSettings, [], 420, maxReportBytes, 64);
        Assert.Equal(status, result.Status);
        string[] reasons = status == "invalid" ? ["idle_statistics_shape"] : [];
        Assert.Equal(reasons, result.Reasons);
    }

    [Fact]
    public void Given_SequenceGap_When_Judged_Then_OrderIsInvalid()
    {
        SingleActionStatistic[] statistics = Series(times: [15000, 30000]);
        statistics[1] = statistics[1] with { Sequence = 3 };
        Assert.Contains("idle_statistics_order", JudgeOne(Repeat(statistics)).Reasons);
    }

    [Fact]
    public void Given_DecreasingElapsed_When_Judged_Then_OrderIsInvalid()
    {
        Assert.Contains("idle_statistics_order", JudgeOne(Repeat(Series(times: [15000, 30000, 29999]))).Reasons);
    }

    [Theory]
    [InlineData(29999L, "invalid")]
    [InlineData(30000L, "complete_valid")]
    [InlineData(30001L, "complete_valid")]
    public void Given_ThirdReportTimeAroundSecond_When_Judged_Then_OnlyADecreaseIsOutOfOrder(long third, string status)
    {
        long[] times = [15000, 30000, third, .. DefaultTimes.Skip(3)];
        var result = JudgeOne(Repeat(Series(times: times)));
        Assert.Equal(status, result.Status);
        string[] reasons = status == "invalid" ? ["idle_statistics_order"] : [];
        Assert.Equal(reasons, result.Reasons);
    }

    [Theory]
    [InlineData("repeat")]
    [InlineData("client")]
    [InlineData("extra_setting")]
    [InlineData("changed_value")]
    [InlineData("omission")]
    public void Given_IdentityOrSettingsMismatch_When_Judged_Then_RepeatIsInvalid(string kind)
    {
        IdleRepeat repeat = Repeat();
        string reason = "idle_identity";
        switch (kind)
        {
            case "repeat": repeat = repeat with { Repeat = 2 }; break;
            case "client": repeat = repeat with { ClientId = "p-r2" }; break;
            case "extra_setting": repeat.Settings["extra"] = "1"; reason = "idle_settings"; break;
            case "changed_value": repeat.Settings["acks"] = "1"; reason = "idle_settings"; break;
            default: repeat = repeat with { OmittedKeys = ["x"] }; reason = "idle_settings"; break;
        }
        var result = JudgeOne(repeat);
        Assert.Equal("invalid", result.Status);
        Assert.Contains(reason, result.Reasons);
    }

    [Fact]
    public void Given_FirstProduceError_When_Judged_Then_ClassIsFirstFailedWithoutReports()
    {
        var result = JudgeOne(Repeat([]) with { First = Produce(1, 0, 1000, "error") });
        Assert.Equal("complete_valid", result.Status);
        Assert.Equal("first_failed", result.ConnectionClass);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_FirstProduceNotTerminal_When_Judged_Then_RepeatIsIncomplete(bool missing)
    {
        IdleRepeat repeat = Repeat();
        repeat = repeat with { First = missing ? null : repeat.First! with { Terminal = false } };
        var result = JudgeOne(repeat);
        Assert.Equal("incomplete", result.Status);
        Assert.Contains("idle_first_not_terminal", result.Reasons);
    }

    [Fact]
    public void Given_SecondProduceNotTerminal_When_Judged_Then_RepeatIsIncomplete()
    {
        IdleRepeat repeat = Repeat();
        var result = JudgeOne(repeat with { Second = repeat.Second! with { Terminal = false } });
        Assert.Equal("incomplete", result.Status);
        Assert.Contains("idle_second_not_terminal", result.Reasons);
    }

    [Theory]
    [InlineData(421999L, "invalid")]
    [InlineData(422000L, "complete_valid")]
    public void Given_IdleSpan_When_Judged_Then_420000MsIsTheMinimum(long end, string status)
    {
        var result = JudgeOne(Repeat() with { IdleEndedElapsedMs = end });
        Assert.Equal(status, result.Status);
        if (status == "invalid") Assert.Contains("idle_window", result.Reasons);
    }

    [Fact]
    public void Given_Diagnostics_When_Judged_Then_RepeatIsIncomplete()
    {
        var result = JudgeOne(Repeat() with { Diagnostics = ["action_window_elapsed:p2"] });
        Assert.Equal("incomplete", result.Status);
        Assert.Contains("idle_diagnostics", result.Reasons);
    }

    private static readonly int[] ShuffledOrder = [1, 2, 3, 5, 4];
    private static readonly string[] IdentityReason = ["idle_capture_identity"], CompletionReason = ["idle_completion"], MissingReason = ["idle_capture_missing"];

    private static void AssertProblems(string[] reasons, bool invalid, (string[] Reasons, bool Invalid) actual)
    {
        Assert.Equal(reasons, actual.Reasons);
        Assert.Equal(invalid, actual.Invalid);
    }

    private static (string[] Reasons, bool Invalid) Problems(SingleActionRegistration? registration, IdleCapture? capture, SingleActionCompletion? completion, string runKind = "batch") =>
        IdleObservationJudgement.CaptureProblems(registration, capture, completion, "run1", "A02", "C0", "p", "uid", CommitSha, runKind);

    [Fact]
    public void Given_ValidCapture_When_Checked_Then_NoProblemIsReported()
    {
        AssertProblems([], false, Problems(Registration(), Capture(), Completion()));
    }

    public static TheoryData<string> IdentityMutations => new()
    {
        "reg_schema", "reg_mode", "reg_scenario", "reg_configuration", "reg_run", "reg_instance", "reg_job", "reg_pod", "reg_uid", "reg_commit",
        "comp_schema", "comp_mode", "comp_scenario", "comp_configuration", "comp_run", "comp_instance", "comp_job", "comp_pod", "comp_uid", "comp_commit",
        "cap_schema", "cap_mode", "cap_scenario", "cap_configuration", "cap_run", "cap_instance", "cap_job", "cap_pod", "cap_uid", "cap_commit",
        "idle_seconds", "interval", "max_reports", "partition", "payload", "topic", "synthetic", "run_kind", "repeat_order"
    };

    [Theory, MemberData(nameof(IdentityMutations))]
    public void Given_ChangedIdentityField_When_Checked_Then_CaptureIdentityIsInvalid(string mutation)
    {
        SingleActionRegistration registration = Registration();
        SingleActionCompletion completion = Completion();
        IdleCapture capture = Capture();
        string runKind = "batch";
        static SingleActionIdentity Id(SingleActionIdentity id, string field) => field switch
        {
            "scenario" => id with { ScenarioId = "A03" },
            "configuration" => id with { ConfigurationId = "C1" },
            "run" => id with { RunId = "run2" },
            _ => id with { InstanceId = "q" }
        };
        switch (mutation)
        {
            case "reg_schema": registration = registration with { SchemaVersion = 2 }; break;
            case "reg_mode": registration = registration with { Mode = "idle-observatioN" }; break;
            case "reg_scenario": registration = registration with { Identity = Id(registration.Identity, "scenario") }; break;
            case "reg_configuration": registration = registration with { Identity = Id(registration.Identity, "configuration") }; break;
            case "reg_run": registration = registration with { Identity = Id(registration.Identity, "run") }; break;
            case "reg_instance": registration = registration with { Identity = Id(registration.Identity, "instance") }; break;
            case "reg_job": registration = registration with { JobName = registration.JobName + "x" }; break;
            case "reg_pod": registration = registration with { PodName = "q" }; break;
            case "reg_uid": registration = registration with { PodUid = "uie" }; break;
            case "reg_commit": registration = registration with { SourceCommit = new string('2', 40) }; break;
            case "comp_schema": completion = completion with { SchemaVersion = 2 }; break;
            case "comp_mode": completion = completion with { Mode = "idle-observatioN" }; break;
            case "comp_scenario": completion = completion with { Identity = Id(completion.Identity, "scenario") }; break;
            case "comp_configuration": completion = completion with { Identity = Id(completion.Identity, "configuration") }; break;
            case "comp_run": completion = completion with { Identity = Id(completion.Identity, "run") }; break;
            case "comp_instance": completion = completion with { Identity = Id(completion.Identity, "instance") }; break;
            case "comp_job": completion = completion with { JobName = completion.JobName + "x" }; break;
            case "comp_pod": completion = completion with { PodName = "q" }; break;
            case "comp_uid": completion = completion with { PodUid = "uie" }; break;
            case "comp_commit": completion = completion with { SourceCommit = new string('2', 40) }; break;
            case "cap_schema": capture = capture with { SchemaVersion = 2 }; break;
            case "cap_mode": capture = capture with { Mode = "idle-observatioN" }; break;
            case "cap_scenario": capture = capture with { Identity = Id(capture.Identity, "scenario") }; break;
            case "cap_configuration": capture = capture with { Identity = Id(capture.Identity, "configuration") }; break;
            case "cap_run": capture = capture with { Identity = Id(capture.Identity, "run") }; break;
            case "cap_instance": capture = capture with { Identity = Id(capture.Identity, "instance") }; break;
            case "cap_job": capture = capture with { JobName = capture.JobName + "x" }; break;
            case "cap_pod": capture = capture with { PodName = "q" }; break;
            case "cap_uid": capture = capture with { PodUid = "uie" }; break;
            case "cap_commit": capture = capture with { SourceCommit = new string('2', 40) }; break;
            case "idle_seconds": capture = capture with { IdleSeconds = 421 }; break;
            case "interval": capture = capture with { StatisticsIntervalMs = 15001 }; break;
            case "max_reports": capture = capture with { MaxStatisticsReports = 81 }; break;
            case "partition": capture = capture with { Partition = 1 }; break;
            case "payload": capture = capture with { PayloadBytes = 999 }; break;
            case "topic": capture = capture with { Topic = "x" }; break;
            case "synthetic": capture = capture with { Synthetic = false }; break;
            case "run_kind": runKind = "smoke"; break;
            default: capture = capture with { Repeats = [.. ShuffledOrder.Select(ordinal => Repeat() with { Repeat = ordinal })] }; break;
        }
        AssertProblems(IdentityReason, true, Problems(registration, capture, completion, runKind));
    }

    [Fact]
    public void Given_CaptureWithNoRepeats_When_Checked_Then_CaptureIdentityIsInvalid()
    {
        AssertProblems(IdentityReason, true, Problems(Registration(), Capture() with { Repeats = [] }, Completion()));
    }

    [Theory]
    [InlineData("attempted")]
    [InlineData("exit")]
    [InlineData("cleanup")]
    public void Given_IncompleteCompletion_When_Checked_Then_CompletionIsReported(string field)
    {
        SingleActionCompletion completion = field switch
        {
            "attempted" => Completion() with { RepeatsAttempted = 4 },
            "exit" => Completion() with { HandlerExitCode = 1 },
            _ => Completion() with { CleanupCompleted = false }
        };
        AssertProblems(CompletionReason, false, Problems(Registration(), Capture(), completion));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_AnyMissingBlob_When_Checked_Then_CaptureIsMissing(int missing)
    {
        AssertProblems(MissingReason, false,
            Problems(missing == 0 ? null : Registration(), missing == 1 ? null : Capture(), missing == 2 ? null : Completion()));
    }

    private static IdleRepeatResult[] Five(string connectionClass) => [.. Enumerable.Range(1, 5).Select(_ => Result(connectionClass))];

    [Fact]
    public void Given_AllKept_When_ConfigurationIsSummarised_Then_SummaryIsKept()
    {
        var result = IdleObservationJudgement.Configuration("C0", [], false, Five("kept"), "abc");
        Assert.Equal(("complete_valid", "kept", "abc"), (result.Status, result.Summary, result.CaptureSha256));
    }

    [Fact]
    public void Given_BothClosingClasses_When_ConfigurationIsSummarised_Then_SummaryIsNotKept()
    {
        IdleRepeatResult[] repeats = [Result("closed_while_idle"), Result("stale_on_resume"), Result("closed_while_idle"), Result("closed_while_idle"), Result("stale_on_resume")];
        Assert.Equal("not_kept", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Summary);
    }

    [Fact]
    public void Given_FirstFailedRepeat_When_ConfigurationIsSummarised_Then_SummaryIsFirstFailed()
    {
        IdleRepeatResult[] repeats = [Result("first_failed"), .. Five("kept").Skip(1)];
        Assert.Equal("first_failed", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Summary);
    }

    [Fact]
    public void Given_FourKeptAndOneClosed_When_ConfigurationIsSummarised_Then_SummaryIsUnequal()
    {
        IdleRepeatResult[] repeats = [Result("closed_while_idle"), .. Five("kept").Skip(1)];
        Assert.Equal("unequal", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Summary);
    }

    [Fact]
    public void Given_EveryClosingClass_When_ConfigurationIsSummarised_Then_SummaryIsNotKept()
    {
        IdleRepeatResult[] repeats = [Result("closed_while_idle"), Result("stale_on_resume"), Result("timed_out_on_resume"), Result("timed_out_on_resume"), Result("closed_while_idle")];
        Assert.Equal("not_kept", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Summary);
    }

    [Fact]
    public void Given_FourKeptAndOneTimedOutOnResume_When_ConfigurationIsSummarised_Then_SummaryIsUnequal()
    {
        IdleRepeatResult[] repeats = [Result("timed_out_on_resume"), .. Five("kept").Skip(1)];
        Assert.Equal("unequal", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Summary);
    }

    [Fact]
    public void Given_OneIncompleteRepeat_When_ConfigurationIsSummarised_Then_StatusIsIncompleteWithoutSummary()
    {
        IdleRepeatResult[] repeats = [Result("kept", status: "incomplete"), .. Five("kept").Skip(1)];
        var result = IdleObservationJudgement.Configuration("C0", [], false, repeats, null);
        Assert.Equal(("incomplete", null), (result.Status, result.Summary));
    }

    [Fact]
    public void Given_InvalidRepeat_When_ConfigurationIsSummarised_Then_StatusIsInvalid()
    {
        IdleRepeatResult[] repeats = [Result("kept", status: "invalid"), .. Five("kept").Skip(1)];
        Assert.Equal("invalid", IdleObservationJudgement.Configuration("C0", [], false, repeats, null).Status);
    }

    [Fact]
    public void Given_CaptureProblem_When_ConfigurationIsSummarised_Then_EveryRepeatCarriesTheReason()
    {
        var result = IdleObservationJudgement.Configuration("C0", ["idle_capture_identity"], true, [], null);
        Assert.Equal("invalid", result.Status);
        Assert.All(result.Repeats, repeat => Assert.Equal(["idle_capture_identity"], repeat.Reasons));
        Assert.Equal(5, result.Repeats.Length);
    }

    private static Dictionary<string, IdleConfigurationResult> Results(params (string Id, string? Summary)[] summaries) =>
        summaries.ToDictionary(pair => pair.Id, pair => Summary(pair.Id, pair.Summary), StringComparer.Ordinal);

    [Theory]
    [InlineData("A02", "not_kept", "not_kept", "contradicted", "closed_with_keepalive", "contradicted")]
    [InlineData("A02", "not_kept", "kept", "confirmed", "keepalive_kept_connection", "holds")]
    [InlineData("A02", "kept", "kept", "untestable", "no_idle_close_observed", "undecided")]
    [InlineData("A02", "kept", "not_kept", null, "inverse_effect", "undecided")]
    [InlineData("A03", "not_kept", "not_kept", "contradicted", "closed_with_microsoft_value", "contradicted")]
    [InlineData("A03", "not_kept", "kept", "confirmed", "microsoft_value_kept_connection", "holds")]
    [InlineData("A03", "kept", "kept", "untestable", "no_idle_close_observed", "undecided")]
    [InlineData("A03", "kept", "not_kept", null, "inverse_effect", "undecided")]
    public void Given_KeepaliveOrMetadataItem_When_Assessed_Then_RuleGivesVerdict(string item, string c0, string c1, string? verdict, string reason, string decided)
    {
        var results = item == "A03" ? Results(("C0", c0), ("C1", c1), ("C2", "kept")) : Results(("C0", c0), ("C1", c1));
        var assessment = IdleObservationJudgement.Assess(item, "batch", results);
        Assert.Equal((verdict, reason), (assessment.Verdict, assessment.Reason));
        string first = item == "A02" ? "H_keepalive" : "H_metadata_age";
        Assert.Equal(decided, assessment.Hypotheses[first]);
        Assert.All(assessment.Hypotheses.Where(pair => pair.Key != first), pair => Assert.Equal("recorded_only", pair.Value));
    }

    [Fact]
    public void Given_A02Hypotheses_When_Assessed_Then_ResumeIsRecordedOnly()
    {
        var assessment = IdleObservationJudgement.Assess("A02", "batch", Results(("C0", "not_kept"), ("C1", "not_kept")));
        Assert.Equal("recorded_only", assessment.Hypotheses["H_resume"]);
        Assert.Equal(2, assessment.Hypotheses.Count);
    }

    [Fact]
    public void Given_OnlyOtherCauseDisconnects_When_A02IsAssessed_Then_NoIdleCloseIsObserved()
    {
        var other = JudgeOne(Repeat(Node8Series((elapsed, broker) => Closing(elapsed, broker, 255000, 224999999, 230000000, 240000))));
        IdleRepeatResult[] repeats = [.. Enumerable.Range(1, 5).Select(_ => other)];
        var c0 = IdleObservationJudgement.Configuration("C0", [], false, repeats, null);
        var c1 = IdleObservationJudgement.Configuration("C1", [], false, repeats, null);
        var assessment = IdleObservationJudgement.Assess("A02", "batch", new Dictionary<string, IdleConfigurationResult> { ["C0"] = c0, ["C1"] = c1 });
        Assert.Equal(("untestable", "no_idle_close_observed"), (assessment.Verdict, assessment.Reason));
    }

    private static Dictionary<string, IdleConfigurationResult> A04(long closeAfter, long? quiet, bool disconnect = true)
    {
        IdleRepeatResult[] repeats = [.. Enumerable.Range(1, 5).Select(ordinal => ordinal < 5
            ? Result("kept", CloseWindows(193000, 170000))
            : Result("kept", disconnect ? CloseWindows(closeAfter, quiet) : CloseWindows(null, null)))];
        return new(StringComparer.Ordinal) { ["C0"] = Summary("C0", "kept"), ["C1"] = Summary("C1", "kept", repeats: repeats) };
    }

    [Theory]
    [InlineData(224999L, 163000L, true, "holds")]
    [InlineData(224999L, 162999L, true, "contradicted")]
    [InlineData(225000L, 163000L, true, "contradicted")]
    [InlineData(0L, 0L, false, "contradicted")]
    public void Given_A04ClientCloseRepeats_When_Assessed_Then_LimitsDecideTheHypothesis(long after, long quiet, bool disconnect, string client)
    {
        var assessment = IdleObservationJudgement.Assess("A04", "batch", A04(after, quiet, disconnect));
        Assert.Equal(("undocumented", "not_in_librdkafka_table"), (assessment.Verdict, assessment.Reason));
        Assert.Equal(client, assessment.Hypotheses["H_client_close"]);
        Assert.Equal("recorded_only", assessment.Hypotheses["H_default_close"]);
    }

    [Fact]
    public void Given_SmokeRun_When_Assessed_Then_NoVerdictEvenIfComplete()
    {
        var assessment = IdleObservationJudgement.Assess("A02", "smoke", Results(("C0", "not_kept"), ("C1", "not_kept")));
        Assert.Equal((null, "smoke_only"), (assessment.Verdict, assessment.Reason));
    }

    [Fact]
    public void Given_MissingA03C2_When_Assessed_Then_ConfigurationIsIncomplete()
    {
        var assessment = IdleObservationJudgement.Assess("A03", "batch", Results(("C0", "not_kept"), ("C1", "not_kept")));
        Assert.Equal((null, "incomplete_configuration"), (assessment.Verdict, assessment.Reason));
    }

    [Fact]
    public void Given_IncompleteConfiguration_When_Assessed_Then_NoVerdict()
    {
        var results = Results(("C0", "kept"), ("C1", "kept"));
        results["C1"] = Summary("C1", null, "incomplete");
        Assert.Equal("incomplete_configuration", IdleObservationJudgement.Assess("A02", "batch", results).Reason);
    }

    [Fact]
    public void Given_FirstFailedAndUnequal_When_Assessed_Then_FirstFailedWins()
    {
        var assessment = IdleObservationJudgement.Assess("A02", "batch", Results(("C0", "first_failed"), ("C1", "unequal")));
        Assert.Equal(("untestable", "first_produce_failed"), (assessment.Verdict, assessment.Reason));
    }

    [Fact]
    public void Given_UnequalAndItemRule_When_Assessed_Then_UnequalWins()
    {
        var assessment = IdleObservationJudgement.Assess("A02", "batch", Results(("C0", "unequal"), ("C1", "not_kept")));
        Assert.Equal((null, "unequal_classes"), (assessment.Verdict, assessment.Reason));
    }

    [Fact]
    public void Given_UnequalRecordedOnlyConfiguration_When_A03IsAssessed_Then_NoVerdict()
    {
        var assessment = IdleObservationJudgement.Assess("A03", "batch", Results(("C0", "not_kept"), ("C1", "not_kept"), ("C2", "unequal")));
        Assert.Equal((null, "unequal_classes"), (assessment.Verdict, assessment.Reason));
    }

    [Fact]
    public void Given_Items_When_ConfigurationsAreListed_Then_A03HasThree()
    {
        Assert.Equal(["C0", "C1", "C2"], IdleObservationJudgement.Configurations("A03"));
        Assert.Equal(["C0", "C1"], IdleObservationJudgement.Configurations("A02"));
        Assert.Equal(["C0", "C1"], IdleObservationJudgement.Configurations("A04"));
    }

    [Fact]
    public void Given_DownAndInternalBrokers_When_UpBrokersAreCounted_Then_OnlyUpNonInternalBrokersCount()
    {
        var result = JudgeOne(Repeat(Series(_ => [Bootstrap(), Node8() with { State = "DOWN" }, ("internal/9", 9, "internal", "UP", 1000000, 1000000, 1, 0, 0, 0)])));
        Assert.Equal(1, result.MaximumUpBrokers);
    }
}
