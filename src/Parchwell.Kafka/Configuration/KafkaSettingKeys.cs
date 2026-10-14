namespace Parchwell.Kafka.Configuration;

internal static class KafkaSettingKeys
{
    /// <summary>The <c>bootstrap.servers</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L887">A05</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L888">A06</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string BootstrapServers = "bootstrap.servers";

    /// <summary>The <c>client.id</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1029">H02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string ClientId = "client.id";

    /// <summary>The <c>security.protocol</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L887">A05</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SecurityProtocol = "security.protocol";

    /// <summary>The <c>sasl.mechanism</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslMechanism = "sasl.mechanism";

    /// <summary>The <c>sasl.oauthbearer.method</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerMethod = "sasl.oauthbearer.method";

    /// <summary>The <c>sasl.oauthbearer.metadata.authentication.type</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerMetadataAuthenticationType = "sasl.oauthbearer.metadata.authentication.type";

    /// <summary>The <c>enable.sasl.oauthbearer.unsecure.jwt</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string EnableSaslOauthbearerUnsecureJwt = "enable.sasl.oauthbearer.unsecure.jwt";

    /// <summary>The <c>enable.ssl.certificate.verification</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string EnableSslCertificateVerification = "enable.ssl.certificate.verification";

    /// <summary>The <c>ssl.endpoint.identification.algorithm</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SslEndpointIdentificationAlgorithm = "ssl.endpoint.identification.algorithm";

    /// <summary>The <c>socket.keepalive.enable</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1010">A02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SocketKeepaliveEnable = "socket.keepalive.enable";

    /// <summary>The <c>metadata.max.age.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L885">A03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MetadataMaxAgeMs = "metadata.max.age.ms";

    /// <summary>The <c>connections.max.idle.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1012">A04</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string ConnectionsMaxIdleMs = "connections.max.idle.ms";

    /// <summary>The <c>broker.address.ttl</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L998">K04</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string BrokerAddressTtl = "broker.address.ttl";

    /// <summary>The <c>socket.max.fails</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SocketMaxFails = "socket.max.fails";

    /// <summary>The <c>sasl.username</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslUsername = "sasl.username";

    /// <summary>The <c>sasl.password</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslPassword = "sasl.password";

    /// <summary>The <c>sasl.jaas.config</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslJaasConfig = "sasl.jaas.config";

    /// <summary>The <c>sasl.oauthbearer.client.id</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerClientId = "sasl.oauthbearer.client.id";

    /// <summary>The <c>sasl.oauthbearer.client.secret</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerClientSecret = "sasl.oauthbearer.client.secret";

    /// <summary>The <c>sasl.oauthbearer.token.endpoint.url</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerTokenEndpointUrl = "sasl.oauthbearer.token.endpoint.url";

    /// <summary>The <c>sasl.oauthbearer.scope</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1025">F02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SaslOauthbearerScope = "sasl.oauthbearer.scope";

    /// <summary>The <c>socket.timeout.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1016">C04</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L914">C05</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SocketTimeoutMs = "socket.timeout.ms";

    /// <summary>The <c>statistics.interval.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1028">H01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1029">H02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string StatisticsIntervalMs = "statistics.interval.ms";

    /// <summary>The <c>request.timeout.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string RequestTimeoutMs = "request.timeout.ms";

    /// <summary>The <c>message.timeout.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1022">C18</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MessageTimeoutMs = "message.timeout.ms";

    /// <summary>The <c>message.send.max.retries</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MessageSendMaxRetries = "message.send.max.retries";

    /// <summary>The <c>compression.type</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L896">B03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string CompressionType = "compression.type";

    /// <summary>The <c>acks</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L899">B06</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string Acks = "acks";

    /// <summary>The <c>enable.idempotence</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L897">B04</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string EnableIdempotence = "enable.idempotence";

    /// <summary>The <c>linger.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L900">B07</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string LingerMs = "linger.ms";

    /// <summary>The <c>partitioner</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L901">B08</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string Partitioner = "partitioner";

