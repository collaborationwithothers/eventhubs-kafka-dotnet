using Azure.Core;

namespace Parchwell.Kafka.Authentication;

/// <summary>Exposes SDK invocation boundaries for the explicitly enabled refresh-path diagnostic.</summary>
internal interface IObservedKafkaTokenProvider
{
#pragma warning disable CA1068 // The specified diagnostic seam keeps the observer after the existing token-provider arguments.
    AccessToken GetToken(string scope, CancellationToken cancellationToken, Action<string> observe);
#pragma warning restore CA1068
}
