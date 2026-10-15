using Parchwell.Scenarios;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioLoaderTests
{
    // The loader opens only scenario.yaml and its profile and disposes both readers. It must never
    // open expected.yaml: the hypotheses cannot be allowed to affect a run (ADR-014).
    [Fact]
    public async Task Given_ValidMixedBlockAndFlowDocuments_When_Loading_Then_UsesLogicalPathsAndReturnsTypedDeclaration()
    {
        var source = ScenarioTestData.Source();

        var loaded = await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal(1, loaded.Definition.SchemaVersion);
        Assert.Equal("A01", loaded.Definition.Id);
        Assert.Equal("Parchwell.Kafka", loaded.Definition.Baseline);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], loaded.Definition.Roles);
        Assert.Equal("baseline", Assert.Single(loaded.Definition.Configurations).Key);
        Assert.Empty(Assert.Single(loaded.Definition.Configurations).Value.Overrides);
        Assert.Equal(5, loaded.Definition.Repeats);
        Assert.Equal("synthetic", loaded.Profile.Id);
        Assert.Equal(-17, loaded.Profile.Seed);
        Assert.Equal([new TenantWeight("tenant-a", 3), new TenantWeight("tenant-b", 1)], loaded.Profile.Tenants);
        Assert.Equal(2, loaded.Profile.DocumentsPerBurst);
        Assert.Equal([new PayloadBucket(128, 1), new PayloadBucket(512, 2)], loaded.Profile.Payloads["uploaded"]);
        Assert.Equal([new LoadStage("warmup", 2, 3), new LoadStage("measure", 5, 4)], loaded.Profile.Stages);
        Assert.Equal([ScenarioTestData.ScenarioPath, ScenarioTestData.ProfilePath], source.RequestedPaths);
        Assert.All(source.Readers, reader => Assert.True(reader.WasDisposed));
        Assert.DoesNotContain(source.RequestedPaths, path => path.EndsWith("expected.yaml", StringComparison.Ordinal));
    }

    // The YAML restrictions limit features, not layout: ordinary flow style is valid (scenario-contract.md).
    [Fact]
    public async Task Given_FlowRootDocuments_When_Loading_Then_AcceptsOrdinaryFlowYaml()
    {
        var source = ScenarioTestData.Source(
            "{schema_version: 1, id: A01, baseline: Parchwell.Kafka, roles: [LoadGenProducer, VerifierConsumer], profile: profiles/synthetic.yaml, configurations: {baseline: {}}, fault: {live: {kind: none}, component: {kind: none}}, repeats: 5, gate: {kind: none}, measures: [{id: verifier.ok, role: VerifierConsumer}]}",
            ScenarioTestData.ValidProfile);

        var loaded = await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal("A01", loaded.Definition.Id);
        Assert.Equal("synthetic", loaded.Profile.Id);
    }

    // Unknown, duplicate and case-mismatched fields and an unsupported schema_version are rejected, so a
    // typo can never be silently ignored and leave a setting at its baseline.
    [Theory]
    [InlineData("schema_version: 2", "schema_version")]
    [InlineData("Schema_version: 1", "Schema_version")]
    [InlineData("future_field: x", "future_field")]
    [InlineData("id: A01\nid: A01", "id")]
    [InlineData("profile: profiles/synthetic.yaml\nprofile: profiles/synthetic.yaml", "profile")]
    public async Task Given_InvalidScenarioMapping_When_Loading_Then_ReportsFieldAndSource(string addition, string field)
    {
        var scenario = addition.StartsWith("schema_version:", StringComparison.Ordinal)
            ? ScenarioTestData.ValidScenario.Replace("schema_version: 1", addition, StringComparison.Ordinal)
            : ScenarioTestData.ValidScenario + "\n" + addition + "\n";
        var source = ScenarioTestData.Source(scenario);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.Source == ScenarioTestData.ScenarioPath && item.FieldPath.Contains(field, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_UnknownFieldAtKnownSourcePosition_When_Loading_Then_ReportsExactYamlMark()
    {
        var source = ScenarioTestData.Source(ScenarioTestData.ValidScenario + "\nfuture_field: x\n");

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken));
        var diagnostic = Assert.Single(exception.Diagnostics, item => item.FieldPath == "future_field");

        Assert.Equal(ScenarioTestData.ScenarioPath, diagnostic.Source);
        Assert.Equal(15, diagnostic.Line);
        Assert.Equal(15, diagnostic.Column);
    }

    // scenario-contract.md rejects multiple documents, anchors, merge keys and explicit tags. Each has a
    // stable code, and callers may compare codes (see its diagnostic code migration section).
    [Theory]
    [InlineData("schema_version: 1\n---\nschema_version: 1", "MultipleDocuments")]
    [InlineData("schema_version: 1\nid: A01\nbaseline: Parchwell.Kafka\nroles: &shared [LoadGenProducer, VerifierConsumer]\nprofile: profiles/synthetic.yaml\nconfigurations: {baseline: {}}\nfault: {live: {kind: none}, component: {kind: none}}\nrepeats: 5\ngate: {kind: none}\nmeasures: [{id: verifier.ok, role: VerifierConsumer}]\n", "AnchorNotAllowed")]
    [InlineData("schema_version: 1\nid: A01\nbaseline: Parchwell.Kafka\nroles: [LoadGenProducer]\nprofile: profiles/synthetic.yaml\nconfigurations: {baseline: {<<: {}}}\nfault: {live: {kind: none}, component: {kind: none}}\nrepeats: 5\ngate: {kind: none}\nmeasures: [{id: verifier.ok, role: VerifierConsumer}]\n", "MergeKeyNotAllowed")]
    [InlineData("schema_version: !!str 1", "ExplicitTagNotAllowed")]
    public async Task Given_UnsafeYamlFeature_When_Loading_Then_RejectsWithStableDiagnostic(string yaml, string code)
    {
        var scenario = yaml is "schema_version: 1\n---\nschema_version: 1"
            ? ScenarioTestData.ValidScenario + "\n---\nschema_version: 1\n"
            : yaml == "schema_version: !!str 1" ? ScenarioTestData.ValidScenario.Replace("schema_version: 1", yaml, StringComparison.Ordinal)
            : yaml;
        var source = ScenarioTestData.Source(scenario);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.Code == code);
    }

    // One definition file per inventory item (ADR-014): a file under scenarios/A02 that declares A01
    // would run the wrong experiment.
    [Fact]
    public async Task Given_RequestedIdDiffersFromDocumentId_When_Loading_Then_RejectsIdentityMismatch()
    {
        var source = new InMemoryScenarioSource(("scenarios/A02/scenario.yaml", ScenarioTestData.ValidScenario),
            (ScenarioTestData.ProfilePath, ScenarioTestData.ValidProfile));
        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await ScenarioTestData.Loader(source).LoadAsync("A02", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.FieldPath.Contains("id", StringComparison.Ordinal));
    }

    // Profiles must come from scenarios/profiles/ only; an absolute path, URL, parent traversal or
    // backslash is rejected before anything outside that folder can be opened.
    [Theory]
    [InlineData("/tmp/profile.yaml")]
    [InlineData("https://example.test/profile.yaml")]
    [InlineData("profiles/../private.yaml")]
    [InlineData("profiles\\synthetic.yaml")]
    public async Task Given_InvalidProfileReference_When_Loading_Then_RejectsWithoutOpeningProfile(string profile)
    {
        var scenario = ScenarioTestData.ValidScenario.Replace("profiles/synthetic.yaml", profile, StringComparison.Ordinal);
        var source = ScenarioTestData.Source(scenario);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.FieldPath == "profile");
        Assert.Equal([ScenarioTestData.ScenarioPath], source.RequestedPaths);
    }

    // repeats below 5 breaks the factory minimum (docs/agents/factory.md plan template). The other
    // cases are role and measurement rules from scenario-contract.md "Scenario schema".
    [Theory]
    [InlineData("roles: [loadgenproducer, VerifierConsumer]", "unknown_role")]
    [InlineData("roles: [LoadGenProducer, LoadGenProducer]", "duplicate_role")]
    [InlineData("repeats: 4", "below_minimum")]
    [InlineData("measures:\n  - {id: verifier.ok, role: LoadGenProducer}", "measurement_role_mismatch")]
    [InlineData("measures:\n  - {id: not.catalogued, role: VerifierConsumer}", "unknown_measurement")]
    [InlineData("measures:\n  - {id: verifier.ok, role: VerifierConsumer}\n  - {id: verifier.ok, role: VerifierConsumer}", "duplicate_measurement")]
    public async Task Given_InvalidScenarioContractValue_When_Loading_Then_RejectsAtItsField(string replacement, string code)
    {
        var scenario = ReplaceRootField(ScenarioTestData.ValidScenario, replacement);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(scenario)).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.Code == code);
    }

    // Declaring a measurement is separate from collecting it (scenario-contract.md), so a catalogued
    // statistic loads even though no collector exists yet.
    [Fact]
    public async Task Given_CataloguedBrokerStatisticWithoutImplementedCollector_When_Loading_Then_AcceptsDeclaration()
    {
        var scenario = ReplaceRootField(ScenarioTestData.ValidScenario,
            "measures:\n  - {id: stats.broker.throttle.max, role: LoadGenProducer}");

        var loaded = await ScenarioTestData.Loader(ScenarioTestData.Source(scenario)).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal(new MeasurementRequest("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer), Assert.Single(loaded.Definition.Measures));
    }

    // Faults and the probe may only name declared configurations. The probe also needs its own
    // minimum repeats and its predicate measurement listed in measures.
    [Theory]
    [InlineData("fault:\n  live: {kind: throttling, configurations: [absent]}\n  component: {kind: none}", "unknown_configuration")]
    [InlineData("gate:\n  kind: probe\n  configuration: absent\n  repeats: 5\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater_than, threshold_ms: 1}\n  configurations: [baseline]", "unknown_configuration")]
    [InlineData("gate:\n  kind: probe\n  configuration: baseline\n  repeats: 4\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater_than, threshold_ms: 1}\n  configurations: [baseline]", "below_minimum")]
    [InlineData("gate:\n  kind: probe\n  configuration: baseline\n  repeats: 5\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater_than, threshold_ms: 1}\n  configurations: [baseline]", "predicate_measure_missing")]
    public async Task Given_InvalidFaultOrProbeReference_When_Loading_Then_RejectsTheDeclaredPhase(string replacement, string code)
    {
        var scenario = ReplaceRootField(ScenarioTestData.ValidScenario, replacement);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(scenario)).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.Code == code);
    }

    // scenario-contract.md "Profile schema": numbers are plain Int32 and tenant, payload size and stage
    // identifiers are unique. The diagnostic must name the profile file, not the scenario.
    [Theory]
    [InlineData("seed: \"17\"")]
    [InlineData("seed: 2147483648")]
    [InlineData("tenants:\n  - {id: tenant-a, weight: 1}\n  - {id: tenant-a, weight: 2}")]
    [InlineData("payloads:\n  uploaded: [{size_bytes: 128, weight: 1}, {size_bytes: 128, weight: 2}]\n  ocr-started: [{size_bytes: 64, weight: 1}]\n  ocr-finished: [{size_bytes: 96, weight: 1}]\n  classified: [{size_bytes: 80, weight: 1}]\n  indexed: [{size_bytes: 40, weight: 1}]")]
    [InlineData("stages:\n  - {id: warmup, offered_attempts_per_second: 2, duration_seconds: 3}\n  - {id: warmup, offered_attempts_per_second: 5, duration_seconds: 4}")]
    public async Task Given_InvalidProfileValues_When_Loading_Then_RejectsProfile(string replacement)
    {
        var profile = ReplaceRootField(ScenarioTestData.ValidProfile, replacement);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(profile: profile)).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.Source == ScenarioTestData.ProfilePath);
    }

    // Each weight is Int32 but sums are checked in Int64, so two maximum weights are still valid.
    [Fact]
    public async Task Given_TenantWeightSumAboveInt32_When_Loading_Then_KeepsTheValidInt64Total()
    {
        var profile = ReplaceRootField(ScenarioTestData.ValidProfile,
            "tenants:\n  - {id: tenant-a, weight: 2147483647}\n  - {id: tenant-b, weight: 2147483647}");

        var loaded = await ScenarioTestData.Loader(ScenarioTestData.Source(profile: profile)).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal([new TenantWeight("tenant-a", int.MaxValue), new TenantWeight("tenant-b", int.MaxValue)], loaded.Profile.Tenants);
    }

    // Planned attempts (rate times duration, summed over stages) must fit in Int64; overflow is
    // rejected rather than wrapped into a wrong total.
    [Fact]
    public async Task Given_PlannedAttemptTotalOverflowsInt64_When_Loading_Then_RejectsAggregate()
    {
        var profile = ReplaceRootField(ScenarioTestData.ValidProfile,
            "stages:\n  - {id: huge-a, offered_attempts_per_second: 2147483647, duration_seconds: 2147483647}\n  - {id: huge-b, offered_attempts_per_second: 2147483647, duration_seconds: 2147483647}\n  - {id: huge-c, offered_attempts_per_second: 2147483647, duration_seconds: 2147483647}");

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(profile: profile)).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, item => item.FieldPath == "stages" && item.Code == "int64_overflow");
    }

    // Budgets are inclusive. These values are the valid fixtures' own maxima; the next test goes one
    // below each and expects rejection.
    [Fact]
    public async Task Given_CharacterDepthAndScalarLimitsAtDocumentMaximum_When_Loading_Then_AcceptsExactLimits()
    {
        var options = ScenarioTestData.Limits(document: Math.Max(ScenarioTestData.ValidScenario.Length, ScenarioTestData.ValidProfile.Length),
            depth: 4, scalar: 27, events: 102);

        var loaded = await ScenarioTestData.Loader(ScenarioTestData.Source(), options).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal("A01", loaded.Definition.Id);
    }

    // Any one budget alone stops loading. A document over a limit is rejected, never truncated, so a
    // partial declaration can never load as if it were complete.
    [Theory]
    [InlineData("characters")]
    [InlineData("depth")]
    [InlineData("scalar")]
    [InlineData("events")]
    public async Task Given_DocumentExceedsOneConfiguredBudget_When_Loading_Then_RejectsInsteadOfTruncating(string budget)
    {
        var options = budget switch
        {
            "characters" => ScenarioTestData.Limits(document: Math.Max(ScenarioTestData.ValidScenario.Length, ScenarioTestData.ValidProfile.Length) - 1),
            "depth" => ScenarioTestData.Limits(depth: 3),
            "scalar" => ScenarioTestData.Limits(scalar: 26),
            _ => ScenarioTestData.Limits(events: 101)
        };

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(), options).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.NotEmpty(exception.Diagnostics);
    }

    // The loader owns every reader it opens and must dispose it on failure too
    // (scenario-contract.md "Input and ownership").
    [Fact]
    public async Task Given_ScenarioReaderOpened_When_ProfileParsingFails_Then_DisposesEveryReader()
    {
        var source = ScenarioTestData.Source(profile: "schema_version: 1\nid: wrong\n");

        await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Equal(2, source.Readers.Count);
        Assert.All(source.Readers, reader => Assert.True(reader.WasDisposed));
    }

    // Cancellation applies to opening, reading and parsing (scenario-contract.md); these two tests
    // cover a token cancelled before the first open and one cancelled during it.
    [Fact]
    public async Task Given_CancellationBeforeOpening_When_Loading_Then_DoesNotOpenAnySource()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = ScenarioTestData.Source();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", cancellation.Token));

        Assert.Empty(source.RequestedPaths);
    }

    [Fact]
    public async Task Given_CancellationDuringReaderOpen_When_Loading_Then_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new CancellingScenarioSource(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ScenarioTestData.Loader(source).LoadAsync("A01", cancellation.Token));

        Assert.Empty(source.Readers);
    }

    /// <summary>A source that cancels the token inside the open call, to test cancellation during opening.</summary>
    private sealed class CancellingScenarioSource(CancellationTokenSource cancellation) : IScenarioSource
    {
        internal List<TextReader> Readers { get; } = [];

        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable.");
        }
    }

    // Removes a top-level field and its indented children, then appends the replacement at the end.
    private static string ReplaceRootField(string yaml, string replacement)
    {
        var field = replacement[..replacement.IndexOf(':', StringComparison.Ordinal)];
        var retained = new List<string>();
        var skippingChildren = false;
        foreach (var line in yaml.Split('\n'))
        {
            if (line.StartsWith(field + ":", StringComparison.Ordinal))
            {
                skippingChildren = true;
                continue;
            }
            if (skippingChildren && line.Length > 0 && !char.IsWhiteSpace(line[0])) skippingChildren = false;
            if (!skippingChildren) retained.Add(line);
        }
        return string.Join('\n', retained) + "\n" + replacement + "\n";
    }
}
