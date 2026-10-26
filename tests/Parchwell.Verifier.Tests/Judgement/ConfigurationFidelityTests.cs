using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;
using CapturedConfiguration = Parchwell.Verifier.VerifyRun.Input.ResolvedConfiguration;
using CapturedSetting = Parchwell.Verifier.VerifyRun.Input.ResolvedSetting;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
public sealed class ConfigurationFidelityTests
{
    private const string Timeout = "socket.timeout.ms";
    private static readonly string[] ScenarioPaths = ["scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"];
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Theory]
    [InlineData("C0", "60000")]
    [InlineData("C3", "20000")]
    public async Task Given_RealA01Resolution_When_CapturedSettingsMatch_Then_Check27Passes(string configuration, string timeout)
    {
        ResolvedRoleConfiguration selected = await Resolve(configuration);
        Assert.Equal(timeout, selected.Settings[Timeout].Value);
        CapturedConfiguration captured = Capture(selected);
        Assert.Equal(configuration == "C0" ? FieldPresence.Null : FieldPresence.Value, captured.Settings[Timeout].Override.Presence);
        AssertDisposition(Evaluate(selected, captured), AssessmentDisposition.Pass);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("19999")]
    [InlineData("20001")]
    [InlineData("extra")]
    [InlineData("key_case")]
    public async Task Given_RealC3Selection_When_EffectiveSettingSetOrValueDiffers_Then_Check27Fails(string change)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        CapturedConfiguration captured = Capture(selected);
        var settings = captured.Settings.ToDictionary(StringComparer.Ordinal);
        if (change == "missing") settings.Remove(Timeout);
        else if (change == "extra") settings.Add("unexpected.setting", settings[Timeout]);
        else if (change == "key_case")
        {
            settings.Add("SOCKET.TIMEOUT.MS", settings[Timeout]);
            settings.Remove(Timeout);
        }
        else settings[Timeout] = settings[Timeout] with { Value = change };
        ConfigurationFidelityResult result = Evaluate(selected, captured with { Settings = settings });
        AssertDisposition(result, AssessmentDisposition.Fail);
        Assert.Equal(change == "key_case" ? 2 : 1, result.Differences.Count);
        string field = change switch
        {
            "missing" => "resolved_configuration.settings.socket.timeout.ms",
            "19999" => "resolved_configuration.settings.socket.timeout.ms.value",
            "20001" => "resolved_configuration.settings.socket.timeout.ms.value",
            "extra" => "resolved_configuration.settings.unexpected.setting",
            "key_case" => "resolved_configuration.settings.SOCKET.TIMEOUT.MS",
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Equal(new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", null, field, "check_27_failed", null), result.Assessment);
        SettingDifference[] expected = change switch
        {
            "missing" => [new("socket.timeout.ms", "20000", null)],
            "19999" => [new("socket.timeout.ms", "20000", "19999")],
            "20001" => [new("socket.timeout.ms", "20000", "20001")],
            "extra" => [new("unexpected.setting", null, "20000")],
            "key_case" => [new("SOCKET.TIMEOUT.MS", null, "20000"), new("socket.timeout.ms", "20000", null)],
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Equal(expected, result.Differences);
    }

    [Theory]
    [InlineData("none", "none", true)]
    [InlineData("match", "match", true)]
    [InlineData("none", "match", false)]
    [InlineData("match", "none", false)]
    [InlineData("match", "scenario", false)]
    [InlineData("match", "configuration", false)]
    [InlineData("match", "scenario_case", false)]
    [InlineData("match", "configuration_case", false)]
    public async Task Given_IdenticalEffectiveSettings_When_OverrideAttributionCompared_Then_OnlyApprovedIncludedFieldsMatter(
        string selectedOverride, string capturedOverride, bool passes)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        if (selectedOverride == "none") selected = selected with
        {
            Settings = selected.Settings.SetItem(Timeout, selected.Settings[Timeout] with { Override = null })
        };
        CapturedConfiguration captured = Capture(selected);
        CapturedSetting setting = captured.Settings[Timeout];
        var attribution = new Override("A01", "C3", new Location("synthetic", ParsedField<int>.FromValue(2), ParsedField<int>.FromValue(2)));
        ParsedField<Override> observed = capturedOverride switch
        {
            "none" => ParsedField<Override>.Null(),
            "scenario" => ParsedField<Override>.FromValue(attribution with { ScenarioId = "A02" }),
            "configuration" => ParsedField<Override>.FromValue(attribution with { ConfigurationId = "C2" }),
            "scenario_case" => ParsedField<Override>.FromValue(attribution with { ScenarioId = "a01" }),
            "configuration_case" => ParsedField<Override>.FromValue(attribution with { ConfigurationId = "c3" }),
            _ => ParsedField<Override>.FromValue(attribution)
        };
        captured = Replace(captured, Timeout, setting with { Override = observed });
        AssertDisposition(Evaluate(selected, captured), passes ? AssessmentDisposition.Pass : AssessmentDisposition.Fail);
        if (!passes)
        {
            ConfigurationFidelityResult result = Evaluate(selected, captured);
            string field = capturedOverride switch
            {
                "none" => "resolved_configuration.settings.socket.timeout.ms.override",
                "match" => "resolved_configuration.settings.socket.timeout.ms.override",
                "scenario" => "resolved_configuration.settings.socket.timeout.ms.override.scenario_id",
                "scenario_case" => "resolved_configuration.settings.socket.timeout.ms.override.scenario_id",
                "configuration" => "resolved_configuration.settings.socket.timeout.ms.override.configuration_id",
                "configuration_case" => "resolved_configuration.settings.socket.timeout.ms.override.configuration_id",
                _ => throw new ArgumentOutOfRangeException(nameof(capturedOverride))
            };
            Assert.Equal(new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
                AssessmentClassification.Invalid, "capabilities.json", null, field, "check_27_failed", null), result.Assessment);
            SettingDifference[] expected = [new("socket.timeout.ms", "20000", "20000")];
            Assert.Equal(expected, result.Differences);
        }
    }

    [Theory]
    [InlineData("path", 1)]
    [InlineData("line", 1)]
    [InlineData("line", 2)]
    [InlineData("line", 3)]
    [InlineData("column", 1)]
    [InlineData("column", 2)]
    [InlineData("column", 3)]
    [InlineData("baseline", 1)]
    [InlineData("omissions", 1)]
    public async Task Given_IdenticalIncludedFields_When_ExcludedMetadataDiffers_Then_Check27Passes(string change, int coordinate)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        selected = selected with { Settings = selected.Settings.SetItem(Timeout, selected.Settings[Timeout] with
        {
            Override = selected.Settings[Timeout].Override! with { Location = new SourceLocation("selected", 2, 2) }
        }) };
        CapturedConfiguration captured = Capture(selected);
        CapturedSetting setting = captured.Settings[Timeout];
        Override attribution = setting.Override.Value!;
        setting = change switch
        {
            "path" => setting with { Override = ParsedField<Override>.FromValue(attribution with { Location = attribution.Location with { Path = "elsewhere" } }) },
            "line" => setting with { Override = ParsedField<Override>.FromValue(attribution with { Location = attribution.Location with { Line = ParsedField<int>.FromValue(coordinate) } }) },
            "column" => setting with { Override = ParsedField<Override>.FromValue(attribution with { Location = attribution.Location with { Column = ParsedField<int>.FromValue(coordinate) } }) },
            "baseline" => setting with { Baseline = setting.Baseline with { Key = "different", Value = ParsedField<string>.FromValue("different"), Role = 99, Disposition = 99, InventoryIds = [], Sources = [], SelectionReason = "different" } },
            _ => setting
        };
        captured = Replace(captured, Timeout, setting);
        if (change == "omissions") captured = captured with { Omissions = [setting.Baseline] };
        AssertDisposition(Evaluate(selected, captured), AssessmentDisposition.Pass);
    }

    [Fact]
    public async Task Given_IncludedValueCaseDifference_When_Compared_Then_OrdinalComparisonFails()
    {
        ResolvedRoleConfiguration selected = await Resolve("C0");
        const string key = "client.id";
        CapturedConfiguration captured = Capture(selected);
        Assert.NotEqual(captured.Settings[key].Value, captured.Settings[key].Value.ToUpperInvariant());
        captured = Replace(captured, key, captured.Settings[key] with { Value = captured.Settings[key].Value.ToUpperInvariant() });
        AssertDisposition(Evaluate(selected, captured), AssessmentDisposition.Fail);
    }

    [Theory]
    [InlineData("omitted", false, false, false)]
    [InlineData("null", true, true, true)]
    [InlineData("malformed", false, false, false)]
    [InlineData("override_omitted", false, false, false)]
    public async Task Given_LiteralCapabilitiesWireStates_When_ReadAndJudged_Then_ExistingParserOwnsCheck17AndFidelityOwns27(
        string state, bool parseSuccess, bool evaluated, bool fails)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        string resolved = state switch
        {
            "omitted" => "",
            "malformed" => "\"resolved_configuration\":false,",
            "override_omitted" => """
                "resolved_configuration":{"role":0,"settings":{"socket.timeout.ms":{"value":"20000",
                "baseline":{"key":"socket.timeout.ms","value":"60000","role":0,"disposition":0,
                "inventory_ids":[],"sources":[],"selection_reason":"synthetic"}}},"omissions":[]},
                """,
            _ => "\"resolved_configuration\":null,"
        };
        string json = """
            {"schema_version":1,"identity":{"scenario_id":"A01","configuration_id":"C0","run_id":"run-1",
            "instance_id":"instance-1","expected_instance_id":"instance-1","client_id":"producer-1"},
            "artifacts":[],
            """ + resolved + """
            "request_lifecycle_requirements":[],"request_capability_supported":false,"observed_broker_names":[],
            "guard_observations":[],"guard_observations_complete":true,"session_diagnostics":[],"is_faulted":false}
            """;
        ParseResult<CapturedCapabilities> parsed = ReadCapabilities(Encoding.UTF8.GetBytes(json));
        Assert.Equal(parseSuccess, parsed.IsSuccess);
        if (!parseSuccess) Assert.All(parsed.Problems, problem => Assert.Equal(17, problem.CheckNumber));
        RunJudgementResult result = RunJudgement.Evaluate(RecordedEvidence.SyntheticRequest() with
        {
            Capabilities = parsed, ResolvedProducerConfiguration = selected
        });
        Assert.Equal(parseSuccess ? AssessmentDisposition.Pass : AssessmentDisposition.Fail,
            Assert.Single(result.Assessments, a => a.CheckNumber == 17).Disposition);
        Assert.Equal(evaluated ? AssessmentEvaluation.Evaluated : AssessmentEvaluation.NotEvaluated, result.ConfigurationFidelity.Assessment.Evaluation);
        Assert.Equal(fails ? AssessmentDisposition.Fail : (AssessmentDisposition?)null, result.ConfigurationFidelity.Assessment.Disposition);
    }

    [Fact]
    public async Task Given_SyntheticSuccessfulDtoWithMissingConfiguration_When_Compared_Then_Check27Fails()
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        // This synthetic DTO cannot arise from an omitted required wire property: the reader rejects that under 17.
        request = request with { ResolvedProducerConfiguration = selected, Capabilities = ParseResult<CapturedCapabilities>.Success(
            request.Capabilities.Value! with { ResolvedConfiguration = ParsedField<CapturedConfiguration>.Missing() }) };
        AssertDisposition(ConfigurationFidelityCheck.Evaluate(request), AssessmentDisposition.Fail);
        ConfigurationFidelityResult result = ConfigurationFidelityCheck.Evaluate(request);
        Assert.Equal(new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", null, "resolved_configuration", "check_27_failed", null), result.Assessment);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public async Task Given_SyntheticSuccessfulDtoWithMissingOverride_When_NoOverrideSelected_Then_Check27Passes()
    {
        ResolvedRoleConfiguration selected = await Resolve("C0");
        CapturedConfiguration captured = Capture(selected);
        captured = Replace(captured, Timeout, captured.Settings[Timeout] with { Override = ParsedField<Override>.Missing() });
        AssertDisposition(Evaluate(selected, captured), AssessmentDisposition.Pass);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public async Task Given_SettingDifferencesAtDiagnosticCap_When_Compared_Then_FailureSurvivesOrdinalRetention(int differenceCount, int retained)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        CapturedConfiguration captured = Capture(selected);
        var settings = captured.Settings.ToDictionary(StringComparer.Ordinal);
        string[] keys = ["z.unexpected", "a.unexpected", "m.unexpected"];
        foreach (string key in keys.Take(differenceCount)) settings.Add(key, settings[Timeout]);
        RunJudgementRequest request = Request(selected, captured with { Settings = settings });
        ConfigurationFidelityResult result = ConfigurationFidelityCheck.Evaluate(request with
        {
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 2 }
        });
        AssertDisposition(result, AssessmentDisposition.Fail);
        Assert.Equal(retained, result.Differences.Count);
        Assert.Equal(keys.Take(differenceCount).Order(StringComparer.Ordinal).Take(2), result.Differences.Select(d => d.Setting));
        string field = differenceCount == 1
            ? "resolved_configuration.settings.z.unexpected"
            : "resolved_configuration.settings.a.unexpected";
        Assert.Equal(new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", null, field, "check_27_failed", null), result.Assessment);
        SettingDifference[] expected = differenceCount switch
        {
            1 => [new("z.unexpected", null, "20000")],
            2 => [new("a.unexpected", null, "20000"), new("z.unexpected", null, "20000")],
            3 => [new("a.unexpected", null, "20000"), new("m.unexpected", null, "20000")],
            _ => throw new ArgumentOutOfRangeException(nameof(differenceCount))
        };
        Assert.Equal(expected, result.Differences);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives(int differenceCount)
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        CapturedConfiguration captured = Capture(selected);
        var settings = captured.Settings.ToDictionary(StringComparer.Ordinal);
        string[] keys = ["z.unexpected", "a.unexpected", "m.unexpected"];
        foreach (string key in keys.Take(differenceCount)) settings.Add(key, captured.Settings[Timeout]);
        RunJudgementRequest request = Request(selected, captured with { Settings = settings });
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 2 }
        });
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal([27], result.FailedChecks);
        Assert.Null(result.Counts);
        string field = differenceCount == 1
            ? "resolved_configuration.settings.z.unexpected"
            : "resolved_configuration.settings.a.unexpected";
        Assert.Equal(new CheckAssessment(27, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, "capabilities.json", null, field, "check_27_failed", null), result.ConfigurationFidelity.Assessment);
        SettingDifference[] expected = differenceCount switch
        {
            1 => [new("z.unexpected", null, "20000")],
            2 => [new("a.unexpected", null, "20000"), new("z.unexpected", null, "20000")],
            3 => [new("a.unexpected", null, "20000"), new("m.unexpected", null, "20000")],
            _ => throw new ArgumentOutOfRangeException(nameof(differenceCount))
        };
        Assert.Equal(expected, result.ConfigurationFidelity.Differences);
    }

    [Fact]
    public async Task Given_EmptyCapturedAndSelectedMaps_When_Compared_Then_Check27Passes()
    {
        ResolvedRoleConfiguration selected = await Resolve("C0");
        selected = selected with { Settings = selected.Settings.Clear() };
        AssertDisposition(Evaluate(selected, Capture(selected)), AssessmentDisposition.Pass);
    }

    [Fact]
    public async Task Given_SyntheticMissingOverrideDto_When_OverrideSelected_Then_Check27Fails()
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        CapturedConfiguration captured = Capture(selected);
        captured = Replace(captured, Timeout, captured.Settings[Timeout] with { Override = ParsedField<Override>.Missing() });
        AssertDisposition(Evaluate(selected, captured), AssessmentDisposition.Fail);
    }

    [Fact]
    public async Task Given_FidelityFailureWithOneDiagnosticSlot_When_Composed_Then_EmbeddedAssessmentConsumesThatSlot()
    {
        ResolvedRoleConfiguration selected = await Resolve("C3");
        CapturedConfiguration captured = Capture(selected);
        captured = Replace(captured, Timeout, captured.Settings[Timeout] with { Value = "20001" });
        RunJudgementRequest request = Request(selected, captured);
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 1 }
        });
        Assert.Contains(27, result.FailedChecks);
        AssertDisposition(result.ConfigurationFidelity, AssessmentDisposition.Fail);
        Assert.Single(result.ConfigurationFidelity.Differences);
        Assert.DoesNotContain(result.Diagnostics, d => d.Check == 27);
    }

    [Theory]
    [InlineData("definition")]
    [InlineData("profile")]
    [InlineData("resolution")]
    public async Task Given_ScenarioInputOrResolutionFailure_When_SelectingConfiguration_Then_ExistingScenarioBoundaryThrows(string failure)
    {
        Dictionary<string, string> documents = await ScenarioDocuments();
        if (failure == "definition") documents.Remove(ScenarioPaths[0]);
        if (failure == "profile") documents.Remove(ScenarioPaths[1]);
        var source = new MemorySource(documents);
        using ServiceProvider provider = Provider(source, includeVerifier: failure != "resolution");
        ScenarioLoader loader = provider.GetRequiredService<ScenarioLoader>();
        if (failure != "resolution")
        {
            await Assert.ThrowsAsync<FileNotFoundException>(async () => await loader.LoadAsync("A01", TestContext.Current.CancellationToken));
            return;
        }
        LoadedScenario loaded = await loader.LoadAsync("A01", TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ScenarioResolver>().Resolve(loaded));
    }

    internal static async Task<ResolvedRoleConfiguration> Resolve(string configuration)
    {
        var source = new MemorySource(await ScenarioDocuments());
        using ServiceProvider provider = Provider(source);
        Assert.NotNull(provider.GetRequiredService<KafkaConfigurationFactory>());
        LoadedScenario loaded = await provider.GetRequiredService<ScenarioLoader>().LoadAsync("A01");
        ResolvedScenario resolved = provider.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        Assert.Equal(ScenarioPaths, source.Requested);
        return resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer];
    }

    private static async Task<Dictionary<string, string>> ScenarioDocuments()
    {
        string root = FindRepositoryRoot();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScenarioPaths[0]] = await File.ReadAllTextAsync(Path.Combine(root, ScenarioPaths[0])),
            [ScenarioPaths[1]] = await File.ReadAllTextAsync(Path.Combine(root, ScenarioPaths[1]))
        };
    }

    private static ServiceProvider Provider(MemorySource source, bool includeVerifier = true)
    {
        var services = new ServiceCollection();
        KafkaClientRole[] roles = includeVerifier
            ? [KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer]
            : [KafkaClientRole.LoadGenProducer];
        foreach (KafkaClientRole role in roles)
            services.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "golden-synthetic-namespace";
                options.ClientId = "golden-loadgen-client";
            });
        services.AddSingleton<IScenarioSource>(source);
        services.AddParchwellScenarios(options =>
        {
            options.MaxDocumentCharacters = 256 * 1024;
            options.MaxNestingDepth = 32;
            options.MaxScalarCharacters = 8 * 1024;
            options.MaxParserEvents = 20_000;
        });
        return services.BuildServiceProvider();
    }

    private static CapturedConfiguration Capture(ResolvedRoleConfiguration selected)
    {
        // Exercise the actual captured-evidence parser, including the wire override:null baseline representation.
        string configuration = JsonSerializer.Serialize(new { selected.Role, selected.Settings, selected.Omissions }, WireOptions);
        string json = """
            {"schema_version":1,"identity":{"scenario_id":"A01","configuration_id":"C0","run_id":"run-1",
            "instance_id":"instance-1","expected_instance_id":"instance-1","client_id":"producer-1"},
            "artifacts":[],"resolved_configuration":
            """ + configuration + """
            ,"request_lifecycle_requirements":[],"request_capability_supported":false,"observed_broker_names":[],
            "guard_observations":[],"guard_observations_complete":true,"session_diagnostics":[],"is_faulted":false}
            """;
        ParseResult<CapturedCapabilities> parsed = ReadCapabilities(Encoding.UTF8.GetBytes(json));
        Assert.True(parsed.IsSuccess, string.Join(",", parsed.Problems.Select(p => p.Field)));
        return parsed.Value!.ResolvedConfiguration.Value!;
    }

    private static ParseResult<CapturedCapabilities> ReadCapabilities(byte[] bytes)
    {
        InputLimits limits = RecordedEvidence.SmallLimits with { MaxDepth = 32, MaxArtifactBytes = 262_144, MaxTotalInputBytes = 524_288, MaxEntries = 20_000 };
        return CapturedEvidenceReader.ReadCapabilities(bytes, "capabilities.json", limits, new InputBudget(limits));
    }

    private static RunJudgementRequest Request(ResolvedRoleConfiguration selected, CapturedConfiguration captured)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        return request with { ResolvedProducerConfiguration = selected, Capabilities = ParseResult<CapturedCapabilities>.Success(
            request.Capabilities.Value! with { ResolvedConfiguration = ParsedField<CapturedConfiguration>.FromValue(captured) }) };
    }

    private static ConfigurationFidelityResult Evaluate(ResolvedRoleConfiguration selected, CapturedConfiguration captured) =>
        ConfigurationFidelityCheck.Evaluate(Request(selected, captured));

    private static CapturedConfiguration Replace(CapturedConfiguration captured, string key, CapturedSetting setting)
    {
        var settings = captured.Settings.ToDictionary(StringComparer.Ordinal);
        settings[key] = setting;
        return captured with { Settings = settings };
    }

    private static void AssertDisposition(ConfigurationFidelityResult result, AssessmentDisposition disposition)
    {
        Assert.Equal(27, result.Assessment.CheckNumber);
        Assert.Equal(AssessmentEvaluation.Evaluated, result.Assessment.Evaluation);
        Assert.Equal(disposition, result.Assessment.Disposition);
        Assert.Equal(disposition == AssessmentDisposition.Fail ? AssessmentClassification.Invalid : null, result.Assessment.Classification);
        if (disposition == AssessmentDisposition.Pass) Assert.Empty(result.Differences);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scenarios/A01/scenario.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("A01 source fixture was not found.");
    }

    private sealed class MemorySource(IReadOnlyDictionary<string, string> documents) : IScenarioSource
    {
        internal List<string> Requested { get; } = [];
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested.Add(logicalPath);
            if (!documents.TryGetValue(logicalPath, out string? document))
                throw new FileNotFoundException("Synthetic scenario source document is absent.", logicalPath);
            return ValueTask.FromResult<TextReader>(new StringReader(document));
        }
    }
}
