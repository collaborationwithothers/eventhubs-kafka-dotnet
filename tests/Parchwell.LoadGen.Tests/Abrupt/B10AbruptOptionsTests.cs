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
    private static readonly string[] CutAttemptIds = ["a"];
    [Theory]
    [InlineData("parent")]
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
            () => options.Validate(role == "successor" ? "parent" : "successor")).Message);
    }

    [Theory]
    [InlineData("parent", "StartLineBindingPath", false)]
    [InlineData("parent", "PredecessorBindingPath", true)]
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
        var values = Valid("parent");
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
        var values = Valid("parent");
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
        var values = Valid("parent");
        values[name] = "forbidden";
        Invalid(values);
    }

    [Fact]
    public void Given_DuplicateSetting_When_Read_Then_FailsClosed()
    {
        var values = Valid("parent").ToList();
        values.Add(new(B10AbruptOptions.Prefix + "Role", "parent"));
        Invalid(values);
    }

    [Theory]
    [InlineData("EventsPipeHandle")]
    [InlineData("AckPipeHandle")]
    [InlineData("IdentityChallenge")]
    [InlineData("ArmTimeoutSeconds")]
    [InlineData("KillTimeoutSeconds")]
    [InlineData("ReapTimeoutSeconds")]
    [InlineData("EofTimeoutSeconds")]
    [InlineData("MaxFrameBytes")]
    public void Given_ChildFieldMissingOrUsedByPodRole_When_Validated_Then_FailsClosed(string field)
    {
        foreach (string role in new[] { "parent", "child", "successor" })
        {
            var values = Valid(role);
            values[B10AbruptOptions.Prefix + field] = "4";
            var failure = Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values));
            Assert.Equal("b10_abrupt_configuration_invalid", failure.Message);
            Assert.Equal("SettingNameOrDuplicate", B10AbruptOptions.ReadValidationDetail(failure));
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
        var failure = Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values));
        Assert.Equal("b10_abrupt_configuration_invalid", failure.Message);
        Assert.Equal("SettingNameOrDuplicate", B10AbruptOptions.ReadValidationDetail(failure));
    }

    [Theory]
    [InlineData("supervisor")]
    [InlineData("Supervisor")]
    public void Given_RemovedOrCaseChangedRole_When_Validated_Then_NamesRole(string role)
    {
        var values = Valid("parent");
        values[B10AbruptOptions.Prefix + "Role"] = role;
        var failure = Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values).Validate());
        Assert.Equal("Role", B10AbruptOptions.ReadValidationDetail(failure));
    }

    [Theory]
    [InlineData("parent", "loadgen-restart", true)]
    [InlineData("parent", "loadgen", false)]
    [InlineData("parent", "loadgen2", false)]
    [InlineData("parent", "loadgen-successor", false)]
    [InlineData("child", "loadgen-restart", true)]
    [InlineData("child", "loadgen", false)]
    [InlineData("child", "loadgen2", false)]
    [InlineData("child", "loadgen-successor", false)]
    [InlineData("successor", "loadgen-successor", true)]
    [InlineData("successor", "loadgen", false)]
    [InlineData("successor", "loadgen2", false)]
    [InlineData("successor", "loadgen-restart", false)]
    public void Given_JobNameForRole_When_Validated_Then_OnlyItsOwnJobIsAdmitted(string role, string suffix, bool valid)
    {
        var values = Valid(role);
        values[B10AbruptOptions.Prefix + "JobName"] = "synthetic-c1-r1-" + suffix;
        if (valid) B10AbruptOptions.Read(values).Validate(role);
        else
        {
            var failure = Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values).Validate(role));
            Assert.Equal("JobName", B10AbruptOptions.ReadValidationDetail(failure));
        }
    }

    [Fact]
    public void Given_ChangedFixedPolicy_When_Validated_Then_EachBoundaryFailsClosed()
    {
        foreach (var policy in B10AbruptOptions.Policy)
        {
            foreach (int delta in new[] { -1, 0, 1 })
            {
                var values = Valid("parent");
                values[B10AbruptOptions.Prefix + policy.Key] = (policy.Value + delta).ToString(CultureInfo.InvariantCulture);
                if (delta == 0) B10AbruptOptions.Read(values).Validate();
                else
                {
                    var failure = Assert.Throws<InvalidDataException>(() => B10AbruptOptions.Read(values).Validate());
                    Assert.Equal("FixedPolicy", B10AbruptOptions.ReadValidationDetail(failure));
                }
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
    [InlineData("parent")]
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
        var values = Valid("parent");
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
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "journal.jsonl");
            B10AbruptOptions options = B10AbruptOptions.Read(Valid("child"));
            await using var journal = new B10AbruptJournal(path, options.Identity(), TimeProvider.System);
            await journal.AppendAsync("child_started", B10AbruptWire.Element(new { pid = 1, start_time_ticks = 1, boot_id = "boot" }), TestContext.Current.CancellationToken);
            await journal.AppendAsync("prepared", B10AbruptWire.Element(new
            {
                attempt_id = "a", arm = "same_instance", role = "original", ordinal = 4, probe_id = "B10/synthetic/C1/1/same_instance", key = "B10/synthetic/C1/1/same_instance", sequence = 4,
                payload_length = 1, payload_sha256 = new string('a', 64), predecessor_attempt_id = (string?)null
            }), TestContext.Current.CancellationToken);
            await journal.AppendAsync("accepted", B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 2 }), TestContext.Current.CancellationToken);
            if (empty) await journal.AppendAsync("terminal", B10AbruptWire.Element(new
            { attempt_id = "a", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 }), TestContext.Current.CancellationToken);
            B10AbruptWire.Frame cut = await journal.AppendCutAsync("a", TestContext.Current.CancellationToken);
            B10AbruptJournal.Snapshot snapshot = B10AbruptJournal.Read(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken), options.Identity());
            Assert.Equal(empty ? Array.Empty<string>() : CutAttemptIds, snapshot.ControlUnconfirmedAttemptIds);
            Assert.Equal(DateTimeOffset.ParseExact(cut.ObservedUtc, "O", CultureInfo.InvariantCulture).AddSeconds(120),
                DateTimeOffset.ParseExact(cut.Data.GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture));
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
            ["JobName"] = "synthetic-c1-r1-loadgen-" + (role == "successor" ? "successor" : "restart"),
            ["PodName"] = "synthetic-pod", ["PodUid"] = "synthetic-uid", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = "/app", ["OutputRoot"] = "/work/loadgen",
            [role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath"] = "/bindings/not-mounted.json",
        };
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
