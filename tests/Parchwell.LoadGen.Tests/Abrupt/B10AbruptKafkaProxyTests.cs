using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// A killed producer's final request must outlive its client socket in the delaying proxy.
public sealed class B10AbruptKafkaProxyTests
{
    [Fact]
    public async Task Given_ClientClosedBeforeUpstreamConnects_When_ProxyDrainsRequest_Then_AllClientTailBytesReachBroker()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var proxy = new B10AbruptKafkaProxy();
        proxy.SetUpstream(listener.LocalEndpoint.ToString()!);
        byte[] expected = new byte[262144];
        new Random(404).NextBytes(expected);
        using (var client = new TcpClient())
        {
            string[] address = proxy.BootstrapServers.Split(':');
            await client.ConnectAsync(IPAddress.Loopback, int.Parse(address[1], System.Globalization.CultureInfo.InvariantCulture), timeout.Token);
            await client.GetStream().WriteAsync(expected, timeout.Token);
            client.Client.Shutdown(SocketShutdown.Send);
        }
        using TcpClient broker = await listener.AcceptTcpClientAsync(timeout.Token);
        using var received = new MemoryStream();
        await broker.GetStream().CopyToAsync(received, timeout.Token);
        Assert.Equal(expected, received.ToArray());
        Assert.Equal(1, proxy.AcceptedConnections);
    }
}
