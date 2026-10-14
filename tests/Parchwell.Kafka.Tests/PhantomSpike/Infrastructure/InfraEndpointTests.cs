using Xunit;
using DotNet.Testcontainers.Configurations;
using Docker.DotNet.Models;

namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then test names.

public sealed class InfraEndpointTests
{
    [Fact]
    public void Given_PublishedFixturePorts_When_ConfiguringDockerCreation_Then_BindsEveryPortToLoopbackAndPreservesAllocation()
    {
        var control = new PortBinding { HostPort = string.Empty };
        var proxy = new PortBinding { HostIP = "0.0.0.0", HostPort = "49152" };
        var broker = new PortBinding { HostIP = "::", HostPort = "49153" };
        var parameters = new CreateContainerParameters
        {
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["8474/tcp"] = [control],
                    ["8666/tcp"] = [proxy],
                    ["9093/tcp"] = [broker]
                }
            }
        };

        BrokerEnvironment.BindPublishedPortsToLoopback(parameters);

        Assert.All(parameters.HostConfig.PortBindings.Values.SelectMany(bindings => bindings),
            binding => Assert.Equal("127.0.0.1", binding.HostIP));
        Assert.Equal(string.Empty, control.HostPort);
        Assert.Equal("49152", proxy.HostPort);
        Assert.Equal("49153", broker.HostPort);
    }

    [Fact]
    public void Given_DetectedDockerEndpoint_When_ConfiguringExperimentClient_Then_PreservesEndpointAndUsesApprovedBudget()
    {
        var original = new DockerEndpointAuthenticationConfiguration(new Uri("unix:///var/run/docker.sock"));
        var configuration = new ExperimentDockerAuthentication(original);
        var builder = configuration.GetDockerClientBuilder();

        Assert.Equal(original.Endpoint, configuration.Endpoint);
        Assert.Equal(original.Version, configuration.Version);
        using var client = builder.Build();
        Assert.Equal(TimeSpan.FromMinutes(30), client.Options.Timeout);
    }

    [Theory]
    [InlineData("0.0.0.0:8666")]
    [InlineData("[::]:8666")]
    public void Given_ExpectedWildcardOnProxyPort_When_ValidatingNormalizedListen_Then_AcceptsRoute(string listen)
    {
        Assert.True(BrokerEnvironment.IsExpectedWildcardListen(listen));
    }

    [Theory]
    [InlineData("0.0.0.0:8667")]
    [InlineData("[::]:8667")]
    [InlineData("127.0.0.1:8666")]
    [InlineData("[::1]:8666")]
    [InlineData("localhost:8666")]
    [InlineData(null)]
    public void Given_WrongPortOrSpecificAddress_When_ValidatingNormalizedListen_Then_RejectsRoute(string? listen)
    {
        Assert.False(BrokerEnvironment.IsExpectedWildcardListen(listen));
    }
}
