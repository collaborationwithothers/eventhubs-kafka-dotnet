using System.Globalization;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Repository test naming convention.
[CollectionDefinition("B10AbruptEnvironment", DisableParallelization = true)]
public sealed class B10AbruptEnvironmentScopeDefinition { }

[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptOptionsTests
{
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
