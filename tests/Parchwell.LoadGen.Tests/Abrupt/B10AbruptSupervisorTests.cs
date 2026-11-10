using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptSupervisorTests
{
    [Fact]
    public void Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists()
    {
        var journal = new Records();
        journal.Prepare();
        journal.Accept();
        journal.Report();
        var snapshot = journal.Read();
        Assert.Equal("b10_abrupt_sample_missing", Assert.Single(snapshot.Failures).Value);
        Assert.DoesNotContain("b10_abrupt_report_binding_invalid", snapshot.Failures.Values);
        Assert.Empty(snapshot.Gaps);
    }

    [Fact]
    public void Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists()
    {
        var journal = new Records();
        journal.Prepare();
        journal.Report();
        journal.Report();
        journal.Accept();
        var snapshot = journal.Read();
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(snapshot.Failures).Value);
        Assert.DoesNotContain("b10_abrupt_sample_missing", snapshot.Failures.Values);
        Assert.Empty(snapshot.Gaps);
    }

    [Theory]
    [InlineData("attempt_unknown")]
    [InlineData("wrong_run")]
    [InlineData("empty_client")]
    [InlineData("member_one")]
    public void Given_InvalidReportBinding_When_AcceptedArrives_Then_FirstBindingFailureWins(string fault)
    {
        var journal = new Records();
        journal.Prepare();
        if (fault == "member_one") journal.Prepare(1);
        JsonObject report = JsonNode.Parse(journal.ReportData().GetRawText())!.AsObject();
        switch (fault)
        {
            case "attempt_unknown": report["attempt_id"] = "unknown"; break;
            case "wrong_run": report["report_identity"]!["run_id"] = "another-run"; break;
            case "empty_client": report["report_identity"]!["client_id"] = "another-client"; break;
            case "member_one": report["attempt_id"] = "same_instance-original-1"; break;
        }
        journal.Add("pre_acceptance_report", JsonSerializer.SerializeToElement(report));
        journal.Accept();
        journal.Report();
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(journal.Read().Failures).Value);
    }

    [Theory]
    [InlineData(1048575, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public void Given_ReportFileAtByteBoundary_When_Bound_Then_ExactLimitIsEnforced(int length, bool valid)
    {
        var journal = new Records();
        JsonObject report = JsonNode.Parse(journal.ReportData().GetRawText())!.AsObject();
        report["length"] = length;
        if (valid) B10AbruptWire.ValidateData("pre_acceptance_report", JsonSerializer.SerializeToElement(report));
        else Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() =>
            B10AbruptWire.ValidateData("pre_acceptance_report", JsonSerializer.SerializeToElement(report))).Message);
    }

    [Theory]
    [InlineData("length_zero")]
    [InlineData("length_negative")]
    [InlineData("length_oversized")]
    [InlineData("hash_changed")]
    [InlineData("identity_missing")]
    [InlineData("identity_extra")]
    [InlineData("wrong_scenario")]
    [InlineData("wrong_role")]
    [InlineData("numeric_role")]
    [InlineData("empty_client")]
    [InlineData("sequence_zero")]
    [InlineData("sequence_negative")]
    [InlineData("sequence_fraction")]
    [InlineData("sequence_string")]
    [InlineData("utc_short")]
    [InlineData("utc_offset")]
    [InlineData("utc_bad_date")]
    [InlineData("unknown_arm")]
    public void Given_InvalidReportDescriptor_When_Reading_Then_RejectsClosedSchema(string fault)
    {
        JsonObject report = JsonNode.Parse(new Records().ReportData().GetRawText())!.AsObject();
        switch (fault)
        {
            case "length_zero": report["length"] = 0; break;
            case "length_negative": report["length"] = -1; break;
            case "length_oversized": report["length"] = 1048577; break;
            case "hash_changed": report["sha256"] = new string('g', 64); break;
            case "identity_missing": report["report_identity"]!.AsObject().Remove("client_id"); break;
            case "identity_extra": report["report_identity"]!["extra"] = true; break;
            case "wrong_scenario": report["report_identity"]!["scenario_id"] = "B11"; break;
            case "wrong_role": report["report_identity"]!["role"] = "Verifier"; break;
            case "numeric_role": report["report_identity"]!["role"] = 0; break;
            case "empty_client": report["report_identity"]!["client_id"] = ""; break;
            case "sequence_zero": report["report_sequence"] = 0; break;
            case "sequence_negative": report["report_sequence"] = -1; break;
            case "sequence_fraction": report["report_sequence"] = 1.5; break;
            case "sequence_string": report["report_sequence"] = "1"; break;
            case "utc_short": report["report_capture_utc"] = "2026-01-01T00:00:00Z"; break;
            case "utc_offset": report["report_capture_utc"] = "2026-01-01T00:00:00.0000000+01:00"; break;
            case "utc_bad_date": report["report_capture_utc"] = "2026-13-01T00:00:00.0000000+00:00"; break;
            case "unknown_arm": report["arm"] = "unknown"; break;
        }
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() => B10AbruptWire.ValidateData("pre_acceptance_report", JsonSerializer.SerializeToElement(report))).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained(bool beforeAcceptance)
    {
        var journal = new Records();
        journal.Prepare(); journal.Report();
        if (!beforeAcceptance) journal.Accept();
        journal.Terminal();
        if (beforeAcceptance) journal.Accept();
        var snapshot = journal.Read();
        Assert.Empty(snapshot.Gaps);
        Assert.Empty(snapshot.Failures);
        Assert.Single(snapshot.Frames, frame => frame.Kind == "terminal");
        Assert.Single(snapshot.Frames, frame => frame.Kind == "accepted");
        Assert.Empty(B10AbruptJournal.Unconfirmed(snapshot.Frames, "same_instance"));
    }

    [Theory]
    [InlineData(0, 2, true, true)]
    [InlineData(0, 0, true, true)]
    [InlineData(0, 1, true, false)]
    [InlineData(-192, 0, false, true)]
    [InlineData(-192, 1, false, true)]
    [InlineData(-192, 1, true, false)]
    [InlineData(-192, 2, false, false)]
    [InlineData(-195, 1, false, false)]
    [InlineData(0, null, true, false)]
    public void Given_TerminalPersistenceTuple_When_SupervisorCaptures_Then_OnlyPermittedTuplePasses(int code, int? status, bool delivered, bool permitted)
    {
        var journal = new Records();
        journal.Prepare(); journal.Report(); journal.Accept();
        journal.Add("terminal", B10AbruptWire.Element(new { attempt_id = "same_instance-original-0", completion_source = "callback", delivered, error_code = code, persistence_status = status }));
        var snapshot = journal.Read();
        Assert.Empty(snapshot.Gaps);
        if (permitted) Assert.Empty(snapshot.Failures);
        else Assert.Equal("b10_abrupt_guard_failed", Assert.Single(snapshot.Failures).Value);
    }

    [Fact]
    public void Given_SequenceOutOfOrder_When_Accepted_Then_CaptureGap()
    {
        var journal = new Records();
        journal.Prepare(); journal.Report(); journal.Accept();
        var frame = journal.Frames[^1];
        journal.Frames[^1] = frame with { Data = B10AbruptWire.Element(new { attempt_id = "same_instance-original-0", prepared_sequence = 999 }) };
        Assert.Contains("malformed_frame", journal.Read().Gaps);
    }

    [Fact]
    public void Given_ReportNamesMember1BeforeMember0Accepted_When_Member0Accepted_Then_ReportBindingInvalid()
    {
        var journal = new Records(); journal.Prepare(); journal.Prepare(1); journal.Report(1); journal.Accept();
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(journal.Read().Failures).Value);
    }

    [Fact]
    public async Task Given_OversizeFailure_When_Reporting_Then_OriginalSurvivesAndDiagnosticReduces()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-oversize-" + Guid.NewGuid().ToString("N"));
        try
        {
            Exception cause = new InvalidOperationException("synthetic", B10AbruptDiagnosticsTests.HugeChain(7));
            var options = B10AbruptWireProducerTests.TestOptions(root);
            var timeline = new B10AbruptTimeline(TimeProvider.System, "parent", null);
            for (int index = 0; index < 48; index++) timeline.Hold(B10AbruptStage.FailureObserved,
                Enum.GetValues<B10AbruptFact>().Take(8).Select(key => B10AbruptDiagnostics.Fact(key, long.MinValue)).ToArray());
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            TextWriter previous = Console.Error;
            byte[] bytes;
            try
            {
                Console.SetError(errors);
                bytes = await B10AbruptDiagnostics.WriteFailureAsync(options, TimeProvider.System, cause, new Records().Read(), timeline);
            }
            finally { Console.SetError(previous); }
            Assert.Equal(Encoding.UTF8.GetString(bytes) + Environment.NewLine, errors.ToString());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(root, "abrupt", "failure.json"), TestContext.Current.CancellationToken));
            using var report = JsonDocument.Parse(bytes);
            var data = report.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("timeline").GetProperty("omitted_for_size").GetBoolean());
            Assert.True(data.GetProperty("diagnostic").GetProperty("reduced").GetBoolean());
            Assert.Equal(B10AbruptParent.FailureName(cause), data.GetProperty("failure").GetString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("capture")]
    public async Task Given_OversizeCleanupCause_When_Reporting_Then_BuildReducesItAndWrites(string cause)
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-oversize-cleanup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Exception huge = B10AbruptDiagnosticsTests.HugeChain();
            var options = B10AbruptWireProducerTests.TestOptions(root, configure: values =>
            { foreach (string key in values.Keys.ToArray()) values[key] = values[key].Replace("synthetic", "fixture", StringComparison.Ordinal); });
            var timeline = new B10AbruptTimeline(TimeProvider.System, "parent", null);
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            TextWriter previous = Console.Error;
            byte[] bytes;
            try
            {
                Console.SetError(errors);
                bytes = await B10AbruptDiagnostics.WriteFailureAsync(options, TimeProvider.System,
                    new InvalidDataException("b10_abrupt_guard_failed"), new Records().Read(), timeline,
                    cleanupFailure: cause == "failure" ? huge : null, cleanupCaptureFailure: cause == "capture" ? huge : null);
            }
            finally { Console.SetError(previous); }
            string text = Encoding.UTF8.GetString(bytes);
            Assert.Equal(text + Environment.NewLine, errors.ToString());
            Assert.True(bytes.Length <= B10AbruptWire.MaximumBodyBytes);
            Assert.DoesNotContain(":" + JsonSerializer.Serialize(huge.Message), text, StringComparison.Ordinal);
            using var report = JsonDocument.Parse(bytes);
            var data = report.RootElement.GetProperty("data");
            B10AbruptWire.ExactMembers(data, "mechanical_status", "failure", "exception_type", "diagnostic", "process", "kill_requested", "has_exited", "capture_gaps", "arm_failures", "fatal_capture_status", "signal_observation", "cleanup", "startup_cleanup", "timeline");
            var cleanup = data.GetProperty("cleanup");
            B10AbruptWire.ExactMembers(cleanup, "arm", "failure", "signal_observation", "capture_failure", "failure_diagnostic", "capture_diagnostic");
            var reduced = cleanup.GetProperty(cause == "failure" ? "failure_diagnostic" : "capture_diagnostic");
            Assert.DoesNotContain(huge.Message, reduced.GetRawText(), StringComparison.Ordinal);
            Assert.True(reduced.GetProperty("reduced").GetBoolean());
            Assert.Equal(JsonValueKind.Null, reduced.GetProperty("exception_chain").ValueKind);
            Assert.Equal(JsonValueKind.Null, cleanup.GetProperty(cause == "failure" ? "capture_diagnostic" : "failure_diagnostic").ValueKind);
            Assert.True(data.GetProperty("timeline").GetProperty("omitted_for_size").GetBoolean());
            Assert.True(data.GetProperty("diagnostic").GetProperty("reduced").GetBoolean());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Given_UnknownArmAlongsideTwoValidArms_When_Bound_Then_RecognizedArmsHaveNoSampleFailure()
    {
        var journal = new Records();
        foreach (string arm in new[] { "same_instance", "cross_restart" })
        { journal.Prepare(arm: arm); journal.Report(arm: arm); journal.Accept(arm: arm); }
        JsonObject unknown = JsonNode.Parse(journal.ReportData().GetRawText())!.AsObject();
        unknown["arm"] = "unknown";
        journal.Add("pre_acceptance_report", JsonSerializer.SerializeToElement(unknown));
        var snapshot = journal.Read();
        Assert.Empty(snapshot.Failures);
        Assert.Contains("malformed_frame", snapshot.Gaps);
        Assert.Equal(2, snapshot.Frames.Count(frame => frame.Kind == "accepted"));
    }

    [Theory]
    [InlineData("b10_abrupt_child_start_failed")]
    [InlineData("b10_abrupt_signal_preflight_failed")]
    public async Task Given_StartupThrows_When_SupervisorReports_Then_PreservesOriginalAndDoesNotRepeatOwnedCleanup(string failureName)
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-startup-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = B10AbruptWireProducerTests.TestOptions(root);
            var failure = new InvalidOperationException(failureName + ":private-startup-message /private/startup-path token=private-token");
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            TextWriter previous = Console.Error;
            byte[] bytes;
            try
            {
                Console.SetError(errors);
                bytes = await B10AbruptDiagnostics.WriteFailureAsync(options, TimeProvider.System, failure,
                    new([], [], new Dictionary<string, string>(), [], []), new B10AbruptTimeline(TimeProvider.System, "parent", null));
            }
            finally { Console.SetError(previous); }
            using var report = JsonDocument.Parse(bytes);
            var data = report.RootElement.GetProperty("data");
            Assert.Equal("failed_diagnostic", report.RootElement.GetProperty("kind").GetString());
            B10AbruptWire.ExactMembers(data, "mechanical_status", "failure", "exception_type", "diagnostic", "process", "kill_requested", "has_exited", "capture_gaps", "arm_failures", "fatal_capture_status", "signal_observation", "cleanup", "startup_cleanup", "timeline");
            Assert.Equal("incomplete", data.GetProperty("mechanical_status").GetString());
            Assert.Equal(failureName, data.GetProperty("failure").GetString());
            Assert.Equal(failure.GetType().FullName, data.GetProperty("exception_type").GetString());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("process").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("has_exited").ValueKind);
            Assert.False(data.GetProperty("kill_requested").GetBoolean());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("cleanup").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("startup_cleanup").ValueKind);
            foreach (string secret in new[] { "private-startup-message", "/private/startup-path", "private-token" }) Assert.DoesNotContain(secret, errors.ToString(), StringComparison.Ordinal);
            Assert.Equal(Encoding.UTF8.GetString(bytes) + Environment.NewLine, errors.ToString());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(root, "abrupt", "failure.json"), TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_SignalAndWriterCleanupFailures_When_Reporting_Then_BothCauseDiagnosticsSurvive()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-two-causes-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            TextWriter previous = Console.Error;
            byte[] bytes;
            try
            {
                Console.SetError(errors);
                bytes = await B10AbruptDiagnostics.WriteFailureAsync(B10AbruptWireProducerTests.TestOptions(root), TimeProvider.System,
                    new InvalidDataException("b10_abrupt_guard_failed"), new Records().Read(), new B10AbruptTimeline(TimeProvider.System, "parent", null),
                    cleanupFailure: new InvalidOperationException("b10_abrupt_signal_failed"), cleanupCaptureFailure: new InvalidDataException("b10_abrupt_capture_gap"));
            }
            finally { Console.SetError(previous); }
            using var report = JsonDocument.Parse(bytes);
            var data = report.RootElement.GetProperty("data");
            Assert.Equal("b10_abrupt_guard_failed", data.GetProperty("failure").GetString());
            var cleanup = data.GetProperty("cleanup");
            Assert.Equal("b10_abrupt_signal_failed", cleanup.GetProperty("failure").GetString());
            Assert.Equal("b10_abrupt_capture_gap", cleanup.GetProperty("capture_failure").GetString());
            Assert.Equal("System.InvalidOperationException", cleanup.GetProperty("failure_diagnostic").GetProperty("exception_chain").GetProperty("entries")[0].GetProperty("exception_type").GetString());
            Assert.Equal("System.IO.InvalidDataException", cleanup.GetProperty("capture_diagnostic").GetProperty("exception_chain").GetProperty("entries")[0].GetProperty("exception_type").GetString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    internal sealed class Records
    {
        internal B10AbruptWire.Identity Identity { get; } = B10AbruptWireProducerTests.TestOptions(Path.GetTempPath()).Identity();
        internal List<B10AbruptWire.Frame> Frames { get; } = [];
        internal Records() => Add("child_started", B10AbruptWire.Element(new { pid = 1234, start_time_ticks = 123L, boot_id = "synthetic-boot" }));
        internal B10AbruptWire.Frame Add(string kind, JsonElement data)
        {
            long sequence = Frames.Count + 1L;
            var frame = new B10AbruptWire.Frame(Identity, sequence, new DateTimeOffset(2026, 11, 7, 0, 0, 1, TimeSpan.Zero).AddTicks(sequence).ToString("O", CultureInfo.InvariantCulture), sequence, TimeSpan.TicksPerSecond, kind, data);
            Frames.Add(frame); return frame;
        }
        internal B10AbruptWire.Frame Prepare(int ordinal = 0, string arm = "same_instance") => Add("prepared", B10AbruptWire.Element(new
        {
            attempt_id = arm + "-original-" + ordinal, arm, role = "original", ordinal, probe_id = "B10/synthetic/C1/1/" + arm,
            key = "B10/synthetic/C1/1/" + arm, sequence = ordinal, payload_length = 1, payload_sha256 = B10AbruptPublisher.Hash([1]), predecessor_attempt_id = (string?)null
        }));
        internal void Accept(int ordinal = 0, string arm = "same_instance") => Add("accepted", B10AbruptWire.Element(new
        { attempt_id = arm + "-original-" + ordinal, prepared_sequence = Frames.Single(frame => frame.Kind == "prepared" && frame.Data.GetProperty("attempt_id").GetString() == arm + "-original-" + ordinal).Sequence }));
        internal JsonElement ReportData(int ordinal = 0, string arm = "same_instance") => B10AbruptWire.Element(new
        {
            arm, attempt_id = arm + "-original-" + ordinal, name = arm + "-pre-acceptance-report.json", length = 1, sha256 = B10AbruptPublisher.Hash([1]),
            report_identity = new { scenario_id = "B10", run_id = "synthetic", role = "LoadGenProducer", client_id = Identity.InstanceId },
            report_sequence = 1, report_capture_utc = "2026-11-07T00:00:00.0000000+00:00"
        });
        internal void Report(int ordinal = 0, string arm = "same_instance") => Add("pre_acceptance_report", ReportData(ordinal, arm));
        internal void Terminal(int ordinal = 0, string arm = "same_instance") => Add("terminal", B10AbruptWire.Element(new
        { attempt_id = arm + "-original-" + ordinal, completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 }));
        internal byte[] Bytes() => Frames.SelectMany(frame => B10AbruptWire.WriteFrame(frame).Append((byte)10)).ToArray();
        internal B10AbruptJournal.Snapshot Read() => B10AbruptJournal.Read(Bytes(), Identity);
    }
}
