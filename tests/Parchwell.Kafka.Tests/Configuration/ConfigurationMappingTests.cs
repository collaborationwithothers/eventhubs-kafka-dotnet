using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects ADR-015: every setting the harness depends on is emitted explicitly, tagged with its
// inventory IDs and cited, so a change of library default cannot silently change a scenario.
[Trait("Category", "Unit")]
public sealed class ConfigurationMappingTests
{
    // The whole emitted dictionary must equal the hand-written contract, so an added, dropped or retagged
    // key fails. The absent keys are deliberate omissions and alias spellings (kafka-baseline.md).
    [Theory]
    [InlineData(KafkaClientRole.LoadGenProducer)]
    [InlineData(KafkaClientRole.EnrichmentProducer)]
    [InlineData(KafkaClientRole.EnrichmentConsumer)]
    [InlineData(KafkaClientRole.VerifierConsumer)]
    public void Given_ApprovedRole_When_CreatingBaseline_Then_EmitsCompleteIndependentSourceContract(KafkaClientRole role)
    {
        using var provider = Provider(role);
        var (configuration, metadata, omissions) = Create(provider.GetRequiredService<KafkaConfigurationFactory>(), role);
        var expected = ApprovedConfigurationContract.Settings(role);

        Assert.Equal(expected.Select(item => new KeyValuePair<string, string>(item.Key, item.Value.Value)).OrderBy(item => item.Key, StringComparer.Ordinal),
            configuration.OrderBy(item => item.Key, StringComparer.Ordinal));
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), metadata.Select(item => item.Key).Order(StringComparer.Ordinal));
        Assert.All(metadata, item =>
        {
            var contract = expected[item.Key];
            Assert.Equal(role, item.Role);
            Assert.Equal(KafkaSettingDisposition.Explicit, item.Disposition);
            Assert.Equal(contract.Value, item.Value);
            Assert.Equal(contract.Tags.Order(StringComparer.Ordinal), item.InventoryIds.Order(StringComparer.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(item.SelectionReason));
            Assert.Contains(item.Sources, source => source.Url.AbsoluteUri == contract.Source);
            if ((item.Key is "compression.type" or "max.in.flight") ||
                (item.Key == "auto.offset.reset" && role == KafkaClientRole.EnrichmentConsumer))
                Assert.Contains(item.Sources, source => source.Url.AbsoluteUri == ApprovedConfigurationContract.Managed);
            Assert.All(item.Sources, source =>
            {
                Assert.Equal(Uri.UriSchemeHttps, source.Url.Scheme);
                Assert.False(string.IsNullOrWhiteSpace(source.VersionOrDate));
                if (source.Url.AbsoluteUri is ApprovedConfigurationContract.Native or ApprovedConfigurationContract.Managed)
                    Assert.Equal("v2.16.0", source.VersionOrDate);
            });
        });
        Assert.Equal(OmissionKeys(role).Order(StringComparer.Ordinal), omissions.Select(item => item.Key).Order(StringComparer.Ordinal));
        Assert.All(omissions, item =>
        {
            Assert.Equal(role, item.Role);
            Assert.Null(item.Value);
            Assert.Equal(item.Key is "group.instance.id" or "auto.commit.interval.ms" or "enable.auto.offset.store" or "debug"
                ? KafkaSettingDisposition.Unspecified : KafkaSettingDisposition.Excluded, item.Disposition);
            Assert.DoesNotContain(configuration, pair => pair.Key == item.Key);
            Assert.Equal(OmissionTags(item.Key).Order(StringComparer.Ordinal), item.InventoryIds.Order(StringComparer.Ordinal));
            Assert.NotEmpty(item.Sources);
            Assert.False(string.IsNullOrWhiteSpace(item.SelectionReason));
            if (item.Key == "debug")
            {
                Assert.Equal("No debug contexts are selected; the pinned native default enables none.", item.SelectionReason);
                var source = Assert.Single(item.Sources);
                Assert.Equal(ApprovedConfigurationContract.Native, source.Url.AbsoluteUri);
                Assert.Equal("v2.16.0", source.VersionOrDate);
            }
        });
        foreach (string excluded in new[] { "transactional.id", "sasl.username", "sasl.password", "sasl.jaas.config", "sasl.oauthbearer.client.secret", "sasl.oauthbearer.token.endpoint.url", "sasl.oauthbearer.scope", "delivery.report.only.error", "retries", "sasl.mechanisms", "compression.codec", "max.in.flight.requests.per.connection" })
            Assert.Null(configuration.Get(excluded));
    }

    // Distinct inputs per role expose any leak between roles, and reading back through the typed getters
    // checks the managed key spellings the services will rely on (kafka-baseline.md).
    [Fact]
    public void Given_AllNamedRoles_When_CreatingBaselines_Then_UsesIndependentInputsAndNativeTypedValues()
    {
        var services = new ServiceCollection();
        foreach (var role in Enum.GetValues<KafkaClientRole>())
            services.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "synthetic-" + role.ToString().ToLowerInvariant();
                options.ClientId = role + "-pod";
                options.EnrichmentGroupId = "synthetic-" + role;
            });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        var factory = provider.GetRequiredService<KafkaConfigurationFactory>();
        foreach (var role in Enum.GetValues<KafkaClientRole>())
        {
            var (configuration, _, _) = Create(factory, role);
            Assert.Equal("synthetic-" + role.ToString().ToLowerInvariant() + ".servicebus.windows.net:9093", configuration.BootstrapServers);
            Assert.Equal(role + "-pod", configuration.ClientId);
            Assert.Equal(SecurityProtocol.SaslSsl, configuration.SecurityProtocol);
            Assert.Equal(SaslMechanism.OAuthBearer, configuration.SaslMechanism);
            if (configuration is ProducerConfig producer)
            {
                Assert.Equal(Acks.All, producer.Acks);
                Assert.Equal(int.MaxValue, producer.MessageSendMaxRetries);
                Assert.Equal(CompressionType.None, producer.CompressionType);
                Assert.Equal(1000000, producer.MaxInFlight);
                Assert.True(producer.AllowAutoCreateTopics);
                Assert.False(producer.EnableIdempotence);
                Assert.True(producer.EnableBackgroundPoll);
                Assert.True(producer.EnableDeliveryReports);
                Assert.Equal("all", producer.DeliveryReportFields);
            }
            else
            {
                var consumer = Assert.IsType<ConsumerConfig>(configuration);
                Assert.False(consumer.AllowAutoCreateTopics);
                Assert.Equal(IsolationLevel.ReadCommitted, consumer.IsolationLevel);
                if (role == KafkaClientRole.VerifierConsumer)
                {
                    Assert.Equal("parchwell-verifier", consumer.GroupId);
                    Assert.False(consumer.EnableAutoCommit);
                    Assert.Equal(AutoOffsetReset.Error, consumer.AutoOffsetReset);
                    Assert.Null(consumer.EnableAutoOffsetStore);
                    Assert.Null(consumer.AutoCommitIntervalMs);
                }
                else
                {
                    Assert.Equal("synthetic-" + role, consumer.GroupId);
                    Assert.True(consumer.EnableAutoCommit);
                    Assert.False(consumer.EnableAutoOffsetStore);
                    Assert.Equal(AutoOffsetReset.Latest, consumer.AutoOffsetReset);
                }
            }
        }
    }

    // Each result is caller-owned: an edit to one configuration must not reach the next result, and the
    // metadata must keep describing what was first emitted, not later edits.
    [Theory]
    [InlineData(KafkaClientRole.LoadGenProducer)]
    [InlineData(KafkaClientRole.EnrichmentProducer)]
    [InlineData(KafkaClientRole.EnrichmentConsumer)]
    [InlineData(KafkaClientRole.VerifierConsumer)]
    public void Given_CallerMutatedConfig_When_CreatingAgain_Then_PreservesFreshConfigAndImmutableInitialMetadata(KafkaClientRole role)
    {
        using var provider = Provider(role);
        var factory = provider.GetRequiredService<KafkaConfigurationFactory>();
        var (first, metadata, _) = Create(factory, role);
        first.ClientId = "caller-changed";
        first.Set("unapproved.override", "caller-owned");
        var (second, nextMetadata, _) = Create(factory, role);

        Assert.NotSame(first, second);
        Assert.Equal("synthetic-client", second.ClientId);
        Assert.Null(second.Get("unapproved.override"));
        Assert.Equal("synthetic-client", Assert.Single(metadata, item => item.Key == "client.id").Value);
        Assert.Equal("synthetic-client", Assert.Single(nextMetadata, item => item.Key == "client.id").Value);
        Assert.Equal(ApprovedConfigurationContract.Settings(role).Keys.Order(StringComparer.Ordinal), metadata.Select(item => item.Key).Order(StringComparer.Ordinal));
    }

    // The factory snapshots options per call. The verifier group stays fixed whatever the inputs (ADR-013).
    [Theory]
    [InlineData(KafkaClientRole.LoadGenProducer)]
    [InlineData(KafkaClientRole.EnrichmentProducer)]
    [InlineData(KafkaClientRole.EnrichmentConsumer)]
    [InlineData(KafkaClientRole.VerifierConsumer)]
    public void Given_ValidNamedOptionsMutation_When_CreatingAgain_Then_PreservesEarlierSnapshotAndUsesCurrentInputs(KafkaClientRole role)
    {
        using var provider = Provider(role);
        var factory = provider.GetRequiredService<KafkaConfigurationFactory>();
        var (first, firstMetadata, _) = Create(factory, role);
        var configured = provider.GetRequiredService<IOptionsMonitor<KafkaBaselineOptions>>().Get(role.ToString());
        configured.NamespaceName = "synthetic-updated";
        configured.ClientId = "synthetic-updated-client";
        configured.EnrichmentGroupId = "synthetic-updated-group";

        var (second, secondMetadata, _) = Create(factory, role);

        Assert.Equal("synthetic-namespace.servicebus.windows.net:9093", first.BootstrapServers);
        Assert.Equal("synthetic-client", first.ClientId);
        Assert.Equal("synthetic-client", Assert.Single(firstMetadata, item => item.Key == "client.id").Value);
        Assert.Equal("synthetic-updated.servicebus.windows.net:9093", second.BootstrapServers);
        Assert.Equal("synthetic-updated-client", second.ClientId);
        Assert.Equal("synthetic-updated-client", Assert.Single(secondMetadata, item => item.Key == "client.id").Value);
        if (role == KafkaClientRole.EnrichmentConsumer)
        {
            Assert.Equal("synthetic-group", first.Get("group.id"));
            Assert.Equal("synthetic-group", Assert.Single(firstMetadata, item => item.Key == "group.id").Value);
            Assert.Equal("synthetic-updated-group", second.Get("group.id"));
            Assert.Equal("synthetic-updated-group", Assert.Single(secondMetadata, item => item.Key == "group.id").Value);
        }
        else if (role == KafkaClientRole.VerifierConsumer)
        {
            Assert.Equal("parchwell-verifier", first.Get("group.id"));
            Assert.Equal("parchwell-verifier", second.Get("group.id"));
        }
    }

    internal static ServiceProvider Provider(KafkaClientRole role)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(role, Configure);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        return provider;
    }

    internal static void Configure(KafkaBaselineOptions options)
    {
        options.NamespaceName = "synthetic-namespace";
        options.ClientId = "synthetic-client";
        options.EnrichmentGroupId = "synthetic-group";
    }

    private static (ClientConfig Configuration, IReadOnlyList<KafkaSettingMetadata> Settings, IReadOnlyList<KafkaSettingMetadata> Omissions) Create(KafkaConfigurationFactory factory, KafkaClientRole role) => role switch
    {
        KafkaClientRole.LoadGenProducer => AsTuple(factory.CreateLoadGenProducer()),
        KafkaClientRole.EnrichmentProducer => AsTuple(factory.CreateEnrichmentProducer()),
        KafkaClientRole.EnrichmentConsumer => AsTuple(factory.CreateEnrichmentConsumer()),
        KafkaClientRole.VerifierConsumer => AsTuple(factory.CreateVerifierConsumer()),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static (ClientConfig, IReadOnlyList<KafkaSettingMetadata>, IReadOnlyList<KafkaSettingMetadata>) AsTuple<TConfig>(KafkaBaseline<TConfig> baseline) where TConfig : ClientConfig =>
        (baseline.Configuration, baseline.Settings, baseline.Omissions);

    private static string[] OmissionTags(string key) => key switch
    {
        "sasl.username" or "sasl.password" or "sasl.jaas.config" => ["F01"],
        "sasl.oauthbearer.client.id" or "sasl.oauthbearer.client.secret" or "sasl.oauthbearer.token.endpoint.url" or "sasl.oauthbearer.scope" => ["F01", "F02"],
        "transactional.id" => ["B05"],
        "delivery.report.only.error" => ["A01", "B10"],
        "group.instance.id" => ["C06"],
        "debug" => ["C08", "F04"],
        "auto.commit.interval.ms" => ["C09", "C10"],
        "enable.auto.offset.store" => ["C10"],
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static IEnumerable<string> OmissionKeys(KafkaClientRole role)
    {
        string[] common = ["sasl.username", "sasl.password", "sasl.jaas.config", "sasl.oauthbearer.client.id", "sasl.oauthbearer.client.secret", "sasl.oauthbearer.token.endpoint.url", "sasl.oauthbearer.scope"];
        return role switch
        {
            KafkaClientRole.LoadGenProducer or KafkaClientRole.EnrichmentProducer => common.Concat(["transactional.id", "delivery.report.only.error"]),
            KafkaClientRole.EnrichmentConsumer => common.Concat(["group.instance.id", "debug"]),
            KafkaClientRole.VerifierConsumer => common.Concat(["group.instance.id", "auto.commit.interval.ms", "enable.auto.offset.store", "debug"]),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }
}
