using System.Globalization;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Repository test naming convention.
[CollectionDefinition("B10AbruptEnvironment", DisableParallelization = true)]
public sealed class B10AbruptEnvironmentScopeDefinition { }

[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptOptionsTests
{
    private static readonly string[] CheckpointAttemptIds = ["a", "b"];
    [Theory]
    [InlineData("supervisor")]
    [InlineData("successor")]
    [InlineData("child")]
    public void Given_ValidRoleInputs_When_Validated_Then_OnlyThatRoleIsAdmitted(string role)
    {
        B10AbruptOptions options = B10AbruptOptions.Read(Valid(role));
        options.Validate(role);
        Assert.True(options.Enabled);
        Assert.Equal(role, options.Role);
        Assert.Equal(1, options.Repeat);
        Assert.Equal("b10_abrupt_configuration_invalid", Assert.Throws<InvalidDataException>(
            () => options.Validate(role == "successor" ? "supervisor" : "successor")).Message);
    }

    [Theory]
    [InlineData("supervisor", "StartLineBindingPath", false)]
    [InlineData("supervisor", "PredecessorBindingPath", true)]
    [InlineData("successor", "PredecessorBindingPath", false)]
    [InlineData("successor", "StartLineBindingPath", true)]
    [InlineData("child", "StartLineBindingPath", false)]
    [InlineData("child", "PredecessorBindingPath", true)]
    public void Given_MissingRequiredOrPresentRejectedBinding_When_Validated_Then_FailsClosed(string role, string field, bool present)
    {
        var values = Valid(role);
        if (present) values[B10AbruptOptions.Prefix + field] = "/bindings/rejected.json";
        else values.Remove(B10AbruptOptions.Prefix + field);
        Invalid(values);
    }

    [Theory]
    [InlineData("Role", "unknown")]
    [InlineData("Role", "")]
    [InlineData("ScenarioId", "A01")]
    [InlineData("ConfigurationId", "C0")]
    [InlineData("Phase", "C0")]
    [InlineData("Phase", "probe")]
    [InlineData("Repeat", "0")]
    [InlineData("Repeat", "2")]
    [InlineData("Repeat", "01")]
    [InlineData("Purpose", "scientific")]
    [InlineData("Purpose", "")]
    [InlineData("Enabled", "false")]
    [InlineData("Enabled", "True")]
    [InlineData("SourceCommit", "main")]
    [InlineData("SourceCommit", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("ImageReference", "registry.invalid/parchwell/loadgen:latest")]
    [InlineData("ImageReference", "registry.invalid/parchwell/loadgen@sha256:xyz")]
    [InlineData("JobName", "other-c1-r1-loadgen")]
    [InlineData("StartLineBindingPath", "relative.json")]
    [InlineData("StartLineBindingPath", "/bindings/../escape.json")]
    public void Given_IncompatibleDiagnosticInput_When_Validated_Then_FailsClosed(string field, string value)
    {
        var values = Valid("supervisor");
        values[B10AbruptOptions.Prefix + field] = value;
        Invalid(values);
    }

    [Theory]
    [InlineData("Enabled")]
    [InlineData("Purpose")]
    [InlineData("Role")]
    [InlineData("SourceCommit")]
    [InlineData("ImageReference")]
    public void Given_MissingDiagnosticAuthorization_When_Validated_Then_FailsClosed(string field)
    {
        var values = Valid("supervisor");
        values.Remove(B10AbruptOptions.Prefix + field);
        Invalid(values);
    }

    [Fact]
    public void Given_NoDiagnosticEnvironment_When_Read_Then_DisabledAndCannotEnterDiagnosticHost()
    {
        B10AbruptOptions options = B10AbruptOptions.Read([]);
        Assert.False(options.Enabled);
        Assert.Equal("b10_abrupt_configuration_invalid", Assert.Throws<InvalidDataException>(() => options.Validate()).Message);
    }

    [Theory]
    [InlineData("B10Abrupt__UnsupportedSetting")]
    [InlineData("B10Abrupt__role")]
    [InlineData("b10abrupt__Role")]
    [InlineData("LoadGen__ScenarioId")]
    [InlineData("Verifier__Mode")]
    [InlineData("SingleAction__Role")]
    [InlineData("ProbeV2__Role")]
    [InlineData("IdleObservation__Role")]
    [InlineData("SeedF04__Role")]
    public void Given_UnknownCaseCollidingOrMixedPrefix_When_Read_Then_FailsClosed(string name)
    {
        var values = Valid("supervisor");
        values[name] = "forbidden";
        Invalid(values);
    }

    [Fact]
    public void Given_DuplicateSetting_When_Read_Then_FailsClosed()
    {
        var values = Valid("supervisor").ToList();
        values.Add(new(B10AbruptOptions.Prefix + "Role", "supervisor"));
        Invalid(values);
    }

    [Theory]
    [InlineData("EventsPipeHandle")]
    [InlineData("AckPipeHandle")]
    [InlineData("IdentityChallenge")]
    public void Given_ChildFieldMissingOrUsedByPodRole_When_Validated_Then_FailsClosed(string field)
    {
        var child = Valid("child");
        string value = child[B10AbruptOptions.Prefix + field];
        child.Remove(B10AbruptOptions.Prefix + field);
        Invalid(child);
        foreach (string role in new[] { "supervisor", "successor" })
        {
            var values = Valid(role);
            values[B10AbruptOptions.Prefix + field] = value;
            Invalid(values);
        }
    }

    [Theory]
    [InlineData("EventsPipeHandle", "-1")]
    [InlineData("AckPipeHandle", "4")]
    [InlineData("IdentityChallenge", "not-hex")]
    public void Given_MalformedChildHandshake_When_Validated_Then_FailsClosed(string field, string value)
    {
        var values = Valid("child");
        values[B10AbruptOptions.Prefix + field] = value;
        Invalid(values);
    }

    [Fact]
    public void Given_ChangedFixedPolicy_When_Validated_Then_EachBoundaryFailsClosed()
    {
        foreach (var policy in B10AbruptOptions.Policy)
        {
            foreach (int delta in new[] { -1, 1 })
            {
                var values = Valid("supervisor");
                values[B10AbruptOptions.Prefix + policy.Key] = (policy.Value + delta).ToString(CultureInfo.InvariantCulture);
                Invalid(values);
            }
        }
    }

    [Theory]
    [InlineData("true", "")]
    [InlineData("false", "")]
    [InlineData("true", "--validate-configuration")]
    [InlineData("true", "--image-smoke")]
    public async Task Given_ReservedPrefix_When_OrdinaryEntryRuns_Then_RejectsBeforeHostOrProducerConstruction(string enabled, string argument)
    {
        using var environment = new EnvironmentScope(new Dictionary<string, string> { [B10AbruptOptions.Prefix + "Enabled"] = enabled });
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        TextWriter original = Console.Error;
        bool constructed = false;
        Console.SetError(errors);
        try
        {
            int exit = await Program.RunAsync(argument.Length == 0 ? [] : [argument], hostFactory: () =>
            {
                constructed = true;
                throw new InvalidOperationException("ordinary_host_must_not_be_constructed");
            });
            Assert.Equal(1, exit);
            Assert.False(constructed);
            Assert.Contains("b10_abrupt_diagnostic_not_scientific", errors.ToString(), StringComparison.Ordinal);
        }
        finally { Console.SetError(original); }
    }

    [Fact]
    public async Task Given_NoDiagnosticFlag_When_OrdinaryEntryRuns_Then_ReachesOnlyOrdinaryHostConstruction()
    {
        using var environment = new EnvironmentScope([]);
        bool ordinaryHost = false;
        int exit = await Program.RunAsync([], hostFactory: () =>
        {
            ordinaryHost = true;
            throw new InvalidOperationException("ordinary_host_boundary_reached");
        });
        Assert.Equal(1, exit);
        Assert.True(ordinaryHost);
    }

    [Theory]
    [InlineData("supervisor")]
    [InlineData("successor")]
    [InlineData("child")]
    public async Task Given_ValidConfigurationWithUnmountedBindings_When_ValidatorEntryRuns_Then_ValidatesWithoutOpeningThem(string role)
    {
        var values = Valid(role);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), "not-mounted.json");
        values[B10AbruptOptions.Prefix + (role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath")] = path;
        using var environment = new EnvironmentScope(values);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            Assert.Equal(0, await Program.RunAsync(["--validate-b10-abrupt-configuration"]));
            Assert.Equal("{\"b10_abrupt_configuration_valid\":true}" + Environment.NewLine, output.ToString());
            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        }
        finally { Console.SetOut(original); }
    }

    [Theory]
    [InlineData("Role", "private-role-value", "Role")]
    [InlineData("ImageReference", "private-image-value", "RunSourceOrImage")]
    [InlineData("StartLineBindingPath", "private-binding-value", "AbsolutePath")]
    public async Task Given_InvalidConfiguration_When_ValidatorReportsFailure_Then_NamesPredicateWithoutInputValues(
        string field, string value, string detail)
    {
        var values = Valid("supervisor");
        values[B10AbruptOptions.Prefix + field] = value;
        using var environment = new EnvironmentScope(values);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        TextWriter original = Console.Error;
        Console.SetError(errors);
        try
        {
            Assert.Equal(1, await Program.RunAsync(["--validate-b10-abrupt-configuration"]));
            B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_configuration_invalid validation_detail=" + detail + " exception_type=System.IO.InvalidDataException");
            Assert.DoesNotContain(value, errors.ToString(), StringComparison.Ordinal);
        }
        finally { Console.SetError(original); }
    }

    [Fact]
    public void Given_UnexpectedExceptionWithUntrustedDetail_When_Reported_Then_EmitsOnlySafeFallback()
    {
        var exception = new InvalidOperationException("private-exception-message");
        exception.Data["validation_detail"] = "private-detail-value";
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_capture_gap exception_type=System.InvalidOperationException");
        Assert.DoesNotContain("private-exception-message", errors.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-detail-value", errors.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("b10_abrupt_untrusted_secret", "b10_abrupt_capture_gap")]
    [InlineData("b10_abrupt_exposure_inconclusive:private-token", "b10_abrupt_exposure_inconclusive")]
    [InlineData("b10_abrupt_guard_failed:private-token", "b10_abrupt_guard_failed")]
    [InlineData("kill_too_late:private-token", "kill_too_late")]
    [InlineData("kill_too_late:no_unconfirmed_attempt:private-token", "kill_too_late")]
    [InlineData("kill_too_early:private-token", "kill_too_early")]
    public void Given_ThrownSecretBearingException_When_Reported_Then_EmitsClosedLabelAndTypeOnly(string message, string expectedFailure)
    {
        Exception inner;
        try { throw new IOException("private-inner-message /private/sentinel-path private-token"); }
        catch (IOException exception) { inner = exception; }
        Assert.NotNull(inner.StackTrace);
        try { throw new InvalidOperationException(message, inner); }
        catch (InvalidOperationException exception)
        {
            Assert.NotNull(exception.StackTrace);
            exception.Data["validation_detail"] = "private-detail-value";
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
            string report = errors.ToString();
            B10AbruptDiagnosticsTests.AssertPlainReport(report, expectedFailure + " exception_type=System.InvalidOperationException");
            Assert.DoesNotContain(message, report, StringComparison.Ordinal);
            Assert.DoesNotContain("private-inner-message", report, StringComparison.Ordinal);
            Assert.DoesNotContain("/private/sentinel-path", report, StringComparison.Ordinal);
            Assert.DoesNotContain("private-token", report, StringComparison.Ordinal);
            Assert.DoesNotContain("private-detail-value", report, StringComparison.Ordinal);
            Assert.DoesNotContain(exception.StackTrace, report, StringComparison.Ordinal);
            Assert.DoesNotContain(inner.StackTrace, report, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Given_ExplicitConfigurationFailureWithUntrustedDetail_When_Reported_Then_EmitsUnclassifiedAndRealType()
    {
        var exception = new InvalidDataException("b10_abrupt_configuration_invalid");
        exception.Data["validation_detail"] = "private-detail-value";
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_configuration_invalid validation_detail=Unclassified exception_type=System.IO.InvalidDataException");
        Assert.DoesNotContain("private-detail-value", errors.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private-cancellation-message")]
    [InlineData("kill_too_late:no_unconfirmed_attempt")]
    public void Given_Cancellation_When_Reported_Then_EmitsDeadlineAndTypeOnly(string message)
    {
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(new OperationCanceledException(message), errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_deadline_exhausted exception_type=System.OperationCanceledException");
    }

    [Fact]
    public void Given_ExactNoUnconfirmedAttemptFailure_When_Reported_Then_PreservesOnlyItsAuthorizedSuffix()
    {
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(new InvalidDataException("kill_too_late:no_unconfirmed_attempt"), errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "kill_too_late:no_unconfirmed_attempt exception_type=System.IO.InvalidDataException");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ControlCheckpointSelection_When_ReleaseRead_Then_UsesFirstSelectedMemberOrRejectsEmpty(bool empty)
    {
        string root = Path.Combine(Path.GetTempPath(), "abrupt-release-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path.Combine(root, "abrupt"));
        Directory.CreateDirectory(Path.Combine(root, "abrupt-child"));
        try
        {
            var values = Valid("child");
            values[B10AbruptOptions.Prefix + "OutputRoot"] = root;
            B10AbruptOptions options = B10AbruptOptions.Read(values);
            var release = new DateTimeOffset(2026, 11, 7, 0, 2, 0, TimeSpan.Zero);
            string captured = release.AddMinutes(-2).ToString("O", CultureInfo.InvariantCulture);
            var frame = new B10AbruptWire.Frame(options.Identity(), 1, captured, 1, 1000, "checkpoint", JsonSerializer.SerializeToElement(new
            {
                arm = "same_instance", accepted_attempt_ids = CheckpointAttemptIds,
                selected_attempt_ids = empty ? Array.Empty<string>() : CheckpointAttemptIds, last_complete_sequence = 0,
                pre_acceptance_report = new { name = "abrupt-child/same_instance-pre-acceptance-report.json", length = 1,
                    sha256 = new string('a', 64), report_identity = new { scenario_id = "B10", run_id = "synthetic", role = "LoadGenProducer", client_id = "client" },
                    report_sequence = 1, report_capture_utc = captured },
                process = new { pid = 1, parent_pid = 2, boot_id = "boot", start_identity = 1 },
                checkpoint_utc = captured, checkpoint_monotonic_ticks = 1, release_utc = release.ToString("O", CultureInfo.InvariantCulture)
            }));
            await File.WriteAllBytesAsync(Path.Combine(root, "abrupt", "same_instance-checkpoint.json"), B10AbruptWire.WriteFrame(frame), TestContext.Current.CancellationToken);

            Task<B10AbruptProducer.ReleaseInfo> operation = B10AbruptHost.ReadReleaseAsync(options, "same_instance", TimeProvider.System, TestContext.Current.CancellationToken);

            if (empty)
            {
                Assert.Equal("b10_abrupt_control_incomplete", (await Assert.ThrowsAsync<InvalidDataException>(() => operation)).Message);
                Assert.False(File.Exists(Path.Combine(root, "abrupt-child", "same_instance-resumed.json")));
            }
            else
            {
                B10AbruptProducer.ReleaseInfo info = await operation;
                Assert.Equal(release, info.Release);
                Assert.Equal("a", info.SelectedAttemptId);
                Assert.True(File.Exists(Path.Combine(root, "abrupt-child", "same_instance-resumed.json")));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Invalid(IEnumerable<KeyValuePair<string, string>> values) =>
        Assert.Equal("b10_abrupt_configuration_invalid", Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values).Validate()).Message);

    private static Dictionary<string, string> Valid(string role)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = role,
            ["RunId"] = "synthetic", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "registry.invalid/parchwell/loadgen@sha256:" + new string('b', 64),
            ["JobName"] = "synthetic-c1-r1-loadgen" + (role == "successor" ? "2" : ""),
            ["PodName"] = "synthetic-pod", ["PodUid"] = "synthetic-uid", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = "/app", ["OutputRoot"] = "/work/loadgen",
            [role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath"] = "/bindings/not-mounted.json",
        };
        if (role == "child")
        {
            values["EventsPipeHandle"] = "4";
            values["AckPipeHandle"] = "5";
            values["IdentityChallenge"] = new string('c', 64);
        }
        foreach (var policy in B10AbruptOptions.Policy) values[policy.Key] = policy.Value.ToString(CultureInfo.InvariantCulture);
        return values.ToDictionary(pair => B10AbruptOptions.Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);
        internal EnvironmentScope(IEnumerable<KeyValuePair<string, string>> values)
        {
            string[] prefixes = [B10AbruptOptions.Prefix, "LoadGen__", "Verifier__", "SingleAction__", "ProbeV2__", "IdleObservation__", "SeedF04__"];
            foreach (string name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            {
                _original[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
            foreach (var value in values)
            {
                _original.TryAdd(value.Key, Environment.GetEnvironmentVariable(value.Key));
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }
        public void Dispose()
        {
            foreach (var value in _original) Environment.SetEnvironmentVariable(value.Key, value.Value);
        }
    }
}
