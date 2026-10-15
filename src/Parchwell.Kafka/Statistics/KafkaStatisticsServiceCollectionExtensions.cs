using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Parchwell.Kafka.Statistics;

/// <summary>Registers the runtime services used by the Kafka statistics bridge.</summary>
public static class KafkaStatisticsServiceCollectionExtensions
{
    /// <summary>Registers the statistics bridge and validates its required limits when the host starts.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">The required capture and completion limits.</param>
    /// <returns>The same service collection for registration chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddParchwellKafkaStatistics(
        this IServiceCollection services,
        Action<KafkaStatisticsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<KafkaStatisticsOptions>()
            .Configure(configure)
            .Validate(options => options.IsValid(out _), "Kafka statistics options must contain positive finite limits.")
            .Validate<IKafkaStatisticsEvidenceSink>((_, sink) => sink is not null,
                "Register an IKafkaStatisticsEvidenceSink before the host starts.")
            .ValidateOnStart();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton(_ => new KafkaStatisticsMetrics());
        services.TryAddSingleton(provider => new KafkaStatisticsBridgeFactory(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaStatisticsOptions>>().Value,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IKafkaStatisticsEvidenceSink>(),
            provider.GetRequiredService<KafkaStatisticsMetrics>(),
            // librdkafka v2.16.0 stats use CLOCK_MONOTONIC on Linux:
            // https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdtime.h#L85-L111 and
            // https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c#L1892-L1915.
            // .NET 10 TimeProvider.System uses Stopwatch on the Linux CLOCK_MONOTONIC path:
            // https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Common/src/System/TimeProvider.cs#L83-L97,
            // https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Unix.cs
            // and https://github.com/dotnet/runtime/blob/v10.0.13/src/native/minipal/time.c.
            ReferenceEquals(provider.GetRequiredService<TimeProvider>(), TimeProvider.System) && OperatingSystem.IsLinux()));
        return services;
    }
}
