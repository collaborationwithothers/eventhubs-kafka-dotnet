using Docker.DotNet;
using Docker.DotNet.Handler.Abstractions;
using DotNet.Testcontainers.Configurations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;


namespace Parchwell.Verifier.Tests.Component;

/// <summary>
/// The spike's local broker environment: one Kafka container and one Toxiproxy container on a Docker
/// network, started with generic Testcontainers. The producer reaches Kafka only through the proxy
/// listener; the admin client and observer use a separate direct listener. Published ports bind to
/// host loopback. See docs/operations/phantom-spike/README.md and ADR-016.
/// </summary>
internal sealed class LocalKafkaFixture : IAsyncDisposable
{
    internal const string KafkaImage = "confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf";
    internal const string ProxyImage = "ghcr.io/shopify/toxiproxy:2.12.0@sha256:9378ed52a28bc50edc1350f936f518f31fa95f0d15917d6eb40b8e376d1a214e";
    private const int ProxyControlPort = 8474;
    private const int ProxyDataPort = 8666;
    private const int KafkaDirectPort = 9093;
    private readonly ExperimentDockerAuthentication dockerAuthentication;
    private readonly string proxyContainerName = $"verifier-proof-proxy-{Guid.NewGuid():N}";
    private readonly string kafkaContainerName = $"verifier-proof-kafka-{Guid.NewGuid():N}";
    private readonly INetwork network;
    private readonly IContainer proxy;
    private readonly HttpClient control = new();
    private IContainer? kafka;
    private string? proxyName;
    private bool disposed;
    private bool proxyCreationAttempted;
    private bool kafkaCreationAttempted;

