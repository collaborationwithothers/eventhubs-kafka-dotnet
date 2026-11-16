#pragma warning disable CA1707
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Parchwell.Enrichment.SeedF04;
using Parchwell.Enrichment.Tests.SeedF04;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Enrichment.Tests.NativeMonitoring;

public sealed class ValidationOnlyTests
{
    [Fact]
    public async Task Given_valid_seed_configuration_When_validation_host_starts_Then_no_client_or_exporter_is_registered()
    {
        using IHost host = Program.BuildHost(builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(SeedF04HostTests.Configuration());
        }, validationOnly: true);
        await host.StartAsync(TestContext.Current.CancellationToken);
        Assert.Null(host.Services.GetService<Func<IProducer<byte[], byte[]>>>());
        Assert.Null(host.Services.GetService<KafkaNativeMonitoringFactory>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.NotNull(host.Services.GetService<Microsoft.Extensions.Options.IOptions<SeedF04Options>>());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
