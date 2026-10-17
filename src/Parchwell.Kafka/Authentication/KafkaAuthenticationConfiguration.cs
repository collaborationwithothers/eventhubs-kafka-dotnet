using Confluent.Kafka;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Authentication;

/// <summary>Builds the approved Event Hubs OAuth scope and validates native authentication settings.</summary>
internal static class KafkaAuthenticationConfiguration
{
    private static readonly string[] ExcludedKeys =
    [
        KafkaSettingKeys.SaslUsername, KafkaSettingKeys.SaslPassword, KafkaSettingKeys.SaslJaasConfig,
        KafkaSettingKeys.SaslOauthbearerClientId, KafkaSettingKeys.SaslOauthbearerClientSecret,
        KafkaSettingKeys.SaslOauthbearerTokenEndpointUrl, KafkaSettingKeys.SaslOauthbearerScope
    ];

    internal static string GetScope(KafkaBaselineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!KafkaRequiredInput.NamespaceEndpoint.IsValid(options))
            throw new InvalidOperationException("Kafka namespace input is invalid.");
        return $"https://{options.NamespaceName}.servicebus.windows.net/.default";
    }

    // This is the complete authentication baseline check from the approved Enforced checks:
    // https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/74#issuecomment-6230988038
    internal static void Validate(ClientConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        (string Key, string Expected)[] required =
        [
            (KafkaSettingKeys.SecurityProtocol, "sasl_ssl"), (KafkaSettingKeys.SaslMechanism, "OAUTHBEARER"),
            (KafkaSettingKeys.SaslOauthbearerMethod, "default"),
            (KafkaSettingKeys.SaslOauthbearerMetadataAuthenticationType, "none"),
            (KafkaSettingKeys.EnableSaslOauthbearerUnsecureJwt, "false"),
            (KafkaSettingKeys.EnableSslCertificateVerification, "true"),
            (KafkaSettingKeys.SslEndpointIdentificationAlgorithm, "https")
        ];
        foreach ((string key, string expected) in required)
        {
            if (!string.Equals(configuration.Get(key), expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"Kafka authentication setting '{key}' is invalid.");
        }
        foreach (string key in ExcludedKeys)
        {
            if (configuration.Get(key) is not null)
                throw new InvalidOperationException($"Kafka authentication setting '{key}' must be absent.");
        }
    }
}
