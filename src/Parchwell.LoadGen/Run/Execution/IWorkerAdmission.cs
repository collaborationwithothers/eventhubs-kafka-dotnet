using Confluent.Kafka;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Blocks production admission until the configured native authentication path is available.</summary>
public interface IWorkerAdmission
{
    /// <summary>Checks that production authentication is ready before a native producer is created.</summary>
    void EnsureProducerAuthenticationAvailable(ProducerConfig configuration);
}

/// <summary>Honest production gate while the ADR-003 workload-identity refresh handler is unavailable.</summary>
public sealed class UnavailableWorkerAdmission : IWorkerAdmission
{
    /// <inheritdoc />
    public void EnsureProducerAuthenticationAvailable(ProducerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        throw new InvalidOperationException("production_oauthbearer_refresh_handler_unavailable");
    }
}
