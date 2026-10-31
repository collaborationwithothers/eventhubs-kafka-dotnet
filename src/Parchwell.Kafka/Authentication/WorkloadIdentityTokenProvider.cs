using Azure.Core;

namespace Parchwell.Kafka.Authentication;

/// <summary>Acquires Kafka access tokens with the Azure workload identity credential.</summary>
internal sealed class WorkloadIdentityTokenProvider(TokenCredential credential) : IKafkaTokenProvider, IObservedKafkaTokenProvider
{
    private readonly TokenCredential credential = credential ?? throw new ArgumentNullException(nameof(credential));

    /// <inheritdoc />
    public AccessToken GetToken(string scope, CancellationToken cancellationToken)
    {
        return Acquire(scope, null, cancellationToken);
    }

#pragma warning disable CA1068 // Preserve the explicitly specified observed-provider signature.
    public AccessToken GetToken(string scope, CancellationToken cancellationToken, Action<string> observe)
    {
        ArgumentNullException.ThrowIfNull(observe);
        return Acquire(scope, observe, cancellationToken);
    }
#pragma warning restore CA1068

    private AccessToken Acquire(string scope, Action<string>? observe, CancellationToken cancellationToken)
    {
        TryObserve(observe, "sdk_get_token_invoked");
        AccessToken token = credential.GetToken(new TokenRequestContext([scope]), cancellationToken);
        TryObserve(observe, "sdk_get_token_returned");
        return token;
    }

    private static void TryObserve(Action<string>? observe, string stage)
    {
        try { observe?.Invoke(stage); }
        catch (Exception) { }
    }
}
