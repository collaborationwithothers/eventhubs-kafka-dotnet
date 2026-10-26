using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class SingleActionDefinitionTests
{
    private static readonly string[] RequiredMeasures = ["single_action.client_errors", "single_action.outcome"];
    private static readonly string[] RemovedConsumerKeys = ["heartbeat.interval.ms", "partition.assignment.strategy", "session.timeout.ms"];
    [Theory]
    [InlineData("single_action.outcome", "outcome_class")]
    [InlineData("single_action.client_errors", "error_code")]
    public void Given_SingleActionMeasurement_When_ReadingMetadata_Then_PreservesApprovedParentLiteral(string id, string kind)
    {
        Assert.True(MeasurementCatalog.TryGet(id, out var descriptor));
        Assert.Equal("single_action", descriptor.Source);
        Assert.Equal(kind, descriptor.ValueKind);
        Assert.Equal("none", descriptor.Unit);
        Assert.Equal("repeat", descriptor.Scope);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer], descriptor.Roles);
        Assert.Equal("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278", descriptor.SourceUrl);
        Assert.Equal("plan 6415356278 revision 4; 2026-10-26", descriptor.VersionOrDate);
    }

    [Fact]
    public async Task Given_SixApprovedFiles_When_LoadingAndResolving_Then_ExactlySixteenConfigurationsAndEightyRepeats()
    {
        var configurations = 0;
        var repeats = 0;
        foreach (var item in new[] { "B01", "B03", "B05", "C08", "C12", "F04" })
        {
            var loaded = await Load(item);
            Assert.Equal(5, loaded.Definition.Repeats);
            KafkaClientRole[] expectedRoles = item == "F04" ? [KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer] :
                item is "C08" or "C12" ? [KafkaClientRole.EnrichmentConsumer] : [KafkaClientRole.LoadGenProducer];
            Assert.Equal(expectedRoles, loaded.Definition.Roles);
            string[] expectedConfigurations = item is "B01" or "C08" ? ["C0", "C1"] : item == "B03" ? ["C1", "C2", "C3", "C4"] :
                item == "B05" ? ["C1"] : item == "C12" ? ["C1", "C2"] : ["C0", "C1", "C2", "C3", "C4"];
            Assert.Equal(expectedConfigurations, loaded.Definition.Configurations.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("profiles/saturation-ramp.yaml", loaded.Definition.Profile);
            Assert.IsType<NoGate>(loaded.Definition.Gate);
            Assert.Equal(LiveFaultKind.None, loaded.Definition.Fault.Live.Kind);
            Assert.Equal(ComponentFaultKind.None, loaded.Definition.Fault.Component.Kind);
            var action = Assert.IsType<SingleActionDefinition>(loaded.Definition.SingleAction);
            Assert.Equal(loaded.Definition.Configurations.Keys.Order(StringComparer.Ordinal), action.Configurations.Keys.Order(StringComparer.Ordinal));
            using var provider = ScenarioTestData.Provider([.. loaded.Definition.Roles]);
            var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
            foreach (var (id, row) in action.Configurations)
            {
                Assert.Equal(0, row.Partition);
                Assert.Equal(item == "F04" && id is "C1" or "C2" ? "document-state" : "document-events", row.Topic);
                Assert.Equal(item == "F04" && id == "C3" ? "storage" : "namespace", row.TokenScope);
                Assert.Equal(item == "F04" && id is "C0" or "C2" or "C4" ? "f04_seed" : "none", row.Match);
                Assert.Equal(item is "C08" or "C12" ? "subscribe" : item == "B05" ? "init_transactions" : item == "F04" && id is "C2" or "C4" ? "assign_consume" : "produce_one", row.Action);
                Assert.Equal(row.Action == "produce_one" ? 330000 : row.Action == "init_transactions" ? 150000 : 120000, row.WindowMs);
                Assert.Equal(row.Action is "subscribe" or "init_transactions" ? 0 : item == "B01" && id == "C1" ? 1100000 : 1000, row.PayloadBytes);
                Assert.Equal(row.Action == "subscribe" ? KafkaClientRole.EnrichmentConsumer : row.Action == "assign_consume" ? KafkaClientRole.VerifierConsumer : KafkaClientRole.LoadGenProducer, row.Role);
                Assert.Equal(RequiredMeasures, loaded.Definition.Measures.Where(measure => measure.Role == row.Role).Select(measure => measure.Id).Order(StringComparer.Ordinal));
                var deltas = loaded.Definition.Configurations[id].Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}"));
                Assert.Equal(ExpectedDeltas(item, id).Order(StringComparer.Ordinal), deltas.Order(StringComparer.Ordinal));
                var actual = resolved.Configurations[id].Roles;
                foreach (var (role, roleConfiguration) in actual)
                {
                    var expected = ApprovedConfigurationContract.Settings(role).ToDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal);
                    foreach (var delta in loaded.Definition.Configurations[id].Overrides.GetValueOrDefault(role) ?? [])
                    {
                        if (delta.Value.Value == "omit") expected.Remove(delta.Key);
                        else expected[delta.Key] = delta.Value.Value;
                    }
                    Assert.Equal(expected.OrderBy(x => x.Key, StringComparer.Ordinal), roleConfiguration.Settings.Select(x => KeyValuePair.Create(x.Key, x.Value.Value)).OrderBy(x => x.Key, StringComparer.Ordinal));
                }
            }
            configurations += action.Configurations.Count;
            repeats += action.Configurations.Count * loaded.Definition.Repeats;
        }
        Assert.Equal(16, configurations);
        Assert.Equal(80, repeats);
    }

    [Fact]
    public async Task Given_TransactionalActivationAndConsumerOmissions_When_Resolving_Then_RetainsOriginalCitedMetadataAndYamlOrigin()
    {
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer);
        var resolver = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>());
        var producer = resolver.Resolve(await Load("B05")).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
        var transaction = producer.Settings["transactional.id"];
        Assert.Null(transaction.Baseline.Value);
        Assert.NotEqual(KafkaSettingDisposition.Explicit, transaction.Baseline.Disposition);
        Assert.NotEmpty(transaction.Baseline.Sources);
        Assert.Equal("b05-probe", transaction.Value);
        Assert.Equal("B05", transaction.Override?.ScenarioId);
        Assert.DoesNotContain(producer.Omissions, item => item.Key == "transactional.id");
        Assert.Empty(producer.ScenarioOmissions);
        var consumer = resolver.Resolve(await Load("C08"));
        var baseline = consumer.Configurations["C0"].Roles[KafkaClientRole.EnrichmentConsumer];
        var changed = consumer.Configurations["C1"].Roles[KafkaClientRole.EnrichmentConsumer];
        Assert.Equal(RemovedConsumerKeys, changed.ScenarioOmissions.Select(x => x.Key).Order(StringComparer.Ordinal));
        foreach (var omission in changed.ScenarioOmissions)
        {
            Assert.False(changed.Settings.ContainsKey(omission.Key));
            Assert.Same(baseline.Settings[omission.Key].Baseline, omission.Baseline);
            Assert.Equal("C08", omission.Origin.ScenarioId);
            Assert.Equal("C1", omission.Origin.ConfigurationId);
            Assert.Equal("scenarios/C08/scenario.yaml", omission.Origin.Location.Path);
            Assert.NotNull(omission.Origin.Location.Line);
            Assert.NotNull(omission.Origin.Location.Column);
        }
        Assert.Empty(baseline.ScenarioOmissions);
        Assert.Equal(baseline.Omissions, changed.Omissions);
    }

    [Fact]
    public async Task Given_GroupLengthExperiment_When_Loading_Then_PreservesBothAsciiLengths()
    {
        var definition = (await Load("C12")).Definition;
        var first = definition.Configurations["C1"].Overrides[KafkaClientRole.EnrichmentConsumer]["group.id"].Value;
        var second = definition.Configurations["C2"].Overrides[KafkaClientRole.EnrichmentConsumer]["group.id"].Value;
        Assert.Equal("c12-" + new string('g', 252), first);
        Assert.Equal("c12-" + new string('g', 253), second);
        Assert.Equal(256, first.Length);
        Assert.Equal(257, second.Length);
    }

    [Theory]
    [InlineData("action: produce_one", "action: subscribe", "single_action_action")]
    [InlineData("topic: document-events", "topic: unknown", "single_action_topic")]
    [InlineData("partition: 0", "partition: 1", "single_action_partition")]
    [InlineData("payload_bytes: 1000", "payload_bytes: 999", "single_action_payload")]
    [InlineData("window_ms: 330000", "window_ms: 329999", "single_action_window")]
    [InlineData("token_scope: namespace", "token_scope: storage", "single_action_token_scope")]
    [InlineData("match: none", "match: f04_seed", "single_action_match")]
    [InlineData("role: LoadGenProducer", "role: VerifierConsumer", "single_action_role")]
    [InlineData("repeats: 5", "repeats: 6", "single_action_repeats")]
    [InlineData("single_action.outcome", "ledger.delivery_error_code", "single_action_measures")]
    [InlineData("message.max.bytes: 2000000", "message.max.bytes: 2000001", "single_action_deltas")]
    [InlineData("single_action:\n  configurations:", "single_action:\n  surprise: 1\n  configurations:", "unknown_field")]
    [InlineData("      match: none\n", "", "required_field")]
    [InlineData("partition: 0", "partition: '0'", "int32_required")]
    [InlineData("partition: 0", "partition: 2147483648", "int32_required")]
    [InlineData("topic: document-events", "topic: null", "scalar_required")]
    public async Task Given_MutatedLiteralDeclaration_When_Loading_Then_RejectsWithOwningCode(string before, string after, string code)
    {
        var yaml = Text("B01").Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(Text("B01"), yaml);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("B01", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Theory]
    [InlineData("'omit'")]
    [InlineData("null")]
    [InlineData("~")]
    public async Task Given_NonplainOmission_When_Loading_Then_Rejects(string value)
    {
        var yaml = Text("C08").Replace("session.timeout.ms: omit", "session.timeout.ms: " + value, StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("C08", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "int32_required");
    }

    [Fact]
    public async Task Given_MissingSingleAction_When_LoadingSelectedItem_Then_RequiresDeclaration()
    {
        var yaml = Text("B01").Split("single_action:", StringSplitOptions.None)[0];
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("B01", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "single_action_required");
    }

    [Fact]
    public async Task Given_SingleActionOnLegacyA01_When_Loading_Then_RejectsUnexpectedDeclaration()
    {
        var yaml = ScenarioTestData.ValidScenario + "\nsingle_action: {configurations: {}}\n";
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await ScenarioTestData.Loader(ScenarioTestData.Source(yaml)).LoadAsync("A01", TestContext.Current.CancellationToken));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "single_action_unexpected");
    }

    [Theory]
    [InlineData("single_action:\n  configurations:", "single_action: []\nunused:", "mapping_required")]
    [InlineData("    C0:\n      action:", "    unexpected:\n      action:", "single_action_configurations")]
    [InlineData("  live:\n    kind: none", "  live:\n    kind: throttling\n    configurations: [C0]", "single_action_fault")]
    [InlineData("  component:\n    kind: none", "  component:\n    kind: toxiproxy_latency\n    configurations: [C0]\n    direction: downstream\n    latency_ms: 1", "single_action_fault")]
    public async Task Given_StructuralOrFaultMutation_When_Loading_Then_RejectsOwningCheck(string before, string after, string code)
    {
        var yaml = Text("B01").Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(Text("B01"), yaml);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("B01", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task Given_ProbeGate_When_LoadingSingleAction_Then_RejectsGate()
    {
        var yaml = Text("B01").Replace("gate:\n  kind: none", """
            gate:
              kind: probe
              configuration: C0
              repeats: 5
              configurations: [C1]
              predicate:
                id: stats.broker.throttle.max
                role: LoadGenProducer
                operator: greater_than
                threshold_ms: 1
            """, StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("B01", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "single_action_gate");
    }

    [Fact]
    public async Task Given_AnotherInventoryItemWithSingleAction_When_Loading_Then_RejectsItem()
    {
        var yaml = Text("B01").Replace("id: B01", "id: B02", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("B02", yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "single_action_item");
    }

    private static string[] ExpectedDeltas(string item, string configuration) => (item, configuration) switch
    {
        ("B01", _) => ["LoadGenProducer:message.max.bytes=2000000"],
        ("B03", "C1") => ["LoadGenProducer:compression.type=gzip"],
        ("B03", "C2") => ["LoadGenProducer:compression.type=snappy"],
        ("B03", "C3") => ["LoadGenProducer:compression.type=lz4"],
        ("B03", "C4") => ["LoadGenProducer:compression.type=zstd"],
        ("B05", "C1") => ["LoadGenProducer:transactional.id=b05-probe", "LoadGenProducer:enable.idempotence=true", "LoadGenProducer:max.in.flight=5", "LoadGenProducer:message.timeout.ms=60000"],
        ("C08", "C1") => ["EnrichmentConsumer:group.protocol=consumer", "EnrichmentConsumer:session.timeout.ms=omit", "EnrichmentConsumer:heartbeat.interval.ms=omit", "EnrichmentConsumer:partition.assignment.strategy=omit"],
        ("C12", "C1") => ["EnrichmentConsumer:group.id=c12-" + new string('g', 252)],
        ("C12", "C2") => ["EnrichmentConsumer:group.id=c12-" + new string('g', 253)],
        _ => []
    };

    internal static string Text(string item) => Resource("Parchwell.Scenarios.Tests.SingleAction." + item + ".scenario.yaml");
    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    private static async Task<LoadedScenario> Load(string item, string? yaml = null)
    {
        var source = new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml ?? Text(item)),
            ("scenarios/profiles/saturation-ramp.yaml", Resource("Parchwell.Scenarios.Tests.A01.saturation-ramp.yaml")));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }
}