    /// <summary>The <c>message.max.bytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L894">B01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L895">B02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MessageMaxBytes = "message.max.bytes";

    /// <summary>The <c>batch.size</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L895">B02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string BatchSize = "batch.size";

    /// <summary>The <c>max.in.flight</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L897">B04</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MaxInFlight = "max.in.flight";

    /// <summary>The <c>queue.buffering.max.messages</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string QueueBufferingMaxMessages = "queue.buffering.max.messages";

    /// <summary>The <c>queue.buffering.max.kbytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string QueueBufferingMaxKbytes = "queue.buffering.max.kbytes";

    /// <summary>The <c>dotnet.producer.enable.background.poll</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1024">F01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string DotnetProducerEnableBackgroundPoll = "dotnet.producer.enable.background.poll";

    /// <summary>The <c>dotnet.producer.enable.delivery.reports</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string DotnetProducerEnableDeliveryReports = "dotnet.producer.enable.delivery.reports";

    /// <summary>The <c>dotnet.producer.delivery.report.fields</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string DotnetProducerDeliveryReportFields = "dotnet.producer.delivery.report.fields";

    /// <summary>The <c>allow.auto.create.topics</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L934">D01</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string AllowAutoCreateTopics = "allow.auto.create.topics";

    /// <summary>The <c>transactional.id</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1014">B05</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string TransactionalId = "transactional.id";

    /// <summary>The <c>delivery.report.only.error</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1011">A01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L903">B10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string DeliveryReportOnlyError = "delivery.report.only.error";

    /// <summary>The <c>group.protocol</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L917">C08</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string GroupProtocol = "group.protocol";

    /// <summary>The <c>partition.assignment.strategy</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1018">C07</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string PartitionAssignmentStrategy = "partition.assignment.strategy";

    /// <summary>The <c>session.timeout.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L910">C01</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1016">C04</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string SessionTimeoutMs = "session.timeout.ms";

    /// <summary>The <c>heartbeat.interval.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L911">C02</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string HeartbeatIntervalMs = "heartbeat.interval.ms";

    /// <summary>The <c>max.poll.interval.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1015">C03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string MaxPollIntervalMs = "max.poll.interval.ms";

    /// <summary>The <c>fetch.wait.max.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L914">C05</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string FetchWaitMaxMs = "fetch.wait.max.ms";

    /// <summary>The <c>queued.min.messages</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string QueuedMinMessages = "queued.min.messages";

    /// <summary>The <c>queued.max.messages.kbytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string QueuedMaxMessagesKbytes = "queued.max.messages.kbytes";

    /// <summary>The <c>fetch.max.bytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L914">C05</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string FetchMaxBytes = "fetch.max.bytes";

    /// <summary>The <c>fetch.message.max.bytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string FetchMessageMaxBytes = "fetch.message.max.bytes";

    /// <summary>The <c>receive.message.max.bytes</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L997">K03</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string ReceiveMessageMaxBytes = "receive.message.max.bytes";

    /// <summary>The <c>group.instance.id</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1017">C06</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string GroupInstanceId = "group.instance.id";

    /// <summary>The <c>group.id</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L921">C12</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L922">C13</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1021">C14</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string GroupId = "group.id";

    /// <summary>The <c>enable.auto.commit</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1019">C09</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L919">C10</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1022">C18</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string EnableAutoCommit = "enable.auto.commit";

    /// <summary>The <c>auto.commit.interval.ms</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1019">C09</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L919">C10</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L924">C15</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string AutoCommitIntervalMs = "auto.commit.interval.ms";

    /// <summary>The <c>enable.auto.offset.store</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1015">C03</see>, <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L919">C10</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string EnableAutoOffsetStore = "enable.auto.offset.store";

    /// <summary>The <c>auto.offset.reset</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L924">C15</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string AutoOffsetReset = "auto.offset.reset";

    /// <summary>The <c>isolation.level</c> setting key.</summary>
    /// <remarks>Inventory: <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md#L1014">B05</see>.
    /// Scenario results are pending; add published result links when available.</remarks>
    internal const string IsolationLevel = "isolation.level";
}
