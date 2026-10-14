using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

internal static class BaselineSettingsCatalog
{
    private static readonly KafkaSettingSource MicrosoftGuidance = new(
        new Uri("https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations"),
        "Updated 2026-01-28; read 2026-10-14");
    private static readonly KafkaSettingSource NativeConfiguration = new(
        new Uri("https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md"), "v2.16.0");
    private static readonly KafkaSettingSource ManagedConfiguration = new(
        new Uri("https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config_gen.cs"),
        "v2.16.0");
    private static readonly KafkaSettingSource ManagedProducer = new(
        new Uri("https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L726-L732"),
        "v2.16.0");
    private static readonly KafkaSettingSource Blueprint = new(
        new Uri("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/blueprint.md"),
        "b8a25359c8c531c20b0950f3981b6c22a867df1e; read 2026-10-14");
    private static readonly KafkaSettingSource ClientIdentityResearch = new(
        new Uri("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b8a25359c8c531c20b0950f3981b6c22a867df1e/docs/research/kubernetes-kafka-clients.md"),
        "b8a25359c8c531c20b0950f3981b6c22a867df1e; K-51");
    private static readonly KafkaSettingSource ApprovedPlan = new(
        new Uri("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/54#issuecomment-6174869137"),
        "Approved 2026-10-14");

