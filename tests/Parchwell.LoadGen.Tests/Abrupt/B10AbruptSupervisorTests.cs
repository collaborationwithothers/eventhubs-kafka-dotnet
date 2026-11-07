using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptSupervisorTests
{
    [Fact]
    public async Task Given_NoDurableAcceptance_When_Finalizing_Then_ReportsKillTooEarly()
    {
        await using var fixture = new Fixture();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.FinalizeSelectionAsync(TestContext.Current.CancellationToken));
        Assert.Equal("kill_too_early", error.Message);
        Assert.Empty(fixture.Journal.Frames);
    }

    [Fact]
    public async Task Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_sample_missing", error.Message);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        Assert.Equal("b10_abrupt_sample_missing", Assert.Single(fixture.Supervisor.ArmFailures).Value);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "signal" or "checkpoint");
    }

    [Fact]
    public async Task Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_report_binding_invalid", error.Message);
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
    }

    [Theory]
    [InlineData("length_zero")]
    [InlineData("length_negative")]
    [InlineData("length_oversized")]
    [InlineData("length_wrong")]
    [InlineData("hash_changed")]
    [InlineData("file_changed")]
    [InlineData("file_missing")]
    [InlineData("identity_missing")]
    [InlineData("identity_extra")]
    [InlineData("wrong_scenario")]
    [InlineData("wrong_run")]
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
    [InlineData("attempt_unknown")]
    public async Task Given_InvalidReportBinding_When_AcceptedArrives_Then_FirstBindingFailureWins(string fault)
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fixture.Report().GetRawText())!;
        var identity = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data["report_identity"].GetRawText())!;
        void Set(string key, object value) => data[key] = B10AbruptWire.Element(value);
        void Id(string key, object value) => identity[key] = B10AbruptWire.Element(value);
        switch (fault)
        {
            case "length_zero": Set("report_length", 0); break;
            case "length_negative": Set("report_length", -1); break;
            case "length_oversized": Set("report_length", 1048577); break;
            case "length_wrong": Set("report_length", 1); break;
            case "hash_changed": Set("report_sha256", new string('f', 64)); break;
            case "file_changed": await File.WriteAllTextAsync(fixture.ReportPath, "changed", TestContext.Current.CancellationToken); break;
            case "file_missing": File.Delete(fixture.ReportPath); break;
            case "identity_missing": identity.Remove("client_id"); break;
            case "identity_extra": Id("extra", true); break;
            case "wrong_scenario": Id("scenario_id", "B11"); break;
            case "wrong_run": Id("run_id", "wrong"); break;
            case "wrong_role": Id("role", "Verifier"); break;
            case "numeric_role": Id("role", 0); break;
            case "empty_client": Id("client_id", ""); break;
            case "sequence_zero": Set("report_sequence", 0); break;
            case "sequence_negative": Set("report_sequence", -1); break;
            case "sequence_fraction": Set("report_sequence", 1.5); break;
            case "sequence_string": Set("report_sequence", "1"); break;
            case "utc_short": Set("report_capture_utc", "2026-01-01T00:00:00Z"); break;
            case "utc_offset": Set("report_capture_utc", "2026-01-01T00:00:00.0000000+01:00"); break;
            case "utc_bad_date": Set("report_capture_utc", "2026-13-01T00:00:00.0000000+00:00"); break;
            case "attempt_unknown": Set("attempt_id", "unknown"); break;
        }
        data["report_identity"] = B10AbruptWire.Element(identity);
        await fixture.Supervisor.AcceptReportAsync(B10AbruptWire.Element(data), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "signal" or "checkpoint");
    }

    [Theory]
    [InlineData(1048575, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public async Task Given_ReportFileAtByteBoundary_When_Bound_Then_ExactLimitIsEnforced(int length, bool valid)
    {
        await using var fixture = new Fixture(new byte[length]);
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        if (valid) { await fixture.AcceptAsync(); Assert.Empty(fixture.Supervisor.ArmFailures); }
        else Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
    }

    [Fact]
    public async Task Given_UnknownArmAlongsideTwoValidArms_When_Bound_Then_RecognizedArmsHaveNoSampleFailure()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.PrepareAsync("cross_restart");
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report("cross_restart"), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report("unknown"), TestContext.Current.CancellationToken);
        await fixture.AcceptAsync();
        await fixture.AcceptAsync("cross_restart");
        Assert.Equal("unknown", Assert.Single(fixture.Supervisor.ArmFailures).Key);
        Assert.Equal("b10_abrupt_report_binding_invalid", fixture.Supervisor.ArmFailures["unknown"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained(bool beforeAcceptance)
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        if (!beforeAcceptance) await fixture.AcceptAsync();
        await fixture.Supervisor.HandleAsync(new("terminal", 5, B10AbruptWire.Element(new
        {
            attempt_id = "same_instance-original", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2
        })), TestContext.Current.CancellationToken, true);
        if (beforeAcceptance) await fixture.AcceptAsync();
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "terminal");
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "accepted");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "b10-supervisor-" + Guid.NewGuid().ToString("N"));
        internal string ReportPath => Path.Combine(Root, "abrupt-child", "same_instance-pre-acceptance-report.json");
        internal B10AbruptSupervisor Supervisor { get; }
        internal B10AbruptJournal Journal { get; }
        private readonly byte[] raw;
        private readonly Dictionary<string, long> preparedSequences = new(StringComparer.Ordinal);
        internal Fixture(byte[]? bytes = null)
        {
            raw = bytes ?? Encoding.UTF8.GetBytes("{\"synthetic\":true}");
            Directory.CreateDirectory(Path.Combine(Root, "abrupt-child"));
            foreach (string arm in new[] { "same_instance", "cross_restart" }) File.WriteAllBytes(Path.Combine(Root, "abrupt-child", arm + "-pre-acceptance-report.json"), raw);
            B10AbruptOptions options = B10AbruptWireProducerTests.TestOptions(Root);
            Journal = new(Path.Combine(Root, "journal.jsonl"), options.Identity(), TimeProvider.System);
            Supervisor = new(options, TimeProvider.System, Journal, new(new B10AbruptWireProducerTests.FileStorage(Path.Combine(Root, "publication")), TimeProvider.System),
                () => throw new InvalidOperationException("Process must not start in message validation test"));
        }
        internal async Task PrepareAsync(string arm = "same_instance")
        {
            await Supervisor.HandleAsync(new("prepared", 1, B10AbruptWire.Element(new
            {
                attempt_id = arm + "-original", invocation_ordinal = 0, cohort = arm, probe_id = arm, key = "B10/synthetic/C1/1/" + arm,
                sequence = 0, payload_length = 1, payload_sha256 = B10AbruptPublisher.Hash([1]), predecessor_attempt_id = (string?)null
            })), TestContext.Current.CancellationToken, true);
            preparedSequences[arm] = Journal.Frames[^1].Sequence;
        }
        internal Task AcceptAsync(string arm = "same_instance") => Supervisor.HandleAsync(new("accepted", 3, B10AbruptWire.Element(new
        { attempt_id = arm + "-original", prepared_sequence = preparedSequences[arm] })), TestContext.Current.CancellationToken, true);
        internal JsonElement Report(string arm = "same_instance") => B10AbruptWire.Element(new
        {
            arm, attempt_id = arm + "-original", report_length = raw.Length, report_sha256 = B10AbruptPublisher.Hash(raw),
            report_identity = new { scenario_id = "B10", run_id = "synthetic", role = "LoadGenProducer", client_id = "synthetic-first" },
            report_sequence = 1, report_capture_utc = TimeProvider.System.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)
        });
        public async ValueTask DisposeAsync() { await Journal.DisposeAsync(); Directory.Delete(Root, true); }
    }
}
