using System.Reflection;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

/// <summary>Native local construction evidence, never a broker or Azure outcome.</summary>
[Trait("Category", "Native")]
public sealed class SingleActionConstructionTests
{
    [Theory]
    [InlineData("B01", "C0")]
    [InlineData("B01", "C1")]
    [InlineData("B03", "C1")]
    [InlineData("B03", "C2")]
    [InlineData("B03", "C3")]
    [InlineData("B03", "C4")]
    [InlineData("B05", "C1")]
    [InlineData("C08", "C0")]
    [InlineData("C08", "C1")]
    [InlineData("C12", "C1")]
    [InlineData("C12", "C2")]
    [InlineData("F04", "C0")]
    [InlineData("F04", "C1")]
    [InlineData("F04", "C2")]
    [InlineData("F04", "C3")]
    [InlineData("F04", "C4")]
    public async Task Given_ResolvedSingleActionConfiguration_When_Building_Then_NativeTwoSixteenClientConstructs(string item, string configuration)
    {
        using var services = Services();
        var (settings, role) = await Resolve(services, item, configuration);
        Assert.Equal("2.16.0", Library.VersionString);
        if (item == "C08" && configuration == "C1")
        {
            Assert.Equal("consumer", settings["group.protocol"]);
            Assert.DoesNotContain("session.timeout.ms", settings.Keys);
            Assert.DoesNotContain("heartbeat.interval.ms", settings.Keys);
            Assert.DoesNotContain("partition.assignment.strategy", settings.Keys);
        }
        if (item == "B05")
        {
            Assert.Equal("b05-probe", settings["transactional.id"]);
            Assert.Equal("true", settings["enable.idempotence"]);
            Assert.Equal("5", settings["max.in.flight"]);
            Assert.Equal("60000", settings["message.timeout.ms"]);
        }
        if (item == "C12") Assert.Equal(configuration == "C1" ? 256 : 257, settings["group.id"].Length);
        if (item == "C08" && configuration == "C1") Assert.Equal("cgrp", settings["debug"]);
        else if (item == "F04" && configuration == "C2") Assert.Equal("topic,fetch,metadata", settings["debug"]);
        else Assert.DoesNotContain("debug", settings.Keys);
        Build(services, settings, role);
    }

    [Theory]
    [InlineData("session.timeout.ms", "30000")]
    [InlineData("heartbeat.interval.ms", "3000")]
    [InlineData("partition.assignment.strategy", "range")]
    public async Task Given_ConsumerProtocolWithForbiddenExplicitSetting_When_Building_Then_NativeRejects(string key, string value)
    {
        using var services = Services();
        var (settings, role) = await Resolve(services, "C08", "C1");
        settings[key] = value;
        var exception = Assert.ThrowsAny<Exception>(() => Build(services, settings, role));
        Assert.Contains(key, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("enable.idempotence", "false", "transactional.id")]
    [InlineData("max.in.flight", "1000000", "max.in.flight")]
    [InlineData("message.timeout.ms", "300000", "message.timeout.ms")]
    public async Task Given_TransactionConfigurationWithBaselineContradiction_When_Building_Then_NativeRejects(string key, string value, string errorKey)
    {
        using var services = Services();
        var (settings, role) = await Resolve(services, "B05", "C1");
        settings[key] = value;
        var exception = Assert.ThrowsAny<Exception>(() => Build(services, settings, role));
        Assert.Contains(errorKey, exception.ToString(), StringComparison.Ordinal);
    }

    private static void Build(ServiceProvider services, Dictionary<string, string> settings, KafkaClientRole role)
    {
        // Dummy loopback bootstrap prevents these tests from reaching any live broker.
        settings["bootstrap.servers"] = "127.0.0.1:1";
        var authentication = services.GetRequiredService<KafkaAuthenticationFactory>();
        var session = authentication.CreateSession(new("synthetic", "synthetic-run", role, settings["client.id"]), TestContext.Current.CancellationToken);
        if (role == KafkaClientRole.LoadGenProducer)
        {
            using var client = authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings), session).Build();
            Assert.NotNull(client);
        }
        else
        {
            using var client = authentication.CreateConsumerBuilder<byte[], byte[]>(new ConsumerConfig(settings), session).Build();
            Assert.NotNull(client);
        }
    }

    private static async Task<(Dictionary<string, string> Settings, KafkaClientRole Role)> Resolve(ServiceProvider services, string item, string configuration)
    {
        var source = new EmbeddedSource();
        var loaded = await new ScenarioLoader(source, Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 65536, MaxScalarCharacters = 65536, MaxNestingDepth = 32, MaxParserEvents = 10000 })).LoadAsync(item, TestContext.Current.CancellationToken);
        var resolved = new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        var role = loaded.Definition.SingleAction!.Configurations[configuration].Role;
        return (resolved.Configurations[configuration].Roles[role].Settings.ToDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal), role);
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        foreach (var role in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer })
            services.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "synthetic-namespace";
                options.ClientId = "synthetic-client";
                options.EnrichmentGroupId = "synthetic-group";
            });
        services.AddSingleton<IKafkaTokenProvider>(new SyntheticTokens());
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload";
            options.TokenFilePath = "unused-synthetic-token";
        });
        return services.BuildServiceProvider();
    }

    private sealed class SyntheticTokens : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            new("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
    }

    private sealed class EmbeddedSource : IScenarioSource
    {
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = "Parchwell.Kafka.Tests." + logicalPath.Replace('/', '.');
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException(name);
            return ValueTask.FromResult<TextReader>(new StreamReader(stream));
        }
    }
}
