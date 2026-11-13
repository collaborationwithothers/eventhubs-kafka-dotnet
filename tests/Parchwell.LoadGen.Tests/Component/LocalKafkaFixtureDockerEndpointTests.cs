using DotNet.Testcontainers.Configurations;
using Xunit;

namespace Parchwell.LoadGen.Tests.Component;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class LocalKafkaFixtureDockerEndpointTests
{
    [Fact]
    public void Given_NoDetectedDockerEndpoint_When_RequiringEndpoint_Then_ThrowsDockerNotReachable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => LocalKafkaFixture.RequireDockerEndpoint(null));

        Assert.Equal("Docker daemon not reachable; this test is Tier=Docker", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Given_DetectedDockerEndpoint_When_RequiringEndpoint_Then_ReturnsSameConfiguration()
    {
        // Declared as the interface so the struct is boxed once and reference identity holds.
        IDockerEndpointAuthenticationConfiguration detected =
            new DockerEndpointAuthenticationConfiguration(new Uri("unix:///var/run/docker.sock"));

        Assert.Same(detected, LocalKafkaFixture.RequireDockerEndpoint(detected));
    }
}
