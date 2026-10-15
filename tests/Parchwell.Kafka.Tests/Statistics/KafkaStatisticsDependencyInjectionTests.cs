using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects startup configuration because unbounded capture or completion must fail before a client starts.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsDependencyInjectionTests
{
    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(1, 1, 1, -1)]
    public void Given_MissingOrNonpositiveRequiredLimit_When_HostStarts_Then_ValidationFails(
        int maxCharacters,
        int queueCapacity,
        int maxBrokerSeries,
        int completionSeconds)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaStatisticsEvidenceSink, RecordingStatisticsSink>();
        services.AddParchwellKafkaStatistics(options =>
        {
            options.MaxSnapshotCharacters = maxCharacters;
            options.QueueCapacity = queueCapacity;
            options.MaxBrokerSeries = maxBrokerSeries;
            options.CompletionTimeout = completionSeconds < 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromSeconds(completionSeconds);
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public async Task Given_ValidLimitsAndRegisteredEvidenceSink_When_HostStarts_Then_FactoryCreatesSession()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaStatisticsEvidenceSink, RecordingStatisticsSink>();
        services.AddParchwellKafkaStatistics(options =>
        {
            options.MaxSnapshotCharacters = 16_384;
            options.QueueCapacity = 8;
            options.MaxBrokerSeries = 4;
            options.CompletionTimeout = TimeSpan.FromSeconds(5);
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
        KafkaStatisticsBridgeFactory factory = provider.GetRequiredService<KafkaStatisticsBridgeFactory>();
        await using KafkaStatisticsSession session = factory.Create(StatisticsTestSupport.ProducerIdentity());

        Assert.NotNull(session);
    }

    [Fact]
    public void Given_NoEvidenceSinkRegistration_When_ResolvingFactory_Then_RequiresCallerOwnedSink()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafkaStatistics(options =>
        {
            options.MaxSnapshotCharacters = 16_384;
            options.QueueCapacity = 8;
            options.MaxBrokerSeries = 4;
            options.CompletionTimeout = TimeSpan.FromSeconds(5);
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }
}
