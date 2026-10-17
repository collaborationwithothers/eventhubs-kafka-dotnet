using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Authentication;

/// <summary>Registers the opt-in workload identity authentication services for Kafka clients.</summary>
public static class KafkaAuthenticationServiceCollectionExtensions
{
    /// <summary>Registers workload identity token acquisition and validates its options on host start.</summary>
    /// <param name="services">The application's service registrations.</param>
    /// <param name="configure">Supplies the tenant, credential application client ID and projected token file path.</param>
    /// <returns>The service collection for registration chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddParchwellKafkaAuthentication(
        this IServiceCollection services,
        Action<KafkaWorkloadIdentityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IValidateOptions<KafkaWorkloadIdentityOptions>, KafkaWorkloadIdentityOptionsValidator>();
        services.AddOptions<KafkaWorkloadIdentityOptions>()
            .Configure(configure)
            .ValidateOnStart();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton(_ => new KafkaAuthenticationMetrics());
        services.TryAddSingleton<IKafkaTokenProvider>(provider =>
        {
            KafkaWorkloadIdentityOptions options = provider
                .GetRequiredService<IOptions<KafkaWorkloadIdentityOptions>>().Value;
            // WorkloadIdentityCredential lives in Azure.Core at the central package pin:
            // https://github.com/Azure/azure-sdk-for-net/blob/84e76f0ebc1bb8ce92b5c28b2eddfa6016cfdc4a/sdk/core/Azure.Core/src/Identity/Credentials/WorkloadIdentityCredential.cs
            var credential = new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
            {
                TenantId = options.TenantId,
                ClientId = options.ClientId,
                TokenFilePath = options.TokenFilePath
            });
            return new WorkloadIdentityTokenProvider(credential);
        });
        services.TryAddSingleton(provider => new KafkaAuthenticationFactory(
            provider.GetRequiredService<IOptionsMonitor<Configuration.KafkaBaselineOptions>>(),
            provider.GetRequiredService<IOptions<KafkaWorkloadIdentityOptions>>(),
            provider.GetRequiredService<IKafkaTokenProvider>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<KafkaAuthenticationMetrics>()));
        return services;
    }
}
