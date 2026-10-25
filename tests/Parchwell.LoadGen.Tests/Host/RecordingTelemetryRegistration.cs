using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Telemetry;

namespace Parchwell.LoadGen.Tests.Host;

internal sealed class RecordingTelemetryRegistration
{
    public int RegistrationCount { get; private set; }
    public ParchwellTelemetryOptions? ConfiguredOptions { get; private set; }

    public void Register(IServiceCollection services, Action<ParchwellTelemetryOptions> configure)
    {
        var options = new ParchwellTelemetryOptions();
        configure(options);
        ConfiguredOptions = options;
        RegistrationCount++;
        services.AddSingleton<IOptions<ParchwellTelemetryOptions>>(Options.Create(options));
    }
}
