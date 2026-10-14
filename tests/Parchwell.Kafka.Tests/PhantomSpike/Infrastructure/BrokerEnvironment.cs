using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

internal sealed class BrokerEnvironment : IAsyncDisposable
{
    internal const string KafkaImage = "confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf";
    internal const string ProxyImage = "ghcr.io/shopify/toxiproxy:2.12.0@sha256:9378ed52a28bc50edc1350f936f518f31fa95f0d15917d6eb40b8e376d1a214e";
    private const int ProxyControlPort = 8474;
    private const int ProxyDataPort = 8666;
    private const int KafkaDirectPort = 9093;
    private readonly INetwork network;
    private readonly IContainer proxy;
    private readonly HttpClient control = new();
    private IContainer? kafka;
    private string? proxyName;
    private bool disposed;

    private BrokerEnvironment()
    {
        network = new NetworkBuilder().Build();
        // The scratch image has no shell. HTTP readiness and explicit metric flags are required.
        // https://github.com/Shopify/toxiproxy/blob/v2.12.0/Dockerfile
        proxy = new ContainerBuilder(ProxyImage)
            .WithNetwork(network)
            .WithPortBinding(ProxyControlPort, true)
            .WithPortBinding(ProxyDataPort, true)
            .WithCommand("-host=0.0.0.0", "-proxy-metrics")
            .WithEnvironment("LOG_LEVEL", "debug")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPath("/version").ForPort(ProxyControlPort)))
            .Build();
    }

    public string ProducerBootstrapServers { get; private set; } = string.Empty;
    public string ObserverBootstrapServers { get; private set; } = string.Empty;

    public static async Task<BrokerEnvironment> StartAsync(CancellationToken cancellationToken)
    {
        var environment = new BrokerEnvironment();
        try
        {
            await environment.proxy.StartAsync(cancellationToken).ConfigureAwait(false);
            environment.ProducerBootstrapServers = Endpoint(environment.proxy, ProxyDataPort);
            environment.control.BaseAddress = new Uri($"http://{Endpoint(environment.proxy, ProxyControlPort)}/");
            environment.kafka = environment.BuildKafka();
            await environment.kafka.StartAsync(cancellationToken).ConfigureAwait(false);
            environment.ObserverBootstrapServers = Endpoint(environment.kafka, KafkaDirectPort);
            return environment;
        }
        catch (Exception startFailure)
        {
            var diagnosticText = await environment.CaptureStartupDiagnosticsAsync().ConfigureAwait(false);
            var failure = new InvalidOperationException("Broker startup failed.\n" + diagnosticText, startFailure);
            try
            {
                await environment.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Broker startup and cleanup failed.", failure, cleanupFailure);
            }

            throw failure;
        }
    }

    private async Task<string> CaptureStartupDiagnosticsAsync()
    {
        // Diagnostic collection has its own bounded budget even when startup was cancelled.
        using var diagnosticsDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = diagnosticsDeadline.Token;
        var proxyLogs = CaptureTextAsync(async () =>
        {
            var logs = await proxy.GetLogsAsync(ct: ct).ConfigureAwait(false);
            return logs.Stdout + "\n" + logs.Stderr;
        });
        var broker = kafka;
        var kafkaLogs = broker is null ? Task.FromResult("Kafka was not created.") : CaptureTextAsync(async () =>
        {
            var logs = await broker.GetLogsAsync(ct: ct).ConfigureAwait(false);
            return logs.Stdout + "\n" + logs.Stderr;
        });
        var proxyState = control.BaseAddress is null ? Task.FromResult("Proxy API endpoint was not mapped.")
            : CaptureTextAsync(() => ReadControlAsync("proxies", ct));
        await Task.WhenAll(proxyLogs, kafkaLogs, proxyState).ConfigureAwait(false);
        return "Kafka logs:\n" + await kafkaLogs.ConfigureAwait(false)
            + "\nProxy logs:\n" + await proxyLogs.ConfigureAwait(false)
            + "\nProxy state:\n" + await proxyState.ConfigureAwait(false);
    }

    private static async Task<string> CaptureTextAsync(Func<Task<string>> capture)
    {
        try
        {
            return await capture().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return "Diagnostic capture failed: " + exception;
        }
    }

    private IContainer BuildKafka()
    {
        // Adapted KRaft setup and startup-script gate from the pinned module; no module package is used.
        // https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers.Kafka/KafkaBuilder.cs
        return new ContainerBuilder(KafkaImage)
            .WithNetwork(network)
            .WithNetworkAliases("phantom-kafka")
            .WithPortBinding(KafkaDirectPort, true)
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
                var listeners = $"PROXY://{ProducerBootstrapServers},DIRECT://{Endpoint(container, KafkaDirectPort)},BROKER://phantom-kafka:9095";
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

        var name = $"phantom-{trialId}";
        using var creation = await control.PostAsJsonAsync("proxies", new
        {
            name,
            listen = $"0.0.0.0:{ProxyDataPort}",
            upstream = "phantom-kafka:9092",
            enabled = true
        }, cancellationToken).ConfigureAwait(false);
        creation.EnsureSuccessStatusCode();
        proxyName = name;
        using var state = JsonDocument.Parse(await ReadControlAsync($"proxies/{Uri.EscapeDataString(name)}", cancellationToken).ConfigureAwait(false));
        var root = state.RootElement;
        if (!IsExpectedWildcardListen(root.GetProperty("listen").GetString())
            || root.GetProperty("upstream").GetString() != "phantom-kafka:9092"
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

    public async Task<string> ApplyFaultAsync(SpikeFault fault, TimingSettings timings, CancellationToken cancellationToken)
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
                stream = "downstream",
                toxicity = 1,
                attributes
            }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        var state = await ReadControlAsync(path, cancellationToken).ConfigureAwait(false);
        ValidateFaultState(state, fault, timings);
        return state;
    }

    private static void ValidateFaultState(string state, SpikeFault fault, TimingSettings timings)
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
            || toxic.GetProperty("stream").GetString() != "downstream"
            || toxic.GetProperty("toxicity").GetDouble() != 1
            || !attributesMatch)
        {
            throw new InvalidOperationException("The activated toxic did not match the requested configuration.");
        }
    }

    public async Task<InfrastructureDiagnostics> CaptureDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var broker = kafka ?? throw new InvalidOperationException("Kafka has not been created.");
        var state = ReadControlAsync(proxyName is null ? "proxies" : $"proxies/{Uri.EscapeDataString(proxyName)}", cancellationToken);
        var metrics = ReadControlAsync("metrics", cancellationToken);
        var kafkaLogs = broker.GetLogsAsync(ct: cancellationToken);
        var proxyLogs = proxy.GetLogsAsync(ct: cancellationToken);
        await Task.WhenAll(state, metrics, kafkaLogs, proxyLogs).ConfigureAwait(false);
        var kafkaOutput = await kafkaLogs.ConfigureAwait(false);
        var proxyOutput = await proxyLogs.ConfigureAwait(false);
        return new InfrastructureDiagnostics(await state.ConfigureAwait(false), await metrics.ConfigureAwait(false),
            kafkaOutput.Stdout + "\n" + kafkaOutput.Stderr, proxyOutput.Stdout + "\n" + proxyOutput.Stderr,
            ContainerIds: new Dictionary<string, string> { ["Kafka"] = broker.Id, ["Toxiproxy"] = proxy.Id });
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

    private static string Endpoint(IContainer container, int port)
    {
        var host = container.Hostname;
        return $"{(host.Contains(':', StringComparison.Ordinal) ? $"[{host}]" : host)}:{container.GetMappedPublicPort(port)}";
    }

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
