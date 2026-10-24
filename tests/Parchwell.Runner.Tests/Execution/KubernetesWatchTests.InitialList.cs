using Parchwell.Runner.Run.Contracts;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed partial class KubernetesWatchTests
{
    private static readonly string[] InitialListArguments = ["get", "--raw",
        "/api/v1/namespaces/parchwell/pods?labelSelector=app.kubernetes.io%2Finstance%3Dsynthetic-a01-r1%2Cparchwell.io%2Frole%3Dloadgen"];

    // The raw list must retain the server's version so the first watch does not miss intervening pods.
    [Fact]
    public async Task Given_ServerPodList_When_EstablishingWatch_Then_UsesRawEncodedSelectorAndServerResourceVersion()
    {
        var initial = Step.Return(Event("BOOKMARK", "rv-2"));
        var running = new Step(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("watch_fixture_must_be_cancelled");
        });
        using var fixture = new WatchFixture(initial, running);
        await fixture.BindAsync();

        await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(fixture.Process.InitialListRequests);
        Assert.Equal("kubectl", request.FileName);
        Assert.Equal(InitialListArguments, request.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
        Assert.Equal(16777216, request.MaxOutputBytes);
        Assert.Equal(fixture.KubeconfigPath, request.Environment!["KUBECONFIG"]);
        Assert.Equal("list/rv&1", ResourceVersion(fixture.Process.WatchRequests.First()));
    }

    // kubectl's generic List can lose list metadata; an empty version must never authorize a watch.
    [Fact]
    public async Task Given_KubectlListWithEmptyResourceVersion_When_EstablishingWatch_Then_RejectsWithMissingResourceVersion()
    {
        using var fixture = new WatchFixture();
        fixture.Process.InitialListResult = new(0,
            "{\"apiVersion\":\"v1\",\"kind\":\"List\",\"metadata\":{\"resourceVersion\":\"\"},\"items\":[]}", string.Empty);
        await fixture.BindAsync();

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken));

        Assert.Equal("missing_resourceVersion", exception.Message);
        Assert.Single(fixture.Process.InitialListRequests);
        Assert.Empty(fixture.Process.WatchRequests);
    }

    // Switching transport must preserve the checked process boundary before parsing or watching.
    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task Given_InitialListProcessFailure_When_EstablishingWatch_Then_RejectsBeforeStartingWatch(int exitCode, bool timedOut)
    {
        using var fixture = new WatchFixture();
        fixture.Process.InitialListResult = new(exitCode, string.Empty, string.Empty, timedOut);
        await fixture.BindAsync();

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken));

        Assert.Equal("external_command_failed", exception.Message);
        Assert.Single(fixture.Process.InitialListRequests);
        Assert.Empty(fixture.Process.WatchRequests);
    }
}
#pragma warning restore CA1707