    private static readonly ImmutableArray<KafkaSettingDefinition> Common =
    [
        Entry(KafkaSettingKeys.BootstrapServers, null, ["A05", "A06"], [MicrosoftGuidance, ApprovedPlan],
            "Microsoft's required namespace endpoint and Kafka port; namespace is a required caller input.",
            KafkaSettingValueKind.NamespaceEndpoint),
        Entry(KafkaSettingKeys.ClientId, null, ["H02"], [Blueprint, ClientIdentityResearch, ApprovedPlan],
            "Required caller-supplied workload/pod identity for client tracing; no deployment identity is guessed.",
            KafkaSettingValueKind.ClientId),
        Entry(KafkaSettingKeys.SecurityProtocol, "sasl_ssl", ["A05", "F01"], [MicrosoftGuidance],
            "Microsoft's required SASL_SSL connection protocol."),
        Entry(KafkaSettingKeys.SaslMechanism, "OAUTHBEARER", ["F01"], [Blueprint, NativeConfiguration, ManagedConfiguration],
            "ADR-003 selects OAUTHBEARER; use the singular key emitted by Confluent .NET."),
        Entry(KafkaSettingKeys.SaslOauthbearerMethod, "default", ["F01", "F02"], [NativeConfiguration, Blueprint],
            "Pinned native default for the separately implemented ADR-003 custom refresh callback."),
        Entry(KafkaSettingKeys.SaslOauthbearerMetadataAuthenticationType, "none", ["F02"], [NativeConfiguration, Blueprint],
            "Pinned native default; ADR-003 does not select built-in IMDS or metadata authentication."),
        Entry(KafkaSettingKeys.EnableSaslOauthbearerUnsecureJwt, "false", ["F01"], [NativeConfiguration],
            "Pinned native default keeps the built-in unsecured JWT handler disabled."),
        Entry(KafkaSettingKeys.EnableSslCertificateVerification, "true", ["F01"], [NativeConfiguration],
            "Pinned native default enables broker certificate verification."),
        Entry(KafkaSettingKeys.SslEndpointIdentificationAlgorithm, "https", ["F01"], [NativeConfiguration],
            "Pinned native default selects broker hostname verification."),
        Entry(KafkaSettingKeys.SocketKeepaliveEnable, "true", ["A02"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation; this setting alone does not choose OS keepalive timers."),
        Entry(KafkaSettingKeys.MetadataMaxAgeMs, "180000", ["A03"], [MicrosoftGuidance],
            "Microsoft's specifically librdkafka-labelled metadata age recommendation."),
        Entry(KafkaSettingKeys.ConnectionsMaxIdleMs, "0", ["A04"], [NativeConfiguration, Blueprint],
            "ADR-015 selects the pinned native default; Java and generic troubleshooting values do not replace it."),
        Entry(KafkaSettingKeys.BrokerAddressTtl, "1000", ["K04"], [NativeConfiguration],
            "Pinned native broker DNS cache lifetime."),
        Entry(KafkaSettingKeys.SocketMaxFails, "1", ["A01"], [NativeConfiguration],
            "Pinned native default, which its documentation recommends retaining."),
        Entry(KafkaSettingKeys.SaslUsername, null, ["F01"], [NativeConfiguration, Blueprint],
            "ADR-003's custom OAUTHBEARER baseline excludes SAS/PLAIN usernames.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslPassword, null, ["F01"], [NativeConfiguration, Blueprint],
            "ADR-003's custom OAUTHBEARER baseline excludes SAS/PLAIN passwords.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslJaasConfig, null, ["F01"], [MicrosoftGuidance, Blueprint],
            "Microsoft's Java SAS connection-string configuration is outside the ADR-003 .NET baseline.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslOauthbearerClientId, null, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "Native OIDC client credentials are outside the ADR-003 custom refresh callback path.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslOauthbearerClientSecret, null, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "ADR-003 selects workload identity rather than native OIDC client secrets.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslOauthbearerTokenEndpointUrl, null, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "The future custom token provider owns token acquisition; native OIDC endpoints are excluded.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.SaslOauthbearerScope, null, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "This native property is OIDC-specific; the future custom token provider owns its scope.",
            disposition: KafkaSettingDisposition.Excluded)
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Producer =
    [
        Entry(KafkaSettingKeys.SocketTimeoutMs, "60000", ["A01"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Pinned native default and approved A01 C0 client network timeout; separate from broker acknowledgement timeout."),
        Entry(KafkaSettingKeys.StatisticsIntervalMs, "15000", ["H01", "H02", "A01", "B10"], [Blueprint, NativeConfiguration],
            "ADR-007's starting interval; later producer harness code must collect the ADR-013 guard evidence."),
        Entry(KafkaSettingKeys.RequestTimeoutMs, "60000", ["A01"], [MicrosoftGuidance, NativeConfiguration, Blueprint, ApprovedPlan],
            "Microsoft's librdkafka recommendation and A01 C0; native default is 30000, not Microsoft's stated 5000."),
        Entry(KafkaSettingKeys.MessageTimeoutMs, "300000", ["A01", "B10", "C18"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Pinned native local delivery timeout and approved A01 C0, independent of the per-request timeout."),
        Entry(KafkaSettingKeys.MessageSendMaxRetries, "2147483647", ["A01", "B10"], [MicrosoftGuidance, NativeConfiguration, ManagedConfiguration],
            "Microsoft's librdkafka retries default recommendation, using the .NET MessageSendMaxRetries emitted key."),
        Entry(KafkaSettingKeys.CompressionType, "none", ["B03"], [MicrosoftGuidance, NativeConfiguration, ManagedConfiguration, ApprovedPlan],
            "Approved none choice and pinned native default; emit the equivalent compression.type alias used by the .NET typed property."),
        Entry(KafkaSettingKeys.Acks, "-1", ["B06"], [NativeConfiguration, ManagedConfiguration],
            "Pinned native all acknowledgement default, serialized as -1 by Confluent .NET Acks.All."),
        Entry(KafkaSettingKeys.EnableIdempotence, "false", ["B04"], [Blueprint, NativeConfiguration],
            "ADR-015 retains the pinned native default until B04; Microsoft's Java-only value is not selected."),
        Entry(KafkaSettingKeys.LingerMs, "5", ["B07"], [NativeConfiguration],
            "Pinned native default; Microsoft's Java linger value does not govern this client."),
        Entry(KafkaSettingKeys.Partitioner, "consistent_random", ["B08"], [MicrosoftGuidance, NativeConfiguration],
            "Microsoft's specifically librdkafka-labelled default partitioner recommendation."),
        Entry(KafkaSettingKeys.MessageMaxBytes, "1000000", ["B01", "B02"], [NativeConfiguration],
            "Pinned native request-size setting; it is not a guarantee of strict produce-time size enforcement."),
        Entry(KafkaSettingKeys.BatchSize, "1000000", ["B02"], [NativeConfiguration],
            "Pinned native batch limit; Microsoft's Java batch size is not selected."),
        Entry(KafkaSettingKeys.MaxInFlight, "1000000", ["B04", "B10"], [NativeConfiguration, ManagedConfiguration],
            "Pinned native default with idempotence disabled; emit the equivalent max.in.flight alias used by the .NET MaxInFlight property."),
        Entry(KafkaSettingKeys.QueueBufferingMaxMessages, "100000", ["K03"], [NativeConfiguration],
            "Pinned native producer queue message limit, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.QueueBufferingMaxKbytes, "1048576", ["K03"], [NativeConfiguration],
            "Pinned native producer queue kilobyte limit, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.DotnetProducerEnableBackgroundPoll, "true", ["F01", "A01"], [ManagedConfiguration],
            "Pinned managed producer default; callback and polling implementation remains separate work."),
        Entry(KafkaSettingKeys.DotnetProducerEnableDeliveryReports, "true", ["A01", "B10"], [ManagedConfiguration, Blueprint],
            "Pinned managed producer default retains delivery reports needed by the later ADR-013 harness."),
        Entry(KafkaSettingKeys.DotnetProducerDeliveryReportFields, "all", ["A01", "B10"], [ManagedConfiguration, Blueprint],
            "Pinned managed default includes persistence status required by ADR-013."),
        Entry(KafkaSettingKeys.AllowAutoCreateTopics, "true", ["D01"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Approved producer-specific native default under ADR-015; Terraform ownership does not substitute false."),
        Entry(KafkaSettingKeys.TransactionalId, null, ["B05"], [Blueprint, NativeConfiguration],
            "ADR-015 excludes transactions from the baseline regardless of the unsupported/preview documentation conflict.",
            disposition: KafkaSettingDisposition.Excluded),
        Entry(KafkaSettingKeys.DeliveryReportOnlyError, null, ["A01", "B10"], [ManagedProducer, ManagedConfiguration],
            "Confluent .NET rejects this native key even when false; use the managed delivery report settings.",
            disposition: KafkaSettingDisposition.Excluded)
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Consumer =
    [
        Entry(KafkaSettingKeys.SocketTimeoutMs, "60000", ["C04", "C05"], [NativeConfiguration, ApprovedPlan],
            "Pinned native consumer network timeout; producer-only request.timeout.ms is not emitted here."),
        Entry(KafkaSettingKeys.StatisticsIntervalMs, "15000", ["H01", "H02"], [Blueprint, NativeConfiguration],
            "ADR-007's starting interval; setting it does not implement statistics callbacks or bridging."),
        Entry(KafkaSettingKeys.GroupProtocol, "classic", ["C08"], [NativeConfiguration],
            "Pinned native group protocol default; assigned verifier configuration is not a group-coordination test."),
        Entry(KafkaSettingKeys.PartitionAssignmentStrategy, "range,roundrobin", ["C07"], [NativeConfiguration],
            "Pinned native eager assignor defaults; no cooperative migration is introduced."),
        Entry(KafkaSettingKeys.SessionTimeoutMs, "30000", ["C01", "C04"], [MicrosoftGuidance],
            "Microsoft's specifically librdkafka-labelled session timeout recommendation."),
        Entry(KafkaSettingKeys.HeartbeatIntervalMs, "3000", ["C02"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation to retain the heartbeat default."),
        Entry(KafkaSettingKeys.MaxPollIntervalMs, "300000", ["C03"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation to retain the poll interval default."),
        Entry(KafkaSettingKeys.FetchWaitMaxMs, "500", ["C05"], [NativeConfiguration],
            "Pinned native fetch wait default."),
        Entry(KafkaSettingKeys.QueuedMinMessages, "100000", ["K03"], [NativeConfiguration],
            "Pinned native consumer prefetch message target, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.QueuedMaxMessagesKbytes, "65536", ["K03"], [NativeConfiguration],
            "Pinned native consumer queue kilobyte limit, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.FetchMaxBytes, "52428800", ["K03", "C05"], [NativeConfiguration],
            "Pinned native fetch response size setting, explicitly inventoried for K03 and C05."),
        Entry(KafkaSettingKeys.FetchMessageMaxBytes, "1048576", ["K03"], [NativeConfiguration],
            "Pinned native initial per-partition fetch size, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.ReceiveMessageMaxBytes, "100000000", ["K03"], [NativeConfiguration],
            "Pinned native response size setting, explicitly inventoried for K03."),
        Entry(KafkaSettingKeys.AllowAutoCreateTopics, "false", ["D01"], [NativeConfiguration, ApprovedPlan],
            "Pinned consumer-specific native default; the producer default differs."),
        Entry(KafkaSettingKeys.GroupInstanceId, null, ["C06"], [NativeConfiguration],
            "Static membership is unspecified; retain native dynamic membership without an instance ID.",
            disposition: KafkaSettingDisposition.Unspecified)
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Enrichment =
    [
        Entry(KafkaSettingKeys.GroupId, null, ["C12", "C13", "C14"], [Blueprint, NativeConfiguration, ApprovedPlan],
            "Approved required caller input for a stable enrichment workload group; no deployment group is guessed.",
            KafkaSettingValueKind.EnrichmentGroupId),
        Entry(KafkaSettingKeys.EnableAutoCommit, "true", ["C09", "C10", "C18"], [Blueprint, NativeConfiguration],
            "ADR-005 selects auto-commit with store-after-process; true is also the pinned native default."),
        Entry(KafkaSettingKeys.AutoCommitIntervalMs, "5000", ["C09", "C10", "C15"], [NativeConfiguration],
            "Pinned high-level consumer default; the legacy simple-consumer interval does not apply."),
        Entry(KafkaSettingKeys.EnableAutoOffsetStore, "false", ["C10", "C03"], [Blueprint, NativeConfiguration],
            "ADR-005 selects store-after-process as recommended in the native max.poll.interval.ms description."),
        Entry(KafkaSettingKeys.AutoOffsetReset, "latest", ["C15"], [NativeConfiguration, ManagedConfiguration, ApprovedPlan],
            "Emit latest, the native alias for the pinned largest reset default recognized by the .NET AutoOffsetReset enum; behavior is unchanged."),
        Entry(KafkaSettingKeys.IsolationLevel, "read_committed", ["B05"], [NativeConfiguration],
            "Pinned native isolation default; this selects no transactional producer behavior.")
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Verifier =
    [
        Entry(KafkaSettingKeys.GroupId, "parchwell-verifier", ["C13"], [Blueprint],
            "ADR-013's fixed verifier group prevents creating a group per run."),
        Entry(KafkaSettingKeys.EnableAutoCommit, "false", ["C10"], [Blueprint, NativeConfiguration],
            "ADR-013 explicitly disables auto-commit for the verifier's assignment-based oracle."),
        Entry(KafkaSettingKeys.AutoOffsetReset, "error", ["C15"], [Blueprint, NativeConfiguration],
            "ADR-013 requires an out-of-range start offset to fail rather than silently reset."),
        Entry(KafkaSettingKeys.IsolationLevel, "read_committed", ["B05"], [Blueprint, NativeConfiguration],
            "ADR-013 explicitly selects the pinned native isolation default."),
        Entry(KafkaSettingKeys.AutoCommitIntervalMs, null, ["C09", "C10"], [Blueprint, NativeConfiguration],
            "Verifier does not depend on this interval: auto-commit is disabled and it does not commit offsets.",
            disposition: KafkaSettingDisposition.Unspecified),
        Entry(KafkaSettingKeys.EnableAutoOffsetStore, null, ["C10"], [Blueprint, NativeConfiguration],
            "Verifier assigns explicit offsets and does not store or commit offsets; revisit if that contract changes.",
            disposition: KafkaSettingDisposition.Unspecified)
    ];

    internal static ImmutableArray<KafkaSettingDefinition> GetEntries(KafkaClientRole role) => role switch
    {
        KafkaClientRole.LoadGenProducer => Common.AddRange(Producer),
        KafkaClientRole.EnrichmentProducer => Common.AddRange(Producer),
        KafkaClientRole.EnrichmentConsumer => Common.AddRange(Consumer).AddRange(Enrichment),
        KafkaClientRole.VerifierConsumer => Common.AddRange(Consumer).AddRange(Verifier),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Kafka client role.")
    };

    private static KafkaSettingDefinition Entry(string key, string? value, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason,
        KafkaSettingValueKind valueKind = KafkaSettingValueKind.Literal,
        KafkaSettingDisposition disposition = KafkaSettingDisposition.Explicit) =>
        new(key, value, valueKind, disposition, inventoryIds, sources, selectionReason);
}
