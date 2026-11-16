using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaNativeMonitoringOptionsTests
{
    [Theory]
    [InlineData("native-monitoring", true)]
    [InlineData("verifier", false)]
    [InlineData("ledger", false)]
    [InlineData("claim-check", false)]
    [InlineData("Native-Monitoring", false)]
    [InlineData("native-monitoring/other", false)]
    [InlineData("", false)]
    public void Given_ConfiguredContainer_When_Validated_Then_OnlyDeclaredMonitoringDestinationIsAccepted(string container, bool expected)
    {
        var options = new KafkaNativeMonitoringOptions
        {
            BlobEndpoint = "https://synthetic.blob.core.windows.net/", BlobContainer = container,
            RunId = "run", Phase = "phase", ScenarioId = "scenario", ConfigurationId = "configuration",
            JobName = "job", PodUid = "pod", SourceCommit = new string('a', 40)
        };

        Assert.Equal(expected, options.IsValid(out string? failure));
        if (expected)
        {
            Assert.Null(failure);
            Assert.Equal("run", options.CreateIdentity().RunId);
        }
        else
        {
            Assert.NotNull(failure);
            Assert.Throws<InvalidOperationException>(() => options.CreateIdentity());
        }
    }
}
