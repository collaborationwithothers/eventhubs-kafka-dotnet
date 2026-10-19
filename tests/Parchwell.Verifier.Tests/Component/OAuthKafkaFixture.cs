using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Parchwell.Verifier.Tests.Component;

// Independent adaptation of the pinned spike fixture. These settings test a local
// fixture, not Event Hubs. Kafka's PEM configuration and unsecured validator:
// https://github.com/apache/kafka/blob/3.5.2/clients/src/main/java/org/apache/kafka/common/config/SslConfigs.java
// https://github.com/apache/kafka/blob/3.5.2/clients/src/main/java/org/apache/kafka/common/security/oauthbearer/internals/unsecured/OAuthBearerUnsecuredValidatorCallbackHandler.java
internal sealed class OAuthKafkaFixture : IAsyncDisposable
{
    internal const string KafkaImage = "confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf";
    private readonly IContainer broker;
    private readonly string privateKey;
    private readonly string certificate;
    private readonly string name = $"verifier-oauth-{Guid.NewGuid():N}";
    internal string BootstrapServers => $"127.0.0.1:{broker.GetMappedPublicPort(9093)}";
    internal string Certificate => certificate;
    internal string ContainerName => name;

    internal OAuthKafkaFixture()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var cert = request.CreateSelfSigned(TimeProvider.System.GetUtcNow().AddMinutes(-1), TimeProvider.System.GetUtcNow().AddHours(1));
        privateKey = key.ExportPkcs8PrivateKeyPem();
        certificate = cert.ExportCertificatePem();
        broker = new ContainerBuilder(KafkaImage)
            .WithName(name)
            .WithPortBinding(9093, true)
            .WithCreateParameterModifier(parameters =>
            {
                var ports = parameters.HostConfig?.PortBindings
                    ?? throw new InvalidOperationException("Fixture ports were not configured.");
                foreach (var bindings in ports.Values)
                foreach (var binding in bindings)
                    binding.HostIP = "127.0.0.1";
            })
            .WithEntrypoint("/bin/bash", "-c")
            .WithCommand("while [ ! -f /tmp/verifier-start.sh ]; do sleep 0.1; done; exec bash /tmp/verifier-start.sh")
            .WithStartupCallback(async (container, ct) =>
            {
                var config = Configuration($"127.0.0.1:{container.GetMappedPublicPort(9093)}");
                await container.CopyAsync(Encoding.UTF8.GetBytes(config), "/tmp/verifier.properties", ct: ct);
                const string start = "#!/bin/bash\nset -e\nkafka-storage format --ignore-formatted -t 4L6g3nShT-eMCtK--X86sw -c /tmp/verifier.properties\nexec kafka-server-start /tmp/verifier.properties\n";
                await container.CopyAsync(Encoding.UTF8.GetBytes(start), "/tmp/verifier-start.sh", ct: ct);
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Transitioning from RECOVERY to RUNNING.*"))
            .Build();
    }

    internal Task StartAsync(CancellationToken ct) => broker.StartAsync(ct);

    internal async Task<string> DiagnosticsAsync(CancellationToken ct)
    {
        try
        {
            var logs = await broker.GetLogsAsync(ct: ct);
            return $"Container: {name}\n{logs.Stdout}\n{logs.Stderr}";
        }
        catch (Exception exception)
        {
            return $"Container: {name}\nDiagnostic failure: {exception}";
        }
    }

    private string Configuration(string advertised) => $$"""
        process.roles=broker,controller
        node.id=1
        controller.quorum.voters=1@localhost:9094
        listeners=OAUTH://:9093,CONTROLLER://:9094,BROKER://:9095
        advertised.listeners=OAUTH://{{advertised}},BROKER://localhost:9095
        listener.security.protocol.map=OAUTH:SASL_SSL,CONTROLLER:PLAINTEXT,BROKER:PLAINTEXT
        inter.broker.listener.name=BROKER
        controller.listener.names=CONTROLLER
        log.dirs=/tmp/verifier-kafka-data
        offsets.topic.replication.factor=1
        offsets.topic.num.partitions=1
        transaction.state.log.replication.factor=1
        transaction.state.log.min.isr=1
        group.initial.rebalance.delay.ms=0
        auto.create.topics.enable=true
        num.partitions=1
        listener.name.oauth.sasl.enabled.mechanisms=OAUTHBEARER
        listener.name.oauth.oauthbearer.sasl.jaas.config=org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required;
        listener.name.oauth.oauthbearer.sasl.server.callback.handler.class=org.apache.kafka.common.security.oauthbearer.internals.unsecured.OAuthBearerUnsecuredValidatorCallbackHandler
        listener.name.oauth.ssl.keystore.type=PEM
        listener.name.oauth.ssl.keystore.key={{PropertyPem(privateKey)}}
        listener.name.oauth.ssl.keystore.certificate.chain={{PropertyPem(certificate)}}
        listener.name.oauth.ssl.client.auth=none
        """;

    private static string PropertyPem(string pem) => pem.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    public ValueTask DisposeAsync() => broker.DisposeAsync();
}
