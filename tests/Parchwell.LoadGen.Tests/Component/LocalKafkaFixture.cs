using System.Text;
using Docker.DotNet;
using Docker.DotNet.Handler.Abstractions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Parchwell.LoadGen.Tests.Component;

/// <summary>Minimal pinned Kafka fixture copied from the ADR-016 phantom-spike broker setup.</summary>
internal sealed class LocalKafkaFixture : IAsyncDisposable
{
    internal const string Image = "confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf";
    private const int KafkaPort = 9092;
    private readonly IContainer container;
    private bool started;

    private LocalKafkaFixture()
    {
        var docker = new BoundedDockerAuthentication(TestcontainersSettings.OS.DockerEndpointAuthConfig);
        container = new ContainerBuilder(Image)
            .WithDockerEndpoint(docker)
            .WithNetworkAliases("loadgen-kafka")
            .WithPortBinding(KafkaPort, true)
            .WithCreateParameterModifier(parameters =>
            {
                var bindings = parameters.HostConfig?.PortBindings
                    ?? throw new InvalidOperationException("Published fixture port was not configured.");
                foreach (var binding in bindings.Values.SelectMany(value => value))
                    binding.HostIP = "127.0.0.1";
            })
            .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://:9092,CONTROLLER://:9093,BROKER://:9094")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,BROKER:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "BROKER")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_BROKER_ID", "1")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9093")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("CLUSTER_ID", "4L6g3nShT-eMCtK--X86sw")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand("while [ ! -f /testcontainers.sh ]; do sleep 0.1; done; /testcontainers.sh")
            .WithStartupCallback((instance, cancellationToken) =>
            {
                var external = $"127.0.0.1:{instance.GetMappedPublicPort(KafkaPort)}";
                var listeners = $"PLAINTEXT://{external},BROKER://loadgen-kafka:9094";
                var script = "#!/bin/bash\nexport KAFKA_ADVERTISED_LISTENERS='" + listeners + "'\nexec /etc/confluent/docker/run\n";
                return instance.CopyAsync(Encoding.UTF8.GetBytes(script), "/testcontainers.sh", fileMode: Unix.FileMode755, ct: cancellationToken);
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Transitioning from RECOVERY to RUNNING.*"))
            .Build();
    }

    internal string BootstrapServers => $"127.0.0.1:{container.GetMappedPublicPort(KafkaPort)}";

    internal static async Task<LocalKafkaFixture> StartAsync(CancellationToken cancellationToken)
    {
        var fixture = new LocalKafkaFixture();
        try
        {
            await fixture.container.StartAsync(cancellationToken).ConfigureAwait(false);
            fixture.started = true;
            return fixture;
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await fixture.container.DisposeAsync().AsTask().WaitAsync(cleanup.Token).ConfigureAwait(false); }
            catch (Exception) { /* Preserve the startup failure; cleanup has its own bound. */ }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!started) return;
        try
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await container.StopAsync(stop.Token).WaitAsync(stop.Token).ConfigureAwait(false);
        }
        finally
        {
            using var dispose = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await container.DisposeAsync().AsTask().WaitAsync(dispose.Token).ConfigureAwait(false);
            started = false;
        }
    }

    /// <summary>Preserves the detected endpoint and credentials while bounding Docker HTTP requests.</summary>
    private sealed class BoundedDockerAuthentication(IDockerEndpointAuthenticationConfiguration original)
        : IDockerEndpointAuthenticationConfiguration
    {
        public Version Version => original.Version;
        public Uri Endpoint => original.Endpoint;
        public IAuthProvider AuthProvider => original.AuthProvider;

        public DockerClientBuilder GetDockerClientBuilder(Guid sessionId = default) =>
            original.GetDockerClientBuilder(sessionId).WithTimeout(TimeSpan.FromMinutes(2));
    }
}
