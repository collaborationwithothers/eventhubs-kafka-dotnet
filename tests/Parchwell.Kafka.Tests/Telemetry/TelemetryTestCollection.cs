using Xunit;

namespace Parchwell.Kafka.Tests.Telemetry;

[CollectionDefinition("Parchwell telemetry", DisableParallelization = true)]
#pragma warning disable CA1711 // xUnit collection definitions use the Collection suffix.
public sealed class TelemetryTestCollection : ICollectionFixture<TelemetryEnvironmentFixture>;
#pragma warning restore CA1711

public sealed class TelemetryEnvironmentFixture : IDisposable
{
    private readonly string? _statsbeat;
    private readonly string? _sdkStats;

    public TelemetryEnvironmentFixture()
    {
        _statsbeat = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_STATSBEAT_DISABLED");
        _sdkStats = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_SDKSTATS_DISABLED");
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_STATSBEAT_DISABLED", "true");
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_SDKSTATS_DISABLED", "true");
    }

    public void Dispose()
    {
        // The exporter caches SDK-stats enablement. Restoring the variables does not reset it;
        // a future test of enabled customer statistics must use a different process.
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_STATSBEAT_DISABLED", _statsbeat);
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_SDKSTATS_DISABLED", _sdkStats);
    }
}
