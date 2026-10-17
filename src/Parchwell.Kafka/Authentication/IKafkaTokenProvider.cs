using Azure.Core;

namespace Parchwell.Kafka.Authentication;

/// <summary>Acquires an access token for a requested resource scope.</summary>
public interface IKafkaTokenProvider
{
    /// <summary>Gets an access token synchronously for use by the native Kafka refresh callback.</summary>
    /// <param name="scope">The resource scope requested by Kafka.</param>
    /// <param name="cancellationToken">The owning run's cancellation token.</param>
    /// <returns>The token and its actual expiry.</returns>
    AccessToken GetToken(string scope, CancellationToken cancellationToken);
}
