using Xunit;

namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then test names.

public sealed class InfraEndpointTests
{
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
