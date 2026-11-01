using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

/// <summary>The baseline: every setting entry for every role, with its tags, sources and reason.</summary>
/// <remarks>
/// <para>
/// This is the single place where baseline values are chosen. The selection rule is ADR-015:
/// take the value Microsoft labels for librdkafka; ignore labels in Microsoft's Java table; if no
/// page labels a value for librdkafka, set the librdkafka default explicitly. "Pinned native
/// default" in a reason means the librdkafka default at the pinned version, v2.16.0.
/// "Managed" means Confluent.Kafka, the .NET library, at the same version.
/// </para>
/// <para>
/// Entries are grouped by the roles that share them: all roles, producers, consumers, the
/// enrichment consumer and the verifier. The verifier's entries are harness settings that guard
/// the oracle (ADR-013); they are not part of what the inventory tests.
/// The expected values are also transcribed, independently, in the tests' approved contract
/// (issue #54 plan), so a change here must match an approved change there.
/// </para>
/// </remarks>
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
        KafkaSettingDefinition.FromInput(KafkaSettingKeys.BootstrapServers, KafkaRequiredInput.NamespaceEndpoint, ["A05", "A06"], [MicrosoftGuidance, ApprovedPlan],
            "Microsoft's required namespace endpoint and Kafka port; namespace is a required caller input."),
        KafkaSettingDefinition.FromInput(KafkaSettingKeys.ClientId, KafkaRequiredInput.ClientId, ["H02"], [Blueprint, ClientIdentityResearch, ApprovedPlan],
            "Required caller-supplied workload/pod identity for client tracing; no deployment identity is guessed."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SecurityProtocol, "sasl_ssl", ["A05", "F01"], [MicrosoftGuidance],
            "Microsoft's required SASL_SSL connection protocol."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SaslMechanism, "OAUTHBEARER", ["F01"], [Blueprint, NativeConfiguration, ManagedConfiguration],
            "ADR-003 selects OAUTHBEARER; use the singular key emitted by Confluent .NET."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SaslOauthbearerMethod, "default", ["F01", "F02"], [NativeConfiguration, Blueprint],
            "Pinned native default for the separately implemented ADR-003 custom refresh callback."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SaslOauthbearerMetadataAuthenticationType, "none", ["F02"], [NativeConfiguration, Blueprint],
            "Pinned native default; ADR-003 does not select built-in IMDS or metadata authentication."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableSaslOauthbearerUnsecureJwt, "false", ["F01"], [NativeConfiguration],
            "Pinned native default keeps the built-in unsecured JWT handler disabled."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableSslCertificateVerification, "true", ["F01"], [NativeConfiguration],
            "Pinned native default enables broker certificate verification."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SslEndpointIdentificationAlgorithm, "https", ["F01"], [NativeConfiguration],
            "Pinned native default selects broker hostname verification."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SocketKeepaliveEnable, "true", ["A02"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation; this setting alone does not choose OS keepalive timers."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MetadataMaxAgeMs, "180000", ["A03"], [MicrosoftGuidance],
            "Microsoft's specifically librdkafka-labelled metadata age recommendation."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.TopicMetadataRefreshIntervalMs, "300000", ["A03"], [NativeConfiguration],
            "ADR-015 selects the pinned native default; it sets the periodic metadata refresh interval."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.ConnectionsMaxIdleMs, "0", ["A04"], [NativeConfiguration, Blueprint],
            "ADR-015 selects the pinned native default; Java and generic troubleshooting values do not replace it."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.BrokerAddressTtl, "1000", ["K04"], [NativeConfiguration],
            "Pinned native broker DNS cache lifetime."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SocketMaxFails, "1", ["A01"], [NativeConfiguration],
            "Pinned native default, which its documentation recommends retaining."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslUsername, KafkaSettingDisposition.Excluded, ["F01"], [NativeConfiguration, Blueprint],
            "ADR-003's custom OAUTHBEARER baseline excludes SAS/PLAIN usernames."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslPassword, KafkaSettingDisposition.Excluded, ["F01"], [NativeConfiguration, Blueprint],
            "ADR-003's custom OAUTHBEARER baseline excludes SAS/PLAIN passwords."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslJaasConfig, KafkaSettingDisposition.Excluded, ["F01"], [MicrosoftGuidance, Blueprint],
            "Microsoft's Java SAS connection-string configuration is outside the ADR-003 .NET baseline."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslOauthbearerClientId, KafkaSettingDisposition.Excluded, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "Native OIDC client credentials are outside the ADR-003 custom refresh callback path."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslOauthbearerClientSecret, KafkaSettingDisposition.Excluded, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "ADR-003 selects workload identity rather than native OIDC client secrets."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslOauthbearerTokenEndpointUrl, KafkaSettingDisposition.Excluded, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "The future custom token provider owns token acquisition; native OIDC endpoints are excluded."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.SaslOauthbearerScope, KafkaSettingDisposition.Excluded, ["F01", "F02"], [NativeConfiguration, Blueprint],
            "This native property is OIDC-specific; the future custom token provider owns its scope.")
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Producer =
    [
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SocketTimeoutMs, "60000", ["A01"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Pinned native default and approved A01 C0 client network timeout; separate from broker acknowledgement timeout."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.StatisticsIntervalMs, "15000", ["H01", "H02", "A01", "B10"], [Blueprint, NativeConfiguration],
            "ADR-007's starting interval; later producer harness code must collect the ADR-013 guard evidence."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.RequestTimeoutMs, "60000", ["A01"], [MicrosoftGuidance, NativeConfiguration, Blueprint, ApprovedPlan],
            "Microsoft's librdkafka recommendation and A01 C0; native default is 30000, not Microsoft's stated 5000."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MessageTimeoutMs, "300000", ["A01", "B10", "C18"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Pinned native local delivery timeout and approved A01 C0, independent of the per-request timeout."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MessageSendMaxRetries, "2147483647", ["A01", "B10"], [MicrosoftGuidance, NativeConfiguration, ManagedConfiguration],
            "Microsoft's librdkafka retries default recommendation, using the .NET MessageSendMaxRetries emitted key."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.CompressionType, "none", ["B03"], [MicrosoftGuidance, NativeConfiguration, ManagedConfiguration, ApprovedPlan],
            "Approved none choice and pinned native default; emit the equivalent compression.type alias used by the .NET typed property."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.Acks, "-1", ["B06"], [NativeConfiguration, ManagedConfiguration],
            "Pinned native all acknowledgement default, serialized as -1 by Confluent .NET Acks.All."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableIdempotence, "false", ["B04"], [Blueprint, NativeConfiguration],
            "ADR-015 retains the pinned native default until B04; Microsoft's Java-only value is not selected."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.LingerMs, "5", ["B07"], [NativeConfiguration],
            "Pinned native default; Microsoft's Java linger value does not govern this client."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.Partitioner, "consistent_random", ["B08"], [MicrosoftGuidance, NativeConfiguration],
            "Microsoft's specifically librdkafka-labelled default partitioner recommendation."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MessageMaxBytes, "1000000", ["B01", "B02"], [NativeConfiguration],
            "Pinned native request-size setting; it is not a guarantee of strict produce-time size enforcement."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.BatchSize, "1000000", ["B02"], [NativeConfiguration],
            "Pinned native batch limit; Microsoft's Java batch size is not selected."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MaxInFlight, "1000000", ["B04", "B10"], [NativeConfiguration, ManagedConfiguration],
            "Pinned native default with idempotence disabled; emit the equivalent max.in.flight alias used by the .NET MaxInFlight property."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.QueueBufferingMaxMessages, "100000", ["K03"], [NativeConfiguration],
            "Pinned native producer queue message limit, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.QueueBufferingMaxKbytes, "1048576", ["K03"], [NativeConfiguration],
            "Pinned native producer queue kilobyte limit, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.DotnetProducerEnableBackgroundPoll, "true", ["F01", "A01"], [ManagedConfiguration],
            "Pinned managed producer default; callback and polling implementation remains separate work."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.DotnetProducerEnableDeliveryReports, "true", ["A01", "B10"], [ManagedConfiguration, Blueprint],
            "Pinned managed producer default retains delivery reports needed by the later ADR-013 harness."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.DotnetProducerDeliveryReportFields, "all", ["A01", "B10"], [ManagedConfiguration, Blueprint],
            "Pinned managed default includes persistence status required by ADR-013."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.AllowAutoCreateTopics, "true", ["D01"], [NativeConfiguration, Blueprint, ApprovedPlan],
            "Approved producer-specific native default under ADR-015; Terraform ownership does not substitute false."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.TransactionalId, KafkaSettingDisposition.Excluded, ["B05"], [Blueprint, NativeConfiguration],
            "ADR-015 excludes transactions from the baseline regardless of the unsupported/preview documentation conflict."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.DeliveryReportOnlyError, KafkaSettingDisposition.Excluded, ["A01", "B10"], [ManagedProducer, ManagedConfiguration],
            "Confluent .NET rejects this native key even when false; use the managed delivery report settings.")
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Consumer =
    [
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SocketTimeoutMs, "60000", ["C04", "C05"], [NativeConfiguration, ApprovedPlan],
            "Pinned native consumer network timeout; producer-only request.timeout.ms is not emitted here."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.StatisticsIntervalMs, "15000", ["H01", "H02"], [Blueprint, NativeConfiguration],
            "ADR-007's starting interval; setting it does not implement statistics callbacks or bridging."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.GroupProtocol, "classic", ["C08"], [NativeConfiguration],
            "Pinned native group protocol default; assigned verifier configuration is not a group-coordination test."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.PartitionAssignmentStrategy, "range,roundrobin", ["C07"], [NativeConfiguration],
            "Pinned native eager assignor defaults; no cooperative migration is introduced."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.SessionTimeoutMs, "30000", ["C01", "C04"], [MicrosoftGuidance],
            "Microsoft's specifically librdkafka-labelled session timeout recommendation."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.HeartbeatIntervalMs, "3000", ["C02"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation to retain the heartbeat default."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.MaxPollIntervalMs, "300000", ["C03"], [MicrosoftGuidance],
            "Microsoft's librdkafka recommendation to retain the poll interval default."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.FetchWaitMaxMs, "500", ["C05"], [NativeConfiguration],
            "Pinned native fetch wait default."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.QueuedMinMessages, "100000", ["K03"], [NativeConfiguration],
            "Pinned native consumer prefetch message target, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.QueuedMaxMessagesKbytes, "65536", ["K03"], [NativeConfiguration],
            "Pinned native consumer queue kilobyte limit, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.FetchMaxBytes, "52428800", ["K03", "C05"], [NativeConfiguration],
            "Pinned native fetch response size setting, explicitly inventoried for K03 and C05."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.FetchMessageMaxBytes, "1048576", ["K03"], [NativeConfiguration],
            "Pinned native initial per-partition fetch size, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.ReceiveMessageMaxBytes, "100000000", ["K03"], [NativeConfiguration],
            "Pinned native response size setting, explicitly inventoried for K03."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.AllowAutoCreateTopics, "false", ["D01"], [NativeConfiguration, ApprovedPlan],
            "Pinned consumer-specific native default; the producer default differs."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.GroupInstanceId, KafkaSettingDisposition.Unspecified, ["C06"], [NativeConfiguration],
            "Static membership is unspecified; retain native dynamic membership without an instance ID."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.Debug, KafkaSettingDisposition.Unspecified, ["C08", "F04"], [NativeConfiguration],
            "No debug contexts are selected; the pinned native default enables none.")
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Enrichment =
    [
        KafkaSettingDefinition.FromInput(KafkaSettingKeys.GroupId, KafkaRequiredInput.EnrichmentGroupId, ["C12", "C13", "C14"], [Blueprint, NativeConfiguration, ApprovedPlan],
            "Approved required caller input for a stable enrichment workload group; no deployment group is guessed."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableAutoCommit, "true", ["C09", "C10", "C18"], [Blueprint, NativeConfiguration],
            "ADR-005 selects auto-commit with store-after-process; true is also the pinned native default."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.AutoCommitIntervalMs, "5000", ["C09", "C10", "C15"], [NativeConfiguration],
            "Pinned high-level consumer default; the legacy simple-consumer interval does not apply."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableAutoOffsetStore, "false", ["C10", "C03"], [Blueprint, NativeConfiguration],
            "ADR-005 selects store-after-process as recommended in the native max.poll.interval.ms description."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.AutoOffsetReset, "latest", ["C15"], [NativeConfiguration, ManagedConfiguration, ApprovedPlan],
            "Emit latest, the native alias for the pinned largest reset default recognized by the .NET AutoOffsetReset enum; behavior is unchanged."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.IsolationLevel, "read_committed", ["B05"], [NativeConfiguration],
            "Pinned native isolation default; this selects no transactional producer behavior.")
    ];

    private static readonly ImmutableArray<KafkaSettingDefinition> Verifier =
    [
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.GroupId, "parchwell-verifier", ["C13"], [Blueprint],
            "ADR-013's fixed verifier group prevents creating a group per run."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.EnableAutoCommit, "false", ["C10"], [Blueprint, NativeConfiguration],
            "ADR-013 explicitly disables auto-commit for the verifier's assignment-based oracle."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.AutoOffsetReset, "error", ["C15"], [Blueprint, NativeConfiguration],
            "ADR-013 requires an out-of-range start offset to fail rather than silently reset."),
        KafkaSettingDefinition.Explicit(KafkaSettingKeys.IsolationLevel, "read_committed", ["B05"], [Blueprint, NativeConfiguration],
            "ADR-013 explicitly selects the pinned native isolation default."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.AutoCommitIntervalMs, KafkaSettingDisposition.Unspecified, ["C09", "C10"], [Blueprint, NativeConfiguration],
            "Verifier does not depend on this interval: auto-commit is disabled and it does not commit offsets."),
        KafkaSettingDefinition.Omitted(KafkaSettingKeys.EnableAutoOffsetStore, KafkaSettingDisposition.Unspecified, ["C10"], [Blueprint, NativeConfiguration],
            "Verifier assigns explicit offsets and does not store or commit offsets; revisit if that contract changes.")
    ];

    internal static ImmutableArray<KafkaSettingDefinition> GetEntries(KafkaClientRole role) => role switch
    {
        KafkaClientRole.LoadGenProducer => Common.AddRange(Producer),
        KafkaClientRole.EnrichmentProducer => Common.AddRange(Producer),
        KafkaClientRole.EnrichmentConsumer => Common.AddRange(Consumer).AddRange(Enrichment),
        KafkaClientRole.VerifierConsumer => Common.AddRange(Consumer).AddRange(Verifier),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Kafka client role.")
    };
}
