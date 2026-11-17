using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.IdleObservation;
using Parchwell.LoadGen.SingleAction;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class BatchNativePhaseIdentityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_SingleActionIdentity_When_PhaseDiffers_Then_ItIsRejected(bool matches)
    {
        var options = Parchwell.LoadGen.Tests.SingleAction.SingleActionCaptureTests.Options();
        var native = Native(options.RunId, options.ScenarioId, options.ConfigurationId, options.JobName, options.PodUid, options.SourceCommit, options.BlobEndpoint, matches);
        if (matches) SingleActionHost.ValidateNativeIdentity(native, options);
        else Assert.Equal("native_monitoring_identity_mismatch", Assert.Throws<InvalidOperationException>(() => SingleActionHost.ValidateNativeIdentity(native, options)).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_IdleIdentity_When_PhaseDiffers_Then_ItIsRejected(bool matches)
    {
        var options = Parchwell.LoadGen.Tests.IdleObservation.IdleObservationOptionsTests.Options();
        var native = Native(options.RunId, options.ScenarioId, options.ConfigurationId, options.JobName, options.PodUid, options.SourceCommit, options.BlobEndpoint, matches);
        if (matches) IdleObservationHost.ValidateNativeIdentity(native, options);
        else Assert.Equal("native_monitoring_identity_mismatch", Assert.Throws<InvalidOperationException>(() => IdleObservationHost.ValidateNativeIdentity(native, options)).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_ProbeV2Identity_When_PhaseDiffers_Then_ItIsRejected(bool matches)
    {
        var options = Parchwell.LoadGen.Tests.SingleActionV2.ProbeV2OptionsTests.Options();
        var native = Native(options.RunId, options.ScenarioId, options.ConfigurationId, options.JobName, options.PodUid, options.SourceCommit, "https://example.blob.core.windows.net/", matches);
        if (matches) ProbeV2Host.ValidateNativeIdentity(native, options);
        else Assert.Equal("native_monitoring_identity_mismatch", Assert.Throws<InvalidOperationException>(() => ProbeV2Host.ValidateNativeIdentity(native, options)).Message);
    }

    private static KafkaNativeMonitoringOptions Native(string run, string scenario, string configuration, string job, string pod, string commit, string endpoint, bool matches) => new()
    {
        RunId = run, ScenarioId = scenario, ConfigurationId = configuration, Phase = matches ? configuration : "C9",
        JobName = job, PodUid = pod, SourceCommit = commit, BlobEndpoint = endpoint, BlobContainer = "native-monitoring"
    };
}
