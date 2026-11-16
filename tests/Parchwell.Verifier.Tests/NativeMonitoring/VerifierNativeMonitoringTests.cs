#pragma warning disable CA1707
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.NativeMonitoring;

public sealed class VerifierNativeMonitoringTests
{
    [Fact]
    public void Given_matching_verifier_destination_and_phase_When_options_resolve_Then_identity_is_accepted()
    {
        using var host = Program.BuildHost(VerifierHostTestOptions.ArgumentsFor("verify", VerifierHostTestOptions.TempPath()));
        KafkaNativeMonitoringOptions options = host.Services.GetRequiredService<IOptions<KafkaNativeMonitoringOptions>>().Value;
        Assert.Equal("https://storage.example/", options.BlobEndpoint);
        Assert.Equal("C0", options.Phase);
    }

    [Theory]
    [InlineData("BlobEndpoint", "https://other.example/")]
    [InlineData("Phase", "C1")]
    [InlineData("ConfigurationId", "C1")]
    [InlineData("JobName", "other-job")]
    [InlineData("PodUid", "other-pod")]
    [InlineData("SourceCommit", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void Given_mismatched_verifier_monitoring_identity_When_options_resolve_Then_validation_fails(string field, string value)
    {
        string[] args = VerifierHostTestOptions.ArgumentsFor("verify", VerifierHostTestOptions.TempPath());
        using var host = Program.BuildHost([.. args, "--NativeMonitoring:" + field + "=" + value]);
        Assert.Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<KafkaNativeMonitoringOptions>>().Value);
    }

    [Fact]
    public async Task Given_callback_during_consumer_disposal_When_session_cleans_up_Then_original_raw_is_drained_after_disposal()
    {
        using var fixture = new KafkaSessionTestSupport();
        var store = new NativeMonitoringTestSupport.Store();
        using ServiceProvider provider = Services(store);
        KafkaNativeMonitoringCoordinator coordinator = Coordinator(provider);
        KafkaNativeMonitoringLifetime lifetime = coordinator.CreateLifetime(new KafkaStatisticsIdentity("A01", "run", KafkaClientRole.VerifierConsumer, "client"));
        lifetime.MarkBuilt();
        const string raw = "{\"synthetic\": \"unchanged\"}";
        bool disposed = false;
        fixture.ConsumerDispose = () =>
        {
            VerifierKafkaSessionFactory.StatisticsHandler(lifetime)(fixture.Consumer, raw);
            disposed = true;
        };
        IAdminClient admin = NativeProxy.Create<IAdminClient>((method, _) => method.Name == "Dispose" ? null : throw new InvalidOperationException(method.Name));
        var session = new VerifierKafkaSession(fixture.Consumer, admin, fixture.Authentication, fixture.Clock, fixture.Options, new CancellationTokenSource(), lifetime);
        session.Cleanup();
        Assert.True(disposed);
        fixture.ConsumerDispose = null;
        Assert.Empty(store.Objects);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using JsonDocument row = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value);
        Assert.Equal(raw, row.RootElement.GetProperty("raw").GetString());
        using JsonDocument index = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith("index.json", StringComparison.Ordinal)).Value);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.Equal("VerifierConsumer", client.GetProperty("role").GetString());
        Assert.Equal("client", client.GetProperty("client_id").GetString());
        Assert.Equal("built", client.GetProperty("build_status").GetString());
    }

    [Fact]
    public async Task Given_offset_reader_build_failure_When_callback_arrives_Then_failure_and_original_raw_are_recorded()
    {
        var store = new NativeMonitoringTestSupport.Store();
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaNativeMonitoringStore>(store);
        services.AddSingleton<IKafkaTokenProvider>(new TokenProvider());
        services.AddParchwellNativeMonitoring();
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options => { options.NamespaceName = "synthetic-namespace"; options.ClientId = "reader"; });
        services.AddParchwellKafkaAuthentication(options => { options.TenantId = "tenant"; options.ClientId = "identity"; options.TokenFilePath = "token"; });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaNativeMonitoringCoordinator coordinator = Coordinator(provider);
        var options = new VerifierHostOptions { ScenarioId = "A01", RunId = "run", Phase = "C0", Repeat = 1,
            VerifierClientId = "reader", TokenTimeout = TimeSpan.FromSeconds(1) };
        var settings = new Dictionary<string, string> { ["isolation.level"] = "read_committed" };
        const string raw = "{\"from\":\"failed-build\"}";
        var reader = new ConsumerCommittedOffsetReader(provider.GetRequiredService<KafkaConfigurationFactory>(),
            provider.GetRequiredService<KafkaAuthenticationFactory>(), Options.Create(options), new ConsumerVerificationPlan(null, 1000, settings, settings),
            TimeProvider.System, (_, _, lifetime) =>
            {
                VerifierKafkaSessionFactory.StatisticsHandler(lifetime!)(null!, raw);
                throw new KafkaException(new Error(ErrorCode.Local_InvalidArg));
            }, coordinator);
        ConsumerOffsetRead result = await reader.ReadAsync([0], DateTimeOffset.UtcNow.AddSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("kafka_exception", result.Outcome);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using JsonDocument index = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith("index.json", StringComparison.Ordinal)).Value);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.Equal("reader", client.GetProperty("client_id").GetString());
        Assert.Equal("failed", client.GetProperty("build_status").GetString());
        Assert.False(client.GetProperty("capture_complete").GetBoolean());
        using JsonDocument row = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value);
        Assert.Equal(raw, row.RootElement.GetProperty("raw").GetString());
    }

    private static ServiceProvider Services(NativeMonitoringTestSupport.Store store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaNativeMonitoringStore>(store);
        services.AddParchwellNativeMonitoring();
        return services.BuildServiceProvider();
    }

    private static KafkaNativeMonitoringCoordinator Coordinator(ServiceProvider provider) =>
        provider.GetRequiredService<KafkaNativeMonitoringFactory>().Create(new KafkaNativeMonitoringIdentity("run", "C0", "A01", "C0", "job", "pod", "commit"));

    private sealed class TokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected token request.");
    }
}
