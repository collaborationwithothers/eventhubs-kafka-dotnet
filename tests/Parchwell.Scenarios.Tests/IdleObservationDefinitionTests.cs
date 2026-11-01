using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects scenario-contract.md, "Idle observation declarations": the committed A02-A04 files, checks 1 to 7 and the policy entries.
[Trait("Category", "Unit")]
public sealed class IdleObservationDefinitionTests
{
    private const string Block = "idle_observation:\n  action: produce\n  role: LoadGenProducer\n";

    [Theory]
    [InlineData("A02")]
    [InlineData("A03")]
    [InlineData("A04")]
    public async Task Given_CommittedIdleFile_When_LoadingAndResolving_Then_ExposesFixedDeclarationAndExactDeltas(string item)
    {
        var loaded = await Load(item);
        var definition = loaded.Definition;
        Assert.Equal(new IdleObservationDefinition("produce", KafkaClientRole.LoadGenProducer), definition.IdleObservation);
        Assert.Null(definition.SingleAction);
        Assert.Null(definition.ProbeV2);
        Assert.Equal([KafkaClientRole.LoadGenProducer], definition.Roles);
        Assert.Equal(5, definition.Repeats);
        Assert.IsType<NoGate>(definition.Gate);
        Assert.Equal(LiveFaultKind.None, definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, definition.Fault.Component.Kind);
        Assert.Equal(["single_action.client_errors", "single_action.outcome"], definition.Measures.Select(m => m.Id).Order(StringComparer.Ordinal));
        Assert.All(definition.Measures, m => Assert.Equal(KafkaClientRole.LoadGenProducer, m.Role));
        var expected = Expected(item);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), definition.Configurations.Keys.Order(StringComparer.Ordinal));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
        var factory = services.GetRequiredService<KafkaConfigurationFactory>();
        var resolved = new ScenarioResolver(factory).Resolve(loaded);
        foreach (var (id, changes) in expected)
        {
            var settings = factory.CreateLoadGenProducer().Configuration.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            foreach (var (key, value) in changes)
                if (value is null) settings.Remove(key);
                else settings[key] = value;
            var role = resolved.Configurations[id].Roles[KafkaClientRole.LoadGenProducer];
            Assert.Equal(settings.OrderBy(x => x.Key, StringComparer.Ordinal),
                role.Settings.Select(x => KeyValuePair.Create(x.Key, x.Value.Value)).OrderBy(x => x.Key, StringComparer.Ordinal));
            Assert.Equal(changes.Where(c => c.Value is null).Select(c => c.Key), role.ScenarioOmissions.Select(o => o.Key));
        }
    }

    [Fact]
    public async Task Given_CommittedIdleFiles_When_Resolving_Then_KeepTheDecidedLiterals()
    {
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
        var resolver = new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>());
        var a02 = resolver.Resolve(await Load("A02"));
        var a03 = resolver.Resolve(await Load("A03"));
        var a04 = resolver.Resolve(await Load("A04"));
        Assert.Equal("false", Value(a02, "C0", "socket.keepalive.enable"));
        Assert.Equal("true", Value(a02, "C1", "socket.keepalive.enable"));
        Assert.Equal("900000", Value(a03, "C0", "metadata.max.age.ms"));
        Assert.Equal("300000", Value(a03, "C0", "topic.metadata.refresh.interval.ms"));
        Assert.Equal("180000", Value(a03, "C1", "metadata.max.age.ms"));
        Assert.Equal("300000", Value(a03, "C1", "topic.metadata.refresh.interval.ms"));
        Assert.Equal("180000", Value(a03, "C2", "metadata.max.age.ms"));
        Assert.Equal("180000", Value(a03, "C2", "topic.metadata.refresh.interval.ms"));
        Assert.False(a04.Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer].Settings.ContainsKey("connections.max.idle.ms"));
        Assert.Equal("180000", Value(a04, "C1", "connections.max.idle.ms"));
    }

    [Theory]
    [InlineData("A02", Block, "idle_observation:\n  action: produce_one\n  role: LoadGenProducer\n", "idle_observation_literal")]
    [InlineData("A02", Block, "idle_observation:\n  action: null\n  role: LoadGenProducer\n", "idle_observation_literal")]
    [InlineData("A02", Block, "idle_observation:\n  action: produce\n  role: VerifierConsumer\n", "idle_observation_literal")]
    [InlineData("A02", Block, Block + "  topic: document-events\n", "idle_observation_shape")]
    [InlineData("A02", Block, "idle_observation:\n  action: produce\n", "idle_observation_shape")]
    [InlineData("A02", Block, "idle_observation: produce\n", "idle_observation_shape")]
    [InlineData("A02", Block, "idle_observation:\n- produce\n", "idle_observation_shape")]
    [InlineData("A02", Block, "", "idle_observation_required")]
    [InlineData("A02", Block, Block + "single_action:\n  configurations: {}\n", "idle_observation_combined")]
    [InlineData("A02", Block, Block + "probe_v2:\n  configurations: {}\n", "idle_observation_combined")]
    [InlineData("A02", "repeats: 5\n", "repeats: 6\n", "idle_observation_experiment")]
    [InlineData("A02", "roles:\n- LoadGenProducer\n", "roles:\n- LoadGenProducer\n- VerifierConsumer\n", "idle_observation_experiment")]
    [InlineData("A02", "  live:\n    kind: none\n", "  live:\n    kind: throttling\n    configurations:\n    - C0\n", "idle_observation_experiment")]
    [InlineData("A02", "- id: single_action.client_errors\n  role: LoadGenProducer\n", "", "idle_observation_experiment")]
    [InlineData("A02", "measures:\n", "measures:\n- id: stats.broker.disconnects\n  role: LoadGenProducer\n", "idle_observation_experiment")]
    [InlineData("A02", "  C1: {}\n", "  C1: {}\n  C2: {}\n", "idle_observation_configurations")]
    [InlineData("A02", "  C1: {}\n", "", "idle_observation_configurations")]
    [InlineData("A02", "  C0:\n    LoadGenProducer:\n      socket.keepalive.enable: false\n", "  C0: {}\n", "idle_observation_deltas")]
    [InlineData("A02", "  C1: {}\n", "  C1:\n    LoadGenProducer:\n      socket.keepalive.enable: false\n", "idle_observation_deltas")]
    [InlineData("A03", "metadata.max.age.ms: 900000\n", "metadata.max.age.ms: 900001\n", "idle_observation_deltas")]
    [InlineData("A03", "topic.metadata.refresh.interval.ms: 180000\n", "topic.metadata.refresh.interval.ms: 170000\n", "idle_observation_deltas")]
    [InlineData("A03", "      topic.metadata.refresh.interval.ms: 180000\n", "      topic.metadata.refresh.interval.ms: 180000\n      metadata.max.age.ms: 900000\n", "idle_observation_deltas")]
    [InlineData("A04", "connections.max.idle.ms: omit\n", "connections.max.idle.ms: 0\n", "idle_observation_deltas")]
    [InlineData("A04", "connections.max.idle.ms: 180000\n", "connections.max.idle.ms: 179999\n", "idle_observation_deltas")]
    public async Task Given_MutatedIdleFile_When_Loading_Then_RejectsWithTheOwningCheck(string item, string before, string after, string code)
    {
        var original = Text(item);
        Assert.Equal(1, Count(original, before));
        await Reject(item, original.Replace(before, after, StringComparison.Ordinal), code);
    }

    [Fact]
    public async Task Given_IdleBlockOnOtherItems_When_Loading_Then_RejectsAdmission()
    {
        await Reject("A01", ScenarioTestData.ValidScenario + "\n" + Block, "idle_observation_unexpected");
        await Reject("B08", ProbeV2DefinitionTests.Text("B08") + Block, "idle_observation_unexpected", "idle_observation_combined");
    }

    [Theory]
    [InlineData("metadata.max.age.ms", "0", "setting_out_of_range")]
    [InlineData("metadata.max.age.ms", "1", null)]
    [InlineData("metadata.max.age.ms", "86400000", null)]
    [InlineData("metadata.max.age.ms", "86400001", "setting_out_of_range")]
    [InlineData("metadata.max.age.ms", "180000", "no_op_override")]
    [InlineData("topic.metadata.refresh.interval.ms", "-2", "setting_out_of_range")]
    [InlineData("topic.metadata.refresh.interval.ms", "-1", null)]
    [InlineData("topic.metadata.refresh.interval.ms", "3600000", null)]
    [InlineData("topic.metadata.refresh.interval.ms", "3600001", "setting_out_of_range")]
    [InlineData("topic.metadata.refresh.interval.ms", "300000", "no_op_override")]
    [InlineData("connections.max.idle.ms", "-1", "setting_out_of_range")]
    [InlineData("connections.max.idle.ms", "0", "no_op_override")]
    [InlineData("connections.max.idle.ms", "1", null)]
    [InlineData("connections.max.idle.ms", "2147483647", null)]
    [InlineData("connections.max.idle.ms", "omit", "setting_out_of_range")]
    [InlineData("socket.keepalive.enable", "false", null)]
    [InlineData("socket.keepalive.enable", "true", "no_op_override")]
    public async Task Given_IdleKeyOnLoadGenProducer_When_Resolving_Then_AppliesNativeRangesAndA04OnlyOmission(string key, string value, string? code)
    {
        var loaded = await Load("A01", Synthetic("LoadGenProducer", key, value));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var resolver = new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>());
        if (code is null)
        {
            var setting = resolver.Resolve(loaded).Configurations["baseline"].Roles[KafkaClientRole.LoadGenProducer].Settings[key];
            Assert.Equal(value, setting.Value);
            Assert.NotNull(setting.Override);
            return;
        }
        var error = Assert.Throws<ScenarioValidationException>(() => resolver.Resolve(loaded));
        Assert.Equal(code, Assert.Single(error.Diagnostics).Code);
    }

    [Fact]
    public async Task Given_IdleKeyOnVerifierOrOmitOnIntegerKey_When_Loading_Then_Rejects()
    {
        var loaded = await Load("A01", Synthetic("VerifierConsumer", "socket.keepalive.enable", "false"));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var error = Assert.Throws<ScenarioValidationException>(() => new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));
        Assert.Equal("unsupported_setting", Assert.Single(error.Diagnostics).Code);
        await Reject("A01", Synthetic("LoadGenProducer", "metadata.max.age.ms", "omit"), "int32_required");
    }

    private static string Synthetic(string role, string key, string value)
    {
        Assert.Equal(1, Count(ScenarioTestData.ValidScenario, "  baseline: {}"));
        return ScenarioTestData.ValidScenario.Replace("  baseline: {}", "  baseline: {" + role + ": {" + key + ": " + value + "}}", StringComparison.Ordinal);
    }

    private static string Value(ResolvedScenario scenario, string configuration, string key) =>
        scenario.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer].Settings[key].Value;

    private static Dictionary<string, (string Key, string? Value)[]> Expected(string item) => item switch
    {
        "A02" => new() { ["C0"] = [("socket.keepalive.enable", "false")], ["C1"] = [] },
        "A03" => new() { ["C0"] = [("metadata.max.age.ms", "900000")], ["C1"] = [], ["C2"] = [("topic.metadata.refresh.interval.ms", "180000")] },
        "A04" => new() { ["C0"] = [("connections.max.idle.ms", null)], ["C1"] = [("connections.max.idle.ms", "180000")] },
        _ => throw new ArgumentOutOfRangeException(nameof(item))
    };

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal)) count++;
        return count;
    }

    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }

    private static string Text(string item) => File.ReadAllText(Path.Combine(Root(), "scenarios", item, "scenario.yaml"));

    private static async Task<LoadedScenario> Load(string item, string? yaml = null)
    {
        var source = item == "A01" ? ScenarioTestData.Source(yaml!) :
            new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml ?? Text(item)),
                ("scenarios/profiles/saturation-ramp.yaml", File.ReadAllText(Path.Combine(Root(), "scenarios", "profiles", "saturation-ramp.yaml"))));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }

    private static async Task Reject(string item, string yaml, params string[] codes)
    {
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(item, yaml));
        foreach (var code in codes) Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == code);
    }
}
