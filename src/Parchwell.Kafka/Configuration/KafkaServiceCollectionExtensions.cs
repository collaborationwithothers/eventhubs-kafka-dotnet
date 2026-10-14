using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Configuration;

/// <summary>Registers configuration mapping and startup validation for selected workload roles.</summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>Registers one role's named options and the concrete configuration factory.</summary>
    /// <param name="services">The application's service registrations.</param>
    /// <param name="role">The workload role whose options are being configured.</param>
    /// <param name="configure">Supplies that role's required caller inputs.</param>
    /// <returns>The service registrations for chaining.</returns>
    /// <exception cref="ArgumentNullException">Services or the configuration action is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The role is unknown.</exception>
    public static IServiceCollection AddParchwellKafka(this IServiceCollection services,
        KafkaClientRole role, Action<KafkaBaselineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        services.AddSingleton(new KafkaRoleRegistration(role));
        services.AddSingleton<IValidateOptions<KafkaBaselineOptions>>(new KafkaBaselineOptionsValidator(role));
        services.AddOptions<KafkaBaselineOptions>(role.ToString()).Configure(configure).ValidateOnStart();
        services.TryAddSingleton(provider => new KafkaConfigurationFactory(
            provider.GetRequiredService<IOptionsMonitor<KafkaBaselineOptions>>(),
            provider.GetServices<KafkaRoleRegistration>()));
        return services;
    }
}

internal sealed record KafkaRoleRegistration(KafkaClientRole Role);
