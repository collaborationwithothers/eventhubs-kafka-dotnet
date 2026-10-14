using Docker.DotNet;
using Docker.DotNet.Handler.Abstractions;
using DotNet.Testcontainers.Configurations;

namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

internal sealed class ExperimentDockerAuthentication(IDockerEndpointAuthenticationConfiguration original)
    : IDockerEndpointAuthenticationConfiguration
{
    public Version Version => original.Version;
    public Uri Endpoint => original.Endpoint;
    public IAuthProvider AuthProvider => original.AuthProvider;

    public DockerClientBuilder GetDockerClientBuilder(Guid sessionId = default)
    {
        // Supported public timeout hook; retain detected transport/authentication and TC headers.
        // The experiment cancellation token still bounds startup to its approved overall deadline.
        // https://github.com/testcontainers/Docker.DotNet/blob/1e4015a84fa48cbcfe9002ecc4e2cf14177edc2d/src/Docker.DotNet/DockerClientBuilder.cs#L180
        return original.GetDockerClientBuilder(sessionId).WithTimeout(TimeSpan.FromMinutes(30));
    }
}
