using Confluent.Kafka;
using Parchwell.Kafka.Authentication;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Checks production authentication wiring before output and native client creation.</summary>
public interface IWorkerAdmission
{
    /// <summary>Validates the approved producer authentication configuration without acquiring a token.</summary>
    void EnsureProducerAuthenticationAvailable(ProducerConfig configuration);
}

/// <summary>Validates producer authentication configuration through the shared Kafka factory.</summary>
public sealed class OAuthBearerWorkerAdmission(KafkaAuthenticationFactory authenticationFactory) : IWorkerAdmission
{
    private readonly KafkaAuthenticationFactory _authenticationFactory = authenticationFactory
        ?? throw new ArgumentNullException(nameof(authenticationFactory));

    /// <summary>Checks the existing authentication baseline without acquiring credentials or constructing a client.</summary>
    /// <param name="configuration">The final resolved load generator producer configuration.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="InvalidOperationException">The authentication settings do not satisfy the approved baseline.</exception>
    public void EnsureProducerAuthenticationAvailable(ProducerConfig configuration)
    {
        _authenticationFactory.ValidateProducerConfiguration(configuration);
    }
}
