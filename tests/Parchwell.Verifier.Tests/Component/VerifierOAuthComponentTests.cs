using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Common.Kafka;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifierOAuthComponentTests
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };
    [Fact]
    public async Task Given_UnassignedOAuthConsumer_When_PumpedThenQueried_Then_InstallsTokenAndReadsRealWatermarks()
    {
        var evidenceRoot = Environment.GetEnvironmentVariable("PARCHWELL_FIXTURE_PROOF_DIR")
            ?? Path.Combine(Path.GetTempPath(), $"verifier-oauth-proof-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidenceRoot);
        var events = new List<string>();
        var nativeLogs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var clock = Stopwatch.StartNew();
        var fixture = new OAuthKafkaFixture();
        Exception? failure = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(300));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, TestContext.Current.CancellationToken);
        try
        {
            await fixture.StartAsync(linked.Token);
            events.Add($"started {clock.Elapsed}");
            var services = new ServiceCollection();
            services.AddSingleton<IKafkaTokenProvider>(new SyntheticTokenProvider());
            services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
            {
                options.NamespaceName = "synthetic-namespace";
                options.ClientId = "synthetic-verifier";
            });
            services.AddParchwellKafkaAuthentication(options =>
            {
                options.TenantId = "synthetic-tenant";
                options.ClientId = "synthetic-workload-client";
                options.TokenFilePath = "synthetic-token-file";
            });
            using var provider = services.BuildServiceProvider();
            var configurations = provider.GetRequiredService<KafkaConfigurationFactory>();
            var authentication = provider.GetRequiredService<KafkaAuthenticationFactory>();
            var configuration = configurations.CreateVerifierConsumer().Configuration;
            configuration.BootstrapServers = fixture.BootstrapServers;
            configuration.SslCaPem = fixture.Certificate;
            configuration.Debug = "security,broker,metadata,protocol";
            var authenticationSession = authentication.CreateSession(new KafkaAuthenticationIdentity("synthetic-scenario",
                Guid.NewGuid().ToString("N"), KafkaClientRole.VerifierConsumer, "synthetic-verifier"), linked.Token);
            var consumer = authentication.CreateConsumerBuilder<byte[], byte[]>(configuration, authenticationSession)
                .SetLogHandler((_, entry) => nativeLogs.Enqueue($"{entry.Level} {entry.Facility} {entry.Message}"))
                .SetErrorHandler((_, error) => nativeLogs.Enqueue($"{error.Code} {error.Reason}"))
                .Build();
            IAdminClient admin;
            try { admin = new DependentAdminClientBuilder(consumer.Handle).Build(); }
            catch { consumer.Dispose(); throw; }
            var nativeSession = new VerifierKafkaSession(consumer, admin, authenticationSession,
                TimeProvider.System, new VerifierHostOptions
                {
                    TokenTimeout = TimeSpan.FromSeconds(300), PollSlice = TimeSpan.FromMilliseconds(100),
                    MetadataTimeout = TimeSpan.FromSeconds(10), WatermarkTimeout = TimeSpan.FromSeconds(10), MaxPartitions = 1
                }, CancellationTokenSource.CreateLinkedTokenSource(linked.Token));
            try
            {
                Assert.Empty(consumer.Assignment);
                nativeSession.InstallToken(linked.Token);
                Assert.Empty(consumer.Assignment);
                Assert.Null(authenticationSession.InitialFailure);
                Assert.NotNull(authenticationSession.InstalledTokenExpiresOn);
                events.Add($"production token pump installed token while unassigned {clock.Elapsed}");
                const string topic = "document-events";
                await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
                    new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10), OperationTimeout = TimeSpan.FromSeconds(10) }).WaitAsync(linked.Token);
                Metadata metadata;
                do
                {
                    linked.Token.ThrowIfCancellationRequested();
                    metadata = admin.GetMetadata(topic, Remaining(clock));
                    events.Add($"setup metadata {clock.Elapsed}: " + JsonSerializer.Serialize(metadata));
                    if (metadata.Topics.Count == 1 && metadata.Topics[0].Error.Code == ErrorCode.NoError
                        && metadata.Topics[0].Partitions.Count == 1 && metadata.Topics[0].Partitions[0].Leader >= 0)
                        break;
                    await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
                } while (true);
                // Repeat the actual proof query only after setup reports a ready topic.
                metadata = admin.GetMetadata(topic, Remaining(clock));
                events.Add($"proof metadata {clock.Elapsed}: " + JsonSerializer.Serialize(metadata));
                Assert.Single(metadata.Topics);
                Assert.Single(metadata.Topics[0].Partitions);
                var watermark = consumer.QueryWatermarkOffsets(new TopicPartition(topic, 0), Remaining(clock));
                Assert.Equal(0, watermark.Low.Value);
                Assert.Equal(0, watermark.High.Value);
                Assert.Empty(consumer.Assignment);
                events.Add($"authenticated metadata/watermarks low={watermark.Low} high={watermark.High}, assignment empty {clock.Elapsed}");
                var productionHighs = nativeSession.ReadHighWatermarks(cancellationToken: linked.Token);
                Assert.Single(productionHighs);
                Assert.Equal(0, productionHighs[0]);
                Assert.Empty(consumer.Assignment);
                events.Add($"production session queried fixed high 0 while unassigned {clock.Elapsed}");
            }
            finally
            {
                using var nativeCleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                nativeSession.Cleanup(nativeCleanupDeadline.Token);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            using var diagnosticDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var diagnostics = await fixture.DiagnosticsAsync(diagnosticDeadline.Token);
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "diagnostics.log"), diagnostics, CancellationToken.None);
            using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var cleanupSucceeded = false;
            try
            {
                await fixture.DisposeAsync().AsTask().WaitAsync(cleanupDeadline.Token);
                cleanupSucceeded = true;
            }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "proof.json"), JsonSerializer.Serialize(new
                {
                    recipe = "verified TLS; unsecured fixture-only exp/sub JWT; real shared OAuth factory; Consume while unassigned; authenticated dependent-admin metadata and QueryWatermarkOffsets",
                    kafkaImage = OAuthKafkaFixture.KafkaImage,
                    containerName = fixture.ContainerName,
                    elapsed = clock.Elapsed,
                    events,
                    nativeLogs = nativeLogs.ToArray(),
                    failure = failure?.ToString(),
                    cleanupSucceeded
                }, EvidenceJsonOptions), CancellationToken.None);
            }
        }
    }

    private static TimeSpan Remaining(Stopwatch clock)
    {
        var remaining = TimeSpan.FromSeconds(300) - clock.Elapsed;
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("OAuth proof setup budget expired.");
        return remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10);
    }

    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expiry = TimeProvider.System.GetUtcNow().AddMinutes(30);
            var header = Encode("{\"alg\":\"none\"}");
            var payload = Encode(JsonSerializer.Serialize(new { sub = "synthetic-workload-client", exp = expiry.ToUnixTimeSeconds() }));
            return new AccessToken($"{header}.{payload}.", expiry);
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
