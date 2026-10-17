using Azure.Core;

namespace Parchwell.Kafka.Authentication;

/// <summary>Acquires Kafka access tokens with the Azure workload identity credential.</summary>
internal sealed class WorkloadIdentityTokenProvider(TokenCredential credential) : IKafkaTokenProvider
{
    private readonly TokenCredential credential = credential ?? throw new ArgumentNullException(nameof(credential));

    /// <inheritdoc />
    public AccessToken GetToken(string scope, CancellationToken cancellationToken)
    {
        return credential.GetToken(new TokenRequestContext([scope]), cancellationToken);
    }
}
