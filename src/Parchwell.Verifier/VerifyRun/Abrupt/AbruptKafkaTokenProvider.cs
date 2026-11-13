using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

/// <summary>
/// Acquires the abrupt-verify mode's Kafka tokens with a credential whose HTTP retry settings let one stalled
/// token request be abandoned and sent again inside the verifier's initial token timeout.
/// </summary>
/// <remarks>
/// Azure.Core abandons a request after <see cref="RetryOptions.NetworkTimeout"/>, 100 s by default, so a stalled
/// request outlives the 10 s initial token timeout and is never retried. The values are policy, not measured.
/// </remarks>
internal sealed class AbruptKafkaTokenProvider : IKafkaTokenProvider
{
    // Values chosen in decisions 6844950677 and 6845385609 on issue 404; they are not configuration.
    internal static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(2);
    internal const int MaxRetries = 1;
    internal static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(200);

    private readonly WorkloadIdentityCredential credential;

    /// <summary>Creates the provider for the registered workload identity.</summary>
    /// <param name="identity">The tenant, client and token file the credential signs in with.</param>
    /// <param name="transport">An optional transport, supplied only by tests.</param>
    internal AbruptKafkaTokenProvider(KafkaWorkloadIdentityOptions identity, HttpPipelineTransport? transport = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        CredentialOptions = CreateCredentialOptions(identity, transport);
        credential = new WorkloadIdentityCredential(CredentialOptions);
    }

    /// <summary>Gets the options the credential was created with.</summary>
    internal WorkloadIdentityCredentialOptions CredentialOptions { get; }

    /// <summary>Replaces the shared token provider registration with this provider for the abrupt-verify mode.</summary>
    internal static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IKafkaTokenProvider>();
        services.AddSingleton<IKafkaTokenProvider>(provider => new AbruptKafkaTokenProvider(
            provider.GetRequiredService<IOptions<KafkaWorkloadIdentityOptions>>().Value));
    }

    /// <inheritdoc />
    public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
        credential.GetToken(new TokenRequestContext([scope]), cancellationToken);

    private static WorkloadIdentityCredentialOptions CreateCredentialOptions(
        KafkaWorkloadIdentityOptions identity, HttpPipelineTransport? transport)
    {
        var options = new WorkloadIdentityCredentialOptions
        {
            TenantId = identity.TenantId,
            ClientId = identity.ClientId,
            TokenFilePath = identity.TokenFilePath
        };
        options.Retry.NetworkTimeout = NetworkTimeout;
        options.Retry.MaxRetries = MaxRetries;
        options.Retry.Mode = RetryMode.Fixed;
        options.Retry.Delay = Delay;
        if (transport is not null) options.Transport = transport;
        return options;
    }
}