    internal LocalKafkaFixture()
    {
        // Set the HTTP ceiling before any resource Build: TC caches the first client per endpoint.
        // https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers/Clients/DockerApiClient.cs
        dockerAuthentication = new ExperimentDockerAuthentication(TestcontainersSettings.OS.DockerEndpointAuthConfig);
        network = new NetworkBuilder().WithDockerEndpoint(dockerAuthentication).Build();
        // The scratch image has no shell. HTTP readiness and explicit metric flags are required.
        // https://github.com/Shopify/toxiproxy/blob/v2.12.0/Dockerfile
        proxy = new ContainerBuilder(ProxyImage)
            .WithDockerEndpoint(dockerAuthentication)
            .WithName(proxyContainerName)
            .WithNetwork(network)
            .WithPortBinding(ProxyControlPort, true)
            .WithPortBinding(ProxyDataPort, true)
            .WithCreateParameterModifier(BindPublishedPortsToLoopback)
            .WithCommand("-host=0.0.0.0", "-proxy-metrics")
            .WithEnvironment("LOG_LEVEL", "debug")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPath("/version").ForPort(ProxyControlPort)))
            .Build();
    }

    public string ProducerBootstrapServers { get; private set; } = string.Empty;
    public string ObserverBootstrapServers { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        proxyCreationAttempted = true;
        await proxy.StartAsync(cancellationToken).ConfigureAwait(false);
        ProducerBootstrapServers = Endpoint(proxy, ProxyDataPort);
        control.BaseAddress = new Uri($"http://{Endpoint(proxy, ProxyControlPort)}/");
        _ = await ReadControlAsync("version", cancellationToken).ConfigureAwait(false);
        kafka = BuildKafka();
        kafkaCreationAttempted = true;
        await kafka.StartAsync(cancellationToken).ConfigureAwait(false);
        ObserverBootstrapServers = Endpoint(kafka, KafkaDirectPort);
    }

    private IContainer BuildKafka()
    {
        // Adapted KRaft setup and startup-script gate from the pinned module; no module package is used.
        // https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers.Kafka/KafkaBuilder.cs
        return new ContainerBuilder(KafkaImage)
            .WithDockerEndpoint(dockerAuthentication)
            .WithName(kafkaContainerName)
            .WithNetwork(network)
            .WithNetworkAliases("verifier-proof-kafka")
            .WithPortBinding(KafkaDirectPort, true)
            .WithCreateParameterModifier(BindPublishedPortsToLoopback)
            .WithEnvironment("KAFKA_LISTENERS", "PROXY://:9092,DIRECT://:9093,CONTROLLER://:9094,BROKER://:9095")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PROXY:PLAINTEXT,DIRECT:PLAINTEXT,CONTROLLER:PLAINTEXT,BROKER:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "BROKER")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_BROKER_ID", "1")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9094")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("CLUSTER_ID", "4L6g3nShT-eMCtK--X86sw")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand("while [ ! -f /testcontainers.sh ]; do sleep 0.1; done; /testcontainers.sh")
            .WithStartupCallback((container, ct) =>
            {
                // The callback runs after dynamic bindings exist, before the readiness strategy.
                // https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers/Containers/DockerContainer.cs
                var listeners = $"PROXY://{ProducerBootstrapServers},DIRECT://{Endpoint(container, KafkaDirectPort)},BROKER://verifier-proof-kafka:9095";
                var script = "#!/bin/bash\nexport KAFKA_ADVERTISED_LISTENERS=" + ShellQuote(listeners) + "\nexec /etc/confluent/docker/run\n";
                return container.CopyAsync(Encoding.UTF8.GetBytes(script), "/testcontainers.sh", fileMode: Unix.FileMode755, ct: ct);
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Transitioning from RECOVERY to RUNNING.*"))
            .Build();
    }

    public async Task ResetProxyAsync(string trialId, CancellationToken cancellationToken)
    {
        if (proxyName is not null)
        {
            using var deletion = await control.DeleteAsync($"proxies/{Uri.EscapeDataString(proxyName)}", cancellationToken).ConfigureAwait(false);
            deletion.EnsureSuccessStatusCode();
            proxyName = null;
        }

        var name = $"verifier-proof-{trialId}";
        using var creation = await control.PostAsJsonAsync("proxies", new
        {
            name,
            listen = $"0.0.0.0:{ProxyDataPort}",
            upstream = "verifier-proof-kafka:9092",
            enabled = true
        }, cancellationToken).ConfigureAwait(false);
        creation.EnsureSuccessStatusCode();
        proxyName = name;
        using var state = JsonDocument.Parse(await ReadControlAsync($"proxies/{Uri.EscapeDataString(name)}", cancellationToken).ConfigureAwait(false));
        var root = state.RootElement;
        if (!IsExpectedWildcardListen(root.GetProperty("listen").GetString())
            || root.GetProperty("upstream").GetString() != "verifier-proof-kafka:9092"
            || !root.GetProperty("enabled").GetBoolean()
            || root.GetProperty("toxics").GetArrayLength() != 0)
        {
            throw new InvalidOperationException("Trial proxy state did not match the requested clean route.");
        }
    }

    internal static bool IsExpectedWildcardListen(string? listen)
    {
        // Toxiproxy replaces the request address with net.Listener.Addr().String().
        // A dual-stack wildcard can therefore be returned as [::], not 0.0.0.0.
        // https://github.com/Shopify/toxiproxy/blob/v2.12.0/proxy.go#L118
        return string.Equals(listen, $"0.0.0.0:{ProxyDataPort}", StringComparison.Ordinal)
            || string.Equals(listen, $"[::]:{ProxyDataPort}", StringComparison.Ordinal);
    }

    public async Task<string> ApplyFaultAsync(SpikeFault fault, TimingSettings timings, CancellationToken cancellationToken, string stream = "downstream")
    {
        var name = proxyName ?? throw new InvalidOperationException("A trial proxy must exist before applying a fault.");
        var path = $"proxies/{Uri.EscapeDataString(name)}";
        if (fault != SpikeFault.None)
        {
            var type = fault switch
            {
                SpikeFault.Latency => "latency",
                SpikeFault.Timeout => "timeout",
                SpikeFault.ResetPeer => "reset_peer",
                _ => throw new ArgumentOutOfRangeException(nameof(fault))
            };
            var attributes = fault == SpikeFault.Latency
                ? new Dictionary<string, int> { ["latency"] = timings.LatencyMs, ["jitter"] = 0 }
                : new Dictionary<string, int> { ["timeout"] = 0 };
            using var response = await control.PostAsJsonAsync($"{path}/toxics", new
            {
                name = "target-fault",
                type,
                stream,
                toxicity = 1,
                attributes
            }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        var state = await ReadControlAsync(path, cancellationToken).ConfigureAwait(false);
        ValidateFaultState(state, fault, timings, stream);
        return state;
    }

    private static void ValidateFaultState(string state, SpikeFault fault, TimingSettings timings, string stream)
    {
        using var document = JsonDocument.Parse(state);
        var toxics = document.RootElement.GetProperty("toxics");
        if (fault == SpikeFault.None)
        {
            if (toxics.GetArrayLength() != 0)
            {
                throw new InvalidOperationException("The control proxy has unexpected toxics.");
            }

            return;
        }

        var expectedType = fault switch
        {
            SpikeFault.Latency => "latency",
            SpikeFault.Timeout => "timeout",
            SpikeFault.ResetPeer => "reset_peer",
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        if (toxics.GetArrayLength() != 1)
        {
            throw new InvalidOperationException("The trial proxy must contain exactly the requested toxic.");
        }

        var toxic = toxics[0];
        var attributes = toxic.GetProperty("attributes");
        var attributesMatch = fault == SpikeFault.Latency
            ? attributes.GetProperty("latency").GetInt32() == timings.LatencyMs && attributes.GetProperty("jitter").GetInt32() == 0
            : attributes.GetProperty("timeout").GetInt32() == 0;
        if (toxic.GetProperty("name").GetString() != "target-fault"
            || toxic.GetProperty("type").GetString() != expectedType
            || toxic.GetProperty("stream").GetString() != stream
            || toxic.GetProperty("toxicity").GetDouble() != 1
            || !attributesMatch)
        {
            throw new InvalidOperationException("The activated toxic did not match the requested configuration.");
        }
    }

    public async Task<string> RemoveFaultAsync(CancellationToken cancellationToken)
    {
        var name = proxyName ?? throw new InvalidOperationException("A trial proxy must exist before removing a fault.");
        var path = $"proxies/{Uri.EscapeDataString(name)}";
        using var response = await control.DeleteAsync($"{path}/toxics/target-fault", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var state = await ReadControlAsync(path, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(state);
        if (document.RootElement.GetProperty("toxics").GetArrayLength() != 0)
            throw new InvalidOperationException("The trial toxic was not removed.");
        return state;
    }

    public async Task<InfrastructureDiagnostics> CaptureDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var broker = kafka;
        var state = ReadControlAsync(proxyName is null ? "proxies" : $"proxies/{Uri.EscapeDataString(proxyName)}", cancellationToken);
        var metrics = ReadControlAsync("metrics", cancellationToken);
        var kafkaLogs = broker is null ? Task.FromResult((Stdout: "Kafka not created.", Stderr: "")) : broker.GetLogsAsync(ct: cancellationToken);
        var proxyLogs = proxy.GetLogsAsync(ct: cancellationToken);
        await Task.WhenAll(state, metrics, kafkaLogs, proxyLogs).ConfigureAwait(false);
        var kafkaOutput = await kafkaLogs.ConfigureAwait(false);
        var proxyOutput = await proxyLogs.ConfigureAwait(false);
        return new InfrastructureDiagnostics(await state.ConfigureAwait(false), await metrics.ConfigureAwait(false),
            kafkaOutput.Stdout + "\n" + kafkaOutput.Stderr, proxyOutput.Stdout + "\n" + proxyOutput.Stderr,
            ContainerIds: null);
    }

    private async Task<string> ReadControlAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await control.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        // Planned harness safety budget, not a measured disposal duration. This token is
        // independent of trial/startup cancellation. Testcontainers disposal itself is
        // uncancellable; expiration marks cleanup failed and leaves its reaper enabled.
        using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var failures = new List<Exception>();
        // A cancelled create may continue inside the daemon after the client loses the ID.
        // Dispose of an ID-less Testcontainers object does not prove that daemon work stopped.
        RecordUnknownCreation(kafka, kafkaCreationAttempted, kafkaContainerName, failures);
        RecordUnknownCreation(proxy, proxyCreationAttempted, proxyContainerName, failures);
        if (kafka is not null)
        {
            await DisposeResourceAsync(kafka, failures, cleanupDeadline.Token).ConfigureAwait(false);
        }

        await DisposeResourceAsync(proxy, failures, cleanupDeadline.Token).ConfigureAwait(false);
        await DisposeResourceAsync(network, failures, cleanupDeadline.Token).ConfigureAwait(false);
        control.Dispose();
        if (failures.Count != 0)
        {
            throw new AggregateException("Broker environment cleanup failed.", failures);
        }
    }

    private static void RecordUnknownCreation(IContainer? container, bool creationAttempted, string name, List<Exception> failures)
    {
        if (!creationAttempted || container is null)
        {
            return;
        }

        try
        {
            _ = container.Id;
        }
        catch (InvalidOperationException exception)
        {
            failures.Add(new InvalidOperationException($"Docker creation outcome is unknown for owned container '{name}'; cleanup cannot be asserted. Inspect and remove this exact name if the daemon completes creation later.", exception));
        }
    }

    private static async Task DisposeResourceAsync(IAsyncDisposable resource, List<Exception> failures, CancellationToken cancellationToken)
    {
        Task? disposal = null;
        try
        {
            disposal = resource.DisposeAsync().AsTask();
            await disposal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            if (disposal is not null)
            {
                // A timeout does not stop Docker removal. Observe any later fault without
                // asserting that removal succeeded or extending the caller's safety budget.
                _ = disposal.ContinueWith(static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    // Modifiers run after bindings are assembled and before Docker container creation.
    // https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers/Clients/DockerContainerOperations.cs#L222-L230
    // HostIP serializes as Docker's HostIp field; this preserves random host-port allocation.
    // https://github.com/testcontainers/Docker.DotNet/blob/1e4015a84fa48cbcfe9002ecc4e2cf14177edc2d/src/Docker.DotNet/Models/PortBinding.Generated.cs#L9-L19
    internal static void BindPublishedPortsToLoopback(CreateContainerParameters parameters)
    {
        var publishedPorts = parameters.HostConfig?.PortBindings
            ?? throw new InvalidOperationException("Published fixture ports were not configured.");
        foreach (var bindings in publishedPorts.Values)
        foreach (var binding in bindings)
            binding.HostIP = "127.0.0.1";
    }

    private static string Endpoint(IContainer container, int port) => $"127.0.0.1:{container.GetMappedPublicPort(port)}";

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}



/// <summary>
/// The detected Docker endpoint and authentication, unchanged except for the HTTP request timeout,
/// a planned safety bound (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal sealed class ExperimentDockerAuthentication(IDockerEndpointAuthenticationConfiguration original)
    : IDockerEndpointAuthenticationConfiguration
{
    public System.Version Version => original.Version;
    public Uri Endpoint => original.Endpoint;
    public IAuthProvider AuthProvider => original.AuthProvider;

    public DockerClientBuilder GetDockerClientBuilder(Guid sessionId = default)
    {
        // Supported public timeout hook; retain detected transport/authentication and TC headers.
        // The experiment cancellation token still bounds startup to its approved overall deadline.
        // https://github.com/testcontainers/Docker.DotNet/blob/1e4015a84fa48cbcfe9002ecc4e2cf14177edc2d/src/Docker.DotNet/DockerClientBuilder.cs#L180
        return original.GetDockerClientBuilder(sessionId).WithTimeout(TimeSpan.FromMinutes(30));
    }
}
